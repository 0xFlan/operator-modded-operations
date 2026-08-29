using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using OperatorModdedOperations.NativePatching;

internal static class NativePolicyReceiptTests
{
    private static readonly byte[] AdapterNonce =
        TestAssert.Sequence(16, 0xA1);
    private static readonly byte[] ProcessNonce =
        TestAssert.Sequence(16, 0x71);
    private static readonly byte[] ApiDigest =
        TestAssert.Sequence(32, 0x21);
    private static readonly byte[] WrapperDigest =
        TestAssert.Sequence(32, 0x31);
    private static readonly byte[] DetourDigest =
        TestAssert.Sequence(32, 0x41);
    private static readonly byte[] ProbeDigest =
        TestAssert.Sequence(32, 0x51);
    private static readonly byte[] CanaryDigest =
        TestAssert.Sequence(32, 0x61);
    private static readonly byte[] ConstructionApiDigest =
        TestAssert.Sequence(32, 0x81);
    private static readonly byte[] ConstructionPhysicalDigest =
        TestAssert.Sequence(32, 0x91);

    internal static void Run()
    {
        TestBackendProbeTemporalAuthority();
        TestStateIssuedReceiptChainAndHardGate();
        TestGenerationAndLeaseAdmission();
        TestRestartRequiredResidentDrain();
        TestReceiptMutationAndImmutability();
        TestGoldenDigests();
    }

    private static void TestBackendProbeTemporalAuthority()
    {
        NativeTargetPolicyManifest target =
            NativePolicyContractTests.CreatePolicy();
        NativeConstructionApiManifest construction = CreateConstruction();
        NativePatchAdapterStateMachine adapter = CreateInstalledAdapter(
            AdapterNonce,
            ProcessNonce);

        TestAssert.True(typeof(NativePatchAdapterStateMachine).GetProperty(
            "CapabilityOwnerForEvidence",
            BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic) == null);
        MethodInfo rawFactory = typeof(NativeTargetPolicyCodec).GetMethod(
            "TryCreateBackendReceipt",
            BindingFlags.Static | BindingFlags.NonPublic);
        TestAssert.True(rawFactory != null &&
            rawFactory.GetParameters().All(value =>
                value.ParameterType != typeof(NativePatchAdapterState)));

        TestAssert.True(!TryFinalizeBackend(
            adapter,
            null,
            target,
            construction,
            NativePatchLoaderKind.BepInEx,
            "pre-mint",
            out _,
            out _));
        TestAssert.True(adapter.TryEnterBackendProbe(
            out NativePatchAdapterStateMachine.BackendProbeEvidence evidence,
            out string evidenceError), evidenceError);
        TestAssert.True(evidence.IsCanonical && evidence.PhaseSerial == 1 &&
            NativePolicyValue.FixedEquals(
                evidence.ProcessNonce,
                adapter.ProcessNonce) &&
            NativePolicyValue.FixedEquals(
                evidence.AdapterInstanceNonce,
                adapter.AdapterInstanceNonce));

        byte evidenceFirst = evidence.EvidenceDigest[0];
        byte[] evidenceView = evidence.EvidenceDigest;
        evidenceView[0] ^= 0xFF;
        TestAssert.True(evidence.EvidenceDigest[0] == evidenceFirst);

        TestAssert.True(!TryCreateRawBackend(
            new object(),
            evidence,
            target,
            construction,
            NativePatchLoaderKind.BepInEx,
            "raw-pre-mint",
            out _));

        NativePatchAdapterStateMachine foreignAdapter =
            CreateInstalledAdapter(AdapterNonce, ProcessNonce);
        TestAssert.True(foreignAdapter.TryEnterBackendProbe(
            out NativePatchAdapterStateMachine.BackendProbeEvidence
                foreignEvidence,
            out string foreignEvidenceError), foreignEvidenceError);
        TestAssert.True(!TryFinalizeBackend(
            adapter,
            foreignEvidence,
            target,
            construction,
            NativePatchLoaderKind.BepInEx,
            "foreign",
            out _,
            out _));

        NativePatchAdapterStateMachine.BackendProbeEvidence copiedEvidence =
            ShallowCopy(evidence);
        TestAssert.True(!ReferenceEquals(evidence, copiedEvidence) &&
            copiedEvidence.IsCanonical);
        TestAssert.True(!TryFinalizeBackend(
            adapter,
            copiedEvidence,
            target,
            construction,
            NativePatchLoaderKind.BepInEx,
            "copied",
            out _,
            out _));

        bool[] raceResults = new bool[2];
        NativeBackendReceipt[] raceReceipts = new NativeBackendReceipt[2];
        Parallel.Invoke(
            () => raceResults[0] = TryFinalizeBackend(
                adapter,
                evidence,
                target,
                construction,
                NativePatchLoaderKind.BepInEx,
                "bep",
                out raceReceipts[0],
                out _),
            () => raceResults[1] = TryFinalizeBackend(
                adapter,
                evidence,
                target,
                construction,
                NativePatchLoaderKind.BepInEx,
                "bep",
                out raceReceipts[1],
                out _));
        TestAssert.True(raceResults.Count(value => value) == 1);
        NativeBackendReceipt issued = raceReceipts.Single(value => value != null);
        TestAssert.True(issued.AdapterState ==
            NativePatchAdapterState.BackendProbe &&
            issued.BackendProbePhaseSerial == evidence.PhaseSerial &&
            NativePolicyValue.FixedEquals(
                issued.BackendProbeEvidenceDigest,
                evidence.EvidenceDigest) &&
            NativePolicyValue.FixedEquals(
                issued.AdapterInstanceNonce,
                adapter.AdapterInstanceNonce));
        TestAssert.True(!TryFinalizeBackend(
            adapter,
            evidence,
            target,
            construction,
            NativePatchLoaderKind.BepInEx,
            "second-use",
            out _,
            out _));

        NativePatchAdapterStateMachine failed = CreateInstalledAdapter(
            TestAssert.Sequence(16, 0xB1),
            TestAssert.Sequence(16, 0xC1));
        TestAssert.True(failed.TryEnterBackendProbe(
            out NativePatchAdapterStateMachine.BackendProbeEvidence
                failedEvidence,
            out string failedEvidenceError), failedEvidenceError);
        TestAssert.True(!failed.TryFinalizeBackendProbe(
            failedEvidence,
            target,
            construction,
            NativePatchLoaderKind.BepInEx,
            NativeTargetPolicyCodec.CommonBackendReceiptFlags ^
                NativeBackendReceiptFlags.CriticalCanaryReceipt,
            17,
            CreateComponents("failed"),
            ApiDigest,
            WrapperDigest,
            DetourDigest,
            ProbeDigest,
            CanaryDigest,
            ConstructionApiDigest,
            ConstructionPhysicalDigest,
            out _,
            out _));
        TestAssert.True(failed.RequiresProcessRestart &&
            failed.BackendPassThroughResident &&
            failed.State ==
                NativePatchAdapterState.RestartRequiredResidentFault);
        TestAssert.True(!TryFinalizeBackend(
            failed,
            failedEvidence,
            target,
            construction,
            NativePatchLoaderKind.BepInEx,
            "post-fault",
            out _,
            out _));
    }

