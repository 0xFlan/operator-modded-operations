using System;
using System.IO;
using System.Security.Cryptography;

namespace OperatorModdedOperations.NativePatching
{
    internal static class NativeBackendProbeEvidenceCodec
    {
        internal const ushort Schema = 1;
        internal const int MaximumPreimageBytes = 128;

        internal static byte[] ComputeDigest(
            byte[] processNonce,
            byte[] adapterInstanceNonce,
            ulong phaseSerial)
        {
            if (phaseSerial == 0)
                throw new ArgumentOutOfRangeException(nameof(phaseSerial));
            using var writer = new NativePolicyCanonicalWriter(
                MaximumPreimageBytes);
            writer.WriteDomain("operator-native-backend-probe-evidence-v1\0");
            writer.WriteUInt16(Schema);
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
            writer.WriteUInt64(phaseSerial);
            writer.WriteByte((byte)NativePatchAdapterState.BackendProbe);
            using SHA256 algorithm = SHA256.Create();
            return algorithm.ComputeHash(writer.Complete());
        }

        internal static bool TryValidateFields(
            byte[] processNonce,
            byte[] adapterInstanceNonce,
            ulong phaseSerial,
            byte[] digest)
        {
            try
            {
                return NativePolicyValue.FixedEquals(
                    ComputeDigest(
                        processNonce,
                        adapterInstanceNonce,
                        phaseSerial),
                    NativePolicyValue.CloneDigest(
                        digest,
                        nameof(digest)));
            }
            catch (Exception ex) when (ex is ArgumentException ||
                ex is InvalidDataException || ex is OverflowException)
            {
                return false;
            }
        }
    }

    internal static class NativeNeutralSemanticPolicyCodec
    {
        internal const ushort Schema = 1;
        internal const int MaximumPreimageBytes = 256;

        internal static bool TryComputeDigest(
            NativeTargetPolicyManifest targetPolicy,
            NativeConstructionApiManifest constructionPolicy,
            out byte[] digest,
            out string error)
        {
            digest = null;
            if (!NativePatchCapabilityPolicy.AreNeutralManifestsComplete(
                    targetPolicy,
                    constructionPolicy,
                    out error))
            {
                return false;
            }
            using var writer = new NativePolicyCanonicalWriter(
                MaximumPreimageBytes);
            writer.WriteDomain("operator-native-neutral-semantic-policy-v1\0");
            writer.WriteUInt16(Schema);
            writer.WriteBytes(targetPolicy.TargetPolicyDigest);
            writer.WriteBytes(constructionPolicy.ConstructionPolicyDigest);
            using SHA256 algorithm = SHA256.Create();
            digest = algorithm.ComputeHash(writer.Complete());
            return true;
        }
    }

    internal sealed class NativeOperationalCapabilityOwner
    {
        private readonly object gate = new object();
        private readonly byte[] adapterInstanceNonce;
        private NativeOperationalCapabilityReceipt exactReceipt;

        internal NativeOperationalCapabilityOwner(byte[] adapterInstanceNonce)
        {
            this.adapterInstanceNonce = NativePolicyValue.CloneFixed(
                adapterInstanceNonce,
                NativePolicyValue.ProcessNonceBytes,
                nameof(adapterInstanceNonce),
                requireNonzero: true);
        }

        internal byte[] AdapterInstanceNonce =>
            (byte[])adapterInstanceNonce.Clone();

