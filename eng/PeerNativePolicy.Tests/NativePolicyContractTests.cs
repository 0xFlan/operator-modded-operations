using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using OperatorModdedOperations.NativePatching;

internal static class NativePolicyContractTests
{
    internal static readonly byte[] GameIdentity =
        TestAssert.Sequence(32, 0x11);

    internal static void Run()
    {
        TestAdapterStateAndLeases();
        TestPinnedInventoryAndConstructionBlocker();
        TestConstructionManifest();
        TestNeutralPolicyAndRootLifecycle();
        TestMutationRejection();
        TestBoundsAndImmutability();
        TestCanonicalWriter();
        TestNeutralDomainSeparation();
    }

    internal static NativeTargetPolicyManifest CreatePolicy()
    {
        NativeTargetPolicyManifest manifest =
            NativeTargetPolicyCodec.CreatePinnedNativeGameModesV1Policy(
                GameIdentity);
        TestAssert.True(NativeTargetPolicyCodec.TryValidateTargetPolicyManifest(
            manifest,
            out string error), error);
        return manifest;
    }

    private static void TestAdapterStateAndLeases()
    {
        var adapter = new NativePatchAdapterStateMachine(
            TestAssert.Sequence(16, 0xA1),
            TestAssert.Sequence(16, 0xB1));
        TestAssert.True(adapter.State == NativePatchAdapterState.Cold);
        byte adapterNonceFirst = adapter.AdapterInstanceNonce[0];
        byte[] adapterNonceView = adapter.AdapterInstanceNonce;
        adapterNonceView[0] ^= 0xFF;
        TestAssert.True(adapter.AdapterInstanceNonce.Any(value => value != 0) &&
            adapter.AdapterInstanceNonce[0] == adapterNonceFirst);
        TestAssert.Throws<ArgumentException>(() =>
            new NativePatchAdapterStateMachine(new byte[16]));
        TestAssert.Throws<ArgumentException>(() =>
            new NativePatchAdapterStateMachine(
                TestAssert.Sequence(16, 0xA1),
                new byte[16]));
        TestAssert.True(!adapter.TryEnterBackendProbe(out _, out _));
        TestAssert.True(!adapter.TryAcquireLease(
            null,
            null,
            NativePatchLeaseKind.PersistentTemplate,
            "cold-template",
            out _,
            out _));
        TestAssert.True(!adapter.TryAdvance(
            NativePatchAdapterState.Cold,
            NativePatchAdapterState.PolicyResolved,
            out _));

        NativePatchAdapterState[] path =
        {
            NativePatchAdapterState.InventoryAttested,
            NativePatchAdapterState.PolicyResolved,
            NativePatchAdapterState.TargetsResolved,
            NativePatchAdapterState.Installing,
            NativePatchAdapterState.InstalledNativeVerified
        };
        NativePatchAdapterState previous = NativePatchAdapterState.Cold;
        foreach (NativePatchAdapterState next in path)
        {
            TestAssert.True(adapter.TryAdvance(previous, next, out string error),
                error);
            previous = next;
        }
        TestAssert.True(!adapter.TryAdvance(
            NativePatchAdapterState.InstalledNativeVerified,
            NativePatchAdapterState.BackendProbe,
            out _));
        TestAssert.True(adapter.TryEnterBackendProbe(
            out NativePatchAdapterStateMachine.BackendProbeEvidence evidence,
            out string probeError), probeError);
        TestAssert.True(evidence != null && evidence.IsCanonical &&
            adapter.State == NativePatchAdapterState.BackendProbe);
        TestAssert.True(!adapter.TryEnterBackendProbe(out _, out _));
        TestAssert.True(adapter.State == NativePatchAdapterState.BackendProbe);
        TestAssert.True(!adapter.TryAdvance(
            NativePatchAdapterState.BackendProbe,
            NativePatchAdapterState.Operational,
            out _));
        foreach (NativePatchLeaseKind kind in
            Enum.GetValues<NativePatchLeaseKind>())
        {
            TestAssert.True(!adapter.TryAcquireLease(
                null,
                null,
                kind,
                "blocked-" + kind,
                out _,
                out _), "lease escaped hard gate: " + kind);
        }
        TestAssert.True(!adapter.TryAcquireLease(
            null,
            null,
            (NativePatchLeaseKind)0,
            "invalid-kind",
            out _,
            out _));
        TestAssert.True(adapter.ActiveLeaseCount == 0 &&
            adapter.SnapshotLeases().Count == 0);
        TestAssert.True(adapter.TryEnterRestartRequiredResidentQuarantine(
            false,
            out string blockedFaultError), blockedFaultError);
        TestAssert.True(adapter.RequiresProcessRestart &&
            adapter.BackendPassThroughResident &&
            adapter.State ==
                NativePatchAdapterState.RestartRequiredResidentFault);
        TestAssert.True(!adapter.TryAdvance(
            adapter.State,
            NativePatchAdapterState.Operational,
            out _));

        var faulted = new NativePatchAdapterStateMachine();
        TestAssert.True(faulted.TryEnterRestartRequiredResidentQuarantine(
            true,
            out string quarantineError), quarantineError);
        TestAssert.True(faulted.RequiresProcessRestart &&
            faulted.BackendPassThroughResident);
        TestAssert.True(!faulted.TryEnterRestartRequiredResidentQuarantine(
            true,
            out _));
    }

