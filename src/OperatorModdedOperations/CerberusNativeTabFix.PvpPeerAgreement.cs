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
using System.Text.Json;
using System.Text.Json.Serialization;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Mirror;
using OperatorModAPI;
using OperatorModdedOperations;
using UnityEngine.SceneManagement;
#if MELONLOADER
using MelonLoader;
#endif

public sealed partial class CerberusNativeTabFix
{
    private const string PvpAgreementFrameworkVersion = "0.3.30";
    private const string PvpAgreementCapabilities =
        "exact-content-v2;suite-install-receipt-v1;loader-neutral-runtime-pair-v1;" +
        "package-runtime-ready-v1;remote-preload-v1;client-hello-v1;" +
        "scene-ready-epoch-v1;scene-contract-digest-v2;" +
        "runtime-clone-contract-v1;owner-placement-v1;" +
        "pve-population-barrier-v1;begin-commit-v1;" +
        "mirror-ready-v1;" +
        "native-pvp-v1;native-pve-v1;pve-enemy-count-v1";
    private const ushort PvpAgreementProtocolVersion = 6;
    private static readonly bool PveAgreementV6RuntimeContractComplete = true;
    private static readonly bool PvpAgreementV6RuntimeContractComplete = true;
    private const string RuntimePairIdentityDomain =
        "operator-loader-neutral-runtime-pair-v1";
    private const string SuiteInstallReceiptName =
        ".operator-mod-suite-install.json";
    private const string SuiteManifestSidecarName =
        ".operator-mod-suite-manifest.json";
    private const int SuiteInstallReceiptMaxBytes = 1024 * 1024;
    private const int SuiteInstallReceiptMaxFiles = 64;
    // Private, collision-checked Mirror envelope. This is intentionally not a
    // public package protocol and is removed when the network lifetime ends.
    private const ushort PvpAgreementMessageId = 0x4D4F;
    private const int PvpAgreementMaxEnvelopeBytes = 8192;
    private const int PvpAgreementMaxIdentityString = 1024;
    private const int PvpAgreementMaxReasonString = 6144;
    private const double PvpContentReadyTimeoutSeconds = 90d;
    private const double PvpClientHelloRetrySeconds = 2d;
    private const double PvpOfferRetrySeconds = 3d;
    private const double PvpSceneReadyTimeoutSeconds = 90d;
    private const double PvpSceneReadyRequestRetrySeconds = 5d;
    private const double PvpNativeOwnerTimeoutSeconds = 90d;
    private const double PvpNativeLifecycleTimeoutSeconds = 90d;
    private const double PeerRuntimeReadyTimeoutSeconds = 90d;
    private const double PeerPlayerReadyTimeoutSeconds = 45d;
    private const double PeerPopulationReadyTimeoutSeconds = 45d;
    private const double PeerRuntimeRetrySeconds = 2d;
    private const double PvpAbortReturnRetrySeconds = 2d;
    private const double PvpAbortReturnConfirmationSeconds = 30d;
    private const int PvpAbortReturnMaximumAttempts = 3;
    private const int PvpRefusedDisconnectMaximumAttempts = 5;
    private const double PvpRefusedDisconnectRetrySeconds = 2d;
    private const int PvpMaxSessionTombstones = 64;
    private const string PvpNoCompanionIdentity = "none";
    private const string PvpApiPluginGuid = "operator.modapi";

    private enum PvpAgreementMessageKind : byte
    {
        Offer = 1,
        ContentReady = 2,
        SceneReady = 3,
        Reject = 4,
        Cancel = 5,
        SceneReadyRequest = 6,
        ClientHello = 7,
        OfferAccepted = 8,
        ContentProgress = 9,
        TransitionCommit = 10,
        PopulationManifest = 11,
        PopulationReady = 12,
        PrepareBegin = 13,
        BeginReady = 14,
        BeginCommit = 15,
        PlacePlayer = 16,
        PlayerReady = 17,
        RuntimeOwnerManifest = 18,
        RuntimeReady = 19
    }

    private enum HostPvpAgreementPhase
    {
        WaitingForHello,
        WaitingForAccepted,
        WaitingForContent,
        WaitingForScenes,
        ReadyToSpawn,
        SpawnAuthorized
    }

    private sealed class PvpPeerIdentity
    {
        public string Nonce;
        public string FrameworkVersion;
        public string ApiVersion;
        public string SuiteManifestSha256;
        public string GameBuildId;
        public string Capabilities;
        public string FrozenRosterDigest;
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
        public int ParticipantCount;
        public int MinimumEnemies;
        public int MaximumEnemies;
        public int RequestedEnemies;
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
        public int PveEnemyCount;
        public string SessionNonce;
        public PvpPeerIdentity Identity;
        public readonly HashSet<int> RequiredConnectionIds = new HashSet<int>();
        public readonly Dictionary<int, NetworkConnectionToClient> RequiredConnections =
            new Dictionary<int, NetworkConnectionToClient>();
        public readonly HashSet<int> ContentReadyConnectionIds = new HashSet<int>();
        public readonly HashSet<int> OfferAcceptedConnectionIds =
            new HashSet<int>();
        public readonly HashSet<int> HelloConnectionIds = new HashSet<int>();
        public readonly HashSet<int> OfferSentConnectionIds = new HashSet<int>();
        public readonly Dictionary<int, int> FrozenSlotByConnectionId =
            new Dictionary<int, int>();
        public readonly Dictionary<int, PvpFrozenPeer> FrozenPeerBySlot =
            new Dictionary<int, PvpFrozenPeer>();
        public NetworkConnection FrozenLocalClientConnection;
        public readonly Dictionary<int, ulong> SceneReadyEpochByConnectionId =
            new Dictionary<int, ulong>();
        public readonly Dictionary<int, string> SceneContractDigestByConnectionId =
            new Dictionary<int, string>();
        public HostPvpAgreementPhase Phase;
        public long DeadlineTimestamp;
        public long OfferRetryTimestamp;
        public bool LocalSceneReady;
        public bool NativeTransitionArmed;
        public bool NativeTransitionCommitted;
        public ulong SceneGenerationEpoch;
        public ulong LocalSceneReadyEpoch;
        public string LocalSceneContractDigest;
        public long SceneReadyRequestRetryTimestamp;
        public bool NativeLaunchInvoked;
        public long NativeLifecycleDeadlineTimestamp;
        public int SafePveCapacity;
        public bool LoadingExitLogged;
        public bool GroundedPlayersLogged;
        public string GroundedCandidatePopulationDigest;
        public int GroundedCandidateSamples;
        public int GroundedCandidateLastFrame = -1;
        public bool PvePopulationLogged;
        public bool PveCompletionLogged;
        public readonly HashSet<int> RuntimeReadyConnectionIds = new();
        public readonly Dictionary<int, PeerPlayerPlacement> PlayerPlacementByConnectionId =
            new();
        public readonly HashSet<int> PlayerReadyConnectionIds = new();
        public readonly HashSet<int> PopulationReadyConnectionIds = new();
        public bool RuntimeOwnerManifestSent;
        public long RuntimeOwnerRetryTimestamp;
        public long RuntimeBarrierDeadlineTimestamp;
        public bool LocalPlayerReady;
        public int LocalPlayerReadyStableFrames;
        public int LocalPlayerReadyLastFrame = -1;
        public bool PlayerBarrierPassed;
        public PeerPvePopulationManifest PopulationManifest;
        public long PopulationRetryTimestamp;
        public bool BeginCommitSent;
    }

    private sealed class RemotePvpAgreement
    {
        public PvpPeerIdentity Identity;
        public ModdedMapDefinition Map;
        public ModdedOperationDefinition Operation;
        public SceneVariantSelection SceneSelection;
        public int FrozenSlot;
        public NetworkConnection FrozenConnection;
        public bool OfferAcceptedSent;
        public bool ContentCommitted;
        public bool ContentReadySent;
        public bool SceneReadySent;
        public bool LocalSceneReady;
        public bool NativeTransitionArmed;
        public ulong NativeTransitionCommittedEpoch;
        public ulong RequestedSceneGenerationEpoch;
        public ulong RequestedLocalSceneGeneration;
        public ulong LocalSceneGeneration;
        public ulong LocalReadySceneGeneration;
        public ulong SceneReadySentEpoch;
        public ulong SceneReadySentLocalGeneration;
        public string LocalSceneContractDigest;
        public int LastObservedSceneHandle;
        public bool AwaitingLocalSceneGeneration;
        public bool NativeOwnerAdopted;
        public bool NativeReadinessInitialized;
        public int SafePveCapacity;
        public bool LoadingExitLogged;
        public bool GroundedPlayersLogged;
        public string GroundedCandidatePopulationDigest;
        public int GroundedCandidateSamples;
        public int GroundedCandidateLastFrame = -1;
        public bool PvePopulationLogged;
        public bool PveCompletionLogged;
        public long DeadlineTimestamp;
        public PeerRuntimeOwnerReceipt RuntimeOwner;
        public bool RuntimeReadySent;
        public PeerPlayerPlacement PendingPlayerPlacement;
        public bool PlayerReadySent;
        public int PlayerReadyStableFrames;
        public int PlayerReadyLastFrame = -1;
        public PeerPvePopulationManifest PopulationManifest;
        public bool PopulationReadySent;
        public int PopulationReadyStableFrames;
        public int PopulationReadyLastFrame = -1;
        public bool BeginCommitReceived;
        public long RuntimeBarrierDeadlineTimestamp;
    }

    private sealed class PeerRuntimeOwnerReceipt
    {
        public uint OwnerNetId { get; set; }
        public uint AssetId { get; set; }
        public ulong LocalGeneration { get; set; }
        public string SceneContractDigest { get; set; }
    }

    private sealed class PeerPlayerPlacement
    {
        [JsonIgnore]
        public int ConnectionId { get; set; }
        public uint PlayerMasterNetId { get; set; }
        public uint PlayerNetId { get; set; }
        public string MarkerName { get; set; }
        public string AssignmentDigest { get; set; }
        [JsonIgnore]
        public long RetryTimestamp { get; set; }
        [JsonIgnore]
        public bool ReceiptReceived { get; set; }
    }

    private sealed class PeerPvePopulationRecord
    {
        public uint NetId { get; set; }
        public uint AssetId { get; set; }
        public int TeamId { get; set; }
        public int Xmm { get; set; }
        public int Ymm { get; set; }
        public int Zmm { get; set; }
        public int YawMilliDegrees { get; set; }
    }

    private sealed class PeerPvePopulationManifest
    {
        public int Revision { get; set; }
        public string Digest { get; set; }
        public List<PeerPvePopulationRecord> Records { get; set; }
    }

    private sealed class RuntimeBinaryIdentity
    {
        public string SuiteManifestSha256;
        public string CompanionPluginGuid;
        public string CompanionPluginVersion;
        public string CompanionSha256;
        public string CompanionReadyMarkerName;
        public string CompanionFailureMarkerName;
    }

    private sealed class PvpClientHello
    {
        public string Nonce;
        public string FrameworkVersion;
        public string ApiVersion;
        public string Capabilities;
        public NetworkConnection Connection;
    }

    private sealed class PvpFrozenPeer
    {
        public int Slot;
        public int ConnectionId;
        public string HelloNonce;
        public string HelloIdentityDigest;
        public NetworkConnection Connection;
    }

    private sealed class PvpConnectionSnapshot
    {
        public readonly Dictionary<int, NetworkConnectionToClient> All =
            new Dictionary<int, NetworkConnectionToClient>();
        public readonly Dictionary<int, NetworkConnectionToClient> Authenticated =
            new Dictionary<int, NetworkConnectionToClient>();
        public readonly Dictionary<int, NetworkConnectionToClient> Pending =
            new Dictionary<int, NetworkConnectionToClient>();
    }

    private sealed class PvpSessionTombstone
    {
        public string Nonce;
        public string Digest;
        public ulong FinalEpoch;
        public string Outcome;
        public bool TransportReleased;
        public string TeardownDisposition;
    }

    private sealed class PvpRefusedConnection
    {
        public int ConnectionId;
        public NetworkConnectionToClient Connection;
        public string AttemptId;
        public string AuthenticationState;
        public int DisconnectAttempts;
        public bool FailureLogged;
    }

    private sealed class SuiteReceiptFile
    {
        public string FullPath;
        public long Length;
        public string Sha256;
    }

    private Action<NetworkConnection, NetworkReader, int> pvpClientManagedHandler;
    private Action<NetworkConnection, NetworkReader, int> pvpServerManagedHandler;
    private NetworkMessageDelegate pvpClientHandler;
    private NetworkMessageDelegate pvpServerHandler;
    private bool pvpClientHandlerRegistered;
    private bool pvpServerHandlerRegistered;
    private string pvpAgreementTransportFailure;
    private readonly Dictionary<int, PvpClientHello> pvpClientHellos =
        new Dictionary<int, PvpClientHello>();
    private string pvpClientHelloNonce;
    private long pvpClientHelloRetryTimestamp;
    private readonly List<PvpSessionTombstone> pvpSessionTombstones =
        new List<PvpSessionTombstone>();
    private readonly List<PvpRefusedConnection> pvpRefusedConnections =
        new List<PvpRefusedConnection>();
    private long pvpRefusedConnectionRetryTimestamp;
    private HostPvpAgreement hostPvpAgreement;
    private RemotePvpAgreement remotePvpAgreement;

    private bool IsPvpSessionTombstoned(string nonce, string digest)
    {
        if (string.IsNullOrEmpty(nonce) || string.IsNullOrEmpty(digest))
            return false;
        return pvpSessionTombstones.Any(item =>
            string.Equals(item.Nonce, nonce, StringComparison.Ordinal) &&
            string.Equals(item.Digest, digest, StringComparison.Ordinal));
    }

    private bool HasPvpSessionTombstoneCapacity() =>
        pvpSessionTombstones.Count < PvpMaxSessionTombstones;

    private void TombstonePvpSession(
        PvpPeerIdentity identity,
        ulong finalEpoch,
        string outcome)
    {
        if (identity == null || string.IsNullOrEmpty(identity.Nonce) ||
            !IsLowercasePvpSha256(identity.Digest))
        {
            return;
        }
        PvpSessionTombstone existing = pvpSessionTombstones.FirstOrDefault(item =>
            string.Equals(item.Nonce, identity.Nonce, StringComparison.Ordinal) &&
            string.Equals(item.Digest, identity.Digest, StringComparison.Ordinal));
        if (existing != null)
        {
            if (existing.FinalEpoch != finalEpoch ||
                !string.Equals(existing.Outcome, outcome, StringComparison.Ordinal))
            {
                pvpAgreementTransportFailure =
                    "closed peer-session tombstone changed for digest " +
                    identity.Digest;
                log.LogError(pvpAgreementTransportFailure);
            }
            return;
        }
        if (pvpSessionTombstones.Count >= PvpMaxSessionTombstones)
        {
            pvpAgreementTransportFailure =
                "peer-session tombstone ledger reached its network-lifetime bound";
            log.LogError(pvpAgreementTransportFailure);
            return;
        }
        pvpSessionTombstones.Add(new PvpSessionTombstone
        {
            Nonce = identity.Nonce,
            Digest = identity.Digest,
            FinalEpoch = finalEpoch,
            Outcome = outcome ?? string.Empty
        });
        LogFrameworkEvidence(
            "pvp-session-tombstoned",
            identity.OperationId,
            identity.MapId,
            activeOperation?.SceneHandle ?? 0,
            activeOperation?.EvidenceSceneGeneration ?? 0,
            "identityDigest=" + FrameworkEvidence.Encode(identity.Digest) +
            "|finalEpoch=" + FrameworkEvidence.Number(finalEpoch) +
            "|outcome=" + FrameworkEvidence.Encode(outcome));
    }

    private void MarkPvpSessionTransportTeardown(
        PvpPeerIdentity identity,
        string disposition)
    {
        if (identity == null)
            return;
        PvpSessionTombstone tombstone = pvpSessionTombstones.FirstOrDefault(item =>
            string.Equals(item.Nonce, identity.Nonce, StringComparison.Ordinal) &&
            string.Equals(item.Digest, identity.Digest, StringComparison.Ordinal));
        if (tombstone == null)
            return;
        if (tombstone.TransportReleased)
        {
            if (!string.Equals(
                    tombstone.TeardownDisposition,
                    disposition,
                    StringComparison.Ordinal))
            {
                pvpAgreementTransportFailure =
                    "peer-session teardown disposition changed after closure";
            }
            return;
        }
        tombstone.TransportReleased = true;
        tombstone.TeardownDisposition = disposition ?? string.Empty;
    }

    private void LogPvpSessionClose(
        PvpPeerIdentity identity,
        ActiveMapOperation operation,
        string role,
        ulong finalEpoch,
        string outcome,
        string reason)
    {
        operation = ResolvePvpEvidenceOperation(operation, identity);
        int sceneHandle = operation?.SceneHandle ?? 0;
        if (sceneHandle == 0)
            sceneHandle = operation?.EvidenceLastSceneHandle ?? 0;

        LogFrameworkEvidence(
            identity?.Mode ==
                (int)ModdedOperationMode.PlayerVersusEnvironment
                ? "pve-peer-session-close"
                : "pvp-session-close",
            identity?.OperationId ?? operation?.Operation?.Id,
            identity?.MapId ?? operation?.Map?.Id,
            sceneHandle,
            operation?.EvidenceSceneGeneration ?? 0,
            "role=" + FrameworkEvidence.Encode(role) +
            "|outcome=" + FrameworkEvidence.Encode(outcome) +
            "|finalEpoch=" + FrameworkEvidence.Number(finalEpoch) +
            "|identityDigest=" + FrameworkEvidence.Encode(identity?.Digest) +
            "|suiteManifestSha256=" + FrameworkEvidence.Encode(
                identity?.SuiteManifestSha256) +
            "|packageContentId=" + FrameworkEvidence.Encode(
                identity?.PackageContentId) +
            "|companionRuntimeContentId=" + FrameworkEvidence.Encode(
                identity?.CompanionSha256) +
            "|reason=" + FrameworkEvidence.Encode(reason));
    }

    private void LogPveLateJoinRefusal(
        PvpPeerIdentity identity,
        ActiveMapOperation operation,
        string role,
        ulong epoch,
        string attemptId,
        string connectionIds,
        int frozenRemoteCount,
        int currentRemoteCount,
        string outcome)
    {
        operation = ResolvePvpEvidenceOperation(operation, identity);
        LogFrameworkEvidence(
            "pve-peer-late-join-refused",
            identity?.OperationId ?? operation?.Operation?.Id,
            identity?.MapId ?? operation?.Map?.Id,
            operation?.SceneHandle ?? operation?.EvidenceLastSceneHandle ?? 0,
            operation?.EvidenceSceneGeneration ?? 0,
            "role=" + FrameworkEvidence.Encode(role) +
            "|identityDigest=" + FrameworkEvidence.Encode(identity?.Digest) +
            "|suiteManifestSha256=" + FrameworkEvidence.Encode(
                identity?.SuiteManifestSha256) +
            "|packageContentId=" + FrameworkEvidence.Encode(
                identity?.PackageContentId) +
            "|attemptId=" + FrameworkEvidence.Encode(attemptId) +
            "|epoch=" + FrameworkEvidence.Number(epoch) +
            "|connectionIds=" + FrameworkEvidence.Encode(connectionIds) +
            "|frozenRemoteCount=" + FrameworkEvidence.Number(frozenRemoteCount) +
            "|currentRemoteCount=" + FrameworkEvidence.Number(currentRemoteCount) +
            "|outcome=" + FrameworkEvidence.Encode(outcome));
    }

    private void LogPvePeerAgreementEvidence(
        string eventName,
        PvpPeerIdentity identity,
        ActiveMapOperation operation,
        string role,
        ulong epoch,
        string detail)
    {
        if (identity?.Mode !=
            (int)ModdedOperationMode.PlayerVersusEnvironment)
        {
            return;
        }
        operation = ResolvePvpEvidenceOperation(operation, identity);
        string payload =
            "role=" + FrameworkEvidence.Encode(role) +
            "|identityDigest=" + FrameworkEvidence.Encode(identity.Digest) +
            "|suiteManifestSha256=" + FrameworkEvidence.Encode(
                identity.SuiteManifestSha256) +
            "|packageId=" + FrameworkEvidence.Encode(identity.PackageId) +
            "|packageVersion=" + FrameworkEvidence.Encode(
                identity.PackageVersion) +
            "|packageContentId=" + FrameworkEvidence.Encode(
                identity.PackageContentId) +
            "|companionPlugin=" + FrameworkEvidence.Encode(
                identity.CompanionPluginGuid + "@" +
                identity.CompanionPluginVersion) +
            "|companionRuntimeContentId=" + FrameworkEvidence.Encode(
                identity.CompanionSha256) +
            "|operation=" + FrameworkEvidence.Encode(identity.OperationId) +
            "|mode=" + FrameworkEvidence.Number(identity.Mode) +
            "|spawnSet=" + FrameworkEvidence.Encode(identity.SpawnSetId) +
            "|variant=" + FrameworkEvidence.Encode(identity.VariantId) +
            "|scenePath=" + FrameworkEvidence.Encode(identity.ScenePath) +
            "|time=" + FrameworkEvidence.Encode(identity.TimeCode) +
            "|minimumPlayers=" + FrameworkEvidence.Number(
                identity.MinimumPlayers) +
            "|maximumPlayers=" + FrameworkEvidence.Number(
                identity.MaximumPlayers) +
            "|participantCount=" + FrameworkEvidence.Number(
                identity.ParticipantCount) +
            "|minimumEnemies=" + FrameworkEvidence.Number(
                identity.MinimumEnemies) +
            "|maximumEnemies=" + FrameworkEvidence.Number(
                identity.MaximumEnemies) +
            "|requestedEnemies=" + FrameworkEvidence.Number(
                identity.RequestedEnemies) +
            "|epoch=" + FrameworkEvidence.Number(epoch);
        if (!string.IsNullOrEmpty(detail))
            payload += "|" + detail;
        LogFrameworkEvidence(
            eventName,
            identity.OperationId,
            identity.MapId,
            operation?.SceneHandle ?? operation?.EvidenceLastSceneHandle ?? 0,
            operation?.EvidenceSceneGeneration ?? 0,
            payload);
    }

