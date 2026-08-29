using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OperatorModdedOperations.PeerProtocol
{
    internal enum PeerBarrierMessageKind : byte
    {
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

    internal enum PeerPlacementRoute : byte
    {
        PveOwnerRpc = 1,
        PvpNativeObserve = 2
    }

    internal sealed class PeerBarrierHeader
    {
        private readonly byte[] sessionNonce;
        private readonly byte[] identityDigest;
        private readonly byte[] generationKey;

        internal PeerBarrierHeader(
            PeerBarrierMessageKind kind,
            int senderSlot,
            int recipientSlot,
            byte[] sessionNonce,
            byte[] identityDigest,
            ulong sceneGenerationEpoch,
            byte[] generationKey)
        {
            Kind = kind;
            SenderSlot = senderSlot;
            RecipientSlot = recipientSlot;
            this.sessionNonce = PeerRuntimeBarrierCodec.CloneExact(
                sessionNonce,
                PeerRuntimeBarrierCodec.SessionNonceBytes,
                nameof(sessionNonce));
            this.identityDigest = PeerRuntimeBarrierCodec.CloneExact(
                identityDigest,
                PeerRuntimeBarrierCodec.DigestBytes,
                nameof(identityDigest));
            SceneGenerationEpoch = sceneGenerationEpoch;
            this.generationKey = PeerRuntimeBarrierCodec.CloneExact(
                generationKey,
                PeerRuntimeBarrierCodec.DigestBytes,
                nameof(generationKey));
        }

        internal PeerBarrierMessageKind Kind { get; }
        internal int SenderSlot { get; }
        internal int RecipientSlot { get; }
        internal byte[] SessionNonce => PeerRuntimeBarrierCodec.CloneForRead(
            sessionNonce);
        internal byte[] IdentityDigest => PeerRuntimeBarrierCodec.CloneForRead(
            identityDigest);
        internal ulong SceneGenerationEpoch { get; }
        internal byte[] GenerationKey => PeerRuntimeBarrierCodec.CloneForRead(
            generationKey);
    }

    internal sealed class PeerBarrierExpectation
    {
        private readonly byte[] sceneContractDigest;
        private readonly ReadOnlyCollection<int> expectedConnectionIdsBySlot;

        internal PeerBarrierExpectation(
            PeerBarrierMessageKind kind,
            int senderSlot,
            int recipientSlot,
            byte[] sessionNonce,
            byte[] identityDigest,
            ulong sceneGenerationEpoch,
            byte[] generationKey,
            byte[] sceneContractDigest,
            int participantCount,
            int requestedEnemies,
            bool isPve,
            int maximumMarkerOrdinalExclusive = 0,
            int expectedPvePlayerTeamId = int.MinValue,
            IEnumerable<int> expectedConnectionIdsBySlot = null)
        {
            Header = new PeerBarrierHeader(
                kind,
                senderSlot,
                recipientSlot,
                sessionNonce,
                identityDigest,
                sceneGenerationEpoch,
                generationKey);
            this.sceneContractDigest = PeerRuntimeBarrierCodec.CloneExact(
                sceneContractDigest,
                PeerRuntimeBarrierCodec.DigestBytes,
                nameof(sceneContractDigest));
            ParticipantCount = participantCount;
            RequestedEnemies = requestedEnemies;
            IsPve = isPve;
            MaximumMarkerOrdinalExclusive = maximumMarkerOrdinalExclusive;
            ExpectedPvePlayerTeamId = expectedPvePlayerTeamId;
            this.expectedConnectionIdsBySlot = expectedConnectionIdsBySlot == null
                ? null
                : Array.AsReadOnly(expectedConnectionIdsBySlot.ToArray());
        }

        internal PeerBarrierHeader Header { get; }
        internal byte[] SceneContractDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(sceneContractDigest);
        internal int ParticipantCount { get; }
        internal int RequestedEnemies { get; }
        internal bool IsPve { get; }
        internal int MaximumMarkerOrdinalExclusive { get; }
        internal int ExpectedPvePlayerTeamId { get; }
        internal ReadOnlyCollection<int> ExpectedConnectionIdsBySlot =>
            expectedConnectionIdsBySlot;
    }

    internal readonly struct PeerPopulationRecord
    {
        internal PeerPopulationRecord(
            uint netId,
            uint assetId,
            int teamId,
            int positionXMillimetres,
            int positionYMillimetres,
            int positionZMillimetres,
            ushort yawCentidegrees,
            ushort componentMask)
        {
            NetId = netId;
            AssetId = assetId;
            TeamId = teamId;
            PositionXMillimetres = positionXMillimetres;
            PositionYMillimetres = positionYMillimetres;
            PositionZMillimetres = positionZMillimetres;
            YawCentidegrees = yawCentidegrees;
            ComponentMask = componentMask;
        }

        internal uint NetId { get; }
        internal uint AssetId { get; }
        internal int TeamId { get; }
        internal int PositionXMillimetres { get; }
        internal int PositionYMillimetres { get; }
        internal int PositionZMillimetres { get; }
        internal ushort YawCentidegrees { get; }
        internal ushort ComponentMask { get; }
    }

    internal sealed class PeerPopulationManifest
    {
        private readonly byte[] sceneContractDigest;
        private readonly byte[] populationDigest;
        private readonly byte[] populationIdentityDigest;

        internal PeerPopulationManifest(
            PeerBarrierHeader header,
            byte[] sceneContractDigest,
            byte[] populationDigest,
            byte[] populationIdentityDigest,
            long hostNetworkTimeMicroseconds,
            IEnumerable<PeerPopulationRecord> records)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            this.sceneContractDigest = PeerRuntimeBarrierCodec.CloneDigest(
                sceneContractDigest,
                nameof(sceneContractDigest));
            this.populationDigest = PeerRuntimeBarrierCodec.CloneDigest(
                populationDigest,
                nameof(populationDigest));
            this.populationIdentityDigest = PeerRuntimeBarrierCodec.CloneDigest(
                populationIdentityDigest,
                nameof(populationIdentityDigest));
            HostNetworkTimeMicroseconds = hostNetworkTimeMicroseconds;
            Records = Array.AsReadOnly((records ?? throw new ArgumentNullException(
                nameof(records))).ToArray());
        }

        internal PeerBarrierHeader Header { get; }
        internal byte[] SceneContractDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(sceneContractDigest);
        internal byte[] PopulationDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(populationDigest);
        internal byte[] PopulationIdentityDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(populationIdentityDigest);
        internal long HostNetworkTimeMicroseconds { get; }
        internal ReadOnlyCollection<PeerPopulationRecord> Records { get; }
    }

    internal sealed class PeerPopulationReady
    {
        private readonly byte[] sceneContractDigest;
        private readonly byte[] populationDigest;
        private readonly byte[] observedPopulationIdentityDigest;
        private readonly byte[] receiptDigest;

        internal PeerPopulationReady(
            PeerBarrierHeader header,
            byte[] sceneContractDigest,
            byte[] populationDigest,
            byte[] observedPopulationIdentityDigest,
            long clientNetworkTimeMicroseconds,
            uint maximumHorizontalErrorMillimetres,
            uint maximumVerticalErrorMillimetres,
            ushort maximumYawErrorCentidegrees,
            byte count,
            byte stableSamples,
            byte[] receiptDigest)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            this.sceneContractDigest = PeerRuntimeBarrierCodec.CloneDigest(
                sceneContractDigest,
                nameof(sceneContractDigest));
            this.populationDigest = PeerRuntimeBarrierCodec.CloneDigest(
                populationDigest,
                nameof(populationDigest));
            this.observedPopulationIdentityDigest = PeerRuntimeBarrierCodec.CloneDigest(
                observedPopulationIdentityDigest,
                nameof(observedPopulationIdentityDigest));
            ClientNetworkTimeMicroseconds = clientNetworkTimeMicroseconds;
            MaximumHorizontalErrorMillimetres = maximumHorizontalErrorMillimetres;
            MaximumVerticalErrorMillimetres = maximumVerticalErrorMillimetres;
            MaximumYawErrorCentidegrees = maximumYawErrorCentidegrees;
            Count = count;
            StableSamples = stableSamples;
            this.receiptDigest = PeerRuntimeBarrierCodec.CloneDigest(
                receiptDigest,
                nameof(receiptDigest));
        }

        internal PeerBarrierHeader Header { get; }
        internal byte[] SceneContractDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(sceneContractDigest);
        internal byte[] PopulationDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(populationDigest);
        internal byte[] ObservedPopulationIdentityDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(observedPopulationIdentityDigest);
        internal long ClientNetworkTimeMicroseconds { get; }
        internal uint MaximumHorizontalErrorMillimetres { get; }
        internal uint MaximumVerticalErrorMillimetres { get; }
        internal ushort MaximumYawErrorCentidegrees { get; }
        internal byte Count { get; }
        internal byte StableSamples { get; }
        internal byte[] ReceiptDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(receiptDigest);
    }

    internal sealed class PeerPrepareBegin
    {
        private readonly byte[] sceneContractDigest;
        private readonly byte[] populationDigest;
        private readonly byte[] populationIdentityDigest;
        private readonly byte[] groundedPlayerSetDigest;
        private readonly byte[] playerPlacementDigest;
        private readonly byte[] runtimeOwnerManifestDigest;
        private readonly byte[] runtimeReadySetDigest;
        private readonly byte[] beginContractDigest;

        internal PeerPrepareBegin(
            PeerBarrierHeader header,
            byte[] sceneContractDigest,
            byte[] populationDigest,
            byte[] populationIdentityDigest,
            byte[] groundedPlayerSetDigest,
            byte[] playerPlacementDigest,
            byte[] runtimeOwnerManifestDigest,
            byte[] runtimeReadySetDigest,
            uint runtimeOwnerNetId,
            byte participantCount,
            byte enemyCount,
            ushort flags,
            byte[] beginContractDigest)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            this.sceneContractDigest = PeerRuntimeBarrierCodec.CloneDigest(sceneContractDigest,
                nameof(sceneContractDigest));
            this.populationDigest = PeerRuntimeBarrierCodec.CloneDigest(populationDigest,
                nameof(populationDigest));
            this.populationIdentityDigest = PeerRuntimeBarrierCodec.CloneDigest(
                populationIdentityDigest,
                nameof(populationIdentityDigest));
            this.groundedPlayerSetDigest = PeerRuntimeBarrierCodec.CloneDigest(
                groundedPlayerSetDigest,
                nameof(groundedPlayerSetDigest));
            this.playerPlacementDigest = PeerRuntimeBarrierCodec.CloneDigest(
                playerPlacementDigest,
                nameof(playerPlacementDigest));
            this.runtimeOwnerManifestDigest = PeerRuntimeBarrierCodec.CloneDigest(
                runtimeOwnerManifestDigest,
                nameof(runtimeOwnerManifestDigest));
            this.runtimeReadySetDigest = PeerRuntimeBarrierCodec.CloneDigest(
                runtimeReadySetDigest,
                nameof(runtimeReadySetDigest));
            RuntimeOwnerNetId = runtimeOwnerNetId;
            ParticipantCount = participantCount;
            EnemyCount = enemyCount;
            Flags = flags;
            this.beginContractDigest = PeerRuntimeBarrierCodec.CloneDigest(
                beginContractDigest,
                nameof(beginContractDigest));
        }

        internal PeerBarrierHeader Header { get; }
        internal byte[] SceneContractDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(sceneContractDigest);
        internal byte[] PopulationDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(populationDigest);
        internal byte[] PopulationIdentityDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(populationIdentityDigest);
        internal byte[] GroundedPlayerSetDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(groundedPlayerSetDigest);
        internal byte[] PlayerPlacementDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(playerPlacementDigest);
        internal byte[] RuntimeOwnerManifestDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(runtimeOwnerManifestDigest);
        internal byte[] RuntimeReadySetDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(runtimeReadySetDigest);
        internal uint RuntimeOwnerNetId { get; }
        internal byte ParticipantCount { get; }
        internal byte EnemyCount { get; }
        internal ushort Flags { get; }
        internal byte[] BeginContractDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(beginContractDigest);
    }

    internal sealed class PeerBeginReady
    {
        private readonly byte[] beginContractDigest;
        private readonly byte[] sceneContractDigest;
        private readonly byte[] populationDigest;
        private readonly byte[] groundedPlayerSetDigest;
        private readonly byte[] playerPlacementDigest;
        private readonly byte[] runtimeOwnerManifestDigest;
        private readonly byte[] runtimeReadySetDigest;
        private readonly byte[] receiptDigest;

        internal PeerBeginReady(
            PeerBarrierHeader header,
            byte[] beginContractDigest,
            byte[] sceneContractDigest,
            byte[] populationDigest,
            byte[] groundedPlayerSetDigest,
            byte[] playerPlacementDigest,
            byte[] runtimeOwnerManifestDigest,
            byte[] runtimeReadySetDigest,
            uint runtimeOwnerNetId,
            byte participantCount,
            byte enemyCount,
            ushort flags,
            byte groundedStableSamples,
            byte[] receiptDigest)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            this.beginContractDigest = PeerRuntimeBarrierCodec.CloneDigest(
                beginContractDigest,
                nameof(beginContractDigest));
            this.sceneContractDigest = PeerRuntimeBarrierCodec.CloneDigest(
                sceneContractDigest,
                nameof(sceneContractDigest));
            this.populationDigest = PeerRuntimeBarrierCodec.CloneDigest(populationDigest,
                nameof(populationDigest));
            this.groundedPlayerSetDigest = PeerRuntimeBarrierCodec.CloneDigest(
                groundedPlayerSetDigest,
                nameof(groundedPlayerSetDigest));
            this.playerPlacementDigest = PeerRuntimeBarrierCodec.CloneDigest(
                playerPlacementDigest,
                nameof(playerPlacementDigest));
            this.runtimeOwnerManifestDigest = PeerRuntimeBarrierCodec.CloneDigest(
                runtimeOwnerManifestDigest,
                nameof(runtimeOwnerManifestDigest));
            this.runtimeReadySetDigest = PeerRuntimeBarrierCodec.CloneDigest(
                runtimeReadySetDigest,
                nameof(runtimeReadySetDigest));
            RuntimeOwnerNetId = runtimeOwnerNetId;
            ParticipantCount = participantCount;
            EnemyCount = enemyCount;
            Flags = flags;
            GroundedStableSamples = groundedStableSamples;
            this.receiptDigest = PeerRuntimeBarrierCodec.CloneDigest(receiptDigest,
                nameof(receiptDigest));
        }

        internal PeerBarrierHeader Header { get; }
        internal byte[] BeginContractDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(beginContractDigest);
        internal byte[] SceneContractDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(sceneContractDigest);
        internal byte[] PopulationDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(populationDigest);
        internal byte[] GroundedPlayerSetDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(groundedPlayerSetDigest);
        internal byte[] PlayerPlacementDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(playerPlacementDigest);
        internal byte[] RuntimeOwnerManifestDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(runtimeOwnerManifestDigest);
        internal byte[] RuntimeReadySetDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(runtimeReadySetDigest);
        internal uint RuntimeOwnerNetId { get; }
        internal byte ParticipantCount { get; }
        internal byte EnemyCount { get; }
        internal ushort Flags { get; }
        internal byte GroundedStableSamples { get; }
        internal byte[] ReceiptDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(receiptDigest);
    }

    internal sealed class PeerBeginCommit
    {
        private readonly byte[] beginContractDigest;
        private readonly byte[] populationDigest;
        private readonly byte[] readySetDigest;
        private readonly byte[] commitDigest;

        internal PeerBeginCommit(
            PeerBarrierHeader header,
            byte[] beginContractDigest,
            byte[] populationDigest,
            byte[] readySetDigest,
            byte[] commitDigest,
            byte remoteReadyCount,
            byte participantCount,
            ushort flags)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            this.beginContractDigest = PeerRuntimeBarrierCodec.CloneDigest(
                beginContractDigest,
                nameof(beginContractDigest));
            this.populationDigest = PeerRuntimeBarrierCodec.CloneDigest(populationDigest,
                nameof(populationDigest));
            this.readySetDigest = PeerRuntimeBarrierCodec.CloneDigest(readySetDigest,
                nameof(readySetDigest));
            this.commitDigest = PeerRuntimeBarrierCodec.CloneDigest(commitDigest,
                nameof(commitDigest));
            RemoteReadyCount = remoteReadyCount;
            ParticipantCount = participantCount;
            Flags = flags;
        }

        internal PeerBarrierHeader Header { get; }
        internal byte[] BeginContractDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(beginContractDigest);
        internal byte[] PopulationDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(populationDigest);
        internal byte[] ReadySetDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(readySetDigest);
        internal byte[] CommitDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(commitDigest);
        internal byte RemoteReadyCount { get; }
        internal byte ParticipantCount { get; }
        internal ushort Flags { get; }
    }

    internal readonly struct PeerSlotReceiptDigest
    {
        private readonly byte[] receiptDigest;

        internal PeerSlotReceiptDigest(int slot, byte[] receiptDigest)
        {
            Slot = slot;
            this.receiptDigest = PeerRuntimeBarrierCodec.CloneDigest(
                receiptDigest,
                nameof(receiptDigest));
        }

        internal int Slot { get; }
        internal byte[] ReceiptDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(receiptDigest);
    }

    internal readonly struct PeerPlayerAssignmentRecord
    {
        private readonly byte[] markerDigest;

        internal PeerPlayerAssignmentRecord(
            byte slot,
            byte capsuleDirection,
            ushort physicalFlags,
            int connectionId,
            uint playerMasterNetId,
            uint playerNetworkingNetId,
            int teamId,
            ushort markerOrdinal,
            byte[] markerDigest,
            int positionXMillimetres,
            int positionYMillimetres,
            int positionZMillimetres,
            ushort yawCentidegrees,
            ushort capsuleRadiusMillimetres,
            ushort capsuleHeightMillimetres,
            short capsuleCenterXMillimetres,
            short capsuleCenterYMillimetres,
            short capsuleCenterZMillimetres,
            short groundLayer)
        {
            Slot = slot;
            CapsuleDirection = capsuleDirection;
            PhysicalFlags = physicalFlags;
            ConnectionId = connectionId;
            PlayerMasterNetId = playerMasterNetId;
            PlayerNetworkingNetId = playerNetworkingNetId;
            TeamId = teamId;
            MarkerOrdinal = markerOrdinal;
            this.markerDigest = PeerRuntimeBarrierCodec.CloneDigest(markerDigest,
                nameof(markerDigest));
            PositionXMillimetres = positionXMillimetres;
            PositionYMillimetres = positionYMillimetres;
            PositionZMillimetres = positionZMillimetres;
            YawCentidegrees = yawCentidegrees;
            CapsuleRadiusMillimetres = capsuleRadiusMillimetres;
            CapsuleHeightMillimetres = capsuleHeightMillimetres;
            CapsuleCenterXMillimetres = capsuleCenterXMillimetres;
            CapsuleCenterYMillimetres = capsuleCenterYMillimetres;
            CapsuleCenterZMillimetres = capsuleCenterZMillimetres;
            GroundLayer = groundLayer;
        }

        internal byte Slot { get; }
        internal byte CapsuleDirection { get; }
        internal ushort PhysicalFlags { get; }
        internal int ConnectionId { get; }
        internal uint PlayerMasterNetId { get; }
        internal uint PlayerNetworkingNetId { get; }
        internal int TeamId { get; }
        internal ushort MarkerOrdinal { get; }
        internal byte[] MarkerDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(markerDigest);
        internal int PositionXMillimetres { get; }
        internal int PositionYMillimetres { get; }
        internal int PositionZMillimetres { get; }
        internal ushort YawCentidegrees { get; }
        internal ushort CapsuleRadiusMillimetres { get; }
        internal ushort CapsuleHeightMillimetres { get; }
        internal short CapsuleCenterXMillimetres { get; }
        internal short CapsuleCenterYMillimetres { get; }
        internal short CapsuleCenterZMillimetres { get; }
        internal short GroundLayer { get; }
    }

    internal sealed class PeerPlacePlayer
    {
        private readonly byte[] sceneContractDigest;
        private readonly byte[] playerSetDigest;
        private readonly byte[] assignmentDigest;

        internal PeerPlacePlayer(
            PeerBarrierHeader header,
            byte[] sceneContractDigest,
            byte[] playerSetDigest,
            byte[] assignmentDigest,
            PeerPlacementRoute route,
            ushort flags,
            ushort requiredOwnerStatusMask,
            IEnumerable<PeerPlayerAssignmentRecord> records)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            this.sceneContractDigest = PeerRuntimeBarrierCodec.CloneDigest(
                sceneContractDigest,
                nameof(sceneContractDigest));
            this.playerSetDigest = PeerRuntimeBarrierCodec.CloneDigest(playerSetDigest,
                nameof(playerSetDigest));
            this.assignmentDigest = PeerRuntimeBarrierCodec.CloneDigest(assignmentDigest,
                nameof(assignmentDigest));
            Route = route;
            Flags = flags;
            RequiredOwnerStatusMask = requiredOwnerStatusMask;
            Records = Array.AsReadOnly((records ?? throw new ArgumentNullException(
                nameof(records))).ToArray());
        }

        internal PeerBarrierHeader Header { get; }
        internal byte[] SceneContractDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(sceneContractDigest);
        internal byte[] PlayerSetDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(playerSetDigest);
        internal byte[] AssignmentDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(assignmentDigest);
        internal PeerPlacementRoute Route { get; }
        internal ushort Flags { get; }
        internal ushort RequiredOwnerStatusMask { get; }
        internal ReadOnlyCollection<PeerPlayerAssignmentRecord> Records { get; }
    }

    internal sealed class PeerPlayerReady
    {
        private readonly byte[] sceneContractDigest;
        private readonly byte[] assignmentDigest;
        private readonly byte[] playerSetDigest;
        private readonly byte[] assignedMarkerDigest;
        private readonly byte[] preRouteStateDigest;
        private readonly byte[] postRouteStateDigest;
        private readonly byte[] groundColliderDigest;
        private readonly byte[] receiptDigest;

        internal PeerPlayerReady(
            PeerBarrierHeader header,
            byte[] sceneContractDigest,
            byte[] assignmentDigest,
            byte[] playerSetDigest,
            byte[] assignedMarkerDigest,
            byte[] preRouteStateDigest,
            byte[] postRouteStateDigest,
            byte[] groundColliderDigest,
            uint playerMasterNetId,
            uint playerNetworkingNetId,
            int observedPositionXMillimetres,
            int observedPositionYMillimetres,
            int observedPositionZMillimetres,
            ushort observedYawCentidegrees,
            short feetToGroundDeltaMillimetres,
            ushort maximumHorizontalErrorMillimetres,
            ushort maximumVerticalErrorMillimetres,
            ushort maximumYawErrorCentidegrees,
            ushort maximumLinearSpeedMillimetresPerSecond,
            ushort maximumVerticalSpeedMillimetresPerSecond,
            ushort maximumAngularSpeedCentidegreesPerSecond,
            ushort groundSlopeCentidegrees,
            ushort maximumStableRootDriftMillimetres,
            byte stableSamples,
            PeerPlacementRoute route,
            ushort ownerStatusMask,
            byte[] receiptDigest)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            this.sceneContractDigest = PeerRuntimeBarrierCodec.CloneDigest(
                sceneContractDigest,
                nameof(sceneContractDigest));
            this.assignmentDigest = PeerRuntimeBarrierCodec.CloneDigest(assignmentDigest,
                nameof(assignmentDigest));
            this.playerSetDigest = PeerRuntimeBarrierCodec.CloneDigest(playerSetDigest,
                nameof(playerSetDigest));
            this.assignedMarkerDigest = PeerRuntimeBarrierCodec.CloneDigest(
                assignedMarkerDigest,
                nameof(assignedMarkerDigest));
            this.preRouteStateDigest = PeerRuntimeBarrierCodec.CloneDigest(
                preRouteStateDigest,
                nameof(preRouteStateDigest));
            this.postRouteStateDigest = PeerRuntimeBarrierCodec.CloneDigest(
                postRouteStateDigest,
                nameof(postRouteStateDigest));
            this.groundColliderDigest = PeerRuntimeBarrierCodec.CloneDigest(
                groundColliderDigest,
                nameof(groundColliderDigest));
            PlayerMasterNetId = playerMasterNetId;
            PlayerNetworkingNetId = playerNetworkingNetId;
            ObservedPositionXMillimetres = observedPositionXMillimetres;
            ObservedPositionYMillimetres = observedPositionYMillimetres;
            ObservedPositionZMillimetres = observedPositionZMillimetres;
            ObservedYawCentidegrees = observedYawCentidegrees;
            FeetToGroundDeltaMillimetres = feetToGroundDeltaMillimetres;
            MaximumHorizontalErrorMillimetres = maximumHorizontalErrorMillimetres;
            MaximumVerticalErrorMillimetres = maximumVerticalErrorMillimetres;
            MaximumYawErrorCentidegrees = maximumYawErrorCentidegrees;
            MaximumLinearSpeedMillimetresPerSecond =
                maximumLinearSpeedMillimetresPerSecond;
            MaximumVerticalSpeedMillimetresPerSecond =
                maximumVerticalSpeedMillimetresPerSecond;
            MaximumAngularSpeedCentidegreesPerSecond =
                maximumAngularSpeedCentidegreesPerSecond;
            GroundSlopeCentidegrees = groundSlopeCentidegrees;
            MaximumStableRootDriftMillimetres = maximumStableRootDriftMillimetres;
            StableSamples = stableSamples;
            Route = route;
            OwnerStatusMask = ownerStatusMask;
            this.receiptDigest = PeerRuntimeBarrierCodec.CloneDigest(receiptDigest,
                nameof(receiptDigest));
        }

        internal PeerBarrierHeader Header { get; }
        internal byte[] SceneContractDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(sceneContractDigest);
        internal byte[] AssignmentDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(assignmentDigest);
        internal byte[] PlayerSetDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(playerSetDigest);
        internal byte[] AssignedMarkerDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(assignedMarkerDigest);
        internal byte[] PreRouteStateDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(preRouteStateDigest);
        internal byte[] PostRouteStateDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(postRouteStateDigest);
        internal byte[] GroundColliderDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(groundColliderDigest);
        internal uint PlayerMasterNetId { get; }
        internal uint PlayerNetworkingNetId { get; }
        internal int ObservedPositionXMillimetres { get; }
        internal int ObservedPositionYMillimetres { get; }
        internal int ObservedPositionZMillimetres { get; }
        internal ushort ObservedYawCentidegrees { get; }
        internal short FeetToGroundDeltaMillimetres { get; }
        internal ushort MaximumHorizontalErrorMillimetres { get; }
        internal ushort MaximumVerticalErrorMillimetres { get; }
        internal ushort MaximumYawErrorCentidegrees { get; }
        internal ushort MaximumLinearSpeedMillimetresPerSecond { get; }
        internal ushort MaximumVerticalSpeedMillimetresPerSecond { get; }
        internal ushort MaximumAngularSpeedCentidegreesPerSecond { get; }
        internal ushort GroundSlopeCentidegrees { get; }
        internal ushort MaximumStableRootDriftMillimetres { get; }
        internal byte StableSamples { get; }
        internal PeerPlacementRoute Route { get; }
        internal ushort OwnerStatusMask { get; }
        internal byte[] ReceiptDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(receiptDigest);
    }

    internal static partial class PeerRuntimeBarrierCodec
    {
        internal const ushort ProtocolVersion = 6;
        internal const ushort MirrorMessageId = 0x4D4F;
        internal const int MaximumEnvelopeBytes = 8192;
        internal const int SessionNonceBytes = 16;
        internal const int DigestBytes = 32;
        internal const int CommonHeaderBytes = 101;
        internal const int MaximumPopulation = 100;
        internal const int MaximumParticipants = 64;
        internal const byte PopulationRecordVersion = 1;
        internal const ushort PopulationRecordBytes = 28;
        internal const ushort PopulationComponentMask = 0x007F;
        internal const byte PlayerRecordVersion = 1;
        internal const byte PlayerRecordBytes = 84;
        internal const ushort PlayerPhysicalFlags = 0x003F;
        internal const ushort PlayerOwnerStatusMask = 0x03FF;
        internal const ushort PvePlaceFlags = 0x000F;
        internal const ushort PvpPlaceFlags = 0x0017;
        internal const ushort PveBeginFlags = 0x0007;
        internal const ushort PvpBeginFlags = 0x0004;
        internal const int PrepareBeginBytes = 365;
        internal const int BeginReadyBytes = 369;
        internal const int PrepareBeginRuntimeOwnerManifestDigestOffset = 261;
        internal const int PrepareBeginRuntimeReadySetDigestOffset = 293;
        internal const int PrepareBeginRuntimeOwnerNetIdOffset = 325;
        internal const int BeginReadyRuntimeOwnerManifestDigestOffset = 261;
        internal const int BeginReadyRuntimeReadySetDigestOffset = 293;
        internal const int BeginReadyRuntimeOwnerNetIdOffset = 325;

        internal static byte[] CloneDigest(byte[] value, string name) =>
            CloneExact(value, DigestBytes, name);

        internal static byte[] CloneForRead(byte[] value)
        {
            if (value == null)
                throw new InvalidOperationException(
                    "immutable barrier byte field was not initialized");
            return (byte[])value.Clone();
        }

        internal static byte[] CloneExact(byte[] value, int length, string name)
        {
            if (value == null)
                throw new ArgumentNullException(name);
            if (value.Length != length)
                throw new ArgumentException(
                    name + " must contain exactly " + length + " bytes",
                    name);
            return (byte[])value.Clone();
        }

        internal static bool TryDecodeCanonicalSessionNonce(
            string value,
            out byte[] bytes)
        {
            return TryDecodeLowerHex(value, SessionNonceBytes, out bytes);
        }

        internal static bool TryDecodeCanonicalDigest(
            string value,
            out byte[] bytes)
        {
            return TryDecodeLowerHex(value, DigestBytes, out bytes);
        }

        private static bool TryDecodeLowerHex(
            string value,
            int byteCount,
            out byte[] bytes)
        {
            bytes = null;
            if (value == null || value.Length != byteCount * 2)
                return false;
            var result = new byte[byteCount];
            for (int index = 0; index < result.Length; index++)
            {
                int high = LowerHexNibble(value[index * 2]);
                int low = LowerHexNibble(value[index * 2 + 1]);
                if (high < 0 || low < 0)
                    return false;
                result[index] = checked((byte)((high << 4) | low));
            }
            bytes = result;
            return true;
        }

        private static int LowerHexNibble(char value)
        {
            if (value >= '0' && value <= '9')
                return value - '0';
            if (value >= 'a' && value <= 'f')
                return value - 'a' + 10;
            return -1;
        }

        internal static bool TryQuantizeMillimetres(
            double metres,
            out int millimetres)
        {
            millimetres = 0;
            if (double.IsNaN(metres) || double.IsInfinity(metres))
                return false;
            try
            {
                double rounded = Math.Round(
                    checked(metres * 1000d),
                    MidpointRounding.AwayFromZero);
                if (rounded < int.MinValue || rounded > int.MaxValue)
                    return false;
                millimetres = checked((int)rounded);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        internal static bool TryQuantizeYawCentidegrees(
            double yawDegrees,
            out ushort centidegrees)
        {
            centidegrees = 0;
            if (double.IsNaN(yawDegrees) || double.IsInfinity(yawDegrees))
                return false;
            double normalized = ((yawDegrees % 360d) + 360d) % 360d;
            double rounded = Math.Round(
                normalized * 100d,
                MidpointRounding.AwayFromZero);
            int canonical = checked((int)rounded % 36000);
            centidegrees = checked((ushort)canonical);
            return true;
        }

        internal static ushort WrappedYawErrorCentidegrees(
            ushort left,
            ushort right)
        {
            if (left >= 36000 || right >= 36000)
                throw new ArgumentOutOfRangeException(nameof(left));
            int delta = Math.Abs((int)left - right);
            return checked((ushort)Math.Min(delta, 36000 - delta));
        }

        internal static byte[] ComputeGenerationKey(
            byte[] sessionNonce,
            byte[] identityDigest,
            ulong sceneGenerationEpoch,
            byte[] sceneContractDigest,
            int requestedEnemies)
        {
            byte[] nonce = CloneExact(
                sessionNonce,
                SessionNonceBytes,
                nameof(sessionNonce));
            byte[] identity = CloneDigest(identityDigest, nameof(identityDigest));
            byte[] scene = CloneDigest(
                sceneContractDigest,
                nameof(sceneContractDigest));
            if (sceneGenerationEpoch == 0 || requestedEnemies < 0 ||
                requestedEnemies > MaximumPopulation)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedEnemies));
            }
            return HashCanonical(writer =>
            {
                writer.WriteDomain("operator-peer-generation-v1\0");
                writer.WriteBytes(nonce);
                writer.WriteBytes(identity);
                writer.WriteUInt64(sceneGenerationEpoch);
                writer.WriteBytes(scene);
                writer.WriteInt32(requestedEnemies);
            });
        }

        internal static bool DigestEquals(byte[] left, byte[] right)
        {
            return left != null && right != null &&
                left.Length == DigestBytes && right.Length == DigestBytes &&
                CryptographicOperations.FixedTimeEquals(left, right);
        }

        private static bool FixedBytesEqual(byte[] left, byte[] right, int length)
        {
            return left != null && right != null && left.Length == length &&
                right.Length == length &&
                CryptographicOperations.FixedTimeEquals(left, right);
        }

        private static bool IsAllZero(byte[] value)
        {
            if (value == null)
                return true;
            int aggregate = 0;
            for (int index = 0; index < value.Length; index++)
                aggregate |= value[index];
            return aggregate == 0;
        }

        private static bool TryValidateHeader(
            PeerBarrierHeader header,
            PeerBarrierMessageKind expectedKind,
            out string error,
            bool allowLocalConceptual = false)
        {
            error = string.Empty;
            bool localConceptual = allowLocalConceptual &&
                (expectedKind == PeerBarrierMessageKind.PlayerReady ||
                 expectedKind == PeerBarrierMessageKind.PlacePlayer ||
                 expectedKind == PeerBarrierMessageKind.RuntimeReady ||
                 expectedKind == PeerBarrierMessageKind.RuntimeOwnerManifest) &&
                header != null && header.SenderSlot == 0 &&
                header.RecipientSlot == 0;
            if (header == null || header.Kind != expectedKind ||
                header.SceneGenerationEpoch == 0 ||
                header.SenderSlot < 0 ||
                header.SenderSlot >= MaximumParticipants ||
                header.RecipientSlot < 0 ||
                header.RecipientSlot >= MaximumParticipants ||
                (header.SenderSlot == header.RecipientSlot && !localConceptual) ||
                header.SessionNonce == null ||
                header.SessionNonce.Length != SessionNonceBytes ||
                IsAllZero(header.SessionNonce) ||
                header.IdentityDigest == null ||
                header.IdentityDigest.Length != DigestBytes ||
                IsAllZero(header.IdentityDigest) ||
                header.GenerationKey == null ||
                header.GenerationKey.Length != DigestBytes ||
                IsAllZero(header.GenerationKey))
            {
                error = "barrier header is noncanonical";
                return false;
            }

            if (localConceptual)
                return true;
            bool hostToRemote = expectedKind ==
                    PeerBarrierMessageKind.PopulationManifest ||
                expectedKind == PeerBarrierMessageKind.PrepareBegin ||
                expectedKind == PeerBarrierMessageKind.BeginCommit ||
                expectedKind == PeerBarrierMessageKind.PlacePlayer ||
                expectedKind == PeerBarrierMessageKind.RuntimeOwnerManifest;
            if (hostToRemote
                    ? header.SenderSlot != 0 || header.RecipientSlot == 0
                    : header.SenderSlot == 0 || header.RecipientSlot != 0)
            {
                error = "barrier header direction/slot is invalid";
                return false;
            }
            return true;
        }

        private static bool TryValidateExpectedHeader(
            PeerBarrierHeader observed,
            PeerBarrierExpectation expected,
            out string error,
            bool allowLocalConceptual = false)
        {
            error = string.Empty;
            if (expected == null || expected.Header == null ||
                !TryValidateHeader(
                    observed,
                    expected.Header.Kind,
                    out error,
                    allowLocalConceptual) ||
                observed.SenderSlot != expected.Header.SenderSlot ||
                observed.RecipientSlot != expected.Header.RecipientSlot ||
                observed.SceneGenerationEpoch !=
                    expected.Header.SceneGenerationEpoch ||
                !FixedBytesEqual(
                    observed.SessionNonce,
                    expected.Header.SessionNonce,
                    SessionNonceBytes) ||
                !DigestEquals(
                    observed.IdentityDigest,
                    expected.Header.IdentityDigest) ||
                !DigestEquals(
                    observed.GenerationKey,
                    expected.Header.GenerationKey))
            {
                if (string.IsNullOrEmpty(error))
                    error = "barrier header does not match the frozen generation";
                return false;
            }
            if (expected.ParticipantCount < 1 ||
                expected.ParticipantCount > MaximumParticipants ||
                expected.Header.SenderSlot >= expected.ParticipantCount ||
                expected.Header.RecipientSlot >= expected.ParticipantCount ||
                expected.RequestedEnemies < 0 ||
                expected.RequestedEnemies > MaximumPopulation ||
                (expected.IsPve
                    ? expected.RequestedEnemies == 0
                    : expected.RequestedEnemies != 0) ||
                expected.SceneContractDigest == null ||
                expected.SceneContractDigest.Length != DigestBytes ||
                IsAllZero(expected.SceneContractDigest))
            {
                error = "barrier expectation is noncanonical";
                return false;
            }
            if (expected.ExpectedConnectionIdsBySlot != null &&
                (expected.ExpectedConnectionIdsBySlot.Count !=
                     expected.ParticipantCount ||
                 expected.ExpectedConnectionIdsBySlot.Distinct().Count() !=
                     expected.ParticipantCount))
            {
                error = "frozen slot-to-connection roster is noncanonical";
                return false;
            }
            byte[] generation = ComputeGenerationKey(
                expected.Header.SessionNonce,
                expected.Header.IdentityDigest,
                expected.Header.SceneGenerationEpoch,
                expected.SceneContractDigest,
                expected.RequestedEnemies);
            if (!DigestEquals(generation, observed.GenerationKey))
            {
                error = "barrier generation key does not match frozen semantics";
                return false;
            }
            return true;
        }

        private static void WriteHeader(
            PeerFixedWriter writer,
            PeerBarrierHeader header,
            PeerBarrierMessageKind kind,
            bool allowLocalConceptual = false)
        {
            if (!TryValidateHeader(
                    header,
                    kind,
                    out string error,
                    allowLocalConceptual))
                throw new InvalidDataException(error);
            writer.WriteUInt16(MirrorMessageId);
            writer.WriteUInt16(ProtocolVersion);
            writer.WriteByte((byte)kind);
            writer.WriteInt32(header.SenderSlot);
            writer.WriteInt32(header.RecipientSlot);
            writer.WriteBytes(header.SessionNonce);
            writer.WriteBytes(header.IdentityDigest);
            writer.WriteUInt64(header.SceneGenerationEpoch);
            writer.WriteBytes(header.GenerationKey);
        }

        private static PeerBarrierHeader ReadHeader(
            PeerFixedReader reader,
            PeerBarrierMessageKind expectedKind,
            bool allowLocalConceptual = false)
        {
            if (reader.ReadUInt16() != MirrorMessageId ||
                reader.ReadUInt16() != ProtocolVersion ||
                reader.ReadByte() != (byte)expectedKind)
            {
                throw new InvalidDataException(
                    "barrier namespace/protocol/kind is invalid");
            }
            var header = new PeerBarrierHeader(
                expectedKind,
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadBytes(SessionNonceBytes),
                reader.ReadBytes(DigestBytes),
                reader.ReadUInt64(),
                reader.ReadBytes(DigestBytes));
            if (!TryValidateHeader(
                    header,
                    expectedKind,
                    out string error,
                    allowLocalConceptual))
                throw new InvalidDataException(error);
            return header;
        }

        private static byte[] HashCanonical(Action<PeerCanonicalWriter> write)
        {
            if (write == null)
                throw new ArgumentNullException(nameof(write));
            using var writer = new PeerCanonicalWriter();
            write(writer);
            using SHA256 sha = SHA256.Create();
            return sha.ComputeHash(writer.ToArray());
        }

        private sealed class PeerCanonicalWriter : IDisposable
        {
            private readonly MemoryStream stream = new MemoryStream();

            internal void WriteDomain(string value)
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                WriteBytes(Encoding.ASCII.GetBytes(value));
            }

            internal void WriteByte(byte value) => stream.WriteByte(value);
            internal void WriteBytes(byte[] value)
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                stream.Write(value, 0, value.Length);
            }
            internal void WriteUInt16(ushort value)
            {
                var bytes = new byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
                WriteBytes(bytes);
            }
            internal void WriteInt16(short value)
            {
                var bytes = new byte[2];
                BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
                WriteBytes(bytes);
            }
            internal void WriteUInt32(uint value)
            {
                var bytes = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
                WriteBytes(bytes);
            }
            internal void WriteInt32(int value)
            {
                var bytes = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
                WriteBytes(bytes);
            }
            internal void WriteUInt64(ulong value)
            {
                var bytes = new byte[8];
                BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
                WriteBytes(bytes);
            }
            internal void WriteInt64(long value)
            {
                var bytes = new byte[8];
                BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
                WriteBytes(bytes);
            }
            internal byte[] ToArray() => stream.ToArray();
            public void Dispose() => stream.Dispose();
        }

        private sealed class PeerFixedWriter
        {
            private readonly byte[] bytes;
            private int offset;

            internal PeerFixedWriter(int length)
            {
                if (length < CommonHeaderBytes || length > MaximumEnvelopeBytes)
                    throw new ArgumentOutOfRangeException(nameof(length));
                bytes = new byte[length];
            }

            internal int Offset => offset;
            internal void WriteByte(byte value)
            {
                Require(1);
                bytes[offset++] = value;
            }
            internal void WriteBytes(byte[] value)
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                Require(value.Length);
                Buffer.BlockCopy(value, 0, bytes, offset, value.Length);
                offset += value.Length;
            }
            internal void WriteUInt16(ushort value)
            {
                Require(2);
                BinaryPrimitives.WriteUInt16LittleEndian(
                    bytes.AsSpan(offset, 2), value);
                offset += 2;
            }
            internal void WriteInt16(short value)
            {
                Require(2);
                BinaryPrimitives.WriteInt16LittleEndian(
                    bytes.AsSpan(offset, 2), value);
                offset += 2;
            }
            internal void WriteUInt32(uint value)
            {
                Require(4);
                BinaryPrimitives.WriteUInt32LittleEndian(
                    bytes.AsSpan(offset, 4), value);
                offset += 4;
            }
            internal void WriteInt32(int value)
            {
                Require(4);
                BinaryPrimitives.WriteInt32LittleEndian(
                    bytes.AsSpan(offset, 4), value);
                offset += 4;
            }
            internal void WriteUInt64(ulong value)
            {
                Require(8);
                BinaryPrimitives.WriteUInt64LittleEndian(
                    bytes.AsSpan(offset, 8), value);
                offset += 8;
            }
            internal void WriteInt64(long value)
            {
                Require(8);
                BinaryPrimitives.WriteInt64LittleEndian(
                    bytes.AsSpan(offset, 8), value);
                offset += 8;
            }
            internal byte[] Complete()
            {
                if (offset != bytes.Length)
                    throw new InvalidDataException(
                        "barrier encoder did not consume its fixed envelope");
                return bytes;
            }
            private void Require(int count)
            {
                if (count < 0 || offset > bytes.Length - count)
                    throw new InvalidDataException("barrier encoder overflow");
            }
        }

        private sealed class PeerFixedReader
        {
            private readonly byte[] bytes;
            private int offset;

            internal PeerFixedReader(byte[] value)
            {
                bytes = value ?? throw new ArgumentNullException(nameof(value));
                if (bytes.Length < CommonHeaderBytes ||
                    bytes.Length > MaximumEnvelopeBytes)
                {
                    throw new InvalidDataException(
                        "barrier envelope length is out of bounds");
                }
            }

            internal int Remaining => bytes.Length - offset;
            internal byte ReadByte()
            {
                Require(1);
                return bytes[offset++];
            }
            internal byte[] ReadBytes(int count)
            {
                Require(count);
                var result = new byte[count];
                Buffer.BlockCopy(bytes, offset, result, 0, count);
                offset += count;
                return result;
            }
            internal ushort ReadUInt16()
            {
                Require(2);
                ushort value = BinaryPrimitives.ReadUInt16LittleEndian(
                    bytes.AsSpan(offset, 2));
                offset += 2;
                return value;
            }
            internal short ReadInt16()
            {
                Require(2);
                short value = BinaryPrimitives.ReadInt16LittleEndian(
                    bytes.AsSpan(offset, 2));
                offset += 2;
                return value;
            }
            internal uint ReadUInt32()
            {
                Require(4);
                uint value = BinaryPrimitives.ReadUInt32LittleEndian(
                    bytes.AsSpan(offset, 4));
                offset += 4;
                return value;
            }
            internal int ReadInt32()
            {
                Require(4);
                int value = BinaryPrimitives.ReadInt32LittleEndian(
                    bytes.AsSpan(offset, 4));
                offset += 4;
                return value;
            }
            internal ulong ReadUInt64()
            {
                Require(8);
                ulong value = BinaryPrimitives.ReadUInt64LittleEndian(
                    bytes.AsSpan(offset, 8));
                offset += 8;
                return value;
            }
            internal long ReadInt64()
            {
                Require(8);
                long value = BinaryPrimitives.ReadInt64LittleEndian(
                    bytes.AsSpan(offset, 8));
                offset += 8;
                return value;
            }
            internal void RequireComplete()
            {
                if (Remaining != 0)
                    throw new InvalidDataException(
                        "barrier envelope contains trailing bytes");
            }
            private void Require(int count)
            {
                if (count < 0 || offset > bytes.Length - count)
                    throw new InvalidDataException("barrier envelope is truncated");
            }
        }

        internal static byte[] ComputePopulationIdentityDigest(
            byte[] generationKey,
            IReadOnlyList<PeerPopulationRecord> records)
        {
            byte[] generation = CloneDigest(generationKey, nameof(generationKey));
            if (!TryValidatePopulationRecords(records, -1, out string error))
                throw new InvalidDataException(error);
            return HashCanonical(writer =>
            {
                writer.WriteDomain("operator-pve-population-identity-v1\0");
                writer.WriteBytes(generation);
                for (int index = 0; index < records.Count; index++)
                {
                    PeerPopulationRecord record = records[index];
                    writer.WriteUInt32(record.NetId);
                    writer.WriteUInt32(record.AssetId);
                    writer.WriteInt32(record.TeamId);
                    writer.WriteUInt16(record.ComponentMask);
                }
            });
        }

        internal static byte[] ComputePopulationDigest(
            byte[] generationKey,
            byte[] sceneContractDigest,
            long hostNetworkTimeMicroseconds,
            IReadOnlyList<PeerPopulationRecord> records)
        {
            byte[] generation = CloneDigest(generationKey, nameof(generationKey));
            byte[] scene = CloneDigest(sceneContractDigest,
                nameof(sceneContractDigest));
            if (hostNetworkTimeMicroseconds < 0)
            {
                throw new InvalidDataException(
                    "population snapshot time is negative");
            }
            if (!TryValidatePopulationRecords(records, -1, out string error))
                throw new InvalidDataException(error);
            return HashCanonical(writer =>
            {
                writer.WriteDomain("operator-pve-population-manifest-v1\0");
                writer.WriteBytes(generation);
                writer.WriteBytes(scene);
                writer.WriteInt64(hostNetworkTimeMicroseconds);
                writer.WriteByte(checked((byte)records.Count));
                writer.WriteByte(PopulationRecordVersion);
                writer.WriteUInt16(PopulationRecordBytes);
                for (int index = 0; index < records.Count; index++)
                    WritePopulationRecord(writer, records[index]);
            });
        }

        internal static byte[] EncodePopulationManifest(
            PeerPopulationManifest message)
        {
            if (!TryValidatePopulationManifest(message, null, out string error))
                throw new InvalidDataException(error);
            int length = checked(
                CommonHeaderBytes + 108 +
                message.Records.Count * PopulationRecordBytes);
            var writer = new PeerFixedWriter(length);
            WriteHeader(
                writer,
                message.Header,
                PeerBarrierMessageKind.PopulationManifest);
            writer.WriteBytes(message.SceneContractDigest);
            writer.WriteBytes(message.PopulationDigest);
            writer.WriteBytes(message.PopulationIdentityDigest);
            writer.WriteInt64(message.HostNetworkTimeMicroseconds);
            writer.WriteByte(checked((byte)message.Records.Count));
            writer.WriteByte(PopulationRecordVersion);
            writer.WriteUInt16(PopulationRecordBytes);
            for (int index = 0; index < message.Records.Count; index++)
                WritePopulationRecord(writer, message.Records[index]);
            return writer.Complete();
        }

        internal static bool TryDecodePopulationManifest(
            byte[] envelope,
            PeerBarrierExpectation expectation,
            out PeerPopulationManifest message,
            out string error)
        {
            message = null;
            error = string.Empty;
            try
            {
                var reader = new PeerFixedReader(envelope);
                PeerBarrierHeader header = ReadHeader(
                    reader,
                    PeerBarrierMessageKind.PopulationManifest);
                byte[] scene = reader.ReadBytes(DigestBytes);
                byte[] population = reader.ReadBytes(DigestBytes);
                byte[] identity = reader.ReadBytes(DigestBytes);
                long snapshot = reader.ReadInt64();
                int count = reader.ReadByte();
                if (reader.ReadByte() != PopulationRecordVersion ||
                    reader.ReadUInt16() != PopulationRecordBytes ||
                    count < 1 || count > MaximumPopulation ||
                    reader.Remaining != checked(count * PopulationRecordBytes))
                {
                    throw new InvalidDataException(
                        "population manifest count/version/length is invalid");
                }
                var records = new PeerPopulationRecord[count];
                for (int index = 0; index < count; index++)
                    records[index] = ReadPopulationRecord(reader);
                reader.RequireComplete();
                var candidate = new PeerPopulationManifest(
                    header,
                    scene,
                    population,
                    identity,
                    snapshot,
                    records);
                if (!TryValidatePopulationManifest(candidate, expectation, out error))
                    return false;
                message = candidate;
                return true;
            }
            catch (Exception ex) when (ex is InvalidDataException ||
                ex is ArgumentException || ex is OverflowException)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryValidatePopulationManifest(
            PeerPopulationManifest message,
            PeerBarrierExpectation expectation,
            out string error)
        {
            error = string.Empty;
            if (message == null ||
                !TryValidateHeader(
                    message.Header,
                    PeerBarrierMessageKind.PopulationManifest,
                    out error) ||
                !TryValidateNonzeroDigest(message.SceneContractDigest) ||
                !TryValidateNonzeroDigest(message.PopulationDigest) ||
                !TryValidateNonzeroDigest(message.PopulationIdentityDigest) ||
                message.HostNetworkTimeMicroseconds < 0 ||
                !TryValidatePopulationRecords(
                    message.Records,
                    expectation?.RequestedEnemies ?? -1,
                    out error))
            {
                if (string.IsNullOrEmpty(error))
                    error = "population manifest is noncanonical";
                return false;
            }
            byte[] expectedGeneration = ComputeGenerationKey(
                message.Header.SessionNonce,
                message.Header.IdentityDigest,
                message.Header.SceneGenerationEpoch,
                message.SceneContractDigest,
                message.Records.Count);
            byte[] expectedIdentity = ComputePopulationIdentityDigest(
                message.Header.GenerationKey,
                message.Records);
            byte[] expectedPopulation = ComputePopulationDigest(
                message.Header.GenerationKey,
                message.SceneContractDigest,
                message.HostNetworkTimeMicroseconds,
                message.Records);
            if (!DigestEquals(message.Header.GenerationKey, expectedGeneration) ||
                !DigestEquals(message.PopulationIdentityDigest, expectedIdentity) ||
                !DigestEquals(message.PopulationDigest, expectedPopulation))
            {
                error = "population manifest digest is invalid";
                return false;
            }
            if (expectation != null &&
                (!expectation.IsPve ||
                 !TryValidateExpectedHeader(message.Header, expectation, out error) ||
                 !DigestEquals(
                     message.SceneContractDigest,
                     expectation.SceneContractDigest)))
            {
                if (string.IsNullOrEmpty(error))
                    error = "population manifest does not match frozen PVE state";
                return false;
            }
            return true;
        }

        private static bool TryValidatePopulationRecords(
            IReadOnlyList<PeerPopulationRecord> records,
            int expectedCount,
            out string error)
        {
            error = string.Empty;
            if (records == null || records.Count < 1 ||
                records.Count > MaximumPopulation ||
                (expectedCount >= 0 && records.Count != expectedCount))
            {
                error = "population record count is invalid";
                return false;
            }
            uint previousNetId = 0;
            for (int index = 0; index < records.Count; index++)
            {
                PeerPopulationRecord record = records[index];
                if (record.NetId == 0 || record.NetId <= previousNetId ||
                    record.AssetId == 0 || record.TeamId == -1 ||
                    record.YawCentidegrees >= 36000 ||
                    record.ComponentMask != PopulationComponentMask)
                {
                    error = "population record " + index + " is noncanonical";
                    return false;
                }
                previousNetId = record.NetId;
            }
            return true;
        }

        private static void WritePopulationRecord(
            PeerFixedWriter writer,
            PeerPopulationRecord record)
        {
            writer.WriteUInt32(record.NetId);
            writer.WriteUInt32(record.AssetId);
            writer.WriteInt32(record.TeamId);
            writer.WriteInt32(record.PositionXMillimetres);
            writer.WriteInt32(record.PositionYMillimetres);
            writer.WriteInt32(record.PositionZMillimetres);
            writer.WriteUInt16(record.YawCentidegrees);
            writer.WriteUInt16(record.ComponentMask);
        }

        private static void WritePopulationRecord(
            PeerCanonicalWriter writer,
            PeerPopulationRecord record)
        {
            writer.WriteUInt32(record.NetId);
            writer.WriteUInt32(record.AssetId);
            writer.WriteInt32(record.TeamId);
            writer.WriteInt32(record.PositionXMillimetres);
            writer.WriteInt32(record.PositionYMillimetres);
            writer.WriteInt32(record.PositionZMillimetres);
            writer.WriteUInt16(record.YawCentidegrees);
            writer.WriteUInt16(record.ComponentMask);
        }

        private static PeerPopulationRecord ReadPopulationRecord(
            PeerFixedReader reader)
        {
            return new PeerPopulationRecord(
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadUInt16(),
                reader.ReadUInt16());
        }

        internal static byte[] ComputePopulationReadyReceiptDigest(
            PeerPopulationReady message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            var prefix = new PeerFixedWriter(CommonHeaderBytes + 116);
            WriteHeader(
                prefix,
                message.Header,
                PeerBarrierMessageKind.PopulationReady);
            WritePopulationReadyTailWithoutDigest(prefix, message);
            byte[] semantic = prefix.Complete();
            return HashCanonical(writer =>
            {
                writer.WriteDomain("operator-pve-population-ready-v1\0");
                writer.WriteBytes(semantic);
            });
        }

        internal static byte[] EncodePopulationReady(PeerPopulationReady message)
        {
            if (!TryValidatePopulationReady(
                    message,
                    null,
                    null,
                    null,
                    out string error))
            {
                throw new InvalidDataException(error);
            }
            var writer = new PeerFixedWriter(249);
            WriteHeader(writer, message.Header, PeerBarrierMessageKind.PopulationReady);
            WritePopulationReadyTailWithoutDigest(writer, message);
            writer.WriteBytes(message.ReceiptDigest);
            return writer.Complete();
        }

        internal static bool TryDecodePopulationReady(
            byte[] envelope,
            PeerBarrierExpectation expectation,
            byte[] expectedPopulationDigest,
            byte[] expectedPopulationIdentityDigest,
            out PeerPopulationReady message,
            out string error)
        {
            message = null;
            error = string.Empty;
            try
            {
                if (envelope == null || envelope.Length != 249)
                    throw new InvalidDataException(
                        "PopulationReady must contain exactly 249 bytes");
                var reader = new PeerFixedReader(envelope);
                var candidate = new PeerPopulationReady(
                    ReadHeader(reader, PeerBarrierMessageKind.PopulationReady),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadInt64(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadUInt16(),
                    reader.ReadByte(),
                    reader.ReadByte(),
                    reader.ReadBytes(DigestBytes));
                reader.RequireComplete();
                if (!TryValidatePopulationReady(
                        candidate,
                        expectation,
                        expectedPopulationDigest,
                        expectedPopulationIdentityDigest,
                        out error))
                {
                    return false;
                }
                message = candidate;
                return true;
            }
            catch (Exception ex) when (ex is InvalidDataException ||
                ex is ArgumentException || ex is OverflowException)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryValidatePopulationReady(
            PeerPopulationReady message,
            PeerBarrierExpectation expectation,
            byte[] expectedPopulationDigest,
            byte[] expectedPopulationIdentityDigest,
            out string error)
        {
            error = string.Empty;
            if (message == null ||
                !TryValidateHeader(
                    message.Header,
                    PeerBarrierMessageKind.PopulationReady,
                    out error) ||
                !TryValidateNonzeroDigest(message.SceneContractDigest) ||
                !TryValidateNonzeroDigest(message.PopulationDigest) ||
                !TryValidateNonzeroDigest(
                    message.ObservedPopulationIdentityDigest) ||
                message.ClientNetworkTimeMicroseconds < 0 ||
                message.Count < 1 || message.Count > MaximumPopulation ||
                message.StableSamples != 3 ||
                message.MaximumHorizontalErrorMillimetres > 750 ||
                message.MaximumVerticalErrorMillimetres > 500 ||
                message.MaximumYawErrorCentidegrees > 1500 ||
                !DigestEquals(
                    message.ReceiptDigest,
                    ComputePopulationReadyReceiptDigest(message)))
            {
                if (string.IsNullOrEmpty(error))
                    error = "PopulationReady is noncanonical";
                return false;
            }
            byte[] generation = ComputeGenerationKey(
                message.Header.SessionNonce,
                message.Header.IdentityDigest,
                message.Header.SceneGenerationEpoch,
                message.SceneContractDigest,
                message.Count);
            if (!DigestEquals(generation, message.Header.GenerationKey))
            {
                error = "PopulationReady generation key is invalid";
                return false;
            }
            if (expectation != null &&
                (!expectation.IsPve ||
                 message.Count != expectation.RequestedEnemies ||
                 !TryValidateExpectedHeader(message.Header, expectation, out error) ||
                 !DigestEquals(
                     message.SceneContractDigest,
                     expectation.SceneContractDigest)))
            {
                if (string.IsNullOrEmpty(error))
                    error = "PopulationReady does not match frozen PVE state";
                return false;
            }
            if ((expectedPopulationDigest != null &&
                 !DigestEquals(message.PopulationDigest, expectedPopulationDigest)) ||
                (expectedPopulationIdentityDigest != null &&
                 !DigestEquals(
                     message.ObservedPopulationIdentityDigest,
                     expectedPopulationIdentityDigest)))
            {
                error = "PopulationReady does not match the frozen manifest";
                return false;
            }
            return true;
        }

        private static void WritePopulationReadyTailWithoutDigest(
            PeerFixedWriter writer,
            PeerPopulationReady message)
        {
            writer.WriteBytes(message.SceneContractDigest);
            writer.WriteBytes(message.PopulationDigest);
            writer.WriteBytes(message.ObservedPopulationIdentityDigest);
            writer.WriteInt64(message.ClientNetworkTimeMicroseconds);
            writer.WriteUInt32(message.MaximumHorizontalErrorMillimetres);
            writer.WriteUInt32(message.MaximumVerticalErrorMillimetres);
            writer.WriteUInt16(message.MaximumYawErrorCentidegrees);
            writer.WriteByte(message.Count);
            writer.WriteByte(message.StableSamples);
        }

        private static bool TryValidateNonzeroDigest(byte[] value)
        {
            return value != null && value.Length == DigestBytes && !IsAllZero(value);
        }

        internal static byte[] ComputePrepareBeginDigest(PeerPrepareBegin message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            return HashCanonical(writer =>
            {
                writer.WriteDomain("operator-prepare-begin-v1\0");
                writer.WriteBytes(message.Header.GenerationKey);
                writer.WriteBytes(message.SceneContractDigest);
                writer.WriteBytes(message.PopulationDigest);
                writer.WriteBytes(message.PopulationIdentityDigest);
                writer.WriteBytes(message.GroundedPlayerSetDigest);
                writer.WriteBytes(message.PlayerPlacementDigest);
                writer.WriteBytes(message.RuntimeOwnerManifestDigest);
                writer.WriteBytes(message.RuntimeReadySetDigest);
                writer.WriteUInt32(message.RuntimeOwnerNetId);
                writer.WriteByte(message.ParticipantCount);
                writer.WriteByte(message.EnemyCount);
                writer.WriteUInt16(message.Flags);
            });
        }

        internal static byte[] EncodePrepareBeginForFrozenRuntime(
            PeerPrepareBegin message,
            PeerBarrierExpectation expectation,
            byte[] expectedRuntimeOwnerManifestDigest,
            byte[] expectedRuntimeReadySetDigest)
        {
            if (expectation == null ||
                expectedRuntimeOwnerManifestDigest == null ||
                expectedRuntimeReadySetDigest == null)
            {
                throw new ArgumentNullException(nameof(expectation));
            }
            if (!TryValidatePrepareBegin(
                    message,
                    expectation,
                    expectedRuntimeOwnerManifestDigest,
                    expectedRuntimeReadySetDigest,
                    out string error))
                throw new InvalidDataException(error);
            var writer = new PeerFixedWriter(PrepareBeginBytes);
            WriteHeader(writer, message.Header, PeerBarrierMessageKind.PrepareBegin);
            WritePrepareBeginTailWithoutDigest(writer, message);
            writer.WriteBytes(message.BeginContractDigest);
            return writer.Complete();
        }

        internal static bool TryDecodePrepareBegin(
            byte[] envelope,
            PeerBarrierExpectation expectation,
            byte[] expectedPopulationDigest,
            byte[] expectedPopulationIdentityDigest,
            byte[] expectedGroundedPlayerSetDigest,
            byte[] expectedPlayerPlacementDigest,
            byte[] expectedRuntimeOwnerManifestDigest,
            byte[] expectedRuntimeReadySetDigest,
            out PeerPrepareBegin message,
            out string error)
        {
            message = null;
            error = string.Empty;
            try
            {
                if (expectation == null ||
                    expectedRuntimeOwnerManifestDigest == null ||
                    expectedRuntimeReadySetDigest == null)
                {
                    throw new InvalidDataException(
                        "PrepareBegin has no frozen runtime expectation");
                }
                if (envelope == null || envelope.Length != PrepareBeginBytes)
                    throw new InvalidDataException(
                        "PrepareBegin must contain exactly 365 bytes");
                var reader = new PeerFixedReader(envelope);
                var candidate = new PeerPrepareBegin(
                    ReadHeader(reader, PeerBarrierMessageKind.PrepareBegin),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadUInt32(),
                    reader.ReadByte(),
                    reader.ReadByte(),
                    reader.ReadUInt16(),
                    reader.ReadBytes(DigestBytes));
                reader.RequireComplete();
                if (!TryValidatePrepareBegin(
                        candidate,
                        expectation,
                        expectedRuntimeOwnerManifestDigest,
                        expectedRuntimeReadySetDigest,
                        out error) ||
                    !OptionalDigestEquals(
                        candidate.PopulationDigest,
                        expectedPopulationDigest) ||
                    !OptionalDigestEquals(
                        candidate.PopulationIdentityDigest,
                        expectedPopulationIdentityDigest) ||
                    !OptionalDigestEquals(
                        candidate.GroundedPlayerSetDigest,
                        expectedGroundedPlayerSetDigest) ||
                    !OptionalDigestEquals(
                        candidate.PlayerPlacementDigest,
                        expectedPlayerPlacementDigest))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "PrepareBegin does not match frozen barriers";
                    return false;
                }
                message = candidate;
                return true;
            }
            catch (Exception ex) when (ex is InvalidDataException ||
                ex is ArgumentException || ex is OverflowException)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryValidatePrepareBegin(
            PeerPrepareBegin message,
            PeerBarrierExpectation expectation,
            byte[] expectedRuntimeOwnerManifestDigest,
            byte[] expectedRuntimeReadySetDigest,
            out string error)
        {
            error = string.Empty;
            bool isPve = message != null && message.Flags == PveBeginFlags;
            bool populationValid = message != null && (isPve
                ? TryValidateNonzeroDigest(message.PopulationDigest) &&
                  TryValidateNonzeroDigest(message.PopulationIdentityDigest) &&
                  message.EnemyCount >= 1 &&
                  message.EnemyCount <= MaximumPopulation
                : message.Flags == PvpBeginFlags &&
                  IsZeroDigest(message.PopulationDigest) &&
                  IsZeroDigest(message.PopulationIdentityDigest) &&
                  message.EnemyCount == 0);
            if (message == null ||
                !TryValidateHeader(
                    message.Header,
                    PeerBarrierMessageKind.PrepareBegin,
                    out error) ||
                !TryValidateNonzeroDigest(message.SceneContractDigest) ||
                !populationValid ||
                !TryValidateNonzeroDigest(message.GroundedPlayerSetDigest) ||
                !TryValidateNonzeroDigest(message.PlayerPlacementDigest) ||
                !TryValidateNonzeroDigest(message.RuntimeOwnerManifestDigest) ||
                !TryValidateNonzeroDigest(message.RuntimeReadySetDigest) ||
                message.RuntimeOwnerNetId == 0 ||
                message.ParticipantCount < 1 ||
                message.ParticipantCount > MaximumParticipants ||
                !DigestEquals(
                    message.BeginContractDigest,
                    ComputePrepareBeginDigest(message)))
            {
                if (string.IsNullOrEmpty(error))
                    error = "PrepareBegin is noncanonical";
                return false;
            }
            byte[] generation = ComputeGenerationKey(
                message.Header.SessionNonce,
                message.Header.IdentityDigest,
                message.Header.SceneGenerationEpoch,
                message.SceneContractDigest,
                message.EnemyCount);
            if (!DigestEquals(generation, message.Header.GenerationKey))
            {
                error = "PrepareBegin generation key is invalid";
                return false;
            }
            if (expectation != null &&
                (expectation.IsPve != isPve ||
                 message.ParticipantCount != expectation.ParticipantCount ||
                 message.EnemyCount != expectation.RequestedEnemies ||
                 !TryValidateExpectedHeader(message.Header, expectation, out error) ||
                 !DigestEquals(
                     message.SceneContractDigest,
                     expectation.SceneContractDigest)))
            {
                if (string.IsNullOrEmpty(error))
                    error = "PrepareBegin does not match frozen generation";
                return false;
            }
            if (!OptionalDigestEquals(
                    message.RuntimeOwnerManifestDigest,
                    expectedRuntimeOwnerManifestDigest) ||
                !OptionalDigestEquals(
                    message.RuntimeReadySetDigest,
                    expectedRuntimeReadySetDigest))
            {
                error = "PrepareBegin does not match the frozen runtime ledger";
                return false;
            }
            return true;
        }

        private static void WritePrepareBeginTailWithoutDigest(
            PeerFixedWriter writer,
            PeerPrepareBegin message)
        {
            writer.WriteBytes(message.SceneContractDigest);
            writer.WriteBytes(message.PopulationDigest);
            writer.WriteBytes(message.PopulationIdentityDigest);
            writer.WriteBytes(message.GroundedPlayerSetDigest);
            writer.WriteBytes(message.PlayerPlacementDigest);
            writer.WriteBytes(message.RuntimeOwnerManifestDigest);
            writer.WriteBytes(message.RuntimeReadySetDigest);
            writer.WriteUInt32(message.RuntimeOwnerNetId);
            writer.WriteByte(message.ParticipantCount);
            writer.WriteByte(message.EnemyCount);
            writer.WriteUInt16(message.Flags);
        }

        internal static byte[] ComputeBeginReadyReceiptDigest(
            PeerBeginReady message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            var prefix = new PeerFixedWriter(BeginReadyBytes - DigestBytes);
            WriteHeader(prefix, message.Header, PeerBarrierMessageKind.BeginReady);
            WriteBeginReadyTailWithoutDigest(prefix, message);
            byte[] semantic = prefix.Complete();
            return HashCanonical(writer =>
            {
                writer.WriteDomain("operator-begin-ready-v1\0");
                writer.WriteBytes(semantic);
            });
        }

        internal static byte[] EncodeBeginReadyForFrozenRuntime(
            PeerBeginReady message,
            PeerBarrierExpectation expectation,
            PeerPrepareBegin expectedPrepare)
        {
            if (expectation == null || expectedPrepare == null)
                throw new ArgumentNullException(nameof(expectation));
            if (!TryValidateBeginReady(
                    message,
                    expectation,
                    expectedPrepare,
                    out string error))
                throw new InvalidDataException(error);
            var writer = new PeerFixedWriter(BeginReadyBytes);
            WriteHeader(writer, message.Header, PeerBarrierMessageKind.BeginReady);
            WriteBeginReadyTailWithoutDigest(writer, message);
            writer.WriteBytes(message.ReceiptDigest);
            return writer.Complete();
        }

        internal static bool TryDecodeBeginReady(
            byte[] envelope,
            PeerBarrierExpectation expectation,
            PeerPrepareBegin expectedPrepare,
            out PeerBeginReady message,
            out string error)
        {
            message = null;
            error = string.Empty;
            try
            {
                if (expectation == null || expectedPrepare == null)
                {
                    throw new InvalidDataException(
                        "BeginReady has no frozen PrepareBegin expectation");
                }
                if (envelope == null || envelope.Length != BeginReadyBytes)
                    throw new InvalidDataException(
                        "BeginReady must contain exactly 369 bytes");
                var reader = new PeerFixedReader(envelope);
                PeerBarrierHeader header = ReadHeader(
                    reader,
                    PeerBarrierMessageKind.BeginReady);
                byte[] begin = reader.ReadBytes(DigestBytes);
                byte[] scene = reader.ReadBytes(DigestBytes);
                byte[] population = reader.ReadBytes(DigestBytes);
                byte[] grounded = reader.ReadBytes(DigestBytes);
                byte[] placement = reader.ReadBytes(DigestBytes);
                byte[] runtimeManifest = reader.ReadBytes(DigestBytes);
                byte[] runtimeReadySet = reader.ReadBytes(DigestBytes);
                uint owner = reader.ReadUInt32();
                byte participants = reader.ReadByte();
                byte enemies = reader.ReadByte();
                ushort flags = reader.ReadUInt16();
                byte samples = reader.ReadByte();
                if (reader.ReadByte() != 0 || reader.ReadByte() != 0 ||
                    reader.ReadByte() != 0)
                {
                    throw new InvalidDataException(
                        "BeginReady reserved bytes are nonzero");
                }
                var candidate = new PeerBeginReady(
                    header,
                    begin,
                    scene,
                    population,
                    grounded,
                    placement,
                    runtimeManifest,
                    runtimeReadySet,
                    owner,
                    participants,
                    enemies,
                    flags,
                    samples,
                    reader.ReadBytes(DigestBytes));
                reader.RequireComplete();
                if (!TryValidateBeginReady(
                        candidate,
                        expectation,
                        expectedPrepare,
                        out error))
                {
                    return false;
                }
                message = candidate;
                return true;
            }
            catch (Exception ex) when (ex is InvalidDataException ||
                ex is ArgumentException || ex is OverflowException)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryValidateBeginReady(
            PeerBeginReady message,
            PeerBarrierExpectation expectation,
            PeerPrepareBegin expectedPrepare,
            out string error)
        {
            error = string.Empty;
            bool isPve = message != null && message.Flags == PveBeginFlags;
            bool populationValid = message != null && (isPve
                ? TryValidateNonzeroDigest(message.PopulationDigest) &&
                  message.EnemyCount >= 1 &&
                  message.EnemyCount <= MaximumPopulation
                : message.Flags == PvpBeginFlags &&
                  IsZeroDigest(message.PopulationDigest) &&
                  message.EnemyCount == 0);
            if (message == null ||
                !TryValidateHeader(
                    message.Header,
                    PeerBarrierMessageKind.BeginReady,
                    out error) ||
                !TryValidateNonzeroDigest(message.BeginContractDigest) ||
                !TryValidateNonzeroDigest(message.SceneContractDigest) ||
                !populationValid ||
                !TryValidateNonzeroDigest(message.GroundedPlayerSetDigest) ||
                !TryValidateNonzeroDigest(message.PlayerPlacementDigest) ||
                !TryValidateNonzeroDigest(message.RuntimeOwnerManifestDigest) ||
                !TryValidateNonzeroDigest(message.RuntimeReadySetDigest) ||
                message.RuntimeOwnerNetId == 0 ||
                message.ParticipantCount < 1 ||
                message.ParticipantCount > MaximumParticipants ||
                message.GroundedStableSamples < 30 ||
                !DigestEquals(
                    message.ReceiptDigest,
                    ComputeBeginReadyReceiptDigest(message)))
            {
                if (string.IsNullOrEmpty(error))
                    error = "BeginReady is noncanonical";
                return false;
            }
            byte[] generation = ComputeGenerationKey(
                message.Header.SessionNonce,
                message.Header.IdentityDigest,
                message.Header.SceneGenerationEpoch,
                message.SceneContractDigest,
                message.EnemyCount);
            if (!DigestEquals(generation, message.Header.GenerationKey))
            {
                error = "BeginReady generation key is invalid";
                return false;
            }
            if (expectation != null &&
                (expectation.IsPve != isPve ||
                 message.ParticipantCount != expectation.ParticipantCount ||
                 message.EnemyCount != expectation.RequestedEnemies ||
                 !TryValidateExpectedHeader(message.Header, expectation, out error) ||
                 !DigestEquals(
                     message.SceneContractDigest,
                     expectation.SceneContractDigest)))
            {
                if (string.IsNullOrEmpty(error))
                    error = "BeginReady does not match frozen generation";
                return false;
            }
            if (expectedPrepare != null &&
                (!DigestEquals(
                     message.BeginContractDigest,
                     expectedPrepare.BeginContractDigest) ||
                 !DigestEquals(
                     message.SceneContractDigest,
                     expectedPrepare.SceneContractDigest) ||
                 !DigestEquals(
                     message.PopulationDigest,
                     expectedPrepare.PopulationDigest) ||
                 !DigestEquals(
                     message.GroundedPlayerSetDigest,
                     expectedPrepare.GroundedPlayerSetDigest) ||
                  !DigestEquals(
                      message.PlayerPlacementDigest,
                      expectedPrepare.PlayerPlacementDigest) ||
                  !DigestEquals(
                      message.RuntimeOwnerManifestDigest,
                      expectedPrepare.RuntimeOwnerManifestDigest) ||
                  !DigestEquals(
                      message.RuntimeReadySetDigest,
                      expectedPrepare.RuntimeReadySetDigest) ||
                 message.RuntimeOwnerNetId != expectedPrepare.RuntimeOwnerNetId ||
                 message.ParticipantCount != expectedPrepare.ParticipantCount ||
                 message.EnemyCount != expectedPrepare.EnemyCount ||
                 message.Flags != expectedPrepare.Flags))
            {
                error = "BeginReady does not echo the immutable PrepareBegin";
                return false;
            }
            return true;
        }

        private static void WriteBeginReadyTailWithoutDigest(
            PeerFixedWriter writer,
            PeerBeginReady message)
        {
            writer.WriteBytes(message.BeginContractDigest);
            writer.WriteBytes(message.SceneContractDigest);
            writer.WriteBytes(message.PopulationDigest);
            writer.WriteBytes(message.GroundedPlayerSetDigest);
            writer.WriteBytes(message.PlayerPlacementDigest);
            writer.WriteBytes(message.RuntimeOwnerManifestDigest);
            writer.WriteBytes(message.RuntimeReadySetDigest);
            writer.WriteUInt32(message.RuntimeOwnerNetId);
            writer.WriteByte(message.ParticipantCount);
            writer.WriteByte(message.EnemyCount);
            writer.WriteUInt16(message.Flags);
            writer.WriteByte(message.GroundedStableSamples);
            writer.WriteByte(0);
            writer.WriteByte(0);
            writer.WriteByte(0);
        }

        internal static byte[] ComputeBeginReadySetDigest(
            byte[] generationKey,
            IReadOnlyList<PeerSlotReceiptDigest> receipts,
            int participantCount)
        {
            byte[] generation = CloneDigest(generationKey, nameof(generationKey));
            if (participantCount < 1 || participantCount > MaximumParticipants ||
                receipts == null || receipts.Count != participantCount - 1)
            {
                throw new InvalidDataException(
                    "BeginReady receipt set count is invalid");
            }
            int previous = 0;
            for (int index = 0; index < receipts.Count; index++)
            {
                PeerSlotReceiptDigest receipt = receipts[index];
                if (receipt.Slot <= previous || receipt.Slot >= participantCount ||
                    !TryValidateNonzeroDigest(receipt.ReceiptDigest))
                {
                    throw new InvalidDataException(
                        "BeginReady receipt set is not strictly slot ordered");
                }
                previous = receipt.Slot;
            }
            return HashCanonical(writer =>
            {
                writer.WriteDomain("operator-begin-ready-set-v1\0");
                writer.WriteBytes(generation);
                writer.WriteByte(checked((byte)participantCount));
                for (int index = 0; index < receipts.Count; index++)
                {
                    writer.WriteInt32(receipts[index].Slot);
                    writer.WriteBytes(receipts[index].ReceiptDigest);
                }
            });
        }

        internal static byte[] ComputeBeginCommitDigest(PeerBeginCommit message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            return HashCanonical(writer =>
            {
                writer.WriteDomain("operator-begin-commit-v1\0");
                writer.WriteBytes(message.Header.GenerationKey);
                writer.WriteBytes(message.BeginContractDigest);
                writer.WriteBytes(message.ReadySetDigest);
                writer.WriteByte(message.RemoteReadyCount);
                writer.WriteByte(message.ParticipantCount);
                writer.WriteUInt16(message.Flags);
            });
        }

        internal static byte[] EncodeBeginCommitForFrozenReadySet(
            PeerBeginCommit message,
            PeerBarrierExpectation expectation,
            PeerPrepareBegin expectedPrepare,
            byte[] expectedReadySetDigest)
        {
            if (expectation == null || expectedPrepare == null ||
                expectedReadySetDigest == null)
            {
                throw new ArgumentNullException(nameof(expectation));
            }
            if (!TryValidateBeginCommit(
                    message,
                    expectation,
                    expectedPrepare,
                    expectedReadySetDigest,
                    out string error))
            {
                throw new InvalidDataException(error);
            }
            var writer = new PeerFixedWriter(233);
            WriteHeader(writer, message.Header, PeerBarrierMessageKind.BeginCommit);
            writer.WriteBytes(message.BeginContractDigest);
            writer.WriteBytes(message.PopulationDigest);
            writer.WriteBytes(message.ReadySetDigest);
            writer.WriteBytes(message.CommitDigest);
            writer.WriteByte(message.RemoteReadyCount);
            writer.WriteByte(message.ParticipantCount);
            writer.WriteUInt16(message.Flags);
            return writer.Complete();
        }

        internal static bool TryDecodeBeginCommit(
            byte[] envelope,
            PeerBarrierExpectation expectation,
            PeerPrepareBegin expectedPrepare,
            byte[] expectedReadySetDigest,
            out PeerBeginCommit message,
            out string error)
        {
            message = null;
            error = string.Empty;
            try
            {
                if (expectation == null || expectedPrepare == null ||
                    expectedReadySetDigest == null)
                {
                    throw new InvalidDataException(
                        "BeginCommit has no frozen Prepare/ready-set expectation");
                }
                if (envelope == null || envelope.Length != 233)
                    throw new InvalidDataException(
                        "BeginCommit must contain exactly 233 bytes");
                var reader = new PeerFixedReader(envelope);
                var candidate = new PeerBeginCommit(
                    ReadHeader(reader, PeerBarrierMessageKind.BeginCommit),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadByte(),
                    reader.ReadByte(),
                    reader.ReadUInt16());
                reader.RequireComplete();
                if (!TryValidateBeginCommit(
                        candidate,
                        expectation,
                        expectedPrepare,
                        expectedReadySetDigest,
                        out error))
                {
                    return false;
                }
                message = candidate;
                return true;
            }
            catch (Exception ex) when (ex is InvalidDataException ||
                ex is ArgumentException || ex is OverflowException)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryValidateBeginCommit(
            PeerBeginCommit message,
            PeerBarrierExpectation expectation,
            PeerPrepareBegin expectedPrepare,
            byte[] expectedReadySetDigest,
            out string error)
        {
            error = string.Empty;
            bool isPve = message != null && message.Flags == PveBeginFlags;
            bool populationValid = message != null && (isPve
                ? TryValidateNonzeroDigest(message.PopulationDigest)
                : message.Flags == PvpBeginFlags &&
                  IsZeroDigest(message.PopulationDigest));
            if (message == null ||
                !TryValidateHeader(
                    message.Header,
                    PeerBarrierMessageKind.BeginCommit,
                    out error) ||
                !TryValidateNonzeroDigest(message.BeginContractDigest) ||
                !populationValid ||
                !TryValidateNonzeroDigest(message.ReadySetDigest) ||
                message.ParticipantCount < 1 ||
                message.ParticipantCount > MaximumParticipants ||
                message.RemoteReadyCount != message.ParticipantCount - 1 ||
                !DigestEquals(
                    message.CommitDigest,
                    ComputeBeginCommitDigest(message)))
            {
                if (string.IsNullOrEmpty(error))
                    error = "BeginCommit is noncanonical";
                return false;
            }
            if (expectation != null &&
                (expectation.IsPve != isPve ||
                 message.ParticipantCount != expectation.ParticipantCount ||
                 !TryValidateExpectedHeader(message.Header, expectation, out error)))
            {
                if (string.IsNullOrEmpty(error))
                    error = "BeginCommit does not match frozen generation";
                return false;
            }
            if (expectedPrepare != null &&
                (!DigestEquals(
                     message.BeginContractDigest,
                     expectedPrepare.BeginContractDigest) ||
                 !DigestEquals(
                     message.PopulationDigest,
                     expectedPrepare.PopulationDigest) ||
                 message.ParticipantCount != expectedPrepare.ParticipantCount ||
                 message.Flags != expectedPrepare.Flags))
            {
                error = "BeginCommit does not match immutable PrepareBegin";
                return false;
            }
            if (expectedReadySetDigest != null &&
                !DigestEquals(message.ReadySetDigest, expectedReadySetDigest))
            {
                error = "BeginCommit ready-set digest is invalid";
                return false;
            }
            return true;
        }

        private static bool OptionalDigestEquals(byte[] observed, byte[] expected)
        {
            return expected == null || DigestEquals(observed, expected);
        }

        private static bool IsZeroDigest(byte[] value)
        {
            return value != null && value.Length == DigestBytes && IsAllZero(value);
        }

        internal static byte[] ComputePlayerSetDigest(
            byte[] generationKey,
            PeerPlacementRoute route,
            IReadOnlyList<PeerPlayerAssignmentRecord> records)
        {
            byte[] generation = CloneDigest(generationKey, nameof(generationKey));
            bool isPve = route == PeerPlacementRoute.PveOwnerRpc;
            if (!TryValidatePlayerAssignmentRecords(
                    records,
                    isPve,
                    records?.Count ?? -1,
                    0,
                    int.MinValue,
                    out string error))
            {
                throw new InvalidDataException(error);
            }
            return HashCanonical(writer =>
            {
                writer.WriteDomain("operator-player-set-v1\0");
                writer.WriteBytes(generation);
                writer.WriteByte((byte)route);
                writer.WriteByte(checked((byte)records.Count));
                for (int index = 0; index < records.Count; index++)
                {
                    PeerPlayerAssignmentRecord record = records[index];
                    writer.WriteByte(record.Slot);
                    writer.WriteInt32(record.ConnectionId);
                    writer.WriteUInt32(record.PlayerMasterNetId);
                    writer.WriteUInt32(record.PlayerNetworkingNetId);
                    writer.WriteInt32(record.TeamId);
                }
            });
        }

        internal static byte[] ComputePlayerAssignmentDigest(
            PeerPlacePlayer message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            return HashCanonical(writer =>
            {
                writer.WriteDomain("operator-player-assignment-v1\0");
                writer.WriteBytes(message.Header.GenerationKey);
                writer.WriteBytes(message.SceneContractDigest);
                writer.WriteBytes(message.PlayerSetDigest);
                writer.WriteByte((byte)message.Route);
                writer.WriteByte(checked((byte)message.Records.Count));
                writer.WriteByte(PlayerRecordVersion);
                writer.WriteByte(PlayerRecordBytes);
                writer.WriteUInt16(message.Flags);
                writer.WriteUInt16(message.RequiredOwnerStatusMask);
                for (int index = 0; index < message.Records.Count; index++)
                    WritePlayerAssignmentRecord(writer, message.Records[index]);
            });
        }

        internal static byte[] EncodePlacePlayerForFrozenRoster(
            PeerPlacePlayer message,
            PeerBarrierExpectation expectation)
        {
            if (expectation == null)
                throw new ArgumentNullException(nameof(expectation));
            if (!TryValidatePlacePlayer(
                    message,
                    expectation,
                    out string error,
                    allowLocalConceptual: false))
            {
                throw new InvalidDataException(error);
            }
            return EncodePlacePlayerValidated(message);
        }

        private static byte[] EncodePlacePlayerValidated(PeerPlacePlayer message)
        {
            int length = checked(
                CommonHeaderBytes + 104 +
                message.Records.Count * PlayerRecordBytes);
            var writer = new PeerFixedWriter(length);
            WriteHeader(writer, message.Header, PeerBarrierMessageKind.PlacePlayer);
            writer.WriteBytes(message.SceneContractDigest);
            writer.WriteBytes(message.PlayerSetDigest);
            writer.WriteBytes(message.AssignmentDigest);
            writer.WriteByte((byte)message.Route);
            writer.WriteByte(checked((byte)message.Records.Count));
            writer.WriteByte(PlayerRecordVersion);
            writer.WriteByte(PlayerRecordBytes);
            writer.WriteUInt16(message.Flags);
            writer.WriteUInt16(message.RequiredOwnerStatusMask);
            for (int index = 0; index < message.Records.Count; index++)
                WritePlayerAssignmentRecord(writer, message.Records[index]);
            return writer.Complete();
        }

        internal static bool TryDecodePlacePlayer(
            byte[] envelope,
            PeerBarrierExpectation expectation,
            out PeerPlacePlayer message,
            out string error)
        {
            message = null;
            error = string.Empty;
            try
            {
                if (expectation == null)
                    throw new InvalidDataException(
                        "PlacePlayer has no frozen roster expectation");
                var reader = new PeerFixedReader(envelope);
                PeerBarrierHeader header = ReadHeader(
                    reader,
                    PeerBarrierMessageKind.PlacePlayer);
                byte[] scene = reader.ReadBytes(DigestBytes);
                byte[] playerSet = reader.ReadBytes(DigestBytes);
                byte[] assignment = reader.ReadBytes(DigestBytes);
                PeerPlacementRoute route = (PeerPlacementRoute)reader.ReadByte();
                int count = reader.ReadByte();
                if (reader.ReadByte() != PlayerRecordVersion ||
                    reader.ReadByte() != PlayerRecordBytes)
                {
                    throw new InvalidDataException(
                        "PlacePlayer record version/size is invalid");
                }
                ushort flags = reader.ReadUInt16();
                ushort ownerMask = reader.ReadUInt16();
                if (count < 1 || count > MaximumParticipants ||
                    reader.Remaining != checked(count * PlayerRecordBytes))
                {
                    throw new InvalidDataException(
                        "PlacePlayer count/length is invalid");
                }
                var records = new PeerPlayerAssignmentRecord[count];
                for (int index = 0; index < count; index++)
                    records[index] = ReadPlayerAssignmentRecord(reader);
                reader.RequireComplete();
                var candidate = new PeerPlacePlayer(
                    header,
                    scene,
                    playerSet,
                    assignment,
                    route,
                    flags,
                    ownerMask,
                    records);
                if (!TryValidatePlacePlayer(
                        candidate,
                        expectation,
                        out error,
                        allowLocalConceptual: false))
                    return false;
                message = candidate;
                return true;
            }
            catch (Exception ex) when (ex is InvalidDataException ||
                ex is ArgumentException || ex is OverflowException)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryValidatePlacePlayer(
            PeerPlacePlayer message,
            PeerBarrierExpectation expectation,
            out string error,
            bool allowLocalConceptual)
        {
            error = string.Empty;
            bool isPve = message != null &&
                message.Route == PeerPlacementRoute.PveOwnerRpc;
            ushort expectedFlags = isPve ? PvePlaceFlags : PvpPlaceFlags;
            if (message == null ||
                !TryValidateHeader(
                    message.Header,
                    PeerBarrierMessageKind.PlacePlayer,
                    out error,
                    allowLocalConceptual) ||
                (message.Route != PeerPlacementRoute.PveOwnerRpc &&
                 message.Route != PeerPlacementRoute.PvpNativeObserve) ||
                message.Flags != expectedFlags ||
                message.RequiredOwnerStatusMask != PlayerOwnerStatusMask ||
                !TryValidateNonzeroDigest(message.SceneContractDigest) ||
                !TryValidateNonzeroDigest(message.PlayerSetDigest) ||
                !TryValidateNonzeroDigest(message.AssignmentDigest) ||
                !TryValidatePlayerAssignmentRecords(
                    message.Records,
                    isPve,
                    expectation?.ParticipantCount ?? message.Records.Count,
                    expectation?.MaximumMarkerOrdinalExclusive ?? 0,
                    expectation?.ExpectedPvePlayerTeamId ?? int.MinValue,
                    out error) ||
                message.Header.RecipientSlot >= message.Records.Count ||
                !DigestEquals(
                    message.PlayerSetDigest,
                    ComputePlayerSetDigest(
                        message.Header.GenerationKey,
                        message.Route,
                        message.Records)) ||
                !DigestEquals(
                    message.AssignmentDigest,
                    ComputePlayerAssignmentDigest(message)))
            {
                if (string.IsNullOrEmpty(error))
                    error = "PlacePlayer is noncanonical";
                return false;
            }
            if (expectation != null &&
                (expectation.IsPve != isPve ||
                 expectation.ExpectedConnectionIdsBySlot == null ||
                 !TryValidateExpectedHeader(
                     message.Header,
                     expectation,
                     out error,
                     allowLocalConceptual) ||
                 !DigestEquals(
                     message.SceneContractDigest,
                     expectation.SceneContractDigest)))
            {
                if (string.IsNullOrEmpty(error))
                    error = "PlacePlayer does not match frozen generation";
                return false;
            }
            if (expectation != null)
            {
                for (int slot = 0; slot < message.Records.Count; slot++)
                {
                    if (message.Records[slot].ConnectionId !=
                        expectation.ExpectedConnectionIdsBySlot[slot])
                    {
                        error = "PlacePlayer connection roster differs at slot " +
                            slot;
                        return false;
                    }
                }
            }
            return true;
        }

        internal static bool TryValidateLocalPlacePlayerAssignment(
            PeerPlacePlayer message,
            PeerBarrierExpectation expectation,
            out string error)
        {
            if (message?.Header == null || message.Header.SenderSlot != 0 ||
                message.Header.RecipientSlot != 0 || expectation?.Header == null ||
                expectation.Header.SenderSlot != 0 ||
                expectation.Header.RecipientSlot != 0 ||
                message.Records.Count != 1 || expectation.ParticipantCount != 1)
            {
                error = "local PlacePlayer assignment is not bound to slot zero";
                return false;
            }
            return TryValidatePlacePlayer(
                message,
                expectation,
                out error,
                allowLocalConceptual: true);
        }

        private static bool TryValidatePlayerAssignmentRecords(
            IReadOnlyList<PeerPlayerAssignmentRecord> records,
            bool isPve,
            int expectedCount,
            int maximumMarkerOrdinalExclusive,
            int expectedPvePlayerTeamId,
            out string error)
        {
            error = string.Empty;
            if (records == null || records.Count < 1 ||
                records.Count > MaximumParticipants ||
                records.Count != expectedCount ||
                maximumMarkerOrdinalExclusive < 0 ||
                maximumMarkerOrdinalExclusive > ushort.MaxValue + 1)
            {
                error = "player assignment record count/bounds are invalid";
                return false;
            }
            var netIds = new HashSet<uint>();
            var markerOrdinals = new HashSet<ushort>();
            var markerDigests = new HashSet<string>(StringComparer.Ordinal);
            var connectionIds = new HashSet<int>();
            for (int index = 0; index < records.Count; index++)
            {
                PeerPlayerAssignmentRecord record = records[index];
                string markerKey = record.MarkerDigest == null
                    ? string.Empty
                    : Convert.ToHexString(record.MarkerDigest);
                bool teamValid = isPve
                    ? record.TeamId != -1 &&
                      (expectedPvePlayerTeamId == int.MinValue ||
                       record.TeamId == expectedPvePlayerTeamId)
                    : record.TeamId == 1 || record.TeamId == 2;
                if (record.Slot != index ||
                    !connectionIds.Add(record.ConnectionId) ||
                    record.CapsuleDirection > 2 ||
                    record.PhysicalFlags != PlayerPhysicalFlags ||
                    record.PlayerMasterNetId == 0 ||
                    record.PlayerNetworkingNetId == 0 ||
                    record.PlayerMasterNetId == record.PlayerNetworkingNetId ||
                    !netIds.Add(record.PlayerMasterNetId) ||
                    !netIds.Add(record.PlayerNetworkingNetId) ||
                    !teamValid ||
                    !markerOrdinals.Add(record.MarkerOrdinal) ||
                    (maximumMarkerOrdinalExclusive > 0 &&
                     record.MarkerOrdinal >= maximumMarkerOrdinalExclusive) ||
                    !TryValidateNonzeroDigest(record.MarkerDigest) ||
                    !markerDigests.Add(markerKey) ||
                    record.YawCentidegrees >= 36000 ||
                    record.CapsuleRadiusMillimetres == 0 ||
                    record.CapsuleHeightMillimetres <
                        record.CapsuleRadiusMillimetres * 2 ||
                    record.GroundLayer < 0 || record.GroundLayer > 31)
                {
                    error = "player assignment record " + index +
                        " is noncanonical";
                    return false;
                }
            }
            return true;
        }

        private static void WritePlayerAssignmentRecord(
            PeerFixedWriter writer,
            PeerPlayerAssignmentRecord record)
        {
            writer.WriteByte(record.Slot);
            writer.WriteByte(record.CapsuleDirection);
            writer.WriteUInt16(record.PhysicalFlags);
            writer.WriteInt32(record.ConnectionId);
            writer.WriteUInt32(record.PlayerMasterNetId);
            writer.WriteUInt32(record.PlayerNetworkingNetId);
            writer.WriteInt32(record.TeamId);
            writer.WriteUInt16(record.MarkerOrdinal);
            writer.WriteUInt16(0);
            writer.WriteBytes(record.MarkerDigest);
            writer.WriteInt32(record.PositionXMillimetres);
            writer.WriteInt32(record.PositionYMillimetres);
            writer.WriteInt32(record.PositionZMillimetres);
            writer.WriteUInt16(record.YawCentidegrees);
            writer.WriteUInt16(record.CapsuleRadiusMillimetres);
            writer.WriteUInt16(record.CapsuleHeightMillimetres);
            writer.WriteInt16(record.CapsuleCenterXMillimetres);
            writer.WriteInt16(record.CapsuleCenterYMillimetres);
            writer.WriteInt16(record.CapsuleCenterZMillimetres);
            writer.WriteInt16(record.GroundLayer);
            writer.WriteUInt16(0);
        }

        private static void WritePlayerAssignmentRecord(
            PeerCanonicalWriter writer,
            PeerPlayerAssignmentRecord record)
        {
            writer.WriteByte(record.Slot);
            writer.WriteByte(record.CapsuleDirection);
            writer.WriteUInt16(record.PhysicalFlags);
            writer.WriteInt32(record.ConnectionId);
            writer.WriteUInt32(record.PlayerMasterNetId);
            writer.WriteUInt32(record.PlayerNetworkingNetId);
            writer.WriteInt32(record.TeamId);
            writer.WriteUInt16(record.MarkerOrdinal);
            writer.WriteUInt16(0);
            writer.WriteBytes(record.MarkerDigest);
            writer.WriteInt32(record.PositionXMillimetres);
            writer.WriteInt32(record.PositionYMillimetres);
            writer.WriteInt32(record.PositionZMillimetres);
            writer.WriteUInt16(record.YawCentidegrees);
            writer.WriteUInt16(record.CapsuleRadiusMillimetres);
            writer.WriteUInt16(record.CapsuleHeightMillimetres);
            writer.WriteInt16(record.CapsuleCenterXMillimetres);
            writer.WriteInt16(record.CapsuleCenterYMillimetres);
            writer.WriteInt16(record.CapsuleCenterZMillimetres);
            writer.WriteInt16(record.GroundLayer);
            writer.WriteUInt16(0);
        }

        private static PeerPlayerAssignmentRecord ReadPlayerAssignmentRecord(
            PeerFixedReader reader)
        {
            byte slot = reader.ReadByte();
            byte direction = reader.ReadByte();
            ushort flags = reader.ReadUInt16();
            int connectionId = reader.ReadInt32();
            uint master = reader.ReadUInt32();
            uint player = reader.ReadUInt32();
            int team = reader.ReadInt32();
            ushort ordinal = reader.ReadUInt16();
            if (reader.ReadUInt16() != 0)
                throw new InvalidDataException(
                    "player assignment record reserved word is nonzero");
            byte[] marker = reader.ReadBytes(DigestBytes);
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            int z = reader.ReadInt32();
            ushort yaw = reader.ReadUInt16();
            ushort radius = reader.ReadUInt16();
            ushort height = reader.ReadUInt16();
            short centerX = reader.ReadInt16();
            short centerY = reader.ReadInt16();
            short centerZ = reader.ReadInt16();
            short layer = reader.ReadInt16();
            if (reader.ReadUInt16() != 0)
                throw new InvalidDataException(
                    "player assignment record trailing reserved word is nonzero");
            return new PeerPlayerAssignmentRecord(
                slot,
                direction,
                flags,
                connectionId,
                master,
                player,
                team,
                ordinal,
                marker,
                x,
                y,
                z,
                yaw,
                radius,
                height,
                centerX,
                centerY,
                centerZ,
                layer);
        }

        internal static byte[] ComputeStablePlayerMarkerDigest(
            byte[] packageContentDigest,
            string semanticTransformKey,
            int teamId,
            int positionXMillimetres,
            int positionYMillimetres,
            int positionZMillimetres,
            ushort yawCentidegrees,
            byte[] groundReceiptDigest)
        {
            byte[] content = CloneDigest(
                packageContentDigest,
                nameof(packageContentDigest));
            byte[] ground = CloneDigest(
                groundReceiptDigest,
                nameof(groundReceiptDigest));
            if (string.IsNullOrEmpty(semanticTransformKey) ||
                semanticTransformKey.Length > 1024 ||
                semanticTransformKey.IndexOf('\0') >= 0 ||
                semanticTransformKey.IndexOf('\r') >= 0 ||
                semanticTransformKey.IndexOf('\n') >= 0 ||
                yawCentidegrees >= 36000)
            {
                throw new InvalidDataException(
                    "stable player marker semantics are noncanonical");
            }
            byte[] key = new UTF8Encoding(false, true).GetBytes(
                semanticTransformKey);
            return HashCanonical(writer =>
            {
                writer.WriteDomain("operator-player-marker-stable-v1\0");
                writer.WriteBytes(content);
                writer.WriteUInt32(checked((uint)key.Length));
                writer.WriteBytes(key);
                writer.WriteInt32(teamId);
                writer.WriteInt32(positionXMillimetres);
                writer.WriteInt32(positionYMillimetres);
                writer.WriteInt32(positionZMillimetres);
                writer.WriteUInt16(yawCentidegrees);
                writer.WriteBytes(ground);
            });
        }

        internal static byte[] ComputePlayerReadyReceiptDigest(
            PeerPlayerReady message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            var prefix = new PeerFixedWriter(369);
            WriteHeader(
                prefix,
                message.Header,
                PeerBarrierMessageKind.PlayerReady,
                allowLocalConceptual: true);
            WritePlayerReadyTailWithoutDigest(prefix, message);
            byte[] semantic = prefix.Complete();
            return HashCanonical(writer =>
            {
                writer.WriteDomain("operator-player-ready-v1\0");
                writer.WriteBytes(semantic);
            });
        }

        internal static byte[] EncodePlayerReadyForFrozenAssignment(
            PeerPlayerReady message,
            PeerBarrierExpectation expectation,
            PeerPlacePlayer expectedAssignment)
        {
            if (expectation == null || expectedAssignment == null)
                throw new ArgumentNullException(nameof(expectation));
            if (!TryValidatePlayerReady(
                    message,
                    expectation,
                    expectedAssignment,
                    out string error,
                    allowLocalConceptual: false))
                throw new InvalidDataException(error);
            return EncodePlayerReadyValidated(message);
        }

        private static byte[] EncodePlayerReadyValidated(PeerPlayerReady message)
        {
            var writer = new PeerFixedWriter(401);
            WriteHeader(writer, message.Header, PeerBarrierMessageKind.PlayerReady);
            WritePlayerReadyTailWithoutDigest(writer, message);
            writer.WriteBytes(message.ReceiptDigest);
            return writer.Complete();
        }

        internal static bool TryDecodePlayerReady(
            byte[] envelope,
            PeerBarrierExpectation expectation,
            PeerPlacePlayer expectedAssignment,
            out PeerPlayerReady message,
            out string error)
        {
            message = null;
            error = string.Empty;
            try
            {
                if (expectation == null || expectedAssignment == null)
                {
                    throw new InvalidDataException(
                        "PlayerReady has no frozen assignment expectation");
                }
                if (envelope == null || envelope.Length != 401)
                    throw new InvalidDataException(
                        "PlayerReady must contain exactly 401 bytes");
                var reader = new PeerFixedReader(envelope);
                var candidate = new PeerPlayerReady(
                    ReadHeader(reader, PeerBarrierMessageKind.PlayerReady),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadUInt16(),
                    reader.ReadInt16(),
                    reader.ReadUInt16(),
                    reader.ReadUInt16(),
                    reader.ReadUInt16(),
                    reader.ReadUInt16(),
                    reader.ReadUInt16(),
                    reader.ReadUInt16(),
                    reader.ReadUInt16(),
                    reader.ReadUInt16(),
                    reader.ReadByte(),
                    (PeerPlacementRoute)reader.ReadByte(),
                    reader.ReadUInt16(),
                    reader.ReadBytes(DigestBytes));
                reader.RequireComplete();
                if (!TryValidatePlayerReady(
                        candidate,
                        expectation,
                        expectedAssignment,
                        out error,
                        allowLocalConceptual: false))
                {
                    return false;
                }
                message = candidate;
                return true;
            }
            catch (Exception ex) when (ex is InvalidDataException ||
                ex is ArgumentException || ex is OverflowException)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryValidatePlayerReady(
            PeerPlayerReady message,
            PeerBarrierExpectation expectation,
            PeerPlacePlayer expectedAssignment,
            out string error,
            bool allowLocalConceptual)
        {
            error = string.Empty;
            bool isPve = message != null &&
                message.Route == PeerPlacementRoute.PveOwnerRpc;
            bool routeValid = message != null &&
                (isPve || message.Route == PeerPlacementRoute.PvpNativeObserve);
            bool toleranceValid = message != null && (isPve
                ? message.MaximumHorizontalErrorMillimetres <= 350 &&
                  message.MaximumVerticalErrorMillimetres <= 750 &&
                  message.MaximumYawErrorCentidegrees <= 500
                : message.MaximumHorizontalErrorMillimetres <= 750 &&
                  message.MaximumVerticalErrorMillimetres <= 750 &&
                  message.MaximumYawErrorCentidegrees <= 1000);
            if (message == null ||
                !TryValidateHeader(
                    message.Header,
                    PeerBarrierMessageKind.PlayerReady,
                    out error,
                    allowLocalConceptual) ||
                !routeValid ||
                !TryValidateNonzeroDigest(message.SceneContractDigest) ||
                !TryValidateNonzeroDigest(message.AssignmentDigest) ||
                !TryValidateNonzeroDigest(message.PlayerSetDigest) ||
                !TryValidateNonzeroDigest(message.AssignedMarkerDigest) ||
                !TryValidateNonzeroDigest(message.PreRouteStateDigest) ||
                !TryValidateNonzeroDigest(message.PostRouteStateDigest) ||
                !DigestEquals(
                    message.PreRouteStateDigest,
                    message.PostRouteStateDigest) ||
                !TryValidateNonzeroDigest(message.GroundColliderDigest) ||
                message.PlayerMasterNetId == 0 ||
                message.PlayerNetworkingNetId == 0 ||
                message.PlayerMasterNetId == message.PlayerNetworkingNetId ||
                message.ObservedYawCentidegrees >= 36000 ||
                message.FeetToGroundDeltaMillimetres < -20 ||
                message.FeetToGroundDeltaMillimetres > 100 ||
                !toleranceValid ||
                message.MaximumLinearSpeedMillimetresPerSecond > 150 ||
                message.MaximumVerticalSpeedMillimetresPerSecond > 100 ||
                message.MaximumAngularSpeedCentidegreesPerSecond > 500 ||
                message.GroundSlopeCentidegrees > 6000 ||
                message.MaximumStableRootDriftMillimetres > 100 ||
                message.StableSamples < 30 ||
                message.OwnerStatusMask != PlayerOwnerStatusMask ||
                !DigestEquals(
                    message.ReceiptDigest,
                    ComputePlayerReadyReceiptDigest(message)))
            {
                if (string.IsNullOrEmpty(error))
                    error = "PlayerReady is noncanonical";
                return false;
            }
            if (expectation != null &&
                (expectation.IsPve != isPve ||
                 expectation.ExpectedConnectionIdsBySlot == null ||
                 !TryValidateExpectedHeader(
                     message.Header,
                     expectation,
                     out error,
                     allowLocalConceptual) ||
                 !DigestEquals(
                     message.SceneContractDigest,
                     expectation.SceneContractDigest)))
            {
                if (string.IsNullOrEmpty(error))
                    error = "PlayerReady does not match frozen generation";
                return false;
            }
            if (expectedAssignment != null)
            {
                int slot = message.Header.SenderSlot;
                if (slot < 0 || slot >= expectedAssignment.Records.Count)
                {
                    error = "PlayerReady sender slot is outside the assignment";
                    return false;
                }
                PeerPlayerAssignmentRecord record = expectedAssignment.Records[slot];
                long deltaX = Math.Abs(
                    (long)message.ObservedPositionXMillimetres -
                    record.PositionXMillimetres);
                long deltaZ = Math.Abs(
                    (long)message.ObservedPositionZMillimetres -
                    record.PositionZMillimetres);
                long verticalDelta = Math.Abs(
                    (long)message.ObservedPositionYMillimetres -
                    record.PositionYMillimetres);
                bool horizontalWithinAdvertised =
                    deltaX <= message.MaximumHorizontalErrorMillimetres &&
                    deltaZ <= message.MaximumHorizontalErrorMillimetres &&
                    deltaX * deltaX + deltaZ * deltaZ <=
                        (long)message.MaximumHorizontalErrorMillimetres *
                        message.MaximumHorizontalErrorMillimetres;
                ushort yawDelta = WrappedYawErrorCentidegrees(
                    message.ObservedYawCentidegrees,
                    record.YawCentidegrees);
                if (message.Route != expectedAssignment.Route ||
                    !DigestEquals(
                        message.SceneContractDigest,
                        expectedAssignment.SceneContractDigest) ||
                    !DigestEquals(
                        message.AssignmentDigest,
                        expectedAssignment.AssignmentDigest) ||
                    !DigestEquals(
                        message.PlayerSetDigest,
                        expectedAssignment.PlayerSetDigest) ||
                    !DigestEquals(
                        message.AssignedMarkerDigest,
                        record.MarkerDigest) ||
                    message.PlayerMasterNetId != record.PlayerMasterNetId ||
                    message.PlayerNetworkingNetId !=
                        record.PlayerNetworkingNetId ||
                    expectation == null ||
                    slot >= expectation.ExpectedConnectionIdsBySlot.Count ||
                    record.ConnectionId !=
                        expectation.ExpectedConnectionIdsBySlot[slot] ||
                    !horizontalWithinAdvertised ||
                    verticalDelta > message.MaximumVerticalErrorMillimetres ||
                    yawDelta > message.MaximumYawErrorCentidegrees)
                {
                    error = "PlayerReady does not echo its immutable assignment";
                    return false;
                }
            }
            return true;
        }

        internal static bool TryValidateLocalPlayerReadyReceipt(
            PeerPlayerReady message,
            PeerBarrierExpectation expectation,
            PeerPlacePlayer expectedAssignment,
            out string error)
        {
            if (message?.Header == null || message.Header.SenderSlot != 0 ||
                message.Header.RecipientSlot != 0 || expectation?.Header == null ||
                expectedAssignment == null ||
                expectation.Header.SenderSlot != 0 ||
                expectation.Header.RecipientSlot != 0)
            {
                error = "local PlayerReady receipt is not bound to slot zero";
                return false;
            }
            return TryValidatePlayerReady(
                message,
                expectation,
                expectedAssignment,
                out error,
                allowLocalConceptual: true);
        }

        private static void WritePlayerReadyTailWithoutDigest(
            PeerFixedWriter writer,
            PeerPlayerReady message)
        {
            writer.WriteBytes(message.SceneContractDigest);
            writer.WriteBytes(message.AssignmentDigest);
            writer.WriteBytes(message.PlayerSetDigest);
            writer.WriteBytes(message.AssignedMarkerDigest);
            writer.WriteBytes(message.PreRouteStateDigest);
            writer.WriteBytes(message.PostRouteStateDigest);
            writer.WriteBytes(message.GroundColliderDigest);
            writer.WriteUInt32(message.PlayerMasterNetId);
            writer.WriteUInt32(message.PlayerNetworkingNetId);
            writer.WriteInt32(message.ObservedPositionXMillimetres);
            writer.WriteInt32(message.ObservedPositionYMillimetres);
            writer.WriteInt32(message.ObservedPositionZMillimetres);
            writer.WriteUInt16(message.ObservedYawCentidegrees);
            writer.WriteInt16(message.FeetToGroundDeltaMillimetres);
            writer.WriteUInt16(message.MaximumHorizontalErrorMillimetres);
            writer.WriteUInt16(message.MaximumVerticalErrorMillimetres);
            writer.WriteUInt16(message.MaximumYawErrorCentidegrees);
            writer.WriteUInt16(message.MaximumLinearSpeedMillimetresPerSecond);
            writer.WriteUInt16(message.MaximumVerticalSpeedMillimetresPerSecond);
            writer.WriteUInt16(message.MaximumAngularSpeedCentidegreesPerSecond);
            writer.WriteUInt16(message.GroundSlopeCentidegrees);
            writer.WriteUInt16(message.MaximumStableRootDriftMillimetres);
            writer.WriteByte(message.StableSamples);
            writer.WriteByte((byte)message.Route);
            writer.WriteUInt16(message.OwnerStatusMask);
        }
    }
}