    private static void TestPinnedInventoryAndConstructionBlocker()
    {
        ReadOnlyCollection<NativeSemanticTargetDescriptor> targets =
            NativePinnedPolicyRegistry.CreatePinnedTargets();
        TestAssert.True(targets.Count == 32);
        TestAssert.True(targets.Count(value => value.Kind ==
            NativeSemanticTargetKind.OperationalHook) == 22);
        TestAssert.True(targets.Count(value => value.Kind ==
            NativeSemanticTargetKind.InvokeDependency) == 10);
        TestAssert.True(targets.Select(value => value.TargetId)
            .SequenceEqual(targets.Select(value => value.TargetId)
                .OrderBy(value => value, StringComparer.Ordinal)));
        TestAssert.True(targets.Select(value => value.TargetId)
            .Distinct(StringComparer.Ordinal).Count() == 32);

        NativeSemanticTargetDescriptor startCoroutine = targets.Single(
            value => value.TargetId == "Unity.StartCoroutineIEnumerator");
        TestAssert.True(startCoroutine.Kind ==
            NativeSemanticTargetKind.InvokeDependency);
        TestAssert.True(startCoroutine.OriginalMetadataToken == 0x06001648);
        TestAssert.True(startCoroutine.NativeRva == 0x04A69BE0);
        TestAssert.True(startCoroutine.OriginalFlags == 0x0086);
        TestAssert.True(startCoroutine.OriginalSlot == 0xFFFF);
        TestAssert.True(startCoroutine.ParameterCount == 1);
        TestAssert.True(startCoroutine.RvaIdentityPolicy ==
            NativeRvaIdentityPolicy.SharedRvaRequiresExactTokenAndSignature);
        TestAssert.True(Convert.ToHexString(startCoroutine.First32NativeSha256) ==
            "97AD0FB68C0348882FF76053A10B56B561B44257E02CD9662B6CFD9EA313E646");

        NativeTargetPolicyManifest manifest = CreatePolicy();
        TestAssert.True(manifest.HasCompletePinnedSemanticInventory);
        TestAssert.True(!manifest.ConstructionApiManifestComplete);
        TestAssert.True(!manifest.RuntimeCapabilityComplete);
        TestAssert.True(manifest.RequiresInactiveScenePlacementImplementation);
        TestAssert.True((manifest.Flags &
            NativeTargetPolicyFlags.ConstructionApiAnchorsRequired) != 0);
    }

