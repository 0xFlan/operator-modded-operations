using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mirror;
using OperatorModAPI;
using OperatorModdedOperations;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class CerberusNativeTabFix
{
    private static string SerializePeerRuntimePayload<T>(T value)
    {
        if (value == null)
            throw new InvalidOperationException("peer runtime payload is null");
        string json = JsonSerializer.Serialize(value);
        if (json.Length == 0 || json.Length > PvpAgreementMaxReasonString)
            throw new InvalidOperationException("peer runtime payload length is invalid");
        return json;
    }

    private static T ParsePeerRuntimePayload<T>(string payload, string label)
    {
        if (string.IsNullOrEmpty(payload) || payload.Length > PvpAgreementMaxReasonString)
            throw new InvalidOperationException(label + " payload length is invalid");
        T value = JsonSerializer.Deserialize<T>(payload);
        if (value == null || !string.Equals(
                SerializePeerRuntimePayload(value),
                payload,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(label + " payload is not canonical");
        }
        return value;
    }

    private static string ComputePeerRuntimeDigest(string domain, string canonical)
    {
        using SHA256 sha = SHA256.Create();
        return ToLowerHex(sha.ComputeHash(Encoding.UTF8.GetBytes(
            domain + "\n" + (canonical ?? string.Empty))));
    }

    private static int QuantizePeerMillimetres(float value)
    {
        if (!float.IsFinite(value) || value < -100000f || value > 100000f)
            throw new InvalidOperationException("peer runtime position is non-finite/out of bounds");
        return checked((int)Math.Round(value * 1000f));
    }

    private static int QuantizePeerYaw(float value)
    {
        if (!float.IsFinite(value))
            throw new InvalidOperationException("peer runtime yaw is non-finite");
        float normalized = Mathf.Repeat(value, 360f);
        return checked((int)Math.Round(normalized * 1000f));
    }

    private static string ComputePeerPlayerAssignmentDigest(
        PvpPeerIdentity identity,
        ulong epoch,
        uint playerMasterNetId,
        uint playerNetId,
        Transform marker)
    {
        if (identity == null || epoch == 0 || playerMasterNetId == 0 ||
            playerNetId == 0 || marker == null || string.IsNullOrEmpty(marker.name) ||
            marker.name.Length > 128 || marker.name.Contains('|'))
        {
            throw new InvalidOperationException("player placement identity is incomplete");
        }
        Vector3 position = marker.position;
        string canonical = identity.Digest + "|" +
            epoch.ToString(CultureInfo.InvariantCulture) + "|" +
            playerMasterNetId.ToString(CultureInfo.InvariantCulture) + "|" +
            playerNetId.ToString(CultureInfo.InvariantCulture) + "|" + marker.name + "|" +
            QuantizePeerMillimetres(position.x).ToString(CultureInfo.InvariantCulture) + "|" +
            QuantizePeerMillimetres(position.y).ToString(CultureInfo.InvariantCulture) + "|" +
            QuantizePeerMillimetres(position.z).ToString(CultureInfo.InvariantCulture) + "|" +
            QuantizePeerYaw(marker.eulerAngles.y).ToString(CultureInfo.InvariantCulture);
        return ComputePeerRuntimeDigest("operator-peer-player-placement-v1", canonical);
    }

    private static bool SamePeerRuntimeOwner(
        PeerRuntimeOwnerReceipt left,
        PeerRuntimeOwnerReceipt right)
    {
        return left != null && right != null && left.OwnerNetId == right.OwnerNetId &&
            left.AssetId == right.AssetId && left.LocalGeneration == right.LocalGeneration &&
            string.Equals(left.SceneContractDigest, right.SceneContractDigest,
                StringComparison.Ordinal);
    }

    private static bool SamePeerPlayerPlacement(
        PeerPlayerPlacement left,
        PeerPlayerPlacement right)
    {
        return left != null && right != null &&
            left.PlayerMasterNetId == right.PlayerMasterNetId &&
            left.PlayerNetId == right.PlayerNetId &&
            string.Equals(left.MarkerName, right.MarkerName, StringComparison.Ordinal) &&
            string.Equals(left.AssignmentDigest, right.AssignmentDigest,
                StringComparison.Ordinal);
    }

    private bool HandlePeerRuntimeClientControl(
        PvpAgreementMessageKind kind,
        RemotePvpAgreement remote,
        string payload,
        ulong epoch)
    {
        if (kind != PvpAgreementMessageKind.RuntimeOwnerManifest &&
            kind != PvpAgreementMessageKind.PlacePlayer &&
            kind != PvpAgreementMessageKind.PopulationManifest &&
            kind != PvpAgreementMessageKind.BeginCommit)
        {
            return false;
        }
        if (remote == null || epoch == 0 || epoch != remote.RequestedSceneGenerationEpoch ||
            !remote.ContentCommitted || !IsRemoteSceneReadyForCurrentRequest(remote))
        {
            throw new InvalidOperationException(
                "host runtime control is outside the current prepared scene generation");
        }
        ActiveMapOperation operation = activeOperation;
        if (operation == null || !OperationMatchesPvpIdentity(operation, remote.Identity))
            throw new InvalidOperationException("host runtime control lost operation ownership");

        if (kind == PvpAgreementMessageKind.RuntimeOwnerManifest)
        {
            PeerRuntimeOwnerReceipt receipt = ParsePeerRuntimePayload<PeerRuntimeOwnerReceipt>(
                payload, "runtime-owner");
            if (receipt.OwnerNetId == 0 || receipt.AssetId != operation.BootstrapAssetId ||
                receipt.LocalGeneration != remote.LocalSceneGeneration ||
                !IsLowercasePvpSha256(receipt.SceneContractDigest) ||
                !string.Equals(receipt.SceneContractDigest,
                    remote.LocalSceneContractDigest, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "runtime-owner receipt does not match the prepared scene/template");
            }
            if (remote.RuntimeOwner != null &&
                !SamePeerRuntimeOwner(remote.RuntimeOwner, receipt))
            {
                throw new InvalidOperationException(
                    "runtime-owner receipt changed within one immutable epoch");
            }
            remote.RuntimeOwner = receipt;
            remote.RuntimeBarrierDeadlineTimestamp = DeadlineAfter(
                PeerRuntimeReadyTimeoutSeconds);
            TrySendRemotePeerRuntimeReady(operation, remote);
            return true;
        }

        if (kind == PvpAgreementMessageKind.PlacePlayer)
        {
            PeerPlayerPlacement placement = ParsePeerRuntimePayload<PeerPlayerPlacement>(
                payload, "player-placement");
            placement.ConnectionId = 0;
            placement.RetryTimestamp = 0;
            placement.ReceiptReceived = false;
            if (placement.PlayerMasterNetId == 0 || placement.PlayerNetId == 0 ||
                string.IsNullOrEmpty(placement.MarkerName) ||
                !IsLowercasePvpSha256(placement.AssignmentDigest))
            {
                throw new InvalidOperationException("player-placement receipt is incomplete");
            }
            if (remote.PendingPlayerPlacement != null &&
                !SamePeerPlayerPlacement(remote.PendingPlayerPlacement, placement))
            {
                throw new InvalidOperationException(
                    "host changed the owner placement assignment within one epoch");
            }
            remote.PendingPlayerPlacement = placement;
            remote.RuntimeBarrierDeadlineTimestamp = DeadlineAfter(
                PeerPlayerReadyTimeoutSeconds);
            ProcessRemotePeerPlayerPlacement(operation, remote);
            return true;
        }

        if (kind == PvpAgreementMessageKind.PopulationManifest)
        {
            if (operation.Operation.Mode != ModdedOperationMode.PlayerVersusEnvironment)
                throw new InvalidOperationException("PVP received a PVE population manifest");
            PeerPvePopulationManifest manifest =
                ParsePeerRuntimePayload<PeerPvePopulationManifest>(payload, "PVE population");
            ValidatePeerPopulationManifestShape(manifest, remote.Identity.RequestedEnemies);
            if (remote.PopulationManifest != null && !string.Equals(
                    SerializePeerRuntimePayload(remote.PopulationManifest),
                    SerializePeerRuntimePayload(manifest), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "host changed the immutable PVE population manifest");
            }
            remote.PopulationManifest = manifest;
            remote.RuntimeBarrierDeadlineTimestamp = DeadlineAfter(
                PeerPopulationReadyTimeoutSeconds);
            TryValidateAndAcknowledgeRemotePvePopulation(operation, remote);
            return true;
        }

        if (!remote.PlayerReadySent || operation.Operation.Mode !=
                ModdedOperationMode.PlayerVersusEnvironment ||
            remote.PopulationManifest == null || !remote.PopulationReadySent ||
            !string.Equals(payload, remote.PopulationManifest.Digest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "BeginCommit arrived before the owner/player/population barriers");
        }
        if (remote.BeginCommitReceived)
            return true;
        remote.BeginCommitReceived = true;
        remote.RuntimeBarrierDeadlineTimestamp = 0;
        operation.GameplayBeginCommitted = true;
        LogPvePeerAgreementEvidence(
            "pve-peer-begin-commit",
            remote.Identity,
            operation,
            "remote",
            epoch,
            "populationDigest=" + FrameworkEvidence.Encode(
                remote.PopulationManifest.Digest) +
            "|playerReady=true|populationReady=true");
        log.LogInfo("Remote PVE peer accepted BeginGameplay after exact runtime, " +
            "owner-placement, and AI-population receipts: epoch=" + epoch + ".");
        return true;
    }

    private bool HandlePeerRuntimeServerControl(
        NetworkConnection connection,
        PvpAgreementMessageKind kind,
        HostPvpAgreement host,
        string payload,
        ulong epoch)
    {
        if (kind != PvpAgreementMessageKind.RuntimeReady &&
            kind != PvpAgreementMessageKind.PlayerReady &&
            kind != PvpAgreementMessageKind.PopulationReady)
        {
            return false;
        }
        if (host == null || connection == null || epoch == 0 ||
            epoch != host.SceneGenerationEpoch || !connection.isReady ||
            !host.RequiredConnectionIds.Contains(connection.connectionId))
        {
            throw new InvalidOperationException(
                "remote runtime receipt is outside the frozen current epoch");
        }
        ActiveMapOperation operation = activeOperation;
        if (operation == null || !OperationMatchesPvpIdentity(operation, host.Identity))
            throw new InvalidOperationException("remote runtime receipt lost operation ownership");

        if (kind == PvpAgreementMessageKind.RuntimeReady)
        {
            PeerRuntimeOwnerReceipt receipt = ParsePeerRuntimePayload<PeerRuntimeOwnerReceipt>(
                payload, "runtime-ready");
            if (receipt.OwnerNetId != operation.BootstrapSpawnedNetId ||
                receipt.AssetId != operation.BootstrapAssetId ||
                receipt.LocalGeneration != host.SceneGenerationEpoch ||
                !string.Equals(receipt.SceneContractDigest,
                    host.LocalSceneContractDigest, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "remote runtime-ready receipt does not match the spawned owner");
            }
            bool firstRuntimeReady = host.RuntimeReadyConnectionIds.Add(
                connection.connectionId);
            host.RuntimeBarrierDeadlineTimestamp = DeadlineAfter(
                PeerPlayerReadyTimeoutSeconds);
            if (firstRuntimeReady)
            {
                LogPvePeerAgreementEvidence(
                    "pve-peer-runtime-ready",
                    host.Identity,
                    operation,
                    "host",
                    epoch,
                    "connectionId=" + FrameworkEvidence.Number(
                        connection.connectionId) +
                    "|ownerNetId=" + FrameworkEvidence.Number(
                        receipt.OwnerNetId) +
                    "|assetId=" + FrameworkEvidence.Number(
                        receipt.AssetId));
            }
            log.LogInfo("Peer runtime-ready receipt accepted: connection=" +
                connection.connectionId + ", ready=" +
                host.RuntimeReadyConnectionIds.Count + "/" +
                host.RequiredConnectionIds.Count + ".");
            return true;
        }

        if (kind == PvpAgreementMessageKind.PlayerReady)
        {
            PeerPlayerPlacement receipt = ParsePeerRuntimePayload<PeerPlayerPlacement>(
                payload, "player-ready");
            if (!host.PlayerPlacementByConnectionId.TryGetValue(
                    connection.connectionId, out PeerPlayerPlacement assignment) ||
                !SamePeerPlayerPlacement(assignment, receipt))
            {
                throw new InvalidOperationException(
                    "remote PlayerReady does not match its frozen owner assignment");
            }
            bool firstPlayerReady = !assignment.ReceiptReceived;
            assignment.ReceiptReceived = true;
            if (firstPlayerReady)
            {
                LogPvePeerAgreementEvidence(
                    "pve-peer-player-ready",
                    host.Identity,
                    operation,
                    "host",
                    epoch,
                    "connectionId=" + FrameworkEvidence.Number(
                        connection.connectionId) +
                    "|playerMasterNetId=" + FrameworkEvidence.Number(
                        receipt.PlayerMasterNetId) +
                    "|playerNetId=" + FrameworkEvidence.Number(
                        receipt.PlayerNetId) +
                    "|marker=" + FrameworkEvidence.Encode(receipt.MarkerName) +
                    "|assignmentDigest=" + FrameworkEvidence.Encode(
                        receipt.AssignmentDigest));
            }
            return true;
        }

        if (host.PopulationManifest == null ||
            !string.Equals(payload, host.PopulationManifest.Digest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "remote PopulationReady does not match the authoritative manifest");
        }
        bool firstPopulationReady = host.PopulationReadyConnectionIds.Add(
            connection.connectionId);
        if (firstPopulationReady)
        {
            LogPvePeerAgreementEvidence(
                "pve-peer-population-ready",
                host.Identity,
                operation,
                "host",
                epoch,
                "connectionId=" + FrameworkEvidence.Number(
                    connection.connectionId) +
                "|count=" + FrameworkEvidence.Number(
                    host.PopulationManifest.Records.Count) +
                "|populationDigest=" + FrameworkEvidence.Encode(
                    host.PopulationManifest.Digest));
        }
        log.LogInfo("PVE population-ready receipt accepted: connection=" +
            connection.connectionId + ", ready=" +
            host.PopulationReadyConnectionIds.Count + "/" +
            host.RequiredConnectionIds.Count + ".");
        return true;
    }

    private void PublishPeerRuntimeOwnerManifest(
        ActiveMapOperation operation,
        HostPvpAgreement host)
    {
        if (operation == null || host == null || !NetworkServer.active ||
            operation.BootstrapSpawnedNetId == 0 || operation.BootstrapAssetId == 0 ||
            host.SceneGenerationEpoch == 0 ||
            !IsLowercasePvpSha256(host.LocalSceneContractDigest))
        {
            return;
        }
        bool firstPublication = !host.RuntimeOwnerManifestSent;
        var receipt = new PeerRuntimeOwnerReceipt
        {
            OwnerNetId = operation.BootstrapSpawnedNetId,
            AssetId = operation.BootstrapAssetId,
            LocalGeneration = host.SceneGenerationEpoch,
            SceneContractDigest = host.LocalSceneContractDigest
        };
        BroadcastHostPvpControl(
            PvpAgreementMessageKind.RuntimeOwnerManifest,
            SerializePeerRuntimePayload(receipt),
            host.SceneGenerationEpoch);
        host.RuntimeOwnerManifestSent = true;
        host.RuntimeOwnerRetryTimestamp = DeadlineAfter(PeerRuntimeRetrySeconds);
        if (firstPublication)
        {
            LogPvePeerAgreementEvidence(
                "pve-peer-runtime-owner",
                host.Identity,
                operation,
                "host",
                host.SceneGenerationEpoch,
                "ownerNetId=" + FrameworkEvidence.Number(receipt.OwnerNetId) +
                "|assetId=" + FrameworkEvidence.Number(receipt.AssetId) +
                "|sceneContractDigest=" + FrameworkEvidence.Encode(
                    receipt.SceneContractDigest));
        }
        if (host.RuntimeBarrierDeadlineTimestamp == 0)
            host.RuntimeBarrierDeadlineTimestamp = DeadlineAfter(
                PeerRuntimeReadyTimeoutSeconds);
    }

    private void TrySendRemotePeerRuntimeReady(
        ActiveMapOperation operation,
        RemotePvpAgreement remote)
    {
        if (operation == null || remote == null || remote.RuntimeReadySent ||
            remote.RuntimeOwner == null || !remote.NativeOwnerAdopted ||
            !remote.NativeReadinessInitialized || operation.BootstrapIdentity == null ||
            operation.BootstrapIdentity.netId != remote.RuntimeOwner.OwnerNetId ||
            operation.BootstrapIdentity.assetId != remote.RuntimeOwner.AssetId)
        {
            return;
        }
        SendRemotePvpControl(
            PvpAgreementMessageKind.RuntimeReady,
            SerializePeerRuntimePayload(remote.RuntimeOwner),
            sceneGenerationEpoch: remote.RequestedSceneGenerationEpoch);
        remote.RuntimeReadySent = true;
        remote.RuntimeBarrierDeadlineTimestamp = DeadlineAfter(
            PeerPlayerReadyTimeoutSeconds);
        LogPvePeerAgreementEvidence(
            "pve-peer-runtime-ready",
            remote.Identity,
            operation,
            "remote",
            remote.RequestedSceneGenerationEpoch,
            "ownerNetId=" + FrameworkEvidence.Number(
                remote.RuntimeOwner.OwnerNetId) +
            "|assetId=" + FrameworkEvidence.Number(
                remote.RuntimeOwner.AssetId));
        log.LogInfo("Remote peer acknowledged the exact pre-deserialization-safe " +
            "native owner and readiness lifecycle: netId=" +
            remote.RuntimeOwner.OwnerNetId + ".");
    }

    private bool TryIssueRemotePeerPlayerPlacement(
        ActiveMapOperation operation,
        PlayerMaster player,
        PlayerNetworking spawned,
        Transform marker,
        out string error)
    {
        error = string.Empty;
        HostPvpAgreement host = hostPvpAgreement;
        if (operation == null || player == null || spawned == null || marker == null ||
            host == null || !operation.PeerAgreementRequired ||
            !OperationMatchesPvpIdentity(operation, host.Identity))
        {
            error = "remote owner placement has no matching peer session";
            return false;
        }
        NetworkConnectionToClient connection;
        NetworkIdentity masterIdentity = player.GetComponent<NetworkIdentity>();
        NetworkIdentity playerIdentity = spawned.GetComponent<NetworkIdentity>();
        try { connection = player.connectionToClient; }
        catch { connection = null; }
        if (connection == null || masterIdentity == null || playerIdentity == null ||
            masterIdentity.netId == 0 || playerIdentity.netId == 0 ||
            !host.RequiredConnections.TryGetValue(
                connection.connectionId, out NetworkConnectionToClient frozen) ||
            !SamePvpNetworkConnection(frozen, connection) || !connection.isReady)
        {
            error = "remote player has no exact frozen Ready owner connection/netIds";
            return false;
        }
        string assignmentDigest = ComputePeerPlayerAssignmentDigest(
            host.Identity,
            host.SceneGenerationEpoch,
            masterIdentity.netId,
            playerIdentity.netId,
            marker);
        var assignment = new PeerPlayerPlacement
        {
            ConnectionId = connection.connectionId,
            PlayerMasterNetId = masterIdentity.netId,
            PlayerNetId = playerIdentity.netId,
            MarkerName = marker.name,
            AssignmentDigest = assignmentDigest
        };
        if (host.PlayerPlacementByConnectionId.TryGetValue(
                connection.connectionId, out PeerPlayerPlacement existing))
        {
            if (!SamePeerPlayerPlacement(existing, assignment))
            {
                error = "remote owner placement assignment changed within one epoch";
                return false;
            }
            assignment = existing;
        }
        else
        {
            host.PlayerPlacementByConnectionId.Add(connection.connectionId, assignment);
        }
        if (!assignment.ReceiptReceived &&
            (assignment.RetryTimestamp == 0 || DeadlineExpired(assignment.RetryTimestamp)))
        {
            SendPvpControl(
                connection,
                PvpAgreementMessageKind.PlacePlayer,
                host.Identity,
                SerializePeerRuntimePayload(assignment),
                host.SceneGenerationEpoch);
            assignment.RetryTimestamp = DeadlineAfter(PeerRuntimeRetrySeconds);
        }
        return true;
    }

    private void ProcessRemotePeerPlayerPlacement(
        ActiveMapOperation operation,
        RemotePvpAgreement remote)
    {
        PeerPlayerPlacement placement = remote?.PendingPlayerPlacement;
        if (operation == null || placement == null || remote.PlayerReadySent)
            return;
        Scene scene = FindLoadedSceneByHandle(operation.SceneHandle);
        if (!scene.IsValid() || !scene.isLoaded)
            return;
        List<Transform> matches = FindStandalonePlayerMarkers(
                scene, operation.Operation.Mode)
            .Where(marker => string.Equals(
                marker.name, placement.MarkerName, StringComparison.Ordinal))
            .ToList();
        if (matches.Count != 1)
        {
            FailRemotePvpAgreement(
                "owner placement marker is missing or ambiguous", true);
            return;
        }
        Transform marker = matches[0];
        string expectedDigest = ComputePeerPlayerAssignmentDigest(
            remote.Identity,
            remote.RequestedSceneGenerationEpoch,
            placement.PlayerMasterNetId,
            placement.PlayerNetId,
            marker);
        if (!string.Equals(expectedDigest, placement.AssignmentDigest,
                StringComparison.Ordinal))
        {
            FailRemotePvpAgreement("owner placement marker digest changed", true);
            return;
        }
        PlayerMaster player = FindPlayerMasterByNetId(placement.PlayerMasterNetId);
        PlayerNetworking spawned = null;
        bool playerOwned = false;
        if (player != null)
        {
            try
            {
                spawned = player.PlayerSpawnedObject;
                playerOwned = player.isOwned;
            }
            catch { }
        }
        NetworkIdentity spawnedIdentity = spawned == null
            ? null
            : spawned.GetComponent<NetworkIdentity>();
        bool spawnedOwned = false;
        try { spawnedOwned = spawned?.isOwned == true || spawned?.isLocalPlayer == true; }
        catch { }
        if (player == null || !playerOwned || spawned == null || spawnedIdentity == null ||
            spawnedIdentity.netId != placement.PlayerNetId || !spawnedOwned)
        {
            return;
        }
        if (!TryValidatePeerPlayerNativeNetworkContract(
                spawned,
                requireOwnedWeaponAuthority: true,
                out int ownedWeaponCount,
                out _))
        {
            // The retail loadout and corrective ownership command can settle
            // after the player root appears. Do not acknowledge placement until
            // the exact native transform/animation/health graph and every
            // populated weapon slot belong to this owning client.
            remote.PlayerReadyStableFrames = 0;
            remote.PlayerReadyLastFrame = -1;
            return;
        }
        try
        {
            player.LastSpawnPoint = marker;
            player.spawnRotation = marker.eulerAngles;
        }
        catch { }
        Vector3 target = marker.position + Vector3.up * 0.25f;
        if (!IsPlayerAtPackageSpawn(
                spawned,
                target,
                operation.SceneHandle,
                true,
                out string state))
        {
            if (!operation.PlayerMoveRequestFrames.TryGetValue(
                    unchecked((int)placement.PlayerMasterNetId), out int lastFrame) ||
                Time.frameCount >= lastFrame + 120)
            {
                if (GameManager.instance == null)
                    return;
                GameManager.instance.StartCoroutine(
                    GameManager.instance.MovePlayerToSpawn(target, marker.rotation));
                operation.PlayerMoveRequestFrames[
                    unchecked((int)placement.PlayerMasterNetId)] = Time.frameCount;
                log.LogInfo("Remote owner invoked shipped MovePlayerToSpawn for " +
                    "assignment=" + placement.AssignmentDigest + ", priorState=" +
                    state + ".");
            }
            remote.PlayerReadyStableFrames = 0;
            remote.PlayerReadyLastFrame = -1;
            return;
        }
        if (remote.PlayerReadyLastFrame == Time.frameCount - 1)
            remote.PlayerReadyStableFrames++;
        else
            remote.PlayerReadyStableFrames = 1;
        remote.PlayerReadyLastFrame = Time.frameCount;
        if (remote.PlayerReadyStableFrames < 15)
            return;
        SendRemotePvpControl(
            PvpAgreementMessageKind.PlayerReady,
            SerializePeerRuntimePayload(placement),
            sceneGenerationEpoch: remote.RequestedSceneGenerationEpoch);
        remote.PlayerReadySent = true;
        remote.RuntimeBarrierDeadlineTimestamp = DeadlineAfter(
            PeerPopulationReadyTimeoutSeconds);
        LogPvePeerAgreementEvidence(
            "pve-peer-player-ready",
            remote.Identity,
            operation,
            "remote",
            remote.RequestedSceneGenerationEpoch,
            "playerMasterNetId=" + FrameworkEvidence.Number(
                placement.PlayerMasterNetId) +
            "|playerNetId=" + FrameworkEvidence.Number(
                placement.PlayerNetId) +
            "|marker=" + FrameworkEvidence.Encode(placement.MarkerName) +
            "|assignmentDigest=" + FrameworkEvidence.Encode(
                placement.AssignmentDigest) +
            "|nativeNetworkContract=player-v1|ownedWeapons=" +
                FrameworkEvidence.Number(ownedWeaponCount));
        log.LogInfo("Remote owner placement passed for 15 consecutive frames: " +
            "marker=" + marker.name + ", playerNetId=" + placement.PlayerNetId +
            ", nativeNetworkContract=player-v1, ownedWeapons=" +
            ownedWeaponCount + ".");
    }

    private static PlayerMaster FindPlayerMasterByNetId(uint netId)
    {
        if (netId == 0)
            return null;
        PlayerMaster[] players;
        try { players = Resources.FindObjectsOfTypeAll<PlayerMaster>(); }
        catch { return null; }
        PlayerMaster match = null;
        foreach (PlayerMaster player in players)
        {
            NetworkIdentity identity = player == null
                ? null
                : player.GetComponent<NetworkIdentity>();
            if (identity == null || identity.netId != netId)
                continue;
            if (match != null && match != player)
                return null;
            match = player;
        }
        return match;
    }

    private static bool TryValidatePeerPlayerNativeNetworkContract(
        PlayerNetworking player,
        bool requireOwnedWeaponAuthority,
        out int ownedWeaponCount,
        out string error)
    {
        ownedWeaponCount = 0;
        error = string.Empty;
        if (player == null || player.gameObject == null ||
            !player.gameObject.activeInHierarchy)
        {
            error = "player network root is unavailable/inactive";
            return false;
        }
        GameObject root = player.gameObject;
        NetworkIdentity identity = root.GetComponent<NetworkIdentity>();
        Health health = root.GetComponent<Health>();
        Component animator = root.GetComponents<Component>().FirstOrDefault(
            item => item != null && string.Equals(
                item.GetType().Name,
                "NetworkAnimatorSmooth",
                StringComparison.Ordinal));
        NetworkBehaviour networkAnimator = animator as NetworkBehaviour;
        Behaviour animatorBehaviour = animator as Behaviour;
        global::Smooth.SmoothSyncMirror[] smooth =
            root.GetComponents<global::Smooth.SmoothSyncMirror>();
        if (identity == null || identity.gameObject != root || identity.netId == 0 ||
            player.netIdentity != identity || health == null ||
            health.netIdentity != identity || animator == null ||
            networkAnimator == null || networkAnimator.netIdentity != identity ||
            animatorBehaviour == null || !animatorBehaviour.enabled ||
            smooth == null || smooth.Length != 1 ||
            smooth[0] == null || smooth[0].netIdentity != identity ||
            !smooth[0].enabled || !PeerIdentityContainsBehaviour(identity, player) ||
            !PeerIdentityContainsBehaviour(identity, health) ||
            !PeerIdentityContainsBehaviour(identity, networkAnimator) ||
            !PeerIdentityContainsBehaviour(identity, smooth[0]))
        {
            error = "player native NetworkIdentity/SmoothSync/Health/Animator closure is incomplete";
            return false;
        }
        if (!requireOwnedWeaponAuthority)
            return true;

        bool rootOwned;
        try { rootOwned = player.isOwned || player.isLocalPlayer || identity.isOwned; }
        catch { rootOwned = false; }
        if (!rootOwned)
        {
            error = "player root is not owned by this client";
            return false;
        }

        var slots = new (int NetId, Component Weapon)[]
        {
            (player.PrimaryWeaponID, player.primaryWeapon),
            (player.SecondPrimaryWeaponID, player.secondPrimaryWeapon),
            (player.SecondaryWeaponID, player.SecondaryWeaponID == 0
                ? null
                : player.secondaryWeapon),
            (player.SpecialPurposeWeaponID, player.specialPurposeWeapon),
            (player.CurrentGrenadeWeaponID, player.GrenadeV2Weapon == null
                ? null
                : player.GrenadeV2Weapon.GetComponent<GrenadeV2>())
        };
        foreach ((int syncedNetId, Component weapon) in slots)
        {
            if (syncedNetId == 0 && weapon == null)
                continue;
            if (syncedNetId == 0 || weapon == null)
            {
                error = "a populated native weapon slot has incomplete identity state";
                return false;
            }
            NetworkIdentity weaponIdentity = weapon.GetComponent<NetworkIdentity>();
            bool weaponOwned;
            try { weaponOwned = weaponIdentity?.isOwned == true; }
            catch { weaponOwned = false; }
            if (weaponIdentity == null || weaponIdentity.netId == 0 ||
                weaponIdentity.netId != unchecked((uint)syncedNetId) || !weaponOwned)
            {
                error = "a populated native weapon slot has not completed owner authority";
                return false;
            }
            ownedWeaponCount++;
        }
        if (ownedWeaponCount == 0)
        {
            error = "the owned player has no native network weapon available for firearm play";
            return false;
        }
        return true;
    }

    private static bool TryValidatePeerPveActorNativeNetworkContract(
        NetworkIdentity identity,
        out string error)
    {
        error = string.Empty;
        GameObject root = identity == null ? null : identity.gameObject;
        if (identity == null || root == null || identity.netId == 0 ||
            identity.assetId == 0 || !root.activeInHierarchy)
        {
            error = "PVE actor NetworkIdentity/root is unavailable";
            return false;
        }
        BrainAI brain = root.GetComponent<BrainAI>();
        TeamIdentifier team = root.GetComponent<TeamIdentifier>();
        Health health = root.GetComponent<Health>();
        WeaponsAI weapons = root.GetComponent<WeaponsAI>();
        NetworkAnimatorSyncNPC animator = root.GetComponent<NetworkAnimatorSyncNPC>();
        global::Smooth.SmoothSyncMirror[] smooth =
            root.GetComponents<global::Smooth.SmoothSyncMirror>();
        if (brain == null || team == null || health == null || weapons == null ||
            animator == null || smooth == null || smooth.Length != 2 ||
            smooth.Any(item => item == null || !item.enabled ||
                item.netIdentity != identity) ||
            brain.netIdentity != identity || team.netIdentity != identity ||
            health.netIdentity != identity || weapons.netIdentity != identity ||
            animator.netIdentity != identity ||
            !PeerIdentityContainsBehaviour(identity, brain) ||
            !PeerIdentityContainsBehaviour(identity, team) ||
            !PeerIdentityContainsBehaviour(identity, health) ||
            !PeerIdentityContainsBehaviour(identity, weapons) ||
            !PeerIdentityContainsBehaviour(identity, animator) ||
            smooth.Any(item => !PeerIdentityContainsBehaviour(identity, item)))
        {
            error = "PVE actor native AI/SmoothSync/Health/Weapons/Animator closure is incomplete";
            return false;
        }
        return true;
    }

    private static bool PeerIdentityContainsBehaviour(
        NetworkIdentity identity,
        NetworkBehaviour behaviour)
    {
        if (identity == null || behaviour == null ||
            identity.NetworkBehaviours == null)
        {
            return false;
        }
        for (int index = 0; index < identity.NetworkBehaviours.Length; index++)
        {
            if (identity.NetworkBehaviours[index] == behaviour)
                return true;
        }
        return false;
    }

    private bool IsHostPeerPlayerBarrierReady(
        ActiveMapOperation operation,
        HostPvpAgreement host)
    {
        if (operation == null || host == null || !operation.AllPlayersLoaded ||
            !host.RequiredConnectionIds.SetEquals(host.RuntimeReadyConnectionIds))
        {
            return false;
        }
        PlayerMaster[] players;
        try { players = Resources.FindObjectsOfTypeAll<PlayerMaster>(); }
        catch { return false; }
        int livePlayers = 0;
        bool localReady = false;
        foreach (PlayerMaster player in players.Where(item => item != null))
        {
            NetworkIdentity masterIdentity = player.GetComponent<NetworkIdentity>();
            PlayerNetworking spawned;
            try { spawned = player.PlayerSpawnedObject; }
            catch { continue; }
            if (masterIdentity == null || masterIdentity.netId == 0 || spawned == null)
                continue;
            livePlayers++;
            bool owned;
            try { owned = player.isOwned || spawned.isOwned || spawned.isLocalPlayer; }
            catch { owned = false; }
            if (!TryValidatePeerPlayerNativeNetworkContract(
                    spawned,
                    requireOwnedWeaponAuthority: owned,
                    out _,
                    out _))
            {
                return false;
            }
            Transform marker = SelectPlayerMarker(
                operation,
                player,
                FindStandalonePlayerMarkers(
                    FindLoadedSceneByHandle(operation.SceneHandle),
                    operation.Operation.Mode));
            if (marker == null)
                return false;
            Vector3 target = marker.position + Vector3.up * 0.25f;
            if (owned)
            {
                if (!IsPlayerAtPackageSpawn(
                        spawned,
                        target,
                        operation.SceneHandle,
                        true,
                        out _))
                    return false;
                localReady = true;
            }
            else
            {
                NetworkConnectionToClient connection;
                try { connection = player.connectionToClient; }
                catch { return false; }
                if (connection == null ||
                    !host.PlayerPlacementByConnectionId.TryGetValue(
                        connection.connectionId, out PeerPlayerPlacement assignment) ||
                    !assignment.ReceiptReceived ||
                    !IsPlayerAtPackageSpawn(
                        spawned,
                        target,
                        operation.SceneHandle,
                        false,
                        out _))
                {
                    return false;
                }
                host.PlayerReadyConnectionIds.Add(connection.connectionId);
            }
        }
        if (livePlayers != host.Identity.ParticipantCount || !localReady ||
            !host.RequiredConnectionIds.SetEquals(host.PlayerReadyConnectionIds))
        {
            return false;
        }
        if (host.LocalPlayerReadyLastFrame == Time.frameCount - 1)
            host.LocalPlayerReadyStableFrames++;
        else
            host.LocalPlayerReadyStableFrames = 1;
        host.LocalPlayerReadyLastFrame = Time.frameCount;
        host.LocalPlayerReady = host.LocalPlayerReadyStableFrames >= 15;
        return host.LocalPlayerReady;
    }

    private static void ValidatePeerPopulationManifestShape(
        PeerPvePopulationManifest manifest,
        int expectedCount)
    {
        if (manifest == null || manifest.Revision != 1 ||
            manifest.Records == null || manifest.Records.Count != expectedCount ||
            !IsLowercasePvpSha256(manifest.Digest))
        {
            throw new InvalidOperationException("PVE population manifest shape is invalid");
        }
        if (manifest.Records.Any(record => record == null || record.NetId == 0 ||
                record.AssetId == 0 || record.TeamId < 0) ||
            manifest.Records.Select(record => record.NetId).Distinct().Count() !=
                manifest.Records.Count ||
            !manifest.Records.Select(record => record.NetId)
                .SequenceEqual(manifest.Records.Select(record => record.NetId).OrderBy(id => id)))
        {
            throw new InvalidOperationException("PVE population records are invalid/unsorted");
        }
        string expectedDigest = ComputePeerPopulationDigest(manifest.Records);
        if (!string.Equals(expectedDigest, manifest.Digest, StringComparison.Ordinal))
            throw new InvalidOperationException("PVE population digest is invalid");
    }

    private static string ComputePeerPopulationDigest(
        IReadOnlyList<PeerPvePopulationRecord> records)
    {
        string canonical = string.Join("\n", records.Select(record =>
            record.NetId.ToString(CultureInfo.InvariantCulture) + "|" +
            record.AssetId.ToString(CultureInfo.InvariantCulture) + "|" +
            record.TeamId.ToString(CultureInfo.InvariantCulture) + "|" +
            record.Xmm.ToString(CultureInfo.InvariantCulture) + "|" +
            record.Ymm.ToString(CultureInfo.InvariantCulture) + "|" +
            record.Zmm.ToString(CultureInfo.InvariantCulture) + "|" +
            record.YawMilliDegrees.ToString(CultureInfo.InvariantCulture)));
        return ComputePeerRuntimeDigest("operator-pve-population-manifest-v1", canonical);
    }

    private PeerPvePopulationManifest CreateAuthoritativePvePopulationManifest(
        ActiveMapOperation operation)
    {
        if (operation == null || !operation.PveTeamContractValidated ||
            operation.PveOwnedServerIdentities.Count != operation.PveExpectedSpawnCount)
        {
            throw new InvalidOperationException(
                "authoritative PVE population is not fully team-validated");
        }
        var records = new List<PeerPvePopulationRecord>();
        foreach (var entry in operation.PveOwnedServerIdentities.OrderBy(item => item.Key))
        {
            NetworkIdentity identity = entry.Value;
            GameObject root = identity == null ? null : identity.gameObject;
            BrainAI brain = root?.GetComponent<BrainAI>();
            TeamIdentifier team = root?.GetComponent<TeamIdentifier>();
            Health health = root?.GetComponent<Health>();
            WeaponsAI weapons = root?.GetComponent<WeaponsAI>();
            NetworkAnimatorSyncNPC animator = root?.GetComponent<NetworkAnimatorSyncNPC>();
            if (identity == null || root == null || identity.netId != entry.Key ||
                identity.assetId == 0 || brain == null || team == null ||
                health == null || weapons == null || animator == null ||
                team.TeamID != operation.PveExpectedTeamId ||
                !TryValidatePeerPveActorNativeNetworkContract(identity, out _))
            {
                throw new InvalidOperationException(
                    "authoritative PVE population component/team closure changed");
            }
            if (!operation.PveOwnedInitialPositions.TryGetValue(
                    entry.Key, out Vector3 position) ||
                !operation.PveOwnedInitialYaws.TryGetValue(
                    entry.Key, out float initialYaw))
            {
                throw new InvalidOperationException(
                    "authoritative PVE initial spawn transform was not captured");
            }
            records.Add(new PeerPvePopulationRecord
            {
                NetId = entry.Key,
                AssetId = identity.assetId,
                TeamId = team.TeamID,
                Xmm = QuantizePeerMillimetres(position.x),
                Ymm = QuantizePeerMillimetres(position.y),
                Zmm = QuantizePeerMillimetres(position.z),
                YawMilliDegrees = QuantizePeerYaw(initialYaw)
            });
        }
        var manifest = new PeerPvePopulationManifest
        {
            Revision = 1,
            Records = records
        };
        manifest.Digest = ComputePeerPopulationDigest(records);
        ValidatePeerPopulationManifestShape(manifest, operation.PveExpectedSpawnCount);
        return manifest;
    }

    private void PublishAuthoritativePvePopulationManifest(
        ActiveMapOperation operation,
        HostPvpAgreement host)
    {
        if (operation == null || host == null || !host.PlayerBarrierPassed)
            return;
        bool firstPublication = host.PopulationManifest == null;
        host.PopulationManifest ??= CreateAuthoritativePvePopulationManifest(operation);
        BroadcastHostPvpControl(
            PvpAgreementMessageKind.PopulationManifest,
            SerializePeerRuntimePayload(host.PopulationManifest),
            host.SceneGenerationEpoch);
        host.PopulationRetryTimestamp = DeadlineAfter(PeerRuntimeRetrySeconds);
        host.RuntimeBarrierDeadlineTimestamp = DeadlineAfter(
            PeerPopulationReadyTimeoutSeconds);
        if (firstPublication)
        {
            LogPvePeerAgreementEvidence(
                "pve-peer-population-manifest",
                host.Identity,
                operation,
                "host",
                host.SceneGenerationEpoch,
                "count=" + FrameworkEvidence.Number(
                    host.PopulationManifest.Records.Count) +
                "|populationDigest=" + FrameworkEvidence.Encode(
                    host.PopulationManifest.Digest));
        }
    }

    private void TryValidateAndAcknowledgeRemotePvePopulation(
        ActiveMapOperation operation,
        RemotePvpAgreement remote)
    {
        PeerPvePopulationManifest manifest = remote?.PopulationManifest;
        if (operation == null || manifest == null || remote.PopulationReadySent ||
            !remote.PlayerReadySent || !NetworkClient.active ||
            NetworkClient.spawned == null)
        {
            return;
        }
        var seen = new HashSet<uint>();
        foreach (PeerPvePopulationRecord record in manifest.Records)
        {
            if (!NetworkClient.spawned.TryGetValue(
                    record.NetId, out NetworkIdentity identity) || identity == null ||
                identity.netId != record.NetId || identity.assetId != record.AssetId)
            {
                return;
            }
            GameObject root = identity.gameObject;
            BrainAI brain = root?.GetComponent<BrainAI>();
            TeamIdentifier team = root?.GetComponent<TeamIdentifier>();
            Health health = root?.GetComponent<Health>();
            WeaponsAI weapons = root?.GetComponent<WeaponsAI>();
            NetworkAnimatorSyncNPC animator = root?.GetComponent<NetworkAnimatorSyncNPC>();
            if (root == null || brain == null || team == null || health == null ||
                weapons == null || animator == null || team.TeamID != record.TeamId ||
                !TryValidatePeerPveActorNativeNetworkContract(identity, out _))
            {
                return;
            }
            Vector3 expected = new Vector3(
                record.Xmm / 1000f,
                record.Ymm / 1000f,
                record.Zmm / 1000f);
            if (!operation.PeerObservedPveInitialPositions.TryGetValue(
                    record.NetId, out Vector3 observedInitial) ||
                !operation.PeerObservedPveInitialYaws.TryGetValue(
                    record.NetId, out float observedYaw) ||
                Vector3.Distance(observedInitial, expected) > 1.5f ||
                Mathf.Abs(Mathf.DeltaAngle(
                    observedYaw,
                    record.YawMilliDegrees / 1000f)) > 15f)
                return;
            seen.Add(record.NetId);
        }
        int remoteBrainRoots = 0;
        var enumerator = NetworkClient.spawned.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                NetworkIdentity identity = enumerator.Current.Value;
                if (identity?.gameObject?.GetComponent<BrainAI>() != null)
                    remoteBrainRoots++;
            }
        }
        finally { enumerator.Dispose(); }
        if (seen.Count != manifest.Records.Count ||
            remoteBrainRoots != manifest.Records.Count)
        {
            return;
        }
        if (remote.PopulationReadyLastFrame == Time.frameCount - 1)
            remote.PopulationReadyStableFrames++;
        else
            remote.PopulationReadyStableFrames = 1;
        remote.PopulationReadyLastFrame = Time.frameCount;
        if (remote.PopulationReadyStableFrames < 15)
            return;
        SendRemotePvpControl(
            PvpAgreementMessageKind.PopulationReady,
            manifest.Digest,
            sceneGenerationEpoch: remote.RequestedSceneGenerationEpoch);
        remote.PopulationReadySent = true;
        LogPvePeerAgreementEvidence(
            "pve-peer-population-ready",
            remote.Identity,
            operation,
            "remote",
            remote.RequestedSceneGenerationEpoch,
            "count=" + FrameworkEvidence.Number(manifest.Records.Count) +
            "|populationDigest=" + FrameworkEvidence.Encode(manifest.Digest));
        log.LogInfo("Remote PVE peer validated the authoritative AI population for " +
            "15 consecutive frames: count=" + manifest.Records.Count +
            ", digest=" + manifest.Digest + ".");
    }

    private void ProcessPeerRuntimeBarriers(ActiveMapOperation operation)
    {
        try
        {
            ProcessPeerRuntimeBarriersCore(operation);
        }
        catch (Exception ex)
        {
            string reason = "peer runtime barrier failed closed: " +
                ex.GetType().Name + ": " + ex.Message;
            if (NetworkServer.active)
                FailHostPvpAgreement(reason, true);
            else if (NetworkClient.active)
                FailRemotePvpAgreement(reason, true);
            else
                log.LogError(reason);
        }
    }

    private void ProcessPeerRuntimeBarriersCore(ActiveMapOperation operation)
    {
        if (operation == null || !operation.PeerAgreementRequired ||
            operation.Operation?.Mode != ModdedOperationMode.PlayerVersusEnvironment)
        {
            return;
        }
        if (NetworkServer.active)
        {
            SuppressStandalonePveExtraction(operation);
            HostPvpAgreement host = hostPvpAgreement;
            if (host == null || !OperationMatchesPvpIdentity(operation, host.Identity))
                return;
            if (operation.NetworkSpawnRequested && operation.BootstrapSpawnedNetId != 0 &&
                (!host.RuntimeOwnerManifestSent ||
                 (!host.RequiredConnectionIds.SetEquals(host.RuntimeReadyConnectionIds) &&
                  DeadlineExpired(host.RuntimeOwnerRetryTimestamp))))
            {
                PublishPeerRuntimeOwnerManifest(operation, host);
            }
            if (host.RuntimeBarrierDeadlineTimestamp > 0 &&
                DeadlineExpired(host.RuntimeBarrierDeadlineTimestamp) &&
                !operation.GameplayBeginCommitted)
            {
                FailHostPvpAgreement(
                    !host.RequiredConnectionIds.SetEquals(host.RuntimeReadyConnectionIds)
                        ? "timed out waiting for remote native-owner readiness"
                        : !host.PlayerBarrierPassed
                            ? "timed out waiting for owner-side player placement"
                            : "timed out waiting for remote PVE population replication",
                    true);
                return;
            }
            if (!operation.AllPlayersLoaded ||
                !host.RequiredConnectionIds.SetEquals(host.RuntimeReadyConnectionIds))
            {
                return;
            }
            SpawnAndPositionStandalonePlayers(operation, true);
            if (!host.PlayerBarrierPassed &&
                IsHostPeerPlayerBarrierReady(operation, host))
            {
                host.PlayerBarrierPassed = true;
                host.RuntimeBarrierDeadlineTimestamp = DeadlineAfter(
                    PeerPopulationReadyTimeoutSeconds);
                log.LogInfo("PVE owner-side player placement barrier passed on every " +
                    "frozen peer: epoch=" + host.SceneGenerationEpoch + ".");
                LogPvePeerAgreementEvidence(
                    "pve-peer-player-barrier",
                    host.Identity,
                    operation,
                    "host",
                    host.SceneGenerationEpoch,
                    "participantCount=" + FrameworkEvidence.Number(
                        host.Identity.ParticipantCount) +
                    "|remoteReady=" + FrameworkEvidence.Number(
                        host.PlayerReadyConnectionIds.Count) +
                    "|localReady=true");
            }
            if (!host.PlayerBarrierPassed)
                return;
            if (operation.PveTeamContractValidated && host.PopulationManifest == null)
                PublishAuthoritativePvePopulationManifest(operation, host);
            if (host.PopulationManifest != null &&
                !host.RequiredConnectionIds.SetEquals(host.PopulationReadyConnectionIds) &&
                DeadlineExpired(host.PopulationRetryTimestamp))
            {
                PublishAuthoritativePvePopulationManifest(operation, host);
            }
            if (host.PopulationManifest != null && !host.BeginCommitSent &&
                host.RequiredConnectionIds.SetEquals(host.PopulationReadyConnectionIds))
            {
                BroadcastHostPvpControl(
                    PvpAgreementMessageKind.BeginCommit,
                    host.PopulationManifest.Digest,
                    host.SceneGenerationEpoch);
                host.BeginCommitSent = true;
                host.RuntimeBarrierDeadlineTimestamp = 0;
                operation.GameplayBeginCommitted = true;
                LogPvePeerAgreementEvidence(
                    "pve-peer-begin-commit",
                    host.Identity,
                    operation,
                    "host",
                    host.SceneGenerationEpoch,
                    "populationDigest=" + FrameworkEvidence.Encode(
                        host.PopulationManifest.Digest) +
                    "|playerReady=true|populationReady=true");
                log.LogInfo("PVE BeginGameplay committed after exact runtime, " +
                    "owner-placement, and AI-population barriers: epoch=" +
                    host.SceneGenerationEpoch + ".");
            }
            return;
        }

        RemotePvpAgreement remote = remotePvpAgreement;
        if (remote == null || !OperationMatchesPvpIdentity(operation, remote.Identity))
            return;
        CaptureRemotePeerPveInitialPopulation(operation);
        TrySendRemotePeerRuntimeReady(operation, remote);
        ProcessRemotePeerPlayerPlacement(operation, remote);
        TryValidateAndAcknowledgeRemotePvePopulation(operation, remote);
        if (remote.RuntimeBarrierDeadlineTimestamp > 0 &&
            DeadlineExpired(remote.RuntimeBarrierDeadlineTimestamp) &&
            !operation.GameplayBeginCommitted)
        {
            FailRemotePvpAgreement(
                !remote.RuntimeReadySent
                    ? "timed out acknowledging the native owner/readiness lifecycle"
                    : !remote.PlayerReadySent
                        ? "timed out applying the owner-side player placement"
                        : "timed out validating the authoritative PVE population",
                true);
        }
    }

    private static void CaptureRemotePeerPveInitialPopulation(
        ActiveMapOperation operation)
    {
        if (operation == null || NetworkServer.active || !NetworkClient.active ||
            NetworkClient.spawned == null)
        {
            return;
        }
        var enumerator = NetworkClient.spawned.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                var entry = enumerator.Current;
                uint netId = entry.Key;
                NetworkIdentity identity = entry.Value;
                GameObject root = identity == null ? null : identity.gameObject;
                if (netId == 0 || root == null ||
                    root.GetComponent<BrainAI>() == null ||
                    operation.PeerObservedPveInitialPositions.ContainsKey(netId))
                {
                    continue;
                }
                operation.PeerObservedPveInitialPositions.Add(
                    netId, root.transform.position);
                operation.PeerObservedPveInitialYaws.Add(
                    netId, root.transform.eulerAngles.y);
            }
        }
        finally { enumerator.Dispose(); }
    }
}