    private static void TestStateIssuedReceiptChainAndHardGate()
    {
        ReceiptChain chain = CreateChain(
            NativePatchLoaderKind.BepInEx,
            "bep",
            AdapterNonce,
            ProcessNonce);
        TestAssert.True(NativeTargetPolicyCodec.TryValidateBackendReceipt(
            chain.Backend,
            chain.Target,
            chain.Construction,
            out string backendError), backendError);
        TestAssert.True(NativeConstructionPhysicalReceiptCodec.TryValidate(
            chain.Physical,
            chain.Target,
            chain.Construction,
            chain.Backend,
            out string physicalError), physicalError);

        NativeOperationalCapabilityReceipt copiedCapability =
            ShallowCopy(chain.Capability);
        TestAssert.True(!chain.Adapter.TryEnterOperationalComposite(
            copiedCapability,
            chain.Target,
            chain.Construction,
            chain.Backend,
            chain.Physical,
            true,
            out string copiedError) &&
            copiedError.IndexOf("issued", StringComparison.Ordinal) >= 0);

        var foreignOwner = new NativeOperationalCapabilityOwner(
            chain.Adapter.AdapterInstanceNonce);
        TestAssert.True(NativeOperationalCapabilityReceiptCodec
            .TryCreateEvidenceReceipt(
                foreignOwner,
                chain.Target,
                chain.Construction,
                chain.Backend,
                chain.Physical,
                out NativeOperationalCapabilityReceipt foreignCapability,
                out string foreignCapabilityError), foreignCapabilityError);
        TestAssert.True(!chain.Adapter.TryEnterOperationalComposite(
            foreignCapability,
            chain.Target,
            chain.Construction,
            chain.Backend,
            chain.Physical,
            true,
            out _));

        TestAssert.True(!chain.Adapter.TryEnterOperationalComposite(
            chain.Capability,
            chain.Target,
            chain.Construction,
            chain.Backend,
            chain.Physical,
            false,
            out _));
        TestAssert.True(!chain.Adapter.TryEnterOperationalComposite(
            chain.Capability,
            chain.Target,
            chain.Construction,
            chain.Backend,
            chain.Physical,
            true,
            out string gateError));
        TestAssert.True(gateError.IndexOf(
            "kill switch",
            StringComparison.Ordinal) >= 0 &&
            chain.Adapter.State == NativePatchAdapterState.BackendProbe &&
            !NativePatchCapabilityPolicy.ExternalRuntimeKillSwitchAuthorized);
        TestAssert.True(!chain.Adapter.TryAdvance(
            NativePatchAdapterState.BackendProbe,
            NativePatchAdapterState.Operational,
            out _));
        foreach (NativePatchLeaseKind kind in
            Enum.GetValues<NativePatchLeaseKind>())
        {
            TestAssert.True(!chain.Adapter.TryAcquireLease(
                chain.Capability,
                null,
                kind,
                "hard-gated-" + kind,
                out _,
                out _), "real lease escaped hard gate: " + kind);
        }

        TestAssert.True(!chain.Adapter.TryFinalizeOperationalCapabilityReceipt(
            chain.Target,
            chain.Construction,
            chain.Backend,
            chain.Physical,
            out _,
            out _));
    }