    private static void TestConstructionManifest()
    {
        NativeConstructionApiManifest construction =
            NativeConstructionApiCodec.CreatePinnedManifest(GameIdentity);
        TestAssert.True(NativeConstructionApiCodec.TryValidateManifest(
            construction,
            out string error), error);
        TestAssert.True(construction.Apis.Count == 4 &&
            construction.HasCompletePinnedSemanticInventory);
        TestAssert.True(!construction.LoaderLocalRuntimeReceiptComplete &&
            !construction.PhysicalBehaviorReceiptComplete &&
            !construction.RuntimeCapabilityComplete);
        TestAssert.True(construction.Apis.Select(value => value.ApiId)
            .SequenceEqual(construction.Apis.Select(value => value.ApiId)
                .OrderBy(value => value, StringComparer.Ordinal)));
        NativeConstructionApiDescriptor generic = construction.Apis.Single(
            value => value.ApiId ==
                "Unity.AddComponentExactNativeGeneric");
        TestAssert.True(generic.OriginalMetadataToken == 0x060015C8 &&
            generic.NativeRva == 0x01B03F00 &&
            generic.OriginalFlags == 0x0086 &&
            generic.OriginalSlot == 0xFFFF &&
            generic.ParameterCount == 0 &&
            generic.RequiresLoaderClosedGenericReceipt);

        NativeTargetPolicyManifest targets = CreatePolicy();
        TestAssert.True(NativePatchCapabilityPolicy.AreNeutralManifestsComplete(
            targets,
            construction,
            out string manifestsError), manifestsError);
        TestAssert.True(
            !NativePatchCapabilityPolicy.ExternalRuntimeKillSwitchAuthorized);
        NativeConstructionApiManifest wrongGame =
            NativeConstructionApiCodec.CreatePinnedManifest(
                TestAssert.Sequence(32, 0x41));
        TestAssert.True(!NativePatchCapabilityPolicy.AreNeutralManifestsComplete(
            targets,
            wrongGame,
            out _));

        NativeConstructionApiDescriptor[] missing = construction.Apis
            .Take(3).ToArray();
        TestAssert.True(!NativeConstructionApiCodec.TryCreateManifest(
            GameIdentity,
            missing,
            out _,
            out _));
        NativeConstructionApiDescriptor[] duplicated = construction.Apis
            .ToArray();
        duplicated[1] = duplicated[0];
        TestAssert.True(!NativeConstructionApiCodec.TryCreateManifest(
            GameIdentity,
            duplicated,
            out _,
            out _));
        NativeConstructionApiDescriptor[] changed = construction.Apis.ToArray();
        NativeConstructionApiDescriptor source = changed[0];
        changed[0] = new NativeConstructionApiDescriptor(
            source.ApiId,
            source.Kind,
            source.CanonicalSignature,
            source.OriginalMetadataToken,
            source.NativeRva + 1,
            source.OriginalFlags,
            source.OriginalSlot,
            source.ParameterCount,
            source.First32NativeSha256,
            source.RequiresLoaderClosedGenericReceipt);
        TestAssert.True(!NativeConstructionApiCodec.TryCreateManifest(
            GameIdentity,
            changed,
            out _,
            out _));

        byte first = construction.ConstructionPolicyDigest[0];
        byte[] view = construction.ConstructionPolicyDigest;
        view[0] ^= 0xFF;
        TestAssert.True(construction.ConstructionPolicyDigest[0] == first);
    }

