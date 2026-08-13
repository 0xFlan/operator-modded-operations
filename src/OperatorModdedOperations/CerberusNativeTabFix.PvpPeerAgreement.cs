using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Mirror;
using OperatorModAPI;
using UnityEngine.SceneManagement;

public sealed partial class CerberusNativeTabFix
{
    private const string PvpAgreementFrameworkVersion = "0.3.29";
    private const string PvpAgreementCapabilities =
        "exact-content-v2;runtime-binary-sha256-v1;package-runtime-ready-v1;" +
        "remote-preload-v1;scene-ready-epoch-v1;native-pvp-v1";
    private const ushort PvpAgreementProtocolVersion = 2;
    // Private, collision-checked Mirror envelope. This is intentionally not a
    // public package protocol and is removed when the network lifetime ends.
    private const ushort PvpAgreementMessageId = 0x4D4F;
    private const int PvpAgreementMaxEnvelopeBytes = 8192;
    private const int PvpAgreementMaxIdentityString = 1024;
    private const int PvpAgreementMaxReasonString = 512;
    private const double PvpContentReadyTimeoutSeconds = 45d;
    private const double PvpSceneReadyTimeoutSeconds = 90d;
    private const double PvpSceneReadyRequestRetrySeconds = 5d;
    private const double PvpNativeOwnerTimeoutSeconds = 90d;
    private const double PvpNativeLifecycleTimeoutSeconds = 90d;
    private const string PvpNoCompanionIdentity = "none";
    private const string PvpApiPluginGuid = "operator.modapi";
    private const string PvpApiHostTypeName =
        "OperatorModAPI.BepInEx.OperatorApiPlugin";

    private enum PvpAgreementMessageKind : byte
    {
        Offer = 1,
        ContentReady = 2,
        SceneReady = 3,
        Reject = 4,
        Cancel = 5,
        SceneReadyRequest = 6
    }

    private enum HostPvpAgreementPhase
    {
        WaitingForContent,
        WaitingForScenes,
        ReadyToSpawn,
        SpawnAuthorized
    }

    private sealed class PvpPeerIdentity
    {
        public string Nonce;
        public string FrameworkVersion;
        public string FrameworkSha256;
        public string ApiVersion;
        public string ApiCoreSha256;
        public string ApiHostSha256;
        public string GameBuildId;
        public string Capabilities;
        public string PackageId;
        public string PackageVersion;
        public string PackageContentId;
        public string MapId;
        public string OperationId;
        public int Mode;
        public string SpawnSetId;
        public string VariantId;
        public string ScenePath;
        public string TimeCode;
        public int MinimumPlayers;
        public int MaximumPlayers;
        public string CompanionPluginGuid;
        public string CompanionPluginVersion;
        public string CompanionSha256;
        public string CompanionReadyMarkerName;
        public string CompanionFailureMarkerName;
        public string Digest;
    }

    private sealed class HostPvpAgreement
    {
        public CatalogPresentation Presentation;
        public ModdedMapDefinition Map;
        public ModdedOperationDefinition Operation;
        public string TimeCode;
        public MissionLaptop LaunchLaptop;
        public PlayerNetworking LaunchPlayer;
        public SceneVariantSelection SceneSelection;
        public PvpPeerIdentity Identity;
        public readonly HashSet<int> RequiredConnectionIds = new HashSet<int>();
        public readonly Dictionary<int, NetworkConnectionToClient> RequiredConnections =
            new Dictionary<int, NetworkConnectionToClient>();
        public readonly HashSet<int> ContentReadyConnectionIds = new HashSet<int>();
        public readonly Dictionary<int, ulong> SceneReadyEpochByConnectionId =
            new Dictionary<int, ulong>();
        public HostPvpAgreementPhase Phase;
        public long DeadlineTimestamp;
        public bool LocalSceneReady;
        public ulong SceneGenerationEpoch;
        public ulong LocalSceneReadyEpoch;
        public long SceneReadyRequestRetryTimestamp;
        public bool NativeLaunchInvoked;
        public long NativeLifecycleDeadlineTimestamp;
    }

    private sealed class RemotePvpAgreement
    {
        public PvpPeerIdentity Identity;
        public ModdedMapDefinition Map;
        public ModdedOperationDefinition Operation;
        public SceneVariantSelection SceneSelection;
        public bool ContentCommitted;
        public bool ContentReadySent;
        public bool SceneReadySent;
        public bool LocalSceneReady;
        public ulong RequestedSceneGenerationEpoch;
        public ulong RequestedLocalSceneGeneration;
        public ulong LocalSceneGeneration;
        public ulong LocalReadySceneGeneration;
        public ulong SceneReadySentEpoch;
        public ulong SceneReadySentLocalGeneration;
        public int LastObservedSceneHandle;
        public bool AwaitingLocalSceneGeneration;
        public bool NativeOwnerAdopted;
        public bool NativeReadinessInitialized;
        public long DeadlineTimestamp;
    }

    private sealed class RuntimeBinaryIdentity
    {
        public string FrameworkSha256;
        public string ApiCoreSha256;
        public string ApiHostSha256;
        public string CompanionPluginGuid;
        public string CompanionPluginVersion;
        public string CompanionSha256;
        public string CompanionReadyMarkerName;
        public string CompanionFailureMarkerName;
    }

    private Action<NetworkConnection, NetworkReader, int> pvpClientManagedHandler;
    private Action<NetworkConnection, NetworkReader, int> pvpServerManagedHandler;
    private NetworkMessageDelegate pvpClientHandler;
    private NetworkMessageDelegate pvpServerHandler;
    private bool pvpClientHandlerRegistered;
    private bool pvpServerHandlerRegistered;
    private string pvpAgreementTransportFailure;
    private HostPvpAgreement hostPvpAgreement;
    private RemotePvpAgreement remotePvpAgreement;

    private void MaintainPvpPeerAgreementTransport()
    {
        try
        {
            if (!NetworkClient.active && !NetworkServer.active)
            {
                if (pvpClientHandlerRegistered || pvpServerHandlerRegistered ||
                    hostPvpAgreement != null || remotePvpAgreement != null)
                {
                    ReleasePvpPeerAgreementTransport("network lifetime ended");
                }
                return;
            }

            NetworkMessageDelegate registeredClient = null;
            if (pvpClientHandlerRegistered &&
                (NetworkClient.handlers == null ||
                 !NetworkClient.handlers.TryGetValue(
                     PvpAgreementMessageId,
                     out registeredClient) ||
                 !SamePvpAgreementHandler(registeredClient, pvpClientHandler)))
            {
                pvpClientHandlerRegistered = false;
                if (registeredClient != null)
                {
                    pvpAgreementTransportFailure =
                        "Mirror client message ID ownership changed at 0x" +
                        PvpAgreementMessageId.ToString(
                            "X4",
                            CultureInfo.InvariantCulture);
                }
            }
            NetworkMessageDelegate registeredServer = null;
            if (pvpServerHandlerRegistered &&
                (NetworkServer.handlers == null ||
                 !NetworkServer.handlers.TryGetValue(
                     PvpAgreementMessageId,
                     out registeredServer) ||
                 !SamePvpAgreementHandler(registeredServer, pvpServerHandler)))
            {
                pvpServerHandlerRegistered = false;
                if (registeredServer != null)
                {
                    pvpAgreementTransportFailure =
                        "Mirror server message ID ownership changed at 0x" +
                        PvpAgreementMessageId.ToString(
                            "X4",
                            CultureInfo.InvariantCulture);
                }
            }

            if (NetworkClient.active && !pvpClientHandlerRegistered)
            {
                if (NetworkClient.handlers == null)
                    throw new InvalidOperationException(
                        "Mirror client handler registry is unavailable");
                if (NetworkClient.handlers.TryGetValue(
                        PvpAgreementMessageId,
                        out NetworkMessageDelegate existingClient))
                {
                    pvpAgreementTransportFailure =
                        "Mirror client message ID collision at 0x" +
                        PvpAgreementMessageId.ToString("X4", CultureInfo.InvariantCulture);
                }
                else
                {
                    pvpClientManagedHandler = HandlePvpClientAgreementEnvelope;
                    pvpClientHandler = pvpClientManagedHandler;
                    NetworkClient.handlers.Add(
                        PvpAgreementMessageId,
                        pvpClientHandler);
                    pvpClientHandlerRegistered = true;
                }
            }

            if (NetworkServer.active && !pvpServerHandlerRegistered)
            {
                if (NetworkServer.handlers == null)
                    throw new InvalidOperationException(
                        "Mirror server handler registry is unavailable");
                if (NetworkServer.handlers.TryGetValue(
                        PvpAgreementMessageId,
                        out NetworkMessageDelegate existingServer))
                {
                    pvpAgreementTransportFailure =
                        "Mirror server message ID collision at 0x" +
                        PvpAgreementMessageId.ToString("X4", CultureInfo.InvariantCulture);
                }
                else
                {
                    pvpServerManagedHandler = HandlePvpServerAgreementEnvelope;
                    pvpServerHandler = pvpServerManagedHandler;
                    NetworkServer.handlers.Add(
                        PvpAgreementMessageId,
                        pvpServerHandler);
                    pvpServerHandlerRegistered = true;
                }
            }

            if (string.IsNullOrEmpty(pvpAgreementTransportFailure) &&
                (pvpClientHandlerRegistered || pvpServerHandlerRegistered))
            {
                return;
            }
            if (!string.IsNullOrEmpty(pvpAgreementTransportFailure))
            {
                FailHostPvpAgreement(pvpAgreementTransportFailure, true);
                FailRemotePvpAgreement(pvpAgreementTransportFailure, true);
            }
        }
        catch (Exception ex)
        {
            pvpAgreementTransportFailure = ex.GetType().Name + ": " + ex.Message;
            FailHostPvpAgreement(
                "PVP peer-agreement transport failed: " +
                pvpAgreementTransportFailure,
                true);
            FailRemotePvpAgreement(
                "PVP peer-agreement transport failed: " +
                pvpAgreementTransportFailure,
                true);
        }
    }

    private void ReleasePvpPeerAgreementTransport(string reason)
    {
        ActiveMapOperation agreementOperation = activeOperation;
        bool matchingAgreementOperation =
            agreementOperation?.Operation?.Mode ==
                ModdedOperationMode.PlayerVersusPlayer &&
            ((hostPvpAgreement != null && OperationMatchesPvpIdentity(
                agreementOperation,
                hostPvpAgreement.Identity)) ||
             (remotePvpAgreement != null && OperationMatchesPvpIdentity(
                agreementOperation,
                remotePvpAgreement.Identity)));
        bool postNativeLaunch = matchingAgreementOperation &&
            (agreementOperation.NativeLaunchInvoked ||
             agreementOperation.SceneHandle != 0 ||
             hostPvpAgreement?.NativeLaunchInvoked == true ||
             remotePvpAgreement?.ContentCommitted == true);
        if (matchingAgreementOperation)
        {
            agreementOperation.NetworkSpawnFailed = true;
        }
        try
        {
            if (hostPvpAgreement != null)
                BroadcastHostPvpControl(PvpAgreementMessageKind.Cancel, reason);
            else if (remotePvpAgreement != null && postNativeLaunch)
                SendRemotePvpControl(PvpAgreementMessageKind.Reject, reason);
        }
        catch { }
        if (postNativeLaunch)
            RequestNativePvpAbortReturn(agreementOperation);

        try
        {
            if (pvpClientHandlerRegistered && NetworkClient.handlers != null &&
                NetworkClient.handlers.TryGetValue(
                    PvpAgreementMessageId,
                    out NetworkMessageDelegate existingClient) &&
                SamePvpAgreementHandler(existingClient, pvpClientHandler))
            {
                NetworkClient.handlers.Remove(PvpAgreementMessageId);
            }
        }
        catch { }
        try
        {
            if (pvpServerHandlerRegistered && NetworkServer.handlers != null &&
                NetworkServer.handlers.TryGetValue(
                    PvpAgreementMessageId,
                    out NetworkMessageDelegate existingServer) &&
                SamePvpAgreementHandler(existingServer, pvpServerHandler))
            {
                NetworkServer.handlers.Remove(PvpAgreementMessageId);
            }
        }
        catch { }

        pvpClientHandlerRegistered = false;
        pvpServerHandlerRegistered = false;
        pvpClientHandler = null;
        pvpServerHandler = null;
        pvpClientManagedHandler = null;
        pvpServerManagedHandler = null;
        pvpAgreementTransportFailure = null;
        hostPvpAgreement = null;
        remotePvpAgreement = null;
    }

    private static bool SamePvpAgreementHandler(
        NetworkMessageDelegate left,
        NetworkMessageDelegate right)
    {
        if (ReferenceEquals(left, right))
            return true;
        try { return left != null && right != null && left.Pointer == right.Pointer; }
        catch { return false; }
    }

