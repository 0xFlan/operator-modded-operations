using System;
using System.IO;
using System.Security.Cryptography;

namespace OperatorModdedOperations.NativePatching
{
    internal sealed class NativeConstructionPhysicalReceipt
    {
        private readonly byte[] gameContentIdentityDigest;
        private readonly byte[] targetPolicyDigest;
        private readonly byte[] constructionPolicyDigest;
        private readonly byte[] backendReceiptDigest;
        private readonly byte[] processNonce;
        private readonly byte[] constructionApiBindingReceiptDigest;
        private readonly byte[] runtimePhysicalObservationDigest;
        private readonly byte[] receiptDigest;

        internal NativeConstructionPhysicalReceipt(
            ushort schema,
            NativePatchLoaderKind loaderKind,
            byte[] gameContentIdentityDigest,
            byte[] targetPolicyDigest,
            byte[] constructionPolicyDigest,
            byte[] backendReceiptDigest,
            byte[] processNonce,
            NativeConstructionPhysicalReceiptFlags flags,
            byte[] constructionApiBindingReceiptDigest,
            byte[] runtimePhysicalObservationDigest,
            byte[] receiptDigest)
        {
            Schema = schema;
            LoaderKind = loaderKind;
            this.gameContentIdentityDigest = NativePolicyValue.CloneDigest(
                gameContentIdentityDigest,
                nameof(gameContentIdentityDigest));
            this.targetPolicyDigest = NativePolicyValue.CloneDigest(
                targetPolicyDigest,
                nameof(targetPolicyDigest));
            this.constructionPolicyDigest = NativePolicyValue.CloneDigest(
                constructionPolicyDigest,
                nameof(constructionPolicyDigest));
            this.backendReceiptDigest = NativePolicyValue.CloneDigest(
                backendReceiptDigest,
                nameof(backendReceiptDigest));
            this.processNonce = NativePolicyValue.CloneFixed(
                processNonce,
                NativePolicyValue.ProcessNonceBytes,
                nameof(processNonce),
                requireNonzero: true);
            Flags = flags;
            this.constructionApiBindingReceiptDigest =
                NativePolicyValue.CloneDigest(
                    constructionApiBindingReceiptDigest,
                    nameof(constructionApiBindingReceiptDigest));
            this.runtimePhysicalObservationDigest =
                NativePolicyValue.CloneDigest(
                    runtimePhysicalObservationDigest,
                    nameof(runtimePhysicalObservationDigest));
            this.receiptDigest = NativePolicyValue.CloneDigest(
                receiptDigest,
                nameof(receiptDigest));
        }

        internal ushort Schema { get; }
        internal NativePatchLoaderKind LoaderKind { get; }
        internal byte[] GameContentIdentityDigest =>
            (byte[])gameContentIdentityDigest.Clone();
        internal byte[] TargetPolicyDigest => (byte[])targetPolicyDigest.Clone();
        internal byte[] ConstructionPolicyDigest =>
            (byte[])constructionPolicyDigest.Clone();
        internal byte[] BackendReceiptDigest =>
            (byte[])backendReceiptDigest.Clone();
        internal byte[] ProcessNonce => (byte[])processNonce.Clone();
        internal NativeConstructionPhysicalReceiptFlags Flags { get; }
        internal byte[] ConstructionApiBindingReceiptDigest =>
            (byte[])constructionApiBindingReceiptDigest.Clone();
        internal byte[] RuntimePhysicalObservationDigest =>
            (byte[])runtimePhysicalObservationDigest.Clone();
        internal byte[] ReceiptDigest => (byte[])receiptDigest.Clone();
    }

    internal static class NativeConstructionPhysicalReceiptCodec
    {
        internal const ushort Schema = 1;
        internal const int MaximumPreimageBytes = 1024;
        internal const NativeConstructionPhysicalReceiptFlags ExactFlags =
            NativeConstructionPhysicalReceiptFlags
                .BareRootPersistentBeforeComponents |
            NativeConstructionPhysicalReceiptFlags
                .ExactClosedGenericNativeComponents |
            NativeConstructionPhysicalReceiptFlags
                .ProbeCloneInactiveNoEarlyLifecycle |
            NativeConstructionPhysicalReceiptFlags
                .HostRemoteExactFrozenPackageScene |
            NativeConstructionPhysicalReceiptFlags
                .ActualCloneFreshAndNonAliased |
            NativeConstructionPhysicalReceiptFlags
                .PriorProbeDisposedBeforeRuntimeClone |
            NativeConstructionPhysicalReceiptFlags.RestartUsesFreshRootSet |
            NativeConstructionPhysicalReceiptFlags
                .MoveGameObjectToSceneFallbackForbidden;