    private static void TestNeutralPolicyAndRootLifecycle()
    {
        NativeTargetPolicyManifest bepinPolicy = CreatePolicy();
        NativeTargetPolicyManifest melonPolicy = CreatePolicy();
        TestAssert.True(NativePolicyValue.FixedEquals(
            bepinPolicy.TargetPolicyDigest,
            melonPolicy.TargetPolicyDigest));

        ReadOnlyCollection<NativeRootOwnershipPolicy> roots =
            bepinPolicy.RootPolicies;
        TestAssert.True(roots.Count == 4);
        NativeRootOwnershipPolicy template = roots[0];
        NativeRootOwnershipPolicy probe = roots[1];
        NativeRootOwnershipPolicy host = roots[2];
        NativeRootOwnershipPolicy remote = roots[3];
        TestAssert.True(template.Role == NativeOwnedRootRole.Template &&
            template.SceneResidence ==
                NativeRootSceneResidence.PersistentStagingOutsidePackageScene &&
            template.ConstructionPolicy ==
                NativeRootConstructionPolicy.ExactNativeAddComponent &&
            template.NetworkRoute == NativeRootNetworkRoute.NeverNetworked &&
            template.IsPersistentTemplateSource);
        TestAssert.True(probe.Role == NativeOwnedRootRole.PreflightProbe &&
            probe.SceneResidence ==
                NativeRootSceneResidence.PersistentStagingOutsidePackageScene &&
            probe.ConstructionPolicy ==
                NativeRootConstructionPolicy.ActualInactiveCloneOfFrozenTemplate &&
            probe.NetworkRoute == NativeRootNetworkRoute.NeverNetworked &&
            !probe.IsPersistentTemplateSource);
        TestAssert.True(host.Role == NativeOwnedRootRole.HostRuntime &&
            host.SceneResidence == NativeRootSceneResidence
                .ExactFrozenPackageSceneBeforeNativeBoundary &&
            host.ConstructionPolicy == NativeRootConstructionPolicy
                .ActualInactiveCloneOfFrozenTemplate &&
            host.NetworkRoute == NativeRootNetworkRoute.DirectServerSpawn);
        TestAssert.True(remote.Role == NativeOwnedRootRole.RemoteRuntime &&
            remote.SceneResidence == NativeRootSceneResidence
                .ExactFrozenPackageSceneBeforeHandlerReturn &&
            remote.ConstructionPolicy == NativeRootConstructionPolicy
                .ActualInactiveCloneOfFrozenTemplate &&
            remote.NetworkRoute ==
                NativeRootNetworkRoute.CustomSpawnHandlerReturn);

        TestAssert.True(bepinPolicy.ModePolicies.Count == 2);
        NativeModeLifecyclePolicy pve = bepinPolicy.ModePolicies[0];
        NativeModeLifecyclePolicy pvp = bepinPolicy.ModePolicies[1];
        TestAssert.True(pve.Mode == NativePolicyOperationMode.Pve &&
            pve.LifecyclePolicyRevision == 2 &&
            (pve.Flags & NativeLifecycleSemanticFlags.PveUpdateSuppressedForLifetime) != 0 &&
            (pve.Flags & NativeLifecycleSemanticFlags.PveRaidManagerDisabled) != 0 &&
            (pve.Flags & NativeLifecycleSemanticFlags.PveRunnerRaidTimerOnlyAfterBegin) != 0);
        TestAssert.True(pvp.Mode == NativePolicyOperationMode.Pvp &&
            pvp.LifecyclePolicyRevision == 2 &&
            (pvp.Flags & NativeLifecycleSemanticFlags.PvpNoClockBeforeBegin) != 0 &&
            (pvp.Flags & NativeLifecycleSemanticFlags.PvpOneFreshClockAtBegin) != 0);

        TestAssert.True(bepinPolicy.SemanticRules.Count == 4);
        NativeSemanticPolicyRule dictionary = bepinPolicy.SemanticRules.Single(
            value => value.Kind == NativeSemanticRuleKind.HandlerDictionaryOwnership);
        TestAssert.True(dictionary.Scalars.Single(value =>
            value.Key == "dictionary-count").Value == 3);
        TestAssert.True(dictionary.Scalars.Single(value =>
            value.Key == "exact-remove-count").Value == 2);
        NativeSemanticPolicyRule iterator = bepinPolicy.SemanticRules.Single(
            value => value.Kind == NativeSemanticRuleKind.MovePlayerIteratorLayout);
        TestAssert.True(iterator.Scalars.Single(value =>
            value.Key == "state-offset").Value == 0x10);
        TestAssert.True(iterator.Scalars.Single(value =>
            value.Key == "current-offset").Value == 0x18);
        TestAssert.True(iterator.Scalars.Single(value =>
            value.Key == "position-offset").Value == 0x20);
        TestAssert.True(iterator.Scalars.Single(value =>
            value.Key == "rotation-offset").Value == 0x2C);
        TestAssert.True(iterator.Scalars.Single(value =>
            value.Key == "player-reference-offset").Value == 0x40);
    }