    private static void TestGenerationAndLeaseAdmission()
    {
        ReceiptChain chain = CreateChain(
            NativePatchLoaderKind.BepInEx,
            "bep-generation",
            AdapterNonce,
            ProcessNonce);
        TestAssert.True(chain.Adapter.TestOnlyEnterOperationalComposite(
            chain.Capability,
            chain.Target,
            chain.Construction,
            chain.Backend,
            chain.Physical,
            out string testEntryError), testEntryError);

        TestAssert.True(!chain.Adapter.TryOpenGeneration(
            chain.Capability,
            0,
            out _,
            out _));
        TestAssert.True(!chain.Adapter.TryOpenGeneration(
            chain.Capability,
            2,
            out _,
            out _));
        TestAssert.True(!chain.Adapter.TryOpenGeneration(
            chain.Capability,
            ulong.MaxValue,
            out _,
            out _));
        TestAssert.True(chain.Adapter.TryOpenGeneration(
            chain.Capability,
            1,
            out NativePatchGenerationToken generationOne,
            out string generationOneError), generationOneError);
        TestAssert.True(generationOne.Generation == 1 &&
            NativePolicyValue.FixedEquals(
                generationOne.AdapterInstanceNonce,
                chain.Adapter.AdapterInstanceNonce) &&
            NativePolicyValue.FixedEquals(
                generationOne.ProcessNonce,
                chain.Adapter.ProcessNonce));
        TestAssert.True(!chain.Adapter.TryOpenGeneration(
            chain.Capability,
            2,
            out _,
            out _));

        var leases = new List<NativePatchLease>();
        NativePatchLeaseKind[] singletonKinds =
        {
            NativePatchLeaseKind.PersistentTemplate,
            NativePatchLeaseKind.HostRuntimeRoot,
            NativePatchLeaseKind.RemoteRuntimeRoot,
            NativePatchLeaseKind.MirrorHandler
        };
        foreach (NativePatchLeaseKind kind in singletonKinds)
        {
            TestAssert.True(chain.Adapter.TryAcquireLease(
                chain.Capability,
                generationOne,
                kind,
                "owner-" + kind,
                out NativePatchLease lease,
                out string leaseError), leaseError);
            leases.Add(lease);
            TestAssert.True(!chain.Adapter.TryAcquireLease(
                chain.Capability,
                generationOne,
                kind,
                "duplicate-" + kind,
                out _,
                out _), "singleton duplicate escaped: " + kind);
        }
        TestAssert.True(chain.Adapter.TryAcquireLease(
            chain.Capability,
            generationOne,
            NativePatchLeaseKind.PendingCreationClaim,
            "claim-a",
            out NativePatchLease claimA,
            out string claimAError), claimAError);
        leases.Add(claimA);
        TestAssert.True(chain.Adapter.TryAcquireLease(
            chain.Capability,
            generationOne,
            NativePatchLeaseKind.PendingCreationClaim,
            "claim-b",
            out NativePatchLease claimB,
            out string claimBError), claimBError);
        leases.Add(claimB);
        TestAssert.True(!chain.Adapter.TryAcquireLease(
            chain.Capability,
            generationOne,
            NativePatchLeaseKind.PendingCreationClaim,
            "claim-a",
            out _,
            out _));

        NativePatchLease template = leases[0];
        TestAssert.True(template.Generation == 1 &&
            template.AcquiredState == NativePatchAdapterState.Operational &&
            template.IsBoundTo(chain.Capability) &&
            template.IsBoundToGeneration(generationOne) &&
            NativePolicyValue.FixedEquals(
                template.TargetPolicyDigest,
                chain.Target.TargetPolicyDigest) &&
            NativePolicyValue.FixedEquals(
                template.ConstructionPolicyDigest,
                chain.Construction.ConstructionPolicyDigest));
        byte templateNonceFirst = template.AdapterInstanceNonce[0];
        byte[] templateNonceView = template.AdapterInstanceNonce;
        templateNonceView[0] ^= 0xFF;
        TestAssert.True(template.AdapterInstanceNonce[0] == templateNonceFirst);

        NativePatchLease copiedLease = ShallowCopy(template);
        TestAssert.True(!chain.Adapter.TryReleaseLease(copiedLease, out _));
        TestAssert.True(!chain.Adapter.TryCloseGeneration(
            chain.Capability,
            generationOne,
            out _));

        NativePatchGenerationToken copiedGeneration =
            ShallowCopy(generationOne);
        TestAssert.True(!chain.Adapter.TryAcquireLease(
            chain.Capability,
            copiedGeneration,
            NativePatchLeaseKind.HookInvocation,
            "copied-generation",
            out _,
            out _));
        NativeOperationalCapabilityReceipt copiedCapability =
            ShallowCopy(chain.Capability);
        TestAssert.True(!chain.Adapter.TryAcquireLease(
            copiedCapability,
            generationOne,
            NativePatchLeaseKind.HookInvocation,
            "copied-capability",
            out _,
            out _));

        ReceiptChain foreign = CreateChain(
            NativePatchLoaderKind.BepInEx,
            "foreign-generation",
            AdapterNonce,
            ProcessNonce);
        TestAssert.True(foreign.Adapter.TestOnlyEnterOperationalComposite(
            foreign.Capability,
            foreign.Target,
            foreign.Construction,
            foreign.Backend,
            foreign.Physical,
            out string foreignEntryError), foreignEntryError);
        TestAssert.True(foreign.Adapter.TryOpenGeneration(
            foreign.Capability,
            1,
            out NativePatchGenerationToken foreignGeneration,
            out string foreignGenerationError), foreignGenerationError);
        TestAssert.True(!chain.Adapter.TryAcquireLease(
            foreign.Capability,
            foreignGeneration,
            NativePatchLeaseKind.HookInvocation,
            "foreign-instance",
            out _,
            out _));

        foreach (NativePatchLease lease in leases)
        {
            TestAssert.True(chain.Adapter.TryReleaseLease(
                lease,
                out string releaseError), releaseError);
        }
        TestAssert.True(chain.Adapter.TryCloseGeneration(
            chain.Capability,
            generationOne,
            out string closeOneError), closeOneError);
        TestAssert.True(chain.Adapter.HighestClosedGeneration == 1);
        TestAssert.True(!chain.Adapter.TryAcquireLease(
            chain.Capability,
            generationOne,
            NativePatchLeaseKind.HookInvocation,
            "late-generation-one",
            out _,
            out _));
        TestAssert.True(!chain.Adapter.TryOpenGeneration(
            chain.Capability,
            1,
            out _,
            out _));
        TestAssert.True(!chain.Adapter.TryOpenGeneration(
            chain.Capability,
            3,
            out _,
            out _));
        TestAssert.True(chain.Adapter.TryOpenGeneration(
            chain.Capability,
            2,
            out NativePatchGenerationToken generationTwo,
            out string generationTwoError), generationTwoError);
        TestAssert.True(chain.Adapter.TryCloseGeneration(
            chain.Capability,
            generationTwo,
            out string closeTwoError), closeTwoError);
        TestAssert.True(chain.Adapter.HighestClosedGeneration == 2);
        TestAssert.True(!chain.Adapter.TryOpenGeneration(
            chain.Capability,
            ulong.MaxValue,
            out _,
            out _));

        TestAssert.True(typeof(NativePatchLease).GetConstructors(
            BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic).All(value => value.IsPrivate));
        TestAssert.True(typeof(NativePatchLeaseLedger).GetConstructors(
            BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic).All(value => value.IsPrivate));
        TestAssert.True(typeof(NativePatchGenerationToken).GetConstructors(
            BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic).All(value => value.IsPrivate));
        TestAssert.Throws<InvalidOperationException>(() =>
            NativePatchLeaseLedger.IssueForStateMachine(new object()));
        TestAssert.Throws<InvalidOperationException>(() =>
            NativePatchLease.IssueForStateMachine(
                new object(),
                1,
                NativePatchLeaseKind.HookInvocation,
                "forged",
                null,
                chain.Capability));
    }