        internal NativeOperationalCapabilityReceipt BindOrGetExactReceipt(
            NativeOperationalCapabilityReceipt candidate)
        {
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));
            if (!candidate.IsOwnedBy(this))
            {
                throw new ArgumentException(
                    "operational capability receipt has a foreign owner",
                    nameof(candidate));
            }
            lock (gate)
            {
                if (exactReceipt == null)
                    exactReceipt = candidate;
                return exactReceipt;
            }
        }

        internal bool OwnsExactReceipt(
            NativeOperationalCapabilityReceipt candidate)
        {
            lock (gate)
                return ReferenceEquals(exactReceipt, candidate);
        }
    }

    internal sealed class NativeOperationalCapabilityReceipt
    {
        private readonly NativeOperationalCapabilityOwner owner;
        private readonly byte[] processNonce;
        private readonly byte[] adapterInstanceNonce;
        private readonly byte[] neutralSemanticPolicyDigest;
        private readonly byte[] targetPolicyDigest;
        private readonly byte[] constructionPolicyDigest;
        private readonly byte[] backendReceiptDigest;
        private readonly byte[] physicalReceiptDigest;
        private readonly byte[] receiptDigest;

        internal NativeOperationalCapabilityReceipt(
            ushort schema,
            NativeOperationalCapabilityOwner owner,
            byte[] processNonce,
            byte[] adapterInstanceNonce,
            NativePatchLoaderKind loaderKind,
            byte[] neutralSemanticPolicyDigest,
            byte[] targetPolicyDigest,
            byte[] constructionPolicyDigest,
            byte[] backendReceiptDigest,
            byte[] physicalReceiptDigest,
            NativePatchAdapterState phase,
            byte[] receiptDigest)
        {
            Schema = schema;
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
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
            LoaderKind = loaderKind;
            this.neutralSemanticPolicyDigest = NativePolicyValue.CloneDigest(
                neutralSemanticPolicyDigest,
                nameof(neutralSemanticPolicyDigest));
            this.targetPolicyDigest = NativePolicyValue.CloneDigest(
                targetPolicyDigest,
                nameof(targetPolicyDigest));
            this.constructionPolicyDigest = NativePolicyValue.CloneDigest(
                constructionPolicyDigest,
                nameof(constructionPolicyDigest));
            this.backendReceiptDigest = NativePolicyValue.CloneDigest(
                backendReceiptDigest,
                nameof(backendReceiptDigest));
            this.physicalReceiptDigest = NativePolicyValue.CloneDigest(
                physicalReceiptDigest,
                nameof(physicalReceiptDigest));
            Phase = phase;
            this.receiptDigest = NativePolicyValue.CloneDigest(
                receiptDigest,
                nameof(receiptDigest));
        }

        internal ushort Schema { get; }
        internal byte[] ProcessNonce => (byte[])processNonce.Clone();
        internal byte[] AdapterInstanceNonce =>
            (byte[])adapterInstanceNonce.Clone();
        internal NativePatchLoaderKind LoaderKind { get; }
        internal byte[] NeutralSemanticPolicyDigest =>
            (byte[])neutralSemanticPolicyDigest.Clone();
        internal byte[] TargetPolicyDigest => (byte[])targetPolicyDigest.Clone();
        internal byte[] ConstructionPolicyDigest =>
            (byte[])constructionPolicyDigest.Clone();
        internal byte[] BackendReceiptDigest =>
            (byte[])backendReceiptDigest.Clone();
        internal byte[] PhysicalReceiptDigest =>
            (byte[])physicalReceiptDigest.Clone();
        internal NativePatchAdapterState Phase { get; }
        internal byte[] ReceiptDigest => (byte[])receiptDigest.Clone();

        internal bool IsOwnedBy(NativeOperationalCapabilityOwner candidate)
        {
            return ReferenceEquals(owner, candidate);
        }
    }

    internal static class NativeOperationalCapabilityReceiptCodec
    {
        internal const ushort Schema = 1;
        internal const int MaximumPreimageBytes = 512;

        internal static bool TryCreateEvidenceReceipt(
            NativeOperationalCapabilityOwner owner,
            NativeTargetPolicyManifest targetPolicy,
            NativeConstructionApiManifest constructionPolicy,
            NativeBackendReceipt backendReceipt,
            NativeConstructionPhysicalReceipt physicalReceipt,
            out NativeOperationalCapabilityReceipt receipt,
            out string error)
        {
            receipt = null;
            error = string.Empty;
            try
            {
                if (owner == null ||
                    !NativeConstructionPhysicalReceiptCodec.TryValidate(
                        physicalReceipt,
                        targetPolicy,
                        constructionPolicy,
                        backendReceipt,
                        out error) ||
                    !NativeNeutralSemanticPolicyCodec.TryComputeDigest(
                        targetPolicy,
                        constructionPolicy,
                        out byte[] neutralDigest,
                        out error))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "operational capability inputs are incomplete";
                    return false;
                }
                byte[] adapterNonce = owner.AdapterInstanceNonce;
                byte[] digest = ComputeDigest(
                    backendReceipt.ProcessNonce,
                    adapterNonce,
                    backendReceipt.LoaderKind,
                    neutralDigest,
                    targetPolicy.TargetPolicyDigest,
                    constructionPolicy.ConstructionPolicyDigest,
                    backendReceipt.BackendReceiptDigest,
                    physicalReceipt.ReceiptDigest,
                    NativePatchAdapterState.Operational);
                var candidate = new NativeOperationalCapabilityReceipt(
                    Schema,
                    owner,
                    backendReceipt.ProcessNonce,
                    adapterNonce,
                    backendReceipt.LoaderKind,
                    neutralDigest,
                    targetPolicy.TargetPolicyDigest,
                    constructionPolicy.ConstructionPolicyDigest,
                    backendReceipt.BackendReceiptDigest,
                    physicalReceipt.ReceiptDigest,
                    NativePatchAdapterState.Operational,
                    digest);
                receipt = owner.BindOrGetExactReceipt(candidate);
                if (!ReferenceEquals(receipt, candidate) &&
                    !TryValidateEvidenceReceipt(
                        receipt,
                        owner,
                        targetPolicy,
                        constructionPolicy,
                        backendReceipt,
                        physicalReceipt,
                        out error))
                {
                    receipt = null;
                    if (string.IsNullOrEmpty(error))
                    {
                        error =
                            "operational capability owner is already bound";
                    }
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

        internal static bool TryValidateEvidenceReceipt(
            NativeOperationalCapabilityReceipt receipt,
            NativeOperationalCapabilityOwner owner,
            NativeTargetPolicyManifest targetPolicy,
            NativeConstructionApiManifest constructionPolicy,
            NativeBackendReceipt backendReceipt,
            NativeConstructionPhysicalReceipt physicalReceipt,
            out string error)
        {
            error = string.Empty;
            try
            {
                if (receipt == null || owner == null ||
                    receipt.Schema != Schema ||
                    receipt.Phase != NativePatchAdapterState.Operational ||
                    !receipt.IsOwnedBy(owner) ||
                    !owner.OwnsExactReceipt(receipt) ||
                    !NativeConstructionPhysicalReceiptCodec.TryValidate(
                        physicalReceipt,
                        targetPolicy,
                        constructionPolicy,
                        backendReceipt,
                        out error) ||
                    !NativeNeutralSemanticPolicyCodec.TryComputeDigest(
                        targetPolicy,
                        constructionPolicy,
                        out byte[] neutralDigest,
                        out error) ||
                    receipt.LoaderKind != backendReceipt.LoaderKind ||
                    !NativePolicyValue.FixedEquals(
                        receipt.ProcessNonce,
                        backendReceipt.ProcessNonce) ||
                    !NativePolicyValue.FixedEquals(
                        receipt.AdapterInstanceNonce,
                        owner.AdapterInstanceNonce) ||
                    !NativePolicyValue.FixedEquals(
                        receipt.NeutralSemanticPolicyDigest,
                        neutralDigest) ||
                    !NativePolicyValue.FixedEquals(
                        receipt.TargetPolicyDigest,
                        targetPolicy.TargetPolicyDigest) ||
                    !NativePolicyValue.FixedEquals(
                        receipt.ConstructionPolicyDigest,
                        constructionPolicy.ConstructionPolicyDigest) ||
                    !NativePolicyValue.FixedEquals(
                        receipt.BackendReceiptDigest,
                        backendReceipt.BackendReceiptDigest) ||
                    !NativePolicyValue.FixedEquals(
                        receipt.PhysicalReceiptDigest,
                        physicalReceipt.ReceiptDigest))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "operational capability receipt is noncanonical";
                    return false;
                }
                byte[] expected = ComputeDigest(
                    receipt.ProcessNonce,
                    receipt.AdapterInstanceNonce,
                    receipt.LoaderKind,
                    receipt.NeutralSemanticPolicyDigest,
                    receipt.TargetPolicyDigest,
                    receipt.ConstructionPolicyDigest,
                    receipt.BackendReceiptDigest,
                    receipt.PhysicalReceiptDigest,
                    receipt.Phase);
                if (!NativePolicyValue.FixedEquals(
                        expected,
                        receipt.ReceiptDigest))
                {
                    error = "operational capability receipt digest mismatch";
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

        private static byte[] ComputeDigest(
            byte[] processNonce,
            byte[] adapterInstanceNonce,
            NativePatchLoaderKind loaderKind,
            byte[] neutralSemanticPolicyDigest,
            byte[] targetPolicyDigest,
            byte[] constructionPolicyDigest,
            byte[] backendReceiptDigest,
            byte[] physicalReceiptDigest,
            NativePatchAdapterState phase)
        {
            using var writer = new NativePolicyCanonicalWriter(
                MaximumPreimageBytes);
            writer.WriteDomain(
                "operator-native-operational-capability-receipt-v1\0");
            writer.WriteUInt16(Schema);
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
            writer.WriteByte((byte)loaderKind);
            writer.WriteBytes(NativePolicyValue.CloneDigest(
                neutralSemanticPolicyDigest,
                nameof(neutralSemanticPolicyDigest)));
            writer.WriteBytes(NativePolicyValue.CloneDigest(
                targetPolicyDigest,
                nameof(targetPolicyDigest)));
            writer.WriteBytes(NativePolicyValue.CloneDigest(
                constructionPolicyDigest,
                nameof(constructionPolicyDigest)));
            writer.WriteBytes(NativePolicyValue.CloneDigest(
                backendReceiptDigest,
                nameof(backendReceiptDigest)));
            writer.WriteBytes(NativePolicyValue.CloneDigest(
                physicalReceiptDigest,
                nameof(physicalReceiptDigest)));
            writer.WriteByte((byte)phase);
            using SHA256 algorithm = SHA256.Create();
            return algorithm.ComputeHash(writer.Complete());
        }
    }
}