    private static void TestMutationRejection()
    {
        ReadOnlyCollection<NativeSemanticTargetDescriptor> canonicalTargets =
            NativePinnedPolicyRegistry.CreatePinnedTargets();
        ReadOnlyCollection<NativeRootOwnershipPolicy> canonicalRoots =
            NativePinnedPolicyRegistry.CreateRootPolicies();
        ReadOnlyCollection<NativeModeLifecyclePolicy> canonicalModes =
            NativePinnedPolicyRegistry.CreateModePolicies();
        ReadOnlyCollection<NativeSemanticPolicyRule> canonicalRules =
            NativePinnedPolicyRegistry.CreateSemanticRules();

        TestAssert.True(!TryCreate(
            canonicalTargets.Take(31).ToArray(),
            canonicalRoots,
            canonicalModes,
            canonicalRules));
        TestAssert.True(!TryCreate(
            canonicalTargets.Concat(new[] { canonicalTargets[31] }).ToArray(),
            canonicalRoots,
            canonicalModes,
            canonicalRules));
        NativeSemanticTargetDescriptor[] swapped = canonicalTargets.ToArray();
        (swapped[0], swapped[1]) = (swapped[1], swapped[0]);
        TestAssert.True(!TryCreate(swapped, canonicalRoots, canonicalModes,
            canonicalRules));
        NativeSemanticTargetDescriptor[] duplicate = canonicalTargets.ToArray();
        duplicate[1] = duplicate[0];
        TestAssert.True(!TryCreate(duplicate, canonicalRoots, canonicalModes,
            canonicalRules));

        for (int mutation = 0; mutation < 9; mutation++)
        {
            NativeSemanticTargetDescriptor[] changed = canonicalTargets.ToArray();
            changed[0] = MutateTarget(canonicalTargets[0], mutation);
            TestAssert.True(!TryCreate(changed, canonicalRoots, canonicalModes,
                canonicalRules), "target mutation was accepted: " + mutation);
        }

        NativeRootOwnershipPolicy[] changedRoots = canonicalRoots.ToArray();
        NativeRootOwnershipPolicy host = changedRoots[2];
        changedRoots[2] = new NativeRootOwnershipPolicy(
            host.Role,
            host.SceneResidence,
            NativeRootConstructionPolicy.ExactNativeAddComponent,
            host.ManualInitializeNetworkBehavioursCalls,
            host.NetworkRoute,
            host.ActivationBoundary,
            host.DisposalPolicy,
            host.IsPersistentTemplateSource);
        TestAssert.True(!TryCreate(canonicalTargets, changedRoots,
            canonicalModes, canonicalRules));

        NativeModeLifecyclePolicy[] changedModes = canonicalModes.ToArray();
        NativeModeLifecyclePolicy pve = changedModes[0];
        changedModes[0] = new NativeModeLifecyclePolicy(
            pve.Mode,
            pve.LifecyclePolicyRevision,
            pve.Flags ^ NativeLifecycleSemanticFlags.PveRaidManagerDisabled,
            pve.GameModeInitializeSuccessfulReturns,
            pve.GameModeStartOriginalBodyMaximum,
            pve.OriginalOnStartClientBodyMaximum,
            pve.PreBeginClockMoveNextMaximum,
            pve.RunnerRevision,
            pve.BeginReleaseRevision);
        TestAssert.True(!TryCreate(canonicalTargets, canonicalRoots,
            changedModes, canonicalRules));

        NativeSemanticPolicyRule[] changedRules = canonicalRules.ToArray();
        NativeSemanticPolicyRule rule = changedRules[0];
        NativeSemanticRuleScalar[] scalars = rule.Scalars.ToArray();
        scalars[0] = new NativeSemanticRuleScalar(
            scalars[0].Key,
            scalars[0].Value + 1);
        changedRules[0] = new NativeSemanticPolicyRule(
            rule.RuleId,
            rule.Kind,
            rule.Revision,
            rule.Flags,
            scalars);
        TestAssert.True(!TryCreate(canonicalTargets, canonicalRoots,
            canonicalModes, changedRules));
    }

