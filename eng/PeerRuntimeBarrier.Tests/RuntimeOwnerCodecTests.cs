using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OperatorModdedOperations.PeerProtocol;

internal static class RuntimeOwnerCodecTests
{
    private static readonly byte[] Nonce = Sequential(16, 0x31);
    private static readonly byte[] Identity = Sequential(32, 0x51);
    private static readonly byte[] Scene = Sequential(32, 0x71);
    private static readonly byte[] Template = Sequential(32, 0x91);
    private static readonly byte[] PreScene = Sequential(32, 0xB1);
    private static readonly byte[] RuntimeContract = Sequential(32, 0xD1);

    internal static void Run()
    {
        TestRuntimeOwnerMessages(PeerRuntimeMode.Pve);
        TestRuntimeOwnerMessages(PeerRuntimeMode.Pvp);
        TestRuntimeOwnerNegativeCases();
        TestRuntimeOwnerEnvelopeCoverage();
        TestRuntimeReadySet();
        TestRuntimeDigestDependencies();
        TestRuntimeOwnerImmutability();
        TestLoaderNeutralRuntimeVectors();
        TestRuntimeOwnerGoldenVectors();
    }

    private static void TestRuntimeOwnerMessages(PeerRuntimeMode mode)
    {
        Assert((byte)PeerBarrierMessageKind.RuntimeOwnerManifest == 18);
        Assert((byte)PeerBarrierMessageKind.RuntimeReady == 19);
        Assert(PeerRuntimeBarrierCodec.RuntimeOwnerManifestSchemaOffset == 101);
        Assert(PeerRuntimeBarrierCodec.RuntimeOwnerManifestLifecycleOffset == 147);
        Assert(PeerRuntimeBarrierCodec.RuntimeOwnerManifestSceneDigestOffset == 163);
        Assert(PeerRuntimeBarrierCodec.RuntimeOwnerManifestHostProofDigestOffset ==
            291);
        Assert(PeerRuntimeBarrierCodec.RuntimeOwnerManifestDigestOffset == 323);
        Assert(PeerRuntimeBarrierCodec.RuntimeReadySchemaOffset == 101);
        Assert(PeerRuntimeBarrierCodec.RuntimeReadyLifecycleOffset == 139);
        Assert(PeerRuntimeBarrierCodec.RuntimeReadySceneDigestOffset == 155);
        Assert(PeerRuntimeBarrierCodec.RuntimeReadyManifestDigestOffset == 283);
        Assert(PeerRuntimeBarrierCodec.RuntimeReadyReceiptDigestOffset == 315);
        Assert(PeerRuntimeBarrierCodec.RuntimeLifecycleProofBytes == 16);
        PeerRuntimeOwnerManifest manifest = CreateManifest(mode, 1, 2);
        PeerBarrierExpectation manifestExpectation = Expect(
            PeerBarrierMessageKind.RuntimeOwnerManifest,
            0,
            1,
            mode,
            2);
        byte[] manifestBytes =
            PeerRuntimeBarrierCodec.EncodeRuntimeOwnerManifestForFrozenGeneration(
                manifest,
                manifestExpectation);
        Assert(manifestBytes.Length ==
            PeerRuntimeBarrierCodec.RuntimeOwnerManifestBytes);
        Assert(manifestBytes.Length == 355);
        Assert(PeerRuntimeBarrierCodec.TryDecodeRuntimeOwnerManifest(
            manifestBytes,
            manifestExpectation,
            out PeerRuntimeOwnerManifest decodedManifest,
            out string error), error);
        Assert(!PeerRuntimeBarrierCodec
            .TryDecodeLocalConceptualRuntimeOwnerManifest(
                manifestBytes,
                manifestExpectation,
                out _,
                out _));

        PeerRuntimeReady remote = CreateReady(decodedManifest, 1, false, 22);
        PeerBarrierExpectation remoteExpectation = Expect(
            PeerBarrierMessageKind.RuntimeReady,
            1,
            0,
            mode,
            2);
        byte[] readyBytes =
            PeerRuntimeBarrierCodec.EncodeRuntimeReadyForAcceptedManifest(
                remote,
                remoteExpectation,
                decodedManifest);
        Assert(readyBytes.Length == PeerRuntimeBarrierCodec.RuntimeReadyBytes);
        Assert(readyBytes.Length == 347);
        Assert(PeerRuntimeBarrierCodec.TryDecodeRuntimeReady(
            readyBytes,
            remoteExpectation,
            decodedManifest,
            out PeerRuntimeReady decodedReady,
            out error), error);
        Assert(!PeerRuntimeBarrierCodec.TryDecodeLocalConceptualRuntimeReady(
            readyBytes,
            remoteExpectation,
            decodedManifest,
            out _,
            out _));
        Assert(decodedReady.ProofRoute ==
            PeerRuntimeProofRoute.RemoteCustomHandler);
        Assert(!PeerRuntimeBarrierCodec.TryValidateLocalRuntimeReadyReceipt(
            decodedReady,
            remoteExpectation,
            decodedManifest,
            out _));

        PeerRuntimeReady local = CreateReady(decodedManifest, 0, true, 11);
        PeerBarrierExpectation localExpectation = Expect(
            PeerBarrierMessageKind.RuntimeReady,
            0,
            0,
            mode,
            2);
        Assert(PeerRuntimeBarrierCodec.TryValidateLocalRuntimeReadyReceipt(
            local,
            localExpectation,
            decodedManifest,
            out error), error);
        byte[] localBytes =
            PeerRuntimeBarrierCodec.EncodeLocalConceptualRuntimeReady(
                local,
                localExpectation,
                decodedManifest);
        Assert(localBytes.Length == 347);
        Assert(PeerRuntimeBarrierCodec.TryDecodeLocalConceptualRuntimeReady(
            localBytes,
            localExpectation,
            decodedManifest,
            out PeerRuntimeReady decodedLocal,
            out error), error);
        Assert(decodedLocal.Header.SenderSlot == 0 &&
            decodedLocal.Header.RecipientSlot == 0);
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodeRuntimeReadyForAcceptedManifest(
                local,
                localExpectation,
                decodedManifest));
        Assert(!PeerRuntimeBarrierCodec.TryDecodeRuntimeReady(
            localBytes,
            localExpectation,
            decodedManifest,
            out _,
            out _));