    private static void TestRestartRequiredResidentDrain()
    {
        ReceiptChain chain = CreateChain(
            NativePatchLoaderKind.BepInEx,
            "resident",
            TestAssert.Sequence(16, 0xB1),
            TestAssert.Sequence(16, 0xC1));
        TestAssert.True(chain.Adapter.TestOnlyEnterOperationalComposite(
            chain.Capability,
            chain.Target,
            chain.Construction,
            chain.Backend,
            chain.Physical,
            out string entryError), entryError);
        TestAssert.True(chain.Adapter.TryOpenGeneration(
            chain.Capability,
            1,
            out NativePatchGenerationToken generation,
            out string generationError), generationError);
        TestAssert.True(chain.Adapter.TryAcquireLease(
            chain.Capability,
            generation,
            NativePatchLeaseKind.HostRuntimeRoot,
            "resident-host",
            out NativePatchLease lease,
            out string leaseError), leaseError);

        TestAssert.True(chain.Adapter.TryEnterRestartRequiredResidentQuarantine(
            true,
            out string quarantineError), quarantineError);
        TestAssert.True(chain.Adapter.State == NativePatchAdapterState
                .RestartRequiredResidentQuarantine &&
            chain.Adapter.RequiresProcessRestart &&
            chain.Adapter.BackendPassThroughResident);
        TestAssert.True(!chain.Adapter.TryAcquireLease(
            chain.Capability,
            generation,
            NativePatchLeaseKind.HookInvocation,
            "post-fault",
            out _,
            out _));
        TestAssert.True(!chain.Adapter.TryOpenGeneration(
            chain.Capability,
            2,
            out _,
            out _));
        TestAssert.True(!chain.Adapter.TryCloseGeneration(
            chain.Capability,
            generation,
            out _));
        TestAssert.True(!chain.Adapter.TryAdvance(
            chain.Adapter.State,
            NativePatchAdapterState.DetachingCallbacks,
            out _));
        TestAssert.True(!chain.Adapter.TryEnterOperationalComposite(
            chain.Capability,
            chain.Target,
            chain.Construction,
            chain.Backend,
            chain.Physical,
            true,
            out _));

        TestAssert.True(chain.Adapter.TryReleaseLease(
            lease,
            out string releaseError), releaseError);
        TestAssert.True(chain.Adapter.TryCloseGeneration(
            chain.Capability,
            generation,
            out string closeError), closeError);
        TestAssert.True(chain.Adapter.ActiveLeaseCount == 0 &&
            chain.Adapter.HighestClosedGeneration == 1 &&
            chain.Adapter.RequiresProcessRestart &&
            chain.Adapter.BackendPassThroughResident &&
            chain.Adapter.State == NativePatchAdapterState
                .RestartRequiredResidentQuarantine);
        TestAssert.True(!chain.Adapter.TryAdvance(
            chain.Adapter.State,
            NativePatchAdapterState.DetachingCallbacks,
            out _));
        TestAssert.True(!chain.Adapter.TryEnterRestartRequiredResidentQuarantine(
            false,
            out _));
    }