    private static void TestBoundsAndImmutability()
    {
        TestAssert.Throws<ArgumentException>(() =>
            NativePolicyValue.RequireString(string.Empty, "value"));
        TestAssert.Throws<ArgumentException>(() =>
            NativePolicyValue.RequireString("bad\0value", "value"));
        TestAssert.Throws<ArgumentException>(() =>
            NativePolicyValue.RequireString("bad\rvalue", "value"));
        TestAssert.Throws<ArgumentException>(() =>
            NativePolicyValue.RequireString("bad\nvalue", "value"));
        TestAssert.Throws<ArgumentException>(() =>
            NativePolicyValue.RequireString(new string('x', 257), "value"));
        TestAssert.Throws<ArgumentException>(() =>
            NativePolicyValue.RequireString("\uD800", "value"));
        TestAssert.Throws<ArgumentException>(() =>
            NativePolicyValue.CloneDigest(new byte[32], "digest"));
        TestAssert.Throws<ArgumentException>(() =>
            new NativeSemanticRuleScalar("negative", -1));
        TestAssert.Throws<ArgumentException>(() =>
            new NativeSemanticPolicyRule(
                "unordered",
                NativeSemanticRuleKind.NetworkIdentityGraph,
                1,
                NativeSemanticRuleFlags.ExactManualInitializeCounts,
                new[]
                {
                    new NativeSemanticRuleScalar("b", 1),
                    new NativeSemanticRuleScalar("a", 2)
                }));
        TestAssert.Throws<ArgumentException>(() =>
            new NativeSemanticPolicyRule(
                "too-many",
                NativeSemanticRuleKind.NetworkIdentityGraph,
                1,
                NativeSemanticRuleFlags.ExactManualInitializeCounts,
                Enumerable.Range(0, 33).Select(index =>
                    new NativeSemanticRuleScalar(
                        index.ToString("D2"),
                        index)).ToArray()));

        byte[] inputIdentity = TestAssert.Sequence(32, 0x31);
        NativeTargetPolicyManifest manifest =
            NativeTargetPolicyCodec.CreatePinnedNativeGameModesV1Policy(
                inputIdentity);
        byte firstIdentity = manifest.GameContentIdentityDigest[0];
        byte firstPolicy = manifest.TargetPolicyDigest[0];
        inputIdentity[0] ^= 0xFF;
        byte[] identityView = manifest.GameContentIdentityDigest;
        byte[] policyView = manifest.TargetPolicyDigest;
        identityView[0] ^= 0xFF;
        policyView[0] ^= 0xFF;
        TestAssert.True(manifest.GameContentIdentityDigest[0] == firstIdentity);
        TestAssert.True(manifest.TargetPolicyDigest[0] == firstPolicy);

        NativeSemanticTargetDescriptor target = manifest.Targets[0];
        byte originalHash = target.First32NativeSha256[0];
        byte[] hashView = target.First32NativeSha256;
        hashView[0] ^= 0xFF;
        TestAssert.True(target.First32NativeSha256[0] == originalHash);
        TestAssert.Throws<NotSupportedException>(() =>
            ((IList<NativeSemanticTargetDescriptor>)manifest.Targets)
                .Add(target));
    }