        byte[] localOnWire = (byte[])readyBytes.Clone();
        Array.Clear(localOnWire, 5, 4);
        Assert(!PeerRuntimeBarrierCodec.TryDecodeRuntimeReady(
            localOnWire,
            localExpectation,
            decodedManifest,
            out _,
            out _));
    }

    private static void TestRuntimeOwnerNegativeCases()
    {
        PeerRuntimeOwnerManifest manifest = CreateManifest(
            PeerRuntimeMode.Pve,
            1,
            2);
        PeerBarrierExpectation manifestExpectation = Expect(
            PeerBarrierMessageKind.RuntimeOwnerManifest,
            0,
            1,
            PeerRuntimeMode.Pve,
            2);
        byte[] bytes =
            PeerRuntimeBarrierCodec.EncodeRuntimeOwnerManifestForFrozenGeneration(
                manifest,
                manifestExpectation);
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodeRuntimeOwnerManifestForFrozenGeneration(
                CreateManifest(PeerRuntimeMode.Pve, 0, 2),
                Expect(
                    PeerBarrierMessageKind.RuntimeOwnerManifest,
                    0,
                    0,
                    PeerRuntimeMode.Pve,
                    2)));
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodeRuntimeOwnerManifestForFrozenGeneration(
                CreateManifest(PeerRuntimeMode.Pve, 2, 2),
                Expect(
                    PeerBarrierMessageKind.RuntimeOwnerManifest,
                    0,
                    2,
                    PeerRuntimeMode.Pve,
                    2)));
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodeRuntimeOwnerManifestForFrozenGeneration(
                CreateManifest(PeerRuntimeMode.Pve, 1, 1),
                Expect(
                    PeerBarrierMessageKind.RuntimeOwnerManifest,
                    0,
                    1,
                    PeerRuntimeMode.Pve,
                    1)));

        PeerRuntimeOwnerManifest maximum = CreateManifest(
            PeerRuntimeMode.Pve,
            63,
            64,
            networkBehaviourCount: 64,
            totalSyncObjectCount: 256);
        PeerBarrierExpectation maximumExpectation = Expect(
            PeerBarrierMessageKind.RuntimeOwnerManifest,
            0,
            63,
            PeerRuntimeMode.Pve,
            64);
        Assert(PeerRuntimeBarrierCodec
            .EncodeRuntimeOwnerManifestForFrozenGeneration(
                maximum,
                maximumExpectation).Length == 355);
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodeRuntimeOwnerManifestForFrozenGeneration(
                CreateManifest(
                    PeerRuntimeMode.Pve,
                    1,
                    2,
                    networkBehaviourCount: 0),
                manifestExpectation));
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodeRuntimeOwnerManifestForFrozenGeneration(
                CreateManifest(
                    PeerRuntimeMode.Pve,
                    1,
                    2,
                    networkBehaviourCount: 65),
                manifestExpectation));
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodeRuntimeOwnerManifestForFrozenGeneration(
                CreateManifest(
                    PeerRuntimeMode.Pve,
                    1,
                    2,
                    totalSyncObjectCount: 257),
                manifestExpectation));

        byte[] trailing = new byte[bytes.Length + 1];
        Buffer.BlockCopy(bytes, 0, trailing, 0, bytes.Length);
        Assert(!PeerRuntimeBarrierCodec.TryDecodeRuntimeOwnerManifest(
            trailing,
            manifestExpectation,
            out _,
            out _));
        AssertManifestMutationRejected(bytes, manifestExpectation, 105, 0x80);
        AssertManifestMutationRejected(bytes, manifestExpectation, 109, 0x80);
        AssertManifestMutationRejected(bytes, manifestExpectation, 113, 0x80);
        AssertManifestMutationRejected(bytes, manifestExpectation, 118, 0x01);
        AssertManifestMutationRejected(
            bytes,
            manifestExpectation,
            PeerRuntimeBarrierCodec.RuntimeOwnerManifestSchemaOffset,
            0x01);
        AssertManifestMutationRejected(bytes, manifestExpectation, 103, 0x7F);
        AssertManifestMutationRejected(bytes, manifestExpectation, 104, 0x03);
        AssertManifestMutationRejected(
            bytes,
            manifestExpectation,
            PeerRuntimeBarrierCodec.RuntimeOwnerManifestLifecycleOffset + 14,
            0x01);
        byte[] zeroScene = (byte[])bytes.Clone();
        Array.Clear(
            zeroScene,
            PeerRuntimeBarrierCodec.RuntimeOwnerManifestSceneDigestOffset,
            32);
        Assert(!PeerRuntimeBarrierCodec.TryDecodeRuntimeOwnerManifest(
            zeroScene,
            manifestExpectation,
            out _,
            out _));
        byte[] zeroManifest = (byte[])bytes.Clone();
        Array.Clear(
            zeroManifest,
            PeerRuntimeBarrierCodec.RuntimeOwnerManifestDigestOffset,
            32);
        Assert(!PeerRuntimeBarrierCodec.TryDecodeRuntimeOwnerManifest(
            zeroManifest,
            manifestExpectation,
            out _,
            out _));
        byte[] wrongRecipient = (byte[])bytes.Clone();
        wrongRecipient[9] = 2;
        Assert(!PeerRuntimeBarrierCodec.TryDecodeRuntimeOwnerManifest(
            wrongRecipient,
            manifestExpectation,
            out _,
            out _));

        PeerRuntimeReady remote = CreateReady(manifest, 1, false, 22);
        PeerBarrierExpectation remoteExpectation = Expect(
            PeerBarrierMessageKind.RuntimeReady,
            1,
            0,
            PeerRuntimeMode.Pve,
            2);
        byte[] ready =
            PeerRuntimeBarrierCodec.EncodeRuntimeReadyForAcceptedManifest(
                remote,
                remoteExpectation,
                manifest);
        byte[] wrongRoute = (byte[])ready.Clone();
        wrongRoute[104] = (byte)PeerRuntimeProofRoute.HostServerLocalAdoption;
        Assert(!PeerRuntimeBarrierCodec.TryDecodeRuntimeReady(
            wrongRoute,
            remoteExpectation,
            manifest,
            out _,
            out _));
        byte[] wrongReadySchema = (byte[])ready.Clone();
        wrongReadySchema[PeerRuntimeBarrierCodec.RuntimeReadySchemaOffset] ^= 1;
        Assert(!PeerRuntimeBarrierCodec.TryDecodeRuntimeReady(
            wrongReadySchema,
            remoteExpectation,
            manifest,
            out _,
            out _));
        byte[] zeroReadyScene = (byte[])ready.Clone();
        Array.Clear(
            zeroReadyScene,
            PeerRuntimeBarrierCodec.RuntimeReadySceneDigestOffset,
            32);
        Assert(!PeerRuntimeBarrierCodec.TryDecodeRuntimeReady(
            zeroReadyScene,
            remoteExpectation,
            manifest,
            out _,
            out _));
        byte[] unknownStatus = (byte[])ready.Clone();
        unknownStatus[107] |= 0x10;
        Assert(!PeerRuntimeBarrierCodec.TryDecodeRuntimeReady(
            unknownStatus,
            remoteExpectation,
            manifest,
            out _,
            out _));
        byte[] lifecycleReserved = (byte[])ready.Clone();
        lifecycleReserved[
            PeerRuntimeBarrierCodec.RuntimeReadyLifecycleOffset + 14] = 1;
        Assert(!PeerRuntimeBarrierCodec.TryDecodeRuntimeReady(
            lifecycleReserved,
            remoteExpectation,
            manifest,
            out _,
            out _));
        byte[] zeroAcceptedManifest = (byte[])ready.Clone();
        Array.Clear(
            zeroAcceptedManifest,
            PeerRuntimeBarrierCodec.RuntimeReadyManifestDigestOffset,
            32);
        Assert(!PeerRuntimeBarrierCodec.TryDecodeRuntimeReady(
            zeroAcceptedManifest,
            remoteExpectation,
            manifest,
            out _,
            out _));
        byte[] readyTrailing = new byte[ready.Length + 1];
        Buffer.BlockCopy(ready, 0, readyTrailing, 0, ready.Length);
        Assert(!PeerRuntimeBarrierCodec.TryDecodeRuntimeReady(
            readyTrailing,
            remoteExpectation,
            manifest,
            out _,
            out _));
    }

    private static void TestRuntimeOwnerEnvelopeCoverage()
    {
        Assert(!PeerRuntimeBarrierCodec.RuntimeSemanticDigestBuildersComplete,
            "Td/Cd/Rd builders must remain explicitly pending");

        PeerRuntimeOwnerManifest manifest = CreateManifest(
            PeerRuntimeMode.Pve,
            1,
            2);
        PeerBarrierExpectation manifestExpectation = Expect(
            PeerBarrierMessageKind.RuntimeOwnerManifest,
            0,
            1,
            PeerRuntimeMode.Pve,
            2);
        byte[] manifestBytes = PeerRuntimeBarrierCodec
            .EncodeRuntimeOwnerManifestForFrozenGeneration(
                manifest,
                manifestExpectation);
        AssertManifestEnvelopeLengthsRejected(
            manifestBytes,
            manifestExpectation,
            localConceptual: false);
        AssertEveryManifestByteBound(
            manifestBytes,
            manifestExpectation,
            localConceptual: false);
        foreach (int digestOffset in new[] { 163, 195, 227, 259, 291, 323 })
        {
            AssertManifestDigestZeroRejected(
                manifestBytes,
                manifestExpectation,
                digestOffset,
                localConceptual: false);
        }
        AssertManifestFieldReversalRejected(
            manifestBytes,
            manifestExpectation,
            0,
            2,
            localConceptual: false);
        AssertManifestFieldReversalRejected(
            manifestBytes,
            manifestExpectation,
            2,
            2,
            localConceptual: false);
        AssertManifestFieldReversalRejected(
            manifestBytes,
            manifestExpectation,
            105,
            4,
            localConceptual: false);
        AssertManifestFieldReversalRejected(
            manifestBytes,
            manifestExpectation,
            123,
            4,
            localConceptual: false);
        AssertManifestFieldReversalRejected(
            manifestBytes,
            manifestExpectation,
            139,
            8,
            localConceptual: false);

        PeerRuntimeReady remoteReady = CreateReady(manifest, 1, false, 22);
        PeerBarrierExpectation readyExpectation = Expect(
            PeerBarrierMessageKind.RuntimeReady,
            1,
            0,
            PeerRuntimeMode.Pve,
            2);
        byte[] readyBytes = PeerRuntimeBarrierCodec
            .EncodeRuntimeReadyForAcceptedManifest(
                remoteReady,
                readyExpectation,
                manifest);
        AssertReadyEnvelopeLengthsRejected(
            readyBytes,
            readyExpectation,
            manifest,
            localConceptual: false);
        AssertEveryReadyByteBound(
            readyBytes,
            readyExpectation,
            manifest,
            localConceptual: false);
        foreach (int digestOffset in new[] { 155, 187, 219, 251, 283, 315 })
        {
            AssertReadyDigestZeroRejected(
                readyBytes,
                readyExpectation,
                manifest,
                digestOffset,
                localConceptual: false);
        }
        AssertReadyFieldReversalRejected(
            readyBytes,
            readyExpectation,
            manifest,
            0,
            2,
            localConceptual: false);
        AssertReadyFieldReversalRejected(
            readyBytes,
            readyExpectation,
            manifest,
            2,
            2,
            localConceptual: false);
        AssertReadyFieldReversalRejected(
            readyBytes,
            readyExpectation,
            manifest,
            105,
            4,
            localConceptual: false);
        AssertReadyFieldReversalRejected(
            readyBytes,
            readyExpectation,
            manifest,
            111,
            2,
            localConceptual: false);
        AssertReadyFieldReversalRejected(
            readyBytes,
            readyExpectation,
            manifest,
            131,
            8,
            localConceptual: false);

        PeerRuntimeOwnerManifest soloSource = CreateManifest(
            PeerRuntimeMode.Pve,
            1,
            1);
        PeerBarrierExpectation localManifestExpectation = Expect(
            PeerBarrierMessageKind.RuntimeOwnerManifest,
            0,
            0,
            PeerRuntimeMode.Pve,
            1);
        PeerRuntimeOwnerManifest localManifest = PeerRuntimeBarrierCodec
            .BuildLocalConceptualRuntimeOwnerManifest(
                soloSource,
                localManifestExpectation);
        byte[] localManifestBytes = PeerRuntimeBarrierCodec
            .EncodeLocalConceptualRuntimeOwnerManifest(
                localManifest,
                localManifestExpectation);
        AssertManifestEnvelopeLengthsRejected(
            localManifestBytes,
            localManifestExpectation,
            localConceptual: true);
        AssertEveryManifestByteBound(
            localManifestBytes,
            localManifestExpectation,
            localConceptual: true);
        foreach (int digestOffset in new[] { 163, 195, 227, 259, 291, 323 })
        {
            AssertManifestDigestZeroRejected(
                localManifestBytes,
                localManifestExpectation,
                digestOffset,
                localConceptual: true);
        }

        PeerRuntimeReady localReady = CreateReady(localManifest, 0, true, 11);
        PeerBarrierExpectation localReadyExpectation = Expect(
            PeerBarrierMessageKind.RuntimeReady,
            0,
            0,
            PeerRuntimeMode.Pve,
            1);
        byte[] localReadyBytes = PeerRuntimeBarrierCodec
            .EncodeLocalConceptualRuntimeReady(
                localReady,
                localReadyExpectation,
                localManifest);
        AssertReadyEnvelopeLengthsRejected(
            localReadyBytes,
            localReadyExpectation,
            localManifest,
            localConceptual: true);
        AssertEveryReadyByteBound(
            localReadyBytes,
            localReadyExpectation,
            localManifest,
            localConceptual: true);
        foreach (int digestOffset in new[] { 155, 187, 219, 251, 283, 315 })
        {
            AssertReadyDigestZeroRejected(
                localReadyBytes,
                localReadyExpectation,
                localManifest,
                digestOffset,
                localConceptual: true);
        }
    }

    private static void TestRuntimeReadySet()
    {
        PeerRuntimeOwnerManifest soloManifest = CreateManifest(
            PeerRuntimeMode.Pve,
            1,
            1);
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.BuildRuntimeReadySet(
                soloManifest,
                new[] { CreateReady(soloManifest, 0, true, 11) }));
        PeerBarrierExpectation localManifestExpectation = Expect(
            PeerBarrierMessageKind.RuntimeOwnerManifest,
            0,
            0,
            PeerRuntimeMode.Pve,
            1);
        PeerRuntimeOwnerManifest conceptualSoloManifest =
            PeerRuntimeBarrierCodec.BuildLocalConceptualRuntimeOwnerManifest(
                soloManifest,
                localManifestExpectation);
        Assert(PeerRuntimeBarrierCodec
            .TryValidateLocalConceptualRuntimeOwnerManifest(
                conceptualSoloManifest,
                localManifestExpectation,
                out string localManifestError), localManifestError);
        byte[] conceptualManifestBytes = PeerRuntimeBarrierCodec
            .EncodeLocalConceptualRuntimeOwnerManifest(
                conceptualSoloManifest,
                localManifestExpectation);
        Assert(conceptualManifestBytes.Length == 355);
        Assert(PeerRuntimeBarrierCodec
            .TryDecodeLocalConceptualRuntimeOwnerManifest(
                conceptualManifestBytes,
                localManifestExpectation,
                out PeerRuntimeOwnerManifest decodedConceptualManifest,
                out localManifestError), localManifestError);
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodeRuntimeOwnerManifestForFrozenGeneration(
                conceptualSoloManifest,
                localManifestExpectation));
        Assert(!PeerRuntimeBarrierCodec.TryDecodeRuntimeOwnerManifest(
            conceptualManifestBytes,
            localManifestExpectation,
            out _,
            out _));
        PeerRuntimeReady soloReady = CreateReady(
            decodedConceptualManifest,
            0,
            true,
            11);
        PeerBarrierExpectation soloReadyExpectation = Expect(
            PeerBarrierMessageKind.RuntimeReady,
            0,
            0,
            PeerRuntimeMode.Pve,
            1);
        byte[] soloReadyBytes =
            PeerRuntimeBarrierCodec.EncodeLocalConceptualRuntimeReady(
                soloReady,
                soloReadyExpectation,
                decodedConceptualManifest);
        Assert(PeerRuntimeBarrierCodec.TryDecodeLocalConceptualRuntimeReady(
            soloReadyBytes,
            soloReadyExpectation,
            decodedConceptualManifest,
            out PeerRuntimeReady decodedSoloReady,
            out localManifestError), localManifestError);
        PeerRuntimeReadySet soloSet = PeerRuntimeBarrierCodec.BuildRuntimeReadySet(
            decodedConceptualManifest,
            new[] { decodedSoloReady });
        Assert(soloSet.ParticipantCount == 1 && soloSet.Receipts.Count == 1);

        PeerRuntimeOwnerManifest manifest = CreateManifest(
            PeerRuntimeMode.Pve,
            1,
            3);
        PeerRuntimeOwnerManifest secondRecipientManifest = CreateManifest(
            PeerRuntimeMode.Pve,
            2,
            3);
        Assert(PeerRuntimeBarrierCodec.DigestEquals(
            manifest.HostRuntimeProofDigest,
            secondRecipientManifest.HostRuntimeProofDigest));
        Assert(PeerRuntimeBarrierCodec.DigestEquals(
            manifest.ManifestDigest,
            secondRecipientManifest.ManifestDigest));
        PeerRuntimeOwnerManifest changedManifestField = CopyManifest(
            manifest,
            manifestDigest: Sequential(32, 0x11));
        Assert(PeerRuntimeBarrierCodec.DigestEquals(
            PeerRuntimeBarrierCodec.ComputeHostRuntimeProofDigest(manifest),
            PeerRuntimeBarrierCodec.ComputeHostRuntimeProofDigest(
                changedManifestField)));
        PeerRuntimeReady host = CreateReady(manifest, 0, true, 11);
        PeerRuntimeReady first = CreateReady(manifest, 1, false, 21);
        PeerRuntimeReady second = CreateReady(manifest, 2, false, 22);
        PeerRuntimeReady secondSameGeneration = CreateReady(
            manifest,
            2,
            false,
            21);
        Assert(!PeerRuntimeBarrierCodec.DigestEquals(
            first.ReceiptDigest,
            secondSameGeneration.ReceiptDigest));
        PeerRuntimeReadySet ordered = PeerRuntimeBarrierCodec.BuildRuntimeReadySet(
            manifest,
            new[] { host, first, second });
        PeerRuntimeReadySet reversed = PeerRuntimeBarrierCodec.BuildRuntimeReadySet(
            manifest,
            new[] { second, host, first });
        Assert(PeerRuntimeBarrierCodec.DigestEquals(
            ordered.ReadySetDigest,
            reversed.ReadySetDigest));
        Assert(ordered.Receipts.Select(value => value.Slot)
            .SequenceEqual(new byte[] { 0, 1, 2 }));
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.BuildRuntimeReadySet(
                manifest,
                new[] { host, first }));
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.BuildRuntimeReadySet(
                manifest,
                new[] { host, first, first }));

        PeerRuntimeReadySlotReceipt[] changedRoute = ordered.Receipts.ToArray();
        changedRoute[1] = new PeerRuntimeReadySlotReceipt(
            1,
            PeerRuntimeProofRoute.HostServerLocalAdoption,
            changedRoute[1].ReceiptDigest);
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.ComputeRuntimeReadySetDigest(
                manifest.Header.IdentityDigest,
                manifest.Header.SceneGenerationEpoch,
                manifest.Header.GenerationKey,
                manifest.ManifestDigest,
                3,
                changedRoute));
    }

    private static void AssertManifestMutationRejected(
        byte[] source,
        PeerBarrierExpectation expectation,
        int offset,
        byte xor)
    {
        byte[] changed = (byte[])source.Clone();
        changed[offset] ^= xor;
        Assert(!PeerRuntimeBarrierCodec.TryDecodeRuntimeOwnerManifest(
            changed,
            expectation,
            out _,
            out _));
    }

    private static void AssertManifestEnvelopeLengthsRejected(
        byte[] source,
        PeerBarrierExpectation expectation,
        bool localConceptual)
    {
        byte[] shorter = source.Take(source.Length - 1).ToArray();
        byte[] longer = source.Concat(new byte[] { 0 }).ToArray();
        Assert(!TryDecodeManifest(
            shorter,
            expectation,
            localConceptual));
        Assert(!TryDecodeManifest(
            longer,
            expectation,
            localConceptual));
    }

    private static void AssertEveryManifestByteBound(
        byte[] source,
        PeerBarrierExpectation expectation,
        bool localConceptual)
    {
        // This binds every message-id/protocol/kind, slot, nonce, identity,
        // epoch, generation-key, scalar, lifecycle, and digest byte.
        for (int offset = 0; offset < source.Length; offset++)
        {
            byte[] changed = (byte[])source.Clone();
            changed[offset] ^= 0x01;
            Assert(!TryDecodeManifest(
                changed,
                expectation,
                localConceptual),
                "RuntimeOwnerManifest accepted mutation at byte " + offset);
        }
    }

    private static void AssertManifestDigestZeroRejected(
        byte[] source,
        PeerBarrierExpectation expectation,
        int offset,
        bool localConceptual)
    {
        byte[] changed = (byte[])source.Clone();
        Array.Clear(changed, offset, 32);
        Assert(!TryDecodeManifest(changed, expectation, localConceptual),
            "RuntimeOwnerManifest accepted zero digest at byte " + offset);
    }

    private static void AssertManifestFieldReversalRejected(
        byte[] source,
        PeerBarrierExpectation expectation,
        int offset,
        int length,
        bool localConceptual)
    {
        byte[] changed = (byte[])source.Clone();
        Array.Reverse(changed, offset, length);
        Assert(!changed.SequenceEqual(source),
            "manifest endian fixture must change its field");
        Assert(!TryDecodeManifest(changed, expectation, localConceptual),
            "RuntimeOwnerManifest accepted reversed field at byte " + offset);
    }

    private static bool TryDecodeManifest(
        byte[] envelope,
        PeerBarrierExpectation expectation,
        bool localConceptual)
    {
        return localConceptual
            ? PeerRuntimeBarrierCodec.TryDecodeLocalConceptualRuntimeOwnerManifest(
                envelope,
                expectation,
                out _,
                out _)
            : PeerRuntimeBarrierCodec.TryDecodeRuntimeOwnerManifest(
                envelope,
                expectation,
                out _,
                out _);
    }

    private static void AssertReadyEnvelopeLengthsRejected(
        byte[] source,
        PeerBarrierExpectation expectation,
        PeerRuntimeOwnerManifest acceptedManifest,
        bool localConceptual)
    {
        byte[] shorter = source.Take(source.Length - 1).ToArray();
        byte[] longer = source.Concat(new byte[] { 0 }).ToArray();
        Assert(!TryDecodeReady(
            shorter,
            expectation,
            acceptedManifest,
            localConceptual));
        Assert(!TryDecodeReady(
            longer,
            expectation,
            acceptedManifest,
            localConceptual));
    }

    private static void AssertEveryReadyByteBound(
        byte[] source,
        PeerBarrierExpectation expectation,
        PeerRuntimeOwnerManifest acceptedManifest,
        bool localConceptual)
    {
        // This binds every message-id/protocol/kind, slot, nonce, identity,
        // epoch, generation-key, scalar, lifecycle, and digest byte.
        for (int offset = 0; offset < source.Length; offset++)
        {
            byte[] changed = (byte[])source.Clone();
            changed[offset] ^= 0x01;
            Assert(!TryDecodeReady(
                changed,
                expectation,
                acceptedManifest,
                localConceptual),
                "RuntimeReady accepted mutation at byte " + offset);
        }
    }

    private static void AssertReadyDigestZeroRejected(
        byte[] source,
        PeerBarrierExpectation expectation,
        PeerRuntimeOwnerManifest acceptedManifest,
        int offset,
        bool localConceptual)
    {
        byte[] changed = (byte[])source.Clone();
        Array.Clear(changed, offset, 32);
        Assert(!TryDecodeReady(
            changed,
            expectation,
            acceptedManifest,
            localConceptual),
            "RuntimeReady accepted zero digest at byte " + offset);
    }

    private static void AssertReadyFieldReversalRejected(
        byte[] source,
        PeerBarrierExpectation expectation,
        PeerRuntimeOwnerManifest acceptedManifest,
        int offset,
        int length,
        bool localConceptual)
    {
        byte[] changed = (byte[])source.Clone();
        Array.Reverse(changed, offset, length);
        Assert(!changed.SequenceEqual(source),
            "RuntimeReady endian fixture must change its field");
        Assert(!TryDecodeReady(
            changed,
            expectation,
            acceptedManifest,
            localConceptual),
            "RuntimeReady accepted reversed field at byte " + offset);
    }

    private static bool TryDecodeReady(
        byte[] envelope,
        PeerBarrierExpectation expectation,
        PeerRuntimeOwnerManifest acceptedManifest,
        bool localConceptual)
    {
        return localConceptual
            ? PeerRuntimeBarrierCodec.TryDecodeLocalConceptualRuntimeReady(
                envelope,
                expectation,
                acceptedManifest,
                out _,
                out _)
            : PeerRuntimeBarrierCodec.TryDecodeRuntimeReady(
                envelope,
                expectation,
                acceptedManifest,
                out _,
                out _);
    }

    private static void TestRuntimeDigestDependencies()
    {
        IReadOnlyDictionary<
            PeerRuntimeDigestNode,
            IReadOnlyCollection<PeerRuntimeDigestNode>> registry =
            PeerRuntimeBarrierCodec.CreateRuntimeDigestDependencyRegistry();
        Assert(PeerRuntimeBarrierCodec.TryTopologicallySortRuntimeDigestDependencies(
            registry,
            out System.Collections.ObjectModel.ReadOnlyCollection<
                PeerRuntimeDigestNode> sorted,
            out string error), error);
        Assert(sorted.SequenceEqual(Enum.GetValues(typeof(PeerRuntimeDigestNode))
            .Cast<PeerRuntimeDigestNode>()
            .OrderBy(value => (byte)value)));

        AssertDependencyRegistryRejected(registry, dependencies =>
            dependencies[PeerRuntimeDigestNode.PreSceneClone].Add(
                PeerRuntimeDigestNode.SceneContract));
        AssertDependencyRegistryRejected(registry, dependencies =>
            dependencies[PeerRuntimeDigestNode.PreSceneClone].Add(
                PeerRuntimeDigestNode.GenerationKey));
        AssertDependencyRegistryRejected(registry, dependencies =>
            dependencies[PeerRuntimeDigestNode.HostRuntimeProof].Add(
                PeerRuntimeDigestNode.RuntimeOwnerManifest));
        AssertDependencyRegistryRejected(registry, dependencies =>
            dependencies[PeerRuntimeDigestNode.SceneContract].Add(
                PeerRuntimeDigestNode.SceneContract));
        AssertDependencyRegistryRejected(registry, dependencies =>
            dependencies[PeerRuntimeDigestNode.SceneContract].Add(
                PeerRuntimeDigestNode.PreSceneClone));
        AssertDependencyRegistryRejected(registry, dependencies =>
            dependencies[PeerRuntimeDigestNode.SceneContract].Add(
                (PeerRuntimeDigestNode)255));

        Dictionary<PeerRuntimeDigestNode, List<PeerRuntimeDigestNode>> unknown =
            CloneRegistry(registry);
        unknown.Remove(PeerRuntimeDigestNode.BeginContract);
        unknown[(PeerRuntimeDigestNode)255] = new List<PeerRuntimeDigestNode>();
        Assert(!TrySortMutableRegistry(unknown));
    }

    private static void TestRuntimeOwnerImmutability()
    {
        byte[] mutableScene = (byte[])Scene.Clone();
        PeerRuntimeOwnerManifest manifest = CreateManifest(
            PeerRuntimeMode.Pve,
            1,
            2,
            mutableScene);
        byte expectedScene = manifest.SceneContractDigest[0];
        mutableScene[0] ^= 0x55;
        Assert(manifest.SceneContractDigest[0] == expectedScene);
        byte[] exposedScene = manifest.SceneContractDigest;
        exposedScene[0] ^= 0x55;
        Assert(manifest.SceneContractDigest[0] == expectedScene);

        PeerRuntimeReady ready = CreateReady(manifest, 1, false, 22);
        byte expectedManifest = ready.ManifestDigest[0];
        byte[] exposedManifest = ready.ManifestDigest;
        exposedManifest[0] ^= 0x55;
        Assert(ready.ManifestDigest[0] == expectedManifest);
        PeerRuntimeReadySet set = PeerRuntimeBarrierCodec.BuildRuntimeReadySet(
            manifest,
            new[]
            {
                CreateReady(manifest, 0, true, 11),
                ready
            });
        byte expectedReceipt = set.Receipts[0].ReceiptDigest[0];
        byte[] exposedReceipt = set.Receipts[0].ReceiptDigest;
        exposedReceipt[0] ^= 0x55;
        Assert(set.Receipts[0].ReceiptDigest[0] == expectedReceipt);

        string[] forbidden = { "Loader", "Backend", "PatchReceipt", "Pointer" };
        string[] propertyNames = typeof(PeerRuntimeOwnerManifest)
            .GetProperties(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public)
            .Select(value => value.Name)
            .Concat(typeof(PeerRuntimeReady)
                .GetProperties(
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public)
                .Select(value => value.Name))
            .ToArray();
        Assert(!propertyNames.Any(name => forbidden.Any(term =>
            name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)));
    }

    private static void TestLoaderNeutralRuntimeVectors()
    {
        byte[] bepLoaderLocalReceipt = Sequential(32, 0x07);
        byte[] melonLoaderLocalReceipt = Sequential(32, 0xE7);
        Assert(!bepLoaderLocalReceipt.SequenceEqual(melonLoaderLocalReceipt));
        Assert(!PeerRuntimeBarrierCodec.RuntimeSemanticDigestBuildersComplete);

        foreach (PeerRuntimeMode mode in new[]
        {
            PeerRuntimeMode.Pve,
            PeerRuntimeMode.Pvp
        })
        {
            PeerBarrierExpectation manifestExpectation = Expect(
                PeerBarrierMessageKind.RuntimeOwnerManifest,
                0,
                1,
                mode,
                2);
            PeerRuntimeOwnerManifest bepManifest = CreateManifest(mode, 1, 2);
            PeerRuntimeOwnerManifest melonManifest = CreateManifest(mode, 1, 2);
            byte[] bepManifestEnvelope = PeerRuntimeBarrierCodec
                .EncodeRuntimeOwnerManifestForFrozenGeneration(
                    bepManifest,
                    manifestExpectation);
            byte[] melonManifestEnvelope = PeerRuntimeBarrierCodec
                .EncodeRuntimeOwnerManifestForFrozenGeneration(
                    melonManifest,
                    manifestExpectation);
            Assert(bepManifestEnvelope.SequenceEqual(melonManifestEnvelope),
                "same semantic source must produce a loader-neutral manifest");

            PeerBarrierExpectation readyExpectation = Expect(
                PeerBarrierMessageKind.RuntimeReady,
                1,
                0,
                mode,
                2);
            byte[] bepReadyEnvelope = PeerRuntimeBarrierCodec
                .EncodeRuntimeReadyForAcceptedManifest(
                    CreateReady(bepManifest, 1, false, 22),
                    readyExpectation,
                    bepManifest);
            byte[] melonReadyEnvelope = PeerRuntimeBarrierCodec
                .EncodeRuntimeReadyForAcceptedManifest(
                    CreateReady(melonManifest, 1, false, 22),
                    readyExpectation,
                    melonManifest);
            Assert(bepReadyEnvelope.SequenceEqual(melonReadyEnvelope),
                "same semantic source must produce a loader-neutral Ready");
        }

        PeerRuntimeOwnerManifest bepSoloSource = CreateManifest(
            PeerRuntimeMode.Pve,
            1,
            1);
        PeerRuntimeOwnerManifest melonSoloSource = CreateManifest(
            PeerRuntimeMode.Pve,
            1,
            1);
        PeerBarrierExpectation localManifestExpectation = Expect(
            PeerBarrierMessageKind.RuntimeOwnerManifest,
            0,
            0,
            PeerRuntimeMode.Pve,
            1);
        PeerRuntimeOwnerManifest bepSolo = PeerRuntimeBarrierCodec
            .BuildLocalConceptualRuntimeOwnerManifest(
                bepSoloSource,
                localManifestExpectation);
        PeerRuntimeOwnerManifest melonSolo = PeerRuntimeBarrierCodec
            .BuildLocalConceptualRuntimeOwnerManifest(
                melonSoloSource,
                localManifestExpectation);
        Assert(PeerRuntimeBarrierCodec
            .EncodeLocalConceptualRuntimeOwnerManifest(
                bepSolo,
                localManifestExpectation)
            .SequenceEqual(PeerRuntimeBarrierCodec
                .EncodeLocalConceptualRuntimeOwnerManifest(
                    melonSolo,
                    localManifestExpectation)));
        PeerBarrierExpectation localReadyExpectation = Expect(
            PeerBarrierMessageKind.RuntimeReady,
            0,
            0,
            PeerRuntimeMode.Pve,
            1);
        Assert(PeerRuntimeBarrierCodec.EncodeLocalConceptualRuntimeReady(
                CreateReady(bepSolo, 0, true, 11),
                localReadyExpectation,
                bepSolo)
            .SequenceEqual(PeerRuntimeBarrierCodec
                .EncodeLocalConceptualRuntimeReady(
                    CreateReady(melonSolo, 0, true, 11),
                    localReadyExpectation,
                    melonSolo)));
    }

    private static void TestRuntimeOwnerGoldenVectors()
    {
        PeerRuntimeOwnerManifest manifest = CreateManifest(
            PeerRuntimeMode.Pve,
            1,
            2);
        PeerRuntimeReady host = CreateReady(manifest, 0, true, 11);
        PeerRuntimeReady remote = CreateReady(manifest, 1, false, 22);
        PeerRuntimeReadySet set = PeerRuntimeBarrierCodec.BuildRuntimeReadySet(
            manifest,
            new[] { remote, host });
        AssertHex(manifest.HostRuntimeProofDigest,
            "AE4D001824A14EB9152D322A31969F6EB85C7928E8A7CE92F0BFC3302F1E415A");
        AssertHex(manifest.ManifestDigest,
            "ED8C85229F91FC701AA38FC6F3A299BF40C5701D8075B5CCF36B9720360D93BE");
        AssertHex(host.ReceiptDigest,
            "72046EF3D21F2964A80761D3776AF188EE09A3D22137CF901E9405AB3D8E4FF0");
        AssertHex(remote.ReceiptDigest,
            "2318BE319B648146FB0A110C9110D121CC81FF60899497EC1080DE51EF9F8675");
        AssertHex(set.ReadySetDigest,
            "6D7BA3094FB66710BA42C75C6457AE3AA15FA624B9756C0586CB098AF8AF58D2");

        PeerBarrierExpectation manifestExpectation = Expect(
            PeerBarrierMessageKind.RuntimeOwnerManifest,
            0,
            1,
            PeerRuntimeMode.Pve,
            2);
        PeerBarrierExpectation remoteExpectation = Expect(
            PeerBarrierMessageKind.RuntimeReady,
            1,
            0,
            PeerRuntimeMode.Pve,
            2);
        PeerBarrierExpectation hostExpectation = Expect(
            PeerBarrierMessageKind.RuntimeReady,
            0,
            0,
            PeerRuntimeMode.Pve,
            2);
        byte[] manifestEnvelope = PeerRuntimeBarrierCodec
            .EncodeRuntimeOwnerManifestForFrozenGeneration(
                manifest,
                manifestExpectation);
        byte[] remoteEnvelope = PeerRuntimeBarrierCodec
            .EncodeRuntimeReadyForAcceptedManifest(
                remote,
                remoteExpectation,
                manifest);
        byte[] hostEnvelope = PeerRuntimeBarrierCodec
            .EncodeLocalConceptualRuntimeReady(
                host,
                hostExpectation,
                manifest);
        PeerBarrierExpectation soloManifestExpectation = Expect(
            PeerBarrierMessageKind.RuntimeOwnerManifest,
            0,
            0,
            PeerRuntimeMode.Pve,
            1);
        PeerRuntimeOwnerManifest soloManifest = PeerRuntimeBarrierCodec
            .BuildLocalConceptualRuntimeOwnerManifest(
                CreateManifest(PeerRuntimeMode.Pve, 1, 1),
                soloManifestExpectation);
        byte[] soloManifestEnvelope = PeerRuntimeBarrierCodec
            .EncodeLocalConceptualRuntimeOwnerManifest(
                soloManifest,
                soloManifestExpectation);
        PeerBarrierExpectation soloReadyExpectation = Expect(
            PeerBarrierMessageKind.RuntimeReady,
            0,
            0,
            PeerRuntimeMode.Pve,
            1);
        byte[] soloReadyEnvelope = PeerRuntimeBarrierCodec
            .EncodeLocalConceptualRuntimeReady(
                CreateReady(soloManifest, 0, true, 11),
                soloReadyExpectation,
                soloManifest);
        AssertHex(manifestEnvelope,
            "4F4D06001200000000010000003132333435363738393A3B3C3D3E3F405152535455565758595A5B5C5D5E5F606162636465666768696A6B6C6D6E6F700900000000000000C2996BAD2D8E4C5AB4BB1962E0E36473202E3036BDD00B8FCC2EA567CAC1358D01000101FF0F0000FFFF0300FFFF070002010400070029230000DDCCBBAA00000000000000000B00000000000000000101000101010000000000000000007172737475767778797A7B7C7D7E7F808182838485868788898A8B8C8D8E8F909192939495969798999A9B9C9D9E9FA0A1A2A3A4A5A6A7A8A9AAABACADAEAFB0B1B2B3B4B5B6B7B8B9BABBBCBDBEBFC0C1C2C3C4C5C6C7C8C9CACBCCCDCECFD0D1D2D3D4D5D6D7D8D9DADBDCDDDEDFE0E1E2E3E4E5E6E7E8E9EAEBECEDEEEFF0AE4D001824A14EB9152D322A31969F6EB85C7928E8A7CE92F0BFC3302F1E415AED8C85229F91FC701AA38FC6F3A299BF40C5701D8075B5CCF36B9720360D93BE");
        AssertHex(remoteEnvelope,
            "4F4D06001301000000000000003132333435363738393A3B3C3D3E3F405152535455565758595A5B5C5D5E5F606162636465666768696A6B6C6D6E6F700900000000000000C2996BAD2D8E4C5AB4BB1962E0E36473202E3036BDD00B8FCC2EA567CAC1358D01000102FFFF0B0002010400070029230000DDCCBBAA00000000000000001600000000000000010101010001010000000000000000007172737475767778797A7B7C7D7E7F808182838485868788898A8B8C8D8E8F909192939495969798999A9B9C9D9E9FA0A1A2A3A4A5A6A7A8A9AAABACADAEAFB0B1B2B3B4B5B6B7B8B9BABBBCBDBEBFC0C1C2C3C4C5C6C7C8C9CACBCCCDCECFD0D1D2D3D4D5D6D7D8D9DADBDCDDDEDFE0E1E2E3E4E5E6E7E8E9EAEBECEDEEEFF0ED8C85229F91FC701AA38FC6F3A299BF40C5701D8075B5CCF36B9720360D93BE2318BE319B648146FB0A110C9110D121CC81FF60899497EC1080DE51EF9F8675");
        AssertHex(hostEnvelope,
            "4F4D06001300000000000000003132333435363738393A3B3C3D3E3F405152535455565758595A5B5C5D5E5F606162636465666768696A6B6C6D6E6F700900000000000000C2996BAD2D8E4C5AB4BB1962E0E36473202E3036BDD00B8FCC2EA567CAC1358D01000101FFFF070002010400070029230000DDCCBBAA00000000000000000B00000000000000000101000101010000000000000000007172737475767778797A7B7C7D7E7F808182838485868788898A8B8C8D8E8F909192939495969798999A9B9C9D9E9FA0A1A2A3A4A5A6A7A8A9AAABACADAEAFB0B1B2B3B4B5B6B7B8B9BABBBCBDBEBFC0C1C2C3C4C5C6C7C8C9CACBCCCDCECFD0D1D2D3D4D5D6D7D8D9DADBDCDDDEDFE0E1E2E3E4E5E6E7E8E9EAEBECEDEEEFF0ED8C85229F91FC701AA38FC6F3A299BF40C5701D8075B5CCF36B9720360D93BE72046EF3D21F2964A80761D3776AF188EE09A3D22137CF901E9405AB3D8E4FF0");
        AssertHex(soloManifestEnvelope,
            "4F4D06001200000000000000003132333435363738393A3B3C3D3E3F405152535455565758595A5B5C5D5E5F606162636465666768696A6B6C6D6E6F700900000000000000C2996BAD2D8E4C5AB4BB1962E0E36473202E3036BDD00B8FCC2EA567CAC1358D01000101FF0F0000FFFF0300FFFF070001010400070029230000DDCCBBAA00000000000000000B00000000000000000101000101010000000000000000007172737475767778797A7B7C7D7E7F808182838485868788898A8B8C8D8E8F909192939495969798999A9B9C9D9E9FA0A1A2A3A4A5A6A7A8A9AAABACADAEAFB0B1B2B3B4B5B6B7B8B9BABBBCBDBEBFC0C1C2C3C4C5C6C7C8C9CACBCCCDCECFD0D1D2D3D4D5D6D7D8D9DADBDCDDDEDFE0E1E2E3E4E5E6E7E8E9EAEBECEDEEEFF090CCD36855B9EE3F94F4BBBEB03748413CB192ED5343DFAE71F278CCE917C3481DA2A032240C5A1CF19C62654D8C0EA10BE7F0B74FC95CE2A4F4993B2AED9AB4");
        AssertHex(soloReadyEnvelope,
            "4F4D06001300000000000000003132333435363738393A3B3C3D3E3F405152535455565758595A5B5C5D5E5F606162636465666768696A6B6C6D6E6F700900000000000000C2996BAD2D8E4C5AB4BB1962E0E36473202E3036BDD00B8FCC2EA567CAC1358D01000101FFFF070001010400070029230000DDCCBBAA00000000000000000B00000000000000000101000101010000000000000000007172737475767778797A7B7C7D7E7F808182838485868788898A8B8C8D8E8F909192939495969798999A9B9C9D9E9FA0A1A2A3A4A5A6A7A8A9AAABACADAEAFB0B1B2B3B4B5B6B7B8B9BABBBCBDBEBFC0C1C2C3C4C5C6C7C8C9CACBCCCDCECFD0D1D2D3D4D5D6D7D8D9DADBDCDDDEDFE0E1E2E3E4E5E6E7E8E9EAEBECEDEEEFF01DA2A032240C5A1CF19C62654D8C0EA10BE7F0B74FC95CE2A4F4993B2AED9AB44393BDCCC3677F97AB605AC00ADDC09E8B4D3B9AFF01D589FDDFBE0AFAD41D5D");
    }

    private static PeerRuntimeOwnerManifest CreateManifest(
        PeerRuntimeMode mode,
        int recipientSlot,
        int participantCount,
        byte[] sceneOverride = null,
        ushort networkBehaviourCount = 4,
        ushort totalSyncObjectCount = 7)
    {
        byte[] scene = sceneOverride ?? Scene;
        PeerBarrierHeader header = Header(
            PeerBarrierMessageKind.RuntimeOwnerManifest,
            0,
            recipientSlot,
            mode,
            scene);
        PeerRuntimeLifecycleProof lifecycle = HostLifecycle(mode);
        var pendingHostProof = new PeerRuntimeOwnerManifest(
            header,
            PeerRuntimeBarrierCodec.RuntimeOwnerSchema,
            mode,
            PeerRuntimeProofRoute.HostServerLocalAdoption,
            PeerRuntimeBarrierCodec.RuntimeOwnerContractFlags,
            PeerRuntimeBarrierCodec.RuntimeRequiredCommonStatusMask,
            PeerRuntimeBarrierCodec.RuntimeHostObservedStatusMask,
            checked((byte)participantCount),
            1,
            networkBehaviourCount,
            totalSyncObjectCount,
            9001,
            0xAABBCCDD,
            0,
            11,
            lifecycle,
            scene,
            Template,
            PreScene,
            RuntimeContract,
            new byte[32],
            new byte[32]);
        byte[] hostProof =
            PeerRuntimeBarrierCodec.ComputeHostRuntimeProofDigest(pendingHostProof);
        var pendingManifest = new PeerRuntimeOwnerManifest(
            header,
            pendingHostProof.RuntimeSchema,
            pendingHostProof.Mode,
            pendingHostProof.HostProofRoute,
            pendingHostProof.ContractFlags,
            pendingHostProof.RequiredCommonStatusMask,
            pendingHostProof.HostObservedStatusMask,
            pendingHostProof.ParticipantCount,
            pendingHostProof.OwnerSpawnAttemptOrdinal,
            pendingHostProof.NetworkBehaviourCount,
            pendingHostProof.TotalSyncObjectCount,
            pendingHostProof.RuntimeOwnerNetId,
            pendingHostProof.DeterministicAssetId,
            pendingHostProof.NetworkIdentitySceneId,
            pendingHostProof.HostLocalSceneGeneration,
            pendingHostProof.LifecycleProof,
            pendingHostProof.SceneContractDigest,
            pendingHostProof.ExactNativeTemplateShapeDigest,
            pendingHostProof.PreSceneCloneDigest,
            pendingHostProof.RuntimeOwnerContractDigest,
            hostProof,
            new byte[32]);
        byte[] manifest =
            PeerRuntimeBarrierCodec.ComputeRuntimeOwnerManifestDigest(pendingManifest);
        return new PeerRuntimeOwnerManifest(
            header,
            pendingManifest.RuntimeSchema,
            pendingManifest.Mode,
            pendingManifest.HostProofRoute,
            pendingManifest.ContractFlags,
            pendingManifest.RequiredCommonStatusMask,
            pendingManifest.HostObservedStatusMask,
            pendingManifest.ParticipantCount,
            pendingManifest.OwnerSpawnAttemptOrdinal,
            pendingManifest.NetworkBehaviourCount,
            pendingManifest.TotalSyncObjectCount,
            pendingManifest.RuntimeOwnerNetId,
            pendingManifest.DeterministicAssetId,
            pendingManifest.NetworkIdentitySceneId,
            pendingManifest.HostLocalSceneGeneration,
            pendingManifest.LifecycleProof,
            pendingManifest.SceneContractDigest,
            pendingManifest.ExactNativeTemplateShapeDigest,
            pendingManifest.PreSceneCloneDigest,
            pendingManifest.RuntimeOwnerContractDigest,
            pendingManifest.HostRuntimeProofDigest,
            manifest);
    }

    private static PeerRuntimeReady CreateReady(
        PeerRuntimeOwnerManifest manifest,
        int slot,
        bool localConceptual,
        ulong localGeneration)
    {
        PeerRuntimeProofRoute route = localConceptual
            ? PeerRuntimeProofRoute.HostServerLocalAdoption
            : PeerRuntimeProofRoute.RemoteCustomHandler;
        var header = new PeerBarrierHeader(
            PeerBarrierMessageKind.RuntimeReady,
            slot,
            0,
            manifest.Header.SessionNonce,
            manifest.Header.IdentityDigest,
            manifest.Header.SceneGenerationEpoch,
            manifest.Header.GenerationKey);
        var pending = new PeerRuntimeReady(
            header,
            PeerRuntimeBarrierCodec.RuntimeOwnerSchema,
            manifest.Mode,
            route,
            localConceptual
                ? PeerRuntimeBarrierCodec.RuntimeHostObservedStatusMask
                : PeerRuntimeBarrierCodec.RuntimeRemoteObservedStatusMask,
            manifest.ParticipantCount,
            manifest.OwnerSpawnAttemptOrdinal,
            manifest.NetworkBehaviourCount,
            manifest.TotalSyncObjectCount,
            manifest.RuntimeOwnerNetId,
            manifest.DeterministicAssetId,
            0,
            localGeneration,
            localConceptual
                ? HostLifecycle(manifest.Mode)
                : RemoteLifecycle(),
            manifest.SceneContractDigest,
            manifest.ExactNativeTemplateShapeDigest,
            manifest.PreSceneCloneDigest,
            manifest.RuntimeOwnerContractDigest,
            manifest.ManifestDigest,
            new byte[32]);
        return new PeerRuntimeReady(
            header,
            pending.RuntimeSchema,
            pending.Mode,
            pending.ProofRoute,
            pending.ObservedStatusMask,
            pending.ParticipantCount,
            pending.OwnerSpawnAttemptOrdinal,
            pending.NetworkBehaviourCount,
            pending.TotalSyncObjectCount,
            pending.RuntimeOwnerNetId,
            pending.DeterministicAssetId,
            pending.NetworkIdentitySceneId,
            pending.SenderLocalSceneGeneration,
            pending.LifecycleProof,
            pending.SceneContractDigest,
            pending.ExactNativeTemplateShapeDigest,
            pending.PreSceneCloneDigest,
            pending.RuntimeOwnerContractDigest,
            pending.ManifestDigest,
            PeerRuntimeBarrierCodec.ComputeRuntimeReadyReceiptDigest(pending));
    }

    private static PeerRuntimeOwnerManifest CopyManifest(
        PeerRuntimeOwnerManifest source,
        byte[] manifestDigest = null)
    {
        return new PeerRuntimeOwnerManifest(
            source.Header,
            source.RuntimeSchema,
            source.Mode,
            source.HostProofRoute,
            source.ContractFlags,
            source.RequiredCommonStatusMask,
            source.HostObservedStatusMask,
            source.ParticipantCount,
            source.OwnerSpawnAttemptOrdinal,
            source.NetworkBehaviourCount,
            source.TotalSyncObjectCount,
            source.RuntimeOwnerNetId,
            source.DeterministicAssetId,
            source.NetworkIdentitySceneId,
            source.HostLocalSceneGeneration,
            source.LifecycleProof,
            source.SceneContractDigest,
            source.ExactNativeTemplateShapeDigest,
            source.PreSceneCloneDigest,
            source.RuntimeOwnerContractDigest,
            source.HostRuntimeProofDigest,
            manifestDigest ?? source.ManifestDigest);
    }

    private static PeerRuntimeLifecycleProof HostLifecycle(PeerRuntimeMode mode)
    {
        return new PeerRuntimeLifecycleProof(
            0,
            1,
            1,
            0,
            checked((byte)(mode == PeerRuntimeMode.Pve ? 1 : 0)),
            1,
            1,
            0,
            0,
            0,
            0);
    }

    private static PeerRuntimeLifecycleProof RemoteLifecycle()
    {
        return new PeerRuntimeLifecycleProof(
            1,
            1,
            1,
            1,
            0,
            1,
            1,
            0,
            0,
            0,
            0);
    }

    private static PeerBarrierHeader Header(
        PeerBarrierMessageKind kind,
        int sender,
        int recipient,
        PeerRuntimeMode mode,
        byte[] scene)
    {
        int requested = mode == PeerRuntimeMode.Pve ? 3 : 0;
        return new PeerBarrierHeader(
            kind,
            sender,
            recipient,
            Nonce,
            Identity,
            9,
            PeerRuntimeBarrierCodec.ComputeGenerationKey(
                Nonce,
                Identity,
                9,
                scene,
                requested));
    }

    private static PeerBarrierExpectation Expect(
        PeerBarrierMessageKind kind,
        int sender,
        int recipient,
        PeerRuntimeMode mode,
        int participantCount)
    {
        int requested = mode == PeerRuntimeMode.Pve ? 3 : 0;
        PeerBarrierHeader header = Header(
            kind,
            sender,
            recipient,
            mode,
            Scene);
        return new PeerBarrierExpectation(
            kind,
            sender,
            recipient,
            header.SessionNonce,
            header.IdentityDigest,
            header.SceneGenerationEpoch,
            header.GenerationKey,
            Scene,
            participantCount,
            requested,
            mode == PeerRuntimeMode.Pve);
    }

    private static void AssertDependencyRegistryRejected(
        IReadOnlyDictionary<
            PeerRuntimeDigestNode,
            IReadOnlyCollection<PeerRuntimeDigestNode>> source,
        Action<Dictionary<
            PeerRuntimeDigestNode,
            List<PeerRuntimeDigestNode>>> mutate)
    {
        Dictionary<PeerRuntimeDigestNode, List<PeerRuntimeDigestNode>> changed =
            CloneRegistry(source);
        mutate(changed);
        Assert(!TrySortMutableRegistry(changed));
    }

    private static Dictionary<PeerRuntimeDigestNode, List<PeerRuntimeDigestNode>>
        CloneRegistry(IReadOnlyDictionary<
            PeerRuntimeDigestNode,
            IReadOnlyCollection<PeerRuntimeDigestNode>> source)
    {
        return source.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToList());
    }

    private static bool TrySortMutableRegistry(
        Dictionary<PeerRuntimeDigestNode, List<PeerRuntimeDigestNode>> source)
    {
        var projected = source.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyCollection<PeerRuntimeDigestNode>)pair.Value);
        return PeerRuntimeBarrierCodec.TryTopologicallySortRuntimeDigestDependencies(
            projected,
            out _,
            out _);
    }

    private static byte[] Sequential(int length, byte start)
    {
        var result = new byte[length];
        for (int index = 0; index < length; index++)
            result[index] = unchecked((byte)(start + index));
        return result;
    }

    private static void AssertHex(byte[] value, string expected)
    {
        string actual = Convert.ToHexString(value);
        Assert(actual == expected, "expected " + expected + ", actual " + actual);
    }

    private static void Assert(bool condition, string message = null)
    {
        if (!condition)
            throw new InvalidOperationException(message ?? "assertion failed");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException(
            "expected exception " + typeof(T).Name);
    }
}