    private static void TestReceiptMutationAndImmutability()
    {
        ReceiptChain chain = CreateChain(
            NativePatchLoaderKind.BepInEx,
            "mutation",
            AdapterNonce,
            ProcessNonce);
        NativeBackendReceipt backend = chain.Backend;
        byte nonceFirst = backend.ProcessNonce[0];
        byte adapterFirst = backend.AdapterInstanceNonce[0];
        byte digestFirst = backend.BackendReceiptDigest[0];
        byte[] nonceView = backend.ProcessNonce;
        byte[] adapterView = backend.AdapterInstanceNonce;
        byte[] digestView = backend.BackendReceiptDigest;
        nonceView[0] ^= 0xFF;
        adapterView[0] ^= 0xFF;
        digestView[0] ^= 0xFF;
        TestAssert.True(backend.ProcessNonce[0] == nonceFirst &&
            backend.AdapterInstanceNonce[0] == adapterFirst &&
            backend.BackendReceiptDigest[0] == digestFirst);

        byte[] changedAdapter = backend.AdapterInstanceNonce;
        changedAdapter[0] ^= 0x01;
        TestAssert.True(!NativeTargetPolicyCodec.TryValidateBackendReceipt(
            ForgeBackend(backend, adapterInstanceNonce: changedAdapter),
            chain.Target,
            chain.Construction,
            out _));
        byte[] changedEvidence = backend.BackendProbeEvidenceDigest;
        changedEvidence[0] ^= 0x01;
        TestAssert.True(!NativeTargetPolicyCodec.TryValidateBackendReceipt(
            ForgeBackend(backend, evidenceDigest: changedEvidence),
            chain.Target,
            chain.Construction,
            out _));
        byte[] changedConstruction = backend.ConstructionPolicyDigest;
        changedConstruction[0] ^= 0x01;
        TestAssert.True(!NativeTargetPolicyCodec.TryValidateBackendReceipt(
            ForgeBackend(backend,
                constructionPolicyDigest: changedConstruction),
            chain.Target,
            chain.Construction,
            out _));
        byte[] changedReceipt = backend.BackendReceiptDigest;
        changedReceipt[0] ^= 0x01;
        TestAssert.True(!NativeTargetPolicyCodec.TryValidateBackendReceipt(
            ForgeBackend(backend, receiptDigest: changedReceipt),
            chain.Target,
            chain.Construction,
            out _));
        TestAssert.True(!NativeTargetPolicyCodec.TryValidateBackendReceipt(
            ForgeBackend(
                backend,
                adapterState:
                    NativePatchAdapterState.InstalledNativeVerified),
            chain.Target,
            chain.Construction,
            out _));
        TestAssert.Throws<ArgumentOutOfRangeException>(() =>
            ForgeBackend(backend, phaseSerial: 0));

        byte physicalFirst = chain.Physical.ReceiptDigest[0];
        byte[] physicalView = chain.Physical.ReceiptDigest;
        physicalView[0] ^= 0xFF;
        byte capabilityFirst = chain.Capability.ReceiptDigest[0];
        byte[] capabilityView = chain.Capability.ReceiptDigest;
        capabilityView[0] ^= 0xFF;
        TestAssert.True(chain.Physical.ReceiptDigest[0] == physicalFirst &&
            chain.Capability.ReceiptDigest[0] == capabilityFirst);

        NativeConstructionApiManifest wrongConstruction =
            NativeConstructionApiCodec.CreatePinnedManifest(
                TestAssert.Sequence(32, 0x41));
        TestAssert.True(!NativeTargetPolicyCodec.TryValidateBackendReceipt(
            backend,
            chain.Target,
            wrongConstruction,
            out _));
    }