    private static void TestCanonicalWriter()
    {
        using var writer = new NativePolicyCanonicalWriter(64);
        writer.WriteDomain("test-domain\0");
        writer.WriteUInt16(0x1234);
        writer.WriteUInt32(0x89ABCDEF);
        writer.WriteUInt64(0x0102030405060708UL);
        writer.WriteInt32(-2);
        writer.WriteBoolean(true);
        writer.WriteString("z");
        byte[] bytes = writer.Complete();
        TestAssert.True(Convert.ToHexString(bytes) ==
            "746573742D646F6D61696E003412EFCDAB890807060504030201FEFFFFFF01010000007A");
        byte[] repeated = writer.Complete();
        repeated[0] ^= 0xFF;
        TestAssert.True(NativePolicyValue.FixedEquals(bytes, writer.Complete()));
        using var bounded = new NativePolicyCanonicalWriter(1);
        TestAssert.Throws<InvalidDataException>(() =>
            bounded.WriteUInt16(1));
        using var badDomain = new NativePolicyCanonicalWriter(32);
        TestAssert.Throws<InvalidDataException>(() =>
            badDomain.WriteDomain("missing-terminator"));
    }

    private static void TestNeutralDomainSeparation()
    {
        Type[] neutralTypes =
        {
            typeof(NativeTargetPolicyManifest),
            typeof(NativeSemanticTargetDescriptor),
            typeof(NativeRootOwnershipPolicy),
            typeof(NativeModeLifecyclePolicy),
            typeof(NativeSemanticPolicyRule)
        };
        string[] forbidden =
        {
            "Loader", "Backend", "Harmony", "WrapperToken", "FieldToken",
            "MethodPointer", "VirtualMethodPointer", "AssemblyMvid"
        };
        foreach (Type type in neutralTypes)
        {
            IEnumerable<string> names = type.GetMembers(
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic).Select(member => member.Name);
            foreach (string name in names)
            {
                TestAssert.True(!forbidden.Any(value =>
                    name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0),
                    type.Name + " leaked backend-local member " + name);
            }
        }
        TestAssert.True(typeof(NativeBackendReceipt).GetProperty(
            "LoaderKind",
            BindingFlags.Instance | BindingFlags.NonPublic) != null);
    }

    private static bool TryCreate(
        IReadOnlyList<NativeSemanticTargetDescriptor> targets,
        IReadOnlyList<NativeRootOwnershipPolicy> roots,
        IReadOnlyList<NativeModeLifecyclePolicy> modes,
        IReadOnlyList<NativeSemanticPolicyRule> rules)
    {
        return NativeTargetPolicyCodec.TryCreateNativeGameModesV1Policy(
            GameIdentity,
            targets,
            roots,
            modes,
            rules,
            out _,
            out _);
    }

    private static NativeSemanticTargetDescriptor MutateTarget(
        NativeSemanticTargetDescriptor source,
        int mutation)
    {
        byte[] hash = source.First32NativeSha256;
        if (mutation == 5)
            hash[0] ^= 0x01;
        return new NativeSemanticTargetDescriptor(
            source.TargetId,
            source.Kind,
            mutation == 0 ? source.CanonicalSignature + "x" :
                source.CanonicalSignature,
            mutation == 1 ? source.OriginalMetadataToken + 1 :
                source.OriginalMetadataToken,
            mutation == 2 ? source.NativeRva + 1 : source.NativeRva,
            mutation == 3 ? checked((ushort)(source.OriginalFlags ^ 1)) :
                source.OriginalFlags,
            mutation == 4 ? checked((ushort)(source.OriginalSlot - 1)) :
                source.OriginalSlot,
            mutation == 6 ? checked((byte)(source.ParameterCount + 1)) :
                source.ParameterCount,
            hash,
            source.BundleKind,
            source.InvocationKind,
            mutation == 7
                ? (source.BodyPolicy ==
                    NativeSemanticBodyPolicy.SuppressOwnedForLifetime
                        ? NativeSemanticBodyPolicy.PermitOneGenerationInitialize
                        : NativeSemanticBodyPolicy.SuppressOwnedForLifetime)
                : source.BodyPolicy,
            mutation == 8
                ? NativeRvaIdentityPolicy.SharedRvaRequiresExactTokenAndSignature
                : source.RvaIdentityPolicy);
    }
}