    private void BeginPvpPeerAgreementOrFail(
        CatalogPresentation presentation,
        ModdedMapDefinition map,
        ModdedOperationDefinition operation,
        string timeCode,
        MissionLaptop launchLaptop,
        PlayerNetworking launchPlayer,
        SceneVariantSelection sceneSelection)
    {
        try
        {
            if (operation == null || operation.Mode !=
                ModdedOperationMode.PlayerVersusPlayer)
            {
                throw new InvalidOperationException(
                    "peer agreement can only start for a PVP operation");
            }
            if (!NetworkServer.active || !NetworkClient.active)
            {
                throw new InvalidOperationException(
                    "PVP package launch requires an active native multiplayer host");
            }
            MaintainPvpPeerAgreementTransport();
            if (!pvpClientHandlerRegistered || !pvpServerHandlerRegistered ||
                !string.IsNullOrEmpty(pvpAgreementTransportFailure))
            {
                throw new InvalidOperationException(
                    pvpAgreementTransportFailure ??
                    "PVP peer-agreement handlers are not available");
            }
            if (!SelectionBelongsToMap(map, sceneSelection))
                throw new InvalidOperationException(
                    "selected PVP scene variant is not owned by the selected map");
            if (activeOperation != null && activeOperation.SceneHandle != 0)
                throw new InvalidOperationException(
                    "another package scene is still active on the host");

            if (hostPvpAgreement != null)
                BroadcastHostPvpControl(
                    PvpAgreementMessageKind.Cancel,
                    "a new host selection replaced the prior PVP agreement");
            hostPvpAgreement = null;

            var remoteConnections = CaptureAuthenticatedRemotePvpConnections(
                requireReady: true);
            int participantCount = remoteConnections.Count + 1;
            if (remoteConnections.Count == 0)
            {
                throw new InvalidOperationException(
                    "PVP package launch requires at least one authenticated remote peer");
            }
            if (participantCount < operation.MinimumPlayers ||
                participantCount > operation.MaximumPlayers)
            {
                throw new InvalidOperationException(
                    "PVP lobby population " + participantCount +
                    " is outside the operation range " + operation.MinimumPlayers +
                    "-" + operation.MaximumPlayers);
            }

            PvpPeerIdentity identity = CreatePvpPeerIdentity(
                map,
                operation,
                timeCode,
                sceneSelection,
                Guid.NewGuid().ToString("N"));
            var session = new HostPvpAgreement
            {
                Presentation = presentation,
                Map = map,
                Operation = operation,
                TimeCode = timeCode,
                LaunchLaptop = launchLaptop,
                LaunchPlayer = launchPlayer,
                SceneSelection = sceneSelection,
                Identity = identity,
                Phase = HostPvpAgreementPhase.WaitingForContent,
                DeadlineTimestamp = DeadlineAfter(PvpContentReadyTimeoutSeconds)
            };
            foreach (var entry in remoteConnections)
            {
                session.RequiredConnectionIds.Add(entry.Key);
                session.RequiredConnections.Add(entry.Key, entry.Value);
            }
            hostPvpAgreement = session;

            foreach (var entry in remoteConnections)
                SendPvpOffer(entry.Value, identity);
            log.LogInfo("PVP exact-content agreement offered to " +
                session.RequiredConnectionIds.Count + " remote peer(s): package=" +
                identity.PackageId + "@" + identity.PackageVersion +
                ", content=" + identity.PackageContentId + ", operation=" +
                identity.OperationId + ", variant=" + identity.VariantId + ".");
            log.LogInfo("PVP runtime binary agreement identity: frameworkSha256=" +
                identity.FrameworkSha256 +
                ", apiCoreSha256=" + identity.ApiCoreSha256 +
                ", apiHostSha256=" + identity.ApiHostSha256 +
                ", companion=" + identity.CompanionPluginGuid + "@" +
                identity.CompanionPluginVersion + ", companionSha256=" +
                identity.CompanionSha256 + ".");
        }
        catch (Exception ex)
        {
            SetNativeConfirmationLoadingState(presentation, false);
            hostPvpAgreement = null;
            log.LogError("PVP package launch failed closed before native start: " +
                ex.GetType().Name + ": " + ex.Message + ".");
        }
    }

    private void ClearPvpPeerAgreementForNonPvpLaunch()
    {
        if (hostPvpAgreement != null)
        {
            try
            {
                BroadcastHostPvpControl(
                    PvpAgreementMessageKind.Cancel,
                    "a non-PVP launch closed the prior PVP agreement");
            }
            catch { }
        }
        hostPvpAgreement = null;
        remotePvpAgreement = null;
    }

    private void ProcessPvpPeerAgreement()
    {
        TryAdvancePvpPackageRuntimeReadiness(activeOperation);
        HostPvpAgreement host = hostPvpAgreement;
        if (host != null)
        {
            if (host.NativeLaunchInvoked &&
                (activeOperation == null ||
                 !OperationMatchesPvpIdentity(activeOperation, host.Identity)))
            {
                FailHostPvpAgreement(
                    "active operation ownership changed after native PVP launch",
                    true);
                return;
            }
            if (!TryValidateHostPvpMembership(host, out string membershipError))
            {
                FailHostPvpAgreement(membershipError, true);
                return;
            }
            if ((host.Phase == HostPvpAgreementPhase.WaitingForContent ||
                 host.Phase == HostPvpAgreementPhase.WaitingForScenes) &&
                DeadlineExpired(host.DeadlineTimestamp))
            {
                string phase = host.Phase == HostPvpAgreementPhase.WaitingForContent
                    ? "content-ready"
                    : "scene-ready";
                FailHostPvpAgreement(
                    "timed out waiting for the exact remote " + phase + " barrier",
                    true);
                return;
            }

            if (host.Phase == HostPvpAgreementPhase.WaitingForScenes &&
                host.SceneGenerationEpoch != 0 &&
                DeadlineExpired(host.SceneReadyRequestRetryTimestamp))
            {
                try
                {
                    BroadcastHostPvpSceneReadyRequest(host, isRetry: true);
                }
                catch (Exception ex)
                {
                    FailHostPvpAgreement(
                        "host scene-generation request retry failed: " +
                        ex.GetType().Name + ": " + ex.Message,
                        true);
                    return;
                }
            }

            if (host.Phase == HostPvpAgreementPhase.ReadyToSpawn &&
                DeadlineExpired(host.DeadlineTimestamp))
            {
                FailHostPvpAgreement(
                    "timed out waiting to publish the exact native PVP owner",
                    true);
                return;
            }

            if (host.Phase == HostPvpAgreementPhase.WaitingForContent &&
                host.RequiredConnectionIds.SetEquals(
                    host.ContentReadyConnectionIds))
            {
                if (!TryBeginHostPvpSceneGeneration(
                        host,
                        "initial native scene transition",
                        out string generationError))
                {
                    FailHostPvpAgreement(generationError, true);
                    return;
                }
                log.LogInfo("PVP exact-content agreement passed on every peer; " +
                    "entering the native scene transition for operation=" +
                    host.Operation.Id + ".");
                log.LogInfo("PVP initial scene-generation readiness is bound to " +
                    "epoch=" + host.SceneGenerationEpoch + ".");
                InvokeNativeCatalogLaunch(
                    host.Presentation,
                    host.Map,
                    host.Operation,
                    host.TimeCode,
                    host.LaunchLaptop,
                    host.LaunchPlayer,
                    host.SceneSelection,
                    pvpPeerAgreementSatisfied: true);
                return;
            }

            bool hostNativeLifecycleComplete = activeOperation != null &&
                OperationMatchesPvpIdentity(activeOperation, host.Identity) &&
                activeOperation.AllPlayersLoaded;
            if (host.NativeLifecycleDeadlineTimestamp > 0 &&
                DeadlineExpired(host.NativeLifecycleDeadlineTimestamp) &&
                !hostNativeLifecycleComplete)
            {
                FailHostPvpAgreement(
                    "timed out waiting for the exact native PVP lifecycle barrier",
                    true);
                return;
            }

            if (host.Phase == HostPvpAgreementPhase.WaitingForScenes &&
                IsHostLocalSceneReadyForCurrentEpoch(host) &&
                AreRequiredPvpPeersSceneReadyForCurrentEpoch(host))
            {
                host.Phase = HostPvpAgreementPhase.ReadyToSpawn;
                host.DeadlineTimestamp = DeadlineAfter(PvpNativeOwnerTimeoutSeconds);
                log.LogInfo("PVP scene-ready agreement passed on the host and all " +
                    "remote peers; native PvpGameode spawn is authorized.");
                log.LogInfo("PVP scene-ready epoch barrier passed: " +
                    "sceneGenerationEpoch=" + host.SceneGenerationEpoch + ".");
            }
        }

        RemotePvpAgreement remote = remotePvpAgreement;
        if (remote != null && remote.ContentCommitted &&
            (activeOperation == null ||
             !OperationMatchesPvpIdentity(activeOperation, remote.Identity)))
        {
            FailRemotePvpAgreement(
                "active operation ownership changed after remote content commit",
                true);
            return;
        }
        if (remote != null && DeadlineExpired(remote.DeadlineTimestamp) &&
            (!IsRemoteSceneReadyForCurrentRequest(remote) ||
             !remote.NativeOwnerAdopted ||
             !remote.NativeReadinessInitialized))
        {
            FailRemotePvpAgreement(
                !remote.ContentCommitted
                    ? "timed out preloading the agreed PVP package content"
                    : !IsRemoteSceneReadyForCurrentRequest(remote)
                        ? "timed out waiting for the agreed PVP scene to become ready"
                        : !remote.NativeOwnerAdopted
                            ? "timed out waiting for the agreed native PVP owner"
                            : "timed out waiting for the agreed native PVP readiness lifecycle",
                true);
        }
    }

    private void HandlePvpClientAgreementEnvelope(
        NetworkConnection connection,
        NetworkReader reader,
        int channelId)
    {
        try
        {
            if (NetworkServer.active)
                return;
            if (NetworkClient.connection == null ||
                !NetworkClient.connection.isAuthenticated)
            {
                throw new InvalidOperationException(
                    "PVP agreement arrived before client authentication");
            }
            ValidatePvpEnvelopeReader(reader);
            ushort protocol = NetworkReaderExtensions.ReadUShort(reader);
            PvpAgreementMessageKind kind =
                (PvpAgreementMessageKind)NetworkReaderExtensions.ReadByte(reader);
            if (protocol != PvpAgreementProtocolVersion)
                throw new InvalidOperationException(
                    "unsupported PVP agreement protocol " + protocol);

            if (kind == PvpAgreementMessageKind.Offer)
            {
                PvpPeerIdentity offer = ReadPvpIdentity(reader);
                RequirePvpEnvelopeConsumed(reader);
                AcceptRemotePvpOfferOrReject(offer);
                return;
            }
            string nonce = ReadBoundedPvpString(reader, 64, "nonce");
            string digest = ReadBoundedPvpString(reader, 128, "digest");
            ulong sceneGenerationEpoch = NetworkReaderExtensions.ReadULong(reader);
            string reason = ReadBoundedPvpString(
                reader,
                PvpAgreementMaxReasonString,
                "reason",
                allowEmpty: true);
            RequirePvpEnvelopeConsumed(reader);
            RemotePvpAgreement remote = remotePvpAgreement;
            bool sessionMatches = remote != null &&
                string.Equals(remotePvpAgreement.Identity.Nonce, nonce,
                    StringComparison.Ordinal) &&
                string.Equals(remotePvpAgreement.Identity.Digest, digest,
                    StringComparison.Ordinal);
            if (kind == PvpAgreementMessageKind.SceneReadyRequest)
            {
                if (sceneGenerationEpoch == 0)
                    throw new InvalidOperationException(
                        "host scene-ready request omitted its generation epoch");
                if (sessionMatches)
                    AcceptRemotePvpSceneReadyRequest(remote, sceneGenerationEpoch);
                return;
            }
            if (kind == PvpAgreementMessageKind.Cancel)
            {
                if (sceneGenerationEpoch != 0)
                    throw new InvalidOperationException(
                        "host cancellation carried an invalid scene generation epoch");
                if (sessionMatches)
                    ClearRemotePvpAgreement(reason);
                return;
            }
            throw new InvalidOperationException(
                "unexpected host PVP agreement control " + kind);
        }
        catch (Exception ex)
        {
            FailRemotePvpAgreement(
                "invalid host PVP agreement envelope: " + ex.Message,
                true);
        }
    }