    private static void TestGoldenDigests()
    {
        ReceiptChain bepin = CreateChain(
            NativePatchLoaderKind.BepInEx,
            "bep",
            AdapterNonce,
            ProcessNonce);
        ReceiptChain melon = CreateChain(
            NativePatchLoaderKind.MelonLoader,
            "melon",
            TestAssert.Sequence(16, 0xB1),
            ProcessNonce);
        TestAssert.True(NativeNeutralSemanticPolicyCodec.TryComputeDigest(
            bepin.Target,
            bepin.Construction,
            out byte[] neutral,
            out string neutralError), neutralError);
        TestAssert.True(NativePolicyValue.FixedEquals(
            bepin.Target.TargetPolicyDigest,
            melon.Target.TargetPolicyDigest));
        TestAssert.True(NativePolicyValue.FixedEquals(
            bepin.Capability.NeutralSemanticPolicyDigest,
            melon.Capability.NeutralSemanticPolicyDigest));
        TestAssert.True(!NativePolicyValue.FixedEquals(
            bepin.Backend.BackendReceiptDigest,
            melon.Backend.BackendReceiptDigest));
        TestAssert.True(!NativePolicyValue.FixedEquals(
            bepin.Capability.ReceiptDigest,
            melon.Capability.ReceiptDigest));

        string target = Convert.ToHexString(
            bepin.Target.TargetPolicyDigest);
        string construction = Convert.ToHexString(
            bepin.Construction.ConstructionPolicyDigest);
        string neutralDigest = Convert.ToHexString(neutral);
        string evidence = Convert.ToHexString(
            bepin.Evidence.EvidenceDigest);
        string backend = Convert.ToHexString(
            bepin.Backend.BackendReceiptDigest);
        string physical = Convert.ToHexString(
            bepin.Physical.ReceiptDigest);
        string operational = Convert.ToHexString(
            bepin.Capability.ReceiptDigest);
        TestAssert.True(target ==
            "825375DFC3E41C44EF53ED59DFA56F9A33F2E33E08FEFC7A010069E5C45634FF");
        TestAssert.True(construction ==
            "DA7D68CB87E860F4615832F35ED4378512DC30BC917DA42A4FDE3A8D773EBD82");
        TestAssert.True(neutralDigest ==
            "A2C844F1A4C75F0F8B3493289A1812A287CC631CF1D4ED12A8E370175630E9CE");
        TestAssert.True(evidence ==
            "7DC2DE82278F833AFA359FD1C75B07C2EC24666FEA3E391F8BDB3134A5E0E8D6");
        TestAssert.True(backend ==
            "5D3C35BE84543F2170EECFC1EACC8AC708C7E93230991F2BD4D126A96649FE12");
        TestAssert.True(physical ==
            "10BEC92C4796919ADC54F1596372873ED91F6CBC94A7C62AD559EFF82B13580F");
        TestAssert.True(operational ==
            "3C28017C1B9274C0050B10C99FA58C0C1D0C54C83359C897DA26944EFF4D95EB");
    }

