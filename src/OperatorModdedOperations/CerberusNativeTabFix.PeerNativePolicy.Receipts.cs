using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OperatorModdedOperations.NativePatching
{
    internal sealed class NativeTargetPolicyManifest
    {
        private readonly byte[] gameContentIdentityDigest;
        private readonly byte[] gameAssemblySha256;
        private readonly byte[] globalMetadataSha256;
        private readonly byte[] targetPolicyDigest;
        private readonly ReadOnlyCollection<NativeSemanticTargetDescriptor> targets;
        private readonly ReadOnlyCollection<NativeRootOwnershipPolicy> rootPolicies;
        private readonly ReadOnlyCollection<NativeModeLifecyclePolicy> modePolicies;
        private readonly ReadOnlyCollection<NativeSemanticPolicyRule> semanticRules;

        internal NativeTargetPolicyManifest(
            ushort schema,
            ushort policyRevision,
            byte[] gameContentIdentityDigest,
            byte[] gameAssemblySha256,
            byte[] globalMetadataSha256,
            string patchSetId,
            string operationalOwnerId,
            int callbackPriority,
            ushort lifecyclePolicyRevision,
            ushort handlerOnlyContractRevision,
            NativeTargetPolicyFlags flags,
            IList<NativeSemanticTargetDescriptor> targets,
            IList<NativeRootOwnershipPolicy> rootPolicies,
            IList<NativeModeLifecyclePolicy> modePolicies,
            IList<NativeSemanticPolicyRule> semanticRules,
            byte[] targetPolicyDigest)
        {
            Schema = schema;
            PolicyRevision = policyRevision;
            this.gameContentIdentityDigest = NativePolicyValue.CloneDigest(
                gameContentIdentityDigest,
                nameof(gameContentIdentityDigest));
            this.gameAssemblySha256 = NativePolicyValue.CloneDigest(
                gameAssemblySha256,
                nameof(gameAssemblySha256));
            this.globalMetadataSha256 = NativePolicyValue.CloneDigest(
                globalMetadataSha256,
                nameof(globalMetadataSha256));
            PatchSetId = NativePolicyValue.RequireString(
                patchSetId,
                nameof(patchSetId));
            OperationalOwnerId = NativePolicyValue.RequireString(
                operationalOwnerId,
                nameof(operationalOwnerId));
            CallbackPriority = callbackPriority;
            LifecyclePolicyRevision = lifecyclePolicyRevision;
            HandlerOnlyContractRevision = handlerOnlyContractRevision;
            Flags = flags;
            this.targets = new ReadOnlyCollection<NativeSemanticTargetDescriptor>(
                new List<NativeSemanticTargetDescriptor>(targets ??
                    throw new ArgumentNullException(nameof(targets))));
            this.rootPolicies = new ReadOnlyCollection<NativeRootOwnershipPolicy>(
                new List<NativeRootOwnershipPolicy>(rootPolicies ??
                    throw new ArgumentNullException(nameof(rootPolicies))));
            this.modePolicies = new ReadOnlyCollection<NativeModeLifecyclePolicy>(
                new List<NativeModeLifecyclePolicy>(modePolicies ??
                    throw new ArgumentNullException(nameof(modePolicies))));
            this.semanticRules = new ReadOnlyCollection<NativeSemanticPolicyRule>(
                new List<NativeSemanticPolicyRule>(semanticRules ??
                    throw new ArgumentNullException(nameof(semanticRules))));
            this.targetPolicyDigest = NativePolicyValue.CloneDigest(
                targetPolicyDigest,
                nameof(targetPolicyDigest));
        }

        internal ushort Schema { get; }
        internal ushort PolicyRevision { get; }
        internal byte[] GameContentIdentityDigest =>
            (byte[])gameContentIdentityDigest.Clone();
        internal byte[] GameAssemblySha256 =>
            (byte[])gameAssemblySha256.Clone();
        internal byte[] GlobalMetadataSha256 =>
            (byte[])globalMetadataSha256.Clone();
        internal string PatchSetId { get; }
        internal string OperationalOwnerId { get; }
        internal int CallbackPriority { get; }
        internal ushort LifecyclePolicyRevision { get; }
        internal ushort HandlerOnlyContractRevision { get; }
        internal NativeTargetPolicyFlags Flags { get; }
        internal ReadOnlyCollection<NativeSemanticTargetDescriptor> Targets =>
            targets;
        internal ReadOnlyCollection<NativeRootOwnershipPolicy> RootPolicies =>
            rootPolicies;
        internal ReadOnlyCollection<NativeModeLifecyclePolicy> ModePolicies =>
            modePolicies;
        internal ReadOnlyCollection<NativeSemanticPolicyRule> SemanticRules =>
            semanticRules;
        internal byte[] TargetPolicyDigest =>
            (byte[])targetPolicyDigest.Clone();
        internal bool HasCompletePinnedSemanticInventory => targets.Count == 32;
        internal bool ConstructionApiManifestComplete => false;
        internal bool RequiresInactiveScenePlacementImplementation => true;
        internal bool RuntimeCapabilityComplete =>
            HasCompletePinnedSemanticInventory &&
            ConstructionApiManifestComplete;
    }

    internal sealed class NativeBackendComponentIdentity
    {
        private readonly byte[] assemblyMvid;
        private readonly byte[] fileSha256;

        internal NativeBackendComponentIdentity(
            NativeBackendComponentKind kind,
            string name,
            string version,
            byte[] assemblyMvid,
            byte[] fileSha256)
        {
            Kind = kind;
            Name = NativePolicyValue.RequireString(name, nameof(name));
            Version = NativePolicyValue.RequireString(version, nameof(version));
            this.assemblyMvid = NativePolicyValue.CloneFixed(
                assemblyMvid,
                16,
                nameof(assemblyMvid),
                requireNonzero: true);
            this.fileSha256 = NativePolicyValue.CloneDigest(
                fileSha256,
                nameof(fileSha256));
            if (!Enum.IsDefined(typeof(NativeBackendComponentKind), kind))
            {
                throw new ArgumentException(
                    "native backend component kind is invalid");
            }
        }

        internal NativeBackendComponentKind Kind { get; }
        internal string Name { get; }
        internal string Version { get; }
        internal byte[] AssemblyMvid => (byte[])assemblyMvid.Clone();
        internal byte[] FileSha256 => (byte[])fileSha256.Clone();

        internal bool SemanticEquals(NativeBackendComponentIdentity other)
        {
            return other != null && Kind == other.Kind &&
                string.Equals(Name, other.Name, StringComparison.Ordinal) &&
                string.Equals(Version, other.Version, StringComparison.Ordinal) &&
                NativePolicyValue.FixedEquals(assemblyMvid, other.assemblyMvid) &&
                NativePolicyValue.FixedEquals(fileSha256, other.fileSha256);
        }
    }

    internal sealed class NativeBackendReceipt
    {
        private readonly byte[] targetPolicyDigest;
        private readonly byte[] constructionPolicyDigest;
        private readonly byte[] processNonce;
        private readonly byte[] adapterInstanceNonce;
        private readonly byte[] backendProbeEvidenceDigest;
        private readonly byte[] harmonyApiBindingDigest;
        private readonly byte[] wrapperTargetReceiptSetDigest;
        private readonly byte[] nativeDetourReceiptSetDigest;
        private readonly byte[] backendProbeDigest;
        private readonly byte[] criticalCanaryDigest;
        private readonly byte[] constructionApiBindingReceiptDigest;
        private readonly byte[] constructionRuntimePhysicalReceiptDigest;
        private readonly byte[] backendReceiptDigest;
        private readonly ReadOnlyCollection<NativeBackendComponentIdentity> components;

        internal NativeBackendReceipt(
            ushort schema,
            NativePatchLoaderKind loaderKind,
            byte[] targetPolicyDigest,
            byte[] constructionPolicyDigest,
            byte[] processNonce,
            byte[] adapterInstanceNonce,
            ulong backendProbePhaseSerial,
            byte[] backendProbeEvidenceDigest,
            NativePatchAdapterState adapterState,
            NativeBackendReceiptFlags receiptFlags,
            int unityMainThreadId,
            NativeBackendTeardownDisposition teardownDisposition,
            IList<NativeBackendComponentIdentity> components,
            byte[] harmonyApiBindingDigest,
            byte[] wrapperTargetReceiptSetDigest,
            byte[] nativeDetourReceiptSetDigest,
            byte[] backendProbeDigest,
            byte[] criticalCanaryDigest,
            byte[] constructionApiBindingReceiptDigest,
            byte[] constructionRuntimePhysicalReceiptDigest,
            byte[] backendReceiptDigest)
        {
            Schema = schema;
            LoaderKind = loaderKind;
            this.targetPolicyDigest = NativePolicyValue.CloneDigest(
                targetPolicyDigest,
                nameof(targetPolicyDigest));
            this.constructionPolicyDigest = NativePolicyValue.CloneDigest(
                constructionPolicyDigest,
                nameof(constructionPolicyDigest));
            this.processNonce = NativePolicyValue.CloneFixed(
                processNonce,
                NativePolicyValue.ProcessNonceBytes,
                nameof(processNonce),
                requireNonzero: true);
            this.adapterInstanceNonce = NativePolicyValue.CloneFixed(
                adapterInstanceNonce,
                NativePolicyValue.ProcessNonceBytes,
                nameof(adapterInstanceNonce),
                requireNonzero: true);
            if (backendProbePhaseSerial == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(backendProbePhaseSerial));
            }
            BackendProbePhaseSerial = backendProbePhaseSerial;
            this.backendProbeEvidenceDigest = NativePolicyValue.CloneDigest(
                backendProbeEvidenceDigest,
                nameof(backendProbeEvidenceDigest));
            AdapterState = adapterState;
            ReceiptFlags = receiptFlags;
            UnityMainThreadId = unityMainThreadId;
            TeardownDisposition = teardownDisposition;
            this.components = new ReadOnlyCollection<NativeBackendComponentIdentity>(
                new List<NativeBackendComponentIdentity>(components ??
                    throw new ArgumentNullException(nameof(components))));
            this.harmonyApiBindingDigest = NativePolicyValue.CloneDigest(
                harmonyApiBindingDigest,
                nameof(harmonyApiBindingDigest));
            this.wrapperTargetReceiptSetDigest = NativePolicyValue.CloneDigest(
                wrapperTargetReceiptSetDigest,
                nameof(wrapperTargetReceiptSetDigest));
            this.nativeDetourReceiptSetDigest = NativePolicyValue.CloneDigest(
                nativeDetourReceiptSetDigest,
                nameof(nativeDetourReceiptSetDigest));
            this.backendProbeDigest = NativePolicyValue.CloneDigest(
                backendProbeDigest,
                nameof(backendProbeDigest));
            this.criticalCanaryDigest = NativePolicyValue.CloneDigest(
                criticalCanaryDigest,
                nameof(criticalCanaryDigest));
            this.constructionApiBindingReceiptDigest =
                NativePolicyValue.CloneDigest(
                    constructionApiBindingReceiptDigest,
                    nameof(constructionApiBindingReceiptDigest));
            this.constructionRuntimePhysicalReceiptDigest =
                NativePolicyValue.CloneDigest(
                    constructionRuntimePhysicalReceiptDigest,
                    nameof(constructionRuntimePhysicalReceiptDigest));
            this.backendReceiptDigest = NativePolicyValue.CloneDigest(
                backendReceiptDigest,
                nameof(backendReceiptDigest));
        }

        internal ushort Schema { get; }
        internal NativePatchLoaderKind LoaderKind { get; }
        internal byte[] TargetPolicyDigest =>
            (byte[])targetPolicyDigest.Clone();
        internal byte[] ConstructionPolicyDigest =>
            (byte[])constructionPolicyDigest.Clone();
        internal byte[] ProcessNonce => (byte[])processNonce.Clone();
        internal byte[] AdapterInstanceNonce =>
            (byte[])adapterInstanceNonce.Clone();
        internal ulong BackendProbePhaseSerial { get; }
        internal byte[] BackendProbeEvidenceDigest =>
            (byte[])backendProbeEvidenceDigest.Clone();
        internal NativePatchAdapterState AdapterState { get; }
        internal NativeBackendReceiptFlags ReceiptFlags { get; }
        internal int UnityMainThreadId { get; }
        internal NativeBackendTeardownDisposition TeardownDisposition { get; }
        internal ReadOnlyCollection<NativeBackendComponentIdentity> Components =>
            components;
        internal byte[] HarmonyApiBindingDigest =>
            (byte[])harmonyApiBindingDigest.Clone();
        internal byte[] WrapperTargetReceiptSetDigest =>
            (byte[])wrapperTargetReceiptSetDigest.Clone();
        internal byte[] NativeDetourReceiptSetDigest =>
            (byte[])nativeDetourReceiptSetDigest.Clone();
        internal byte[] BackendProbeDigest =>
            (byte[])backendProbeDigest.Clone();
        internal byte[] CriticalCanaryDigest =>
            (byte[])criticalCanaryDigest.Clone();
        internal byte[] ConstructionApiBindingReceiptDigest =>
            (byte[])constructionApiBindingReceiptDigest.Clone();
        internal byte[] ConstructionRuntimePhysicalReceiptDigest =>
            (byte[])constructionRuntimePhysicalReceiptDigest.Clone();
        internal byte[] BackendReceiptDigest =>
            (byte[])backendReceiptDigest.Clone();
    }
}