    private void HandlePvpServerAgreementEnvelope(
        NetworkConnection connection,
        NetworkReader reader,
        int channelId)
    {
        try
        {
            if (!NetworkServer.active || connection == null ||
                !connection.isAuthenticated)
            {
                return;
            }
            ValidatePvpEnvelopeReader(reader);
            ushort protocol = NetworkReaderExtensions.ReadUShort(reader);
            PvpAgreementMessageKind kind =
                (PvpAgreementMessageKind)NetworkReaderExtensions.ReadByte(reader);
            if (protocol != PvpAgreementProtocolVersion)
                throw new InvalidOperationException(
                    "unsupported PVP agreement protocol " + protocol);
            string nonce = ReadBoundedPvpString(reader, 64, "nonce");
            string digest = ReadBoundedPvpString(reader, 128, "digest");
            ulong sceneGenerationEpoch = NetworkReaderExtensions.ReadULong(reader);
            string reason = ReadBoundedPvpString(
                reader,
                PvpAgreementMaxReasonString,
                "reason",
                allowEmpty: true);
            RequirePvpEnvelopeConsumed(reader);

            HostPvpAgreement host = hostPvpAgreement;
            if (host == null ||
                !host.RequiredConnectionIds.Contains(connection.connectionId) ||
                !host.RequiredConnections.TryGetValue(
                    connection.connectionId,
                    out NetworkConnectionToClient requiredConnection) ||
                !SamePvpNetworkConnection(requiredConnection, connection) ||
                !string.Equals(host.Identity.Nonce, nonce, StringComparison.Ordinal) ||
                !string.Equals(host.Identity.Digest, digest, StringComparison.Ordinal))
            {
                return;
            }
            if (kind == PvpAgreementMessageKind.Reject)
            {
                if (sceneGenerationEpoch != 0)
                    throw new InvalidOperationException(
                        "remote rejection carried an invalid scene generation epoch");
                FailHostPvpAgreement(
                    "remote peer " + connection.connectionId +
                    " rejected the PVP agreement: " + reason,
                    true);
                return;
            }
            if (kind == PvpAgreementMessageKind.ContentReady &&
                sceneGenerationEpoch != 0)
            {
                throw new InvalidOperationException(
                    "remote content-ready acknowledgement carried an invalid " +
                    "scene generation epoch");
            }
            if (kind == PvpAgreementMessageKind.ContentReady &&
                host.Phase == HostPvpAgreementPhase.WaitingForContent)
            {
                host.ContentReadyConnectionIds.Add(connection.connectionId);
                log.LogInfo("PVP peer content-ready acknowledgement accepted: " +
                    "connection=" + connection.connectionId + ", ready=" +
                    host.ContentReadyConnectionIds.Count + "/" +
                    host.RequiredConnectionIds.Count + ".");
            }
            else if (kind == PvpAgreementMessageKind.SceneReady)
            {
                if (sceneGenerationEpoch == 0)
                    throw new InvalidOperationException(
                        "remote scene-ready acknowledgement omitted its generation epoch");
                if (host.Phase == HostPvpAgreementPhase.WaitingForScenes &&
                    sceneGenerationEpoch == host.SceneGenerationEpoch)
                {
                    host.SceneReadyEpochByConnectionId[connection.connectionId] =
                        sceneGenerationEpoch;
                    log.LogInfo("PVP peer scene-ready acknowledgement accepted: " +
                        "connection=" + connection.connectionId + ", ready=" +
                        CountRequiredPvpPeersSceneReadyForCurrentEpoch(host) +
                        "/" + host.RequiredConnectionIds.Count + ".");
                    log.LogInfo("PVP peer scene-ready epoch accepted: connection=" +
                        connection.connectionId + ", sceneGenerationEpoch=" +
                        sceneGenerationEpoch + ".");
                }
                else
                {
                    log.LogWarning("PVP peer scene-ready acknowledgement rejected as " +
                        "stale or out of phase: connection=" + connection.connectionId +
                        ", receivedEpoch=" + sceneGenerationEpoch +
                        ", expectedEpoch=" + host.SceneGenerationEpoch +
                        ", phase=" + host.Phase + ".");
                    if (host.Phase == HostPvpAgreementPhase.WaitingForScenes &&
                        host.SceneGenerationEpoch != 0)
                    {
                        SendHostPvpSceneReadyRequest(connection, host, isRetry: true);
                    }
                }
            }
            else if (kind != PvpAgreementMessageKind.ContentReady)
                throw new InvalidOperationException(
                    "unexpected remote PVP agreement control " + kind);
        }
        catch (Exception ex)
        {
            FailHostPvpAgreement(
                "invalid remote PVP agreement envelope: " + ex.Message,
                true);
        }
    }

    private void AcceptRemotePvpOfferOrReject(PvpPeerIdentity offer)
    {
        if (offer == null)
            throw new InvalidOperationException("host offer was empty");
        if (activeOperation != null && activeOperation.SceneHandle != 0)
        {
            SendRemotePvpControl(
                PvpAgreementMessageKind.Reject,
                "another package scene is still active on the remote peer",
                offer);
            return;
        }
        if (remotePvpAgreement != null)
        {
            if (string.Equals(
                    remotePvpAgreement.Identity.Nonce,
                    offer.Nonce,
                    StringComparison.Ordinal) &&
                string.Equals(
                    remotePvpAgreement.Identity.Digest,
                    offer.Digest,
                    StringComparison.Ordinal))
            {
                if (remotePvpAgreement.ContentCommitted)
                    SendRemotePvpControl(PvpAgreementMessageKind.ContentReady, string.Empty);
                return;
            }
            const string replacementReason =
                "host replaced an unfinished PVP agreement without cancellation";
            try
            {
                // Reject the incoming nonce/digest so the host's new session
                // observes the failure immediately. The old session is then
                // torn down locally; accepting the replacement requires a
                // later fresh Offer after explicit Cancel/teardown.
                SendRemotePvpControl(
                    PvpAgreementMessageKind.Reject,
                    replacementReason,
                    offer);
            }
            catch { }
            ClearRemotePvpAgreement(replacementReason);
            log.LogError("Remote PVP peer agreement failed closed: " +
                replacementReason + ".");
            return;
        }

        if (!TryResolveExactLocalPvpOffer(
                offer,
                out ModdedMapDefinition map,
                out ModdedOperationDefinition operation,
                out SceneVariantSelection selection,
                out string validationError))
        {
            SendRemotePvpControl(
                PvpAgreementMessageKind.Reject,
                validationError,
                offer);
            log.LogError("Remote PVP offer rejected before content loading: " +
                validationError + ".");
            return;
        }

        remotePvpAgreement = new RemotePvpAgreement
        {
            Identity = offer,
            Map = map,
            Operation = operation,
            SceneSelection = selection,
            AwaitingLocalSceneGeneration = true,
            DeadlineTimestamp = DeadlineAfter(PvpContentReadyTimeoutSeconds)
        };

        if (loadedMapBundles.TryGetValue(map.Id, out LoadedMapBundles loaded) &&
            loaded != null && loaded.SceneBundle != null && loaded.Map != null &&
            string.Equals(
                loaded.Map.PackageContentId,
                map.PackageContentId,
                StringComparison.Ordinal))
        {
            CompleteRemotePvpContentReady();
            return;
        }
        if (loaded != null &&
            !TryDiscardStaleCompletedMapBundle(
                map.Id,
                loaded,
                "remote PVP exact-content offer"))
        {
            FailRemotePvpAgreement(
                "stale map bundles could not be released for exact remote content",
                true);
            return;
        }

        if (pendingLaunch != null)
        {
            if (pendingLaunch.LaunchRequested || pendingLaunch.Map == null ||
                !string.Equals(pendingLaunch.Map.Id, map.Id, StringComparison.Ordinal) ||
                !string.Equals(
                    pendingLaunch.Map.PackageContentId,
                    map.PackageContentId,
                    StringComparison.Ordinal))
            {
                FailRemotePvpAgreement(
                    "another package load owns the remote content pipeline",
                    true);
            }
            return;
        }

        var loading = new LoadedMapBundles { Map = map };
        var pending = new PendingMapLaunch
        {
            Map = map,
            Operation = operation,
            TimeCode = offer.TimeCode,
            SceneSelection = selection,
            LoadingBundles = loading,
            LaunchRequested = false,
            LoadStartedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp()
        };
        foreach (string path in map.DependencyBundlePaths)
            pending.BundlePaths.Add(path);
        pending.BundlePaths.Add(map.SceneBundlePath);
        pendingLaunch = pending;
        log.LogInfo("Remote PVP peer began exact package preload: map=" + map.Id +
            ", package=" + map.PackageId + "@" + map.PackageVersion +
            ", content=" + map.PackageContentId + ".");
    }

    private bool TryResolveExactLocalPvpOffer(
        PvpPeerIdentity offer,
        out ModdedMapDefinition map,
        out ModdedOperationDefinition operation,
        out SceneVariantSelection selection,
        out string error)
    {
        map = null;
        operation = null;
        selection = null;
        error = string.Empty;
        if (!TryResolveLocalRuntimeIdentity(map: null,
                out RuntimeBinaryIdentity localRuntime,
                out string runtimeError))
        {
            error = "local runtime binary identity failed closed: " + runtimeError;
            return false;
        }
        if (!string.Equals(offer.FrameworkVersion, PvpAgreementFrameworkVersion,
                StringComparison.Ordinal) ||
            !string.Equals(offer.FrameworkSha256, localRuntime.FrameworkSha256,
                StringComparison.Ordinal) ||
            !string.Equals(offer.ApiVersion, OperatorApi.ApiVersion,
                StringComparison.Ordinal) ||
            !string.Equals(offer.ApiCoreSha256, localRuntime.ApiCoreSha256,
                StringComparison.Ordinal) ||
            !string.Equals(offer.ApiHostSha256, localRuntime.ApiHostSha256,
                StringComparison.Ordinal) ||
            !string.Equals(offer.GameBuildId,
                OperatorApi.Compatibility.DetectedGameBuildId,
                StringComparison.Ordinal) ||
            !string.Equals(offer.Capabilities, PvpAgreementCapabilities,
                StringComparison.Ordinal))
        {
            error = "framework/API binary/game-build/capability identity mismatch";
            return false;
        }
        if (!string.Equals(offer.Digest, ComputePvpIdentityDigest(offer),
                StringComparison.Ordinal))
        {
            error = "offer identity digest mismatch";
            return false;
        }
        if (!OperatorApi.ModdedOperations.TryGetMap(offer.MapId, out map) ||
            map == null)
        {
            error = "exact map ID is not present in the frozen local catalog";
            return false;
        }
        if (!string.Equals(map.PackageId, offer.PackageId, StringComparison.Ordinal) ||
            !string.Equals(map.PackageVersion, offer.PackageVersion,
                StringComparison.Ordinal) ||
            !string.Equals(map.PackageContentId, offer.PackageContentId,
                StringComparison.Ordinal))
        {
            error = "package ID/version/content hash mismatch";
            return false;
        }
        if (!TryResolveLocalRuntimeIdentity(map,
                out RuntimeBinaryIdentity mapRuntime,
                out runtimeError))
        {
            error = "local map runtime identity failed closed: " + runtimeError;
            return false;
        }
        if (!PvpCompanionIdentityMatches(offer, mapRuntime))
        {
            error = "map runtime companion GUID/version/SHA/marker identity mismatch";
            return false;
        }
        operation = map.Operations.FirstOrDefault(candidate => candidate != null &&
            string.Equals(candidate.Id, offer.OperationId, StringComparison.Ordinal));
        if (operation == null ||
            operation.Mode != ModdedOperationMode.PlayerVersusPlayer ||
            offer.Mode != (int)ModdedOperationMode.PlayerVersusPlayer ||
            !string.Equals(operation.PackageId, offer.PackageId,
                StringComparison.Ordinal) ||
            !string.Equals(operation.SpawnSetId, offer.SpawnSetId,
                StringComparison.Ordinal) ||
            operation.MinimumPlayers != offer.MinimumPlayers ||
            operation.MaximumPlayers != offer.MaximumPlayers ||
            !operation.SupportedTimeCodes.Contains(
                offer.TimeCode,
                StringComparer.Ordinal))
        {
            error = "operation/mode/spawn/player/time identity mismatch";
            return false;
        }
        bool declaredVariantMatches = HasDeclaredSceneVariants(map)
            ? map.SceneVariants.Any(variant =>
                variant != null &&
                string.Equals(variant.Id, offer.VariantId, StringComparison.Ordinal) &&
                string.Equals(variant.ScenePath, offer.ScenePath,
                    StringComparison.OrdinalIgnoreCase))
            : map.SceneVariants != null && map.SceneVariants.Count == 1 &&
              string.Equals(
                  offer.VariantId,
                  map.SceneVariants[0].Id,
                  StringComparison.Ordinal) &&
              string.Equals(offer.ScenePath, map.ScenePath,
                  StringComparison.OrdinalIgnoreCase);
        if (!declaredVariantMatches)
        {
            error = "scene variant ID/path mismatch";
            return false;
        }
        selection = new SceneVariantSelection(
            offer.VariantId,
            offer.ScenePath,
            remainingVariantCount: 0);
        return SelectionBelongsToMap(map, selection);
    }

