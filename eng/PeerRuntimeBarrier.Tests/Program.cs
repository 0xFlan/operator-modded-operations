using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OperatorModdedOperations.PeerProtocol;

internal static class Program
{
    private static readonly byte[] Nonce = Sequential(16, 0x01);
    private static readonly byte[] Identity = Sequential(32, 0x21);
    private static readonly byte[] Scene = Sequential(32, 0x61);

    private static int Main()
    {
        try
        {
            TestCanonicalPrimitives();
            TestPopulationMessages();
            TestBeginMessages();
            TestPlacementMessages();
            RuntimeOwnerCodecTests.Run();
            TestImmutability();
            TestGoldenVectors();
            Console.WriteLine("Peer runtime barrier tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static void TestCanonicalPrimitives()
    {
        Assert(PeerRuntimeBarrierCodec.TryDecodeCanonicalSessionNonce(
            "00112233445566778899aabbccddeeff", out byte[] nonce));
        Assert(nonce.Length == 16);
        Assert(!PeerRuntimeBarrierCodec.TryDecodeCanonicalSessionNonce(
            "00112233445566778899AABBCCDDEEFF", out _));
        Assert(!PeerRuntimeBarrierCodec.TryDecodeCanonicalDigest(
            new string('a', 63), out _));

        Assert(PeerRuntimeBarrierCodec.TryQuantizeMillimetres(0.0005, out int pos) &&
            pos == 1);
        Assert(PeerRuntimeBarrierCodec.TryQuantizeMillimetres(-0.0005, out int neg) &&
            neg == -1);
        Assert(!PeerRuntimeBarrierCodec.TryQuantizeMillimetres(double.NaN, out _));
        Assert(!PeerRuntimeBarrierCodec.TryQuantizeMillimetres(
            int.MaxValue / 1000d + 1d, out _));
        Assert(PeerRuntimeBarrierCodec.TryQuantizeYawCentidegrees(
            -0.01, out ushort yaw) && yaw == 35999);
        Assert(PeerRuntimeBarrierCodec.TryQuantizeYawCentidegrees(
            359.995, out yaw) && yaw == 0);
        Assert(PeerRuntimeBarrierCodec.WrappedYawErrorCentidegrees(35999, 0) == 1);

        byte[] generation = PeerRuntimeBarrierCodec.ComputeGenerationKey(
            Nonce,
            Identity,
            7,
            Scene,
            3);
        Assert(Convert.ToHexString(generation) ==
            "66816701ADFB7B6E4C87E43BFDF00EFD752302F48B1A548B394C4837F55E30A9");
    }

    private static void TestPopulationMessages()
    {
        PeerPopulationManifest one = CreateManifest(1);
        byte[] oneBytes = PeerRuntimeBarrierCodec.EncodePopulationManifest(one);
        Assert(oneBytes.Length == 237);
        PeerBarrierExpectation oneExpected = Expect(
            PeerBarrierMessageKind.PopulationManifest,
            0,
            1,
            2,
            1,
            true);
        Assert(PeerRuntimeBarrierCodec.TryDecodePopulationManifest(
            oneBytes,
            oneExpected,
            out PeerPopulationManifest oneDecoded,
            out string error), error);
        Assert(oneDecoded.Records.Count == 1);

        PeerPopulationManifest hundred = CreateManifest(100);
        byte[] hundredBytes = PeerRuntimeBarrierCodec.EncodePopulationManifest(hundred);
        Assert(hundredBytes.Length == 3009);
        PeerBarrierExpectation hundredExpected = Expect(
            PeerBarrierMessageKind.PopulationManifest,
            0,
            1,
            2,
            100,
            true);
        Assert(PeerRuntimeBarrierCodec.TryDecodePopulationManifest(
            hundredBytes,
            hundredExpected,
            out _,
            out error), error);
        Throws<ArgumentOutOfRangeException>(() =>
            CreateManifest(101));

        byte[] trailing = new byte[hundredBytes.Length + 1];
        Buffer.BlockCopy(hundredBytes, 0, trailing, 0, hundredBytes.Length);
        Assert(!PeerRuntimeBarrierCodec.TryDecodePopulationManifest(
            trailing,
            hundredExpected,
            out _,
            out _));
        byte[] badCount = (byte[])hundredBytes.Clone();
        badCount[205] = 0;
        Assert(!PeerRuntimeBarrierCodec.TryDecodePopulationManifest(
            badCount,
            hundredExpected,
            out _,
            out _));
        byte[] zeroPopulationDigest = (byte[])hundredBytes.Clone();
        Array.Clear(zeroPopulationDigest, 133, 32);
        Assert(!PeerRuntimeBarrierCodec.TryDecodePopulationManifest(
            zeroPopulationDigest,
            hundredExpected,
            out _,
            out _));

        PeerPopulationRecord[] duplicate = CreatePopulationRecords(2);
        duplicate[1] = new PeerPopulationRecord(
            duplicate[0].NetId,
            duplicate[1].AssetId,
            duplicate[1].TeamId,
            0,
            0,
            0,
            0,
            PeerRuntimeBarrierCodec.PopulationComponentMask);
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.ComputePopulationIdentityDigest(
                hundred.Header.GenerationKey,
                duplicate));
        PeerPopulationRecord[] outOfOrder = CreatePopulationRecords(2);
        Array.Reverse(outOfOrder);
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.ComputePopulationIdentityDigest(
                hundred.Header.GenerationKey,
                outOfOrder));

        PeerPopulationReady ready = CreatePopulationReady(hundred, 1);
        byte[] readyBytes = PeerRuntimeBarrierCodec.EncodePopulationReady(ready);
        Assert(readyBytes.Length == 249);
        PeerBarrierExpectation readyExpected = Expect(
            PeerBarrierMessageKind.PopulationReady,
            1,
            0,
            2,
            100,
            true);
        Assert(PeerRuntimeBarrierCodec.TryDecodePopulationReady(
            readyBytes,
            readyExpected,
            hundred.PopulationDigest,
            hundred.PopulationIdentityDigest,
            out _,
            out error), error);
    }

    private static void TestBeginMessages()
    {
        Assert(PeerRuntimeBarrierCodec.PrepareBeginBytes == 365);
        Assert(PeerRuntimeBarrierCodec.BeginReadyBytes == 369);
        Assert(PeerRuntimeBarrierCodec.PrepareBeginRuntimeOwnerManifestDigestOffset ==
            261);
        Assert(PeerRuntimeBarrierCodec.PrepareBeginRuntimeReadySetDigestOffset ==
            293);
        Assert(PeerRuntimeBarrierCodec.PrepareBeginRuntimeOwnerNetIdOffset == 325);
        Assert(PeerRuntimeBarrierCodec.BeginReadyRuntimeOwnerManifestDigestOffset ==
            261);
        Assert(PeerRuntimeBarrierCodec.BeginReadyRuntimeReadySetDigestOffset == 293);
        Assert(PeerRuntimeBarrierCodec.BeginReadyRuntimeOwnerNetIdOffset == 325);
        PeerPopulationManifest population = CreateManifest(3);
        byte[] grounded = Digest(0xB1);
        byte[] placement = Digest(0xC1);
        PeerPrepareBegin prepare = CreatePrepare(
            population,
            grounded,
            placement,
            participantCount: 2);
        PeerBarrierExpectation prepareExpected = Expect(
            PeerBarrierMessageKind.PrepareBegin,
            0,
            1,
            2,
            3,
            true);
        byte[] prepareBytes =
            PeerRuntimeBarrierCodec.EncodePrepareBeginForFrozenRuntime(
                prepare,
                prepareExpected,
                prepare.RuntimeOwnerManifestDigest,
                prepare.RuntimeReadySetDigest);
        Assert(prepareBytes.Length == 365);
        Assert(PeerRuntimeBarrierCodec.TryDecodePrepareBegin(
            prepareBytes,
            prepareExpected,
            population.PopulationDigest,
            population.PopulationIdentityDigest,
            grounded,
            placement,
            prepare.RuntimeOwnerManifestDigest,
            prepare.RuntimeReadySetDigest,
            out PeerPrepareBegin decodedPrepare,
            out string error), error);
        byte[] zeroRuntimeManifest = (byte[])prepareBytes.Clone();
        Array.Clear(
            zeroRuntimeManifest,
            PeerRuntimeBarrierCodec.PrepareBeginRuntimeOwnerManifestDigestOffset,
            32);
        Assert(!PeerRuntimeBarrierCodec.TryDecodePrepareBegin(
            zeroRuntimeManifest,
            prepareExpected,
            population.PopulationDigest,
            population.PopulationIdentityDigest,
            grounded,
            placement,
            prepare.RuntimeOwnerManifestDigest,
            prepare.RuntimeReadySetDigest,
            out _,
            out _));
        byte[] zeroRuntimeReadySet = (byte[])prepareBytes.Clone();
        Array.Clear(
            zeroRuntimeReadySet,
            PeerRuntimeBarrierCodec.PrepareBeginRuntimeReadySetDigestOffset,
            32);
        Assert(!PeerRuntimeBarrierCodec.TryDecodePrepareBegin(
            zeroRuntimeReadySet,
            prepareExpected,
            population.PopulationDigest,
            population.PopulationIdentityDigest,
            grounded,
            placement,
            prepare.RuntimeOwnerManifestDigest,
            prepare.RuntimeReadySetDigest,
            out _,
            out _));
        byte[] trailingPrepare = new byte[prepareBytes.Length + 1];
        Buffer.BlockCopy(prepareBytes, 0, trailingPrepare, 0, prepareBytes.Length);
        Assert(!PeerRuntimeBarrierCodec.TryDecodePrepareBegin(
            trailingPrepare,
            prepareExpected,
            population.PopulationDigest,
            population.PopulationIdentityDigest,
            grounded,
            placement,
            prepare.RuntimeOwnerManifestDigest,
            prepare.RuntimeReadySetDigest,
            out _,
            out _));
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodePrepareBeginForFrozenRuntime(
                prepare,
                prepareExpected,
                Digest(0x44),
                prepare.RuntimeReadySetDigest));

        PeerBeginReady ready = CreateBeginReady(decodedPrepare, slot: 1);
        PeerBarrierExpectation readyExpected = Expect(
            PeerBarrierMessageKind.BeginReady,
            1,
            0,
            2,
            3,
            true);
        byte[] readyBytes =
            PeerRuntimeBarrierCodec.EncodeBeginReadyForFrozenRuntime(
                ready,
                readyExpected,
                decodedPrepare);
        Assert(readyBytes.Length == 369);
        Assert(PeerRuntimeBarrierCodec.TryDecodeBeginReady(
            readyBytes,
            readyExpected,
            decodedPrepare,
            out PeerBeginReady decodedReady,
            out error), error);
        byte[] zeroReadyRuntimeManifest = (byte[])readyBytes.Clone();
        Array.Clear(
            zeroReadyRuntimeManifest,
            PeerRuntimeBarrierCodec.BeginReadyRuntimeOwnerManifestDigestOffset,
            32);
        Assert(!PeerRuntimeBarrierCodec.TryDecodeBeginReady(
            zeroReadyRuntimeManifest,
            readyExpected,
            decodedPrepare,
            out _,
            out _));
        byte[] zeroReadyRuntimeSet = (byte[])readyBytes.Clone();
        Array.Clear(
            zeroReadyRuntimeSet,
            PeerRuntimeBarrierCodec.BeginReadyRuntimeReadySetDigestOffset,
            32);
        Assert(!PeerRuntimeBarrierCodec.TryDecodeBeginReady(
            zeroReadyRuntimeSet,
            readyExpected,
            decodedPrepare,
            out _,
            out _));
        byte[] trailingReady = new byte[readyBytes.Length + 1];
        Buffer.BlockCopy(readyBytes, 0, trailingReady, 0, readyBytes.Length);
        Assert(!PeerRuntimeBarrierCodec.TryDecodeBeginReady(
            trailingReady,
            readyExpected,
            decodedPrepare,
            out _,
            out _));
        byte[] badReservedReady = (byte[])readyBytes.Clone();
        badReservedReady[334] = 1;
        Assert(!PeerRuntimeBarrierCodec.TryDecodeBeginReady(
            badReservedReady,
            readyExpected,
            decodedPrepare,
            out _,
            out _));

        byte[] readySet = PeerRuntimeBarrierCodec.ComputeBeginReadySetDigest(
            ready.Header.GenerationKey,
            new[] { new PeerSlotReceiptDigest(1, decodedReady.ReceiptDigest) },
            2);
        PeerBeginCommit commit = CreateBeginCommit(
            decodedPrepare,
            readySet,
            recipientSlot: 1);
        PeerBarrierExpectation commitExpected = Expect(
            PeerBarrierMessageKind.BeginCommit,
            0,
            1,
            2,
            3,
            true);
        byte[] commitBytes =
            PeerRuntimeBarrierCodec.EncodeBeginCommitForFrozenReadySet(
                commit,
                commitExpected,
                decodedPrepare,
                readySet);
        Assert(commitBytes.Length == 233);
        Assert(PeerRuntimeBarrierCodec.TryDecodeBeginCommit(
            commitBytes,
            commitExpected,
            decodedPrepare,
            readySet,
            out _,
            out error), error);
        PeerBeginCommit wrongGenerationCommit = RebindBeginCommitGeneration(
            commit,
            Digest(0xF1));
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodeBeginCommitForFrozenReadySet(
                wrongGenerationCommit,
                commitExpected,
                decodedPrepare,
                readySet));
    }

    private static void TestPlacementMessages()
    {
        Assert(PeerRuntimeBarrierCodec.CommonHeaderBytes + 104 +
            PeerRuntimeBarrierCodec.PlayerRecordBytes == 289);

        PeerPlacePlayer pair = CreatePlace(2, true, 3);
        PeerBarrierExpectation pairExpected = Expect(
            PeerBarrierMessageKind.PlacePlayer,
            0,
            1,
            2,
            3,
            true,
            maximumMarkerOrdinalExclusive: 2,
            expectedPveTeam: 7);
        byte[] pairBytes =
            PeerRuntimeBarrierCodec.EncodePlacePlayerForFrozenRoster(
                pair,
                pairExpected);
        Assert(pairBytes.Length == 373);
        Assert(PeerRuntimeBarrierCodec.TryDecodePlacePlayer(
            pairBytes,
            pairExpected,
            out PeerPlacePlayer decodedPair,
            out string error), error);
        PeerBarrierExpectation wrongRoster = Expect(
            PeerBarrierMessageKind.PlacePlayer,
            0,
            1,
            2,
            3,
            true,
            maximumMarkerOrdinalExclusive: 2,
            expectedPveTeam: 7,
            connectionIds: new[] { -1, 999 });
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodePlacePlayerForFrozenRoster(
                pair,
                wrongRoster));
        Assert(!PeerRuntimeBarrierCodec.TryDecodePlacePlayer(
            pairBytes,
            wrongRoster,
            out _,
            out _));

        PeerPlacePlayer wrongGenerationPlace = RebindPlaceGeneration(
            pair,
            Digest(0xF2));
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodePlacePlayerForFrozenRoster(
                wrongGenerationPlace,
                pairExpected));

        byte[] reservedPlace = (byte[])pairBytes.Clone();
        reservedPlace[227] = 1;
        Assert(!PeerRuntimeBarrierCodec.TryDecodePlacePlayer(
            reservedPlace,
            pairExpected,
            out _,
            out _));
        byte[] badFlagsPlace = (byte[])pairBytes.Clone();
        badFlagsPlace[201] = 0xFF;
        badFlagsPlace[202] = 0xFF;
        Assert(!PeerRuntimeBarrierCodec.TryDecodePlacePlayer(
            badFlagsPlace,
            pairExpected,
            out _,
            out _));
        byte[] zeroScenePlace = (byte[])pairBytes.Clone();
        Array.Clear(zeroScenePlace, 101, 32);
        Assert(!PeerRuntimeBarrierCodec.TryDecodePlacePlayer(
            zeroScenePlace,
            pairExpected,
            out _,
            out _));
        byte[] badCountPlace = (byte[])pairBytes.Clone();
        badCountPlace[198] = 0;
        Assert(!PeerRuntimeBarrierCodec.TryDecodePlacePlayer(
            badCountPlace,
            pairExpected,
            out _,
            out _));
        byte[] trailingPlace = new byte[pairBytes.Length + 1];
        Buffer.BlockCopy(pairBytes, 0, trailingPlace, 0, pairBytes.Length);
        Assert(!PeerRuntimeBarrierCodec.TryDecodePlacePlayer(
            trailingPlace,
            pairExpected,
            out _,
            out _));

        PeerPlacePlayer maximum = CreatePlace(64, true, 100);
        PeerBarrierExpectation maximumExpected = Expect(
            PeerBarrierMessageKind.PlacePlayer,
            0,
            1,
            64,
            100,
            true,
            maximumMarkerOrdinalExclusive: 64,
            expectedPveTeam: 7);
        byte[] maximumBytes =
            PeerRuntimeBarrierCodec.EncodePlacePlayerForFrozenRoster(
                maximum,
                maximumExpected);
        Assert(maximumBytes.Length == 5581);
        Assert(PeerRuntimeBarrierCodec.TryDecodePlacePlayer(
            maximumBytes,
            maximumExpected,
            out _,
            out error), error);
        Throws<InvalidDataException>(() =>
            CreatePlace(65, true, 100));

        PeerPlayerReady remoteReady = CreatePlayerReady(decodedPair, 1, false);
        PeerBarrierExpectation remoteExpected = Expect(
            PeerBarrierMessageKind.PlayerReady,
            1,
            0,
            2,
            3,
            true);
        byte[] remoteBytes =
            PeerRuntimeBarrierCodec.EncodePlayerReadyForFrozenAssignment(
                remoteReady,
                remoteExpected,
                decodedPair);
        Assert(remoteBytes.Length == 401);
        Assert(PeerRuntimeBarrierCodec.TryDecodePlayerReady(
            remoteBytes,
            remoteExpected,
            decodedPair,
            out _,
            out error), error);
        PeerBarrierExpectation wrongReadyRoster = Expect(
            PeerBarrierMessageKind.PlayerReady,
            1,
            0,
            2,
            3,
            true,
            connectionIds: new[] { -1, 999 });
        Assert(!PeerRuntimeBarrierCodec.TryDecodePlayerReady(
            remoteBytes,
            wrongReadyRoster,
            decodedPair,
            out _,
            out _));
        PeerPlayerReady wrongGenerationReady = CreatePlayerReady(
            wrongGenerationPlace,
            1,
            false);
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodePlayerReadyForFrozenAssignment(
                wrongGenerationReady,
                remoteExpected,
                decodedPair));

        PeerPlayerAssignmentRecord remoteRecord = decodedPair.Records[1];
        PeerPlayerReady horizontalTooFar = CopyPlayerReadyPose(
            remoteReady,
            remoteRecord.PositionXMillimetres + 101,
            remoteRecord.PositionYMillimetres,
            remoteRecord.PositionZMillimetres,
            remoteRecord.YawCentidegrees);
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodePlayerReadyForFrozenAssignment(
                horizontalTooFar,
                remoteExpected,
                decodedPair));
        PeerPlayerReady verticalTooFar = CopyPlayerReadyPose(
            remoteReady,
            remoteRecord.PositionXMillimetres,
            remoteRecord.PositionYMillimetres + 101,
            remoteRecord.PositionZMillimetres,
            remoteRecord.YawCentidegrees);
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodePlayerReadyForFrozenAssignment(
                verticalTooFar,
                remoteExpected,
                decodedPair));
        PeerPlayerReady yawTooFar = CopyPlayerReadyPose(
            remoteReady,
            remoteRecord.PositionXMillimetres,
            remoteRecord.PositionYMillimetres,
            remoteRecord.PositionZMillimetres,
            checked((ushort)((remoteRecord.YawCentidegrees + 101) % 36000)));
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodePlayerReadyForFrozenAssignment(
                yawTooFar,
                remoteExpected,
                decodedPair));

        byte[] badStatusReady = (byte[])remoteBytes.Clone();
        badStatusReady[367] = 0;
        badStatusReady[368] = 0;
        Assert(!PeerRuntimeBarrierCodec.TryDecodePlayerReady(
            badStatusReady,
            remoteExpected,
            decodedPair,
            out _,
            out _));
        PeerPlayerReady localReady = CreatePlayerReady(decodedPair, 0, true);
        PeerBarrierExpectation localExpected = Expect(
            PeerBarrierMessageKind.PlayerReady,
            0,
            0,
            2,
            3,
            true);
        Assert(PeerRuntimeBarrierCodec.TryValidateLocalPlayerReadyReceipt(
            localReady,
            localExpected,
            decodedPair,
            out error), error);
        byte[] localOnWire = (byte[])remoteBytes.Clone();
        Array.Clear(localOnWire, 5, 4);
        Assert(!PeerRuntimeBarrierCodec.TryDecodePlayerReady(
            localOnWire,
            localExpected,
            decodedPair,
            out _,
            out _));
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodePlayerReadyForFrozenAssignment(
                localReady,
                localExpected,
                decodedPair));

        PeerPlacePlayer localPlace = CreatePlace(1, true, 3, recipientSlot: 0);
        PeerBarrierExpectation localPlaceExpected = Expect(
            PeerBarrierMessageKind.PlacePlayer,
            0,
            0,
            1,
            3,
            true,
            maximumMarkerOrdinalExclusive: 1,
            expectedPveTeam: 7);
        Assert(PeerRuntimeBarrierCodec.TryValidateLocalPlacePlayerAssignment(
            localPlace,
            localPlaceExpected,
            out error), error);
        byte[] localPlaceOnWire = (byte[])pairBytes.Clone();
        Array.Clear(localPlaceOnWire, 9, 4);
        Assert(!PeerRuntimeBarrierCodec.TryDecodePlacePlayer(
            localPlaceOnWire,
            localPlaceExpected,
            out _,
            out _));
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.EncodePlacePlayerForFrozenRoster(
                localPlace,
                localPlaceExpected));

        PeerPlacePlayer pvp = CreatePlace(2, false, 0);
        PeerPlayerReady pvpReady = CreatePlayerReady(pvp, 1, false);
        PeerBarrierExpectation pvpReadyExpected = Expect(
            PeerBarrierMessageKind.PlayerReady,
            1,
            0,
            2,
            0,
            false);
        Assert(PeerRuntimeBarrierCodec.TryDecodePlayerReady(
            PeerRuntimeBarrierCodec.EncodePlayerReadyForFrozenAssignment(
                pvpReady,
                pvpReadyExpected,
                pvp),
            pvpReadyExpected,
            pvp,
            out _,
            out error), error);

        PeerPlayerAssignmentRecord[] duplicateConnection =
            CreatePlayerRecords(2, true);
        PeerPlayerAssignmentRecord second = duplicateConnection[1];
        duplicateConnection[1] = CopyWithConnection(
            second,
            duplicateConnection[0].ConnectionId);
        Throws<InvalidDataException>(() =>
            PeerRuntimeBarrierCodec.ComputePlayerSetDigest(
                pair.Header.GenerationKey,
                PeerPlacementRoute.PveOwnerRpc,
                duplicateConnection));
    }

    private static void TestImmutability()
    {
        byte[] scene = (byte[])Scene.Clone();
        PeerPopulationManifest manifest = CreateManifest(2, scene);
        byte expected = manifest.SceneContractDigest[0];
        scene[0] ^= 0x7F;
        Assert(manifest.SceneContractDigest[0] == expected);
        byte[] exposed = manifest.SceneContractDigest;
        exposed[0] ^= 0x7F;
        Assert(manifest.SceneContractDigest[0] == expected);

        byte[] encoded = PeerRuntimeBarrierCodec.EncodePopulationManifest(manifest);
        Assert(PeerRuntimeBarrierCodec.TryDecodePopulationManifest(
            encoded,
            Expect(
                PeerBarrierMessageKind.PopulationManifest,
                0,
                1,
                2,
                2,
                true),
            out PeerPopulationManifest decoded,
            out string error), error);
        byte[] decodedDigest = decoded.PopulationDigest;
        byte first = decodedDigest[0];
        decodedDigest[0] ^= 0x55;
        Assert(decoded.PopulationDigest[0] == first);
        byte[] headerDigest = decoded.Header.GenerationKey;
        byte headerFirst = headerDigest[0];
        headerDigest[0] ^= 0x55;
        Assert(decoded.Header.GenerationKey[0] == headerFirst);

        PeerPrepareBegin prepare = CreatePrepare(
            manifest,
            Digest(0x71),
            Digest(0x81),
            2);
        byte runtimeManifestFirst = prepare.RuntimeOwnerManifestDigest[0];
        byte[] exposedRuntimeManifest = prepare.RuntimeOwnerManifestDigest;
        exposedRuntimeManifest[0] ^= 0x55;
        Assert(prepare.RuntimeOwnerManifestDigest[0] == runtimeManifestFirst);
        PeerBeginReady beginReady = CreateBeginReady(prepare, 1);
        byte runtimeSetFirst = beginReady.RuntimeReadySetDigest[0];
        byte[] exposedRuntimeSet = beginReady.RuntimeReadySetDigest;
        exposedRuntimeSet[0] ^= 0x55;
        Assert(beginReady.RuntimeReadySetDigest[0] == runtimeSetFirst);

        PeerPlacePlayer place = CreatePlace(2, true, 2);
        byte[] marker = place.Records[0].MarkerDigest;
        byte markerFirst = marker[0];
        marker[0] ^= 0x55;
        Assert(place.Records[0].MarkerDigest[0] == markerFirst);
    }

    private static void TestGoldenVectors()
    {
        PeerPopulationManifest population = CreateManifest(3);
        PeerPopulationReady populationReady = CreatePopulationReady(population, 1);
        PeerPrepareBegin prepare = CreatePrepare(
            population,
            Digest(0xB1),
            Digest(0xC1),
            2);
        PeerBeginReady beginReady = CreateBeginReady(prepare, 1);
        byte[] readySet = PeerRuntimeBarrierCodec.ComputeBeginReadySetDigest(
            beginReady.Header.GenerationKey,
            new[] { new PeerSlotReceiptDigest(1, beginReady.ReceiptDigest) },
            2);
        PeerBeginCommit commit = CreateBeginCommit(prepare, readySet, 1);
        PeerPlacePlayer place = CreatePlace(2, true, 3);
        PeerPlayerReady playerReady = CreatePlayerReady(place, 1, false);
        byte[] stableMarker = PeerRuntimeBarrierCodec.ComputeStablePlayerMarkerDigest(
            Digest(0x31),
            "root[0]/PVE_PlayerSpawn_01[0]",
            7,
            1000,
            2000,
            -3000,
            1234,
            Digest(0x41));

        AssertHex(population.PopulationIdentityDigest,
            "1E0CA005B22C227BA41DBDF6DEBDE51FAB329844F34D2960026034DFBCA62660");
        AssertHex(population.PopulationDigest,
            "6925D7C379856AB8B138BFF4D15D1C6D894D0A94363426E4A20ACC062E1A3ED7");
        AssertHex(populationReady.ReceiptDigest,
            "4809ABB875D1C10B967C073523998506189A57B7C7F8B2AF692AAFA7584CD2C3");
        AssertHex(prepare.BeginContractDigest,
            "2F9EFE5C1AE174538694A6B86647F04F11748414A3776B3A035CBD61CDFFE905");
        AssertHex(beginReady.ReceiptDigest,
            "3D74CE59345D8BDCF305EA305E233AA77FD5D380DA791C88A692DD03917627DD");
        AssertHex(readySet,
            "60424872E177B907A0540F046B0A02E3E951B5125F4F987402CDB2211C87A672");
        AssertHex(commit.CommitDigest,
            "D07D682A62E55CE4FA92A9E8576B1EE34A3617CC59A63E3804F56709B144C1A6");
        AssertHex(place.PlayerSetDigest,
            "D39AE8AF86B02683E40E46460AE5CA9ECA416894980275262A1764BF48CF7492");
        AssertHex(place.AssignmentDigest,
            "EB2F33F96EAE4103C4643C6DE9A972B53964CEF80E1EFCC13EC7CB362B035CBF");
        AssertHex(stableMarker,
            "900352F69CE8E5BE76C4C8672FD4CBB1E42ED904D68C753E745E5EBDB3A934D4");
        AssertHex(playerReady.ReceiptDigest,
            "AAECF00AB1AAA6E07018D6A60159D8C37E9B4AE90BF97124CDB1F721EAF410B6");
    }

    private static void AssertHex(byte[] value, string expected)
    {
        string actual = Convert.ToHexString(value);
        Assert(actual == expected, "expected " + expected + ", actual " + actual);
    }

    private static PeerPopulationManifest CreateManifest(
        int count,
        byte[] sceneOverride = null)
    {
        byte[] scene = sceneOverride ?? Scene;
        PeerBarrierHeader header = Header(
            PeerBarrierMessageKind.PopulationManifest,
            0,
            1,
            scene,
            count);
        PeerPopulationRecord[] records = CreatePopulationRecords(count);
        byte[] identity = PeerRuntimeBarrierCodec.ComputePopulationIdentityDigest(
            header.GenerationKey,
            records);
        byte[] population = PeerRuntimeBarrierCodec.ComputePopulationDigest(
            header.GenerationKey,
            scene,
            1234567,
            records);
        return new PeerPopulationManifest(
            header,
            scene,
            population,
            identity,
            1234567,
            records);
    }

    private static PeerPopulationReady CreatePopulationReady(
        PeerPopulationManifest manifest,
        int slot)
    {
        PeerBarrierHeader header = Header(
            PeerBarrierMessageKind.PopulationReady,
            slot,
            0,
            manifest.SceneContractDigest,
            manifest.Records.Count);
        var pending = new PeerPopulationReady(
            header,
            manifest.SceneContractDigest,
            manifest.PopulationDigest,
            manifest.PopulationIdentityDigest,
            2234567,
            100,
            100,
            500,
            checked((byte)manifest.Records.Count),
            3,
            new byte[32]);
        return new PeerPopulationReady(
            header,
            pending.SceneContractDigest,
            pending.PopulationDigest,
            pending.ObservedPopulationIdentityDigest,
            pending.ClientNetworkTimeMicroseconds,
            pending.MaximumHorizontalErrorMillimetres,
            pending.MaximumVerticalErrorMillimetres,
            pending.MaximumYawErrorCentidegrees,
            pending.Count,
            pending.StableSamples,
            PeerRuntimeBarrierCodec.ComputePopulationReadyReceiptDigest(pending));
    }

    private static PeerPrepareBegin CreatePrepare(
        PeerPopulationManifest population,
        byte[] grounded,
        byte[] placement,
        int participantCount)
    {
        PeerBarrierHeader header = Header(
            PeerBarrierMessageKind.PrepareBegin,
            0,
            1,
            population.SceneContractDigest,
            population.Records.Count);
        var pending = new PeerPrepareBegin(
            header,
            population.SceneContractDigest,
            population.PopulationDigest,
            population.PopulationIdentityDigest,
            grounded,
            placement,
            Digest(0xD1),
            Digest(0xE1),
            9001,
            checked((byte)participantCount),
            checked((byte)population.Records.Count),
            PeerRuntimeBarrierCodec.PveBeginFlags,
            new byte[32]);
        return new PeerPrepareBegin(
            header,
            pending.SceneContractDigest,
            pending.PopulationDigest,
            pending.PopulationIdentityDigest,
            pending.GroundedPlayerSetDigest,
            pending.PlayerPlacementDigest,
            pending.RuntimeOwnerManifestDigest,
            pending.RuntimeReadySetDigest,
            pending.RuntimeOwnerNetId,
            pending.ParticipantCount,
            pending.EnemyCount,
            pending.Flags,
            PeerRuntimeBarrierCodec.ComputePrepareBeginDigest(pending));
    }

    private static PeerBeginReady CreateBeginReady(
        PeerPrepareBegin prepare,
        int slot)
    {
        PeerBarrierHeader header = Header(
            PeerBarrierMessageKind.BeginReady,
            slot,
            0,
            prepare.SceneContractDigest,
            prepare.EnemyCount);
        var pending = new PeerBeginReady(
            header,
            prepare.BeginContractDigest,
            prepare.SceneContractDigest,
            prepare.PopulationDigest,
            prepare.GroundedPlayerSetDigest,
            prepare.PlayerPlacementDigest,
            prepare.RuntimeOwnerManifestDigest,
            prepare.RuntimeReadySetDigest,
            prepare.RuntimeOwnerNetId,
            prepare.ParticipantCount,
            prepare.EnemyCount,
            prepare.Flags,
            30,
            new byte[32]);
        return new PeerBeginReady(
            header,
            pending.BeginContractDigest,
            pending.SceneContractDigest,
            pending.PopulationDigest,
            pending.GroundedPlayerSetDigest,
            pending.PlayerPlacementDigest,
            pending.RuntimeOwnerManifestDigest,
            pending.RuntimeReadySetDigest,
            pending.RuntimeOwnerNetId,
            pending.ParticipantCount,
            pending.EnemyCount,
            pending.Flags,
            pending.GroundedStableSamples,
            PeerRuntimeBarrierCodec.ComputeBeginReadyReceiptDigest(pending));
    }

    private static PeerBeginCommit CreateBeginCommit(
        PeerPrepareBegin prepare,
        byte[] readySet,
        int recipientSlot)
    {
        PeerBarrierHeader header = Header(
            PeerBarrierMessageKind.BeginCommit,
            0,
            recipientSlot,
            prepare.SceneContractDigest,
            prepare.EnemyCount);
        var pending = new PeerBeginCommit(
            header,
            prepare.BeginContractDigest,
            prepare.PopulationDigest,
            readySet,
            new byte[32],
            checked((byte)(prepare.ParticipantCount - 1)),
            prepare.ParticipantCount,
            prepare.Flags);
        return new PeerBeginCommit(
            header,
            pending.BeginContractDigest,
            pending.PopulationDigest,
            pending.ReadySetDigest,
            PeerRuntimeBarrierCodec.ComputeBeginCommitDigest(pending),
            pending.RemoteReadyCount,
            pending.ParticipantCount,
            pending.Flags);
    }

    private static PeerPlacePlayer CreatePlace(
        int participants,
        bool isPve,
        int requestedEnemies,
        int recipientSlot = 1)
    {
        PeerBarrierHeader header = Header(
            PeerBarrierMessageKind.PlacePlayer,
            0,
            recipientSlot,
            Scene,
            requestedEnemies);
        PeerPlacementRoute route = isPve
            ? PeerPlacementRoute.PveOwnerRpc
            : PeerPlacementRoute.PvpNativeObserve;
        PeerPlayerAssignmentRecord[] records = CreatePlayerRecords(
            participants,
            isPve);
        byte[] playerSet = PeerRuntimeBarrierCodec.ComputePlayerSetDigest(
            header.GenerationKey,
            route,
            records);
        var pending = new PeerPlacePlayer(
            header,
            Scene,
            playerSet,
            new byte[32],
            route,
            isPve
                ? PeerRuntimeBarrierCodec.PvePlaceFlags
                : PeerRuntimeBarrierCodec.PvpPlaceFlags,
            PeerRuntimeBarrierCodec.PlayerOwnerStatusMask,
            records);
        return new PeerPlacePlayer(
            header,
            pending.SceneContractDigest,
            pending.PlayerSetDigest,
            PeerRuntimeBarrierCodec.ComputePlayerAssignmentDigest(pending),
            pending.Route,
            pending.Flags,
            pending.RequiredOwnerStatusMask,
            records);
    }

    private static PeerPlayerReady CreatePlayerReady(
        PeerPlacePlayer place,
        int slot,
        bool local)
    {
        PeerPlayerAssignmentRecord record = place.Records[slot];
        PeerBarrierHeader header = new PeerBarrierHeader(
            PeerBarrierMessageKind.PlayerReady,
            slot,
            0,
            place.Header.SessionNonce,
            place.Header.IdentityDigest,
            place.Header.SceneGenerationEpoch,
            place.Header.GenerationKey);
        byte[] ownerState = Digest(0xD1);
        var pending = new PeerPlayerReady(
            header,
            place.SceneContractDigest,
            place.AssignmentDigest,
            place.PlayerSetDigest,
            record.MarkerDigest,
            ownerState,
            ownerState,
            Digest(0xE1),
            record.PlayerMasterNetId,
            record.PlayerNetworkingNetId,
            record.PositionXMillimetres,
            record.PositionYMillimetres,
            record.PositionZMillimetres,
            record.YawCentidegrees,
            20,
            100,
            100,
            100,
            50,
            40,
            100,
            1000,
            50,
            30,
            place.Route,
            PeerRuntimeBarrierCodec.PlayerOwnerStatusMask,
            new byte[32]);
        return new PeerPlayerReady(
            header,
            pending.SceneContractDigest,
            pending.AssignmentDigest,
            pending.PlayerSetDigest,
            pending.AssignedMarkerDigest,
            pending.PreRouteStateDigest,
            pending.PostRouteStateDigest,
            pending.GroundColliderDigest,
            pending.PlayerMasterNetId,
            pending.PlayerNetworkingNetId,
            pending.ObservedPositionXMillimetres,
            pending.ObservedPositionYMillimetres,
            pending.ObservedPositionZMillimetres,
            pending.ObservedYawCentidegrees,
            pending.FeetToGroundDeltaMillimetres,
            pending.MaximumHorizontalErrorMillimetres,
            pending.MaximumVerticalErrorMillimetres,
            pending.MaximumYawErrorCentidegrees,
            pending.MaximumLinearSpeedMillimetresPerSecond,
            pending.MaximumVerticalSpeedMillimetresPerSecond,
            pending.MaximumAngularSpeedCentidegreesPerSecond,
            pending.GroundSlopeCentidegrees,
            pending.MaximumStableRootDriftMillimetres,
            pending.StableSamples,
            pending.Route,
            pending.OwnerStatusMask,
            PeerRuntimeBarrierCodec.ComputePlayerReadyReceiptDigest(pending));
    }

    private static PeerBarrierHeader Header(
        PeerBarrierMessageKind kind,
        int sender,
        int recipient,
        byte[] scene,
        int requestedEnemies)
    {
        return new PeerBarrierHeader(
            kind,
            sender,
            recipient,
            Nonce,
            Identity,
            7,
            PeerRuntimeBarrierCodec.ComputeGenerationKey(
                Nonce,
                Identity,
                7,
                scene,
                requestedEnemies));
    }

    private static PeerBarrierExpectation Expect(
        PeerBarrierMessageKind kind,
        int sender,
        int recipient,
        int participants,
        int requestedEnemies,
        bool isPve,
        int maximumMarkerOrdinalExclusive = 0,
        int expectedPveTeam = int.MinValue,
        IEnumerable<int> connectionIds = null)
    {
        PeerBarrierHeader header = Header(
            kind,
            sender,
            recipient,
            Scene,
            requestedEnemies);
        return new PeerBarrierExpectation(
            kind,
            sender,
            recipient,
            header.SessionNonce,
            header.IdentityDigest,
            header.SceneGenerationEpoch,
            header.GenerationKey,
            Scene,
            participants,
            requestedEnemies,
            isPve,
            maximumMarkerOrdinalExclusive,
            expectedPveTeam,
            connectionIds ?? Enumerable.Range(0, participants)
                .Select(slot => slot == 0 ? -1 : 100 + slot));
    }

    private static PeerPlacePlayer RebindPlaceGeneration(
        PeerPlacePlayer source,
        byte[] generationKey)
    {
        var header = new PeerBarrierHeader(
            PeerBarrierMessageKind.PlacePlayer,
            source.Header.SenderSlot,
            source.Header.RecipientSlot,
            source.Header.SessionNonce,
            source.Header.IdentityDigest,
            source.Header.SceneGenerationEpoch,
            generationKey);
        byte[] playerSet = PeerRuntimeBarrierCodec.ComputePlayerSetDigest(
            header.GenerationKey,
            source.Route,
            source.Records);
        var pending = new PeerPlacePlayer(
            header,
            source.SceneContractDigest,
            playerSet,
            new byte[32],
            source.Route,
            source.Flags,
            source.RequiredOwnerStatusMask,
            source.Records);
        return new PeerPlacePlayer(
            header,
            pending.SceneContractDigest,
            pending.PlayerSetDigest,
            PeerRuntimeBarrierCodec.ComputePlayerAssignmentDigest(pending),
            pending.Route,
            pending.Flags,
            pending.RequiredOwnerStatusMask,
            pending.Records);
    }

    private static PeerBeginCommit RebindBeginCommitGeneration(
        PeerBeginCommit source,
        byte[] generationKey)
    {
        var header = new PeerBarrierHeader(
            PeerBarrierMessageKind.BeginCommit,
            source.Header.SenderSlot,
            source.Header.RecipientSlot,
            source.Header.SessionNonce,
            source.Header.IdentityDigest,
            source.Header.SceneGenerationEpoch,
            generationKey);
        var pending = new PeerBeginCommit(
            header,
            source.BeginContractDigest,
            source.PopulationDigest,
            source.ReadySetDigest,
            new byte[32],
            source.RemoteReadyCount,
            source.ParticipantCount,
            source.Flags);
        return new PeerBeginCommit(
            header,
            pending.BeginContractDigest,
            pending.PopulationDigest,
            pending.ReadySetDigest,
            PeerRuntimeBarrierCodec.ComputeBeginCommitDigest(pending),
            pending.RemoteReadyCount,
            pending.ParticipantCount,
            pending.Flags);
    }

    private static PeerPlayerReady CopyPlayerReadyPose(
        PeerPlayerReady source,
        int x,
        int y,
        int z,
        ushort yaw)
    {
        var pending = new PeerPlayerReady(
            source.Header,
            source.SceneContractDigest,
            source.AssignmentDigest,
            source.PlayerSetDigest,
            source.AssignedMarkerDigest,
            source.PreRouteStateDigest,
            source.PostRouteStateDigest,
            source.GroundColliderDigest,
            source.PlayerMasterNetId,
            source.PlayerNetworkingNetId,
            x,
            y,
            z,
            yaw,
            source.FeetToGroundDeltaMillimetres,
            source.MaximumHorizontalErrorMillimetres,
            source.MaximumVerticalErrorMillimetres,
            source.MaximumYawErrorCentidegrees,
            source.MaximumLinearSpeedMillimetresPerSecond,
            source.MaximumVerticalSpeedMillimetresPerSecond,
            source.MaximumAngularSpeedCentidegreesPerSecond,
            source.GroundSlopeCentidegrees,
            source.MaximumStableRootDriftMillimetres,
            source.StableSamples,
            source.Route,
            source.OwnerStatusMask,
            new byte[32]);
        return new PeerPlayerReady(
            pending.Header,
            pending.SceneContractDigest,
            pending.AssignmentDigest,
            pending.PlayerSetDigest,
            pending.AssignedMarkerDigest,
            pending.PreRouteStateDigest,
            pending.PostRouteStateDigest,
            pending.GroundColliderDigest,
            pending.PlayerMasterNetId,
            pending.PlayerNetworkingNetId,
            pending.ObservedPositionXMillimetres,
            pending.ObservedPositionYMillimetres,
            pending.ObservedPositionZMillimetres,
            pending.ObservedYawCentidegrees,
            pending.FeetToGroundDeltaMillimetres,
            pending.MaximumHorizontalErrorMillimetres,
            pending.MaximumVerticalErrorMillimetres,
            pending.MaximumYawErrorCentidegrees,
            pending.MaximumLinearSpeedMillimetresPerSecond,
            pending.MaximumVerticalSpeedMillimetresPerSecond,
            pending.MaximumAngularSpeedCentidegreesPerSecond,
            pending.GroundSlopeCentidegrees,
            pending.MaximumStableRootDriftMillimetres,
            pending.StableSamples,
            pending.Route,
            pending.OwnerStatusMask,
            PeerRuntimeBarrierCodec.ComputePlayerReadyReceiptDigest(pending));
    }

    private static PeerPopulationRecord[] CreatePopulationRecords(int count)
    {
        return Enumerable.Range(1, count)
            .Select(index => new PeerPopulationRecord(
                checked((uint)index),
                checked((uint)(1000 + index)),
                9,
                index * 100,
                0,
                index * -100,
                checked((ushort)(index % 36000)),
                PeerRuntimeBarrierCodec.PopulationComponentMask))
            .ToArray();
    }

    private static PeerPlayerAssignmentRecord[] CreatePlayerRecords(
        int count,
        bool isPve)
    {
        var records = new PeerPlayerAssignmentRecord[count];
        for (int slot = 0; slot < count; slot++)
        {
            records[slot] = new PeerPlayerAssignmentRecord(
                checked((byte)slot),
                1,
                PeerRuntimeBarrierCodec.PlayerPhysicalFlags,
                slot == 0 ? -1 : 100 + slot,
                checked((uint)(2000 + slot * 2)),
                checked((uint)(2001 + slot * 2)),
                isPve ? 7 : slot % 2 + 1,
                checked((ushort)slot),
                Digest(0x10 + slot),
                slot * 2000,
                100,
                slot * -2000,
                checked((ushort)(slot * 100 % 36000)),
                300,
                1800,
                0,
                900,
                0,
                8);
        }
        return records;
    }

    private static PeerPlayerAssignmentRecord CopyWithConnection(
        PeerPlayerAssignmentRecord value,
        int connectionId)
    {
        return new PeerPlayerAssignmentRecord(
            value.Slot,
            value.CapsuleDirection,
            value.PhysicalFlags,
            connectionId,
            value.PlayerMasterNetId,
            value.PlayerNetworkingNetId,
            value.TeamId,
            value.MarkerOrdinal,
            value.MarkerDigest,
            value.PositionXMillimetres,
            value.PositionYMillimetres,
            value.PositionZMillimetres,
            value.YawCentidegrees,
            value.CapsuleRadiusMillimetres,
            value.CapsuleHeightMillimetres,
            value.CapsuleCenterXMillimetres,
            value.CapsuleCenterYMillimetres,
            value.CapsuleCenterZMillimetres,
            value.GroundLayer);
    }

    private static byte[] Digest(int seed)
    {
        return Sequential(32, checked((byte)(seed % 224 + 1)));
    }

    private static byte[] Sequential(int length, byte start)
    {
        var result = new byte[length];
        for (int index = 0; index < length; index++)
            result[index] = unchecked((byte)(start + index));
        return result;
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
