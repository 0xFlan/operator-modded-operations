using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace OperatorModdedOperations.PeerProtocol
{
    internal enum PeerRuntimeMode : byte
    {
        Pve = 1,
        Pvp = 2
    }

    internal enum PeerRuntimeProofRoute : byte
    {
        HostServerLocalAdoption = 1,
        RemoteCustomHandler = 2
    }

    internal enum PeerRuntimeDigestNode : byte
    {
        ExactNativeTemplateShape = 1,
        PreSceneClone = 2,
        SceneContract = 3,
        GenerationKey = 4,
        RuntimeOwnerContract = 5,
        HostRuntimeProof = 6,
        RuntimeOwnerManifest = 7,
        RuntimeReadyReceipt = 8,
        RuntimeReadySet = 9,
        BeginContract = 10
    }

    internal readonly struct PeerRuntimeLifecycleProof
    {
        internal PeerRuntimeLifecycleProof(
            byte customSpawnHandlerCalls,
            byte manualInitializeNetworkBehavioursCalls,
            byte networkIdentityAwakeCalls,
            byte deserializeClientCalls,
            byte exactOwnerOnStartServerControlledEntries,
            byte exactOwnerOnStartClientControlledEntries,
            byte gameModeInitializeSuccessfulReturns,
            byte prohibitedOriginalOnStartClientBodyRuns,
            uint prematureClockMoveNextEntries,
            ushort nativeClockStartsBeforeBegin,
            ushort reserved)
        {
            CustomSpawnHandlerCalls = customSpawnHandlerCalls;
            ManualInitializeNetworkBehavioursCalls =
                manualInitializeNetworkBehavioursCalls;
            NetworkIdentityAwakeCalls = networkIdentityAwakeCalls;
            DeserializeClientCalls = deserializeClientCalls;
            ExactOwnerOnStartServerControlledEntries =
                exactOwnerOnStartServerControlledEntries;
            ExactOwnerOnStartClientControlledEntries =
                exactOwnerOnStartClientControlledEntries;
            GameModeInitializeSuccessfulReturns =
                gameModeInitializeSuccessfulReturns;
            ProhibitedOriginalOnStartClientBodyRuns =
                prohibitedOriginalOnStartClientBodyRuns;
            PrematureClockMoveNextEntries = prematureClockMoveNextEntries;
            NativeClockStartsBeforeBegin = nativeClockStartsBeforeBegin;
            Reserved = reserved;
        }

        internal byte CustomSpawnHandlerCalls { get; }
        internal byte ManualInitializeNetworkBehavioursCalls { get; }
        internal byte NetworkIdentityAwakeCalls { get; }
        internal byte DeserializeClientCalls { get; }
        internal byte ExactOwnerOnStartServerControlledEntries { get; }
        internal byte ExactOwnerOnStartClientControlledEntries { get; }
        internal byte GameModeInitializeSuccessfulReturns { get; }
        internal byte ProhibitedOriginalOnStartClientBodyRuns { get; }
        internal uint PrematureClockMoveNextEntries { get; }
        internal ushort NativeClockStartsBeforeBegin { get; }
        internal ushort Reserved { get; }
    }

    internal sealed class PeerRuntimeOwnerManifest
    {
        private readonly byte[] sceneContractDigest;
        private readonly byte[] exactNativeTemplateShapeDigest;
        private readonly byte[] preSceneCloneDigest;
        private readonly byte[] runtimeOwnerContractDigest;
        private readonly byte[] hostRuntimeProofDigest;
        private readonly byte[] manifestDigest;

        internal PeerRuntimeOwnerManifest(
            PeerBarrierHeader header,
            ushort runtimeSchema,
            PeerRuntimeMode mode,
            PeerRuntimeProofRoute hostProofRoute,
            uint contractFlags,
            uint requiredCommonStatusMask,
            uint hostObservedStatusMask,
            byte participantCount,
            byte ownerSpawnAttemptOrdinal,
            ushort networkBehaviourCount,
            ushort totalSyncObjectCount,
            uint runtimeOwnerNetId,
            uint deterministicAssetId,
            ulong networkIdentitySceneId,
            ulong hostLocalSceneGeneration,
            PeerRuntimeLifecycleProof lifecycleProof,
            byte[] sceneContractDigest,
            byte[] exactNativeTemplateShapeDigest,
            byte[] preSceneCloneDigest,
            byte[] runtimeOwnerContractDigest,
            byte[] hostRuntimeProofDigest,
            byte[] manifestDigest)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            RuntimeSchema = runtimeSchema;
            Mode = mode;
            HostProofRoute = hostProofRoute;
            ContractFlags = contractFlags;
            RequiredCommonStatusMask = requiredCommonStatusMask;
            HostObservedStatusMask = hostObservedStatusMask;
            ParticipantCount = participantCount;
            OwnerSpawnAttemptOrdinal = ownerSpawnAttemptOrdinal;
            NetworkBehaviourCount = networkBehaviourCount;
            TotalSyncObjectCount = totalSyncObjectCount;
            RuntimeOwnerNetId = runtimeOwnerNetId;
            DeterministicAssetId = deterministicAssetId;
            NetworkIdentitySceneId = networkIdentitySceneId;
            HostLocalSceneGeneration = hostLocalSceneGeneration;
            LifecycleProof = lifecycleProof;
            this.sceneContractDigest = PeerRuntimeBarrierCodec.CloneDigest(
                sceneContractDigest,
                nameof(sceneContractDigest));
            this.exactNativeTemplateShapeDigest = PeerRuntimeBarrierCodec.CloneDigest(
                exactNativeTemplateShapeDigest,
                nameof(exactNativeTemplateShapeDigest));
            this.preSceneCloneDigest = PeerRuntimeBarrierCodec.CloneDigest(
                preSceneCloneDigest,
                nameof(preSceneCloneDigest));
            this.runtimeOwnerContractDigest = PeerRuntimeBarrierCodec.CloneDigest(
                runtimeOwnerContractDigest,
                nameof(runtimeOwnerContractDigest));
            this.hostRuntimeProofDigest = PeerRuntimeBarrierCodec.CloneDigest(
                hostRuntimeProofDigest,
                nameof(hostRuntimeProofDigest));
            this.manifestDigest = PeerRuntimeBarrierCodec.CloneDigest(
                manifestDigest,
                nameof(manifestDigest));
        }

        internal PeerBarrierHeader Header { get; }
        internal ushort RuntimeSchema { get; }
        internal PeerRuntimeMode Mode { get; }
        internal PeerRuntimeProofRoute HostProofRoute { get; }
        internal uint ContractFlags { get; }
        internal uint RequiredCommonStatusMask { get; }
        internal uint HostObservedStatusMask { get; }
        internal byte ParticipantCount { get; }
        internal byte OwnerSpawnAttemptOrdinal { get; }
        internal ushort NetworkBehaviourCount { get; }
        internal ushort TotalSyncObjectCount { get; }
        internal uint RuntimeOwnerNetId { get; }
        internal uint DeterministicAssetId { get; }
        internal ulong NetworkIdentitySceneId { get; }
        internal ulong HostLocalSceneGeneration { get; }
        internal PeerRuntimeLifecycleProof LifecycleProof { get; }
        internal byte[] SceneContractDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(sceneContractDigest);
        internal byte[] ExactNativeTemplateShapeDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(exactNativeTemplateShapeDigest);
        internal byte[] PreSceneCloneDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(preSceneCloneDigest);
        internal byte[] RuntimeOwnerContractDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(runtimeOwnerContractDigest);
        internal byte[] HostRuntimeProofDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(hostRuntimeProofDigest);
        internal byte[] ManifestDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(manifestDigest);
    }

    internal sealed class PeerRuntimeReady
    {
        private readonly byte[] sceneContractDigest;
        private readonly byte[] exactNativeTemplateShapeDigest;
        private readonly byte[] preSceneCloneDigest;
        private readonly byte[] runtimeOwnerContractDigest;
        private readonly byte[] manifestDigest;
        private readonly byte[] receiptDigest;

        internal PeerRuntimeReady(
            PeerBarrierHeader header,
            ushort runtimeSchema,
            PeerRuntimeMode mode,
            PeerRuntimeProofRoute proofRoute,
            uint observedStatusMask,
            byte participantCount,
            byte ownerSpawnAttemptOrdinal,
            ushort networkBehaviourCount,
            ushort totalSyncObjectCount,
            uint runtimeOwnerNetId,
            uint deterministicAssetId,
            ulong networkIdentitySceneId,
            ulong senderLocalSceneGeneration,
            PeerRuntimeLifecycleProof lifecycleProof,
            byte[] sceneContractDigest,
            byte[] exactNativeTemplateShapeDigest,
            byte[] preSceneCloneDigest,
            byte[] runtimeOwnerContractDigest,
            byte[] manifestDigest,
            byte[] receiptDigest)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            RuntimeSchema = runtimeSchema;
            Mode = mode;
            ProofRoute = proofRoute;
            ObservedStatusMask = observedStatusMask;
            ParticipantCount = participantCount;
            OwnerSpawnAttemptOrdinal = ownerSpawnAttemptOrdinal;
            NetworkBehaviourCount = networkBehaviourCount;
            TotalSyncObjectCount = totalSyncObjectCount;
            RuntimeOwnerNetId = runtimeOwnerNetId;
            DeterministicAssetId = deterministicAssetId;
            NetworkIdentitySceneId = networkIdentitySceneId;
            SenderLocalSceneGeneration = senderLocalSceneGeneration;
            LifecycleProof = lifecycleProof;
            this.sceneContractDigest = PeerRuntimeBarrierCodec.CloneDigest(
                sceneContractDigest,
                nameof(sceneContractDigest));
            this.exactNativeTemplateShapeDigest = PeerRuntimeBarrierCodec.CloneDigest(
                exactNativeTemplateShapeDigest,
                nameof(exactNativeTemplateShapeDigest));
            this.preSceneCloneDigest = PeerRuntimeBarrierCodec.CloneDigest(
                preSceneCloneDigest,
                nameof(preSceneCloneDigest));
            this.runtimeOwnerContractDigest = PeerRuntimeBarrierCodec.CloneDigest(
                runtimeOwnerContractDigest,
                nameof(runtimeOwnerContractDigest));
            this.manifestDigest = PeerRuntimeBarrierCodec.CloneDigest(
                manifestDigest,
                nameof(manifestDigest));
            this.receiptDigest = PeerRuntimeBarrierCodec.CloneDigest(
                receiptDigest,
                nameof(receiptDigest));
        }

        internal PeerBarrierHeader Header { get; }
        internal ushort RuntimeSchema { get; }
        internal PeerRuntimeMode Mode { get; }
        internal PeerRuntimeProofRoute ProofRoute { get; }
        internal uint ObservedStatusMask { get; }
        internal byte ParticipantCount { get; }
        internal byte OwnerSpawnAttemptOrdinal { get; }
        internal ushort NetworkBehaviourCount { get; }
        internal ushort TotalSyncObjectCount { get; }
        internal uint RuntimeOwnerNetId { get; }
        internal uint DeterministicAssetId { get; }
        internal ulong NetworkIdentitySceneId { get; }
        internal ulong SenderLocalSceneGeneration { get; }
        internal PeerRuntimeLifecycleProof LifecycleProof { get; }
        internal byte[] SceneContractDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(sceneContractDigest);
        internal byte[] ExactNativeTemplateShapeDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(exactNativeTemplateShapeDigest);
        internal byte[] PreSceneCloneDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(preSceneCloneDigest);
        internal byte[] RuntimeOwnerContractDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(runtimeOwnerContractDigest);
        internal byte[] ManifestDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(manifestDigest);
        internal byte[] ReceiptDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(receiptDigest);
    }

    internal readonly struct PeerRuntimeReadySlotReceipt
    {
        private readonly byte[] receiptDigest;

        internal PeerRuntimeReadySlotReceipt(
            byte slot,
            PeerRuntimeProofRoute route,
            byte[] receiptDigest)
        {
            Slot = slot;
            Route = route;
            this.receiptDigest = PeerRuntimeBarrierCodec.CloneDigest(
                receiptDigest,
                nameof(receiptDigest));
        }

        internal byte Slot { get; }
        internal PeerRuntimeProofRoute Route { get; }
        internal byte[] ReceiptDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(receiptDigest);
    }

    internal sealed class PeerRuntimeReadySet
    {
        private readonly byte[] manifestDigest;
        private readonly byte[] readySetDigest;
        private readonly ReadOnlyCollection<PeerRuntimeReadySlotReceipt> receipts;

        internal PeerRuntimeReadySet(
            byte participantCount,
            byte[] manifestDigest,
            byte[] readySetDigest,
            IEnumerable<PeerRuntimeReadySlotReceipt> receipts)
        {
            ParticipantCount = participantCount;
            this.manifestDigest = PeerRuntimeBarrierCodec.CloneDigest(
                manifestDigest,
                nameof(manifestDigest));
            this.readySetDigest = PeerRuntimeBarrierCodec.CloneDigest(
                readySetDigest,
                nameof(readySetDigest));
            this.receipts = Array.AsReadOnly((receipts ?? throw new ArgumentNullException(
                nameof(receipts))).ToArray());
        }

        internal byte ParticipantCount { get; }
        internal byte[] ManifestDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(manifestDigest);
        internal byte[] ReadySetDigest =>
            PeerRuntimeBarrierCodec.CloneForRead(readySetDigest);
        internal ReadOnlyCollection<PeerRuntimeReadySlotReceipt> Receipts => receipts;
    }

    internal static partial class PeerRuntimeBarrierCodec
    {
        internal const int RuntimeOwnerManifestBytes = 355;
        internal const int RuntimeReadyBytes = 347;
        internal const int RuntimeOwnerManifestSchemaOffset = 101;
        internal const int RuntimeOwnerManifestLifecycleOffset = 147;
        internal const int RuntimeOwnerManifestSceneDigestOffset = 163;
        internal const int RuntimeOwnerManifestHostProofDigestOffset = 291;
        internal const int RuntimeOwnerManifestDigestOffset = 323;
        internal const int RuntimeReadySchemaOffset = 101;
        internal const int RuntimeReadyLifecycleOffset = 139;
        internal const int RuntimeReadySceneDigestOffset = 155;
        internal const int RuntimeReadyManifestDigestOffset = 283;
        internal const int RuntimeReadyReceiptDigestOffset = 315;
        internal const int RuntimeLifecycleProofBytes = 16;
        internal const ushort RuntimeOwnerSchema = 1;
        internal const uint RuntimeOwnerContractFlags = 0x00000FFF;
        internal const uint RuntimeRequiredCommonStatusMask = 0x0003FFFF;
        internal const uint RuntimeHostObservedStatusMask = 0x0007FFFF;
        internal const uint RuntimeRemoteObservedStatusMask = 0x000BFFFF;
        internal const ushort MaximumRuntimeNetworkBehaviours = 64;
        internal const ushort MaximumRuntimeSyncObjects = 256;
        // Td/Cd/Rd are opaque, nonzero upstream semantic digests here. Their
        // bounded native/configuration builders remain intentionally pending.
        internal const bool RuntimeSemanticDigestBuildersComplete = false;

        internal static byte[] ComputeHostRuntimeProofDigest(
            PeerRuntimeOwnerManifest message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            return HashCanonical(writer =>
            {
                writer.WriteDomain("operator-peer-runtime-host-proof-v1\0");
                WriteRuntimeGenerationTuple(writer, message.Header);
                WriteRuntimeOwnerManifestCanonicalPrefix(writer, message);
            });
        }

        internal static byte[] ComputeRuntimeOwnerManifestDigest(
            PeerRuntimeOwnerManifest message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            return HashCanonical(writer =>
            {
                writer.WriteDomain("operator-peer-runtime-owner-manifest-v1\0");
                WriteRuntimeGenerationTuple(writer, message.Header);
                WriteRuntimeOwnerManifestCanonicalPrefix(writer, message);
                writer.WriteBytes(message.HostRuntimeProofDigest);
            });
        }

        internal static byte[] EncodeRuntimeOwnerManifestForFrozenGeneration(
            PeerRuntimeOwnerManifest message,
            PeerBarrierExpectation expectation)
        {
            if (expectation == null)
                throw new ArgumentNullException(nameof(expectation));
            if (!TryValidateRuntimeOwnerManifest(
                    message,
                    expectation,
                    allowLocalConceptual: false,
                    out string error))
            {
                throw new InvalidDataException(error);
            }
            var writer = new PeerFixedWriter(RuntimeOwnerManifestBytes);
            WriteHeader(
                writer,
                message.Header,
                PeerBarrierMessageKind.RuntimeOwnerManifest);
            WriteRuntimeOwnerManifestTail(writer, message);
            return writer.Complete();
        }

        internal static bool TryDecodeRuntimeOwnerManifest(
            byte[] envelope,
            PeerBarrierExpectation expectation,
            out PeerRuntimeOwnerManifest message,
            out string error)
        {
            return TryDecodeRuntimeOwnerManifestCore(
                envelope,
                expectation,
                allowLocalConceptual: false,
                out message,
                out error);
        }

        internal static PeerRuntimeOwnerManifest BuildLocalConceptualRuntimeOwnerManifest(
            PeerRuntimeOwnerManifest semanticSource,
            PeerBarrierExpectation localExpectation)
        {
            if (semanticSource?.Header == null || localExpectation?.Header == null ||
                semanticSource.Header.Kind !=
                    PeerBarrierMessageKind.RuntimeOwnerManifest ||
                semanticSource.ParticipantCount != 1 ||
                localExpectation.Header.Kind !=
                    PeerBarrierMessageKind.RuntimeOwnerManifest ||
                localExpectation.Header.SenderSlot != 0 ||
                localExpectation.Header.RecipientSlot != 0 ||
                localExpectation.ParticipantCount != 1)
            {
                throw new InvalidDataException(
                    "conceptual RuntimeOwnerManifest requires the frozen solo slot");
            }
            var header = new PeerBarrierHeader(
                PeerBarrierMessageKind.RuntimeOwnerManifest,
                0,
                0,
                semanticSource.Header.SessionNonce,
                semanticSource.Header.IdentityDigest,
                semanticSource.Header.SceneGenerationEpoch,
                semanticSource.Header.GenerationKey);
            var result = new PeerRuntimeOwnerManifest(
                header,
                semanticSource.RuntimeSchema,
                semanticSource.Mode,
                semanticSource.HostProofRoute,
                semanticSource.ContractFlags,
                semanticSource.RequiredCommonStatusMask,
                semanticSource.HostObservedStatusMask,
                semanticSource.ParticipantCount,
                semanticSource.OwnerSpawnAttemptOrdinal,
                semanticSource.NetworkBehaviourCount,
                semanticSource.TotalSyncObjectCount,
                semanticSource.RuntimeOwnerNetId,
                semanticSource.DeterministicAssetId,
                semanticSource.NetworkIdentitySceneId,
                semanticSource.HostLocalSceneGeneration,
                semanticSource.LifecycleProof,
                semanticSource.SceneContractDigest,
                semanticSource.ExactNativeTemplateShapeDigest,
                semanticSource.PreSceneCloneDigest,
                semanticSource.RuntimeOwnerContractDigest,
                semanticSource.HostRuntimeProofDigest,
                semanticSource.ManifestDigest);
            if (!TryValidateRuntimeOwnerManifest(
                    result,
                    localExpectation,
                    allowLocalConceptual: true,
                    out string error))
            {
                throw new InvalidDataException(error);
            }
            return result;
        }

        internal static byte[] EncodeLocalConceptualRuntimeOwnerManifest(
            PeerRuntimeOwnerManifest message,
            PeerBarrierExpectation localExpectation)
        {
            if (!TryValidateLocalConceptualRuntimeOwnerManifest(
                    message,
                    localExpectation,
                    out string error))
            {
                throw new InvalidDataException(error);
            }
            var writer = new PeerFixedWriter(RuntimeOwnerManifestBytes);
            WriteHeader(
                writer,
                message.Header,
                PeerBarrierMessageKind.RuntimeOwnerManifest,
                allowLocalConceptual: true);
            WriteRuntimeOwnerManifestTail(writer, message);
            return writer.Complete();
        }

        internal static bool TryDecodeLocalConceptualRuntimeOwnerManifest(
            byte[] envelope,
            PeerBarrierExpectation localExpectation,
            out PeerRuntimeOwnerManifest message,
            out string error)
        {
            if (!TryDecodeRuntimeOwnerManifestCore(
                    envelope,
                    localExpectation,
                    allowLocalConceptual: true,
                    out message,
                    out error))
            {
                return false;
            }
            if (!IsLocalConceptualRuntimeManifest(message) ||
                message.ParticipantCount != 1)
            {
                message = null;
                error =
                    "local RuntimeOwnerManifest envelope is not conceptual solo";
                return false;
            }
            return true;
        }

        internal static bool TryValidateLocalConceptualRuntimeOwnerManifest(
            PeerRuntimeOwnerManifest message,
            PeerBarrierExpectation localExpectation,
            out string error)
        {
            if (!IsLocalConceptualRuntimeManifest(message) ||
                message.ParticipantCount != 1 ||
                localExpectation?.Header == null ||
                localExpectation.Header.SenderSlot != 0 ||
                localExpectation.Header.RecipientSlot != 0 ||
                localExpectation.ParticipantCount != 1)
            {
                error =
                    "local RuntimeOwnerManifest must be the conceptual solo slot";
                return false;
            }
            return TryValidateRuntimeOwnerManifest(
                message,
                localExpectation,
                allowLocalConceptual: true,
                out error);
        }

        private static bool TryDecodeRuntimeOwnerManifestCore(
            byte[] envelope,
            PeerBarrierExpectation expectation,
            bool allowLocalConceptual,
            out PeerRuntimeOwnerManifest message,
            out string error)
        {
            message = null;
            error = string.Empty;
            try
            {
                if (expectation == null)
                    throw new InvalidDataException(
                        "RuntimeOwnerManifest has no frozen expectation");
                if (envelope == null || envelope.Length != RuntimeOwnerManifestBytes)
                {
                    throw new InvalidDataException(
                        "RuntimeOwnerManifest must contain exactly 355 bytes");
                }
                var reader = new PeerFixedReader(envelope);
                PeerBarrierHeader header = ReadHeader(
                    reader,
                    PeerBarrierMessageKind.RuntimeOwnerManifest,
                    allowLocalConceptual);
                var candidate = new PeerRuntimeOwnerManifest(
                    header,
                    reader.ReadUInt16(),
                    (PeerRuntimeMode)reader.ReadByte(),
                    (PeerRuntimeProofRoute)reader.ReadByte(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadByte(),
                    reader.ReadByte(),
                    reader.ReadUInt16(),
                    reader.ReadUInt16(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadUInt64(),
                    reader.ReadUInt64(),
                    ReadRuntimeLifecycleProof(reader),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes));
                reader.RequireComplete();
                if (!TryValidateRuntimeOwnerManifest(
                        candidate,
                        expectation,
                        allowLocalConceptual,
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

        private static bool TryValidateRuntimeOwnerManifest(
            PeerRuntimeOwnerManifest message,
            PeerBarrierExpectation expectation,
            bool allowLocalConceptual,
            out string error)
        {
            if (!TryValidateRuntimeOwnerManifestCore(
                    message,
                    allowLocalConceptual,
                    out error))
                return false;
            if (expectation == null ||
                !TryValidateExpectedHeader(
                    message.Header,
                    expectation,
                    out error,
                    allowLocalConceptual) ||
                expectation.Header.Kind !=
                    PeerBarrierMessageKind.RuntimeOwnerManifest ||
                message.ParticipantCount != expectation.ParticipantCount ||
                message.Mode != (expectation.IsPve
                    ? PeerRuntimeMode.Pve
                    : PeerRuntimeMode.Pvp) ||
                !DigestEquals(
                    message.SceneContractDigest,
                    expectation.SceneContractDigest))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error =
                        "RuntimeOwnerManifest does not match the frozen generation";
                }
                return false;
            }
            return true;
        }

        private static bool TryValidateRuntimeOwnerManifestCore(
            PeerRuntimeOwnerManifest message,
            bool allowLocalConceptual,
            out string error)
        {
            error = string.Empty;
            bool localConceptual = allowLocalConceptual &&
                message?.Header != null &&
                message.Header.SenderSlot == 0 &&
                message.Header.RecipientSlot == 0;
            if (message == null ||
                !TryValidateHeader(
                    message.Header,
                    PeerBarrierMessageKind.RuntimeOwnerManifest,
                    out error,
                    allowLocalConceptual) ||
                (localConceptual
                    ? message.ParticipantCount != 1
                    : message.ParticipantCount < 2 ||
                      message.Header.RecipientSlot < 1 ||
                      message.Header.RecipientSlot >= message.ParticipantCount) ||
                message.RuntimeSchema != RuntimeOwnerSchema ||
                !IsRuntimeMode(message.Mode) ||
                message.HostProofRoute !=
                    PeerRuntimeProofRoute.HostServerLocalAdoption ||
                message.ContractFlags != RuntimeOwnerContractFlags ||
                message.RequiredCommonStatusMask !=
                    RuntimeRequiredCommonStatusMask ||
                message.HostObservedStatusMask != RuntimeHostObservedStatusMask ||
                message.ParticipantCount < 1 ||
                message.ParticipantCount > MaximumParticipants ||
                message.OwnerSpawnAttemptOrdinal != 1 ||
                message.NetworkBehaviourCount < 1 ||
                message.NetworkBehaviourCount > MaximumRuntimeNetworkBehaviours ||
                message.TotalSyncObjectCount > MaximumRuntimeSyncObjects ||
                message.RuntimeOwnerNetId == 0 ||
                message.DeterministicAssetId == 0 ||
                message.NetworkIdentitySceneId != 0 ||
                message.HostLocalSceneGeneration == 0 ||
                !TryValidateRuntimeLifecycleProof(
                    message.Mode,
                    message.HostProofRoute,
                    message.LifecycleProof) ||
                !TryValidateNonzeroDigest(message.SceneContractDigest) ||
                !TryValidateNonzeroDigest(
                    message.ExactNativeTemplateShapeDigest) ||
                !TryValidateNonzeroDigest(message.PreSceneCloneDigest) ||
                !TryValidateNonzeroDigest(message.RuntimeOwnerContractDigest) ||
                !TryValidateNonzeroDigest(message.HostRuntimeProofDigest) ||
                !TryValidateNonzeroDigest(message.ManifestDigest) ||
                !DigestEquals(
                    message.HostRuntimeProofDigest,
                    ComputeHostRuntimeProofDigest(message)) ||
                !DigestEquals(
                    message.ManifestDigest,
                    ComputeRuntimeOwnerManifestDigest(message)))
            {
                if (string.IsNullOrEmpty(error))
                    error = "RuntimeOwnerManifest is noncanonical";
                return false;
            }
            return true;
        }

        private static void WriteRuntimeOwnerManifestTail(
            PeerFixedWriter writer,
            PeerRuntimeOwnerManifest message)
        {
            writer.WriteUInt16(message.RuntimeSchema);
            writer.WriteByte((byte)message.Mode);
            writer.WriteByte((byte)message.HostProofRoute);
            writer.WriteUInt32(message.ContractFlags);
            writer.WriteUInt32(message.RequiredCommonStatusMask);
            writer.WriteUInt32(message.HostObservedStatusMask);
            writer.WriteByte(message.ParticipantCount);
            writer.WriteByte(message.OwnerSpawnAttemptOrdinal);
            writer.WriteUInt16(message.NetworkBehaviourCount);
            writer.WriteUInt16(message.TotalSyncObjectCount);
            writer.WriteUInt32(message.RuntimeOwnerNetId);
            writer.WriteUInt32(message.DeterministicAssetId);
            writer.WriteUInt64(message.NetworkIdentitySceneId);
            writer.WriteUInt64(message.HostLocalSceneGeneration);
            WriteRuntimeLifecycleProof(writer, message.LifecycleProof);
            writer.WriteBytes(message.SceneContractDigest);
            writer.WriteBytes(message.ExactNativeTemplateShapeDigest);
            writer.WriteBytes(message.PreSceneCloneDigest);
            writer.WriteBytes(message.RuntimeOwnerContractDigest);
            writer.WriteBytes(message.HostRuntimeProofDigest);
            writer.WriteBytes(message.ManifestDigest);
        }

        private static void WriteRuntimeOwnerManifestCanonicalPrefix(
            PeerCanonicalWriter writer,
            PeerRuntimeOwnerManifest message)
        {
            writer.WriteUInt16(message.RuntimeSchema);
            writer.WriteByte((byte)message.Mode);
            writer.WriteByte((byte)message.HostProofRoute);
            writer.WriteUInt32(message.ContractFlags);
            writer.WriteUInt32(message.RequiredCommonStatusMask);
            writer.WriteUInt32(message.HostObservedStatusMask);
            writer.WriteByte(message.ParticipantCount);
            writer.WriteByte(message.OwnerSpawnAttemptOrdinal);
            writer.WriteUInt16(message.NetworkBehaviourCount);
            writer.WriteUInt16(message.TotalSyncObjectCount);
            writer.WriteUInt32(message.RuntimeOwnerNetId);
            writer.WriteUInt32(message.DeterministicAssetId);
            writer.WriteUInt64(message.NetworkIdentitySceneId);
            writer.WriteUInt64(message.HostLocalSceneGeneration);
            WriteRuntimeLifecycleProof(writer, message.LifecycleProof);
            writer.WriteBytes(message.SceneContractDigest);
            writer.WriteBytes(message.ExactNativeTemplateShapeDigest);
            writer.WriteBytes(message.PreSceneCloneDigest);
            writer.WriteBytes(message.RuntimeOwnerContractDigest);
        }

        internal static byte[] ComputeRuntimeReadyReceiptDigest(
            PeerRuntimeReady message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            var prefix = new PeerFixedWriter(RuntimeReadyBytes - DigestBytes);
            WriteHeader(
                prefix,
                message.Header,
                PeerBarrierMessageKind.RuntimeReady,
                allowLocalConceptual: true);
            WriteRuntimeReadyTailWithoutReceipt(prefix, message);
            byte[] semantic = prefix.Complete();
            return HashCanonical(writer =>
            {
                writer.WriteDomain("operator-peer-runtime-ready-v1\0");
                writer.WriteBytes(semantic);
            });
        }

        internal static byte[] EncodeRuntimeReadyForAcceptedManifest(
            PeerRuntimeReady message,
            PeerBarrierExpectation expectation,
            PeerRuntimeOwnerManifest acceptedManifest)
        {
            if (expectation == null)
                throw new ArgumentNullException(nameof(expectation));
            if (!TryValidateRuntimeReady(
                    message,
                    expectation,
                    acceptedManifest,
                    allowLocalConceptual: false,
                    out string error))
            {
                throw new InvalidDataException(error);
            }
            var writer = new PeerFixedWriter(RuntimeReadyBytes);
            WriteHeader(
                writer,
                message.Header,
                PeerBarrierMessageKind.RuntimeReady);
            WriteRuntimeReadyTailWithoutReceipt(writer, message);
            writer.WriteBytes(message.ReceiptDigest);
            return writer.Complete();
        }

        internal static bool TryDecodeRuntimeReady(
            byte[] envelope,
            PeerBarrierExpectation expectation,
            PeerRuntimeOwnerManifest acceptedManifest,
            out PeerRuntimeReady message,
            out string error)
        {
            return TryDecodeRuntimeReadyCore(
                envelope,
                expectation,
                acceptedManifest,
                allowLocalConceptual: false,
                out message,
                out error);
        }

        internal static byte[] EncodeLocalConceptualRuntimeReady(
            PeerRuntimeReady message,
            PeerBarrierExpectation localExpectation,
            PeerRuntimeOwnerManifest acceptedManifest)
        {
            if (!TryValidateLocalRuntimeReadyReceipt(
                    message,
                    localExpectation,
                    acceptedManifest,
                    out string error))
            {
                throw new InvalidDataException(error);
            }
            var writer = new PeerFixedWriter(RuntimeReadyBytes);
            WriteHeader(
                writer,
                message.Header,
                PeerBarrierMessageKind.RuntimeReady,
                allowLocalConceptual: true);
            WriteRuntimeReadyTailWithoutReceipt(writer, message);
            writer.WriteBytes(message.ReceiptDigest);
            return writer.Complete();
        }

        internal static bool TryDecodeLocalConceptualRuntimeReady(
            byte[] envelope,
            PeerBarrierExpectation localExpectation,
            PeerRuntimeOwnerManifest acceptedManifest,
            out PeerRuntimeReady message,
            out string error)
        {
            if (!TryDecodeRuntimeReadyCore(
                    envelope,
                    localExpectation,
                    acceptedManifest,
                    allowLocalConceptual: true,
                    out message,
                    out error))
            {
                return false;
            }
            if (message?.Header == null ||
                message.Header.SenderSlot != 0 ||
                message.Header.RecipientSlot != 0)
            {
                message = null;
                error = "local RuntimeReady envelope is not conceptual slot zero";
                return false;
            }
            return true;
        }

        private static bool TryDecodeRuntimeReadyCore(
            byte[] envelope,
            PeerBarrierExpectation expectation,
            PeerRuntimeOwnerManifest acceptedManifest,
            bool allowLocalConceptual,
            out PeerRuntimeReady message,
            out string error)
        {
            message = null;
            error = string.Empty;
            try
            {
                if (expectation == null || acceptedManifest == null)
                {
                    throw new InvalidDataException(
                        "RuntimeReady has no accepted manifest expectation");
                }
                if (envelope == null || envelope.Length != RuntimeReadyBytes)
                {
                    throw new InvalidDataException(
                        "RuntimeReady must contain exactly 347 bytes");
                }
                var reader = new PeerFixedReader(envelope);
                PeerBarrierHeader header = ReadHeader(
                    reader,
                    PeerBarrierMessageKind.RuntimeReady,
                    allowLocalConceptual);
                var candidate = new PeerRuntimeReady(
                    header,
                    reader.ReadUInt16(),
                    (PeerRuntimeMode)reader.ReadByte(),
                    (PeerRuntimeProofRoute)reader.ReadByte(),
                    reader.ReadUInt32(),
                    reader.ReadByte(),
                    reader.ReadByte(),
                    reader.ReadUInt16(),
                    reader.ReadUInt16(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadUInt64(),
                    reader.ReadUInt64(),
                    ReadRuntimeLifecycleProof(reader),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes),
                    reader.ReadBytes(DigestBytes));
                reader.RequireComplete();
                if (!TryValidateRuntimeReady(
                        candidate,
                        expectation,
                        acceptedManifest,
                        allowLocalConceptual,
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

        internal static bool TryValidateLocalRuntimeReadyReceipt(
            PeerRuntimeReady message,
            PeerBarrierExpectation expectation,
            PeerRuntimeOwnerManifest acceptedManifest,
            out string error)
        {
            if (message?.Header == null || expectation?.Header == null ||
                message.Header.SenderSlot != 0 ||
                message.Header.RecipientSlot != 0 ||
                expectation.Header.SenderSlot != 0 ||
                expectation.Header.RecipientSlot != 0)
            {
                error = "local RuntimeReady must be the conceptual slot-zero receipt";
                return false;
            }
            return TryValidateRuntimeReady(
                message,
                expectation,
                acceptedManifest,
                allowLocalConceptual: true,
                out error);
        }

        internal static PeerRuntimeReadySet BuildRuntimeReadySet(
            PeerRuntimeOwnerManifest acceptedManifest,
            IReadOnlyList<PeerRuntimeReady> readiness)
        {
            if (!TryValidateRuntimeOwnerManifestCore(
                    acceptedManifest,
                    allowLocalConceptual: IsLocalConceptualRuntimeManifest(
                        acceptedManifest),
                    out string manifestError))
            {
                throw new InvalidDataException(manifestError);
            }
            if (readiness == null ||
                readiness.Count != acceptedManifest.ParticipantCount)
            {
                throw new InvalidDataException(
                    "RuntimeReady set count does not match the manifest");
            }
            PeerRuntimeReady[] ordered = readiness
                .OrderBy(value => value?.Header?.SenderSlot ?? int.MaxValue)
                .ToArray();
            var receipts = new PeerRuntimeReadySlotReceipt[ordered.Length];
            for (int index = 0; index < ordered.Length; index++)
            {
                PeerRuntimeReady ready = ordered[index];
                bool local = index == 0;
                string readyError = string.Empty;
                if (ready == null || ready.Header.SenderSlot != index ||
                    !TryValidateRuntimeReadyAgainstManifestCore(
                        ready,
                        acceptedManifest,
                        allowLocalConceptual: local,
                        out readyError))
                {
                    throw new InvalidDataException(
                        string.IsNullOrEmpty(readyError)
                            ? "RuntimeReady slots are missing or duplicated"
                            : readyError);
                }
                receipts[index] = new PeerRuntimeReadySlotReceipt(
                    checked((byte)index),
                    ready.ProofRoute,
                    ready.ReceiptDigest);
            }
            byte[] digest = ComputeRuntimeReadySetDigest(
                acceptedManifest.Header.IdentityDigest,
                acceptedManifest.Header.SceneGenerationEpoch,
                acceptedManifest.Header.GenerationKey,
                acceptedManifest.ManifestDigest,
                acceptedManifest.ParticipantCount,
                receipts);
            return new PeerRuntimeReadySet(
                acceptedManifest.ParticipantCount,
                acceptedManifest.ManifestDigest,
                digest,
                receipts);
        }

        internal static byte[] ComputeRuntimeReadySetDigest(
            byte[] identityDigest,
            ulong sceneGenerationEpoch,
            byte[] generationKey,
            byte[] manifestDigest,
            int participantCount,
            IReadOnlyList<PeerRuntimeReadySlotReceipt> receipts)
        {
            byte[] identity = CloneDigest(identityDigest, nameof(identityDigest));
            byte[] generation = CloneDigest(generationKey, nameof(generationKey));
            byte[] manifest = CloneDigest(manifestDigest, nameof(manifestDigest));
            if (sceneGenerationEpoch == 0 ||
                !TryValidateNonzeroDigest(identity) ||
                !TryValidateNonzeroDigest(generation) ||
                !TryValidateNonzeroDigest(manifest) ||
                participantCount < 1 ||
                participantCount > MaximumParticipants ||
                receipts == null || receipts.Count != participantCount)
            {
                throw new InvalidDataException(
                    "RuntimeReady set inputs are noncanonical");
            }
            for (int index = 0; index < receipts.Count; index++)
            {
                PeerRuntimeReadySlotReceipt receipt = receipts[index];
                PeerRuntimeProofRoute route = index == 0
                    ? PeerRuntimeProofRoute.HostServerLocalAdoption
                    : PeerRuntimeProofRoute.RemoteCustomHandler;
                if (receipt.Slot != index || receipt.Route != route ||
                    !TryValidateNonzeroDigest(receipt.ReceiptDigest))
                {
                    throw new InvalidDataException(
                        "RuntimeReady set is not complete and slot ordered");
                }
            }
            return HashCanonical(writer =>
            {
                writer.WriteDomain("operator-peer-runtime-ready-set-v1\0");
                writer.WriteBytes(identity);
                writer.WriteUInt64(sceneGenerationEpoch);
                writer.WriteBytes(generation);
                writer.WriteBytes(manifest);
                writer.WriteByte(checked((byte)participantCount));
                for (int index = 0; index < receipts.Count; index++)
                {
                    writer.WriteByte(receipts[index].Slot);
                    writer.WriteByte((byte)receipts[index].Route);
                    writer.WriteBytes(receipts[index].ReceiptDigest);
                }
            });
        }

        private static bool TryValidateRuntimeReady(
            PeerRuntimeReady message,
            PeerBarrierExpectation expectation,
            PeerRuntimeOwnerManifest acceptedManifest,
            bool allowLocalConceptual,
            out string error)
        {
            error = string.Empty;
            if (expectation == null ||
                expectation.Header.Kind != PeerBarrierMessageKind.RuntimeReady ||
                !TryValidateExpectedHeader(
                    message?.Header,
                    expectation,
                    out error,
                    allowLocalConceptual) ||
                !TryValidateRuntimeReadyAgainstManifestCore(
                    message,
                    acceptedManifest,
                    allowLocalConceptual,
                    out error) ||
                message.ParticipantCount != expectation.ParticipantCount ||
                message.Mode != (expectation.IsPve
                    ? PeerRuntimeMode.Pve
                    : PeerRuntimeMode.Pvp) ||
                !DigestEquals(
                    message.SceneContractDigest,
                    expectation.SceneContractDigest))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "RuntimeReady does not match the frozen generation";
                }
                return false;
            }
            return true;
        }

        private static bool TryValidateRuntimeReadyAgainstManifestCore(
            PeerRuntimeReady message,
            PeerRuntimeOwnerManifest acceptedManifest,
            bool allowLocalConceptual,
            out string error)
        {
            error = string.Empty;
            if (message == null || acceptedManifest == null ||
                !TryValidateRuntimeOwnerManifestCore(
                    acceptedManifest,
                    allowLocalConceptual: IsLocalConceptualRuntimeManifest(
                        acceptedManifest),
                    out error))
            {
                if (string.IsNullOrEmpty(error))
                    error = "RuntimeReady has no canonical accepted manifest";
                return false;
            }
            bool localConceptual = allowLocalConceptual &&
                message.Header != null &&
                message.Header.SenderSlot == 0 &&
                message.Header.RecipientSlot == 0;
            PeerRuntimeProofRoute requiredRoute = localConceptual
                ? PeerRuntimeProofRoute.HostServerLocalAdoption
                : PeerRuntimeProofRoute.RemoteCustomHandler;
            uint requiredMask = localConceptual
                ? RuntimeHostObservedStatusMask
                : RuntimeRemoteObservedStatusMask;
            if (!TryValidateHeader(
                    message.Header,
                    PeerBarrierMessageKind.RuntimeReady,
                    out error,
                    allowLocalConceptual) ||
                message.Header.SenderSlot >= acceptedManifest.ParticipantCount ||
                message.RuntimeSchema != RuntimeOwnerSchema ||
                message.Mode != acceptedManifest.Mode ||
                message.ProofRoute != requiredRoute ||
                message.ObservedStatusMask != requiredMask ||
                message.ParticipantCount != acceptedManifest.ParticipantCount ||
                message.OwnerSpawnAttemptOrdinal !=
                    acceptedManifest.OwnerSpawnAttemptOrdinal ||
                message.NetworkBehaviourCount !=
                    acceptedManifest.NetworkBehaviourCount ||
                message.TotalSyncObjectCount !=
                    acceptedManifest.TotalSyncObjectCount ||
                message.RuntimeOwnerNetId != acceptedManifest.RuntimeOwnerNetId ||
                message.DeterministicAssetId !=
                    acceptedManifest.DeterministicAssetId ||
                message.NetworkIdentitySceneId != 0 ||
                message.SenderLocalSceneGeneration == 0 ||
                (localConceptual && message.SenderLocalSceneGeneration !=
                    acceptedManifest.HostLocalSceneGeneration) ||
                !TryValidateRuntimeLifecycleProof(
                    message.Mode,
                    message.ProofRoute,
                    message.LifecycleProof) ||
                !FixedRuntimeHeaderSemanticsEqual(
                    message.Header,
                    acceptedManifest.Header) ||
                !DigestEquals(
                    message.SceneContractDigest,
                    acceptedManifest.SceneContractDigest) ||
                !DigestEquals(
                    message.ExactNativeTemplateShapeDigest,
                    acceptedManifest.ExactNativeTemplateShapeDigest) ||
                !DigestEquals(
                    message.PreSceneCloneDigest,
                    acceptedManifest.PreSceneCloneDigest) ||
                !DigestEquals(
                    message.RuntimeOwnerContractDigest,
                    acceptedManifest.RuntimeOwnerContractDigest) ||
                !DigestEquals(
                    message.ManifestDigest,
                    acceptedManifest.ManifestDigest) ||
                !TryValidateNonzeroDigest(message.ReceiptDigest) ||
                !DigestEquals(
                    message.ReceiptDigest,
                    ComputeRuntimeReadyReceiptDigest(message)))
            {
                if (string.IsNullOrEmpty(error))
                    error = "RuntimeReady is noncanonical or mismatches manifest";
                return false;
            }
            return true;
        }

        private static void WriteRuntimeReadyTailWithoutReceipt(
            PeerFixedWriter writer,
            PeerRuntimeReady message)
        {
            writer.WriteUInt16(message.RuntimeSchema);
            writer.WriteByte((byte)message.Mode);
            writer.WriteByte((byte)message.ProofRoute);
            writer.WriteUInt32(message.ObservedStatusMask);
            writer.WriteByte(message.ParticipantCount);
            writer.WriteByte(message.OwnerSpawnAttemptOrdinal);
            writer.WriteUInt16(message.NetworkBehaviourCount);
            writer.WriteUInt16(message.TotalSyncObjectCount);
            writer.WriteUInt32(message.RuntimeOwnerNetId);
            writer.WriteUInt32(message.DeterministicAssetId);
            writer.WriteUInt64(message.NetworkIdentitySceneId);
            writer.WriteUInt64(message.SenderLocalSceneGeneration);
            WriteRuntimeLifecycleProof(writer, message.LifecycleProof);
            writer.WriteBytes(message.SceneContractDigest);
            writer.WriteBytes(message.ExactNativeTemplateShapeDigest);
            writer.WriteBytes(message.PreSceneCloneDigest);
            writer.WriteBytes(message.RuntimeOwnerContractDigest);
            writer.WriteBytes(message.ManifestDigest);
        }

        internal static IReadOnlyDictionary<
            PeerRuntimeDigestNode,
            IReadOnlyCollection<PeerRuntimeDigestNode>>
            CreateRuntimeDigestDependencyRegistry()
        {
            return new ReadOnlyDictionary<
                PeerRuntimeDigestNode,
                IReadOnlyCollection<PeerRuntimeDigestNode>>(
                new Dictionary<
                    PeerRuntimeDigestNode,
                    IReadOnlyCollection<PeerRuntimeDigestNode>>
                {
                    [PeerRuntimeDigestNode.ExactNativeTemplateShape] =
                        Array.AsReadOnly(Array.Empty<PeerRuntimeDigestNode>()),
                    [PeerRuntimeDigestNode.PreSceneClone] = Array.AsReadOnly(
                        new[] { PeerRuntimeDigestNode.ExactNativeTemplateShape }),
                    [PeerRuntimeDigestNode.SceneContract] = Array.AsReadOnly(
                        new[]
                        {
                            PeerRuntimeDigestNode.ExactNativeTemplateShape,
                            PeerRuntimeDigestNode.PreSceneClone
                        }),
                    [PeerRuntimeDigestNode.GenerationKey] = Array.AsReadOnly(
                        new[] { PeerRuntimeDigestNode.SceneContract }),
                    [PeerRuntimeDigestNode.RuntimeOwnerContract] =
                        Array.AsReadOnly(new[]
                        {
                            PeerRuntimeDigestNode.ExactNativeTemplateShape,
                            PeerRuntimeDigestNode.PreSceneClone,
                            PeerRuntimeDigestNode.SceneContract,
                            PeerRuntimeDigestNode.GenerationKey
                        }),
                    [PeerRuntimeDigestNode.HostRuntimeProof] = Array.AsReadOnly(
                        new[]
                        {
                            PeerRuntimeDigestNode.ExactNativeTemplateShape,
                            PeerRuntimeDigestNode.PreSceneClone,
                            PeerRuntimeDigestNode.SceneContract,
                            PeerRuntimeDigestNode.GenerationKey,
                            PeerRuntimeDigestNode.RuntimeOwnerContract
                        }),
                    [PeerRuntimeDigestNode.RuntimeOwnerManifest] =
                        Array.AsReadOnly(new[]
                        {
                            PeerRuntimeDigestNode.ExactNativeTemplateShape,
                            PeerRuntimeDigestNode.PreSceneClone,
                            PeerRuntimeDigestNode.SceneContract,
                            PeerRuntimeDigestNode.GenerationKey,
                            PeerRuntimeDigestNode.RuntimeOwnerContract,
                            PeerRuntimeDigestNode.HostRuntimeProof
                        }),
                    [PeerRuntimeDigestNode.RuntimeReadyReceipt] =
                        Array.AsReadOnly(new[]
                        {
                            PeerRuntimeDigestNode.RuntimeOwnerManifest
                        }),
                    [PeerRuntimeDigestNode.RuntimeReadySet] = Array.AsReadOnly(
                        new[]
                        {
                            PeerRuntimeDigestNode.RuntimeOwnerManifest,
                            PeerRuntimeDigestNode.RuntimeReadyReceipt
                        }),
                    [PeerRuntimeDigestNode.BeginContract] = Array.AsReadOnly(
                        new[]
                        {
                            PeerRuntimeDigestNode.RuntimeOwnerManifest,
                            PeerRuntimeDigestNode.RuntimeReadySet
                        })
                });
        }

        internal static bool TryTopologicallySortRuntimeDigestDependencies(
            IReadOnlyDictionary<
                PeerRuntimeDigestNode,
                IReadOnlyCollection<PeerRuntimeDigestNode>> dependencies,
            out ReadOnlyCollection<PeerRuntimeDigestNode> sorted,
            out string error)
        {
            sorted = null;
            error = string.Empty;
            PeerRuntimeDigestNode[] expected = Enum
                .GetValues(typeof(PeerRuntimeDigestNode))
                .Cast<PeerRuntimeDigestNode>()
                .OrderBy(value => (byte)value)
                .ToArray();
            var valid = new HashSet<PeerRuntimeDigestNode>(expected);
            if (dependencies == null || dependencies.Count != expected.Length ||
                dependencies.Keys.Any(value => !valid.Contains(value)) ||
                expected.Any(value => !dependencies.ContainsKey(value)))
            {
                error = "runtime digest dependency registry has unknown/missing nodes";
                return false;
            }
            var copied = new Dictionary<
                PeerRuntimeDigestNode,
                PeerRuntimeDigestNode[]>();
            foreach (PeerRuntimeDigestNode node in expected)
            {
                IReadOnlyCollection<PeerRuntimeDigestNode> source =
                    dependencies[node];
                if (source == null)
                {
                    error = "runtime digest dependency list is null";
                    return false;
                }
                PeerRuntimeDigestNode[] direct = source.ToArray();
                if (direct.Any(value => !valid.Contains(value)) ||
                    direct.Any(value => value == node) ||
                    direct.Distinct().Count() != direct.Length)
                {
                    error =
                        "runtime digest dependency registry has an unknown, self, or duplicate edge";
                    return false;
                }
                copied[node] = direct;
            }

            var completed = new HashSet<PeerRuntimeDigestNode>();
            var result = new List<PeerRuntimeDigestNode>(expected.Length);
            while (result.Count < expected.Length)
            {
                PeerRuntimeDigestNode? next = expected
                    .Where(value => !completed.Contains(value))
                    .Cast<PeerRuntimeDigestNode?>()
                    .FirstOrDefault(value => value.HasValue &&
                        copied[value.Value].All(completed.Contains));
                if (!next.HasValue)
                {
                    error = "runtime digest dependency registry contains a cycle";
                    return false;
                }
                completed.Add(next.Value);
                result.Add(next.Value);
            }
            sorted = Array.AsReadOnly(result.ToArray());
            return true;
        }

        private static bool FixedRuntimeHeaderSemanticsEqual(
            PeerBarrierHeader observed,
            PeerBarrierHeader acceptedManifestHeader)
        {
            return observed != null && acceptedManifestHeader != null &&
                observed.SceneGenerationEpoch ==
                    acceptedManifestHeader.SceneGenerationEpoch &&
                FixedBytesEqual(
                    observed.SessionNonce,
                    acceptedManifestHeader.SessionNonce,
                    SessionNonceBytes) &&
                DigestEquals(
                    observed.IdentityDigest,
                    acceptedManifestHeader.IdentityDigest) &&
                DigestEquals(
                    observed.GenerationKey,
                    acceptedManifestHeader.GenerationKey);
        }

        private static bool IsLocalConceptualRuntimeManifest(
            PeerRuntimeOwnerManifest message)
        {
            return message?.Header != null &&
                message.Header.Kind ==
                    PeerBarrierMessageKind.RuntimeOwnerManifest &&
                message.Header.SenderSlot == 0 &&
                message.Header.RecipientSlot == 0;
        }

        private static bool IsRuntimeMode(PeerRuntimeMode mode)
        {
            return mode == PeerRuntimeMode.Pve || mode == PeerRuntimeMode.Pvp;
        }

        private static bool TryValidateRuntimeLifecycleProof(
            PeerRuntimeMode mode,
            PeerRuntimeProofRoute route,
            PeerRuntimeLifecycleProof proof)
        {
            bool host = route == PeerRuntimeProofRoute.HostServerLocalAdoption;
            bool remote = route == PeerRuntimeProofRoute.RemoteCustomHandler;
            if (!IsRuntimeMode(mode) || (!host && !remote))
                return false;
            byte expectedOnStartServer = checked((byte)(
                host && mode == PeerRuntimeMode.Pve ? 1 : 0));
            return proof.CustomSpawnHandlerCalls == (remote ? 1 : 0) &&
                proof.ManualInitializeNetworkBehavioursCalls == 1 &&
                proof.NetworkIdentityAwakeCalls == 1 &&
                proof.DeserializeClientCalls == (remote ? 1 : 0) &&
                proof.ExactOwnerOnStartServerControlledEntries ==
                    expectedOnStartServer &&
                proof.ExactOwnerOnStartClientControlledEntries == 1 &&
                proof.GameModeInitializeSuccessfulReturns == 1 &&
                proof.ProhibitedOriginalOnStartClientBodyRuns == 0 &&
                proof.PrematureClockMoveNextEntries == 0 &&
                proof.NativeClockStartsBeforeBegin == 0 &&
                proof.Reserved == 0;
        }

        private static void WriteRuntimeGenerationTuple(
            PeerCanonicalWriter writer,
            PeerBarrierHeader header)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header));
            writer.WriteBytes(header.SessionNonce);
            writer.WriteBytes(header.IdentityDigest);
            writer.WriteUInt64(header.SceneGenerationEpoch);
            writer.WriteBytes(header.GenerationKey);
        }

        private static void WriteRuntimeLifecycleProof(
            PeerCanonicalWriter writer,
            PeerRuntimeLifecycleProof proof)
        {
            writer.WriteByte(proof.CustomSpawnHandlerCalls);
            writer.WriteByte(proof.ManualInitializeNetworkBehavioursCalls);
            writer.WriteByte(proof.NetworkIdentityAwakeCalls);
            writer.WriteByte(proof.DeserializeClientCalls);
            writer.WriteByte(proof.ExactOwnerOnStartServerControlledEntries);
            writer.WriteByte(proof.ExactOwnerOnStartClientControlledEntries);
            writer.WriteByte(proof.GameModeInitializeSuccessfulReturns);
            writer.WriteByte(proof.ProhibitedOriginalOnStartClientBodyRuns);
            writer.WriteUInt32(proof.PrematureClockMoveNextEntries);
            writer.WriteUInt16(proof.NativeClockStartsBeforeBegin);
            writer.WriteUInt16(proof.Reserved);
        }

        private static void WriteRuntimeLifecycleProof(
            PeerFixedWriter writer,
            PeerRuntimeLifecycleProof proof)
        {
            writer.WriteByte(proof.CustomSpawnHandlerCalls);
            writer.WriteByte(proof.ManualInitializeNetworkBehavioursCalls);
            writer.WriteByte(proof.NetworkIdentityAwakeCalls);
            writer.WriteByte(proof.DeserializeClientCalls);
            writer.WriteByte(proof.ExactOwnerOnStartServerControlledEntries);
            writer.WriteByte(proof.ExactOwnerOnStartClientControlledEntries);
            writer.WriteByte(proof.GameModeInitializeSuccessfulReturns);
            writer.WriteByte(proof.ProhibitedOriginalOnStartClientBodyRuns);
            writer.WriteUInt32(proof.PrematureClockMoveNextEntries);
            writer.WriteUInt16(proof.NativeClockStartsBeforeBegin);
            writer.WriteUInt16(proof.Reserved);
        }

        private static PeerRuntimeLifecycleProof ReadRuntimeLifecycleProof(
            PeerFixedReader reader)
        {
            return new PeerRuntimeLifecycleProof(
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadUInt32(),
                reader.ReadUInt16(),
                reader.ReadUInt16());
        }
    }
}