    private void CompleteRemotePvpContentReady()
    {
        RemotePvpAgreement remote = remotePvpAgreement;
        if (remote == null || remote.ContentCommitted)
            return;
        if (NetworkServer.active || !NetworkClient.active ||
            NetworkClient.connection == null ||
            !NetworkClient.connection.isAuthenticated)
        {
            FailRemotePvpAgreement(
                "remote network ownership changed before exact content commit",
                true);
            return;
        }
        if (!loadedMapBundles.TryGetValue(
                remote.Map.Id,
                out LoadedMapBundles bundles) ||
            bundles == null || bundles.SceneBundle == null || bundles.Map == null ||
            !string.Equals(
                bundles.Map.PackageContentId,
                remote.Identity.PackageContentId,
                StringComparison.Ordinal))
        {
            FailRemotePvpAgreement(
                "verified remote package cache was unavailable at commit",
                true);
            return;
        }

        activeOperation = new ActiveMapOperation
        {
            Map = remote.Map,
            Operation = remote.Operation,
            TimeCode = remote.Identity.TimeCode,
            SceneSelection = remote.SceneSelection,
            SceneHandle = 0
        };
        remote.ContentCommitted = true;
        // This peer may finish before other frozen peers. Bound the complete
        // wait by the host's remaining content window plus the full scene
        // window; OnSceneLoaded resets it to one fresh scene window.
        remote.DeadlineTimestamp = DeadlineAfter(
            PvpContentReadyTimeoutSeconds + PvpSceneReadyTimeoutSeconds);
        SendRemotePvpControl(PvpAgreementMessageKind.ContentReady, string.Empty);
        remote.ContentReadySent = true;
        log.LogInfo("Remote PVP peer committed the exact operation before native " +
            "scene transition: operation=" + remote.Operation.Id + ", scene=" +
            remote.SceneSelection.ScenePath + ".");
    }

    private void NotifyPvpVerifiedMapBundlesAvailable(ModdedMapDefinition map)
    {
        RemotePvpAgreement remote = remotePvpAgreement;
        if (remote == null || map == null ||
            !string.Equals(remote.Map.Id, map.Id, StringComparison.Ordinal) ||
            !string.Equals(remote.Identity.PackageContentId,
                map.PackageContentId,
                StringComparison.Ordinal))
        {
            return;
        }
        CompleteRemotePvpContentReady();
    }

    private void NotifyPvpPendingMapLoadFailed(
        PendingMapLaunch pending,
        string reason)
    {
        RemotePvpAgreement remote = remotePvpAgreement;
        if (remote == null || pending?.Map == null ||
            !string.Equals(remote.Map.Id, pending.Map.Id, StringComparison.Ordinal) ||
            !string.Equals(remote.Identity.PackageContentId,
                pending.Map.PackageContentId,
                StringComparison.Ordinal))
        {
            return;
        }
        FailRemotePvpAgreement(
            "exact remote package preload failed: " + reason,
            true);
    }

    private void NotifyPvpNativeLaunchFailed(Exception exception)
    {
        string reason = "native PVP launch failed: " + exception.GetType().Name +
            ": " + exception.Message;
        if (hostPvpAgreement != null)
        {
            FailHostPvpAgreement(reason, true);
            return;
        }
        if (remotePvpAgreement != null)
            FailRemotePvpAgreement(reason, true);
    }

    private void NotifyPvpScenePrepared(ActiveMapOperation operation)
    {
        if (operation == null || operation.Operation == null ||
            operation.Operation.Mode != ModdedOperationMode.PlayerVersusPlayer)
        {
            return;
        }
        if (operation.BootstrapAssetId != StandalonePvpGameModeAssetId ||
            !operation.BootstrapPrefabRegistered ||
            !(operation.GameModeComponent is StandalonePvpGameMode))
        {
            NotifyPvpScenePreparationFailed(
                operation,
                "native PvpGameode template was not constructed and registered");
            return;
        }

        TryAdvancePvpPackageRuntimeReadiness(operation);
    }

    private void TryAdvancePvpPackageRuntimeReadiness(
        ActiveMapOperation operation)
    {
        if (operation == null || operation.Operation == null ||
            operation.Operation.Mode != ModdedOperationMode.PlayerVersusPlayer ||
            operation.NetworkSpawnFailed || !operation.ScenePreparationComplete ||
            operation.SceneHandle == 0)
        {
            return;
        }
        HostPvpAgreement currentHost = hostPvpAgreement;
        RemotePvpAgreement currentRemote = remotePvpAgreement;
        bool isHostOwner = NetworkServer.active && currentHost != null &&
            OperationMatchesPvpIdentity(operation, currentHost.Identity);
        bool isRemoteOwner = !NetworkServer.active && currentRemote != null &&
            currentRemote.ContentCommitted &&
            OperationMatchesPvpIdentity(operation, currentRemote.Identity);
        if (!isHostOwner && !isRemoteOwner)
            return;
        if (!TryValidateInstalledPvpSpawnContract(operation, out string spawnError))
        {
            NotifyPvpScenePreparationFailed(
                operation,
                "package-owned PVP spawn contract changed before SceneReady: " +
                spawnError);
            return;
        }
        ModdedRuntimeCompanionDefinition companion = operation.Map.RuntimeCompanion;
        Scene scene = FindLoadedSceneByHandle(operation.SceneHandle);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            NotifyPvpScenePreparationFailed(
                operation,
                "exact generation scene was unavailable at runtime readiness gate");
            return;
        }
        if (companion != null)
        {
            _ = FindExactSceneTransform(
                scene,
                companion.FailureMarkerName,
                out int failureMarkers);
            _ = FindExactSceneTransform(
                scene,
                companion.ReadyMarkerName,
                out int readyMarkers);
            if (failureMarkers != 0)
            {
                NotifyPvpScenePreparationFailed(
                    operation,
                    "package runtime companion emitted failure marker '" +
                    companion.FailureMarkerName + "' in exact scene handle=" +
                    scene.handle + "; count=" + failureMarkers);
                return;
            }
            if ((isHostOwner && IsHostLocalSceneReadyForCurrentEpoch(currentHost)) ||
                (isRemoteOwner && currentRemote.LocalSceneReady &&
                 currentRemote.LocalReadySceneGeneration ==
                 currentRemote.LocalSceneGeneration))
            {
                return;
            }
            if (readyMarkers == 0)
                return;
            if (readyMarkers != 1)
            {
                NotifyPvpScenePreparationFailed(
                    operation,
                    "package runtime companion ready marker was ambiguous in exact " +
                    "scene handle=" + scene.handle + "; marker='" +
                    companion.ReadyMarkerName + "', count=" + readyMarkers);
                return;
            }
            log.LogInfo("PVP package runtime readiness passed: companion=" +
                companion.PluginGuid + "@" + companion.PluginVersion +
                ", sceneHandle=" + scene.handle + ", readyMarker=" +
                companion.ReadyMarkerName + ".");
        }
        else
        {
            if ((isHostOwner && IsHostLocalSceneReadyForCurrentEpoch(currentHost)) ||
                (isRemoteOwner && currentRemote.LocalSceneReady &&
                 currentRemote.LocalReadySceneGeneration ==
                 currentRemote.LocalSceneGeneration))
            {
                return;
            }
            log.LogInfo("PVP package runtime readiness passed: companion=none, " +
                "sceneHandle=" + scene.handle + ".");
        }