    private static ReceiptChain CreateChain(
        NativePatchLoaderKind loader,
        string prefix,
        byte[] adapterNonce,
        byte[] processNonce)
    {
        NativeTargetPolicyManifest target =
            NativePolicyContractTests.CreatePolicy();
        NativeConstructionApiManifest construction = CreateConstruction();
        NativePatchAdapterStateMachine adapter = CreateInstalledAdapter(
            adapterNonce,
            processNonce);
        TestAssert.True(adapter.TryEnterBackendProbe(
            out NativePatchAdapterStateMachine.BackendProbeEvidence evidence,
            out string evidenceError), evidenceError);
        TestAssert.True(TryFinalizeBackend(
            adapter,
            evidence,
            target,
            construction,
            loader,
            prefix,
            out NativeBackendReceipt backend,
            out string backendError), backendError);
        TestAssert.True(adapter.TryFinalizeConstructionPhysicalReceipt(
            target,
            construction,
            backend,
            NativeConstructionPhysicalReceiptCodec.ExactFlags,
            out NativeConstructionPhysicalReceipt physical,
            out string physicalError), physicalError);
        TestAssert.True(adapter.TryFinalizeOperationalCapabilityReceipt(
            target,
            construction,
            backend,
            physical,
            out NativeOperationalCapabilityReceipt capability,
            out string capabilityError), capabilityError);
        return new ReceiptChain(
            adapter,
            evidence,
            target,
            construction,
            backend,
            physical,
            capability);
    }

    private static NativePatchAdapterStateMachine CreateInstalledAdapter(
        byte[] adapterNonce,
        byte[] processNonce)
    {
        var adapter = new NativePatchAdapterStateMachine(
            adapterNonce,
            processNonce);
        NativePatchAdapterState previous = NativePatchAdapterState.Cold;
        NativePatchAdapterState[] path =
        {
            NativePatchAdapterState.InventoryAttested,
            NativePatchAdapterState.PolicyResolved,
            NativePatchAdapterState.TargetsResolved,
            NativePatchAdapterState.Installing,
            NativePatchAdapterState.InstalledNativeVerified
        };
        foreach (NativePatchAdapterState next in path)
        {
            TestAssert.True(adapter.TryAdvance(
                previous,
                next,
                out string error), error);
            previous = next;
        }
        return adapter;
    }

    private static bool TryFinalizeBackend(
        NativePatchAdapterStateMachine adapter,
        NativePatchAdapterStateMachine.BackendProbeEvidence evidence,
        NativeTargetPolicyManifest target,
        NativeConstructionApiManifest construction,
        NativePatchLoaderKind loader,
        string prefix,
        out NativeBackendReceipt receipt,
        out string error)
    {
        return adapter.TryFinalizeBackendProbe(
            evidence,
            target,
            construction,
            loader,
            RequiredFlags(loader),
            17,
            CreateComponents(prefix),
            ApiDigest,
            WrapperDigest,
            DetourDigest,
            ProbeDigest,
            CanaryDigest,
            ConstructionApiDigest,
            ConstructionPhysicalDigest,
            out receipt,
            out error);
    }