    private static ActiveMapOperation ResolvePvpEvidenceOperation(
        ActiveMapOperation operation,
        PvpPeerIdentity identity)
    {
        if (!IsPeerAgreementMode(operation?.Operation?.Mode))
        {
            return null;
        }
        return identity == null || OperationMatchesPvpIdentity(operation, identity)
            ? operation
            : null;
    }

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
                pvpAgreementTransportFailure = registeredClient == null
                    ? "owned Mirror client handler disappeared at 0x"
                    : "Mirror client message ID ownership changed at 0x";
                pvpAgreementTransportFailure +=
                    PvpAgreementMessageId.ToString(
                        "X4",
                        CultureInfo.InvariantCulture);
            }
            NetworkMessageDelegate registeredServer = null;
            if (pvpServerHandlerRegistered &&
                (NetworkServer.handlers == null ||
                 !NetworkServer.handlers.TryGetValue(
                     PvpAgreementMessageId,
                     out registeredServer) ||
                 !SamePvpAgreementHandler(registeredServer, pvpServerHandler)))
            {
                pvpAgreementTransportFailure = registeredServer == null
                    ? "owned Mirror server handler disappeared at 0x"
                    : "Mirror server message ID ownership changed at 0x";
                pvpAgreementTransportFailure +=
                    PvpAgreementMessageId.ToString(
                        "X4",
                    CultureInfo.InvariantCulture);
            }

            if (!string.IsNullOrEmpty(pvpAgreementTransportFailure))
            {
                FailHostPvpAgreement(pvpAgreementTransportFailure, true);
                FailRemotePvpAgreement(pvpAgreementTransportFailure, true);
                return;
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
                    LogFrameworkEvidence(
                        "pvp-transport-registered",
                        ResolvePvpEvidenceOperation(activeOperation, null),
                        "role=client|messageId=0x" +
                        PvpAgreementMessageId.ToString(
                            "X4",
                            CultureInfo.InvariantCulture));
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
                    LogFrameworkEvidence(
                        "pvp-transport-registered",
                        ResolvePvpEvidenceOperation(activeOperation, null),
                        "role=server|messageId=0x" +
                        PvpAgreementMessageId.ToString(
                            "X4",
                            CultureInfo.InvariantCulture));
                }
            }

            if (string.IsNullOrEmpty(pvpAgreementTransportFailure) &&
                (pvpClientHandlerRegistered || pvpServerHandlerRegistered))
            {
                TryMaintainPvpClientHello();
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
        HostPvpAgreement closingHost = hostPvpAgreement;
        RemotePvpAgreement closingRemote = remotePvpAgreement;
        bool clientWasRegistered = pvpClientHandlerRegistered ||
            pvpClientHandler != null;
        bool serverWasRegistered = pvpServerHandlerRegistered ||
            pvpServerHandler != null;
        bool clientReleased = pvpClientHandler == null;
        bool serverReleased = pvpServerHandler == null;
        bool clientRegistryOwnershipIntact = true;
        bool serverRegistryOwnershipIntact = true;
        bool matchingAgreementOperation =
            IsPeerAgreementMode(agreementOperation?.Operation?.Mode) &&
            ((hostPvpAgreement != null && OperationMatchesPvpIdentity(
                agreementOperation,
                hostPvpAgreement.Identity)) ||
             (remotePvpAgreement != null && OperationMatchesPvpIdentity(
                agreementOperation,
                remotePvpAgreement.Identity)));
        bool postNativeLaunch = matchingAgreementOperation &&
            (agreementOperation.NativeLaunchInvoked ||
             agreementOperation.NativeTransitionStarted ||
             closingHost?.NativeLaunchInvoked == true ||
             closingHost?.NativeTransitionCommitted == true ||
             closingRemote?.NativeTransitionCommittedEpoch > 0);
        if (matchingAgreementOperation)
        {
            agreementOperation.NetworkSpawnFailed = true;
        }
        try
        {
            if (hostPvpAgreement != null)
                BroadcastHostPvpControl(PvpAgreementMessageKind.Cancel, reason);
            else if (remotePvpAgreement != null)
                SendRemotePvpControl(PvpAgreementMessageKind.Reject, reason);
        }
        catch { }
        if (postNativeLaunch)
            RequestNativePvpAbortReturn(agreementOperation);

        try
        {
            if (pvpClientHandler != null && NetworkClient.handlers != null)
            {
                if (!NetworkClient.handlers.TryGetValue(
                        PvpAgreementMessageId,
                        out NetworkMessageDelegate existingClient))
                {
                    clientReleased = true;
                }
                else if (SamePvpAgreementHandler(
                             existingClient,
                             pvpClientHandler))
                {
                    bool removed = NetworkClient.handlers.Remove(
                        PvpAgreementMessageId);
                    clientReleased = removed &&
                        !NetworkClient.handlers.ContainsKey(
                            PvpAgreementMessageId);
                }
                else
                {
                    clientReleased = true;
                    clientRegistryOwnershipIntact = false;
                    pvpAgreementTransportFailure =
                        "foreign Mirror client handler replaced the owned " +
                        "message ID during release";
                }
            }
        }
        catch (Exception ex)
        {
            pvpAgreementTransportFailure =
                "Mirror client handler release failed: " +
                ex.GetType().Name + ": " + ex.Message;
            clientReleased = false;
        }
        try
        {
            if (pvpServerHandler != null && NetworkServer.handlers != null)
            {
                if (!NetworkServer.handlers.TryGetValue(
                        PvpAgreementMessageId,
                        out NetworkMessageDelegate existingServer))
                {
                    serverReleased = true;
                }
                else if (SamePvpAgreementHandler(
                             existingServer,
                             pvpServerHandler))
                {
                    bool removed = NetworkServer.handlers.Remove(
                        PvpAgreementMessageId);
                    serverReleased = removed &&
                        !NetworkServer.handlers.ContainsKey(
                            PvpAgreementMessageId);
                }
                else
                {
                    serverReleased = true;
                    serverRegistryOwnershipIntact = false;
                    pvpAgreementTransportFailure =
                        "foreign Mirror server handler replaced the owned " +
                        "message ID during release";
                }
            }
        }
        catch (Exception ex)
        {
            pvpAgreementTransportFailure =
                "Mirror server handler release failed: " +
                ex.GetType().Name + ": " + ex.Message;
            serverReleased = false;
        }
        bool transportReleaseVerified = clientReleased && serverReleased &&
            clientRegistryOwnershipIntact && serverRegistryOwnershipIntact;

        if (closingHost != null)
        {
            if (!IsPvpSessionTombstoned(
                    closingHost.Identity?.Nonce,
                    closingHost.Identity?.Digest))
            {
                TombstonePvpSession(
                    closingHost.Identity,
                    closingHost.SceneGenerationEpoch,
                    "transport-release");
            }
            LogPvpSessionClose(
                closingHost.Identity,
                agreementOperation,
                "host",
                closingHost.SceneGenerationEpoch,
                "transport-release",
                reason);
        }
        if (closingRemote != null)
        {
            if (!IsPvpSessionTombstoned(
                    closingRemote.Identity?.Nonce,
                    closingRemote.Identity?.Digest))
            {
                TombstonePvpSession(
                    closingRemote.Identity,
                    closingRemote.RequestedSceneGenerationEpoch,
                    "transport-release");
            }
            LogPvpSessionClose(
                closingRemote.Identity,
                agreementOperation,
                "remote",
                closingRemote.RequestedSceneGenerationEpoch,
                "transport-release",
                reason);
        }
        if (clientWasRegistered || serverWasRegistered ||
            closingHost != null || closingRemote != null)
        {
            PvpPeerIdentity transportIdentity =
                closingHost?.Identity ?? closingRemote?.Identity;
            ActiveMapOperation transportOperation = ResolvePvpEvidenceOperation(
                agreementOperation,
                transportIdentity);
            int sceneHandle = transportOperation?.SceneHandle ?? 0;
            if (sceneHandle == 0)
                sceneHandle = transportOperation?.EvidenceLastSceneHandle ?? 0;
            LogFrameworkEvidence(
                transportReleaseVerified
                    ? "pvp-transport-released"
                    : "pvp-transport-release-failed",
                transportIdentity?.OperationId ??
                    transportOperation?.Operation?.Id,
                transportIdentity?.MapId ?? transportOperation?.Map?.Id,
                sceneHandle,
                transportOperation?.EvidenceSceneGeneration ?? 0,
                "messageId=0x" + PvpAgreementMessageId.ToString(
                    "X4",
                    CultureInfo.InvariantCulture) +
                "|clientWasRegistered=" + clientWasRegistered.ToString().ToLowerInvariant() +
                "|clientReleased=" + clientReleased.ToString().ToLowerInvariant() +
                "|serverWasRegistered=" + serverWasRegistered.ToString().ToLowerInvariant() +
                "|serverReleased=" + serverReleased.ToString().ToLowerInvariant() +
                "|ownershipPreserved=" +
                    (!transportReleaseVerified).ToString().ToLowerInvariant() +
                "|clientRegistryOwnershipIntact=" +
                    clientRegistryOwnershipIntact.ToString().ToLowerInvariant() +
                "|serverRegistryOwnershipIntact=" +
                    serverRegistryOwnershipIntact.ToString().ToLowerInvariant() +
                "|hostFinalEpoch=" + FrameworkEvidence.Number(
                    closingHost?.SceneGenerationEpoch ?? 0) +
                "|remoteFinalEpoch=" +
                    FrameworkEvidence.Number(
                        closingRemote?.RequestedSceneGenerationEpoch ?? 0) +
                "|reason=" + FrameworkEvidence.Encode(reason));
        }

        if (clientReleased)
        {
            pvpClientHandlerRegistered = false;
            pvpClientHandler = null;
            pvpClientManagedHandler = null;
        }
        if (serverReleased)
        {
            pvpServerHandlerRegistered = false;
            pvpServerHandler = null;
            pvpServerManagedHandler = null;
        }
        if (transportReleaseVerified)
        {
            MarkPvpSessionTransportTeardown(
                closingHost?.Identity,
                "exact-handler-release");
            MarkPvpSessionTransportTeardown(
                closingRemote?.Identity,
                "exact-handler-release");
            pvpAgreementTransportFailure = null;
        }
        else if (string.IsNullOrEmpty(pvpAgreementTransportFailure))
            pvpAgreementTransportFailure =
                "peer-agreement transport release could not be verified";
        pvpClientHellos.Clear();
        pvpClientHelloNonce = null;
        pvpClientHelloRetryTimestamp = 0;
        hostPvpAgreement = null;
        remotePvpAgreement = null;
        if (transportReleaseVerified &&
            !NetworkClient.active && !NetworkServer.active)
        {
            pvpSessionTombstones.Clear();
            pvpRefusedConnections.Clear();
            pvpRefusedConnectionRetryTimestamp = 0;
        }
    }

    private void TryMaintainPvpClientHello()
    {
        if (!NetworkClient.active || NetworkServer.active ||
            !pvpClientHandlerRegistered || NetworkClient.connection == null ||
            !NetworkClient.connection.isAuthenticated)
        {
            return;
        }
        if (!DeadlineExpired(pvpClientHelloRetryTimestamp))
            return;
        if (string.IsNullOrEmpty(pvpClientHelloNonce))
            pvpClientHelloNonce = Guid.NewGuid().ToString("N");
        SendPvpEnvelope(NetworkClient.connection, writer =>
        {
            NetworkWriterExtensions.WriteUShort(
                writer,
                PvpAgreementProtocolVersion);
            NetworkWriterExtensions.WriteByte(
                writer,
                (byte)PvpAgreementMessageKind.ClientHello);
            NetworkWriterExtensions.WriteInt(writer, -1);
            NetworkWriterExtensions.WriteInt(writer, 0);
            NetworkWriterExtensions.WriteString(writer, pvpClientHelloNonce);
            NetworkWriterExtensions.WriteString(
                writer,
                PvpAgreementFrameworkVersion);
            NetworkWriterExtensions.WriteString(writer, OperatorApi.ApiVersion);
            NetworkWriterExtensions.WriteString(writer, PvpAgreementCapabilities);
        });
        pvpClientHelloRetryTimestamp = DeadlineAfter(PvpClientHelloRetrySeconds);
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
        SceneVariantSelection sceneSelection,
        int pveEnemyCount)
    {
        HostPvpAgreement closeAlreadyObserved = null;
        bool preserveExistingAgreement = false;
        try
        {
            if (operation?.Mode == ModdedOperationMode.PlayerVersusEnvironment &&
                !PveAgreementV6RuntimeContractComplete)
            {
                throw new InvalidOperationException(
                    "peer protocol v6 PVE runtime contract is incomplete; " +
                    "multiplayer PVE launch is disabled fail closed");
            }
            if (operation?.Mode == ModdedOperationMode.PlayerVersusPlayer &&
                !PvpAgreementV6RuntimeContractComplete)
            {
                throw new InvalidOperationException(
                    "peer protocol v6 PVP runtime contract remains pending its " +
                    "physical host/remote validation; PVP launch is disabled fail closed");
            }
            if (operation == null || !IsPeerAgreementMode(operation.Mode))
            {
                throw new InvalidOperationException(
                    "peer agreement can only start for a supported package operation");
            }
            if (!NetworkServer.active || !NetworkClient.active)
            {
                throw new InvalidOperationException(
                    "package peer agreement requires an active native multiplayer host");
            }
            MaintainPvpPeerAgreementTransport();
            if (!pvpClientHandlerRegistered || !pvpServerHandlerRegistered ||
                !string.IsNullOrEmpty(pvpAgreementTransportFailure))
            {
                throw new InvalidOperationException(
                    pvpAgreementTransportFailure ??
                    "package peer-agreement handlers are not available");
            }
            if (!SelectionBelongsToMap(map, sceneSelection))
                throw new InvalidOperationException(
                    "selected scene variant is not owned by the selected map");
            if (operation.Mode == ModdedOperationMode.PlayerVersusEnvironment &&
                !PveEnemyCountSelection.TryValidateConfirmedSelection(
                    pveEnemyCount,
                    operation.MinimumEnemies,
                    operation.MaximumEnemies,
                    operation.MaximumEnemies,
                    out string pveCountError))
            {
                throw new InvalidOperationException(
                    "captured PVE enemy count is invalid for peer agreement: " +
                    pveCountError);
            }
            if (operation.Mode == ModdedOperationMode.PlayerVersusPlayer &&
                pveEnemyCount != 0)
            {
                throw new InvalidOperationException(
                    "PVP peer agreement cannot carry a PVE enemy count");
            }
            if (activeOperation != null &&
                (activeOperation.SceneHandle != 0 ||
                 activeOperation.NativeTransitionStarted))
                throw new InvalidOperationException(
                    "another package transition/scene is still active on the host");

            if (hostPvpAgreement != null)
            {
                if (hostPvpAgreement.NativeTransitionArmed)
                {
                    preserveExistingAgreement = true;
                    throw new InvalidOperationException(
                        "an armed peer scene transition cannot be replaced");
                }
                closeAlreadyObserved = hostPvpAgreement;
                TombstonePvpSession(
                    hostPvpAgreement.Identity,
                    hostPvpAgreement.SceneGenerationEpoch,
                    "replaced");
                LogPvpSessionClose(
                    hostPvpAgreement.Identity,
                    activeOperation,
                    "host",
                    hostPvpAgreement.SceneGenerationEpoch,
                    "replaced",
                    "a new host selection replaced the prior PVP agreement");
                BroadcastHostPvpControl(
                    PvpAgreementMessageKind.Cancel,
                    "a new host selection replaced the prior PVP agreement");
            }
            if (!preserveExistingAgreement)
                hostPvpAgreement = null;
            if (!HasPvpSessionTombstoneCapacity())
                throw new InvalidOperationException(
                    "peer-session tombstone ledger requires a verified network " +
                    "teardown before another launch");

            var remoteConnections = CaptureAuthenticatedRemotePvpConnections(
                requireReady: false);
            int participantCount = remoteConnections.Count + 1;
            if (remoteConnections.Count == 0)
            {
                throw new InvalidOperationException(
                    AgreementModeLabel(operation.Mode) +
                    " package launch requires at least one authenticated remote peer");
            }
            if (participantCount < operation.MinimumPlayers ||
                participantCount > operation.MaximumPlayers)
            {
                throw new InvalidOperationException(
                    AgreementModeLabel(operation.Mode) + " lobby population " +
                    participantCount +
                    " is outside the operation range " + operation.MinimumPlayers +
                    "-" + operation.MaximumPlayers);
            }

            var session = new HostPvpAgreement
            {
                Presentation = presentation,
                Map = map,
                Operation = operation,
                TimeCode = timeCode,
                LaunchLaptop = launchLaptop,
                LaunchPlayer = launchPlayer,
                SceneSelection = sceneSelection,
                PveEnemyCount = pveEnemyCount,
                SessionNonce = Guid.NewGuid().ToString("N"),
                Phase = HostPvpAgreementPhase.WaitingForHello,
                DeadlineTimestamp = DeadlineAfter(PvpContentReadyTimeoutSeconds)
            };
            foreach (var entry in remoteConnections)
            {
                session.RequiredConnectionIds.Add(entry.Key);
                session.RequiredConnections.Add(entry.Key, entry.Value);
                if (pvpClientHellos.TryGetValue(
                        entry.Key,
                        out PvpClientHello hello) &&
                    SamePvpNetworkConnection(hello.Connection, entry.Value))
                {
                    session.HelloConnectionIds.Add(entry.Key);
                }
            }
            hostPvpAgreement = session;
            if (session.RequiredConnectionIds.SetEquals(
                    session.HelloConnectionIds))
            {
                BeginHostPvpOfferBroadcast(session);
            }
            else
            {
                log.LogInfo(AgreementModeLabel(operation.Mode) +
                    " peer session froze " + session.RequiredConnectionIds.Count +
                    " remote peer(s) and is waiting for the protocol-v" +
                    PvpAgreementProtocolVersion + " ClientHello barrier: ready=" +
                    session.HelloConnectionIds.Count + "/" +
                    session.RequiredConnectionIds.Count + ".");
            }
        }
        catch (Exception ex)
        {
            HostPvpAgreement failedSession = hostPvpAgreement;
            if (failedSession != null &&
                !preserveExistingAgreement &&
                !ReferenceEquals(failedSession, closeAlreadyObserved))
            {
                TombstonePvpSession(
                    failedSession.Identity,
                    failedSession.SceneGenerationEpoch,
                    "offer-failed");
                LogPvpSessionClose(
                    failedSession.Identity,
                    activeOperation,
                    "host",
                    failedSession.SceneGenerationEpoch,
                    "offer-failed",
                    ex.GetType().Name + ": " + ex.Message);
            }
            hostPvpAgreement = null;
            SetNativeConfirmationLoadingState(presentation, false);
            log.LogError(AgreementModeLabel(operation?.Mode) +
                " package launch failed closed before native start: " +
                ex.GetType().Name + ": " + ex.Message + ".");
        }
    }

    private bool CommittedPackageTransitionOwnsNativeTeardown()
    {
        return activeOperation != null &&
                   (activeOperation.NativeTransitionStarted ||
                    activeOperation.SceneHandle != 0) ||
               hostPvpAgreement?.NativeTransitionCommitted == true ||
               remotePvpAgreement?.NativeTransitionCommittedEpoch > 0;
    }

    private bool CanCommitFreshCatalogLaunchSelection()
    {
        if (CommittedPackageTransitionOwnsNativeTeardown())
        {
            log.LogError("A package launch was refused before scene selection " +
                "while a committed package transition still owns native teardown.");
            return false;
        }
        return true;
    }

    private bool ClearPvpPeerAgreementForNonPvpLaunch()
    {
        if (CommittedPackageTransitionOwnsNativeTeardown())
        {
            log.LogError("A non-peer launch was refused while a committed peer " +
                "transition still owns native teardown.");
            return false;
        }
        if (hostPvpAgreement != null)
        {
            TombstonePvpSession(
                hostPvpAgreement.Identity,
                hostPvpAgreement.SceneGenerationEpoch,
                "non-pvp-launch");
            LogPvpSessionClose(
                hostPvpAgreement.Identity,
                activeOperation,
                "host",
                hostPvpAgreement.SceneGenerationEpoch,
                "non-pvp-launch",
                "a non-PVP launch closed the prior PVP agreement");
            try
            {
                BroadcastHostPvpControl(
                    PvpAgreementMessageKind.Cancel,
                    "a non-PVP launch closed the prior PVP agreement");
            }
            catch { }
            hostPvpAgreement = null;
        }
        if (remotePvpAgreement != null)
        {
            ClearRemotePvpAgreement(
                "a non-PVP launch closed the prior PVP agreement",
                "non-pvp-launch");
        }
        return true;
    }

    private void BeginHostPvpOfferBroadcast(HostPvpAgreement host)
    {
        if (host == null || host != hostPvpAgreement || !NetworkServer.active ||
            host.Phase != HostPvpAgreementPhase.WaitingForHello ||
            !host.RequiredConnectionIds.SetEquals(host.HelloConnectionIds))
        {
            throw new InvalidOperationException(
                "host offer cannot begin before every frozen peer completes ClientHello");
        }
        if (!TryValidateHostPvpMembership(host, out string membershipError))
            throw new InvalidOperationException(membershipError);
        foreach (int connectionId in host.RequiredConnectionIds)
        {
            if (!host.RequiredConnections.TryGetValue(
                    connectionId,
                    out NetworkConnectionToClient connection) ||
                !pvpClientHellos.TryGetValue(
                    connectionId,
                    out PvpClientHello hello) ||
                !SamePvpNetworkConnection(connection, hello.Connection))
            {
                throw new InvalidOperationException(
                    "frozen peer " + connectionId +
                    " lost its exact ClientHello ownership before Offer");
            }
        }

        if (host.Identity != null)
            throw new InvalidOperationException(
                "host peer identity was finalized more than once");
        string rosterDigest = FinalizeHostPvpFrozenRoster(host);
        host.Identity = CreatePvpPeerIdentity(
            host.Map,
            host.Operation,
            host.TimeCode,
            host.SceneSelection,
            host.SessionNonce,
            host.PveEnemyCount,
            host.RequiredConnectionIds.Count + 1,
            rosterDigest);

        host.Phase = HostPvpAgreementPhase.WaitingForAccepted;
        host.DeadlineTimestamp = DeadlineAfter(PvpContentReadyTimeoutSeconds);
        SendPendingHostPvpOffers(host, isRetry: false);
        LogPvePeerAgreementEvidence(
            "pve-peer-agreement-offered",
            host.Identity,
            activeOperation,
            "host",
            epoch: 0,
            "remoteRequired=" + FrameworkEvidence.Number(
                host.RequiredConnectionIds.Count) +
            "|clientHelloReady=" + FrameworkEvidence.Number(
                host.HelloConnectionIds.Count) +
            "|protocol=" + FrameworkEvidence.Number(
                PvpAgreementProtocolVersion));
        log.LogInfo(AgreementModeLabel(host.Operation.Mode) +
            " exact-content agreement offered after ClientHello to " +
            host.RequiredConnectionIds.Count + " remote peer(s): package=" +
            host.Identity.PackageId + "@" + host.Identity.PackageVersion +
            ", content=" + host.Identity.PackageContentId + ", operation=" +
            host.Identity.OperationId + ", variant=" +
            host.Identity.VariantId + ".");
        log.LogInfo(AgreementModeLabel(host.Operation.Mode) +
            " runtime suite agreement identity: suiteManifestSha256=" +
            host.Identity.SuiteManifestSha256 + ", companion=" +
            host.Identity.CompanionPluginGuid + "@" +
            host.Identity.CompanionPluginVersion +
            ", companionSha256=" + host.Identity.CompanionSha256 + ".");
    }

    private void SendPendingHostPvpOffers(
        HostPvpAgreement host,
        bool isRetry)
    {
        if (host == null || host != hostPvpAgreement ||
            host.Phase != HostPvpAgreementPhase.WaitingForAccepted)
        {
            throw new InvalidOperationException(
                "host Offer retry lost exact agreement ownership");
        }
        int sent = 0;
        foreach (int connectionId in host.RequiredConnectionIds)
        {
            if (host.OfferAcceptedConnectionIds.Contains(connectionId))
                continue;
            if (!host.RequiredConnections.TryGetValue(
                    connectionId,
                    out NetworkConnectionToClient connection) ||
                !connection.isAuthenticated)
            {
                throw new InvalidOperationException(
                    "required peer " + connectionId +
                    " was unavailable for exact Offer delivery");
            }
            SendPvpOffer(connection, host, connectionId);
            host.OfferSentConnectionIds.Add(connectionId);
            sent++;
        }
        host.OfferRetryTimestamp = DeadlineAfter(PvpOfferRetrySeconds);
        if (isRetry && sent != 0)
        {
            log.LogInfo(AgreementModeLabel(host.Operation.Mode) +
                " exact Offer resent idempotently to " + sent +
                " pending peer(s); offerAccepted=" +
                host.OfferAcceptedConnectionIds.Count + "/" +
                host.RequiredConnectionIds.Count + ".");
        }
    }

    private string FinalizeHostPvpFrozenRoster(HostPvpAgreement host)
    {
        if (host == null || string.IsNullOrEmpty(host.SessionNonce) ||
            host.RequiredConnectionIds.Count == 0 ||
            host.RequiredConnectionIds.Count != host.HelloConnectionIds.Count)
        {
            throw new InvalidOperationException(
                "host frozen roster cannot be finalized before ClientHello");
        }
        host.FrozenSlotByConnectionId.Clear();
        host.FrozenPeerBySlot.Clear();
        if (!NetworkClient.active || NetworkClient.connection == null)
        {
            throw new InvalidOperationException(
                "host frozen roster has no exact local-client connection");
        }
        host.FrozenLocalClientConnection = NetworkClient.connection;
        int localConnectionId = NetworkServer.localConnection == null
            ? NetworkConnection.LocalConnectionId
            : NetworkServer.localConnection.connectionId;
        string localHelloDigest = ComputePvpHelloIdentityDigest(
            PvpAgreementFrameworkVersion,
            OperatorApi.ApiVersion,
            PvpAgreementCapabilities);
        host.FrozenPeerBySlot.Add(0, new PvpFrozenPeer
        {
            Slot = 0,
            ConnectionId = localConnectionId,
            HelloNonce = host.SessionNonce,
            HelloIdentityDigest = localHelloDigest,
            Connection = NetworkServer.localConnection
        });

        int slot = 1;
        foreach (int connectionId in host.RequiredConnectionIds.OrderBy(id => id))
        {
            if (!host.RequiredConnections.TryGetValue(
                    connectionId,
                    out NetworkConnectionToClient connection) ||
                !pvpClientHellos.TryGetValue(
                    connectionId,
                    out PvpClientHello hello) ||
                !SamePvpNetworkConnection(connection, hello.Connection))
            {
                throw new InvalidOperationException(
                    "frozen roster lost ClientHello connection=" + connectionId);
            }
            var peer = new PvpFrozenPeer
            {
                Slot = slot,
                ConnectionId = connectionId,
                HelloNonce = hello.Nonce,
                HelloIdentityDigest = ComputePvpHelloIdentityDigest(
                    hello.FrameworkVersion,
                    hello.ApiVersion,
                    hello.Capabilities),
                Connection = connection
            };
            host.FrozenSlotByConnectionId.Add(connectionId, slot);
            host.FrozenPeerBySlot.Add(slot, peer);
            slot++;
        }

        var canonical = new StringBuilder("operator-frozen-peer-roster-v1\n");
        foreach (PvpFrozenPeer peer in host.FrozenPeerBySlot.Values
                     .OrderBy(item => item.Slot))
        {
            canonical.Append(peer.Slot.ToString(CultureInfo.InvariantCulture))
                .Append('\n')
                .Append(peer.ConnectionId.ToString(CultureInfo.InvariantCulture))
                .Append('\n')
                .Append(peer.HelloNonce).Append('\n')
                .Append(peer.HelloIdentityDigest).Append('\n');
        }
        using SHA256 sha = SHA256.Create();
        return ToLowerHex(sha.ComputeHash(
            Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string ComputePvpHelloIdentityDigest(
        string frameworkVersion,
        string apiVersion,
        string capabilities)
    {
        string canonical = "operator-peer-client-hello-v1\n" +
            (frameworkVersion ?? string.Empty) + "\n" +
            (apiVersion ?? string.Empty) + "\n" +
            (capabilities ?? string.Empty);
        using SHA256 sha = SHA256.Create();
        return ToLowerHex(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
    }

    private static bool IsPeerAgreementMode(ModdedOperationMode? mode) =>
        mode == ModdedOperationMode.PlayerVersusPlayer ||
        mode == ModdedOperationMode.PlayerVersusEnvironment;

    private static string AgreementModeLabel(ModdedOperationMode? mode) =>
        mode == ModdedOperationMode.PlayerVersusEnvironment ? "PVE" : "PVP";

    private static string CreateSoloPveSessionDigest(
        ModdedMapDefinition map,
        ModdedOperationDefinition operation,
        string timeCode,
        SceneVariantSelection sceneSelection,
        int enemyCount)
    {
        string canonical = "operator-solo-pve-membership-v1\n" +
            Guid.NewGuid().ToString("N") + "\n" + (map?.PackageContentId ?? "") +
            "\n" + (map?.Id ?? "") + "\n" + (operation?.Id ?? "") + "\n" +
            (sceneSelection?.Id ?? "") + "\n" +
            (sceneSelection?.ScenePath ?? "") + "\n" + (timeCode ?? "") +
            "\n" + enemyCount.ToString(CultureInfo.InvariantCulture);
        using SHA256 sha = SHA256.Create();
        return ToLowerHex(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
    }

    private bool ShouldBeginPvePeerAgreement()
    {
        if (!NetworkServer.active || !NetworkClient.active)
            return false;
        try
        {
            return CaptureRemotePvpConnectionSnapshot().All.Count > 0;
        }
        catch
        {
            // An active host whose connection registry cannot be inspected must
            // enter the agreement path and fail there. It must never silently
            // downgrade a potentially multiplayer launch to the solo path.
            return true;
        }
    }

    private void EnforceSoloPveMembershipFreeze(ActiveMapOperation operation)
    {
        if (operation?.Operation?.Mode !=
                ModdedOperationMode.PlayerVersusEnvironment ||
            !operation.PveSoloMembershipFrozen ||
            !NetworkServer.active ||
            hostPvpAgreement != null)
        {
            return;
        }
        PvpConnectionSnapshot current;
        try
        {
            current = CaptureRemotePvpConnectionSnapshot();
        }
        catch (Exception ex)
        {
            operation.PveSoloLateJoinHandled = true;
            operation.NetworkSpawnFailed = true;
            LogFrameworkEvidence(
                "pve-peer-late-join-refused",
                operation,
                "role=host|identityDigest=" + FrameworkEvidence.Encode(
                    operation.PveSoloSessionDigest) +
                "|attemptId=membership-registry-failure|epoch=0" +
                "|outcome=refused-abort|reason=" + FrameworkEvidence.Encode(
                    ex.GetType().Name + ": " + ex.Message));
            FailActivePvpNativeLifecycle(
                operation,
                "solo PVE membership registry failed: " + ex.Message);
            return;
        }
        if (current.All.Count == 0)
            return;

        bool hasNewConnection = current.All.Values.Any(connection =>
            !pvpRefusedConnections.Any(refused =>
                SamePvpNetworkConnection(refused.Connection, connection)));
        if (operation.PveSoloLateJoinHandled && !hasNewConnection)
        {
            operation.NetworkSpawnFailed = true;
            RequestNativePvpAbortReturn(operation);
            return;
        }

        string connectionIds = string.Join(",", current.All.Keys.OrderBy(id => id));
        string authenticationStates = string.Join(",", current.All
            .OrderBy(entry => entry.Key)
            .Select(entry => entry.Key.ToString(CultureInfo.InvariantCulture) +
                ":" + (entry.Value.isAuthenticated
                    ? "authenticated"
                    : "pending")));
        string attemptId = ComputeLateJoinAttemptId(
            operation.PveSoloSessionDigest,
            epoch: 0,
            connectionIds);
        operation.PveSoloLateJoinHandled = true;
        operation.NetworkSpawnFailed = true;
        foreach (NetworkConnectionToClient connection in current.All.Values)
        {
            RetainRefusedPvpConnection(connection, attemptId);
            try { connection?.Disconnect(); }
            catch { }
        }
        LogFrameworkEvidence(
            "pve-peer-late-join-refused",
            operation,
            "role=host|identityDigest=" + FrameworkEvidence.Encode(
                operation.PveSoloSessionDigest) +
            "|attemptId=" + FrameworkEvidence.Encode(attemptId) +
            "|epoch=0|connectionIds=" + FrameworkEvidence.Encode(connectionIds) +
            "|authenticationStates=" +
                FrameworkEvidence.Encode(authenticationStates) +
            "|frozenRemoteCount=0|currentRemoteCount=" +
                FrameworkEvidence.Number(current.All.Count) +
            "|outcome=refused-abort");
        log.LogError("Standalone solo PVE refused authenticated late join(s) " +
            "and failed the active generation closed: attemptId=" + attemptId + ".");
        FailActivePvpNativeLifecycle(
            operation,
            "solo PVE refused a late network connection: attemptId=" +
            attemptId);
    }

    private static string ComputeLateJoinAttemptId(
        string identityDigest,
        ulong epoch,
        string connectionIds)
    {
        string canonical = "operator-pve-late-join-v1\n" +
            (identityDigest ?? string.Empty) + "\n" +
            epoch.ToString(CultureInfo.InvariantCulture) + "\n" +
            (connectionIds ?? string.Empty);
        using SHA256 sha = SHA256.Create();
        return ToLowerHex(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
    }

    private void RetainRefusedPvpConnection(
        NetworkConnectionToClient connection,
        string attemptId)
    {
        if (connection == null)
            return;
        if (pvpRefusedConnections.Any(item =>
                SamePvpNetworkConnection(item.Connection, connection)))
            return;
        if (pvpRefusedConnections.Count >= PvpMaxSessionTombstones)
        {
            pvpAgreementTransportFailure =
                "refused peer-connection ledger reached its network-lifetime bound";
            return;
        }
        pvpRefusedConnections.Add(new PvpRefusedConnection
        {
            ConnectionId = connection.connectionId,
            Connection = connection,
            AttemptId = attemptId ?? string.Empty,
            AuthenticationState = connection.isAuthenticated
                ? "authenticated"
                : "pending"
        });
    }

    private void MaintainRefusedPvpConnections()
    {
        if (!NetworkServer.active || pvpRefusedConnections.Count == 0)
            return;
        PvpConnectionSnapshot snapshot;
        try
        {
            snapshot = CaptureRemotePvpConnectionSnapshot();
        }
        catch (Exception ex)
        {
            pvpAgreementTransportFailure =
                "refused connection registry failed: " + ex.Message;
            return;
        }
        for (int index = pvpRefusedConnections.Count - 1; index >= 0; index--)
        {
            PvpRefusedConnection refused = pvpRefusedConnections[index];
            if (!snapshot.All.TryGetValue(
                    refused.ConnectionId,
                    out NetworkConnectionToClient current) ||
                !SamePvpNetworkConnection(refused.Connection, current))
            {
                LogFrameworkEvidence(
                    "pve-peer-late-join-disconnected",
                    ResolvePvpEvidenceOperation(activeOperation, null),
                    "role=host|attemptId=" +
                        FrameworkEvidence.Encode(refused.AttemptId) +
                    "|connectionId=" + FrameworkEvidence.Number(
                        refused.ConnectionId) +
                    "|authenticationState=" + FrameworkEvidence.Encode(
                        refused.AuthenticationState) +
                    "|outcome=disappearance-observed");
                pvpRefusedConnections.RemoveAt(index);
            }
        }
        if (pvpRefusedConnections.Count == 0 ||
            (pvpRefusedConnectionRetryTimestamp > 0 &&
             !DeadlineExpired(pvpRefusedConnectionRetryTimestamp)))
        {
            return;
        }
        pvpRefusedConnectionRetryTimestamp = DeadlineAfter(
            PvpRefusedDisconnectRetrySeconds);
        foreach (PvpRefusedConnection refused in pvpRefusedConnections)
        {
            if (refused.DisconnectAttempts >=
                PvpRefusedDisconnectMaximumAttempts)
            {
                if (!refused.FailureLogged)
                {
                    refused.FailureLogged = true;
                    LogFrameworkEvidence(
                        "pve-peer-late-join-disconnect-failed",
                        ResolvePvpEvidenceOperation(activeOperation, null),
                        "role=host|attemptId=" +
                            FrameworkEvidence.Encode(refused.AttemptId) +
                        "|connectionId=" + FrameworkEvidence.Number(
                            refused.ConnectionId) +
                        "|attempts=" + FrameworkEvidence.Number(
                            refused.DisconnectAttempts) +
                        "|outcome=owner-retained-network-teardown-required");
                }
                continue;
            }
            refused.DisconnectAttempts++;
            try { refused.Connection.Disconnect(); }
            catch { }
        }
    }

    private bool HasPeerAgreementForOperation(ActiveMapOperation operation)
    {
        if (operation == null)
            return false;
        return hostPvpAgreement != null &&
                   OperationMatchesPvpIdentity(
                       operation,
                       hostPvpAgreement.Identity) ||
               remotePvpAgreement != null &&
                   OperationMatchesPvpIdentity(
                       operation,
                       remotePvpAgreement.Identity);
    }

    private void ProcessPvpPeerAgreement()
    {
        MaintainRefusedPvpConnections();
        ProcessPvpPendingSceneDisposition(activeOperation);
        if (activeOperation?.PveSoloMembershipFrozen == true &&
            !HasPeerAgreementForOperation(activeOperation))
        {
            EnforceSoloPveMembershipFreeze(activeOperation);
        }
        TryAdvancePvpPackageRuntimeReadiness(activeOperation);
        ProcessPeerRuntimeBarriers(activeOperation);
        TryLogPeerLoadingExit(activeOperation);
        TryLogPeerGroundedPlayers(activeOperation);
        TryLogRemotePvePopulation(activeOperation);
        TryLogPvePeerCompletion(activeOperation);
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
            if ((host.Phase == HostPvpAgreementPhase.WaitingForHello ||
                 host.Phase == HostPvpAgreementPhase.WaitingForAccepted ||
                 host.Phase == HostPvpAgreementPhase.WaitingForContent ||
                 host.Phase == HostPvpAgreementPhase.WaitingForScenes) &&
                DeadlineExpired(host.DeadlineTimestamp))
            {
                string phase = host.Phase == HostPvpAgreementPhase.WaitingForHello
                    ? "client-hello"
                    : host.Phase == HostPvpAgreementPhase.WaitingForAccepted
                        ? "offer-accepted"
                    : host.Phase == HostPvpAgreementPhase.WaitingForContent
                        ? "content-ready"
                        : "scene-ready";
                FailHostPvpAgreement(
                    "timed out waiting for the exact remote " + phase + " barrier",
                    true);
                return;
            }

            if (host.Phase == HostPvpAgreementPhase.WaitingForHello &&
                host.RequiredConnectionIds.SetEquals(host.HelloConnectionIds))
            {
                try
                {
                    BeginHostPvpOfferBroadcast(host);
                }
                catch (Exception ex)
                {
                    FailHostPvpAgreement(
                        "ClientHello barrier could not publish the exact Offer: " +
                        ex.GetType().Name + ": " + ex.Message,
                        true);
                }
                return;
            }

            if (host.Phase == HostPvpAgreementPhase.WaitingForAccepted &&
                DeadlineExpired(host.OfferRetryTimestamp) &&
                !host.RequiredConnectionIds.SetEquals(
                    host.OfferAcceptedConnectionIds))
            {
                try
                {
                    SendPendingHostPvpOffers(host, isRetry: true);
                }
                catch (Exception ex)
                {
                    FailHostPvpAgreement(
                        "exact Offer retry failed: " + ex.GetType().Name +
                        ": " + ex.Message,
                        true);
                    return;
                }
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
                LogPvePeerAgreementEvidence(
                    "pve-peer-content-ready",
                    host.Identity,
                    activeOperation,
                    "host",
                    epoch: 0,
                    "remoteReady=" + FrameworkEvidence.Number(
                        host.ContentReadyConnectionIds.Count) +
                    "|remoteRequired=" + FrameworkEvidence.Number(
                        host.RequiredConnectionIds.Count));
                LogPvePeerAgreementEvidence(
                    "pve-peer-count-agreed",
                    host.Identity,
                    activeOperation,
                    "host",
                    epoch: 0,
                    "countCommitted=" + FrameworkEvidence.Number(
                        host.PveEnemyCount));
                if (!TryBeginHostPvpSceneGeneration(
                        host,
                        "initial native scene transition",
                        out string generationError,
                        evidenceTargetSceneHandle: 0,
                        evidenceTargetSceneGeneration: 1))
                {
                    FailHostPvpAgreement(generationError, true);
                    return;
                }
                log.LogInfo(AgreementModeLabel(host.Operation.Mode) +
                    " exact-content agreement passed on every peer; " +
                    "entering the native scene transition for operation=" +
                    host.Operation.Id + ".");
                log.LogInfo("PVP initial scene-generation readiness is bound to " +
                    "epoch=" + host.SceneGenerationEpoch + ".");
                if (host != hostPvpAgreement ||
                    host.Phase != HostPvpAgreementPhase.WaitingForScenes)
                {
                    log.LogError("Host peer agreement lost ownership before " +
                        "the native scene transition; launch was suppressed.");
                    return;
                }
                InvokeNativeCatalogLaunch(
                    host.Presentation,
                    host.Map,
                    host.Operation,
                    host.TimeCode,
                    host.LaunchLaptop,
                    host.LaunchPlayer,
                    host.SceneSelection,
                    pveEnemyCount: host.PveEnemyCount,
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
                ActiveMapOperation evidenceOperation = ResolvePvpEvidenceOperation(
                    activeOperation,
                    host.Identity);
                LogFrameworkEvidence(
                    "pvp-scene-epoch-barrier-passed",
                    host.Identity.OperationId,
                    host.Identity.MapId,
                    evidenceOperation?.SceneHandle ?? 0,
                    evidenceOperation?.EvidenceSceneGeneration ?? 0,
                    "epoch=" + FrameworkEvidence.Number(
                        host.SceneGenerationEpoch) +
                    "|localReady=true|remoteReady=" +
                    FrameworkEvidence.Number(
                        CountRequiredPvpPeersSceneReadyForCurrentEpoch(host)) +
                    "|remoteRequired=" + FrameworkEvidence.Number(
                        host.RequiredConnectionIds.Count));
                log.LogInfo("PVP scene-ready agreement passed on the host and all " +
                    "remote peers; native PvpGameode spawn is authorized.");
                log.LogInfo("PVP scene-ready epoch barrier passed: " +
                    "sceneGenerationEpoch=" + host.SceneGenerationEpoch + ".");
            }
        }

        RemotePvpAgreement remote = remotePvpAgreement;
        if (remote != null &&
            (!NetworkClient.active || NetworkServer.active ||
             NetworkClient.connection == null ||
             !NetworkClient.connection.isAuthenticated ||
             !SamePvpNetworkConnection(
                 remote.FrozenConnection,
                 NetworkClient.connection)))
        {
            FailRemotePvpAgreement(
                "frozen remote client connection ownership changed",
                false);
            return;
        }
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
            if (kind == PvpAgreementMessageKind.ClientHello)
            {
                throw new InvalidOperationException(
                    "host sent a client-only pre-offer handshake");
            }

            if (kind == PvpAgreementMessageKind.Offer)
            {
                int offerSenderSlot = NetworkReaderExtensions.ReadInt(reader);
                if (offerSenderSlot != 0)
                    throw new InvalidOperationException(
                        "Offer sender is not the frozen host slot");
                PvpPeerIdentity offer = ReadPvpIdentity(reader);
                if (IsPvpSessionTombstoned(offer.Nonce, offer.Digest))
                {
                    LogFrameworkEvidence(
                        "pvp-session-resurrection-refused",
                        offer.OperationId,
                        offer.MapId,
                        activeOperation?.SceneHandle ?? 0,
                        activeOperation?.EvidenceSceneGeneration ?? 0,
                        "role=remote|identityDigest=" +
                            FrameworkEvidence.Encode(offer.Digest) +
                        "|message=Offer");
                    return;
                }
                int frozenSlot = NetworkReaderExtensions.ReadInt(reader);
                List<PvpFrozenPeer> frozenRoster = ReadPvpFrozenRoster(
                    reader,
                    offer.ParticipantCount);
                RequirePvpEnvelopeConsumed(reader);
                ValidateRemotePvpFrozenRoster(
                    offer,
                    frozenSlot,
                    frozenRoster);
                AcceptRemotePvpOfferOrReject(offer, frozenSlot);
                return;
            }
            int senderSlot = NetworkReaderExtensions.ReadInt(reader);
            int recipientSlot = NetworkReaderExtensions.ReadInt(reader);
            string nonce = ReadBoundedPvpString(reader, 64, "nonce");
            string digest = ReadBoundedPvpString(reader, 128, "digest");
            ulong sceneGenerationEpoch = NetworkReaderExtensions.ReadULong(reader);
            string reason = ReadBoundedPvpString(
                reader,
                PvpAgreementMaxReasonString,
                "reason",
                allowEmpty: true);
            RequirePvpEnvelopeConsumed(reader);
            if (IsPvpSessionTombstoned(nonce, digest))
            {
                LogFrameworkEvidence(
                    "pvp-session-control-tombstoned",
                    operationId: null,
                    mapId: null,
                    sceneHandle: activeOperation?.SceneHandle ?? 0,
                    sceneGeneration: activeOperation?.EvidenceSceneGeneration ?? 0,
                    payload: "role=remote|identityDigest=" +
                        FrameworkEvidence.Encode(digest) +
                    "|message=" + FrameworkEvidence.Encode(kind.ToString()));
                return;
            }
            RemotePvpAgreement remote = remotePvpAgreement;
            if (remote == null ||
                !string.Equals(remote.Identity.Nonce, nonce,
                    StringComparison.Ordinal) ||
                !string.Equals(remote.Identity.Digest, digest,
                    StringComparison.Ordinal))
            {
                return;
            }
            if (!SamePvpNetworkConnection(
                    remote.FrozenConnection,
                    NetworkClient.connection) ||
                senderSlot != 0 || recipientSlot != remote.FrozenSlot)
            {
                throw new InvalidOperationException(
                    "host control connection/sender/recipient slot does not " +
                    "match the frozen remote session");
            }
            if (kind == PvpAgreementMessageKind.TransitionCommit)
            {
                if (!string.IsNullOrEmpty(reason) ||
                    sceneGenerationEpoch == 0 ||
                    sceneGenerationEpoch !=
                        remote.RequestedSceneGenerationEpoch)
                {
                    throw new InvalidOperationException(
                        "host transition commit is not bound to the current epoch");
                }
                if (remote.NativeTransitionCommittedEpoch != 0)
                {
                    if (remote.NativeTransitionCommittedEpoch !=
                        sceneGenerationEpoch)
                    {
                        throw new InvalidOperationException(
                            "host transition commit changed after publication");
                    }
                    return;
                }
                remote.NativeTransitionCommittedEpoch = sceneGenerationEpoch;
                LogFrameworkEvidence(
                    "pvp-native-transition-committed",
                    remote.Identity.OperationId,
                    remote.Identity.MapId,
                    activeOperation?.SceneHandle ?? 0,
                    activeOperation?.EvidenceSceneGeneration ?? 0,
                    "role=remote|identityDigest=" +
                        FrameworkEvidence.Encode(remote.Identity.Digest) +
                    "|epoch=" + FrameworkEvidence.Number(
                        sceneGenerationEpoch));
                return;
            }
            if (kind == PvpAgreementMessageKind.SceneReadyRequest)
            {
                if (sceneGenerationEpoch == 0)
                    throw new InvalidOperationException(
                        "host scene-ready request omitted its generation epoch");
                AcceptRemotePvpSceneReadyRequest(remote, sceneGenerationEpoch);
                return;
            }
            if (kind == PvpAgreementMessageKind.Cancel)
            {
                if (sceneGenerationEpoch != 0)
                    throw new InvalidOperationException(
                        "host cancellation carried an invalid scene generation epoch");
                ClearRemotePvpAgreement(reason, "host-cancel");
                return;
            }
            if (HandlePeerRuntimeClientControl(
                    kind,
                    remote,
                    reason,
                    sceneGenerationEpoch))
            {
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
            if (kind == PvpAgreementMessageKind.ClientHello)
            {
                int helloSenderSlot = NetworkReaderExtensions.ReadInt(reader);
                int helloRecipientSlot = NetworkReaderExtensions.ReadInt(reader);
                if (helloSenderSlot != -1 || helloRecipientSlot != 0)
                    throw new InvalidOperationException(
                        "ClientHello carried an assigned session slot");
                string helloNonce = ReadBoundedPvpString(
                    reader,
                    64,
                    "clientHelloNonce");
                string frameworkVersion = ReadBoundedPvpString(
                    reader,
                    64,
                    "clientHelloFrameworkVersion");
                string apiVersion = ReadBoundedPvpString(
                    reader,
                    64,
                    "clientHelloApiVersion");
                string capabilities = ReadBoundedPvpString(
                    reader,
                    256,
                    "clientHelloCapabilities");
                RequirePvpEnvelopeConsumed(reader);
                AcceptPvpClientHello(
                    connection,
                    helloNonce,
                    frameworkVersion,
                    apiVersion,
                    capabilities);
                return;
            }
            int senderSlot = NetworkReaderExtensions.ReadInt(reader);
            int recipientSlot = NetworkReaderExtensions.ReadInt(reader);
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
            if (host == null || host.Identity == null ||
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
            if (recipientSlot != 0 ||
                !host.FrozenSlotByConnectionId.TryGetValue(
                    connection.connectionId,
                    out int expectedSenderSlot) ||
                senderSlot != expectedSenderSlot)
            {
                throw new InvalidOperationException(
                    "remote control sender/recipient slot drifted from the " +
                    "frozen roster");
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
            if (kind == PvpAgreementMessageKind.OfferAccepted)
            {
                if (sceneGenerationEpoch != 0 || !string.IsNullOrEmpty(reason))
                {
                    throw new InvalidOperationException(
                        "remote OfferAccepted receipt is mutable");
                }
                if (host.OfferAcceptedConnectionIds.Contains(
                        connection.connectionId))
                    return;
                if (host.Phase != HostPvpAgreementPhase.WaitingForAccepted)
                    throw new InvalidOperationException(
                        "first remote OfferAccepted receipt is out of phase");
                host.OfferAcceptedConnectionIds.Add(connection.connectionId);
                if (host.RequiredConnectionIds.SetEquals(
                        host.OfferAcceptedConnectionIds))
                {
                    host.Phase = HostPvpAgreementPhase.WaitingForContent;
                    host.DeadlineTimestamp = DeadlineAfter(
                        PvpContentReadyTimeoutSeconds);
                    log.LogInfo("Every frozen peer accepted the immutable Offer; " +
                        "exact content loading is now in progress.");
                }
                return;
            }
            if (kind == PvpAgreementMessageKind.ContentReady &&
                (sceneGenerationEpoch != 0 || !string.IsNullOrEmpty(reason)))
            {
                throw new InvalidOperationException(
                    "remote ContentReady receipt is mutable");
            }
            if (kind == PvpAgreementMessageKind.ContentReady)
            {
                if (host.ContentReadyConnectionIds.Contains(
                        connection.connectionId))
                    return;
                if (host.Phase != HostPvpAgreementPhase.WaitingForContent ||
                    !host.OfferAcceptedConnectionIds.Contains(
                        connection.connectionId))
                {
                    throw new InvalidOperationException(
                        "first remote ContentReady arrived before immutable " +
                        "OfferAccepted or outside the content barrier");
                }
                host.ContentReadyConnectionIds.Add(connection.connectionId);
                log.LogInfo("PVP peer content-ready acknowledgement accepted: " +
                    "connection=" + connection.connectionId + ", ready=" +
                    host.ContentReadyConnectionIds.Count + "/" +
                    host.RequiredConnectionIds.Count + ".");
                return;
            }
            if (kind == PvpAgreementMessageKind.SceneReady)
            {
                if (sceneGenerationEpoch == 0)
                    throw new InvalidOperationException(
                        "remote scene-ready acknowledgement omitted its generation epoch");
                if (!connection.isReady)
                    throw new InvalidOperationException(
                        "remote scene-ready acknowledgement arrived before Mirror Ready");
                if (!IsLowercasePvpSha256(reason))
                    throw new InvalidOperationException(
                        "remote scene-ready acknowledgement omitted its exact " +
                        "scene-contract digest");
                bool hasFrozenEpoch = host.SceneReadyEpochByConnectionId
                    .TryGetValue(
                        connection.connectionId,
                        out ulong frozenEpoch);
                bool hasFrozenDigest = host.SceneContractDigestByConnectionId
                    .TryGetValue(
                        connection.connectionId,
                        out string frozenDigest);
                if (hasFrozenEpoch || hasFrozenDigest)
                {
                    if (!hasFrozenEpoch || !hasFrozenDigest)
                    {
                        FailHostPvpAgreement(
                            "remote peer " + connection.connectionId +
                            " has an incomplete frozen SceneReady receipt",
                            true);
                        return;
                    }
                    if (frozenEpoch == sceneGenerationEpoch)
                    {
                        if (!string.Equals(
                                frozenDigest,
                                reason,
                                StringComparison.Ordinal))
                        {
                            FailHostPvpAgreement(
                                "remote peer " + connection.connectionId +
                                " changed its immutable SceneReady receipt",
                                true);
                        }
                        return;
                    }
                    if (sceneGenerationEpoch > host.SceneGenerationEpoch)
                    {
                        FailHostPvpAgreement(
                            "remote peer " + connection.connectionId +
                            " pre-sent a future SceneReady epoch " +
                            sceneGenerationEpoch,
                            true);
                        return;
                    }
                    if (sceneGenerationEpoch == host.SceneGenerationEpoch ||
                        frozenEpoch > host.SceneGenerationEpoch)
                    {
                        FailHostPvpAgreement(
                            "remote peer SceneReady epoch ledger is inconsistent " +
                            "with the current host generation",
                            true);
                        return;
                    }
                    log.LogWarning("PVP peer stale SceneReady receipt ignored: " +
                        "connection=" + connection.connectionId +
                        ", receivedEpoch=" + sceneGenerationEpoch +
                        ", currentEpoch=" + host.SceneGenerationEpoch +
                        ", frozenEpoch=" + frozenEpoch + ".");
                    return;
                }
                if (host.Phase == HostPvpAgreementPhase.WaitingForScenes &&
                    sceneGenerationEpoch == host.SceneGenerationEpoch)
                {
                    if (!string.IsNullOrEmpty(host.LocalSceneContractDigest) &&
                        !string.Equals(
                            host.LocalSceneContractDigest,
                            reason,
                            StringComparison.Ordinal))
                    {
                        FailHostPvpAgreement(
                            "remote peer " + connection.connectionId +
                            " scene-contract digest does not match the host",
                            true);
                        return;
                    }
                    host.SceneReadyEpochByConnectionId[connection.connectionId] =
                        sceneGenerationEpoch;
                    host.SceneContractDigestByConnectionId[
                        connection.connectionId] = reason;
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
                    if (sceneGenerationEpoch > host.SceneGenerationEpoch)
                    {
                        FailHostPvpAgreement(
                            "remote peer " + connection.connectionId +
                            " pre-sent future SceneReady epoch " +
                            sceneGenerationEpoch,
                            true);
                        return;
                    }
                    if (sceneGenerationEpoch == host.SceneGenerationEpoch &&
                        host.Phase != HostPvpAgreementPhase.WaitingForScenes)
                    {
                        FailHostPvpAgreement(
                            "first current-epoch SceneReady receipt arrived after " +
                            "the host barrier had closed",
                            true);
                        return;
                    }
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
            else if (HandlePeerRuntimeServerControl(
                         connection,
                         kind,
                         host,
                         reason,
                         sceneGenerationEpoch))
            {
                return;
            }
            else
            {
                throw new InvalidOperationException(
                    "unexpected remote PVP agreement control " + kind);
            }
        }
        catch (Exception ex)
        {
            FailHostPvpAgreement(
                "invalid remote PVP agreement envelope: " + ex.Message,
                true);
        }
    }

    private void AcceptRemotePvpOfferOrReject(
        PvpPeerIdentity offer,
        int frozenSlot)
    {
        if (offer == null)
            throw new InvalidOperationException("host offer was empty");
        if (IsPvpSessionTombstoned(offer.Nonce, offer.Digest))
        {
            SendRemotePvpControl(
                PvpAgreementMessageKind.Reject,
                "closed peer session cannot be resurrected",
                offer,
                frozenSlotOverride: frozenSlot);
            LogFrameworkEvidence(
                "pvp-session-resurrection-refused",
                offer.OperationId,
                offer.MapId,
                0,
                0,
                "role=remote|identityDigest=" +
                    FrameworkEvidence.Encode(offer.Digest) +
                "|slot=" + FrameworkEvidence.Number(frozenSlot));
            return;
        }
        if (remotePvpAgreement == null && activeOperation != null &&
            (activeOperation.SceneHandle != 0 ||
             activeOperation.NativeTransitionStarted))
        {
            SendRemotePvpControl(
                PvpAgreementMessageKind.Reject,
                "another package scene is still active on the remote peer",
                offer,
                frozenSlotOverride: frozenSlot);
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
                    StringComparison.Ordinal) &&
                remotePvpAgreement.FrozenSlot == frozenSlot)
            {
                SendRemotePvpControl(
                    PvpAgreementMessageKind.OfferAccepted,
                    string.Empty);
                if (remotePvpAgreement.ContentCommitted)
                    SendRemotePvpControl(PvpAgreementMessageKind.ContentReady, string.Empty);
                return;
            }
            if (remotePvpAgreement.NativeTransitionCommittedEpoch > 0 ||
                activeOperation?.NativeTransitionStarted == true)
            {
                SendRemotePvpControl(
                    PvpAgreementMessageKind.Reject,
                    "a committed peer transition cannot be replaced",
                    offer,
                    frozenSlotOverride: frozenSlot);
                log.LogError("Remote peer refused a replacement Offer while " +
                    "the current native transition still owns teardown.");
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
                    offer,
                    frozenSlotOverride: frozenSlot);
            }
            catch { }
            ClearRemotePvpAgreement(replacementReason, "replaced");
            log.LogError("Remote PVP peer agreement failed closed: " +
                replacementReason + ".");
            return;
        }

        if (!HasPvpSessionTombstoneCapacity())
        {
            SendRemotePvpControl(
                PvpAgreementMessageKind.Reject,
                "peer-session tombstone ledger requires network teardown",
                offer,
                frozenSlotOverride: frozenSlot);
            log.LogError("Remote peer refused a new session because the bounded " +
                "network-lifetime tombstone ledger is full.");
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
                offer,
                frozenSlotOverride: frozenSlot);
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
            FrozenSlot = frozenSlot,
            FrozenConnection = NetworkClient.connection,
            AwaitingLocalSceneGeneration = true,
            DeadlineTimestamp = DeadlineAfter(PvpContentReadyTimeoutSeconds)
        };
        SendRemotePvpControl(
            PvpAgreementMessageKind.OfferAccepted,
            string.Empty);
        remotePvpAgreement.OfferAcceptedSent = true;
        LogPvePeerAgreementEvidence(
            "pve-peer-agreement-accepted",
            offer,
            activeOperation,
            "remote",
            epoch: 0,
            "validation=exact-local-content");

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
            PveEnemyCount = offer.RequestedEnemies,
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
            !string.Equals(offer.ApiVersion, OperatorApi.ApiVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                offer.SuiteManifestSha256,
                localRuntime.SuiteManifestSha256,
                StringComparison.Ordinal) ||
            !string.Equals(offer.GameBuildId,
                OperatorApi.Compatibility.DetectedGameBuildId,
                StringComparison.Ordinal) ||
            !string.Equals(offer.Capabilities, PvpAgreementCapabilities,
                StringComparison.Ordinal))
        {
            error = "suite/framework/API/game-build/capability identity mismatch";
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
            !IsPeerAgreementMode(operation.Mode) ||
            offer.Mode != (int)operation.Mode ||
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
        if (operation.Mode == ModdedOperationMode.PlayerVersusEnvironment)
        {
            string countError = string.Empty;
            if (offer.MinimumEnemies != operation.MinimumEnemies ||
                offer.MaximumEnemies != operation.MaximumEnemies ||
                !PveEnemyCountSelection.TryValidateConfirmedSelection(
                    offer.RequestedEnemies,
                    operation.MinimumEnemies,
                    operation.MaximumEnemies,
                    operation.MaximumEnemies,
                    out countError))
            {
                error = "PVE enemy count/bounds identity mismatch: " + countError;
                return false;
            }
        }
        else if (offer.MinimumEnemies != 0 || offer.MaximumEnemies != 0 ||
                 offer.RequestedEnemies != 0)
        {
            error = "PVP offer carried a PVE enemy count identity";
            return false;
        }
        bool declaredVariantMatches = HasDeclaredSceneVariants(map)
            ? map.SceneVariants.Any(variant =>
                variant != null &&
                string.Equals(variant.Id, offer.VariantId, StringComparison.Ordinal) &&
                string.Equals(variant.ScenePath, offer.ScenePath,
                    StringComparison.Ordinal))
            : map.SceneVariants != null && map.SceneVariants.Count == 1 &&
              string.Equals(
                  offer.VariantId,
                  map.SceneVariants[0].Id,
                  StringComparison.Ordinal) &&
              string.Equals(offer.ScenePath, map.ScenePath,
                  StringComparison.Ordinal);
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
            RequestedPveEnemyCount = remote.Identity.RequestedEnemies,
            PeerAgreementRequired = true,
            PeerAgreementIdentityDigest = remote.Identity.Digest,
            PeerAgreementRole = "remote",
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
        LogPvePeerAgreementEvidence(
            "pve-peer-content-ready",
            remote.Identity,
            activeOperation,
            "remote",
            epoch: 0,
            "contentCommitted=true");
        LogPvePeerAgreementEvidence(
            "pve-peer-count-agreed",
            remote.Identity,
            activeOperation,
            "remote",
            epoch: 0,
            "countCommitted=" + FrameworkEvidence.Number(
                remote.Identity.RequestedEnemies));
        log.LogInfo("Remote " + AgreementModeLabel(remote.Operation.Mode) +
            " peer committed the exact operation before native " +
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
            !IsPeerAgreementMode(operation.Operation.Mode) ||
            !HasPeerAgreementForOperation(operation))
        {
            return;
        }
        bool pve = operation.Operation.Mode ==
            ModdedOperationMode.PlayerVersusEnvironment;
        uint expectedAssetId = pve
            ? StandalonePveGameModeAssetId
            : StandalonePvpGameModeAssetId;
        bool expectedTemplate = pve
            ? operation.GameModeComponent is StandalonePveGameMode
            : operation.GameModeComponent is StandalonePvpGameMode;
        if (operation.BootstrapAssetId != expectedAssetId ||
            !operation.BootstrapPrefabRegistered || !expectedTemplate)
        {
            NotifyPvpScenePreparationFailed(
                operation,
                "native " + (pve ? "StandardPVE" : "PvpGameode") +
                " template was not constructed and registered");
            return;
        }

        TryAdvancePvpPackageRuntimeReadiness(operation);
    }

    private void TryAdvancePvpPackageRuntimeReadiness(
        ActiveMapOperation operation)
    {
        if (operation == null || operation.Operation == null ||
            !IsPeerAgreementMode(operation.Operation.Mode) ||
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
                "package-owned player spawn contract changed before SceneReady: " +
                spawnError);
            return;
        }
        int safePveCapacity = 0;
        if (operation.Operation.Mode ==
                ModdedOperationMode.PlayerVersusEnvironment &&
            !TryValidateAgreedPveSceneCapacity(
                operation,
                isHostOwner ? currentHost.Identity : currentRemote.Identity,
                out safePveCapacity,
                out string capacityError))
        {
            NotifyPvpScenePreparationFailed(
                operation,
                "agreed PVE enemy capacity changed before SceneReady: " +
                capacityError);
            return;
        }
        if (isHostOwner)
            currentHost.SafePveCapacity = safePveCapacity;
        if (isRemoteOwner)
            currentRemote.SafePveCapacity = safePveCapacity;
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
            log.LogInfo(AgreementModeLabel(operation.Operation.Mode) +
                " package runtime readiness passed: companion=" +
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
            log.LogInfo(AgreementModeLabel(operation.Operation.Mode) +
                " package runtime readiness passed: companion=none, " +
                "sceneHandle=" + scene.handle + ".");
        }

        if (!NetworkClient.active || NetworkClient.connection == null ||
            !NetworkClient.connection.isAuthenticated ||
            !NetworkClient.connection.isReady)
        {
            // Package preparation and Mirror Ready are independent barriers.
            // Keep polling until the native scene transition returns this
            // exact connection to Ready; never substitute our message for it.
            return;
        }

        CompletePvpSceneReady(operation);
    }

    private static bool TryValidateAgreedPveSceneCapacity(
        ActiveMapOperation operation,
        PvpPeerIdentity identity,
        out int safeCapacity,
        out string error)
    {
        safeCapacity = 0;
        error = string.Empty;
        if (operation?.Operation?.Mode !=
                ModdedOperationMode.PlayerVersusEnvironment ||
            identity == null || identity.Mode !=
                (int)ModdedOperationMode.PlayerVersusEnvironment ||
            operation.RequestedPveEnemyCount != identity.RequestedEnemies ||
            operation.Operation.MinimumEnemies != identity.MinimumEnemies ||
            operation.Operation.MaximumEnemies != identity.MaximumEnemies)
        {
            error = "active operation/count identity no longer matches agreement";
            return false;
        }
        Scene scene = FindLoadedSceneByHandle(operation.SceneHandle);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            error = "exact PVE scene is unavailable";
            return false;
        }
        List<UnityEngine.Transform> authored = FindSceneMarkers(
            scene,
            "PVE_EnemySpawn_");
        List<UnityEngine.Transform> safe = FindSafeStandalonePveEnemyMarkers(
            authored,
            out int activeMarkers,
            out int navigationMarkers);
        safeCapacity = safe.Count;
        if (!PveEnemyCountSelection.TryValidateConfirmedSelection(
                identity.RequestedEnemies,
                identity.MinimumEnemies,
                identity.MaximumEnemies,
                safeCapacity,
                out string capacityError))
        {
            error = capacityError + "; authored=" + authored.Count +
                ", active=" + activeMarkers + ", navigation=" +
                navigationMarkers + ", safe=" + safeCapacity;
            return false;
        }
        return true;
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
            gameManager.PnextSpawnIndex != 0 ||
            !operation.RandomSpawnsCaptured || operation.OwnedRandomSpawns ||
            gameManager.RandomSpawns != operation.OwnedRandomSpawns)
        {
            error = "installed spawn globals no longer match this scene generation";
            return false;
        }
        return true;
    }

    private static bool TryComputePeerSceneContractDigest(
        ActiveMapOperation operation,
        PvpPeerIdentity identity,
        ulong epoch,
        out string digest,
        out string error)
    {
        digest = string.Empty;
        error = string.Empty;
        if (operation == null || identity == null || epoch == 0)
        {
            error = "scene contract ownership is incomplete";
            return false;
        }
        if (operation.FrozenSceneContract != null ||
            operation.FrozenSceneContractEpoch != 0 ||
            !string.IsNullOrEmpty(operation.FrozenSceneContractDigest))
        {
            if (operation.FrozenSceneContract == null ||
                operation.FrozenSceneContractEpoch != epoch ||
                !IsLowercasePvpSha256(operation.FrozenSceneContractDigest) ||
                !string.Equals(
                    operation.FrozenSceneContract.IdentityDigest,
                    identity.Digest,
                    StringComparison.Ordinal))
            {
                error = "frozen scene-contract epoch/identity ledger is inconsistent";
                return false;
            }
            if (!TryRevalidateFrozenPeerSceneContract(
                    operation,
                    identity,
                    epoch,
                    out error))
            {
                return false;
            }
            digest = operation.FrozenSceneContractDigest;
            return true;
        }
        if (!TryBuildPeerSceneContractSnapshot(
                operation,
                identity,
                epoch,
                out PeerSceneContractSnapshot snapshot,
                out digest,
                out List<UnityEngine.Transform> pveEnemyMarkers,
                out List<UnityEngine.Transform> pveAuthoredEnemyMarkers,
                out error))
        {
            return false;
        }
        operation.FrozenSceneContract = snapshot;
        operation.FrozenSceneContractDigest = digest;
        operation.FrozenSceneContractEpoch = epoch;
        operation.FrozenPveEnemyMarkers.Clear();
        operation.FrozenPveEnemyMarkers.AddRange(pveEnemyMarkers);
        operation.FrozenPveAuthoredEnemyMarkers.Clear();
        operation.FrozenPveAuthoredEnemyMarkers.AddRange(
            pveAuthoredEnemyMarkers);
        return true;
    }

    private void ProcessPvpPendingSceneDisposition(
        ActiveMapOperation operation)
    {
        if (operation?.PeerSceneUnloadDispositionPending != true)
            return;
        if (CompletePvpPeerAgreementOnNativeReturn(operation))
        {
            operation.PeerSceneUnloadDispositionPending = false;
            operation.PeerSceneDispositionDeadlineTimestamp = 0;
            operation.PeerSceneDispositionFailureLogged = false;
            return;
        }
        if (!DeadlineExpired(operation.PeerSceneDispositionDeadlineTimestamp))
            return;
        operation.NetworkSpawnFailed = true;
        if (!operation.PeerSceneDispositionFailureLogged)
        {
            operation.PeerSceneDispositionFailureLogged = true;
            LogFrameworkEvidence(
                "pvp-scene-unload-disposition-timeout",
                operation,
                "unloadedSceneHandle=" + FrameworkEvidence.Number(
                    operation.PeerUnloadedSceneHandle) +
                "|outcome=owner-retained-native-return-requested");
            log.LogError("Peer scene unload did not prove replacement or " +
                "Operation Room before its bounded disposition deadline.");
        }
        RequestNativePvpAbortReturn(operation);
    }

    private static bool IsPeerSceneContractMarker(
        string name,
        PvpPeerIdentity identity)
    {
        if (string.IsNullOrEmpty(name) || identity == null)
            return false;
        return string.Equals(name, "MAP_ID_" + identity.MapId,
                   StringComparison.Ordinal) ||
               string.Equals(name, "SPAWN_SET_" + identity.SpawnSetId,
                   StringComparison.Ordinal) ||
               string.Equals(name, identity.CompanionReadyMarkerName,
                   StringComparison.Ordinal) ||
               string.Equals(name, identity.CompanionFailureMarkerName,
                   StringComparison.Ordinal) ||
               name.StartsWith("Team1_Spawn_",
                   StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Team1_Backup_Spawn_",
                   StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Team2_Spawn_",
                   StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Team2_Backup_Spawn_",
                   StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("PVE_PlayerSpawn_",
                   StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("PVE_EnemySpawn_",
                   StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("PVE_ExfilZone_",
                   StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("PVP_Team1Spawn_",
                   StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("PVP_Team2Spawn_",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPeerSceneTransformPath(UnityEngine.Transform item)
    {
        var segments = new List<string>();
        UnityEngine.Transform current = item;
        while (current != null)
        {
            segments.Add(CanonicalPeerField(current.name ?? string.Empty) + "#" +
                current.GetSiblingIndex().ToString(CultureInfo.InvariantCulture));
            current = current.parent;
        }
        segments.Reverse();
        return string.Join("/", segments);
    }

    private static string DescribePeerTransform(UnityEngine.Transform item) =>
        DescribePeerVector(item.position, 1000d) + "|" +
        DescribePeerVector(item.eulerAngles, 100d) + "|" +
        DescribePeerVector(item.lossyScale, 1000d);

    private static string DescribePeerVector(
        UnityEngine.Vector3 value,
        double scale) =>
        QuantizePeerFloat(value.x, scale) + "," +
        QuantizePeerFloat(value.y, scale) + "," +
        QuantizePeerFloat(value.z, scale);

    private static long QuantizePeerFloat(float value, double scale)
    {
        if (!float.IsFinite(value))
            throw new InvalidOperationException(
                "scene contract contains a non-finite numeric value");
        return checked((long)Math.Round(
            value * scale,
            MidpointRounding.AwayFromZero));
    }

    private static string CanonicalPeerField(string value)
    {
        value ??= string.Empty;
        return value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
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
            if (!TryComputePeerSceneContractDigest(
                    operation,
                    host.Identity,
                    host.SceneGenerationEpoch,
                    out string sceneContractDigest,
                    out string sceneContractError))
            {
                FailHostPvpAgreement(
                    "host scene contract digest failed: " + sceneContractError,
                    true);
                operation.NetworkSpawnFailed = true;
                return;
            }
            host.LocalSceneReady = true;
            host.LocalSceneReadyEpoch = host.SceneGenerationEpoch;
            host.LocalSceneContractDigest = sceneContractDigest;
            foreach (var entry in host.SceneContractDigestByConnectionId)
            {
                if (!string.Equals(
                        entry.Value,
                        sceneContractDigest,
                        StringComparison.Ordinal))
                {
                    FailHostPvpAgreement(
                        "remote peer " + entry.Key +
                        " scene-contract digest does not match the host",
                        true);
                    operation.NetworkSpawnFailed = true;
                    return;
                }
            }
            LogPvePeerAgreementEvidence(
                "pve-peer-scene-ready",
                host.Identity,
                operation,
                "host",
                host.SceneGenerationEpoch,
                "localReady=true|mirrorReady=true|sceneContractDigest=" +
                    FrameworkEvidence.Encode(sceneContractDigest) +
                "|safeCapacity=" + FrameworkEvidence.Number(
                    host.SafePveCapacity));
            if (host.SceneGenerationEpoch > 1)
            {
                LogPvePeerAgreementEvidence(
                    "pve-peer-restart-ready",
                    host.Identity,
                    operation,
                    "host",
                    host.SceneGenerationEpoch,
                    "localReady=true|replacementGeneration=true");
            }
            log.LogInfo("Host " + AgreementModeLabel(operation.Operation.Mode) +
                " scene preparation completed for " +
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
            if (!TryComputePeerSceneContractDigest(
                    operation,
                    remote.Identity,
                    remote.RequestedSceneGenerationEpoch,
                    out string sceneContractDigest,
                    out string sceneContractError))
            {
                FailRemotePvpAgreement(
                    "remote scene contract digest failed: " +
                    sceneContractError,
                    true);
                operation.NetworkSpawnFailed = true;
                return;
            }
            remote.LocalSceneReady = true;
            remote.LocalReadySceneGeneration = remote.LocalSceneGeneration;
            remote.LocalSceneContractDigest = sceneContractDigest;
            TrySendRemotePvpSceneReadyAcknowledgement(remote, allowResend: false);
        }
    }

    private bool TryBeginHostPvpSceneGeneration(
        HostPvpAgreement host,
        string reason,
        out string error,
        int evidenceTargetSceneHandle = 0,
        ulong evidenceTargetSceneGeneration = 0)
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

        ActiveMapOperation evidenceOperation = ResolvePvpEvidenceOperation(
            activeOperation,
            host.Identity);
        if (evidenceTargetSceneGeneration == 0)
        {
            evidenceTargetSceneGeneration = unchecked(
                (evidenceOperation?.EvidenceSceneGeneration ?? 0) + 1);
        }

        host.SceneGenerationEpoch++;
        host.LocalSceneReady = false;
        host.LocalSceneReadyEpoch = 0;
        host.LocalSceneContractDigest = null;
        host.SceneReadyEpochByConnectionId.Clear();
        host.SceneContractDigestByConnectionId.Clear();
        host.Phase = HostPvpAgreementPhase.WaitingForScenes;
        host.NativeTransitionArmed = true;
        host.DeadlineTimestamp = DeadlineAfter(PvpSceneReadyTimeoutSeconds);
        host.NativeLifecycleDeadlineTimestamp = 0;
        host.LoadingExitLogged = false;
        host.GroundedPlayersLogged = false;
        host.GroundedCandidatePopulationDigest = null;
        host.GroundedCandidateSamples = 0;
        host.GroundedCandidateLastFrame = -1;
        host.PvePopulationLogged = false;
        host.RuntimeReadyConnectionIds.Clear();
        host.PlayerPlacementByConnectionId.Clear();
        host.PlayerReadyConnectionIds.Clear();
        host.PopulationReadyConnectionIds.Clear();
        host.RuntimeOwnerManifestSent = false;
        host.RuntimeOwnerRetryTimestamp = 0;
        host.RuntimeBarrierDeadlineTimestamp = 0;
        host.LocalPlayerReady = false;
        host.LocalPlayerReadyStableFrames = 0;
        host.LocalPlayerReadyLastFrame = -1;
        host.PlayerBarrierPassed = false;
        host.PopulationManifest = null;
        host.PopulationRetryTimestamp = 0;
        host.BeginCommitSent = false;
        try
        {
            BroadcastHostPvpSceneReadyRequest(host, isRetry: false);
            LogFrameworkEvidence(
                "pvp-scene-epoch-issued",
                host.Identity.OperationId,
                host.Identity.MapId,
                evidenceTargetSceneHandle,
                evidenceTargetSceneGeneration,
                "epoch=" + FrameworkEvidence.Number(
                    host.SceneGenerationEpoch) +
                "|remoteRequired=" + FrameworkEvidence.Number(
                    host.RequiredConnectionIds.Count) +
                "|sceneCorrelation=target" +
                "|reason=" + FrameworkEvidence.Encode(reason));
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
        remote.NativeTransitionArmed = true;
        remote.RequestedLocalSceneGeneration =
            remote.SceneReadySentLocalGeneration + 1;
        remote.SceneReadySent = false;
        remote.LocalSceneContractDigest = null;
        remote.NativeOwnerAdopted = false;
        remote.NativeReadinessInitialized = false;
        remote.LoadingExitLogged = false;
        remote.GroundedPlayersLogged = false;
        remote.GroundedCandidatePopulationDigest = null;
        remote.GroundedCandidateSamples = 0;
        remote.GroundedCandidateLastFrame = -1;
        remote.PvePopulationLogged = false;
        remote.DeadlineTimestamp = DeadlineAfter(PvpSceneReadyTimeoutSeconds);
        bool preparedGenerationAlreadyLoaded =
            remote.LocalSceneReady &&
            remote.LocalReadySceneGeneration ==
                remote.RequestedLocalSceneGeneration &&
            activeOperation != null &&
            OperationMatchesPvpIdentity(activeOperation, remote.Identity);
        if (preparedGenerationAlreadyLoaded)
        {
            // The replacement scene may finish locally before the host issues
            // its next epoch. Re-enter every companion/spawn/Mirror-ready gate
            // under the new epoch instead of retaining the digest computed for
            // the prior request.
            remote.LocalSceneReady = false;
            remote.LocalReadySceneGeneration = 0;
        }
        log.LogInfo("Remote PVP peer accepted scene-generation readiness request: " +
            "epoch=" + sceneGenerationEpoch + ", localGeneration=" +
            remote.LocalSceneGeneration + ", requiredLocalGeneration=" +
            remote.RequestedLocalSceneGeneration + ".");
        if (preparedGenerationAlreadyLoaded)
            TryAdvancePvpPackageRuntimeReadiness(activeOperation);
        TrySendRemotePvpSceneReadyAcknowledgement(remote, allowResend: false);
    }

    private void TrySendRemotePvpSceneReadyAcknowledgement(
        RemotePvpAgreement remote,
        bool allowResend)
    {
        if (remote == null || remote != remotePvpAgreement ||
            !remote.ContentCommitted || !remote.LocalSceneReady ||
            remote.LocalReadySceneGeneration != remote.LocalSceneGeneration ||
            remote.RequestedSceneGenerationEpoch == 0 ||
            !IsLowercasePvpSha256(remote.LocalSceneContractDigest) ||
            NetworkClient.connection == null ||
            !NetworkClient.connection.isReady)
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

        if (!TryComputePeerSceneContractDigest(
                activeOperation,
                remote.Identity,
                remote.RequestedSceneGenerationEpoch,
                out string currentSceneContractDigest,
                out string currentSceneContractError) ||
            !string.Equals(
                currentSceneContractDigest,
                remote.LocalSceneContractDigest,
                StringComparison.Ordinal))
        {
            FailRemotePvpAgreement(
                "remote scene contract drifted before SceneReady send/resend: " +
                currentSceneContractError,
                true);
            return;
        }

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
            remote.LocalSceneContractDigest,
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
        LogPvePeerAgreementEvidence(
            "pve-peer-scene-ready",
            remote.Identity,
            activeOperation,
            "remote",
            remote.SceneReadySentEpoch,
            "localReady=true|mirrorReady=true|sceneContractDigest=" +
                FrameworkEvidence.Encode(remote.LocalSceneContractDigest) +
            "|localGeneration=" + FrameworkEvidence.Number(
                remote.SceneReadySentLocalGeneration) +
            "|safeCapacity=" + FrameworkEvidence.Number(
                remote.SafePveCapacity));
        if (remote.SceneReadySentEpoch > 1)
        {
            LogPvePeerAgreementEvidence(
                "pve-peer-restart-ready",
                remote.Identity,
                activeOperation,
                "remote",
                remote.SceneReadySentEpoch,
                "localReady=true|replacementGeneration=true");
        }
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

    private int CountRequiredPvpPeersSceneReadyForCurrentEpoch(
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
                epoch == host.SceneGenerationEpoch &&
                host.RequiredConnections.TryGetValue(
                    connectionId,
                    out NetworkConnectionToClient connection) &&
                connection != null && connection.isAuthenticated &&
                connection.isReady &&
                IsLowercasePvpSha256(host.LocalSceneContractDigest) &&
                host.SceneContractDigestByConnectionId.TryGetValue(
                    connectionId,
                    out string remoteDigest) &&
                string.Equals(
                    remoteDigest,
                    host.LocalSceneContractDigest,
                    StringComparison.Ordinal))
            {
                ready++;
            }
        }
        return ready;
    }

    private bool AreRequiredPvpPeersSceneReadyForCurrentEpoch(
        HostPvpAgreement host)
    {
        return host != null && host.RequiredConnectionIds.Count != 0 &&
            CountRequiredPvpPeersSceneReadyForCurrentEpoch(host) ==
            host.RequiredConnectionIds.Count;
    }

    private bool IsRemoteSceneReadyForCurrentRequest(
        RemotePvpAgreement remote)
    {
        return remote != null && remote.RequestedSceneGenerationEpoch != 0 &&
            remote.SceneReadySent &&
            remote.SceneReadySentEpoch == remote.RequestedSceneGenerationEpoch &&
            remote.SceneReadySentLocalGeneration ==
            remote.RequestedLocalSceneGeneration &&
            remote.LocalSceneGeneration == remote.RequestedLocalSceneGeneration &&
            IsLowercasePvpSha256(remote.LocalSceneContractDigest) &&
            NetworkClient.connection != null &&
            NetworkClient.connection.isReady;
    }

    private void NotifyPvpSceneLoading(ActiveMapOperation operation)
    {
        if (!IsPeerAgreementMode(operation?.Operation?.Mode) ||
            !HasPeerAgreementForOperation(operation))
            return;
        RemotePvpAgreement remote = remotePvpAgreement;
        if (!NetworkServer.active && remote != null && remote.ContentCommitted &&
            OperationMatchesPvpIdentity(operation, remote.Identity))
        {
            operation.NativeTransitionStarted = true;
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
                remote.LocalSceneContractDigest = null;
                remote.SceneReadySent = false;
                remote.NativeOwnerAdopted = false;
                remote.NativeReadinessInitialized = false;
                remote.LoadingExitLogged = false;
                remote.GroundedPlayersLogged = false;
                remote.GroundedCandidatePopulationDigest = null;
                remote.GroundedCandidateSamples = 0;
                remote.GroundedCandidateLastFrame = -1;
                remote.PvePopulationLogged = false;
                remote.RuntimeOwner = null;
                remote.RuntimeReadySent = false;
                remote.PendingPlayerPlacement = null;
                remote.PlayerReadySent = false;
                remote.PlayerReadyStableFrames = 0;
                remote.PlayerReadyLastFrame = -1;
                remote.PopulationManifest = null;
                remote.PopulationReadySent = false;
                remote.PopulationReadyStableFrames = 0;
                remote.PopulationReadyLastFrame = -1;
                remote.BeginCommitReceived = false;
                remote.RuntimeBarrierDeadlineTimestamp = 0;
                log.LogInfo("Remote " + AgreementModeLabel(
                    operation.Operation.Mode) +
                    " peer observed exact package scene " +
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
        if (!IsPeerAgreementMode(operation?.Operation?.Mode))
            return;
        if (operation.PveSoloMembershipFrozen &&
            !HasPeerAgreementForOperation(operation))
        {
            operation.NetworkSpawnFailed = true;
            FailActivePvpNativeLifecycle(
                operation,
                "solo PVE scene preparation failed: " + reason);
            return;
        }
        if (!HasPeerAgreementForOperation(operation))
            return;
        if (NetworkServer.active && hostPvpAgreement != null)
            FailHostPvpAgreement("host " + AgreementModeLabel(
                operation.Operation.Mode) +
                " scene preparation failed: " + reason, true);
        else if (remotePvpAgreement != null)
            FailRemotePvpAgreement(
                "remote " + AgreementModeLabel(operation.Operation.Mode) +
                " scene preparation failed: " + reason,
                true);
        operation.NetworkSpawnFailed = true;
    }

    private void ResetPvpSceneAgreementForReload(
        ActiveMapOperation operation,
        int evidenceTargetSceneHandle = 0)
    {
        if (!IsPeerAgreementMode(operation?.Operation?.Mode) ||
            !HasPeerAgreementForOperation(operation))
            return;
        if (operation.EvidenceSceneGeneration == ulong.MaxValue)
        {
            NotifyPvpScenePreparationFailed(
                operation,
                "peer restart target generation overflowed");
            return;
        }
        ulong targetGeneration = operation.EvidenceSceneGeneration + 1;
        if (operation.PeerAgreementLastResetTargetGeneration == targetGeneration)
        {
            log.LogInfo("Peer scene-restart acquisition was already applied " +
                "idempotently for targetGeneration=" + targetGeneration + ".");
            return;
        }
        if (operation.PeerAgreementLastResetTargetGeneration > targetGeneration)
        {
            NotifyPvpScenePreparationFailed(
                operation,
                "peer restart target generation regressed");
            return;
        }
        HostPvpAgreement host = hostPvpAgreement;
        if (NetworkServer.active && host != null &&
            OperationMatchesPvpIdentity(operation, host.Identity))
        {
            if (!TryBeginHostPvpSceneGeneration(
                    host,
                    "exact package scene restart",
                    out string generationError,
                    evidenceTargetSceneHandle,
                    targetGeneration))
            {
                FailHostPvpAgreement(generationError, true);
                operation.NetworkSpawnFailed = true;
                return;
            }
            operation.PeerAgreementLastResetTargetGeneration = targetGeneration;
        }
        RemotePvpAgreement remote = remotePvpAgreement;
        if (!NetworkServer.active && remote != null &&
            OperationMatchesPvpIdentity(operation, remote.Identity))
        {
            remote.SceneReadySent = false;
            remote.LocalSceneReady = false;
            remote.LocalReadySceneGeneration = 0;
            remote.LocalSceneContractDigest = null;
            remote.AwaitingLocalSceneGeneration = true;
            remote.NativeOwnerAdopted = false;
            remote.NativeReadinessInitialized = false;
            remote.LoadingExitLogged = false;
            remote.GroundedPlayersLogged = false;
            remote.GroundedCandidatePopulationDigest = null;
            remote.GroundedCandidateSamples = 0;
            remote.GroundedCandidateLastFrame = -1;
            remote.PvePopulationLogged = false;
            remote.RuntimeOwner = null;
            remote.RuntimeReadySent = false;
            remote.PendingPlayerPlacement = null;
            remote.PlayerReadySent = false;
            remote.PlayerReadyStableFrames = 0;
            remote.PlayerReadyLastFrame = -1;
            remote.PopulationManifest = null;
            remote.PopulationReadySent = false;
            remote.PopulationReadyStableFrames = 0;
            remote.PopulationReadyLastFrame = -1;
            remote.BeginCommitReceived = false;
            remote.RuntimeBarrierDeadlineTimestamp = 0;
            remote.DeadlineTimestamp = DeadlineAfter(PvpSceneReadyTimeoutSeconds);
            operation.PeerAgreementLastResetTargetGeneration = targetGeneration;
        }
    }

    private bool CompletePvpPeerAgreementOnNativeReturn(
        ActiveMapOperation operation)
    {
        if (!IsPeerAgreementMode(operation?.Operation?.Mode))
            return false;
        if (!TryObserveExactNativeOperationRoomReturn(
                operation,
                out string operationRoomPath,
                out string returnError))
            return false;

        if (operation.Operation.Mode ==
            ModdedOperationMode.PlayerVersusEnvironment)
        {
            bool successful = false;
            try
            {
                successful = GameManagerNetwork.instance?.SuccessfulOperation == true;
            }
            catch { }
            if (successful && !operation.EvidencePveSuccessfulOperationLogged)
            {
                operation.EvidencePveSuccessfulOperationLogged = true;
                LogFrameworkEvidence(
                    "pve-successful-operation",
                    operation,
                    "successfulOperation=true|resultOwner=native" +
                    "|observation=verified-operation-room-return");
            }
            LogFrameworkEvidence(
                "pve-operation-room-return",
                operation,
                "successfulOperation=" + successful.ToString().ToLowerInvariant() +
                "|scenePath=" + FrameworkEvidence.Encode(operationRoomPath) +
                "|gameModeNetId=" + FrameworkEvidence.Number(
                    operation.BootstrapSpawnedNetId) +
                "|packageSceneLoaded=false|ownerSpawned=false|returnOwner=native");
        }

        bool closed = false;
        if (hostPvpAgreement != null && OperationMatchesPvpIdentity(
                operation,
                hostPvpAgreement.Identity))
        {
            LogPvePeerAgreementEvidence(
                "pve-peer-natural-return",
                hostPvpAgreement.Identity,
                operation,
                "host",
                hostPvpAgreement.SceneGenerationEpoch,
                "destination=Operation Room|scenePath=" +
                    FrameworkEvidence.Encode(operationRoomPath) +
                "|returnOwner=native");
            TombstonePvpSession(
                hostPvpAgreement.Identity,
                hostPvpAgreement.SceneGenerationEpoch,
                "native-return");
            LogPvpSessionClose(
                hostPvpAgreement.Identity,
                operation,
                "host",
                hostPvpAgreement.SceneGenerationEpoch,
                "native-return",
                "shipped native return to Operation Room");
            hostPvpAgreement = null;
            closed = true;
        }
        if (remotePvpAgreement != null && OperationMatchesPvpIdentity(
                operation,
                remotePvpAgreement.Identity))
        {
            LogPvePeerAgreementEvidence(
                "pve-peer-natural-return",
                remotePvpAgreement.Identity,
                operation,
                "remote",
                remotePvpAgreement.RequestedSceneGenerationEpoch,
                "destination=Operation Room|scenePath=" +
                    FrameworkEvidence.Encode(operationRoomPath) +
                "|returnOwner=native-sync");
            TombstonePvpSession(
                remotePvpAgreement.Identity,
                remotePvpAgreement.RequestedSceneGenerationEpoch,
                "native-return");
            LogPvpSessionClose(
                remotePvpAgreement.Identity,
                operation,
                "remote",
                remotePvpAgreement.RequestedSceneGenerationEpoch,
                "native-return",
                "shipped native return to Operation Room");
            remotePvpAgreement = null;
            closed = true;
        }
        operation.NativeLaunchInvoked = false;
        operation.NativeTransitionStarted = false;
        operation.BootstrapSpawnedNetId = 0;
        operation.PveSoloMembershipFrozen = false;
        operation.PveSoloSessionDigest = string.Empty;
        operation.PveSoloLateJoinHandled = false;
        operation.PvpAbortReturnConfirmed = true;
        operation.PvpAbortReturnAccepted = true;
        operation.NetworkSpawnFailed = false;
        if (ReferenceEquals(activeOperation, operation))
            activeOperation = null;
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
        if (!IsPeerAgreementMode(operation?.Operation?.Mode))
            return true;
        if (operation.Operation.Mode ==
                ModdedOperationMode.PlayerVersusEnvironment &&
            !HasPeerAgreementForOperation(operation))
        {
            EnforceSoloPveMembershipFreeze(operation);
            if (!TryValidateSoloPveNetworkOwnership(operation, out string error))
            {
                operation.NetworkSpawnFailed = true;
                FailActivePvpNativeLifecycle(
                    operation,
                    "solo PVE owner spawn authorization failed: " + error);
                return false;
            }
            if (!TryValidateSoloPveRuntimeCompanion(
                    operation,
                    out bool runtimeReady,
                    out error))
            {
                operation.NetworkSpawnFailed = true;
                FailActivePvpNativeLifecycle(
                    operation,
                    "solo PVE runtime companion failed before owner spawn: " +
                    error);
                return false;
            }
            if (!runtimeReady)
                return false;
            return true;
        }
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
            !TryValidateHostPvpMembership(
                host,
                out membershipError,
                requireReady: true))
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
        bool capacityValid;
        string capacityError;
        if (operation.Operation.Mode == ModdedOperationMode.PlayerVersusPlayer)
        {
            capacityValid = TryValidateCurrentPvpTeamCapacity(
                operation,
                host.RequiredConnectionIds.Count + 1,
                out capacityError);
        }
        else
        {
            capacityValid = TryValidateCurrentPvePeerSpawnContract(
                operation,
                host,
                host.RequiredConnectionIds.Count + 1,
                out capacityError);
        }
        if (!capacityValid)
        {
            FailHostPvpAgreement(capacityError, true);
            operation.NetworkSpawnFailed = true;
            return false;
        }
        host.Phase = HostPvpAgreementPhase.SpawnAuthorized;
        return true;
    }

    private static bool TryValidateSoloPveRuntimeCompanion(
        ActiveMapOperation operation,
        out bool ready,
        out string error)
    {
        ready = false;
        error = string.Empty;
        if (operation?.Operation?.Mode !=
                ModdedOperationMode.PlayerVersusEnvironment ||
            !operation.PveSoloMembershipFrozen || operation.PeerAgreementRequired)
        {
            error = "solo PVE runtime-companion ownership flags are invalid";
            return false;
        }

        ModdedRuntimeCompanionDefinition companion = operation.Map?.RuntimeCompanion;
        if (companion == null)
        {
            ready = true;
            return true;
        }

        Scene scene = FindLoadedSceneByHandle(operation.SceneHandle);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            error = "exact solo PVE generation scene is unavailable";
            return false;
        }

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
            error = "package runtime companion emitted failure marker '" +
                companion.FailureMarkerName + "' in exact scene handle=" +
                scene.handle + "; count=" + failureMarkers;
            return false;
        }
        if (readyMarkers == 0)
        {
            // Before native owner publication, zero means the companion is still
            // completing its bounded scene gate. After publication it means a
            // previously accepted contract marker disappeared and is fatal.
            if (operation.NetworkSpawnRequested ||
                operation.ReadinessInitializationClaimed ||
                operation.AllPlayersLoadedClaimed)
            {
                error = "package runtime companion ready marker disappeared after " +
                    "native owner publication";
                return false;
            }
            return true;
        }
        if (readyMarkers != 1)
        {
            error = "package runtime companion ready marker was ambiguous in exact " +
                "scene handle=" + scene.handle + "; marker='" +
                companion.ReadyMarkerName + "', count=" + readyMarkers;
            return false;
        }

        ready = true;
        return true;
    }

    private bool TryValidatePeerOwnerSpawnBoundary(
        ActiveMapOperation operation,
        out string error)
    {
        error = string.Empty;
        if (operation == null || !NetworkServer.active)
        {
            error = "native owner spawn boundary has no active server operation";
            return false;
        }
        LocalConnectionToClient local = NetworkServer.localConnection;
        if (local == null || !local.isAuthenticated || !local.isReady)
        {
            error = "host-local Mirror connection is not exactly authenticated/Ready";
            return false;
        }
        if (!operation.PeerAgreementRequired)
        {
            if (operation.PveSoloMembershipFrozen &&
                !TryValidateSoloPveNetworkOwnership(operation, out error))
            {
                return false;
            }
            return true;
        }

        HostPvpAgreement host = hostPvpAgreement;
        if (host == null || host.Identity == null ||
            !NetworkClient.active || NetworkClient.connection == null ||
            host.FrozenLocalClientConnection == null ||
            !SamePvpNetworkConnection(
                host.FrozenLocalClientConnection,
                NetworkClient.connection) ||
            !NetworkClient.connection.isAuthenticated ||
            !NetworkClient.connection.isReady ||
            !OperationMatchesPvpIdentity(operation, host.Identity) ||
            host.Phase != HostPvpAgreementPhase.SpawnAuthorized ||
            !IsHostLocalSceneReadyForCurrentEpoch(host) ||
            !AreRequiredPvpPeersSceneReadyForCurrentEpoch(host) ||
            !TryValidateHostPvpMembership(host, out error, requireReady: true))
        {
            if (string.IsNullOrEmpty(error))
                error = "peer scene/Ready ownership changed at native spawn boundary";
            return false;
        }
        if (!TryComputePeerSceneContractDigest(
                operation,
                host.Identity,
                host.SceneGenerationEpoch,
                out string observedDigest,
                out error) ||
            !string.Equals(
                observedDigest,
                host.LocalSceneContractDigest,
                StringComparison.Ordinal))
        {
            if (string.IsNullOrEmpty(error))
                error = "host scene-contract digest drifted at native spawn boundary";
            return false;
        }
        foreach (int connectionId in host.RequiredConnectionIds)
        {
            if (!host.SceneContractDigestByConnectionId.TryGetValue(
                    connectionId,
                    out string remoteDigest) ||
                !string.Equals(
                    remoteDigest,
                    observedDigest,
                    StringComparison.Ordinal))
            {
                error = "remote scene-contract receipt drifted at native spawn boundary";
                return false;
            }
        }
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
        if (!IsPeerAgreementMode(operation?.Operation?.Mode))
            return true;
        if (operation.Operation.Mode ==
                ModdedOperationMode.PlayerVersusEnvironment &&
            !HasPeerAgreementForOperation(operation))
        {
            EnforceSoloPveMembershipFreeze(operation);
            return TryValidateSoloPveNetworkOwnership(operation, out _);
        }
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

    private bool TryValidateSoloPveNetworkOwnership(
        ActiveMapOperation operation,
        out string error)
    {
        error = string.Empty;
        if (operation?.Operation?.Mode !=
                ModdedOperationMode.PlayerVersusEnvironment ||
            !operation.PveSoloMembershipFrozen ||
            operation.PveSoloLateJoinHandled || operation.NetworkSpawnFailed ||
            operation.PeerAgreementRequired || hostPvpAgreement != null ||
            remotePvpAgreement != null || !NetworkServer.active ||
            !NetworkClient.active ||
            !IsLowercasePvpSha256(operation.PveSoloSessionDigest))
        {
            error = "frozen solo PVE ownership flags are invalid";
            return false;
        }
        try
        {
            PvpConnectionSnapshot current = CaptureRemotePvpConnectionSnapshot();
            if (current.All.Count != 0)
            {
                error = "remote membership is no longer empty (authenticated=" +
                    current.Authenticated.Count + ", pending=" +
                    current.Pending.Count + ")";
                return false;
            }
            NetworkConnectionToClient serverLocal = NetworkServer.localConnection;
            NetworkConnection clientLocal = NetworkClient.connection;
            if (serverLocal == null || clientLocal == null ||
                !serverLocal.isAuthenticated || !serverLocal.isReady ||
                !clientLocal.isAuthenticated || !clientLocal.isReady)
            {
                error = "host-local Mirror server/client pair is not authenticated/Ready";
                return false;
            }
            if (operation.PveSoloFrozenServerLocalConnection == null &&
                operation.PveSoloFrozenClientLocalConnection == null)
            {
                operation.PveSoloFrozenServerLocalConnection = serverLocal;
                operation.PveSoloFrozenClientLocalConnection = clientLocal;
            }
            else if (operation.PveSoloFrozenServerLocalConnection == null ||
                operation.PveSoloFrozenClientLocalConnection == null ||
                !SamePvpNetworkConnection(
                    operation.PveSoloFrozenServerLocalConnection,
                    serverLocal) ||
                !SamePvpNetworkConnection(
                    operation.PveSoloFrozenClientLocalConnection,
                    clientLocal))
            {
                error = "frozen solo PVE host-local Mirror connection pair changed";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = "network membership could not be verified: " +
                ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private static bool OperationMatchesPvpIdentity(
        ActiveMapOperation operation,
        PvpPeerIdentity identity)
    {
        return operation != null && identity != null && operation.Map != null &&
            operation.Operation != null && operation.SceneSelection != null &&
            operation.PeerAgreementRequired &&
            IsLowercasePvpSha256(identity.Digest) &&
            string.Equals(
                operation.PeerAgreementIdentityDigest,
                identity.Digest,
                StringComparison.Ordinal) &&
            (string.Equals(
                 operation.PeerAgreementRole,
                 "host",
                 StringComparison.Ordinal) ||
             string.Equals(
                 operation.PeerAgreementRole,
                 "remote",
                 StringComparison.Ordinal)) &&
            identity.Mode == (int)operation.Operation.Mode &&
            string.Equals(operation.Map.Id, identity.MapId, StringComparison.Ordinal) &&
            string.Equals(operation.Map.PackageId, identity.PackageId,
                StringComparison.Ordinal) &&
            string.Equals(operation.Map.PackageVersion, identity.PackageVersion,
                StringComparison.Ordinal) &&
            string.Equals(operation.Map.PackageContentId, identity.PackageContentId,
                StringComparison.Ordinal) &&
            string.Equals(operation.Operation.Id, identity.OperationId,
                StringComparison.Ordinal) &&
            operation.Operation.MinimumPlayers == identity.MinimumPlayers &&
            operation.Operation.MaximumPlayers == identity.MaximumPlayers &&
            string.Equals(operation.Operation.SpawnSetId, identity.SpawnSetId,
                StringComparison.Ordinal) &&
            string.Equals(operation.TimeCode, identity.TimeCode,
                StringComparison.Ordinal) &&
            string.Equals(operation.SceneSelection.Id, identity.VariantId,
                StringComparison.Ordinal) &&
            string.Equals(operation.SceneSelection.ScenePath, identity.ScenePath,
                StringComparison.Ordinal) &&
            (operation.Operation.Mode == ModdedOperationMode.PlayerVersusPlayer
                ? identity.MinimumEnemies == 0 && identity.MaximumEnemies == 0 &&
                  identity.RequestedEnemies == 0
                : operation.Operation.MinimumEnemies == identity.MinimumEnemies &&
                  operation.Operation.MaximumEnemies == identity.MaximumEnemies &&
                  operation.RequestedPveEnemyCount == identity.RequestedEnemies);
    }

    private Dictionary<int, NetworkConnectionToClient>
        CaptureAuthenticatedRemotePvpConnections(bool requireReady)
    {
        PvpConnectionSnapshot snapshot = CaptureRemotePvpConnectionSnapshot();
        if (snapshot.Pending.Count != 0)
        {
            throw new InvalidOperationException(
                "remote peer(s) are connected but not authenticated: " +
                string.Join(",", snapshot.Pending.Keys.OrderBy(id => id)));
        }
        if (requireReady)
        {
            int[] notReady = snapshot.Authenticated
                .Where(entry => !entry.Value.isReady)
                .Select(entry => entry.Key)
                .OrderBy(id => id)
                .ToArray();
            if (notReady.Length != 0)
            {
                throw new InvalidOperationException(
                    "authenticated remote peer(s) are not Mirror Ready: " +
                    string.Join(",", notReady));
            }
        }
        return snapshot.Authenticated;
    }

    private PvpConnectionSnapshot CaptureRemotePvpConnectionSnapshot()
    {
        if (!NetworkServer.active || NetworkServer.connections == null)
            throw new InvalidOperationException(
                "Mirror server connection registry is unavailable");
        var snapshot = new PvpConnectionSnapshot();
        LocalConnectionToClient localConnection =
            NetworkServer.localConnection as LocalConnectionToClient;
        if (localConnection == null ||
            !NetworkServer.connections.TryGetValue(
                localConnection.connectionId,
                out NetworkConnectionToClient registeredLocal) ||
            !SamePvpNetworkConnection(localConnection, registeredLocal))
        {
            throw new InvalidOperationException(
                "Mirror host local connection is not exactly registered");
        }
        int localConnectionId = localConnection.connectionId;
        var enumerator = NetworkServer.connections.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                var entry = enumerator.Current;
                NetworkConnectionToClient connection = entry.Value;
                if (entry.Key == localConnectionId)
                {
                    if (!SamePvpNetworkConnection(connection, localConnection))
                        throw new InvalidOperationException(
                            "Mirror local connection key contains a foreign peer");
                    continue;
                }
                if (connection == null)
                    throw new InvalidOperationException(
                        "Mirror remote connection registry contains a null " +
                        "entry for " + entry.Key);
                if (connection is LocalConnectionToClient)
                    throw new InvalidOperationException(
                        "Mirror remote registry contains a local connection " +
                        "under nonlocal key " + entry.Key);
                if (connection.connectionId != entry.Key)
                    throw new InvalidOperationException(
                        "Mirror remote connection key/identity mismatch for " +
                        entry.Key);
                snapshot.All.Add(entry.Key, connection);
                if (connection.isAuthenticated)
                    snapshot.Authenticated.Add(entry.Key, connection);
                else
                    snapshot.Pending.Add(entry.Key, connection);
            }
        }
        finally
        {
            enumerator.Dispose();
        }
        return snapshot;
    }

    private bool TryValidateHostPvpMembership(
        HostPvpAgreement host,
        out string error,
        bool requireReady = false)
    {
        error = string.Empty;
        try
        {
            PvpConnectionSnapshot current = CaptureRemotePvpConnectionSnapshot();
            var unexpected = current.All
                .Where(entry =>
                    !host.RequiredConnections.TryGetValue(
                        entry.Key,
                        out NetworkConnectionToClient required) ||
                    !SamePvpNetworkConnection(required, entry.Value))
                .OrderBy(entry => entry.Key)
                .ToArray();
            if (unexpected.Length != 0)
            {
                string ids = string.Join(",", unexpected.Select(entry =>
                    entry.Key.ToString(CultureInfo.InvariantCulture)));
                string authStates = string.Join(",", unexpected.Select(entry =>
                    entry.Key.ToString(CultureInfo.InvariantCulture) + ":" +
                    (entry.Value.isAuthenticated ? "authenticated" : "pending")));
                string refusalAttemptId = ComputeLateJoinAttemptId(
                    host.Identity?.Digest ?? host.SessionNonce,
                    host.SceneGenerationEpoch,
                    ids + "|" + authStates);
                foreach (var entry in unexpected)
                {
                    RetainRefusedPvpConnection(
                        entry.Value,
                        refusalAttemptId);
                    try { entry.Value?.Disconnect(); }
                    catch { }
                }
                if (host.Operation.Mode ==
                    ModdedOperationMode.PlayerVersusEnvironment)
                {
                    string attemptId = refusalAttemptId;
                    if (host.Identity != null)
                    {
                        LogPveLateJoinRefusal(
                            host.Identity,
                            activeOperation,
                            "host",
                            host.SceneGenerationEpoch,
                            attemptId,
                            ids + "|auth=" + authStates,
                            host.RequiredConnectionIds.Count,
                            current.All.Count,
                            "refused-abort");
                    }
                    else
                    {
                        LogFrameworkEvidence(
                            "pve-peer-late-join-refused",
                            host.Operation.Id,
                            host.Map.Id,
                            activeOperation?.SceneHandle ?? 0,
                            activeOperation?.EvidenceSceneGeneration ?? 0,
                            "role=host|identityDigest=pending-hello" +
                            "|attemptId=" + FrameworkEvidence.Encode(attemptId) +
                            "|epoch=0|connectionIds=" +
                                FrameworkEvidence.Encode(ids) +
                            "|authenticationStates=" +
                                FrameworkEvidence.Encode(authStates) +
                            "|outcome=refused-abort");
                    }
                    error = "pve late join refused attemptId=" + attemptId +
                        " connectionIds=" + ids + " auth=" + authStates;
                    return false;
                }
                error = AgreementModeLabel(host.Operation.Mode) +
                    " lobby gained or replaced a frozen connection: " + ids;
                return false;
            }
            if (!host.RequiredConnectionIds.SetEquals(current.All.Keys))
            {
                error = AgreementModeLabel(host.Operation.Mode) +
                    " lobby membership changed during peer agreement";
                return false;
            }
            foreach (int connectionId in host.RequiredConnectionIds)
            {
                if (!host.RequiredConnections.TryGetValue(
                        connectionId,
                        out NetworkConnectionToClient required) ||
                    !current.All.TryGetValue(
                        connectionId,
                        out NetworkConnectionToClient connected) ||
                    !SamePvpNetworkConnection(required, connected) ||
                    !connected.isAuthenticated ||
                    (requireReady && !connected.isReady))
                {
                    error = AgreementModeLabel(host.Operation.Mode) +
                        " frozen connection lost exact authenticated ownership for " +
                        "connection=" + connectionId;
                    return false;
                }
                bool helloPresent = pvpClientHellos.TryGetValue(
                    connectionId,
                    out PvpClientHello hello);
                if (helloPresent &&
                    !SamePvpNetworkConnection(hello.Connection, connected))
                {
                    error = AgreementModeLabel(host.Operation.Mode) +
                        " ClientHello connection reference changed for connection=" +
                        connectionId;
                    return false;
                }
                if (host.Phase != HostPvpAgreementPhase.WaitingForHello &&
                    (!helloPresent ||
                     !host.HelloConnectionIds.Contains(connectionId)))
                {
                    error = AgreementModeLabel(host.Operation.Mode) +
                        " frozen ClientHello ownership disappeared for connection=" +
                        connectionId;
                    return false;
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            error = AgreementModeLabel(host?.Operation?.Mode) +
                " lobby membership validation failed: " + ex.Message;
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
            if (activeOperation.NativeLaunchInvoked ||
                activeOperation.NativeTransitionStarted ||
                host.NativeLaunchInvoked ||
                host.NativeTransitionCommitted)
            {
                RequestNativePvpAbortReturn(activeOperation);
            }
        }
        SetNativeConfirmationLoadingState(host.Presentation, false);
        TombstonePvpSession(
            host.Identity,
            host.SceneGenerationEpoch,
            "failed");
        LogPvpSessionClose(
            host.Identity,
            activeOperation,
            "host",
            host.SceneGenerationEpoch,
            "failed",
            reason);
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

    private void ClearRemotePvpAgreement(
        string reason,
        string outcome = "failed")
    {
        RemotePvpAgreement remote = remotePvpAgreement;
        if (remote != null)
        {
            if (remote.Identity?.Mode ==
                    (int)ModdedOperationMode.PlayerVersusEnvironment &&
                TryExtractLateJoinAttemptId(reason, out string attemptId))
            {
                LogPveLateJoinRefusal(
                    remote.Identity,
                    activeOperation,
                    "remote",
                    remote.RequestedSceneGenerationEpoch,
                    attemptId,
                    "host-observed",
                    frozenRemoteCount: -1,
                    currentRemoteCount: -1,
                    "host-refusal-adopted");
            }
            TombstonePvpSession(
                remote.Identity,
                remote.RequestedSceneGenerationEpoch,
                outcome);
            LogPvpSessionClose(
                remote.Identity,
                activeOperation,
                "remote",
                remote.RequestedSceneGenerationEpoch,
                outcome,
                reason);
        }
        if (remote != null && activeOperation != null &&
            OperationMatchesPvpIdentity(activeOperation, remote.Identity))
        {
            if (remote.NativeTransitionCommittedEpoch == 0 &&
                !activeOperation.NativeTransitionStarted)
            {
                if (loadedMapBundles.TryGetValue(
                        remote.Map.Id,
                        out LoadedMapBundles loaded) &&
                    loaded != null && loaded.Map != null &&
                    string.Equals(
                        loaded.Map.PackageContentId,
                        remote.Identity.PackageContentId,
                        StringComparison.Ordinal))
                {
                    TryDiscardStaleCompletedMapBundle(
                        remote.Map.Id,
                        loaded,
                        "pre-transition remote agreement close");
                }
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

    private static bool TryExtractLateJoinAttemptId(
        string reason,
        out string attemptId)
    {
        const string prefix = "pve late join refused attemptId=";
        attemptId = string.Empty;
        if (string.IsNullOrEmpty(reason) ||
            !reason.StartsWith(prefix, StringComparison.Ordinal) ||
            reason.Length < prefix.Length + 64)
        {
            return false;
        }
        attemptId = reason.Substring(prefix.Length, 64);
        return IsLowercasePvpSha256(attemptId);
    }

    private void RequestNativePvpAbortReturn(ActiveMapOperation operation)
    {
        if (operation == null || operation.PvpAbortReturnConfirmed)
            return;
        if (operation.PvpAbortReturnAccepted &&
            !DeadlineExpired(operation.PvpAbortReturnDeadlineTimestamp))
            return;
        if (operation.PvpAbortReturnAccepted)
        {
            operation.PvpAbortReturnAccepted = false;
            log.LogError("Native abort return was accepted but not confirmed " +
                "before its bounded disposition deadline; retrying.");
        }
        if (operation.PvpAbortReturnAttemptCount >=
                PvpAbortReturnMaximumAttempts ||
            (operation.PvpAbortReturnNextRetryTimestamp > 0 &&
             !DeadlineExpired(operation.PvpAbortReturnNextRetryTimestamp)))
        {
            return;
        }

        operation.PvpAbortReturnRequested = true;
        operation.PvpAbortReturnAttemptCount++;
        operation.PvpAbortReturnNextRetryTimestamp = DeadlineAfter(
            PvpAbortReturnRetrySeconds);
        bool accepted = false;
        string acceptedOwner = string.Empty;

        if (NetworkServer.active && GameManagerNetwork.instance != null)
        {
            try
            {
                // Exact installed current-build owner at RVA 0x0090DC00. The
                // owned host body performs its normal pre-return cleanup and
                // invokes the shipped network scene change to "Operation Room".
                GameManagerNetwork.instance.EndOperation();
                accepted = true;
                acceptedOwner = "GameManagerNetwork.EndOperation";
                log.LogWarning("PVP agreement abort entered the shipped " +
                    "GameManagerNetwork.EndOperation return to Operation Room.");
            }
            catch (Exception ex)
            {
                log.LogError("PVP agreement abort could not enter the shipped " +
                    "Operation Room return: " + ex.GetType().Name + ": " +
                    ex.Message + ".");
            }
        }

        if (!accepted && !NetworkServer.active && NetworkClient.active)
        {
            try
            {
                NetworkClient.Disconnect();
                accepted = true;
                acceptedOwner = "NetworkClient.Disconnect";
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
        if (!accepted && scene.IsValid() && scene.isLoaded)
        {
            try
            {
                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload == null)
                    throw new InvalidOperationException(
                        "Unity rejected the exact-scene unload request");
                accepted = true;
                acceptedOwner = "SceneManager.UnloadSceneAsync";
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
        if (accepted)
        {
            operation.PvpAbortReturnAccepted = true;
            operation.PvpAbortReturnDeadlineTimestamp = DeadlineAfter(
                PvpAbortReturnConfirmationSeconds);
            LogFrameworkEvidence(
                "pvp-native-abort-return-accepted",
                operation,
                "attempt=" + FrameworkEvidence.Number(
                    operation.PvpAbortReturnAttemptCount) +
                "|owner=" + FrameworkEvidence.Encode(acceptedOwner));
            return;
        }
        if (operation.PvpAbortReturnAttemptCount >=
            PvpAbortReturnMaximumAttempts)
        {
            LogFrameworkEvidence(
                "pvp-native-abort-return-failed",
                operation,
                "attempts=" + FrameworkEvidence.Number(
                    operation.PvpAbortReturnAttemptCount) +
                "|outcome=unconfirmed-owner-retained");
        }
    }

    private static bool IsNativeAbortReturnPending(
        ActiveMapOperation operation)
    {
        return IsPeerAgreementMode(operation?.Operation?.Mode) &&
            !operation.PvpAbortReturnConfirmed &&
            (operation.NetworkSpawnFailed ||
             operation.PvpAbortReturnRequested ||
             operation.PvpAbortReturnAccepted);
    }

    private void NotifyNativeAbortReturnPackageReloaded(
        ActiveMapOperation operation,
        int previousSceneHandle,
        int replacementSceneHandle)
    {
        if (operation == null)
            return;

        // A newly loaded package scene is concrete evidence that an accepted
        // EndOperation call did not commit the native Operation Room return.
        // Invalidate only that acceptance window, retain the bounded attempt
        // count, and make the next retry eligible immediately. Never reset the
        // failed generation or convert this callback into Restart ownership.
        operation.NetworkSpawnFailed = true;
        operation.PvpAbortReturnRequested = true;
        operation.PvpAbortReturnAccepted = false;
        operation.PvpAbortReturnDeadlineTimestamp = 0;
        operation.PvpAbortReturnNextRetryTimestamp = 0;
        LogFrameworkEvidence(
            "pvp-native-abort-package-reload-rejected",
            operation,
            "previousSceneHandle=" + FrameworkEvidence.Number(
                previousSceneHandle) +
            "|replacementSceneHandle=" + FrameworkEvidence.Number(
                replacementSceneHandle) +
            "|attempts=" + FrameworkEvidence.Number(
                operation.PvpAbortReturnAttemptCount) +
            "|outcome=failed-generation-retained");
        log.LogError("Native abort return lost a race with a package-scene " +
            "reload; the replacement generation was rejected without scene " +
            "preparation and the bounded Operation Room return will retry: " +
            "previousHandle=" + previousSceneHandle +
            ", replacementHandle=" + replacementSceneHandle + ".");
        RequestNativePvpAbortReturn(operation);
    }

    private void FailActivePvpNativeLifecycle(
        ActiveMapOperation operation,
        string reason)
    {
        if (!IsPeerAgreementMode(operation?.Operation?.Mode))
            return;
        operation.NetworkSpawnFailed = true;
        if (operation.PveSoloMembershipFrozen &&
            !HasPeerAgreementForOperation(operation))
        {
            RequestNativePvpAbortReturn(operation);
            return;
        }
        if (!HasPeerAgreementForOperation(operation))
            return;
        if (NetworkServer.active && hostPvpAgreement != null &&
            OperationMatchesPvpIdentity(operation, hostPvpAgreement.Identity))
        {
            FailHostPvpAgreement("host native " + AgreementModeLabel(
                operation.Operation.Mode) + " lifecycle failed: " + reason, true);
            return;
        }
        if (!NetworkServer.active && remotePvpAgreement != null &&
            OperationMatchesPvpIdentity(operation, remotePvpAgreement.Identity))
        {
            FailRemotePvpAgreement(
                "remote native " + AgreementModeLabel(operation.Operation.Mode) +
                " lifecycle failed: " + reason,
                true);
            return;
        }
        RequestNativePvpAbortReturn(operation);
    }

    private void NotifyPvpNativeLaunchInvoked(ActiveMapOperation operation)
    {
        HostPvpAgreement host = hostPvpAgreement;
        if (IsPeerAgreementMode(operation?.Operation?.Mode) &&
            NetworkServer.active && host != null &&
            OperationMatchesPvpIdentity(operation, host.Identity))
        {
            host.NativeLaunchInvoked = true;
        }
    }

    private void NotifyPvpNativeOwnerAdopted(ActiveMapOperation operation)
    {
        if (!IsPeerAgreementMode(operation?.Operation?.Mode) ||
            !HasPeerAgreementForOperation(operation))
            return;
        RemotePvpAgreement remote = remotePvpAgreement;
        if (!NetworkServer.active && remote != null &&
            IsRemoteSceneReadyForCurrentRequest(remote) &&
            OperationMatchesPvpIdentity(operation, remote.Identity))
        {
            remote.NativeOwnerAdopted = true;
            remote.DeadlineTimestamp = DeadlineAfter(PvpNativeLifecycleTimeoutSeconds);
            LogPvePeerAgreementEvidence(
                "pve-peer-owner-adopted",
                remote.Identity,
                operation,
                "remote",
                remote.RequestedSceneGenerationEpoch,
                "ownerType=StandardPVE|hostOwned=true");
            log.LogInfo("Remote " + AgreementModeLabel(operation.Operation.Mode) +
                " peer adopted the agreed native owner; awaiting " +
                "the shipped readiness lifecycle.");
        }
    }

    private void NotifyPvpNativeReadinessInitialized(ActiveMapOperation operation)
    {
        if (!IsPeerAgreementMode(operation?.Operation?.Mode) ||
            !HasPeerAgreementForOperation(operation))
            return;
        RemotePvpAgreement remote = remotePvpAgreement;
        if (!NetworkServer.active && remote != null &&
            IsRemoteSceneReadyForCurrentRequest(remote) &&
            OperationMatchesPvpIdentity(operation, remote.Identity))
        {
            remote.NativeOwnerAdopted = true;
            remote.NativeReadinessInitialized = true;
            remote.DeadlineTimestamp = 0;
            TrySendRemotePeerRuntimeReady(operation, remote);
            log.LogInfo("Remote " + AgreementModeLabel(operation.Operation.Mode) +
                " peer completed the agreed native readiness " +
                "lifecycle.");
        }
    }

    private void NotifyPvpNetworkOwnerSpawned(ActiveMapOperation operation)
    {
        HostPvpAgreement host = hostPvpAgreement;
        if (!IsPeerAgreementMode(operation?.Operation?.Mode) ||
            !NetworkServer.active || host == null ||
            !OperationMatchesPvpIdentity(operation, host.Identity))
        {
            return;
        }
        host.NativeLifecycleDeadlineTimestamp = operation.AllPlayersLoaded
            ? 0
            : DeadlineAfter(PvpNativeLifecycleTimeoutSeconds);
        host.RuntimeBarrierDeadlineTimestamp = DeadlineAfter(
            PeerRuntimeReadyTimeoutSeconds);
        PublishPeerRuntimeOwnerManifest(operation, host);
        log.LogInfo(AgreementModeLabel(operation.Operation.Mode) +
            " native owner spawned; bounded native readiness lifecycle " +
            (operation.AllPlayersLoaded ? "was already complete" : "started") +
            " for operation=" + operation.Operation.Id + ".");
    }

    private void NotifyPvpAllPlayersLoaded(ActiveMapOperation operation)
    {
        HostPvpAgreement host = hostPvpAgreement;
        if (IsPeerAgreementMode(operation?.Operation?.Mode) &&
            NetworkServer.active && host != null &&
            OperationMatchesPvpIdentity(operation, host.Identity))
        {
            host.NativeLifecycleDeadlineTimestamp = 0;
            log.LogInfo(AgreementModeLabel(operation.Operation.Mode) +
                " shipped all-players-loaded lifecycle passed before " +
                "the native-start deadline.");
        }
    }

    private void SendPvpOffer(
        NetworkConnection connection,
        HostPvpAgreement host,
        int connectionId)
    {
        if (host?.Identity == null ||
            !host.FrozenSlotByConnectionId.TryGetValue(
                connectionId,
                out int recipientSlot) ||
            recipientSlot <= 0 ||
            host.FrozenPeerBySlot.Count != host.Identity.ParticipantCount)
        {
            throw new InvalidOperationException(
                "host Offer has no exact frozen recipient slot");
        }
        SendPvpEnvelope(connection, writer =>
        {
            NetworkWriterExtensions.WriteUShort(
                writer,
                PvpAgreementProtocolVersion);
            NetworkWriterExtensions.WriteByte(
                writer,
                (byte)PvpAgreementMessageKind.Offer);
            NetworkWriterExtensions.WriteInt(writer, 0);
            WritePvpIdentity(writer, host.Identity);
            NetworkWriterExtensions.WriteInt(writer, recipientSlot);
            NetworkWriterExtensions.WriteInt(
                writer,
                host.FrozenPeerBySlot.Count);
            foreach (PvpFrozenPeer peer in host.FrozenPeerBySlot.Values
                         .OrderBy(item => item.Slot))
            {
                NetworkWriterExtensions.WriteInt(writer, peer.Slot);
                NetworkWriterExtensions.WriteInt(writer, peer.ConnectionId);
                NetworkWriterExtensions.WriteString(writer, peer.HelloNonce);
                NetworkWriterExtensions.WriteString(
                    writer,
                    peer.HelloIdentityDigest);
            }
        });
    }

    private void BroadcastHostPvpControl(
        PvpAgreementMessageKind kind,
        string reason,
        ulong sceneGenerationEpoch = 0)
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
                SendPvpControl(
                    connection,
                    kind,
                    host.Identity,
                    reason,
                    sceneGenerationEpoch);
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
        ulong sceneGenerationEpoch = 0,
        int frozenSlotOverride = 0)
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
            sceneGenerationEpoch,
            frozenSlotOverride > 0
                ? frozenSlotOverride
                : remotePvpAgreement?.FrozenSlot ?? 0,
            0);
    }

    private void SendPvpControl(
        NetworkConnection connection,
        PvpAgreementMessageKind kind,
        PvpPeerIdentity identity,
        string reason,
        ulong sceneGenerationEpoch = 0,
        int senderSlot = int.MinValue,
        int recipientSlot = int.MinValue)
    {
        if (senderSlot == int.MinValue || recipientSlot == int.MinValue)
        {
            if (NetworkServer.active)
            {
                HostPvpAgreement host = hostPvpAgreement;
                if (host == null ||
                    !host.FrozenSlotByConnectionId.TryGetValue(
                        connection.connectionId,
                        out recipientSlot))
                {
                    throw new InvalidOperationException(
                        "host control has no frozen recipient slot");
                }
                senderSlot = 0;
            }
            else
            {
                senderSlot = remotePvpAgreement?.FrozenSlot ?? 0;
                recipientSlot = 0;
            }
        }
        if (senderSlot < 0 || recipientSlot < 0 || senderSlot == recipientSlot)
            throw new InvalidOperationException(
                "peer control sender/recipient slot is invalid");
        SendPvpEnvelope(connection, writer =>
        {
            NetworkWriterExtensions.WriteUShort(
                writer,
                PvpAgreementProtocolVersion);
            NetworkWriterExtensions.WriteByte(writer, (byte)kind);
            NetworkWriterExtensions.WriteInt(writer, senderSlot);
            NetworkWriterExtensions.WriteInt(writer, recipientSlot);
            NetworkWriterExtensions.WriteString(writer, identity.Nonce ?? string.Empty);
            NetworkWriterExtensions.WriteString(writer, identity.Digest ?? string.Empty);
            NetworkWriterExtensions.WriteULong(writer, sceneGenerationEpoch);
            reason ??= string.Empty;
            if (reason.Length > PvpAgreementMaxReasonString)
                throw new InvalidOperationException(
                    "peer control payload exceeded its character limit");
            NetworkWriterExtensions.WriteString(writer, reason);
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
        string nonce,
        int pveEnemyCount,
        int participantCount,
        string frozenRosterDigest)
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
            !IsPeerAgreementMode(operation.Mode) ||
            !string.Equals(operation.MapId, map.Id, StringComparison.Ordinal) ||
            !operation.SupportedTimeCodes.Contains(timeCode, StringComparer.Ordinal) ||
            participantCount < operation.MinimumPlayers ||
            participantCount > operation.MaximumPlayers ||
            !IsLowercasePvpSha256(frozenRosterDigest))
        {
            throw new InvalidOperationException(
                "local peer offer identity was incomplete or not owned by the map");
        }
        if (operation.Mode == ModdedOperationMode.PlayerVersusEnvironment &&
            !PveEnemyCountSelection.TryValidateConfirmedSelection(
                pveEnemyCount,
                operation.MinimumEnemies,
                operation.MaximumEnemies,
                operation.MaximumEnemies,
                out string countError))
        {
            throw new InvalidOperationException(
                "local PVE peer count identity is invalid: " + countError);
        }
        if (operation.Mode == ModdedOperationMode.PlayerVersusPlayer &&
            pveEnemyCount != 0)
        {
            throw new InvalidOperationException(
                "local PVP peer identity cannot carry a PVE enemy count");
        }
        var identity = new PvpPeerIdentity
        {
            Nonce = nonce,
            FrameworkVersion = PvpAgreementFrameworkVersion,
            ApiVersion = OperatorApi.ApiVersion,
            SuiteManifestSha256 = runtime.SuiteManifestSha256,
            GameBuildId = OperatorApi.Compatibility.DetectedGameBuildId,
            Capabilities = PvpAgreementCapabilities,
            FrozenRosterDigest = frozenRosterDigest,
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
            ParticipantCount = participantCount,
            MinimumEnemies = operation.Mode ==
                ModdedOperationMode.PlayerVersusEnvironment
                ? operation.MinimumEnemies
                : 0,
            MaximumEnemies = operation.Mode ==
                ModdedOperationMode.PlayerVersusEnvironment
                ? operation.MaximumEnemies
                : 0,
            RequestedEnemies = operation.Mode ==
                ModdedOperationMode.PlayerVersusEnvironment
                ? pveEnemyCount
                : 0,
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
        NetworkWriterExtensions.WriteString(writer, identity.ApiVersion);
        NetworkWriterExtensions.WriteString(writer, identity.SuiteManifestSha256);
        NetworkWriterExtensions.WriteString(writer, identity.GameBuildId);
        NetworkWriterExtensions.WriteString(writer, identity.Capabilities);
        NetworkWriterExtensions.WriteString(writer, identity.FrozenRosterDigest);
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
        NetworkWriterExtensions.WriteInt(writer, identity.ParticipantCount);
        NetworkWriterExtensions.WriteInt(writer, identity.MinimumEnemies);
        NetworkWriterExtensions.WriteInt(writer, identity.MaximumEnemies);
        NetworkWriterExtensions.WriteInt(writer, identity.RequestedEnemies);
        NetworkWriterExtensions.WriteString(writer, identity.CompanionPluginGuid);
        NetworkWriterExtensions.WriteString(writer, identity.CompanionPluginVersion);
        NetworkWriterExtensions.WriteString(writer, identity.CompanionSha256);
        NetworkWriterExtensions.WriteString(writer, identity.CompanionReadyMarkerName);
        NetworkWriterExtensions.WriteString(writer, identity.CompanionFailureMarkerName);
        NetworkWriterExtensions.WriteString(writer, identity.Digest);
    }

    private static List<PvpFrozenPeer> ReadPvpFrozenRoster(
        NetworkReader reader,
        int expectedCount)
    {
        int count = NetworkReaderExtensions.ReadInt(reader);
        if (count != expectedCount || count < 2 || count > 64)
            throw new InvalidOperationException(
                "Offer frozen roster count does not match participantCount");
        var result = new List<PvpFrozenPeer>(count);
        for (int index = 0; index < count; index++)
        {
            result.Add(new PvpFrozenPeer
            {
                Slot = NetworkReaderExtensions.ReadInt(reader),
                ConnectionId = NetworkReaderExtensions.ReadInt(reader),
                HelloNonce = ReadBoundedPvpString(
                    reader,
                    64,
                    "frozenHelloNonce"),
                HelloIdentityDigest = ReadPvpSha256(
                    reader,
                    "frozenHelloIdentityDigest")
            });
        }
        return result;
    }

    private void ValidateRemotePvpFrozenRoster(
        PvpPeerIdentity identity,
        int assignedSlot,
        IReadOnlyList<PvpFrozenPeer> roster)
    {
        if (identity == null || roster == null ||
            roster.Count != identity.ParticipantCount || assignedSlot <= 0 ||
            assignedSlot >= roster.Count ||
            string.IsNullOrEmpty(pvpClientHelloNonce) ||
            NetworkClient.connection == null ||
            !NetworkClient.connection.isAuthenticated)
        {
            throw new InvalidOperationException(
                "Offer frozen roster/recipient slot is incomplete");
        }
        var connectionIds = new HashSet<int>();
        var helloNonces = new HashSet<string>(StringComparer.Ordinal);
        string localHelloIdentity = ComputePvpHelloIdentityDigest(
            PvpAgreementFrameworkVersion,
            OperatorApi.ApiVersion,
            PvpAgreementCapabilities);
        int localNonceMatches = 0;
        var canonical = new StringBuilder("operator-frozen-peer-roster-v1\n");
        for (int index = 0; index < roster.Count; index++)
        {
            PvpFrozenPeer peer = roster[index];
            if (peer == null || peer.Slot != index ||
                string.IsNullOrEmpty(peer.HelloNonce) ||
                peer.HelloNonce.Length != 32 ||
                !IsLowercasePvpSha256(peer.HelloIdentityDigest) ||
                !string.Equals(
                    peer.HelloIdentityDigest,
                    localHelloIdentity,
                    StringComparison.Ordinal) ||
                !connectionIds.Add(peer.ConnectionId) ||
                !helloNonces.Add(peer.HelloNonce))
            {
                throw new InvalidOperationException(
                    "Offer frozen roster ordering/identity is invalid");
            }
            if (string.Equals(
                    peer.HelloNonce,
                    pvpClientHelloNonce,
                    StringComparison.Ordinal))
            {
                localNonceMatches++;
                if (peer.Slot != assignedSlot)
                {
                    throw new InvalidOperationException(
                        "Offer recipient slot does not own this ClientHello");
                }
            }
            canonical.Append(peer.Slot.ToString(CultureInfo.InvariantCulture))
                .Append('\n')
                .Append(peer.ConnectionId.ToString(CultureInfo.InvariantCulture))
                .Append('\n')
                .Append(peer.HelloNonce).Append('\n')
                .Append(peer.HelloIdentityDigest).Append('\n');
        }
        if (localNonceMatches != 1 ||
            !string.Equals(
                roster[0].HelloNonce,
                identity.Nonce,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Offer frozen roster does not bind one host and this client");
        }
        using SHA256 sha = SHA256.Create();
        string digest = ToLowerHex(sha.ComputeHash(
            Encoding.UTF8.GetBytes(canonical.ToString())));
        if (!string.Equals(
                digest,
                identity.FrozenRosterDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Offer frozen roster digest mismatch");
        }
    }

    private void CommitPvpNativeTransition(ActiveMapOperation operation)
    {
        if (operation?.PeerAgreementRequired != true)
            return;
        HostPvpAgreement host = hostPvpAgreement;
        string membershipError = string.Empty;
        if (!NetworkServer.active || host == null ||
            host.NativeTransitionCommitted ||
            host.Phase != HostPvpAgreementPhase.WaitingForScenes ||
            !OperationMatchesPvpIdentity(operation, host.Identity) ||
            !TryValidateHostPvpMembership(
                host,
                out membershipError,
                requireReady: true))
        {
            throw new InvalidOperationException(
                "peer native transition commit preflight failed: " +
                membershipError);
        }
        BroadcastHostPvpControl(
            PvpAgreementMessageKind.TransitionCommit,
            string.Empty,
            host.SceneGenerationEpoch);
        host.NativeTransitionCommitted = true;
        LogFrameworkEvidence(
            "pvp-native-transition-committed",
            host.Identity.OperationId,
            host.Identity.MapId,
            0,
            0,
            "role=host|identityDigest=" +
                FrameworkEvidence.Encode(host.Identity.Digest) +
            "|epoch=" + FrameworkEvidence.Number(
                host.SceneGenerationEpoch));
    }

    private static PvpPeerIdentity ReadPvpIdentity(NetworkReader reader)
    {
        var identity = new PvpPeerIdentity
        {
            Nonce = ReadBoundedPvpString(reader, 64, "nonce"),
            FrameworkVersion = ReadBoundedPvpString(reader, 64, "frameworkVersion"),
            ApiVersion = ReadBoundedPvpString(reader, 64, "apiVersion"),
            SuiteManifestSha256 = ReadPvpSha256(reader, "suiteManifestSha256"),
            GameBuildId = ReadBoundedPvpString(reader, 128, "gameBuildId"),
            Capabilities = ReadBoundedPvpString(reader, 256, "capabilities"),
            FrozenRosterDigest = ReadPvpSha256(reader, "frozenRosterDigest"),
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
            ParticipantCount = NetworkReaderExtensions.ReadInt(reader),
            MinimumEnemies = NetworkReaderExtensions.ReadInt(reader),
            MaximumEnemies = NetworkReaderExtensions.ReadInt(reader),
            RequestedEnemies = NetworkReaderExtensions.ReadInt(reader),
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
        ValidatePvpOperationIdentityBounds(identity);
        return identity;
    }

    private static void ValidatePvpOperationIdentityBounds(
        PvpPeerIdentity identity)
    {
        if (identity == null || identity.MinimumPlayers < 1 ||
            identity.MaximumPlayers < identity.MinimumPlayers ||
            identity.MaximumPlayers > 64 ||
            identity.ParticipantCount < identity.MinimumPlayers ||
            identity.ParticipantCount > identity.MaximumPlayers ||
            !IsPeerAgreementMode((ModdedOperationMode)identity.Mode))
        {
            throw new InvalidOperationException(
                "peer operation/player identity bounds are invalid");
        }
        if ((ModdedOperationMode)identity.Mode ==
            ModdedOperationMode.PlayerVersusEnvironment)
        {
            if (!PveEnemyCountSelection.TryValidateConfirmedSelection(
                    identity.RequestedEnemies,
                    identity.MinimumEnemies,
                    identity.MaximumEnemies,
                    identity.MaximumEnemies,
                    out string error))
            {
                throw new InvalidOperationException(
                    "PVE peer enemy identity is invalid: " + error);
            }
            return;
        }
        if (identity.MinimumEnemies != 0 || identity.MaximumEnemies != 0 ||
            identity.RequestedEnemies != 0)
        {
            throw new InvalidOperationException(
                "PVP peer identity carries nonzero PVE enemy fields");
        }
    }

    private void AcceptPvpClientHello(
        NetworkConnection connection,
        string helloNonce,
        string frameworkVersion,
        string apiVersion,
        string capabilities)
    {
        if (connection == null || !connection.isAuthenticated ||
            string.IsNullOrEmpty(helloNonce) || helloNonce.Length != 32 ||
            !string.Equals(
                frameworkVersion,
                PvpAgreementFrameworkVersion,
                StringComparison.Ordinal) ||
            !string.Equals(apiVersion, OperatorApi.ApiVersion,
                StringComparison.Ordinal) ||
            !string.Equals(capabilities, PvpAgreementCapabilities,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "client pre-offer handler identity is invalid or incompatible");
        }
        for (int index = 0; index < helloNonce.Length; index++)
        {
            char value = helloNonce[index];
            if (!((value >= '0' && value <= '9') ||
                  (value >= 'a' && value <= 'f')))
            {
                throw new InvalidOperationException(
                    "client pre-offer nonce is not lowercase hexadecimal");
            }
        }

        int connectionId = connection.connectionId;
        if (pvpClientHellos.TryGetValue(
                connectionId,
                out PvpClientHello existing) &&
            (!SamePvpNetworkConnection(existing.Connection, connection) ||
             !string.Equals(existing.Nonce, helloNonce,
                 StringComparison.Ordinal)))
        {
            HostPvpAgreement activeHost = hostPvpAgreement;
            if (activeHost != null &&
                activeHost.RequiredConnectionIds.Contains(connectionId))
            {
                FailHostPvpAgreement(
                    "frozen peer " + connectionId +
                    " replaced its pre-offer handler identity",
                    true);
                throw new InvalidOperationException(
                    "client pre-offer handler identity changed on a frozen connection");
            }
        }
        pvpClientHellos[connectionId] = new PvpClientHello
        {
            Nonce = helloNonce,
            FrameworkVersion = frameworkVersion,
            ApiVersion = apiVersion,
            Capabilities = capabilities,
            Connection = connection
        };

        HostPvpAgreement host = hostPvpAgreement;
        if (host == null ||
            !host.RequiredConnectionIds.Contains(connectionId) ||
            !host.RequiredConnections.TryGetValue(
                connectionId,
                out NetworkConnectionToClient required) ||
            !SamePvpNetworkConnection(required, connection))
        {
            return;
        }
        host.HelloConnectionIds.Add(connectionId);
        if (host.Phase == HostPvpAgreementPhase.WaitingForHello &&
            host.RequiredConnectionIds.SetEquals(host.HelloConnectionIds))
        {
            BeginHostPvpOfferBroadcast(host);
        }
    }

    private void TryLogPeerLoadingExit(ActiveMapOperation operation)
    {
        if (operation == null || !operation.ReadinessInitialized)
            return;
        GameManagerNetwork manager = GameManagerNetwork.instance;
        if (manager?.LoadingScreen == null ||
            manager.LoadingScreen.activeSelf ||
            manager.LoadingScreen.activeInHierarchy)
        {
            return;
        }
        HostPvpAgreement host = hostPvpAgreement;
        if (NetworkServer.active && host != null && !host.LoadingExitLogged &&
            operation.AllPlayersLoaded &&
            OperationMatchesPvpIdentity(operation, host.Identity))
        {
            host.LoadingExitLogged = true;
            LogPeerLoadingExit(
                host.Identity,
                operation,
                "host",
                host.SceneGenerationEpoch);
        }
        RemotePvpAgreement remote = remotePvpAgreement;
        if (!NetworkServer.active && remote != null &&
            !remote.LoadingExitLogged && remote.NativeOwnerAdopted &&
            remote.NativeReadinessInitialized &&
            OperationMatchesPvpIdentity(operation, remote.Identity))
        {
            remote.LoadingExitLogged = true;
            LogPeerLoadingExit(
                remote.Identity,
                operation,
                "remote",
                remote.RequestedSceneGenerationEpoch);
        }
    }

    private void LogPeerLoadingExit(
        PvpPeerIdentity identity,
        ActiveMapOperation operation,
        string role,
        ulong epoch)
    {
        LogFrameworkEvidence(
            identity.Mode == (int)ModdedOperationMode.PlayerVersusEnvironment
                ? "pve-peer-loading-exit"
                : "pvp-peer-loading-exit",
            identity.OperationId,
            identity.MapId,
            operation.SceneHandle,
            operation.EvidenceSceneGeneration,
            "role=" + FrameworkEvidence.Encode(role) +
            "|identityDigest=" + FrameworkEvidence.Encode(identity.Digest) +
            "|epoch=" + FrameworkEvidence.Number(epoch) +
            "|loadingActiveSelf=false|loadingActiveInHierarchy=false" +
            "|readinessInitialized=true");
    }

    private void TryLogPeerGroundedPlayers(ActiveMapOperation operation)
    {
        if (operation == null || operation.SceneHandle == 0)
            return;
        PvpPeerIdentity identity = null;
        string role = string.Empty;
        ulong epoch = 0;
        if (NetworkServer.active && hostPvpAgreement != null &&
            hostPvpAgreement.LoadingExitLogged &&
            !hostPvpAgreement.GroundedPlayersLogged &&
            OperationMatchesPvpIdentity(operation, hostPvpAgreement.Identity))
        {
            identity = hostPvpAgreement.Identity;
            role = "host";
            epoch = hostPvpAgreement.SceneGenerationEpoch;
        }
        else if (!NetworkServer.active && remotePvpAgreement != null &&
                 remotePvpAgreement.LoadingExitLogged &&
                 !remotePvpAgreement.GroundedPlayersLogged &&
                 OperationMatchesPvpIdentity(
                     operation,
                     remotePvpAgreement.Identity))
        {
            identity = remotePvpAgreement.Identity;
            role = "remote";
            epoch = remotePvpAgreement.RequestedSceneGenerationEpoch;
        }
        if (identity == null)
            return;

        Scene scene = FindLoadedSceneByHandle(operation.SceneHandle);
        if (!scene.IsValid() || !scene.isLoaded)
            return;
        List<UnityEngine.Transform> markers = FindStandalonePlayerMarkers(
            scene,
            operation.Operation.Mode);
        if (markers.Count == 0)
            return;
        PlayerMaster[] allPlayers;
        try { allPlayers = UnityEngine.Resources.FindObjectsOfTypeAll<PlayerMaster>(); }
        catch { return; }
        var netIds = new List<uint>();
        var groundDeltasMm = new List<int>();
        var markerAssignments = new List<string>();
        int grounded = 0;
        uint localPlayerId = 0;
        int localGroundDeltaMm = -1;
        foreach (PlayerMaster player in allPlayers)
        {
            if (player == null || player.gameObject == null ||
                !player.gameObject.scene.IsValid())
            {
                continue;
            }
            PlayerNetworking spawned;
            bool alive;
            try
            {
                spawned = player.PlayerSpawnedObject;
                alive = player.currentlySpawnedAndAlive;
            }
            catch { continue; }
            if (spawned == null || !alive || spawned.gameObject == null)
                continue;
            NetworkIdentity networkIdentity = spawned.GetComponent<NetworkIdentity>();
            if (networkIdentity == null || networkIdentity.netId == 0)
                continue;
            UnityEngine.Vector3 position = spawned.transform.position;
            UnityEngine.Transform authoritativeMarker = markers
                .OrderBy(marker => UnityEngine.Vector3.Distance(
                    marker.position,
                    position))
                .FirstOrDefault();
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) ||
                !float.IsFinite(position.z) ||
                authoritativeMarker == null ||
                UnityEngine.Vector3.Distance(
                    authoritativeMarker.position,
                    position) > 3f)
            {
                continue;
            }
            UnityEngine.Collider[] colliders = spawned
                .GetComponentsInChildren<UnityEngine.Collider>()
                .Where(collider => collider != null && !collider.isTrigger)
                .OrderByDescending(collider => collider.bounds.size.y)
                .ToArray();
            if (colliders.Length == 0)
                continue;
            float feetY = colliders[0].bounds.min.y;
            UnityEngine.Ray ray = new UnityEngine.Ray(
                new UnityEngine.Vector3(position.x, feetY + 0.2f, position.z),
                UnityEngine.Vector3.down);
            if (!UnityEngine.Physics.Raycast(
                    ray,
                    out UnityEngine.RaycastHit hit,
                    0.7f,
                    ~0,
                    UnityEngine.QueryTriggerInteraction.Ignore) ||
                hit.collider == null ||
                hit.collider.gameObject.scene.handle != operation.SceneHandle)
            {
                continue;
            }
            float groundDelta = feetY - hit.point.y;
            if (!float.IsFinite(groundDelta) ||
                groundDelta < -0.05f || groundDelta > 0.45f)
            {
                continue;
            }
            UnityEngine.Rigidbody body = spawned.GetComponent<UnityEngine.Rigidbody>();
            UnityEngine.CharacterController controller =
                spawned.GetComponentInChildren<UnityEngine.CharacterController>();
            float verticalVelocity = controller == null
                ? body == null ? 0f : body.linearVelocity.y
                : controller.velocity.y;
            if (!float.IsFinite(verticalVelocity) ||
                Math.Abs(verticalVelocity) > 0.5f)
                continue;
            int deltaMm = (int)Math.Round(groundDelta * 1000f);
            bool local;
            try { local = spawned.isLocalPlayer || spawned.isOwned; }
            catch { local = false; }
            if (local)
            {
                if (localPlayerId != 0 && localPlayerId != networkIdentity.netId)
                    return;
                localPlayerId = networkIdentity.netId;
                localGroundDeltaMm = deltaMm;
            }
            grounded++;
            netIds.Add(networkIdentity.netId);
            groundDeltasMm.Add(deltaMm);
            markerAssignments.Add(
                networkIdentity.netId.ToString(CultureInfo.InvariantCulture) +
                "=" + authoritativeMarker.name);
        }
        netIds.Sort();
        markerAssignments.Sort(StringComparer.Ordinal);
        if (grounded != identity.ParticipantCount ||
            netIds.Count != identity.ParticipantCount || localPlayerId == 0 ||
            localGroundDeltaMm < -50 || localGroundDeltaMm > 450)
        {
            return;
        }
        string netIdList = string.Join(",", netIds);
        using SHA256 sha = SHA256.Create();
        string populationDigest = ToLowerHex(sha.ComputeHash(
            Encoding.UTF8.GetBytes("operator-grounded-player-set-v1\n" +
                netIdList)));
        string assignmentList = string.Join(",", markerAssignments);
        using SHA256 assignmentSha = SHA256.Create();
        string assignmentDigest = ToLowerHex(assignmentSha.ComputeHash(
            Encoding.UTF8.GetBytes("operator-player-marker-assignment-v1\n" +
                assignmentList)));
        if (role == "host")
        {
            if (hostPvpAgreement.GroundedCandidateLastFrame ==
                    UnityEngine.Time.frameCount - 1 &&
                string.Equals(
                    hostPvpAgreement.GroundedCandidatePopulationDigest,
                    populationDigest,
                    StringComparison.Ordinal))
            {
                hostPvpAgreement.GroundedCandidateSamples++;
            }
            else
            {
                hostPvpAgreement.GroundedCandidateSamples = 1;
                hostPvpAgreement.GroundedCandidatePopulationDigest =
                    populationDigest;
            }
            hostPvpAgreement.GroundedCandidateLastFrame =
                UnityEngine.Time.frameCount;
            if (hostPvpAgreement.GroundedCandidateSamples < 30)
                return;
            hostPvpAgreement.GroundedPlayersLogged = true;
        }
        else
        {
            if (remotePvpAgreement.GroundedCandidateLastFrame ==
                    UnityEngine.Time.frameCount - 1 &&
                string.Equals(
                    remotePvpAgreement.GroundedCandidatePopulationDigest,
                    populationDigest,
                    StringComparison.Ordinal))
            {
                remotePvpAgreement.GroundedCandidateSamples++;
            }
            else
            {
                remotePvpAgreement.GroundedCandidateSamples = 1;
                remotePvpAgreement.GroundedCandidatePopulationDigest =
                    populationDigest;
            }
            remotePvpAgreement.GroundedCandidateLastFrame =
                UnityEngine.Time.frameCount;
            if (remotePvpAgreement.GroundedCandidateSamples < 30)
                return;
            remotePvpAgreement.GroundedPlayersLogged = true;
        }
        int maximumGroundDeltaMm = groundDeltasMm.Count == 0
            ? -1
            : groundDeltasMm.Max();
        LogFrameworkEvidence(
            identity.Mode == (int)ModdedOperationMode.PlayerVersusEnvironment
                ? "pve-peer-players-grounded"
                : "pvp-peer-players-grounded",
            identity.OperationId,
            identity.MapId,
            operation.SceneHandle,
            operation.EvidenceSceneGeneration,
            "role=" + FrameworkEvidence.Encode(role) +
            "|identityDigest=" + FrameworkEvidence.Encode(identity.Digest) +
            "|epoch=" + FrameworkEvidence.Number(epoch) +
            "|participantCount=" + FrameworkEvidence.Number(
                identity.ParticipantCount) +
            "|groundedCount=" + FrameworkEvidence.Number(grounded) +
            "|stableFrames=30|localPlayerId=" + FrameworkEvidence.Number(
                localPlayerId) +
            "|localGroundDeltaMm=" + FrameworkEvidence.Number(
                localGroundDeltaMm) +
            "|maximumGroundDeltaMm=" + FrameworkEvidence.Number(
                maximumGroundDeltaMm) +
            "|playerNetIds=" + FrameworkEvidence.Encode(netIdList) +
            "|markerAssignments=" + FrameworkEvidence.Encode(assignmentList) +
            "|assignmentDigest=" + FrameworkEvidence.Encode(assignmentDigest) +
            "|populationDigest=" + FrameworkEvidence.Encode(populationDigest));
    }

    private void NotifyPvePeerPopulationValidated(ActiveMapOperation operation)
    {
        HostPvpAgreement host = hostPvpAgreement;
        if (!NetworkServer.active || host == null || host.PvePopulationLogged ||
            operation?.Operation?.Mode !=
                ModdedOperationMode.PlayerVersusEnvironment ||
            !operation.PveTeamContractValidated ||
            !OperationMatchesPvpIdentity(operation, host.Identity))
        {
            return;
        }
        List<uint> netIds = operation.PveOwnedServerIdentities.Keys
            .Where(netId => netId != 0)
            .OrderBy(netId => netId)
            .ToList();
        if (netIds.Count != host.Identity.RequestedEnemies)
            return;
        try
        {
            host.PvePopulationLogged = true;
            LogPvePeerPopulation(
                host.Identity,
                operation,
                "host",
                host.SceneGenerationEpoch,
                netIds,
                serverSpawnAiCalled: true);
            if (operation.PeerAgreementRequired && host.PlayerBarrierPassed)
                PublishAuthoritativePvePopulationManifest(operation, host);
        }
        catch (Exception ex)
        {
            FailHostPvpAgreement(
                "authoritative PVE population publication failed closed: " +
                ex.GetType().Name + ": " + ex.Message,
                true);
        }
    }

    private void TryLogRemotePvePopulation(ActiveMapOperation operation)
    {
        RemotePvpAgreement remote = remotePvpAgreement;
        if (NetworkServer.active || remote == null || remote.PvePopulationLogged ||
            !remote.LoadingExitLogged || operation?.Operation?.Mode !=
                ModdedOperationMode.PlayerVersusEnvironment ||
            !OperationMatchesPvpIdentity(operation, remote.Identity))
        {
            return;
        }
        if (!NetworkClient.active || NetworkClient.spawned == null)
            return;
        var netIds = new List<uint>();
        var enumerator = NetworkClient.spawned.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                var entry = enumerator.Current;
                uint netId = entry.Key;
                NetworkIdentity identity = entry.Value;
                UnityEngine.GameObject root =
                    identity == null ? null : identity.gameObject;
                BrainAI[] brains = root == null
                    ? Array.Empty<BrainAI>()
                    : root.GetComponents<BrainAI>();
                if (brains.Length == 0)
                    continue;
                if (brains.Length != 1 || netId == 0 || identity == null ||
                    identity.netId != netId || identity.assetId == 0 ||
                    !identity.isClient || identity.transform.root != identity.transform ||
                    brains[0] == null || brains[0].transform != identity.transform)
                {
                    return;
                }
                netIds.Add(netId);
            }
        }
        finally
        {
            enumerator.Dispose();
        }
        netIds.Sort();
        if (netIds.Count != remote.Identity.RequestedEnemies ||
            netIds.Distinct().Count() != netIds.Count)
        {
            return;
        }
        remote.PvePopulationLogged = true;
        LogPvePeerPopulation(
            remote.Identity,
            operation,
            "remote",
            remote.RequestedSceneGenerationEpoch,
            netIds,
            serverSpawnAiCalled: false);
    }

    private void LogPvePeerPopulation(
        PvpPeerIdentity identity,
        ActiveMapOperation operation,
        string role,
        ulong epoch,
        IReadOnlyList<uint> netIds,
        bool serverSpawnAiCalled)
    {
        string netIdList = string.Join(",", netIds);
        using SHA256 sha = SHA256.Create();
        string populationDigest = ToLowerHex(sha.ComputeHash(
            Encoding.UTF8.GetBytes("operator-pve-network-ai-set-v1\n" +
                netIdList)));
        LogPvePeerAgreementEvidence(
            "pve-peer-ai-population",
            identity,
            operation,
            role,
            epoch,
            "activeCount=" + FrameworkEvidence.Number(netIds.Count) +
            "|aiNetIds=" + FrameworkEvidence.Encode(netIdList) +
            "|populationDigest=" + FrameworkEvidence.Encode(populationDigest) +
            "|hostAuthoritative=true|serverSpawnAiCalled=" +
                serverSpawnAiCalled.ToString().ToLowerInvariant());
    }

    private void TryLogPvePeerCompletion(ActiveMapOperation operation)
    {
        if (operation?.Operation?.Mode !=
                ModdedOperationMode.PlayerVersusEnvironment ||
            GameManagerNetwork.instance?.SuccessfulOperation != true)
        {
            return;
        }
        HostPvpAgreement host = hostPvpAgreement;
        if (NetworkServer.active && host != null &&
            !host.PveCompletionLogged &&
            OperationMatchesPvpIdentity(operation, host.Identity))
        {
            host.PveCompletionLogged = true;
            LogPvePeerAgreementEvidence(
                "pve-peer-completion",
                host.Identity,
                operation,
                "host",
                host.SceneGenerationEpoch,
                "successfulOperation=true|completionOwner=native");
        }
        RemotePvpAgreement remote = remotePvpAgreement;
        if (!NetworkServer.active && remote != null &&
            !remote.PveCompletionLogged &&
            OperationMatchesPvpIdentity(operation, remote.Identity))
        {
            remote.PveCompletionLogged = true;
            LogPvePeerAgreementEvidence(
                "pve-peer-completion",
                remote.Identity,
                operation,
                "remote",
                remote.RequestedSceneGenerationEpoch,
                "successfulOperation=true|completionOwner=native-syncvar");
        }
    }

    private static string ComputePvpIdentityDigest(PvpPeerIdentity identity)
    {
        string canonical = string.Join("\n", new[]
        {
            PvpAgreementProtocolVersion.ToString(CultureInfo.InvariantCulture),
            identity.Nonce ?? string.Empty,
            identity.FrameworkVersion ?? string.Empty,
            identity.ApiVersion ?? string.Empty,
            identity.SuiteManifestSha256 ?? string.Empty,
            identity.GameBuildId ?? string.Empty,
            identity.Capabilities ?? string.Empty,
            identity.FrozenRosterDigest ?? string.Empty,
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
            identity.ParticipantCount.ToString(CultureInfo.InvariantCulture),
            identity.MinimumEnemies.ToString(CultureInfo.InvariantCulture),
            identity.MaximumEnemies.ToString(CultureInfo.InvariantCulture),
            identity.RequestedEnemies.ToString(CultureInfo.InvariantCulture),
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
            bool melonHost = string.Equals(
                OperatorApi.LoaderKind,
                "melonloader",
                StringComparison.Ordinal);
            if (!melonHost && !string.Equals(
                    OperatorApi.LoaderKind,
                    "bepinex",
                    StringComparison.Ordinal))
            {
                error = "active Core loader identity is unavailable";
                return false;
            }

            if (!TryResolveVerifiedSuiteReceipt(
                    melonHost,
                    out string suiteManifestSha256,
                    out IReadOnlyDictionary<string, SuiteReceiptFile> receiptFiles,
                    out error))
            {
                return false;
            }

            var resolved = new RuntimeBinaryIdentity
            {
                SuiteManifestSha256 = suiteManifestSha256,
                CompanionPluginGuid = PvpNoCompanionIdentity,
                CompanionPluginVersion = PvpNoCompanionIdentity,
                CompanionSha256 = PvpNoCompanionIdentity,
                CompanionReadyMarkerName = PvpNoCompanionIdentity,
                CompanionFailureMarkerName = PvpNoCompanionIdentity
            };
            ModdedRuntimeCompanionDefinition companion = map?.RuntimeCompanion;
            if (companion != null)
            {
                string expectedLocalSha = melonHost
                    ? companion.MelonLoaderSha256
                    : companion.Sha256;
                if (!IsLowercasePvpSha256(expectedLocalSha))
                {
                    error = "runtime companion has no exact local " +
                        (melonHost ? "MelonLoader" : "BepInEx") + " DLL identity";
                    return false;
                }
                if (!TryComputeRuntimePairContentId(
                        companion.PluginGuid,
                        companion.PluginVersion,
                        companion.Sha256,
                        companion.MelonLoaderSha256,
                        out string runtimePairContentId,
                        out error))
                {
                    return false;
                }
                if (!string.Equals(
                        companion.RuntimeContentId,
                        runtimePairContentId,
                        StringComparison.Ordinal))
                {
                    error = "runtime companion loader-neutral pair identity " +
                        "does not match its declared BepInEx/MelonLoader DLL hashes";
                    return false;
                }
                if (!TryResolveLoadedPlugin(
                        companion.PluginGuid,
                        companion.PluginVersion,
                        expectedTypeName: null,
                        expectedAssemblyName: null,
                        expectedLocalSha,
                        out string loadedCompanionSha256,
                        out string loadedCompanionPath,
                        out error))
                {
                    return false;
                }
                string companionPath = Path.GetFullPath(loadedCompanionPath);
                if (!receiptFiles.TryGetValue(
                        companionPath,
                        out SuiteReceiptFile ownedCompanion) ||
                    !string.Equals(
                        loadedCompanionSha256,
                        ownedCompanion.Sha256,
                        StringComparison.Ordinal))
                {
                    error = "loaded runtime companion is not owned by the " +
                        "selected suite receipt at its exact loaded path";
                    return false;
                }
                resolved.CompanionPluginGuid = companion.PluginGuid;
                resolved.CompanionPluginVersion = companion.PluginVersion;
                resolved.CompanionSha256 = runtimePairContentId;
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
                                             BadImageFormatException or
                                             JsonException)
        {
            error = exception.GetType().Name + ": " + exception.Message;
            return false;
        }
    }

    private static bool TryResolveVerifiedSuiteReceipt(
        bool melonHost,
        out string manifestSha256,
        out IReadOnlyDictionary<string, SuiteReceiptFile> verifiedFiles,
        out string error)
    {
        manifestSha256 = string.Empty;
        verifiedFiles = null;
        error = string.Empty;
        string dataPath = UnityEngine.Application.dataPath;
        if (string.IsNullOrWhiteSpace(dataPath) ||
            !Path.IsPathFullyQualified(dataPath))
        {
            error = "Unity game data path is unavailable";
            return false;
        }
        DirectoryInfo parent = Directory.GetParent(Path.GetFullPath(dataPath));
        if (parent == null)
        {
            error = "Unity game root could not be derived";
            return false;
        }
        string gameRoot = parent.FullName;
        string loaderName = melonHost ? "MelonLoader" : "BepInEx";
        string loaderRootRelative = melonHost ? "Mods" : "BepInEx/plugins";
        string receiptPath = Path.Combine(
            gameRoot,
            loaderRootRelative.Replace('/', Path.DirectorySeparatorChar),
            SuiteInstallReceiptName);
        if (!TryReadStrictJsonFile(
                receiptPath,
                SuiteInstallReceiptMaxBytes,
                out JsonDocument receiptDocument,
                out error))
        {
            error = "suite install receipt is unavailable or malformed: " + error;
            return false;
        }

        using (receiptDocument)
        {
            JsonElement root = receiptDocument.RootElement;
            if (!HasExactJsonProperties(
                    root,
                    "files",
                    "kind",
                    "loader",
                    "manifestSha256",
                    "schemaVersion",
                    "suiteVersion") ||
                !TryReadExactInt(root, "schemaVersion", out int schemaVersion) ||
                schemaVersion != 1 ||
                !TryReadBoundedJsonString(root, "kind", 64, out string kind) ||
                !string.Equals(kind, "operator-mod-suite-install",
                    StringComparison.Ordinal) ||
                !TryReadBoundedJsonString(root, "loader", 32,
                    out string receiptLoader) ||
                !string.Equals(receiptLoader, loaderName,
                    StringComparison.Ordinal) ||
                !TryReadBoundedJsonString(root, "suiteVersion", 128,
                    out string suiteVersion) ||
                !TryReadBoundedJsonString(root, "manifestSha256", 64,
                    out manifestSha256) ||
                !IsLowercasePvpSha256(manifestSha256) ||
                !root.TryGetProperty("files", out JsonElement filesElement) ||
                filesElement.ValueKind != JsonValueKind.Array ||
                filesElement.GetArrayLength() < 2 ||
                filesElement.GetArrayLength() > SuiteInstallReceiptMaxFiles)
            {
                error = "suite install receipt schema or loader identity is invalid";
                return false;
            }

            var receiptFiles = new Dictionary<string, SuiteReceiptFile>(
                StringComparer.OrdinalIgnoreCase);
            foreach (JsonElement fileElement in filesElement.EnumerateArray())
            {
                if (!HasExactJsonProperties(
                        fileElement,
                        "destinationRelativePath",
                        "length",
                        "sha256") ||
                    !TryReadBoundedJsonString(
                        fileElement,
                        "destinationRelativePath",
                        1024,
                        out string relativePath) ||
                    !fileElement.TryGetProperty("length", out JsonElement lengthElement) ||
                    !lengthElement.TryGetInt64(out long expectedLength) ||
                    expectedLength <= 0 || expectedLength > 512L * 1024 * 1024 ||
                    !TryReadBoundedJsonString(
                        fileElement,
                        "sha256",
                        64,
                        out string expectedSha256) ||
                    !IsLowercasePvpSha256(expectedSha256) ||
                    !TryResolveContainedSuitePath(
                        gameRoot,
                        relativePath,
                        out string fullPath,
                        out error))
                {
                    error = "suite install receipt file entry is invalid: " + error;
                    return false;
                }
                if (!receiptFiles.TryAdd(
                        fullPath,
                        new SuiteReceiptFile
                        {
                            FullPath = fullPath,
                            Length = expectedLength,
                            Sha256 = expectedSha256
                        }))
                {
                    error = "suite install receipt contains a duplicate destination";
                    return false;
                }
            }

            foreach (SuiteReceiptFile file in receiptFiles.Values)
            {
                if (!TryValidateReceiptOwnedFileMetadata(gameRoot, file, out error))
                {
                    error = "suite-owned file metadata failed closed: " + error;
                    return false;
                }
            }

            string sidecarRelative = loaderRootRelative + "/" +
                SuiteManifestSidecarName;
            if (!TryResolveContainedSuitePath(
                    gameRoot,
                    sidecarRelative,
                    out string sidecarPath,
                    out error) ||
                !receiptFiles.TryGetValue(sidecarPath, out SuiteReceiptFile sidecar) ||
                !string.Equals(sidecar.Sha256, manifestSha256,
                    StringComparison.Ordinal))
            {
                error = "suite manifest sidecar is not the exact receipt-owned " +
                    "manifestSha256 file";
                return false;
            }
            if (!TryVerifyReceiptOwnedFile(gameRoot, sidecar, out error))
            {
                error = "suite manifest sidecar byte identity failed closed: " + error;
                return false;
            }
            if (!TryValidateSuiteManifestSidecar(
                    sidecar,
                    loaderName,
                    suiteVersion,
                    gameRoot,
                    receiptFiles,
                    out error))
            {
                return false;
            }
            if (!TryRequireReceiptLoadedAssembly(
                    receiptFiles,
                    typeof(CerberusNativeTabFix).Assembly,
                    expectedAssemblyName: null,
                    authoritativePath: null,
                    "Modded Operations framework",
                    out error) ||
                !TryRequireReceiptLoadedAssembly(
                    receiptFiles,
                    typeof(OperatorApi).Assembly,
                    "OperatorModAPI",
                    authoritativePath: null,
                    "Operator Mod API Core",
                    out error))
            {
                return false;
            }
            if (!TryResolveApiHostAssembly(
                    melonHost,
                    out Assembly apiHostAssembly,
                    out string apiHostPath,
                    out error) ||
                !TryRequireReceiptLoadedAssembly(
                    receiptFiles,
                    apiHostAssembly,
                    melonHost
                        ? "OperatorModAPI.MelonLoader"
                        : "OperatorModAPI.BepInEx",
                    apiHostPath,
                    "Operator Mod API selected loader host",
                    out error))
            {
                return false;
            }
            verifiedFiles = receiptFiles;
        }
        return true;
    }

    private static bool TryObserveExactNativeOperationRoomReturn(
        ActiveMapOperation operation,
        out string operationRoomPath,
        out string error)
    {
        operationRoomPath = string.Empty;
        error = string.Empty;
        if (operation == null || operation.SceneHandle != 0 ||
            operation.SceneSelection == null ||
            operation.PeerAgreementLastResetTargetGeneration >
                operation.EvidenceSceneGeneration)
        {
            error = "package scene/restart ownership is not quiescent";
            return false;
        }
        if (!TryResolveExactNativeOperationRoomBuildPath(
                out operationRoomPath,
                out error))
        {
            return false;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded ||
            !string.Equals(activeScene.name, "Operation Room", StringComparison.Ordinal) ||
            !string.Equals(activeScene.path, operationRoomPath, StringComparison.Ordinal))
        {
            error = "the exact native Operation Room is not the loaded active scene";
            return false;
        }
        string networkSceneName = NetworkManager.networkSceneName ?? string.Empty;
        if (!string.Equals(networkSceneName, "Operation Room", StringComparison.Ordinal) &&
            !string.Equals(networkSceneName, operationRoomPath, StringComparison.Ordinal))
        {
            error = "Mirror has not committed the exact Operation Room return";
            return false;
        }

        int operationRoomCount = 0;
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene loaded = SceneManager.GetSceneAt(index);
            if (!loaded.IsValid() || !loaded.isLoaded)
                continue;
            if (string.Equals(
                    loaded.path,
                    operation.SceneSelection.ScenePath,
                    StringComparison.Ordinal))
            {
                error = "the package scene is still loaded during native return";
                return false;
            }
            if (string.Equals(loaded.name, "Operation Room", StringComparison.Ordinal))
            {
                if (!string.Equals(
                        loaded.path,
                        operationRoomPath,
                        StringComparison.Ordinal))
                {
                    error = "a foreign same-named Operation Room scene is loaded";
                    return false;
                }
                operationRoomCount++;
            }
        }
        if (operationRoomCount != 1)
        {
            error = "the exact native Operation Room loaded-scene count is " +
                operationRoomCount.ToString(CultureInfo.InvariantCulture);
            return false;
        }

        uint ownerNetId = operation.BootstrapSpawnedNetId;
        if (ownerNetId != 0 && IsPeerGameModeOwnerStillSpawned(ownerNetId))
        {
            error = "the generation GameMode owner is still in a Mirror spawned registry";
            return false;
        }
        if (operation.BootstrapIdentity != null &&
            operation.BootstrapIdentity.netId != 0)
        {
            error = "the generation GameMode identity is still live";
            return false;
        }
        return true;
    }

    private static bool TryResolveExactNativeOperationRoomBuildPath(
        out string operationRoomPath,
        out string error)
    {
        operationRoomPath = string.Empty;
        error = string.Empty;
        int matches = 0;
        int buildSceneCount = SceneManager.sceneCountInBuildSettings;
        for (int index = 0; index < buildSceneCount; index++)
        {
            string candidate = SceneUtility.GetScenePathByBuildIndex(index);
            if (string.IsNullOrEmpty(candidate) ||
                !string.Equals(
                    Path.GetFileNameWithoutExtension(candidate),
                    "Operation Room",
                    StringComparison.Ordinal))
            {
                continue;
            }
            operationRoomPath = candidate;
            matches++;
        }
        if (matches != 1 || string.IsNullOrEmpty(operationRoomPath))
        {
            error = "native build settings do not contain exactly one Operation Room";
            operationRoomPath = string.Empty;
            return false;
        }
        return true;
    }

    private static bool IsPeerGameModeOwnerStillSpawned(uint ownerNetId)
    {
        if (ownerNetId == 0)
            return false;
        try
        {
            if (NetworkServer.active && NetworkServer.spawned != null &&
                NetworkServer.spawned.TryGetValue(
                    ownerNetId,
                    out NetworkIdentity serverIdentity) &&
                serverIdentity != null)
            {
                return true;
            }
        }
        catch { return true; }
        try
        {
            if (NetworkClient.active && NetworkClient.spawned != null &&
                NetworkClient.spawned.TryGetValue(
                    ownerNetId,
                    out NetworkIdentity clientIdentity) &&
                clientIdentity != null)
            {
                return true;
            }
        }
        catch { return true; }
        return false;
    }

    private static bool TryValidateCurrentPvePeerSpawnContract(
        ActiveMapOperation operation,
        HostPvpAgreement host,
        int agreedPlayerCount,
        out string error)
    {
        error = string.Empty;
        GameManagerNetwork network = GameManagerNetwork.instance;
        if (operation?.GameModeComponent is not StandalonePveGameMode ||
            network?.playerMasters == null || host?.Identity == null)
        {
            error = "native PVE owner/player registries are unavailable before spawn";
            return false;
        }
        if (network.playerMasters.Count != agreedPlayerCount)
        {
            error = "native PVE player registry count " +
                network.playerMasters.Count + " does not match agreed lobby count " +
                agreedPlayerCount;
            return false;
        }
        if (!TryValidateAgreedPveSceneCapacity(
                operation,
                host.Identity,
                out int safeCapacity,
                out error) || safeCapacity != host.SafePveCapacity)
        {
            if (string.IsNullOrEmpty(error))
                error = "PVE safe enemy capacity changed after SceneReady";
            return false;
        }
        return true;
    }

    private static bool TryValidateSuiteManifestSidecar(
        SuiteReceiptFile sidecar,
        string loaderName,
        string receiptSuiteVersion,
        string gameRoot,
        IReadOnlyDictionary<string, SuiteReceiptFile> receiptFiles,
        out string error)
    {
        error = string.Empty;
        if (!TryReadStrictJsonFile(
                sidecar.FullPath,
                SuiteInstallReceiptMaxBytes,
                out JsonDocument manifestDocument,
                out error))
        {
            error = "suite manifest sidecar is malformed: " + error;
            return false;
        }
        using (manifestDocument)
        {
            JsonElement root = manifestDocument.RootElement;
            if (!HasExactJsonProperties(
                    root,
                    "components",
                    "gameArtifacts",
                    "gameBuildId",
                    "loaderDependencies",
                    "schemaVersion",
                    "sharedPayloads",
                    "suiteVersion") ||
                !TryReadExactInt(root, "schemaVersion", out int schemaVersion) ||
                schemaVersion != 1 ||
                !TryReadBoundedJsonString(root, "suiteVersion", 128,
                    out string suiteVersion) ||
                !string.Equals(suiteVersion, receiptSuiteVersion,
                    StringComparison.Ordinal) ||
                !TryReadBoundedJsonString(root, "gameBuildId", 64,
                    out string gameBuildId) ||
                !IsLowercasePvpSha256(gameBuildId) ||
                !string.Equals(
                    gameBuildId,
                    OperatorApi.Compatibility.DetectedGameBuildId,
                    StringComparison.Ordinal))
            {
                error = "suite manifest top-level identity is invalid";
                return false;
            }
            if (!TryValidateManifestGameArtifacts(root, out error) ||
                !TryValidateManifestLoaderDependencies(root, out error))
            {
                return false;
            }

            var selectedRecords = new Dictionary<string, SuiteReceiptFile>(
                StringComparer.OrdinalIgnoreCase);
            var componentIds = new HashSet<string>(StringComparer.Ordinal);
            if (!TryCollectSelectedManifestRecords(
                    root,
                    "components",
                    requireVersion: true,
                    loaderName,
                    gameRoot,
                    componentIds,
                    selectedRecords,
                    out error) ||
                !TryCollectSelectedManifestRecords(
                    root,
                    "sharedPayloads",
                    requireVersion: false,
                    loaderName,
                    gameRoot,
                    componentIds,
                    selectedRecords,
                    out error))
            {
                return false;
            }
            if (selectedRecords.Count == 0 ||
                receiptFiles.Count != selectedRecords.Count + 1)
            {
                error = "receipt files do not equal selected suite records plus " +
                    "the manifest sidecar";
                return false;
            }
            foreach (var entry in selectedRecords)
            {
                if (!receiptFiles.TryGetValue(entry.Key, out SuiteReceiptFile owned) ||
                    owned.Length != entry.Value.Length ||
                    !string.Equals(owned.Sha256, entry.Value.Sha256,
                        StringComparison.Ordinal))
                {
                    error = "receipt file does not match its selected suite manifest " +
                        "record: " + entry.Value.FullPath;
                    return false;
                }
            }
            if (!receiptFiles.TryGetValue(sidecar.FullPath, out SuiteReceiptFile ownedSidecar) ||
                !ReferenceEquals(ownedSidecar, sidecar))
            {
                error = "receipt-owned manifest sidecar entry is ambiguous";
                return false;
            }
        }
        return true;
    }

    private static bool TryCollectSelectedManifestRecords(
        JsonElement root,
        string propertyName,
        bool requireVersion,
        string loaderName,
        string gameRoot,
        ISet<string> ids,
        IDictionary<string, SuiteReceiptFile> selectedRecords,
        out string error)
    {
        error = string.Empty;
        if (!root.TryGetProperty(propertyName, out JsonElement records) ||
            records.ValueKind != JsonValueKind.Array ||
            records.GetArrayLength() > SuiteInstallReceiptMaxFiles)
        {
            error = "suite manifest " + propertyName + " array is invalid";
            return false;
        }
        foreach (JsonElement record in records.EnumerateArray())
        {
            bool exact = requireVersion
                ? HasExactJsonProperties(
                    record, "BepInEx", "MelonLoader", "id", "version")
                : HasExactJsonProperties(
                    record, "BepInEx", "MelonLoader", "id");
            if (!exact ||
                !TryReadBoundedJsonString(record, "id", 128, out string id) ||
                !ids.Add(id) ||
                (requireVersion &&
                 !TryReadBoundedJsonString(record, "version", 64, out _)))
            {
                error = "suite manifest " + propertyName +
                    " identity is invalid or duplicated";
                return false;
            }
            if (!TryReadManifestLoaderRecord(
                    record,
                    "BepInEx",
                    gameRoot,
                    out SuiteReceiptFile bepRecord,
                    out error) ||
                !TryReadManifestLoaderRecord(
                    record,
                    "MelonLoader",
                    gameRoot,
                    out SuiteReceiptFile melonRecord,
                    out error))
            {
                return false;
            }
            SuiteReceiptFile selected = string.Equals(
                loaderName, "MelonLoader", StringComparison.Ordinal)
                ? melonRecord
                : bepRecord;
            if (!selectedRecords.TryAdd(selected.FullPath, selected))
            {
                error = "suite manifest selected destination is duplicated";
                return false;
            }
        }
        return true;
    }

    private static bool TryReadManifestLoaderRecord(
        JsonElement owner,
        string loaderName,
        string gameRoot,
        out SuiteReceiptFile record,
        out string error)
    {
        record = null;
        error = string.Empty;
        if (!owner.TryGetProperty(loaderName, out JsonElement value) ||
            !HasExactJsonProperties(
                value,
                "destinationRelativePath",
                "length",
                "sha256",
                "sourceRelativePath",
                "sourceRoot") ||
            !TryReadBoundedJsonString(
                value, "sourceRoot", 64, out _) ||
            !TryReadBoundedJsonString(
                value, "sourceRelativePath", 1024, out string sourcePath) ||
            !IsSafeSuiteRelativePath(sourcePath) ||
            !TryReadBoundedJsonString(
                value, "destinationRelativePath", 1024,
                out string destinationPath) ||
            !value.TryGetProperty("length", out JsonElement lengthElement) ||
            !lengthElement.TryGetInt64(out long length) ||
            length <= 0 || length > 512L * 1024 * 1024 ||
            !TryReadBoundedJsonString(value, "sha256", 64, out string sha256) ||
            !IsLowercasePvpSha256(sha256) ||
            !TryResolveContainedSuitePath(
                gameRoot, destinationPath, out string fullPath, out error))
        {
            error = "suite manifest " + loaderName +
                " runtime record is invalid: " + error;
            return false;
        }
        record = new SuiteReceiptFile
        {
            FullPath = fullPath,
            Length = length,
            Sha256 = sha256
        };
        return true;
    }

    private static bool TryValidateManifestGameArtifacts(
        JsonElement root,
        out string error)
    {
        error = string.Empty;
        if (!root.TryGetProperty("gameArtifacts", out JsonElement artifacts) ||
            artifacts.ValueKind != JsonValueKind.Array ||
            artifacts.GetArrayLength() == 0 ||
            artifacts.GetArrayLength() > SuiteInstallReceiptMaxFiles)
        {
            error = "suite manifest gameArtifacts array is invalid";
            return false;
        }
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement artifact in artifacts.EnumerateArray())
        {
            if (!HasExactJsonProperties(
                    artifact, "length", "relativePath", "sha256") ||
                !TryReadBoundedJsonString(
                    artifact, "relativePath", 1024, out string path) ||
                !IsSafeSuiteRelativePath(path) || !paths.Add(path) ||
                !artifact.TryGetProperty("length", out JsonElement lengthElement) ||
                !lengthElement.TryGetInt64(out long length) || length <= 0 ||
                length > 512L * 1024 * 1024 ||
                !TryReadBoundedJsonString(
                    artifact, "sha256", 64, out string sha256) ||
                !IsLowercasePvpSha256(sha256))
            {
                error = "suite manifest game artifact record is invalid";
                return false;
            }
        }
        return true;
    }

    private static bool TryValidateManifestLoaderDependencies(
        JsonElement root,
        out string error)
    {
        error = string.Empty;
        if (!root.TryGetProperty(
                "loaderDependencies", out JsonElement dependencies) ||
            !HasExactJsonProperties(dependencies, "BepInEx", "MelonLoader"))
        {
            error = "suite manifest loaderDependencies object is invalid";
            return false;
        }
        foreach (string loaderName in new[] { "BepInEx", "MelonLoader" })
        {
            JsonElement entries = dependencies.GetProperty(loaderName);
            if (entries.ValueKind != JsonValueKind.Array ||
                entries.GetArrayLength() == 0 ||
                entries.GetArrayLength() > SuiteInstallReceiptMaxFiles)
            {
                error = "suite manifest " + loaderName +
                    " dependency array is invalid";
                return false;
            }
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonElement entry in entries.EnumerateArray())
            {
                if (!HasExactJsonProperties(
                        entry, "length", "relativePath", "sha256", "version") ||
                    !TryReadBoundedJsonString(
                        entry, "relativePath", 1024, out string path) ||
                    !IsSafeSuiteRelativePath(path) || !paths.Add(path) ||
                    !entry.TryGetProperty("length", out JsonElement lengthElement) ||
                    !lengthElement.TryGetInt64(out long length) || length <= 0 ||
                    length > 512L * 1024 * 1024 ||
                    !TryReadBoundedJsonString(
                        entry, "sha256", 64, out string sha256) ||
                    !IsLowercasePvpSha256(sha256) ||
                    !TryReadBoundedJsonString(entry, "version", 64, out _))
                {
                    error = "suite manifest " + loaderName +
                        " dependency record is invalid";
                    return false;
                }
            }
        }
        return true;
    }

    private static bool TryReadStrictJsonFile(
        string path,
        int maximumBytes,
        out JsonDocument document,
        out string error)
    {
        document = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            error = "required JSON file does not exist";
            return false;
        }
        FileInfo info = new FileInfo(path);
        if (info.Length <= 0 || info.Length > maximumBytes ||
            (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            error = "required JSON file length or path ownership is invalid";
            return false;
        }
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length != info.Length ||
            (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB &&
             bytes[2] == 0xBF))
        {
            error = "required JSON file encoding or stable length is invalid";
            return false;
        }
        string json = new UTF8Encoding(false, true).GetString(bytes);
        document = JsonDocument.Parse(
            json,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 24
            });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            document = null;
            error = "required JSON root is not an object";
            return false;
        }
        return true;
    }

    private static bool HasExactJsonProperties(
        JsonElement element,
        params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return false;
        var remaining = new HashSet<string>(expected, StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!remaining.Remove(property.Name))
                return false;
        }
        return remaining.Count == 0;
    }

    private static bool TryReadExactInt(
        JsonElement owner,
        string propertyName,
        out int value)
    {
        value = 0;
        return owner.TryGetProperty(propertyName, out JsonElement element) &&
            element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }

    private static bool TryReadBoundedJsonString(
        JsonElement owner,
        string propertyName,
        int maximumLength,
        out string value)
    {
        value = string.Empty;
        if (!owner.TryGetProperty(propertyName, out JsonElement element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        value = element.GetString() ?? string.Empty;
        return value.Length > 0 && value.Length <= maximumLength &&
            string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
            value.IndexOfAny(new[] { '\0', '\r', '\n' }) < 0;
    }

    private static bool TryResolveContainedSuitePath(
        string gameRoot,
        string relativePath,
        out string fullPath,
        out string error)
    {
        fullPath = string.Empty;
        error = string.Empty;
        if (!IsSafeSuiteRelativePath(relativePath))
        {
            error = "suite path is not a canonical relative path";
            return false;
        }
        string root = Path.GetFullPath(gameRoot);
        string rootPrefix = root.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        fullPath = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            fullPath = string.Empty;
            error = "suite path escaped the game root";
            return false;
        }
        return true;
    }

    private static bool IsSafeSuiteRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            relativePath.Length > 1024 ||
            !string.Equals(relativePath, relativePath.Trim(), StringComparison.Ordinal) ||
            relativePath.IndexOfAny(new[] { '\0', '\r', '\n', '\\', ':' }) >= 0 ||
            relativePath.StartsWith("/", StringComparison.Ordinal) ||
            relativePath.EndsWith("/", StringComparison.Ordinal) ||
            Path.IsPathFullyQualified(relativePath))
        {
            return false;
        }
        string[] segments = relativePath.Split('/');
        return segments.All(segment => segment.Length > 0 &&
            !string.Equals(segment, ".", StringComparison.Ordinal) &&
            !string.Equals(segment, "..", StringComparison.Ordinal));
    }

    private static bool TryValidateReceiptOwnedFileMetadata(
        string gameRoot,
        SuiteReceiptFile file,
        out string error)
    {
        error = string.Empty;
        if (file == null || !File.Exists(file.FullPath) ||
            !TryValidateNoReparsePoints(gameRoot, file.FullPath, out error))
        {
            error = "owned file is missing or traverses a reparse point: " + error;
            return false;
        }
        FileInfo info = new FileInfo(file.FullPath);
        if (info.Length != file.Length)
        {
            error = "owned file length mismatch for '" + file.FullPath + "'";
            return false;
        }
        return true;
    }

    private static bool TryVerifyReceiptOwnedFile(
        string gameRoot,
        SuiteReceiptFile file,
        out string error)
    {
        error = string.Empty;
        if (!TryValidateReceiptOwnedFileMetadata(gameRoot, file, out error))
        {
            return false;
        }
        using var stream = new FileStream(
            file.FullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            65536,
            FileOptions.SequentialScan);
        if (stream.Length != file.Length)
        {
            error = "owned file length mismatch for '" + file.FullPath + "'";
            return false;
        }
        using SHA256 algorithm = SHA256.Create();
        string actualSha256 = ToLowerHex(algorithm.ComputeHash(stream));
        if (!string.Equals(actualSha256, file.Sha256, StringComparison.Ordinal))
        {
            error = "owned file SHA-256 mismatch for '" + file.FullPath + "'";
            return false;
        }
        return true;
    }

    private static bool TryValidateNoReparsePoints(
        string gameRoot,
        string filePath,
        out string error)
    {
        error = string.Empty;
        string root = Path.GetFullPath(gameRoot).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        string current = Path.GetFullPath(filePath);
        while (true)
        {
            FileSystemInfo item = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : new FileInfo(current);
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                error = "reparse point at '" + current + "'";
                return false;
            }
            if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase))
                return true;
            string next = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(next) ||
                string.Equals(next, current, StringComparison.OrdinalIgnoreCase))
            {
                error = "path ancestry did not reach the game root";
                return false;
            }
            current = next;
        }
    }

    private static bool TryRequireReceiptLoadedAssembly(
        IReadOnlyDictionary<string, SuiteReceiptFile> receiptFiles,
        Assembly assembly,
        string expectedAssemblyName,
        string authoritativePath,
        string label,
        out string error)
    {
        error = string.Empty;
        string path = string.IsNullOrWhiteSpace(authoritativePath)
            ? assembly?.Location
            : authoritativePath;
        if (string.IsNullOrWhiteSpace(path) ||
            !Path.IsPathFullyQualified(path))
        {
            error = label + " loaded path is unavailable";
            return false;
        }
        path = Path.GetFullPath(path);
        if (!receiptFiles.TryGetValue(path, out SuiteReceiptFile owned))
        {
            error = label + " is not owned by the selected suite receipt";
            return false;
        }
        if (!TryHashLoadedAssembly(
                assembly,
                expectedAssemblyName,
                path,
                out string loadedSha256,
                out error) ||
            !string.Equals(loadedSha256, owned.Sha256, StringComparison.Ordinal))
        {
            error = label + " loaded/receipt assembly identity mismatch: " + error;
            return false;
        }
        return true;
    }

    private static bool TryResolveApiHostAssembly(
        bool melonHost,
        out Assembly assembly,
        out string path,
        out string error)
    {
        assembly = null;
        path = string.Empty;
        error = string.Empty;
#if MELONLOADER
        if (!melonHost)
        {
            error = "compiled loader host does not match Core loader identity";
            return false;
        }
        MelonBase plugin = MelonBase.RegisteredMelons.FirstOrDefault(candidate =>
            string.Equals(
                candidate?.GetType().FullName,
                "OperatorModAPI.MelonLoader.OperatorApiMelon",
                StringComparison.Ordinal));
        if (plugin == null || plugin.MelonAssembly == null ||
            !string.Equals(plugin.Info?.Version, OperatorApi.ApiVersion,
                StringComparison.Ordinal))
        {
            error = "selected MelonLoader API host is unavailable or version-mismatched";
            return false;
        }
        assembly = plugin.GetType().Assembly;
        path = plugin.MelonAssembly.Location;
#else
        if (melonHost)
        {
            error = "compiled loader host does not match Core loader identity";
            return false;
        }
        IL2CPPChainloader chainloader = IL2CPPChainloader.Instance;
        if (chainloader?.Plugins == null ||
            !chainloader.Plugins.TryGetValue(PvpApiPluginGuid, out PluginInfo plugin) ||
            plugin?.Metadata == null || plugin.Instance == null)
        {
            error = "selected BepInEx API host is unavailable";
            return false;
        }
        PropertyInfo versionProperty = plugin.Metadata.GetType().GetProperty(
            "Version",
            BindingFlags.Public | BindingFlags.Instance);
        string version = versionProperty?.GetValue(plugin.Metadata)?.ToString();
        if (!string.Equals(version, OperatorApi.ApiVersion,
                StringComparison.Ordinal))
        {
            error = "selected BepInEx API host version is mismatched";
            return false;
        }
        assembly = plugin.Instance.GetType().Assembly;
        path = plugin.Location;
#endif
        if (assembly == null || string.IsNullOrWhiteSpace(path))
        {
            error = "selected API host assembly path is unavailable";
            return false;
        }
        return true;
    }

    private static bool TryComputeRuntimePairContentId(
        string pluginGuid,
        string pluginVersion,
        string bepInExSha256,
        string melonLoaderSha256,
        out string contentId,
        out string error)
    {
        contentId = string.Empty;
        error = string.Empty;
        if (!IsBoundedPeerIdentityField(pluginGuid, 128) ||
            !IsBoundedPeerIdentityField(pluginVersion, 64) ||
            !IsLowercasePvpSha256(bepInExSha256) ||
            !IsLowercasePvpSha256(melonLoaderSha256))
        {
            error = "runtime companion pair fields are invalid";
            return false;
        }
        string canonical = RuntimePairIdentityDomain + "\n" +
            pluginGuid + "\n" + pluginVersion + "\n" +
            bepInExSha256 + "\n" + melonLoaderSha256;
        using SHA256 sha = SHA256.Create();
        contentId = ToLowerHex(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
        return true;
    }

    private static bool IsBoundedPeerIdentityField(string value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        value.IndexOfAny(new[] { '\0', '\r', '\n' }) < 0;

    private static bool TryResolveLoadedPlugin(
        string pluginGuid,
        string expectedVersion,
        string expectedTypeName,
        string expectedAssemblyName,
        string expectedSha256,
        out string sha256,
        out string loadedPath,
        out string error)
    {
        sha256 = string.Empty;
        loadedPath = string.Empty;
        error = string.Empty;
#if MELONLOADER
        MelonBase plugin = MelonBase.RegisteredMelons.FirstOrDefault(candidate =>
        {
            BepInPlugin metadata = candidate?.GetType()
                .GetCustomAttribute<BepInPlugin>();
            return metadata != null && string.Equals(
                       metadata.GUID,
                       pluginGuid,
                       StringComparison.Ordinal) ||
                   expectedTypeName != null && string.Equals(
                       candidate?.GetType().FullName,
                       expectedTypeName,
                       StringComparison.Ordinal);
        });
        BepInPlugin pluginMetadata = plugin?.GetType()
            .GetCustomAttribute<BepInPlugin>();
        if (plugin == null || plugin.MelonAssembly == null)
        {
            error = "required loaded plugin '" + pluginGuid + "' is unavailable";
            return false;
        }
        string loadedGuid = pluginMetadata?.GUID ??
            (string.Equals(plugin.GetType().FullName, expectedTypeName, StringComparison.Ordinal)
                ? PvpApiPluginGuid
                : string.Empty);
        string loadedVersion = pluginMetadata?.Version ?? plugin.Info?.Version;
        if (!string.Equals(loadedGuid, pluginGuid, StringComparison.Ordinal) ||
            !string.Equals(loadedVersion, expectedVersion, StringComparison.Ordinal))
        {
            error = "loaded plugin GUID/version mismatch for '" + pluginGuid + "'";
            return false;
        }
        Type pluginType = plugin.GetType();
        if (expectedTypeName != null &&
            !string.Equals(pluginType.FullName, expectedTypeName, StringComparison.Ordinal))
        {
            error = "loaded plugin type mismatch for '" + pluginGuid + "'";
            return false;
        }
        loadedPath = plugin.MelonAssembly.Location;
        return TryHashLoadedAssembly(
            pluginType.Assembly,
            expectedAssemblyName,
            loadedPath,
            out sha256,
            out error) &&
            (expectedSha256 == null ||
             RequireExpectedPvpSha256(pluginGuid, sha256, expectedSha256, out error));
#else
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
        loadedPath = plugin.Location;
        return TryHashLoadedAssembly(
            pluginType.Assembly,
            expectedAssemblyName,
            loadedPath,
            out sha256,
            out error) &&
            (expectedSha256 == null ||
             RequireExpectedPvpSha256(pluginGuid, sha256, expectedSha256, out error));
#endif
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