        internal static bool TryCreate(
            NativeTargetPolicyManifest targetPolicy,
            NativeConstructionApiManifest constructionPolicy,
            NativeBackendReceipt backendReceipt,
            NativeConstructionPhysicalReceiptFlags flags,
            out NativeConstructionPhysicalReceipt receipt,
            out string error)
        {
            receipt = null;
            error = string.Empty;
            try
            {
                if (!NativeTargetPolicyCodec.TryValidateBackendReceipt(
                        backendReceipt,
                        targetPolicy,
                        constructionPolicy,
                        out error) ||
                    backendReceipt.AdapterState !=
                        NativePatchAdapterState.BackendProbe ||
                    flags != ExactFlags)
                {
                    if (string.IsNullOrEmpty(error))
                        error = "native construction physical receipt is not eligible";
                    return false;
                }
                byte[] digest = ComputeDigest(
                    targetPolicy.GameContentIdentityDigest,
                    backendReceipt.TargetPolicyDigest,
                    backendReceipt.ConstructionPolicyDigest,
                    backendReceipt.BackendReceiptDigest,
                    backendReceipt.ProcessNonce,
                    backendReceipt.LoaderKind,
                    flags,
                    backendReceipt.ConstructionApiBindingReceiptDigest,
                    backendReceipt.ConstructionRuntimePhysicalReceiptDigest);
                receipt = new NativeConstructionPhysicalReceipt(
                    Schema,
                    backendReceipt.LoaderKind,
                    targetPolicy.GameContentIdentityDigest,
                    backendReceipt.TargetPolicyDigest,
                    backendReceipt.ConstructionPolicyDigest,
                    backendReceipt.BackendReceiptDigest,
                    backendReceipt.ProcessNonce,
                    flags,
                    backendReceipt.ConstructionApiBindingReceiptDigest,
                    backendReceipt.ConstructionRuntimePhysicalReceiptDigest,
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

        internal static bool TryValidate(
            NativeConstructionPhysicalReceipt receipt,
            NativeTargetPolicyManifest targetPolicy,
            NativeConstructionApiManifest constructionPolicy,
            NativeBackendReceipt backendReceipt,
            out string error)
        {
            error = string.Empty;
            try
            {
                if (receipt == null || receipt.Schema != Schema ||
                    receipt.Flags != ExactFlags ||
                    !NativeTargetPolicyCodec.TryValidateBackendReceipt(
                        backendReceipt,
                        targetPolicy,
                        constructionPolicy,
                        out error) ||
                    backendReceipt.AdapterState !=
                        NativePatchAdapterState.BackendProbe ||
                    receipt.LoaderKind != backendReceipt.LoaderKind ||
                    !NativePolicyValue.FixedEquals(
                        receipt.GameContentIdentityDigest,
                        targetPolicy.GameContentIdentityDigest) ||
                    !NativePolicyValue.FixedEquals(
                        receipt.TargetPolicyDigest,
                        backendReceipt.TargetPolicyDigest) ||
                    !NativePolicyValue.FixedEquals(
                        receipt.ConstructionPolicyDigest,
                        backendReceipt.ConstructionPolicyDigest) ||
                    !NativePolicyValue.FixedEquals(
                        receipt.BackendReceiptDigest,
                        backendReceipt.BackendReceiptDigest) ||
                    !NativePolicyValue.FixedEquals(
                        receipt.ProcessNonce,
                        backendReceipt.ProcessNonce) ||
                    !NativePolicyValue.FixedEquals(
                        receipt.ConstructionApiBindingReceiptDigest,
                        backendReceipt.ConstructionApiBindingReceiptDigest) ||
                    !NativePolicyValue.FixedEquals(
                        receipt.RuntimePhysicalObservationDigest,
                        backendReceipt
                            .ConstructionRuntimePhysicalReceiptDigest))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "native construction physical receipt is noncanonical";
                    return false;
                }
                byte[] expected = ComputeDigest(
                    receipt.GameContentIdentityDigest,
                    receipt.TargetPolicyDigest,
                    receipt.ConstructionPolicyDigest,
                    receipt.BackendReceiptDigest,
                    receipt.ProcessNonce,
                    receipt.LoaderKind,
                    receipt.Flags,
                    receipt.ConstructionApiBindingReceiptDigest,
                    receipt.RuntimePhysicalObservationDigest);
                if (!NativePolicyValue.FixedEquals(
                        expected,
                        receipt.ReceiptDigest))
                {
                    error = "native construction physical receipt digest mismatch";
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
            byte[] gameContentIdentityDigest,
            byte[] targetPolicyDigest,
            byte[] constructionPolicyDigest,
            byte[] backendReceiptDigest,
            byte[] processNonce,
            NativePatchLoaderKind loaderKind,
            NativeConstructionPhysicalReceiptFlags flags,
            byte[] constructionApiBindingReceiptDigest,
            byte[] runtimePhysicalObservationDigest)
        {
            using var writer = new NativePolicyCanonicalWriter(
                MaximumPreimageBytes);
            writer.WriteDomain(
                "operator-native-construction-physical-receipt-v1\0");
            writer.WriteUInt16(Schema);
            writer.WriteBytes(NativePolicyValue.CloneDigest(
                gameContentIdentityDigest,
                nameof(gameContentIdentityDigest)));
            writer.WriteBytes(NativePolicyValue.CloneDigest(
                targetPolicyDigest,
                nameof(targetPolicyDigest)));
            writer.WriteBytes(NativePolicyValue.CloneDigest(
                constructionPolicyDigest,
                nameof(constructionPolicyDigest)));
            writer.WriteBytes(NativePolicyValue.CloneDigest(
                backendReceiptDigest,
                nameof(backendReceiptDigest)));
            writer.WriteBytes(NativePolicyValue.CloneFixed(
                processNonce,
                NativePolicyValue.ProcessNonceBytes,
                nameof(processNonce),
                requireNonzero: true));
            writer.WriteByte((byte)loaderKind);
            writer.WriteUInt32((uint)flags);
            writer.WriteBytes(NativePolicyValue.CloneDigest(
                constructionApiBindingReceiptDigest,
                nameof(constructionApiBindingReceiptDigest)));
            writer.WriteBytes(NativePolicyValue.CloneDigest(
                runtimePhysicalObservationDigest,
                nameof(runtimePhysicalObservationDigest)));
            using SHA256 algorithm = SHA256.Create();
            return algorithm.ComputeHash(writer.Complete());
        }
    }
}