    private static bool TryCreateRawBackend(
        object issuerKey,
        NativePatchAdapterStateMachine.BackendProbeEvidence evidence,
        NativeTargetPolicyManifest target,
        NativeConstructionApiManifest construction,
        NativePatchLoaderKind loader,
        string prefix,
        out NativeBackendReceipt receipt)
    {
        return NativeTargetPolicyCodec.TryCreateBackendReceipt(
            issuerKey,
            target,
            construction,
            evidence,
            loader,
            RequiredFlags(loader),
            17,
            CreateComponents(prefix),
            ApiDigest,
            WrapperDigest,
            DetourDigest,
            ProbeDigest,
            CanaryDigest,
            ConstructionApiDigest,
            ConstructionPhysicalDigest,
            out receipt,
            out _);
    }

    private static NativeBackendReceiptFlags RequiredFlags(
        NativePatchLoaderKind loader)
    {
        return NativeTargetPolicyCodec.CommonBackendReceiptFlags |
            (loader == NativePatchLoaderKind.MelonLoader
                ? NativeBackendReceiptFlags.ManagedWrapperDetourExpected
                : 0);
    }

    private static NativeBackendReceipt ForgeBackend(
        NativeBackendReceipt source,
        byte[] adapterInstanceNonce = null,
        ulong? phaseSerial = null,
        byte[] evidenceDigest = null,
        NativePatchAdapterState? adapterState = null,
        byte[] constructionPolicyDigest = null,
        byte[] receiptDigest = null)
    {
        return new NativeBackendReceipt(
            source.Schema,
            source.LoaderKind,
            source.TargetPolicyDigest,
            constructionPolicyDigest ?? source.ConstructionPolicyDigest,
            source.ProcessNonce,
            adapterInstanceNonce ?? source.AdapterInstanceNonce,
            phaseSerial ?? source.BackendProbePhaseSerial,
            evidenceDigest ?? source.BackendProbeEvidenceDigest,
            adapterState ?? source.AdapterState,
            source.ReceiptFlags,
            source.UnityMainThreadId,
            source.TeardownDisposition,
            source.Components,
            source.HarmonyApiBindingDigest,
            source.WrapperTargetReceiptSetDigest,
            source.NativeDetourReceiptSetDigest,
            source.BackendProbeDigest,
            source.CriticalCanaryDigest,
            source.ConstructionApiBindingReceiptDigest,
            source.ConstructionRuntimePhysicalReceiptDigest,
            receiptDigest ?? source.BackendReceiptDigest);
    }

    private static IReadOnlyList<NativeBackendComponentIdentity>
        CreateComponents(string prefix)
    {
        var values = new List<NativeBackendComponentIdentity>();
        for (int index = 0; index < 6; index++)
        {
            values.Add(new NativeBackendComponentIdentity(
                (NativeBackendComponentKind)(index + 1),
                prefix + "-component-" + (index + 1),
                "1.0." + index,
                TestAssert.Sequence(16, checked((byte)(0x10 + index * 3))),
                TestAssert.Sequence(32, checked((byte)(0x20 + index * 3)))));
        }
        return values;
    }

    private static NativeConstructionApiManifest CreateConstruction()
    {
        return NativeConstructionApiCodec.CreatePinnedManifest(
            NativePolicyContractTests.GameIdentity);
    }

    private static T ShallowCopy<T>(T source) where T : class
    {
        MethodInfo clone = typeof(object).GetMethod(
            "MemberwiseClone",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return (T)clone.Invoke(source, null);
    }

    private sealed class ReceiptChain
    {
        internal ReceiptChain(
            NativePatchAdapterStateMachine adapter,
            NativePatchAdapterStateMachine.BackendProbeEvidence evidence,
            NativeTargetPolicyManifest target,
            NativeConstructionApiManifest construction,
            NativeBackendReceipt backend,
            NativeConstructionPhysicalReceipt physical,
            NativeOperationalCapabilityReceipt capability)
        {
            Adapter = adapter;
            Evidence = evidence;
            Target = target;
            Construction = construction;
            Backend = backend;
            Physical = physical;
            Capability = capability;
        }

        internal NativePatchAdapterStateMachine Adapter { get; }
        internal NativePatchAdapterStateMachine.BackendProbeEvidence Evidence
        {
            get;
        }
        internal NativeTargetPolicyManifest Target { get; }
        internal NativeConstructionApiManifest Construction { get; }
        internal NativeBackendReceipt Backend { get; }
        internal NativeConstructionPhysicalReceipt Physical { get; }
        internal NativeOperationalCapabilityReceipt Capability { get; }
    }
}