        CompletePvpSceneReady(operation);
    }

    private static bool TryValidateInstalledPvpSpawnContract(
        ActiveMapOperation operation,
        out string error)
    {
        error = string.Empty;
        GameManager gameManager = GameManager.instance;
        if (operation == null || !operation.SpawnContractInstalled ||
            operation.OwnedSpawnPoints == null ||
            operation.OwnedFallbackSpawns == null || gameManager == null)
        {
            error = "installed spawn ownership is incomplete";
            return false;
        }
        if (!SameNativeSpawnList(
                GameManager.SpawnPointsInScene,
                operation.OwnedSpawnPoints) ||
            !SameNativeGameObjectArray(
                gameManager.Pspawns,
                operation.OwnedFallbackSpawns) ||
            !operation.RandomSpawnsCaptured || operation.OwnedRandomSpawns ||
            gameManager.RandomSpawns != operation.OwnedRandomSpawns)
        {
            error = "installed spawn globals no longer match this scene generation";
            return false;
        }
        return true;
    }

    private void CompletePvpSceneReady(ActiveMapOperation operation)
    {
        HostPvpAgreement host = hostPvpAgreement;
        if (NetworkServer.active && host != null &&
            OperationMatchesPvpIdentity(operation, host.Identity))
        {
            if (host.SceneGenerationEpoch == 0)
            {
                FailHostPvpAgreement(
                    "host scene became ready before a scene-generation epoch was issued",
                    true);
                operation.NetworkSpawnFailed = true;
                return;
            }
            host.LocalSceneReady = true;
            host.LocalSceneReadyEpoch = host.SceneGenerationEpoch;
            log.LogInfo("Host PVP scene preparation completed for " +
                "sceneGenerationEpoch=" + host.SceneGenerationEpoch + ".");
        }

        RemotePvpAgreement remote = remotePvpAgreement;
        if (!NetworkServer.active && remote != null &&
            remote.ContentCommitted &&
            OperationMatchesPvpIdentity(operation, remote.Identity))
        {
            if (remote.LocalSceneGeneration == 0 ||
                remote.LastObservedSceneHandle != operation.SceneHandle)
            {
                FailRemotePvpAgreement(
                    "remote scene readiness was not bound to an observed exact " +
                    "package scene generation",
                    true);
                operation.NetworkSpawnFailed = true;
                return;
            }
            remote.LocalSceneReady = true;
            remote.LocalReadySceneGeneration = remote.LocalSceneGeneration;
            TrySendRemotePvpSceneReadyAcknowledgement(remote, allowResend: false);
        }
    }

    private bool TryBeginHostPvpSceneGeneration(
        HostPvpAgreement host,
        string reason,
        out string error)
    {
        error = string.Empty;
        if (!NetworkServer.active || host == null || host.Identity == null)
        {
            error = "host scene-generation agreement ownership was unavailable";
            return false;
        }
        if (host.SceneGenerationEpoch == ulong.MaxValue)
        {
            error = "host scene-generation epoch overflowed";
            return false;
        }

        host.SceneGenerationEpoch++;
        host.LocalSceneReady = false;
        host.LocalSceneReadyEpoch = 0;
        host.SceneReadyEpochByConnectionId.Clear();
        host.Phase = HostPvpAgreementPhase.WaitingForScenes;
        host.DeadlineTimestamp = DeadlineAfter(PvpSceneReadyTimeoutSeconds);
        host.NativeLifecycleDeadlineTimestamp = 0;
        try
        {
            BroadcastHostPvpSceneReadyRequest(host, isRetry: false);
            log.LogInfo("PVP scene-generation readiness requested: epoch=" +
                host.SceneGenerationEpoch + ", reason=" + reason + ".");
            return true;
        }
        catch (Exception ex)
        {
            error = "host scene-generation request failed: " +
                ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private void AcceptRemotePvpSceneReadyRequest(
        RemotePvpAgreement remote,
        ulong sceneGenerationEpoch)
    {
        if (remote == null || remote.Identity == null ||
            remote != remotePvpAgreement || !remote.ContentCommitted)
        {
            throw new InvalidOperationException(
                "host scene-generation request had no committed remote session");
        }
        if (sceneGenerationEpoch < remote.RequestedSceneGenerationEpoch)
        {
            log.LogWarning("Remote PVP peer ignored stale scene-generation " +
                "readiness request: receivedEpoch=" + sceneGenerationEpoch +
                ", currentEpoch=" + remote.RequestedSceneGenerationEpoch + ".");
            return;
        }
        if (sceneGenerationEpoch == remote.RequestedSceneGenerationEpoch)
        {
            TrySendRemotePvpSceneReadyAcknowledgement(remote, allowResend: true);
            return;
        }
        if (sceneGenerationEpoch <= remote.SceneReadySentEpoch)
        {
            log.LogWarning("Remote PVP peer ignored already-acknowledged stale " +
                "scene-generation readiness request: receivedEpoch=" +
                sceneGenerationEpoch + ", acknowledgedEpoch=" +
                remote.SceneReadySentEpoch + ".");
            return;
        }
        if (remote.RequestedSceneGenerationEpoch == ulong.MaxValue ||
            sceneGenerationEpoch != remote.RequestedSceneGenerationEpoch + 1)
        {
            FailRemotePvpAgreement(
                "host scene-generation request was not the next monotonic epoch",
                true);
            return;
        }
        if (remote.RequestedSceneGenerationEpoch !=
            remote.SceneReadySentEpoch)
        {
            FailRemotePvpAgreement(
                "host advanced the scene-generation epoch before the prior " +
                "acknowledgement completed",
                true);
            return;
        }
        if (remote.SceneReadySentLocalGeneration == ulong.MaxValue)
        {
            FailRemotePvpAgreement(
                "remote scene-generation request mapping overflowed",
                true);
            return;
        }
        remote.RequestedSceneGenerationEpoch = sceneGenerationEpoch;
        remote.RequestedLocalSceneGeneration =
            remote.SceneReadySentLocalGeneration + 1;
        remote.SceneReadySent = false;
        remote.NativeOwnerAdopted = false;
        remote.NativeReadinessInitialized = false;
        remote.DeadlineTimestamp = DeadlineAfter(PvpSceneReadyTimeoutSeconds);
        log.LogInfo("Remote PVP peer accepted scene-generation readiness request: " +
            "epoch=" + sceneGenerationEpoch + ", localGeneration=" +
            remote.LocalSceneGeneration + ", requiredLocalGeneration=" +
            remote.RequestedLocalSceneGeneration + ".");
        TrySendRemotePvpSceneReadyAcknowledgement(remote, allowResend: false);
    }

    private void TrySendRemotePvpSceneReadyAcknowledgement(
        RemotePvpAgreement remote,
        bool allowResend)
    {
        if (remote == null || remote != remotePvpAgreement ||
            !remote.ContentCommitted || !remote.LocalSceneReady ||
            remote.LocalReadySceneGeneration != remote.LocalSceneGeneration ||
            remote.RequestedSceneGenerationEpoch == 0)
        {
            return;
        }
        if (remote.LocalSceneGeneration > remote.RequestedLocalSceneGeneration)
        {
            // A remote scene can complete its replacement before the host sees
            // the corresponding restart callback. If the prior epoch was
            // already acknowledged, retain the new local readiness and wait
            // for the host's next epoch request instead of reusing the old one.
            if (remote.RequestedSceneGenerationEpoch ==
                remote.SceneReadySentEpoch)
            {
                return;
            }
            FailRemotePvpAgreement(
                "remote exact package scene generation advanced beyond the host " +
                "scene-generation request",
                true);
            return;
        }
        if (remote.LocalSceneGeneration < remote.RequestedLocalSceneGeneration)
            return;

        bool alreadySent = remote.SceneReadySent &&
            remote.SceneReadySentEpoch == remote.RequestedSceneGenerationEpoch &&
            remote.SceneReadySentLocalGeneration == remote.LocalSceneGeneration;
        if (alreadySent && !allowResend)
            return;
        if (!alreadySent &&
            remote.RequestedSceneGenerationEpoch <= remote.SceneReadySentEpoch)
        {
            return;
        }

        SendRemotePvpControl(
            PvpAgreementMessageKind.SceneReady,
            string.Empty,
            identityOverride: null,
            sceneGenerationEpoch: remote.RequestedSceneGenerationEpoch);
        if (alreadySent)
        {
            log.LogInfo("Remote PVP peer resent the exact scene-ready " +
                "acknowledgement on host request: sceneGenerationEpoch=" +
                remote.RequestedSceneGenerationEpoch + ", localGeneration=" +
                remote.LocalSceneGeneration + ".");
            return;
        }

        remote.SceneReadySent = true;
        remote.SceneReadySentEpoch = remote.RequestedSceneGenerationEpoch;
        remote.SceneReadySentLocalGeneration = remote.LocalSceneGeneration;
        remote.DeadlineTimestamp = DeadlineAfter(PvpNativeOwnerTimeoutSeconds);
        log.LogInfo("Remote PVP peer constructed and registered the exact native " +
            "PvpGameode template; scene-ready acknowledgement sent.");
        log.LogInfo("Remote PVP scene-ready acknowledgement identity: " +
            "sceneGenerationEpoch=" + remote.SceneReadySentEpoch +
            ", localGeneration=" + remote.SceneReadySentLocalGeneration + ".");
    }

    private static bool IsHostLocalSceneReadyForCurrentEpoch(
        HostPvpAgreement host)
    {
        return host != null && host.SceneGenerationEpoch != 0 &&
            host.LocalSceneReady &&
            host.LocalSceneReadyEpoch == host.SceneGenerationEpoch;
    }

    private static int CountRequiredPvpPeersSceneReadyForCurrentEpoch(
        HostPvpAgreement host)
    {
        if (host == null || host.SceneGenerationEpoch == 0)
            return 0;
        int ready = 0;
        foreach (int connectionId in host.RequiredConnectionIds)
        {
            if (host.SceneReadyEpochByConnectionId.TryGetValue(
                    connectionId,
                    out ulong epoch) &&
                epoch == host.SceneGenerationEpoch)
            {
                ready++;
            }
        }
        return ready;
    }

    private static bool AreRequiredPvpPeersSceneReadyForCurrentEpoch(
        HostPvpAgreement host)
    {
        return host != null && host.RequiredConnectionIds.Count != 0 &&
            CountRequiredPvpPeersSceneReadyForCurrentEpoch(host) ==
            host.RequiredConnectionIds.Count;
    }

    private static bool IsRemoteSceneReadyForCurrentRequest(
        RemotePvpAgreement remote)
    {
        return remote != null && remote.RequestedSceneGenerationEpoch != 0 &&
            remote.SceneReadySent &&
            remote.SceneReadySentEpoch == remote.RequestedSceneGenerationEpoch &&
            remote.SceneReadySentLocalGeneration ==
            remote.RequestedLocalSceneGeneration &&
            remote.LocalSceneGeneration == remote.RequestedLocalSceneGeneration;
    }

    private void NotifyPvpSceneLoading(ActiveMapOperation operation)
    {
        if (operation?.Operation?.Mode != ModdedOperationMode.PlayerVersusPlayer)
            return;
        RemotePvpAgreement remote = remotePvpAgreement;
        if (!NetworkServer.active && remote != null && remote.ContentCommitted &&
            OperationMatchesPvpIdentity(operation, remote.Identity))
        {
            if (operation.SceneHandle == 0)
            {
                FailRemotePvpAgreement(
                    "remote exact package scene load had no scene handle",
                    true);
                operation.NetworkSpawnFailed = true;
                return;
            }
            if (remote.AwaitingLocalSceneGeneration ||
                remote.LocalSceneGeneration == 0 ||
                remote.LastObservedSceneHandle != operation.SceneHandle)
            {
                if (remote.LocalSceneGeneration == ulong.MaxValue)
                {
                    FailRemotePvpAgreement(
                        "remote local scene-generation counter overflowed",
                        true);
                    operation.NetworkSpawnFailed = true;
                    return;
                }
                remote.LocalSceneGeneration++;
                remote.LastObservedSceneHandle = operation.SceneHandle;
                remote.AwaitingLocalSceneGeneration = false;
                remote.LocalSceneReady = false;
                remote.LocalReadySceneGeneration = 0;
                remote.SceneReadySent = false;
                remote.NativeOwnerAdopted = false;
                remote.NativeReadinessInitialized = false;
                log.LogInfo("Remote PVP peer observed exact package scene " +
                    "generation=" + remote.LocalSceneGeneration +
                    ", sceneHandle=" + operation.SceneHandle + ".");
            }
            remote.DeadlineTimestamp = DeadlineAfter(PvpSceneReadyTimeoutSeconds);
        }
    }

    private void NotifyPvpScenePreparationFailed(
        ActiveMapOperation operation,
        string reason)
    {
        if (operation?.Operation?.Mode != ModdedOperationMode.PlayerVersusPlayer)
            return;
        if (NetworkServer.active && hostPvpAgreement != null)
            FailHostPvpAgreement("host PVP scene preparation failed: " + reason, true);
        else if (remotePvpAgreement != null)
            FailRemotePvpAgreement(
                "remote PVP scene preparation failed: " + reason,
                true);
        operation.NetworkSpawnFailed = true;
    }

    private void ResetPvpSceneAgreementForReload(ActiveMapOperation operation)
    {
        if (operation?.Operation?.Mode != ModdedOperationMode.PlayerVersusPlayer)
            return;
        HostPvpAgreement host = hostPvpAgreement;
        if (NetworkServer.active && host != null &&
            OperationMatchesPvpIdentity(operation, host.Identity))
        {
            if (!TryBeginHostPvpSceneGeneration(
                    host,
                    "exact package scene restart",
                    out string generationError))
            {
                FailHostPvpAgreement(generationError, true);
                operation.NetworkSpawnFailed = true;
                return;
            }
        }
        RemotePvpAgreement remote = remotePvpAgreement;
        if (!NetworkServer.active && remote != null &&
            OperationMatchesPvpIdentity(operation, remote.Identity))
        {
            remote.SceneReadySent = false;
            remote.LocalSceneReady = false;
            remote.LocalReadySceneGeneration = 0;
            remote.AwaitingLocalSceneGeneration = true;
            remote.NativeOwnerAdopted = false;
            remote.NativeReadinessInitialized = false;
            remote.DeadlineTimestamp = DeadlineAfter(PvpSceneReadyTimeoutSeconds);
        }
    }

    private bool CompletePvpPeerAgreementOnNativeReturn(
        ActiveMapOperation operation)
    {
        if (operation?.Operation?.Mode != ModdedOperationMode.PlayerVersusPlayer)
            return false;
        string networkSceneName = NetworkManager.networkSceneName;
        if (!string.Equals(
                networkSceneName,
                "Operation Room",
                StringComparison.OrdinalIgnoreCase) &&
            !(networkSceneName ?? string.Empty).EndsWith(
                "/Operation Room.unity",
                StringComparison.OrdinalIgnoreCase) &&
            !(networkSceneName ?? string.Empty).EndsWith(
                "\\Operation Room.unity",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        bool closed = false;
        if (hostPvpAgreement != null && OperationMatchesPvpIdentity(
                operation,
                hostPvpAgreement.Identity))
        {
            hostPvpAgreement = null;
            closed = true;
        }
        if (remotePvpAgreement != null && OperationMatchesPvpIdentity(
                operation,
                remotePvpAgreement.Identity))
        {
            remotePvpAgreement = null;
            closed = true;
        }
        operation.NativeLaunchInvoked = false;
        operation.PvpAbortReturnRequested = false;
        operation.NetworkSpawnFailed = false;
        if (closed)
        {
            log.LogInfo("PVP peer agreement closed after the shipped native " +
                "return to Operation Room; restart state was not armed.");
        }
        else
        {
            log.LogInfo("PVP native return reached Operation Room after its peer " +
                "agreement had already been closed; restart state was not armed.");
        }
        return true;
    }

    private bool IsPvpNetworkSpawnAuthorized(ActiveMapOperation operation)
    {
        if (operation?.Operation?.Mode != ModdedOperationMode.PlayerVersusPlayer)
            return true;
        HostPvpAgreement host = hostPvpAgreement;
        if (!NetworkServer.active || host == null ||
            !OperationMatchesPvpIdentity(operation, host.Identity))
        {
            operation.NetworkSpawnFailed = true;
            return false;
        }
        if (host.Phase != HostPvpAgreementPhase.ReadyToSpawn &&
            host.Phase != HostPvpAgreementPhase.SpawnAuthorized)
        {
            return false;
        }
        string membershipError = string.Empty;
        if (!IsHostLocalSceneReadyForCurrentEpoch(host) ||
            !AreRequiredPvpPeersSceneReadyForCurrentEpoch(host) ||
            !TryValidateHostPvpMembership(host, out membershipError))
        {
            FailHostPvpAgreement(
                string.IsNullOrEmpty(membershipError)
                    ? "PVP scene-ready barrier changed before native spawn"
                    : membershipError,
                true);
            operation.NetworkSpawnFailed = true;
            return false;
        }
        if (DeadlineExpired(host.DeadlineTimestamp))
        {
            FailHostPvpAgreement(
                "timed out authorizing the exact native PVP owner spawn",
                true);
            operation.NetworkSpawnFailed = true;
            return false;
        }
        if (!TryValidateCurrentPvpTeamCapacity(
                operation,
                host.RequiredConnectionIds.Count + 1,
                out string teamCapacityError))
        {
            FailHostPvpAgreement(teamCapacityError, true);
            operation.NetworkSpawnFailed = true;
            return false;
        }
        host.Phase = HostPvpAgreementPhase.SpawnAuthorized;
        return true;
    }

    private static bool TryValidateCurrentPvpTeamCapacity(
        ActiveMapOperation operation,
        int agreedPlayerCount,
        out string error)
    {
        error = string.Empty;
        StandalonePvpGameMode pvp =
            operation?.GameModeComponent as StandalonePvpGameMode;
        GameManagerNetwork network = GameManagerNetwork.instance;
        if (pvp == null || pvp.Team1SpawnPoints == null ||
            pvp.Team2SpawnPoints == null || network == null ||
            network.playerMasters == null)
        {
            error = "native PVP team/spawn registries are unavailable before spawn";
            return false;
        }
        if (network.playerMasters.Count != agreedPlayerCount)
        {
            error = "native PVP player registry count " +
                network.playerMasters.Count + " does not match agreed lobby count " +
                agreedPlayerCount;
            return false;
        }

        int team1Players = 0;
        int team2Players = 0;
        for (int index = 0; index < network.playerMasters.Count; index++)
        {
            PlayerMaster player = network.playerMasters[index];
            int teamId = player?.MyTeamIdentifier?.TeamID ?? 0;
            if (teamId == 1)
                team1Players++;
            else if (teamId == 2)
                team2Players++;
            else
            {
                error = "native PVP player at index " + index +
                    " has unresolved team ID " + teamId;
                return false;
            }
        }
        if (team1Players > pvp.Team1SpawnPoints.Count ||
            team2Players > pvp.Team2SpawnPoints.Count)
        {
            error = "native PVP team population exceeds authored spawn capacity: " +
                "Team1=" + team1Players + "/" + pvp.Team1SpawnPoints.Count +
                ", Team2=" + team2Players + "/" +
                pvp.Team2SpawnPoints.Count;
            return false;
        }
        return true;
    }

    private bool IsPvpNetworkSpawnExpected(ActiveMapOperation operation)
    {
        if (operation?.Operation?.Mode != ModdedOperationMode.PlayerVersusPlayer)
            return true;
        if (NetworkServer.active)
        {
            HostPvpAgreement host = hostPvpAgreement;
            return host != null && OperationMatchesPvpIdentity(operation, host.Identity) &&
                (host.Phase == HostPvpAgreementPhase.ReadyToSpawn ||
                 host.Phase == HostPvpAgreementPhase.SpawnAuthorized);
        }
        RemotePvpAgreement remote = remotePvpAgreement;
        return remote != null && remote.ContentCommitted &&
            IsRemoteSceneReadyForCurrentRequest(remote) &&
            OperationMatchesPvpIdentity(operation, remote.Identity);
    }

    private static bool OperationMatchesPvpIdentity(
        ActiveMapOperation operation,
        PvpPeerIdentity identity)
    {
        return operation != null && identity != null && operation.Map != null &&
            operation.Operation != null && operation.SceneSelection != null &&
            string.Equals(operation.Map.Id, identity.MapId, StringComparison.Ordinal) &&
            string.Equals(operation.Map.PackageId, identity.PackageId,
                StringComparison.Ordinal) &&
            string.Equals(operation.Map.PackageVersion, identity.PackageVersion,
                StringComparison.Ordinal) &&
            string.Equals(operation.Map.PackageContentId, identity.PackageContentId,
                StringComparison.Ordinal) &&
            string.Equals(operation.Operation.Id, identity.OperationId,
                StringComparison.Ordinal) &&
            string.Equals(operation.Operation.SpawnSetId, identity.SpawnSetId,
                StringComparison.Ordinal) &&
            string.Equals(operation.TimeCode, identity.TimeCode,
                StringComparison.Ordinal) &&
            string.Equals(operation.SceneSelection.Id, identity.VariantId,
                StringComparison.Ordinal) &&
            string.Equals(operation.SceneSelection.ScenePath, identity.ScenePath,
                StringComparison.Ordinal);
    }

    private Dictionary<int, NetworkConnectionToClient>
        CaptureAuthenticatedRemotePvpConnections(bool requireReady)
    {
        if (!NetworkServer.active || NetworkServer.connections == null)
            throw new InvalidOperationException(
                "Mirror server connection registry is unavailable");
        var result = new Dictionary<int, NetworkConnectionToClient>();
        int localConnectionId = NetworkServer.localConnection == null
            ? NetworkConnection.LocalConnectionId
            : NetworkServer.localConnection.connectionId;
        var enumerator = NetworkServer.connections.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                var entry = enumerator.Current;
                NetworkConnectionToClient connection = entry.Value;
                if (connection == null || entry.Key == localConnectionId ||
                    connection is LocalConnectionToClient)
                {
                    continue;
                }
                if (!connection.isAuthenticated ||
                    (requireReady && !connection.isReady))
                {
                    throw new InvalidOperationException(
                        "remote peer " + entry.Key +
                        " is not authenticated and ready");
                }
                result.Add(entry.Key, connection);
            }
        }
        finally
        {
            enumerator.Dispose();
        }
        return result;
    }

    private bool TryValidateHostPvpMembership(
        HostPvpAgreement host,
        out string error)
    {
        error = string.Empty;
        try
        {
            Dictionary<int, NetworkConnectionToClient> current =
                CaptureAuthenticatedRemotePvpConnections(requireReady: false);
            if (!host.RequiredConnectionIds.SetEquals(current.Keys))
            {
                error = "PVP lobby membership changed during peer agreement";
                return false;
            }
            foreach (int connectionId in host.RequiredConnectionIds)
            {
                if (!host.RequiredConnections.TryGetValue(
                        connectionId,
                        out NetworkConnectionToClient required) ||
                    !current.TryGetValue(
                        connectionId,
                        out NetworkConnectionToClient connected) ||
                    !SamePvpNetworkConnection(required, connected))
                {
                    error = "PVP lobby connection identity changed during peer " +
                        "agreement for connection=" + connectionId;
                    return false;
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            error = "PVP lobby membership validation failed: " + ex.Message;
            return false;
        }
    }

    private static bool SamePvpNetworkConnection(
        NetworkConnection left,
        NetworkConnection right)
    {
        if (ReferenceEquals(left, right))
            return true;
        try { return left != null && right != null && left.Pointer == right.Pointer; }
        catch { return false; }
    }

    private void FailHostPvpAgreement(string reason, bool sendCancel)
    {
        HostPvpAgreement host = hostPvpAgreement;
        if (host == null)
            return;
        if (sendCancel)
        {
            try { BroadcastHostPvpControl(PvpAgreementMessageKind.Cancel, reason); }
            catch { }
        }
        if (activeOperation != null &&
            OperationMatchesPvpIdentity(activeOperation, host.Identity))
        {
            activeOperation.NetworkSpawnFailed = true;
            if (host.NativeLaunchInvoked || activeOperation.NativeLaunchInvoked)
                RequestNativePvpAbortReturn(activeOperation);
        }
        SetNativeConfirmationLoadingState(host.Presentation, false);
        hostPvpAgreement = null;
        log.LogError("PVP peer agreement failed closed: " + reason + ".");
    }

    private void FailRemotePvpAgreement(string reason, bool sendReject)
    {
        RemotePvpAgreement remote = remotePvpAgreement;
        if (remote == null)
            return;
        if (sendReject)
        {
            try { SendRemotePvpControl(PvpAgreementMessageKind.Reject, reason); }
            catch { }
        }
        ClearRemotePvpAgreement(reason);
        log.LogError("Remote PVP peer agreement failed closed: " + reason + ".");
    }

    private void ClearRemotePvpAgreement(string reason)
    {
        RemotePvpAgreement remote = remotePvpAgreement;
        if (remote != null && activeOperation != null &&
            OperationMatchesPvpIdentity(activeOperation, remote.Identity))
        {
            if (!activeOperation.NativeLaunchInvoked &&
                activeOperation.SceneHandle == 0 &&
                !remote.ContentCommitted)
            {
                activeOperation = null;
            }
            else
            {
                activeOperation.NetworkSpawnFailed = true;
                RequestNativePvpAbortReturn(activeOperation);
            }
        }
        remotePvpAgreement = null;
    }

    private void RequestNativePvpAbortReturn(ActiveMapOperation operation)
    {
        if (operation == null || operation.PvpAbortReturnRequested)
            return;
        operation.PvpAbortReturnRequested = true;

        if (NetworkServer.active && GameManagerNetwork.instance != null)
        {
            try
            {
                // Exact installed current-build owner at RVA 0x0090DC00. The
                // owned host body performs its normal pre-return cleanup and
                // invokes the shipped network scene change to "Operation Room".
                GameManagerNetwork.instance.EndOperation();
                log.LogWarning("PVP agreement abort entered the shipped " +
                    "GameManagerNetwork.EndOperation return to Operation Room.");
                return;
            }
            catch (Exception ex)
            {
                log.LogError("PVP agreement abort could not enter the shipped " +
                    "Operation Room return: " + ex.GetType().Name + ": " +
                    ex.Message + ".");
            }
        }

        if (!NetworkServer.active && NetworkClient.active)
        {
            try
            {
                NetworkClient.Disconnect();
                log.LogWarning("PVP agreement abort disconnected the remote " +
                    "peer before exact local scene teardown.");
            }
            catch (Exception ex)
            {
                log.LogError("PVP agreement abort could not disconnect the " +
                    "remote peer: " + ex.GetType().Name + ": " + ex.Message + ".");
            }
        }

        Scene scene = FindLoadedSceneByHandle(operation.SceneHandle);
        if (scene.IsValid() && scene.isLoaded)
        {
            try
            {
                SceneManager.UnloadSceneAsync(scene);
                log.LogWarning("PVP agreement abort requested exact local scene " +
                    "unload because the native network return was unavailable: " +
                    "scene=" + scene.name + ".");
            }
            catch (Exception ex)
            {
                log.LogError("PVP agreement abort exact-scene unload failed: " +
                    ex.GetType().Name + ": " + ex.Message + ".");
            }
        }
    }

    private void FailActivePvpNativeLifecycle(
        ActiveMapOperation operation,
        string reason)
    {
        if (operation?.Operation?.Mode != ModdedOperationMode.PlayerVersusPlayer)
            return;
        operation.NetworkSpawnFailed = true;
        if (NetworkServer.active && hostPvpAgreement != null &&
            OperationMatchesPvpIdentity(operation, hostPvpAgreement.Identity))
        {
            FailHostPvpAgreement("host native PVP lifecycle failed: " + reason, true);
            return;
        }
        if (!NetworkServer.active && remotePvpAgreement != null &&
            OperationMatchesPvpIdentity(operation, remotePvpAgreement.Identity))
        {
            FailRemotePvpAgreement(
                "remote native PVP lifecycle failed: " + reason,
                true);
            return;
        }
        RequestNativePvpAbortReturn(operation);
    }

    private void NotifyPvpNativeLaunchInvoked(ActiveMapOperation operation)
    {
        HostPvpAgreement host = hostPvpAgreement;
        if (operation?.Operation?.Mode == ModdedOperationMode.PlayerVersusPlayer &&
            NetworkServer.active && host != null &&
            OperationMatchesPvpIdentity(operation, host.Identity))
        {
            host.NativeLaunchInvoked = true;
        }
    }

    private void NotifyPvpNativeOwnerAdopted(ActiveMapOperation operation)
    {
        if (operation?.Operation?.Mode != ModdedOperationMode.PlayerVersusPlayer)
            return;
        RemotePvpAgreement remote = remotePvpAgreement;
        if (!NetworkServer.active && remote != null &&
            IsRemoteSceneReadyForCurrentRequest(remote) &&
            OperationMatchesPvpIdentity(operation, remote.Identity))
        {
            remote.NativeOwnerAdopted = true;
            remote.DeadlineTimestamp = DeadlineAfter(PvpNativeLifecycleTimeoutSeconds);
            log.LogInfo("Remote PVP peer adopted the agreed native owner; awaiting " +
                "the shipped readiness lifecycle.");
        }
    }

    private void NotifyPvpNativeReadinessInitialized(ActiveMapOperation operation)
    {
        if (operation?.Operation?.Mode != ModdedOperationMode.PlayerVersusPlayer)
            return;
        RemotePvpAgreement remote = remotePvpAgreement;
        if (!NetworkServer.active && remote != null &&
            IsRemoteSceneReadyForCurrentRequest(remote) &&
            OperationMatchesPvpIdentity(operation, remote.Identity))
        {
            remote.NativeOwnerAdopted = true;
            remote.NativeReadinessInitialized = true;
            remote.DeadlineTimestamp = 0;
            log.LogInfo("Remote PVP peer completed the agreed native readiness " +
                "lifecycle.");
        }
    }

    private void NotifyPvpNetworkOwnerSpawned(ActiveMapOperation operation)
    {
        HostPvpAgreement host = hostPvpAgreement;
        if (operation?.Operation?.Mode != ModdedOperationMode.PlayerVersusPlayer ||
            !NetworkServer.active || host == null ||
            !OperationMatchesPvpIdentity(operation, host.Identity))
        {
            return;
        }
        host.NativeLifecycleDeadlineTimestamp = operation.AllPlayersLoaded
            ? 0
            : DeadlineAfter(PvpNativeLifecycleTimeoutSeconds);
        log.LogInfo("PVP native owner spawned; bounded native readiness lifecycle " +
            (operation.AllPlayersLoaded ? "was already complete" : "started") +
            " for operation=" + operation.Operation.Id + ".");
    }

    private void NotifyPvpAllPlayersLoaded(ActiveMapOperation operation)
    {
        HostPvpAgreement host = hostPvpAgreement;
        if (operation?.Operation?.Mode == ModdedOperationMode.PlayerVersusPlayer &&
            NetworkServer.active && host != null &&
            OperationMatchesPvpIdentity(operation, host.Identity))
        {
            host.NativeLifecycleDeadlineTimestamp = 0;
            log.LogInfo("PVP shipped all-players-loaded lifecycle passed before " +
                "the native-start deadline.");
        }
    }

    private void SendPvpOffer(
        NetworkConnection connection,
        PvpPeerIdentity identity)
    {
        SendPvpEnvelope(connection, writer =>
        {
            NetworkWriterExtensions.WriteUShort(
                writer,
                PvpAgreementProtocolVersion);
            NetworkWriterExtensions.WriteByte(
                writer,
                (byte)PvpAgreementMessageKind.Offer);
            WritePvpIdentity(writer, identity);
        });
    }

    private void BroadcastHostPvpControl(
        PvpAgreementMessageKind kind,
        string reason)
    {
        HostPvpAgreement host = hostPvpAgreement;
        if (host == null || !NetworkServer.active)
            return;
        Dictionary<int, NetworkConnectionToClient> connections =
            CaptureAuthenticatedRemotePvpConnections(requireReady: false);
        foreach (int id in host.RequiredConnectionIds)
        {
            if (connections.TryGetValue(id, out NetworkConnectionToClient connection) &&
                host.RequiredConnections.TryGetValue(
                    id,
                    out NetworkConnectionToClient required) &&
                SamePvpNetworkConnection(required, connection))
            {
                SendPvpControl(connection, kind, host.Identity, reason);
            }
        }
    }

    private void BroadcastHostPvpSceneReadyRequest(
        HostPvpAgreement host,
        bool isRetry)
    {
        if (host == null || host != hostPvpAgreement || !NetworkServer.active ||
            host.SceneGenerationEpoch == 0)
        {
            throw new InvalidOperationException(
                "host scene-generation request lost agreement ownership");
        }
        Dictionary<int, NetworkConnectionToClient> connections =
            CaptureAuthenticatedRemotePvpConnections(requireReady: false);
        foreach (int connectionId in host.RequiredConnectionIds)
        {
            if (!connections.TryGetValue(
                    connectionId,
                    out NetworkConnectionToClient connection) ||
                !host.RequiredConnections.TryGetValue(
                    connectionId,
                    out NetworkConnectionToClient required) ||
                !SamePvpNetworkConnection(required, connection))
            {
                throw new InvalidOperationException(
                    "required PVP peer " + connectionId +
                    " was unavailable for scene-generation request");
            }
            SendHostPvpSceneReadyRequest(connection, host, isRetry);
        }
        host.SceneReadyRequestRetryTimestamp =
            DeadlineAfter(PvpSceneReadyRequestRetrySeconds);
        if (isRetry)
        {
            log.LogInfo("PVP scene-generation readiness request resent: epoch=" +
                host.SceneGenerationEpoch + ", ready=" +
                CountRequiredPvpPeersSceneReadyForCurrentEpoch(host) + "/" +
                host.RequiredConnectionIds.Count + ".");
        }
    }

    private void SendHostPvpSceneReadyRequest(
        NetworkConnection connection,
        HostPvpAgreement host,
        bool isRetry)
    {
        if (connection == null || host == null || host.Identity == null ||
            host.SceneGenerationEpoch == 0)
        {
            throw new InvalidOperationException(
                "host scene-generation request was incomplete");
        }
        SendPvpControl(
            connection,
            PvpAgreementMessageKind.SceneReadyRequest,
            host.Identity,
            string.Empty,
            host.SceneGenerationEpoch);
    }

    private void SendRemotePvpControl(
        PvpAgreementMessageKind kind,
        string reason,
        PvpPeerIdentity identityOverride = null,
        ulong sceneGenerationEpoch = 0)
    {
        PvpPeerIdentity identity = identityOverride ?? remotePvpAgreement?.Identity;
        if (identity == null || !NetworkClient.active ||
            NetworkClient.connection == null)
        {
            return;
        }
        SendPvpControl(
            NetworkClient.connection,
            kind,
            identity,
            reason,
            sceneGenerationEpoch);
    }

    private void SendPvpControl(
        NetworkConnection connection,
        PvpAgreementMessageKind kind,
        PvpPeerIdentity identity,
        string reason,
        ulong sceneGenerationEpoch = 0)
    {
        SendPvpEnvelope(connection, writer =>
        {
            NetworkWriterExtensions.WriteUShort(
                writer,
                PvpAgreementProtocolVersion);
            NetworkWriterExtensions.WriteByte(writer, (byte)kind);
            NetworkWriterExtensions.WriteString(writer, identity.Nonce ?? string.Empty);
            NetworkWriterExtensions.WriteString(writer, identity.Digest ?? string.Empty);
            NetworkWriterExtensions.WriteULong(writer, sceneGenerationEpoch);
            NetworkWriterExtensions.WriteString(
                writer,
                BoundPvpString(reason, PvpAgreementMaxReasonString));
        });
    }

    private static void SendPvpEnvelope(
        NetworkConnection connection,
        Action<NetworkWriter> writePayload)
    {
        if (connection == null || writePayload == null)
            throw new ArgumentNullException(nameof(connection));
        NetworkWriterPooled writer = NetworkWriterPool.Get();
        try
        {
            NetworkWriterExtensions.WriteUShort(writer, PvpAgreementMessageId);
            writePayload(writer);
            if (writer.Position > PvpAgreementMaxEnvelopeBytes)
                throw new InvalidOperationException(
                    "PVP agreement envelope exceeded its byte limit");
            connection.Send(writer.ToArraySegment(), Channels.Reliable);
        }
        finally
        {
            NetworkWriterPool.Return(writer);
        }
    }

    private static PvpPeerIdentity CreatePvpPeerIdentity(
        ModdedMapDefinition map,
        ModdedOperationDefinition operation,
        string timeCode,
        SceneVariantSelection selection,
        string nonce)
    {
        if (!TryResolveLocalRuntimeIdentity(map,
                out RuntimeBinaryIdentity runtime,
                out string runtimeError))
        {
            throw new InvalidOperationException(
                "exact runtime binary identity failed closed: " + runtimeError);
        }
        if (string.IsNullOrWhiteSpace(nonce) || nonce.Length > 64 ||
            map == null || operation == null || selection == null ||
            !SelectionBelongsToMap(map, selection) ||
            operation.Mode != ModdedOperationMode.PlayerVersusPlayer ||
            !string.Equals(operation.MapId, map.Id, StringComparison.Ordinal) ||
            !operation.SupportedTimeCodes.Contains(timeCode, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "local PVP offer identity was incomplete or not owned by the map");
        }
        var identity = new PvpPeerIdentity
        {
            Nonce = nonce,
            FrameworkVersion = PvpAgreementFrameworkVersion,
            FrameworkSha256 = runtime.FrameworkSha256,
            ApiVersion = OperatorApi.ApiVersion,
            ApiCoreSha256 = runtime.ApiCoreSha256,
            ApiHostSha256 = runtime.ApiHostSha256,
            GameBuildId = OperatorApi.Compatibility.DetectedGameBuildId,
            Capabilities = PvpAgreementCapabilities,
            PackageId = map.PackageId,
            PackageVersion = map.PackageVersion,
            PackageContentId = map.PackageContentId,
            MapId = map.Id,
            OperationId = operation.Id,
            Mode = (int)operation.Mode,
            SpawnSetId = operation.SpawnSetId,
            VariantId = selection.Id,
            ScenePath = selection.ScenePath,
            TimeCode = timeCode,
            MinimumPlayers = operation.MinimumPlayers,
            MaximumPlayers = operation.MaximumPlayers,
            CompanionPluginGuid = runtime.CompanionPluginGuid,
            CompanionPluginVersion = runtime.CompanionPluginVersion,
            CompanionSha256 = runtime.CompanionSha256,
            CompanionReadyMarkerName = runtime.CompanionReadyMarkerName,
            CompanionFailureMarkerName = runtime.CompanionFailureMarkerName
        };
        identity.Digest = ComputePvpIdentityDigest(identity);
        return identity;
    }

    private static void WritePvpIdentity(
        NetworkWriter writer,
        PvpPeerIdentity identity)
    {
        NetworkWriterExtensions.WriteString(writer, identity.Nonce);
        NetworkWriterExtensions.WriteString(writer, identity.FrameworkVersion);
        NetworkWriterExtensions.WriteString(writer, identity.FrameworkSha256);
        NetworkWriterExtensions.WriteString(writer, identity.ApiVersion);
        NetworkWriterExtensions.WriteString(writer, identity.ApiCoreSha256);
        NetworkWriterExtensions.WriteString(writer, identity.ApiHostSha256);
        NetworkWriterExtensions.WriteString(writer, identity.GameBuildId);
        NetworkWriterExtensions.WriteString(writer, identity.Capabilities);
        NetworkWriterExtensions.WriteString(writer, identity.PackageId);
        NetworkWriterExtensions.WriteString(writer, identity.PackageVersion);
        NetworkWriterExtensions.WriteString(writer, identity.PackageContentId);
        NetworkWriterExtensions.WriteString(writer, identity.MapId);
        NetworkWriterExtensions.WriteString(writer, identity.OperationId);
        NetworkWriterExtensions.WriteInt(writer, identity.Mode);
        NetworkWriterExtensions.WriteString(writer, identity.SpawnSetId);
        NetworkWriterExtensions.WriteString(writer, identity.VariantId);
        NetworkWriterExtensions.WriteString(writer, identity.ScenePath);
        NetworkWriterExtensions.WriteString(writer, identity.TimeCode);
        NetworkWriterExtensions.WriteInt(writer, identity.MinimumPlayers);
        NetworkWriterExtensions.WriteInt(writer, identity.MaximumPlayers);
        NetworkWriterExtensions.WriteString(writer, identity.CompanionPluginGuid);
        NetworkWriterExtensions.WriteString(writer, identity.CompanionPluginVersion);
        NetworkWriterExtensions.WriteString(writer, identity.CompanionSha256);
        NetworkWriterExtensions.WriteString(writer, identity.CompanionReadyMarkerName);
        NetworkWriterExtensions.WriteString(writer, identity.CompanionFailureMarkerName);
        NetworkWriterExtensions.WriteString(writer, identity.Digest);
    }

    private static PvpPeerIdentity ReadPvpIdentity(NetworkReader reader)
    {
        var identity = new PvpPeerIdentity
        {
            Nonce = ReadBoundedPvpString(reader, 64, "nonce"),
            FrameworkVersion = ReadBoundedPvpString(reader, 64, "frameworkVersion"),
            FrameworkSha256 = ReadPvpSha256(reader, "frameworkSha256"),
            ApiVersion = ReadBoundedPvpString(reader, 64, "apiVersion"),
            ApiCoreSha256 = ReadPvpSha256(reader, "apiCoreSha256"),
            ApiHostSha256 = ReadPvpSha256(reader, "apiHostSha256"),
            GameBuildId = ReadBoundedPvpString(reader, 128, "gameBuildId"),
            Capabilities = ReadBoundedPvpString(reader, 256, "capabilities"),
            PackageId = ReadBoundedPvpString(reader, 256, "packageId"),
            PackageVersion = ReadBoundedPvpString(reader, 64, "packageVersion"),
            PackageContentId = ReadBoundedPvpString(reader, 128, "packageContentId"),
            MapId = ReadBoundedPvpString(reader, 256, "mapId"),
            OperationId = ReadBoundedPvpString(reader, 256, "operationId"),
            Mode = NetworkReaderExtensions.ReadInt(reader),
            SpawnSetId = ReadBoundedPvpString(reader, 256, "spawnSetId"),
            VariantId = ReadBoundedPvpString(reader, 256, "variantId"),
            ScenePath = ReadBoundedPvpString(
                reader,
                PvpAgreementMaxIdentityString,
                "scenePath"),
            TimeCode = ReadBoundedPvpString(reader, 16, "timeCode"),
            MinimumPlayers = NetworkReaderExtensions.ReadInt(reader),
            MaximumPlayers = NetworkReaderExtensions.ReadInt(reader),
            CompanionPluginGuid = ReadBoundedPvpString(
                reader, 128, "companionPluginGuid"),
            CompanionPluginVersion = ReadBoundedPvpString(
                reader, 64, "companionPluginVersion"),
            CompanionSha256 = ReadBoundedPvpString(
                reader, 64, "companionSha256"),
            CompanionReadyMarkerName = ReadBoundedPvpString(
                reader, 128, "companionReadyMarkerName"),
            CompanionFailureMarkerName = ReadBoundedPvpString(
                reader, 128, "companionFailureMarkerName"),
            Digest = ReadBoundedPvpString(reader, 128, "digest")
        };
        ValidatePvpCompanionIdentity(identity);
        return identity;
    }

    private static string ComputePvpIdentityDigest(PvpPeerIdentity identity)
    {
        string canonical = string.Join("\n", new[]
        {
            PvpAgreementProtocolVersion.ToString(CultureInfo.InvariantCulture),
            identity.Nonce ?? string.Empty,
            identity.FrameworkVersion ?? string.Empty,
            identity.FrameworkSha256 ?? string.Empty,
            identity.ApiVersion ?? string.Empty,
            identity.ApiCoreSha256 ?? string.Empty,
            identity.ApiHostSha256 ?? string.Empty,
            identity.GameBuildId ?? string.Empty,
            identity.Capabilities ?? string.Empty,
            identity.PackageId ?? string.Empty,
            identity.PackageVersion ?? string.Empty,
            identity.PackageContentId ?? string.Empty,
            identity.MapId ?? string.Empty,
            identity.OperationId ?? string.Empty,
            identity.Mode.ToString(CultureInfo.InvariantCulture),
            identity.SpawnSetId ?? string.Empty,
            identity.VariantId ?? string.Empty,
            identity.ScenePath ?? string.Empty,
            identity.TimeCode ?? string.Empty,
            identity.MinimumPlayers.ToString(CultureInfo.InvariantCulture),
            identity.MaximumPlayers.ToString(CultureInfo.InvariantCulture),
            identity.CompanionPluginGuid ?? string.Empty,
            identity.CompanionPluginVersion ?? string.Empty,
            identity.CompanionSha256 ?? string.Empty,
            identity.CompanionReadyMarkerName ?? string.Empty,
            identity.CompanionFailureMarkerName ?? string.Empty
        });
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        var result = new StringBuilder(digest.Length * 2);
        foreach (byte value in digest)
            result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }

    private static bool TryResolveLocalRuntimeIdentity(
        ModdedMapDefinition map,
        out RuntimeBinaryIdentity identity,
        out string error)
    {
        identity = null;
        error = string.Empty;
        try
        {
            if (!TryHashLoadedAssembly(
                    typeof(CerberusNativeTabFix).Assembly,
                    "OperatorModdedOperations",
                    null,
                    out string frameworkSha,
                    out error))
            {
                return false;
            }
            if (!TryHashLoadedAssembly(
                    typeof(OperatorApi).Assembly,
                    "OperatorModAPI",
                    null,
                    out string apiCoreSha,
                    out error))
            {
                return false;
            }
            if (!TryResolveLoadedPlugin(
                    PvpApiPluginGuid,
                    OperatorApi.ApiVersion,
                    PvpApiHostTypeName,
                    "OperatorModAPI.BepInEx",
                    null,
                    out string apiHostSha,
                    out error))
            {
                return false;
            }

            var resolved = new RuntimeBinaryIdentity
            {
                FrameworkSha256 = frameworkSha,
                ApiCoreSha256 = apiCoreSha,
                ApiHostSha256 = apiHostSha,
                CompanionPluginGuid = PvpNoCompanionIdentity,
                CompanionPluginVersion = PvpNoCompanionIdentity,
                CompanionSha256 = PvpNoCompanionIdentity,
                CompanionReadyMarkerName = PvpNoCompanionIdentity,
                CompanionFailureMarkerName = PvpNoCompanionIdentity
            };
            ModdedRuntimeCompanionDefinition companion = map?.RuntimeCompanion;
            if (companion != null)
            {
                if (!TryResolveLoadedPlugin(
                        companion.PluginGuid,
                        companion.PluginVersion,
                        expectedTypeName: null,
                        expectedAssemblyName: null,
                        companion.Sha256,
                        out string companionSha,
                        out error))
                {
                    return false;
                }
                resolved.CompanionPluginGuid = companion.PluginGuid;
                resolved.CompanionPluginVersion = companion.PluginVersion;
                resolved.CompanionSha256 = companionSha;
                resolved.CompanionReadyMarkerName = companion.ReadyMarkerName;
                resolved.CompanionFailureMarkerName = companion.FailureMarkerName;
            }
            identity = resolved;
            return true;
        }
        catch (Exception exception) when (exception is IOException or
                                             UnauthorizedAccessException or
                                             ArgumentException or
                                             InvalidOperationException or
                                             CryptographicException or
                                             BadImageFormatException)
        {
            error = exception.GetType().Name + ": " + exception.Message;
            return false;
        }
    }

    private static bool TryResolveLoadedPlugin(
        string pluginGuid,
        string expectedVersion,
        string expectedTypeName,
        string expectedAssemblyName,
        string expectedSha256,
        out string sha256,
        out string error)
    {
        sha256 = string.Empty;
        error = string.Empty;
        IL2CPPChainloader chainloader = IL2CPPChainloader.Instance;
        if (chainloader?.Plugins == null ||
            !chainloader.Plugins.TryGetValue(pluginGuid, out PluginInfo plugin) ||
            plugin == null || plugin.Metadata == null || plugin.Instance == null)
        {
            error = "required loaded plugin '" + pluginGuid + "' is unavailable";
            return false;
        }
        PropertyInfo versionProperty = plugin.Metadata.GetType().GetProperty(
            "Version",
            BindingFlags.Public | BindingFlags.Instance);
        string loadedVersion = versionProperty?.GetValue(plugin.Metadata)?.ToString();
        if (!string.Equals(plugin.Metadata.GUID, pluginGuid, StringComparison.Ordinal) ||
            !string.Equals(loadedVersion, expectedVersion, StringComparison.Ordinal))
        {
            error = "loaded plugin GUID/version mismatch for '" + pluginGuid + "'";
            return false;
        }
        Type pluginType = plugin.Instance.GetType();
        if (!string.Equals(plugin.TypeName, pluginType.FullName,
                StringComparison.Ordinal))
        {
            error = "loaded plugin registry/type identity mismatch for '" +
                pluginGuid + "'";
            return false;
        }
        if (expectedTypeName != null &&
            !string.Equals(pluginType.FullName, expectedTypeName,
                StringComparison.Ordinal))
        {
            error = "loaded plugin type mismatch for '" + pluginGuid + "'";
            return false;
        }
        return TryHashLoadedAssembly(
            pluginType.Assembly,
            expectedAssemblyName,
            plugin.Location,
            out sha256,
            out error) &&
            (expectedSha256 == null ||
             RequireExpectedPvpSha256(pluginGuid, sha256, expectedSha256, out error));
    }

    private static bool RequireExpectedPvpSha256(
        string pluginGuid,
        string actual,
        string expected,
        out string error)
    {
        if (IsLowercasePvpSha256(expected) &&
            string.Equals(actual, expected, StringComparison.Ordinal))
        {
            error = string.Empty;
            return true;
        }
        error = "loaded plugin SHA-256 mismatch for '" + pluginGuid + "'";
        return false;
    }

    private static bool TryHashLoadedAssembly(
        Assembly assembly,
        string expectedAssemblyName,
        string authoritativePath,
        out string sha256,
        out string error)
    {
        sha256 = string.Empty;
        error = string.Empty;
        if (assembly == null)
        {
            error = "loaded assembly is unavailable";
            return false;
        }
        string assemblyName = assembly.GetName().Name ?? string.Empty;
        if (expectedAssemblyName != null && !string.Equals(
                assemblyName,
                expectedAssemblyName,
                StringComparison.Ordinal))
        {
            error = "loaded assembly name mismatch for '" + expectedAssemblyName + "'";
            return false;
        }
        string loadedPath = assembly.Location;
        string candidatePath = string.IsNullOrWhiteSpace(authoritativePath)
            ? loadedPath
            : authoritativePath;
        if (string.IsNullOrWhiteSpace(candidatePath) ||
            !Path.IsPathFullyQualified(candidatePath))
        {
            error = "loaded assembly path is unavailable for '" + assemblyName + "'";
            return false;
        }
        candidatePath = Path.GetFullPath(candidatePath);
        if (!string.IsNullOrWhiteSpace(loadedPath) && !string.Equals(
                Path.GetFullPath(loadedPath),
                candidatePath,
                StringComparison.OrdinalIgnoreCase))
        {
            error = "plugin registry path differs from the loaded assembly path for '" +
                assemblyName + "'";
            return false;
        }
        using var stream = new FileStream(
            candidatePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            65536,
            FileOptions.SequentialScan);
        if (stream.Length <= 0 || stream.Length > 128L * 1024 * 1024)
        {
            error = "loaded assembly byte length is invalid for '" + assemblyName + "'";
            return false;
        }
        using (var pe = new PEReader(stream, PEStreamOptions.LeaveOpen))
        {
            if (!pe.HasMetadata)
            {
                error = "loaded assembly metadata is unavailable for '" + assemblyName + "'";
                return false;
            }
            MetadataReader metadata = pe.GetMetadataReader();
            string fileAssemblyName = metadata.GetString(
                metadata.GetAssemblyDefinition().Name);
            Guid fileMvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
            if (!string.Equals(fileAssemblyName, assemblyName, StringComparison.Ordinal) ||
                fileMvid != assembly.ManifestModule.ModuleVersionId)
            {
                error = "loaded assembly path does not match its in-memory module for '" +
                    assemblyName + "'";
                return false;
            }
        }
        stream.Position = 0;
        using SHA256 algorithm = SHA256.Create();
        sha256 = ToLowerHex(algorithm.ComputeHash(stream));
        return IsLowercasePvpSha256(sha256);
    }

    private static string ToLowerHex(byte[] bytes)
    {
        var result = new StringBuilder(bytes.Length * 2);
        foreach (byte value in bytes)
            result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }

    private static string ReadPvpSha256(NetworkReader reader, string field)
    {
        string value = ReadBoundedPvpString(reader, 64, field);
        if (!IsLowercasePvpSha256(value))
            throw new InvalidOperationException(field + " is not a lowercase SHA-256");
        return value;
    }

    private static bool IsLowercasePvpSha256(string value) =>
        value != null && value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void ValidatePvpCompanionIdentity(PvpPeerIdentity identity)
    {
        bool none = string.Equals(
            identity.CompanionPluginGuid,
            PvpNoCompanionIdentity,
            StringComparison.Ordinal);
        if (none)
        {
            if (!string.Equals(identity.CompanionPluginVersion,
                    PvpNoCompanionIdentity, StringComparison.Ordinal) ||
                !string.Equals(identity.CompanionSha256,
                    PvpNoCompanionIdentity, StringComparison.Ordinal) ||
                !string.Equals(identity.CompanionReadyMarkerName,
                    PvpNoCompanionIdentity, StringComparison.Ordinal) ||
                !string.Equals(identity.CompanionFailureMarkerName,
                    PvpNoCompanionIdentity, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "companion none identity is internally inconsistent");
            }
            return;
        }
        if (string.IsNullOrWhiteSpace(identity.CompanionPluginGuid) ||
            string.IsNullOrWhiteSpace(identity.CompanionPluginVersion) ||
            string.IsNullOrWhiteSpace(identity.CompanionReadyMarkerName) ||
            string.IsNullOrWhiteSpace(identity.CompanionFailureMarkerName) ||
            !string.Equals(identity.CompanionPluginGuid,
                identity.CompanionPluginGuid.Trim(), StringComparison.Ordinal) ||
            !string.Equals(identity.CompanionPluginVersion,
                identity.CompanionPluginVersion.Trim(), StringComparison.Ordinal) ||
            !string.Equals(identity.CompanionReadyMarkerName,
                identity.CompanionReadyMarkerName.Trim(), StringComparison.Ordinal) ||
            !string.Equals(identity.CompanionFailureMarkerName,
                identity.CompanionFailureMarkerName.Trim(), StringComparison.Ordinal) ||
            !IsLowercasePvpSha256(identity.CompanionSha256) ||
            string.Equals(identity.CompanionReadyMarkerName,
                identity.CompanionFailureMarkerName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "companion SHA-256 or marker identity is invalid");
        }
    }

    private static bool PvpCompanionIdentityMatches(
        PvpPeerIdentity offer,
        RuntimeBinaryIdentity local) =>
        string.Equals(offer.CompanionPluginGuid, local.CompanionPluginGuid,
            StringComparison.Ordinal) &&
        string.Equals(offer.CompanionPluginVersion, local.CompanionPluginVersion,
            StringComparison.Ordinal) &&
        string.Equals(offer.CompanionSha256, local.CompanionSha256,
            StringComparison.Ordinal) &&
        string.Equals(offer.CompanionReadyMarkerName,
            local.CompanionReadyMarkerName, StringComparison.Ordinal) &&
        string.Equals(offer.CompanionFailureMarkerName,
            local.CompanionFailureMarkerName, StringComparison.Ordinal);

    private static void ValidatePvpEnvelopeReader(NetworkReader reader)
    {
        if (reader == null || reader.Remaining < 3 ||
            reader.Remaining > PvpAgreementMaxEnvelopeBytes)
        {
            throw new InvalidOperationException(
                "PVP agreement envelope length is invalid");
        }
    }

    private static void RequirePvpEnvelopeConsumed(NetworkReader reader)
    {
        if (reader.Remaining != 0)
            throw new InvalidOperationException(
                "PVP agreement envelope has trailing data");
    }

    private static string ReadBoundedPvpString(
        NetworkReader reader,
        int maximumLength,
        string field,
        bool allowEmpty = false)
    {
        string value = NetworkReaderExtensions.ReadString(reader) ?? string.Empty;
        if ((!allowEmpty && value.Length == 0) || value.Length > maximumLength)
            throw new InvalidOperationException(field + " length is invalid");
        return value;
    }

    private static string BoundPvpString(string value, int maximumLength)
    {
        value ??= string.Empty;
        return value.Length <= maximumLength
            ? value
            : value.Substring(0, maximumLength);
    }

    private static long DeadlineAfter(double seconds)
    {
        return System.Diagnostics.Stopwatch.GetTimestamp() +
            (long)(seconds * System.Diagnostics.Stopwatch.Frequency);
    }

    private static bool DeadlineExpired(long deadlineTimestamp)
    {
        return deadlineTimestamp > 0 &&
            System.Diagnostics.Stopwatch.GetTimestamp() >= deadlineTimestamp;
    }
}
