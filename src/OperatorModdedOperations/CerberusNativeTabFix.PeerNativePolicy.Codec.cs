using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace OperatorModdedOperations.NativePatching
{
    internal sealed class NativePolicyCanonicalWriter : IDisposable
    {
        private readonly MemoryStream stream = new MemoryStream();
        private readonly int maximumBytes;

        internal NativePolicyCanonicalWriter(int maximumBytes)
        {
            if (maximumBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            this.maximumBytes = maximumBytes;
        }

        internal void WriteDomain(string domain)
        {
            if (string.IsNullOrEmpty(domain) ||
                domain[domain.Length - 1] != '\0' ||
                domain.Substring(0, domain.Length - 1).IndexOf('\0') >= 0)
            {
                throw new InvalidDataException("canonical domain is invalid");
            }
            WriteRaw(NativePolicyValue.StrictUtf8.GetBytes(domain));
        }

        internal void WriteByte(byte value)
        {
            EnsureCapacity(1);
            stream.WriteByte(value);
        }

        internal void WriteBoolean(bool value)
        {
            WriteByte(value ? (byte)1 : (byte)0);
        }

        internal void WriteUInt16(ushort value)
        {
            WriteByte((byte)value);
            WriteByte((byte)(value >> 8));
        }

        internal void WriteUInt32(uint value)
        {
            WriteByte((byte)value);
            WriteByte((byte)(value >> 8));
            WriteByte((byte)(value >> 16));
            WriteByte((byte)(value >> 24));
        }

        internal void WriteInt32(int value)
        {
            WriteUInt32(unchecked((uint)value));
        }

        internal void WriteUInt64(ulong value)
        {
            WriteUInt32((uint)value);
            WriteUInt32((uint)(value >> 32));
        }

        internal void WriteInt64(long value)
        {
            WriteUInt64(unchecked((ulong)value));
        }

        internal void WriteBytes(byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            WriteRaw(value);
        }

        internal void WriteString(string value)
        {
            string bounded = NativePolicyValue.RequireString(value, nameof(value));
            byte[] encoded = NativePolicyValue.StrictUtf8.GetBytes(bounded);
            WriteUInt32(checked((uint)encoded.Length));
            WriteRaw(encoded);
        }

        internal byte[] Complete()
        {
            if (stream.Length > maximumBytes)
                throw new InvalidDataException("canonical preimage exceeded its bound");
            return stream.ToArray();
        }

        public void Dispose()
        {
            stream.Dispose();
        }

        private void WriteRaw(byte[] value)
        {
            EnsureCapacity(value.Length);
            stream.Write(value, 0, value.Length);
        }

        private void EnsureCapacity(int additionalBytes)
        {
            if (additionalBytes < 0 || stream.Length + additionalBytes > maximumBytes)
            {
                throw new InvalidDataException("canonical preimage exceeded its bound");
            }
        }
    }

    internal static class NativeTargetPolicyCodec
    {
        internal const ushort TargetPolicySchema = 1;
        internal const ushort TargetPolicyRevision = 1;
        internal const ushort LifecyclePolicyRevision = 2;
        internal const ushort HandlerOnlyContractRevision = 1;
        internal const int CallbackPriority = 800;
        internal const int ExpectedTargetCount = 32;
        internal const int ExpectedHookTargetCount = 22;
        internal const int ExpectedInvokeDependencyCount = 10;
        internal const int ExpectedRootPolicyCount = 4;
        internal const int ExpectedModePolicyCount = 2;
        internal const int ExpectedSemanticRuleCount = 4;
        internal const int MaximumTargetPolicyPreimageBytes = 16384;
        internal const int MaximumBackendReceiptPreimageBytes = 8192;
        internal const ushort BackendReceiptSchema = 3;
        internal const string PatchSetId = "NativeGameModesV1";
        internal const string OperationalOwnerId =
            "operator.moddedoperations.native-game-mode-hooks.v1";

        internal const NativeTargetPolicyFlags ExactPolicyFlags =
            NativeTargetPolicyFlags.AllOrNothingTargetSet |
            NativeTargetPolicyFlags.ExactTargetSpecificCallbackTriplets |
            NativeTargetPolicyFlags.ForeignPatchCollisionFailsClosed |
            NativeTargetPolicyFlags.SameCallbackForeignOwnerQuarantines |
            NativeTargetPolicyFlags.UnityMainThreadNoYieldTransitions |
            NativeTargetPolicyFlags.HandlerOnlyNoPrefabRegistration |
            NativeTargetPolicyFlags.HotUnloadUnsupported |
            NativeTargetPolicyFlags.HotReloadUnsupported |
            NativeTargetPolicyFlags.BackendPassThroughResidentTerminal |
            NativeTargetPolicyFlags.LoaderNeutralPolicyOnly |
            NativeTargetPolicyFlags.LoaderLocalReceiptsExcluded |
            NativeTargetPolicyFlags.CrossLoaderPhysicalParityUnproven |
            NativeTargetPolicyFlags.ExactInactiveSceneResidenceRequired |
            NativeTargetPolicyFlags.ExactNativeConstructionRequired |
            NativeTargetPolicyFlags.ActualCloneNonAliasValidationRequired |
            NativeTargetPolicyFlags.ConstructionApiAnchorsRequired;

        internal const NativeBackendReceiptFlags CommonBackendReceiptFlags =
            NativeBackendReceiptFlags.ExactHarmonyApiBinding |
            NativeBackendReceiptFlags.ExactWrapperManifest |
            NativeBackendReceiptFlags.ExactCallbackBridge |
            NativeBackendReceiptFlags.CompleteResolvedTargetReceipts |
            NativeBackendReceiptFlags.NativeDetourReceipts |
            NativeBackendReceiptFlags.BackendProbeReceipt |
            NativeBackendReceiptFlags.CriticalCanaryReceipt |
            NativeBackendReceiptFlags.UnityMainThreadObserved |
            NativeBackendReceiptFlags.ExactConstructionApiBinding |
            NativeBackendReceiptFlags.ExactConstructionRuntimePhysicalReceipt;

        private static readonly byte[] PinnedGameAssemblySha256 =
            NativePinnedPolicyRegistry.Hex(
                "D4347448524D79A7E367F2B22D66BB3A21E1F3733D32ABA37FC7E3E270A620DE");
        private static readonly byte[] PinnedGlobalMetadataSha256 =
            NativePinnedPolicyRegistry.Hex(
                "4B50F42D1280466F757594FDAA6582180EA55614DE82B45240C930046B66405C");

        internal static NativeTargetPolicyManifest
            CreatePinnedNativeGameModesV1Policy(
                byte[] gameContentIdentityDigest)
        {
            if (!TryCreateNativeGameModesV1Policy(
                    gameContentIdentityDigest,
                    NativePinnedPolicyRegistry.CreatePinnedTargets(),
                    NativePinnedPolicyRegistry.CreateRootPolicies(),
                    NativePinnedPolicyRegistry.CreateModePolicies(),
                    NativePinnedPolicyRegistry.CreateSemanticRules(),
                    out NativeTargetPolicyManifest manifest,
                    out string error))
            {
                throw new InvalidDataException(error);
            }
            return manifest;
        }

        internal static bool TryCreateNativeGameModesV1Policy(
            byte[] gameContentIdentityDigest,
            IReadOnlyList<NativeSemanticTargetDescriptor> targets,
            IReadOnlyList<NativeRootOwnershipPolicy> rootPolicies,
            IReadOnlyList<NativeModeLifecyclePolicy> modePolicies,
            IReadOnlyList<NativeSemanticPolicyRule> semanticRules,
            out NativeTargetPolicyManifest manifest,
            out string error)
        {
            manifest = null;
            error = string.Empty;
            try
            {
                byte[] gameIdentity = NativePolicyValue.CloneDigest(
                    gameContentIdentityDigest,
                    nameof(gameContentIdentityDigest));
                NativeSemanticTargetDescriptor[] targetArray =
                    (targets ?? throw new ArgumentNullException(nameof(targets)))
                    .ToArray();
                NativeRootOwnershipPolicy[] rootArray =
                    (rootPolicies ??
                        throw new ArgumentNullException(nameof(rootPolicies)))
                    .ToArray();
                NativeModeLifecyclePolicy[] modeArray =
                    (modePolicies ??
                        throw new ArgumentNullException(nameof(modePolicies)))
                    .ToArray();
                NativeSemanticPolicyRule[] ruleArray =
                    (semanticRules ??
                        throw new ArgumentNullException(nameof(semanticRules)))
                    .ToArray();
                if (!TryValidatePolicyLists(
                        targetArray,
                        rootArray,
                        modeArray,
                        ruleArray,
                        out error))
                {
                    return false;
                }
                byte[] digest = ComputeTargetPolicyDigest(
                    gameIdentity,
                    targetArray,
                    rootArray,
                    modeArray,
                    ruleArray);
                manifest = new NativeTargetPolicyManifest(
                    TargetPolicySchema,
                    TargetPolicyRevision,
                    gameIdentity,
                    PinnedGameAssemblySha256,
                    PinnedGlobalMetadataSha256,
                    PatchSetId,
                    OperationalOwnerId,
                    CallbackPriority,
                    LifecyclePolicyRevision,
                    HandlerOnlyContractRevision,
                    ExactPolicyFlags,
                    targetArray,
                    rootArray,
                    modeArray,
                    ruleArray,
                    digest);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException ||
                ex is InvalidDataException || ex is OverflowException)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static bool TryValidateTargetPolicyManifest(
            NativeTargetPolicyManifest manifest,
            out string error)
        {
            error = string.Empty;
            try
            {
                if (manifest == null || manifest.Schema != TargetPolicySchema ||
                    manifest.PolicyRevision != TargetPolicyRevision ||
                    !NativePolicyValue.FixedEquals(
                        manifest.GameAssemblySha256,
                        PinnedGameAssemblySha256) ||
                    !NativePolicyValue.FixedEquals(
                        manifest.GlobalMetadataSha256,
                        PinnedGlobalMetadataSha256) ||
                    !string.Equals(
                        manifest.PatchSetId,
                        PatchSetId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        manifest.OperationalOwnerId,
                        OperationalOwnerId,
                        StringComparison.Ordinal) ||
                    manifest.CallbackPriority != CallbackPriority ||
                    manifest.LifecyclePolicyRevision !=
                        LifecyclePolicyRevision ||
                    manifest.HandlerOnlyContractRevision !=
                        HandlerOnlyContractRevision ||
                    manifest.Flags != ExactPolicyFlags ||
                    !TryValidatePolicyLists(
                        manifest.Targets,
                        manifest.RootPolicies,
                        manifest.ModePolicies,
                        manifest.SemanticRules,
                        out error))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "native target policy manifest is noncanonical";
                    return false;
                }
                byte[] expected = ComputeTargetPolicyDigest(
                    manifest.GameContentIdentityDigest,
                    manifest.Targets,
                    manifest.RootPolicies,
                    manifest.ModePolicies,
                    manifest.SemanticRules);
                if (!NativePolicyValue.FixedEquals(
                        expected,
                        manifest.TargetPolicyDigest))
                {
                    error = "native target policy digest mismatch";
                    return false;
                }
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException ||
                ex is InvalidDataException || ex is OverflowException)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static byte[] ComputeTargetPolicyDigest(
            byte[] gameContentIdentityDigest,
            IReadOnlyList<NativeSemanticTargetDescriptor> targets,
            IReadOnlyList<NativeRootOwnershipPolicy> rootPolicies,
            IReadOnlyList<NativeModeLifecyclePolicy> modePolicies,
            IReadOnlyList<NativeSemanticPolicyRule> semanticRules)
        {
            using (var writer = new NativePolicyCanonicalWriter(
                MaximumTargetPolicyPreimageBytes))
            {
                writer.WriteDomain("operator-native-target-policy-v1\0");
                writer.WriteUInt16(TargetPolicySchema);
                writer.WriteUInt16(TargetPolicyRevision);
                writer.WriteBytes(NativePolicyValue.CloneDigest(
                    gameContentIdentityDigest,
                    nameof(gameContentIdentityDigest)));
                writer.WriteBytes(PinnedGameAssemblySha256);
                writer.WriteBytes(PinnedGlobalMetadataSha256);
                writer.WriteString(PatchSetId);
                writer.WriteString(OperationalOwnerId);
                writer.WriteInt32(CallbackPriority);
                writer.WriteUInt16(LifecyclePolicyRevision);
                writer.WriteUInt16(HandlerOnlyContractRevision);
                writer.WriteUInt32((uint)ExactPolicyFlags);
                writer.WriteUInt16(checked((ushort)targets.Count));
                foreach (NativeSemanticTargetDescriptor target in targets)
                    WriteTarget(writer, target);
                writer.WriteByte(checked((byte)rootPolicies.Count));
                foreach (NativeRootOwnershipPolicy root in rootPolicies)
                    WriteRootPolicy(writer, root);
                writer.WriteByte(checked((byte)modePolicies.Count));
                foreach (NativeModeLifecyclePolicy mode in modePolicies)
                    WriteModePolicy(writer, mode);
                writer.WriteByte(checked((byte)semanticRules.Count));
                foreach (NativeSemanticPolicyRule rule in semanticRules)
                    WriteSemanticRule(writer, rule);
                return Sha256(writer.Complete());
            }
        }

        private static bool TryValidatePolicyLists(
            IReadOnlyList<NativeSemanticTargetDescriptor> targets,
            IReadOnlyList<NativeRootOwnershipPolicy> roots,
            IReadOnlyList<NativeModeLifecyclePolicy> modes,
            IReadOnlyList<NativeSemanticPolicyRule> rules,
            out string error)
        {
            error = string.Empty;
            if (targets == null || targets.Count != ExpectedTargetCount ||
                targets.Count(value => value?.Kind ==
                    NativeSemanticTargetKind.OperationalHook) !=
                    ExpectedHookTargetCount ||
                targets.Count(value => value?.Kind ==
                    NativeSemanticTargetKind.InvokeDependency) !=
                    ExpectedInvokeDependencyCount ||
                roots == null || roots.Count != ExpectedRootPolicyCount ||
                modes == null || modes.Count != ExpectedModePolicyCount ||
                rules == null || rules.Count != ExpectedSemanticRuleCount)
            {
                error = "native target policy list counts are not exact";
                return false;
            }

            ReadOnlyCollection<NativeSemanticTargetDescriptor> expectedTargets =
                NativePinnedPolicyRegistry.CreatePinnedTargets();
            for (int index = 0; index < targets.Count; index++)
            {
                if (targets[index] == null ||
                    (index > 0 && string.CompareOrdinal(
                        targets[index - 1].TargetId,
                        targets[index].TargetId) >= 0) ||
                    !targets[index].SemanticEquals(expectedTargets[index]))
                {
                    error = "native target rows are missing, changed, duplicated, or unordered";
                    return false;
                }
            }

            ReadOnlyCollection<NativeRootOwnershipPolicy> expectedRoots =
                NativePinnedPolicyRegistry.CreateRootPolicies();
            for (int index = 0; index < roots.Count; index++)
            {
                if (roots[index] == null ||
                    (index > 0 && roots[index - 1].Role >= roots[index].Role) ||
                    !roots[index].SemanticEquals(expectedRoots[index]))
                {
                    error = "native T/P/H/R ownership policy is changed or unordered";
                    return false;
                }
            }

            ReadOnlyCollection<NativeModeLifecyclePolicy> expectedModes =
                NativePinnedPolicyRegistry.CreateModePolicies();
            for (int index = 0; index < modes.Count; index++)
            {
                if (modes[index] == null ||
                    (index > 0 && modes[index - 1].Mode >= modes[index].Mode) ||
                    !modes[index].SemanticEquals(expectedModes[index]))
                {
                    error = "native lifecycle policy is changed or unordered";
                    return false;
                }
            }

            ReadOnlyCollection<NativeSemanticPolicyRule> expectedRules =
                NativePinnedPolicyRegistry.CreateSemanticRules();
            for (int index = 0; index < rules.Count; index++)
            {
                if (rules[index] == null ||
                    (index > 0 && string.CompareOrdinal(
                        rules[index - 1].RuleId,
                        rules[index].RuleId) >= 0) ||
                    !rules[index].SemanticEquals(expectedRules[index]))
                {
                    error = "native ancillary semantic rules are changed or unordered";
                    return false;
                }
            }
            return true;
        }

        private static void WriteTarget(
            NativePolicyCanonicalWriter writer,
            NativeSemanticTargetDescriptor target)
        {
            writer.WriteString(target.TargetId);
            writer.WriteByte((byte)target.Kind);
            writer.WriteString(target.CanonicalSignature);
            writer.WriteUInt32(target.OriginalMetadataToken);
            writer.WriteUInt32(target.NativeRva);
            writer.WriteUInt16(target.OriginalFlags);
            writer.WriteUInt16(target.OriginalSlot);
            writer.WriteByte(target.ParameterCount);
            writer.WriteBytes(target.First32NativeSha256);
            writer.WriteByte((byte)target.BundleKind);
            writer.WriteByte((byte)target.InvocationKind);
            writer.WriteByte((byte)target.BodyPolicy);
            writer.WriteByte((byte)target.RvaIdentityPolicy);
        }

        private static void WriteRootPolicy(
            NativePolicyCanonicalWriter writer,
            NativeRootOwnershipPolicy root)
        {
            writer.WriteByte((byte)root.Role);
            writer.WriteByte((byte)root.SceneResidence);
            writer.WriteByte((byte)root.ConstructionPolicy);
            writer.WriteByte(root.ManualInitializeNetworkBehavioursCalls);
            writer.WriteByte((byte)root.NetworkRoute);
            writer.WriteByte((byte)root.ActivationBoundary);
            writer.WriteByte((byte)root.DisposalPolicy);
            writer.WriteBoolean(root.IsPersistentTemplateSource);
        }

        private static void WriteModePolicy(
            NativePolicyCanonicalWriter writer,
            NativeModeLifecyclePolicy mode)
        {
            writer.WriteByte((byte)mode.Mode);
            writer.WriteUInt16(mode.LifecyclePolicyRevision);
            writer.WriteUInt64((ulong)mode.Flags);
            writer.WriteByte(mode.GameModeInitializeSuccessfulReturns);
            writer.WriteByte(mode.GameModeStartOriginalBodyMaximum);
            writer.WriteByte(mode.OriginalOnStartClientBodyMaximum);
            writer.WriteByte(mode.PreBeginClockMoveNextMaximum);
            writer.WriteString(mode.RunnerRevision);
            writer.WriteString(mode.BeginReleaseRevision);
        }

        private static void WriteSemanticRule(
            NativePolicyCanonicalWriter writer,
            NativeSemanticPolicyRule rule)
        {
            writer.WriteString(rule.RuleId);
            writer.WriteByte((byte)rule.Kind);
            writer.WriteUInt16(rule.Revision);
            writer.WriteUInt64((ulong)rule.Flags);
            writer.WriteByte(checked((byte)rule.Scalars.Count));
            foreach (NativeSemanticRuleScalar scalar in rule.Scalars)
            {
                writer.WriteString(scalar.Key);
                writer.WriteInt64(scalar.Value);
            }
        }

        internal static bool TryCreateBackendReceipt(
            object stateIssuerKey,
            NativeTargetPolicyManifest acceptedPolicy,
            NativeConstructionApiManifest acceptedConstruction,
            NativePatchAdapterStateMachine.BackendProbeEvidence probeEvidence,
            NativePatchLoaderKind loaderKind,
            NativeBackendReceiptFlags receiptFlags,
            int unityMainThreadId,
            IReadOnlyList<NativeBackendComponentIdentity> components,
            byte[] harmonyApiBindingDigest,
            byte[] wrapperTargetReceiptSetDigest,
            byte[] nativeDetourReceiptSetDigest,
            byte[] backendProbeDigest,
            byte[] criticalCanaryDigest,
            byte[] constructionApiBindingReceiptDigest,
            byte[] constructionRuntimePhysicalReceiptDigest,
            out NativeBackendReceipt receipt,
            out string error)
        {
            receipt = null;
            error = string.Empty;
            try
            {
                if (!NativePatchAdapterStateMachine.IsLeaseIssuerKey(
                        stateIssuerKey) ||
                    !NativePatchCapabilityPolicy.AreNeutralManifestsComplete(
                        acceptedPolicy,
                        acceptedConstruction,
                        out error) || probeEvidence == null ||
                    !probeEvidence.IsCanonical)
                    return false;
                byte[] nonce = NativePolicyValue.CloneFixed(
                    probeEvidence.ProcessNonce,
                    NativePolicyValue.ProcessNonceBytes,
                    nameof(probeEvidence.ProcessNonce),
                    requireNonzero: true);
                byte[] adapterNonce = NativePolicyValue.CloneFixed(
                    probeEvidence.AdapterInstanceNonce,
                    NativePolicyValue.ProcessNonceBytes,
                    nameof(probeEvidence.AdapterInstanceNonce),
                    requireNonzero: true);
                const NativePatchAdapterState adapterState =
                    NativePatchAdapterState.BackendProbe;
                const NativeBackendTeardownDisposition teardownDisposition =
                    NativeBackendTeardownDisposition.NotRequested;
                NativeBackendComponentIdentity[] componentArray =
                    (components ??
                        throw new ArgumentNullException(nameof(components)))
                    .ToArray();
                if (!TryValidateBackendFields(
                        loaderKind,
                        adapterState,
                        receiptFlags,
                        unityMainThreadId,
                        teardownDisposition,
                        componentArray,
                        out error))
                {
                    return false;
                }
                byte[] api = NativePolicyValue.CloneDigest(
                    harmonyApiBindingDigest,
                    nameof(harmonyApiBindingDigest));
                byte[] wrappers = NativePolicyValue.CloneDigest(
                    wrapperTargetReceiptSetDigest,
                    nameof(wrapperTargetReceiptSetDigest));
                byte[] detours = NativePolicyValue.CloneDigest(
                    nativeDetourReceiptSetDigest,
                    nameof(nativeDetourReceiptSetDigest));
                byte[] probe = NativePolicyValue.CloneDigest(
                    backendProbeDigest,
                    nameof(backendProbeDigest));
                byte[] canary = NativePolicyValue.CloneDigest(
                    criticalCanaryDigest,
                    nameof(criticalCanaryDigest));
                byte[] constructionApi = NativePolicyValue.CloneDigest(
                    constructionApiBindingReceiptDigest,
                    nameof(constructionApiBindingReceiptDigest));
                byte[] constructionPhysical = NativePolicyValue.CloneDigest(
                    constructionRuntimePhysicalReceiptDigest,
                    nameof(constructionRuntimePhysicalReceiptDigest));
                byte[] digest = ComputeBackendReceiptDigest(
                    acceptedPolicy.TargetPolicyDigest,
                    acceptedConstruction.ConstructionPolicyDigest,
                    loaderKind,
                    nonce,
                    adapterNonce,
                    probeEvidence.PhaseSerial,
                    probeEvidence.EvidenceDigest,
                    adapterState,
                    receiptFlags,
                    unityMainThreadId,
                    teardownDisposition,
                    componentArray,
                    api,
                    wrappers,
                    detours,
                    probe,
                    canary,
                    constructionApi,
                    constructionPhysical);
                receipt = new NativeBackendReceipt(
                    BackendReceiptSchema,
                    loaderKind,
                    acceptedPolicy.TargetPolicyDigest,
                    acceptedConstruction.ConstructionPolicyDigest,
                    nonce,
                    adapterNonce,
                    probeEvidence.PhaseSerial,
                    probeEvidence.EvidenceDigest,
                    adapterState,
                    receiptFlags,
                    unityMainThreadId,
                    teardownDisposition,
                    componentArray,
                    api,
                    wrappers,
                    detours,
                    probe,
                    canary,
                    constructionApi,
                    constructionPhysical,
                    digest);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException ||
                ex is InvalidDataException || ex is OverflowException)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static bool TryValidateBackendReceipt(
            NativeBackendReceipt receipt,
            NativeTargetPolicyManifest acceptedPolicy,
            NativeConstructionApiManifest acceptedConstruction,
            out string error)
        {
            error = string.Empty;
            try
            {
                if (receipt == null || receipt.Schema != BackendReceiptSchema ||
                    !NativePatchCapabilityPolicy.AreNeutralManifestsComplete(
                        acceptedPolicy,
                        acceptedConstruction,
                        out error) ||
                    !NativePolicyValue.FixedEquals(
                        receipt.TargetPolicyDigest,
                        acceptedPolicy.TargetPolicyDigest) ||
                    !NativePolicyValue.FixedEquals(
                        receipt.ConstructionPolicyDigest,
                        acceptedConstruction.ConstructionPolicyDigest) ||
                    receipt.AdapterState !=
                        NativePatchAdapterState.BackendProbe ||
                    receipt.TeardownDisposition !=
                        NativeBackendTeardownDisposition.NotRequested ||
                    !NativeBackendProbeEvidenceCodec.TryValidateFields(
                        receipt.ProcessNonce,
                        receipt.AdapterInstanceNonce,
                        receipt.BackendProbePhaseSerial,
                        receipt.BackendProbeEvidenceDigest) ||
                    !TryValidateBackendFields(
                        receipt.LoaderKind,
                        receipt.AdapterState,
                        receipt.ReceiptFlags,
                        receipt.UnityMainThreadId,
                        receipt.TeardownDisposition,
                        receipt.Components,
                        out error))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "native backend receipt is noncanonical";
                    return false;
                }
                byte[] expected = ComputeBackendReceiptDigest(
                    receipt.TargetPolicyDigest,
                    receipt.ConstructionPolicyDigest,
                    receipt.LoaderKind,
                    receipt.ProcessNonce,
                    receipt.AdapterInstanceNonce,
                    receipt.BackendProbePhaseSerial,
                    receipt.BackendProbeEvidenceDigest,
                    receipt.AdapterState,
                    receipt.ReceiptFlags,
                    receipt.UnityMainThreadId,
                    receipt.TeardownDisposition,
                    receipt.Components,
                    receipt.HarmonyApiBindingDigest,
                    receipt.WrapperTargetReceiptSetDigest,
                    receipt.NativeDetourReceiptSetDigest,
                    receipt.BackendProbeDigest,
                    receipt.CriticalCanaryDigest,
                    receipt.ConstructionApiBindingReceiptDigest,
                    receipt.ConstructionRuntimePhysicalReceiptDigest);
                if (!NativePolicyValue.FixedEquals(
                        expected,
                        receipt.BackendReceiptDigest))
                {
                    error = "native backend receipt digest mismatch";
                    return false;
                }
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException ||
                ex is InvalidDataException || ex is OverflowException)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryValidateBackendFields(
            NativePatchLoaderKind loaderKind,
            NativePatchAdapterState adapterState,
            NativeBackendReceiptFlags receiptFlags,
            int unityMainThreadId,
            NativeBackendTeardownDisposition teardownDisposition,
            IReadOnlyList<NativeBackendComponentIdentity> components,
            out string error)
        {
            error = string.Empty;
            NativeBackendReceiptFlags required = CommonBackendReceiptFlags |
                (loaderKind == NativePatchLoaderKind.MelonLoader
                    ? NativeBackendReceiptFlags.ManagedWrapperDetourExpected
                    : 0);
            if (!Enum.IsDefined(typeof(NativePatchLoaderKind), loaderKind) ||
                receiptFlags != required || unityMainThreadId <= 0 ||
                !IsBackendReceiptState(adapterState) ||
                !IsTeardownDispositionConsistent(
                    adapterState,
                    teardownDisposition) ||
                components == null || components.Count != 6)
            {
                error = "native backend receipt scalar fields are noncanonical";
                return false;
            }
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < components.Count; index++)
            {
                NativeBackendComponentIdentity component = components[index];
                if (component == null || (byte)component.Kind != index + 1 ||
                    !names.Add(component.Name))
                {
                    error =
                        "native backend components are missing, duplicated, or unordered";
                    return false;
                }
            }
            return true;
        }

        private static bool IsBackendReceiptState(NativePatchAdapterState state)
        {
            return state == NativePatchAdapterState.BackendProbe ||
                state == NativePatchAdapterState.PreDeinitRequested ||
                state == NativePatchAdapterState.DrainingRootsAndHandlers ||
                state == NativePatchAdapterState.DetachingCallbacks ||
                state == NativePatchAdapterState
                    .CallbackDetachedBackendPassThroughResident ||
                state == NativePatchAdapterState.RestartRequiredResidentFault ||
                state == NativePatchAdapterState
                    .RestartRequiredResidentQuarantine;
        }

        private static bool IsTeardownDispositionConsistent(
            NativePatchAdapterState state,
            NativeBackendTeardownDisposition disposition)
        {
            if (!Enum.IsDefined(
                    typeof(NativeBackendTeardownDisposition),
                    disposition))
            {
                return false;
            }
            if (state == NativePatchAdapterState.BackendProbe)
            {
                return disposition == NativeBackendTeardownDisposition.NotRequested;
            }
            if (state == NativePatchAdapterState.PreDeinitRequested ||
                state == NativePatchAdapterState.DrainingRootsAndHandlers ||
                state == NativePatchAdapterState.DetachingCallbacks)
            {
                return disposition == NativeBackendTeardownDisposition.DrainPending;
            }
            if (state == NativePatchAdapterState
                .CallbackDetachedBackendPassThroughResident)
            {
                return disposition == NativeBackendTeardownDisposition
                    .CallbackDetachedBackendPassThroughResident;
            }
            return disposition ==
                NativeBackendTeardownDisposition.QuarantinedRestartRequired;
        }

        private static byte[] ComputeBackendReceiptDigest(
            byte[] targetPolicyDigest,
            byte[] constructionPolicyDigest,
            NativePatchLoaderKind loaderKind,
            byte[] processNonce,
            byte[] adapterInstanceNonce,
            ulong backendProbePhaseSerial,
            byte[] backendProbeEvidenceDigest,
            NativePatchAdapterState adapterState,
            NativeBackendReceiptFlags receiptFlags,
            int unityMainThreadId,
            NativeBackendTeardownDisposition teardownDisposition,
            IReadOnlyList<NativeBackendComponentIdentity> components,
            byte[] harmonyApiBindingDigest,
            byte[] wrapperTargetReceiptSetDigest,
            byte[] nativeDetourReceiptSetDigest,
            byte[] backendProbeDigest,
            byte[] criticalCanaryDigest,
            byte[] constructionApiBindingReceiptDigest,
            byte[] constructionRuntimePhysicalReceiptDigest)
        {
            using (var writer = new NativePolicyCanonicalWriter(
                MaximumBackendReceiptPreimageBytes))
            {
                writer.WriteDomain("operator-native-backend-receipt-v3\0");
                writer.WriteUInt16(BackendReceiptSchema);
                writer.WriteBytes(NativePolicyValue.CloneDigest(
                    targetPolicyDigest,
                    nameof(targetPolicyDigest)));
                writer.WriteBytes(NativePolicyValue.CloneDigest(
                    constructionPolicyDigest,
                    nameof(constructionPolicyDigest)));
                writer.WriteBytes(NativePolicyValue.CloneFixed(
                    processNonce,
                    NativePolicyValue.ProcessNonceBytes,
                    nameof(processNonce),
                    requireNonzero: true));
                writer.WriteBytes(NativePolicyValue.CloneFixed(
                    adapterInstanceNonce,
                    NativePolicyValue.ProcessNonceBytes,
                    nameof(adapterInstanceNonce),
                    requireNonzero: true));
                if (backendProbePhaseSerial == 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(backendProbePhaseSerial));
                }
                writer.WriteUInt64(backendProbePhaseSerial);
                writer.WriteBytes(NativePolicyValue.CloneDigest(
                    backendProbeEvidenceDigest,
                    nameof(backendProbeEvidenceDigest)));
                writer.WriteByte((byte)loaderKind);
                writer.WriteByte((byte)adapterState);
                writer.WriteUInt32((uint)receiptFlags);
                writer.WriteInt32(unityMainThreadId);
                writer.WriteByte((byte)teardownDisposition);
                writer.WriteByte(checked((byte)components.Count));
                foreach (NativeBackendComponentIdentity component in components)
                {
                    writer.WriteByte((byte)component.Kind);
                    writer.WriteString(component.Name);
                    writer.WriteString(component.Version);
                    writer.WriteBytes(component.AssemblyMvid);
                    writer.WriteBytes(component.FileSha256);
                }
                writer.WriteBytes(NativePolicyValue.CloneDigest(
                    harmonyApiBindingDigest,
                    nameof(harmonyApiBindingDigest)));
                writer.WriteBytes(NativePolicyValue.CloneDigest(
                    wrapperTargetReceiptSetDigest,
                    nameof(wrapperTargetReceiptSetDigest)));
                writer.WriteBytes(NativePolicyValue.CloneDigest(
                    nativeDetourReceiptSetDigest,
                    nameof(nativeDetourReceiptSetDigest)));
                writer.WriteBytes(NativePolicyValue.CloneDigest(
                    backendProbeDigest,
                    nameof(backendProbeDigest)));
                writer.WriteBytes(NativePolicyValue.CloneDigest(
                    criticalCanaryDigest,
                    nameof(criticalCanaryDigest)));
                writer.WriteBytes(NativePolicyValue.CloneDigest(
                    constructionApiBindingReceiptDigest,
                    nameof(constructionApiBindingReceiptDigest)));
                writer.WriteBytes(NativePolicyValue.CloneDigest(
                    constructionRuntimePhysicalReceiptDigest,
                    nameof(constructionRuntimePhysicalReceiptDigest)));
                return Sha256(writer.Complete());
            }
        }

        private static byte[] Sha256(byte[] value)
        {
            using (SHA256 algorithm = SHA256.Create())
                return algorithm.ComputeHash(value);
        }
    }
}
