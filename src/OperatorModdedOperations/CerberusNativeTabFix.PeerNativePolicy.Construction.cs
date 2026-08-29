using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace OperatorModdedOperations.NativePatching
{
    internal sealed class NativeConstructionApiDescriptor
    {
        private readonly byte[] first32NativeSha256;

        internal NativeConstructionApiDescriptor(
            string apiId,
            NativeConstructionApiKind kind,
            string canonicalSignature,
            uint originalMetadataToken,
            uint nativeRva,
            ushort originalFlags,
            ushort originalSlot,
            byte parameterCount,
            byte[] first32NativeSha256,
            bool requiresLoaderClosedGenericReceipt)
        {
            ApiId = NativePolicyValue.RequireString(apiId, nameof(apiId));
            Kind = kind;
            CanonicalSignature = NativePolicyValue.RequireString(
                canonicalSignature,
                nameof(canonicalSignature));
            OriginalMetadataToken = originalMetadataToken;
            NativeRva = nativeRva;
            OriginalFlags = originalFlags;
            OriginalSlot = originalSlot;
            ParameterCount = parameterCount;
            this.first32NativeSha256 = NativePolicyValue.CloneDigest(
                first32NativeSha256,
                nameof(first32NativeSha256));
            RequiresLoaderClosedGenericReceipt =
                requiresLoaderClosedGenericReceipt;
            if (!Enum.IsDefined(typeof(NativeConstructionApiKind), kind) ||
                originalMetadataToken == 0 || nativeRva == 0 ||
                originalFlags == 0 || parameterCount > 4 ||
                (kind == NativeConstructionApiKind
                    .ExactNativeGenericAddComponent) !=
                    requiresLoaderClosedGenericReceipt)
            {
                throw new ArgumentException(
                    "native construction API descriptor is noncanonical");
            }
        }

        internal string ApiId { get; }
        internal NativeConstructionApiKind Kind { get; }
        internal string CanonicalSignature { get; }
        internal uint OriginalMetadataToken { get; }
        internal uint NativeRva { get; }
        internal ushort OriginalFlags { get; }
        internal ushort OriginalSlot { get; }
        internal byte ParameterCount { get; }
        internal byte[] First32NativeSha256 =>
            (byte[])first32NativeSha256.Clone();
        internal bool RequiresLoaderClosedGenericReceipt { get; }

        internal bool SemanticEquals(NativeConstructionApiDescriptor other)
        {
            return other != null &&
                string.Equals(ApiId, other.ApiId, StringComparison.Ordinal) &&
                Kind == other.Kind &&
                string.Equals(CanonicalSignature, other.CanonicalSignature,
                    StringComparison.Ordinal) &&
                OriginalMetadataToken == other.OriginalMetadataToken &&
                NativeRva == other.NativeRva &&
                OriginalFlags == other.OriginalFlags &&
                OriginalSlot == other.OriginalSlot &&
                ParameterCount == other.ParameterCount &&
                NativePolicyValue.FixedEquals(first32NativeSha256,
                    other.first32NativeSha256) &&
                RequiresLoaderClosedGenericReceipt ==
                    other.RequiresLoaderClosedGenericReceipt;
        }
    }

    internal sealed class NativeConstructionApiManifest
    {
        private readonly byte[] gameContentIdentityDigest;
        private readonly byte[] constructionPolicyDigest;
        private readonly ReadOnlyCollection<NativeConstructionApiDescriptor> apis;

        internal NativeConstructionApiManifest(
            ushort schema,
            ushort revision,
            byte[] gameContentIdentityDigest,
            IList<NativeConstructionApiDescriptor> apis,
            byte[] constructionPolicyDigest)
        {
            Schema = schema;
            Revision = revision;
            this.gameContentIdentityDigest = NativePolicyValue.CloneDigest(
                gameContentIdentityDigest,
                nameof(gameContentIdentityDigest));
            NativeConstructionApiDescriptor[] copied = (apis ??
                throw new ArgumentNullException(nameof(apis))).ToArray();
            if (copied.Length != NativeConstructionApiCodec.ExpectedApiCount ||
                copied.Any(value => value == null))
            {
                throw new ArgumentException(
                    "native construction API rows are incomplete");
            }
            for (int index = 1; index < copied.Length; index++)
            {
                if (string.CompareOrdinal(
                        copied[index - 1].ApiId,
                        copied[index].ApiId) >= 0)
                {
                    throw new ArgumentException(
                        "native construction API rows are duplicated or unordered");
                }
            }
            this.apis = Array.AsReadOnly(copied);
            this.constructionPolicyDigest = NativePolicyValue.CloneDigest(
                constructionPolicyDigest,
                nameof(constructionPolicyDigest));
        }

        internal ushort Schema { get; }
        internal ushort Revision { get; }
        internal byte[] GameContentIdentityDigest =>
            (byte[])gameContentIdentityDigest.Clone();
        internal ReadOnlyCollection<NativeConstructionApiDescriptor> Apis => apis;
        internal byte[] ConstructionPolicyDigest =>
            (byte[])constructionPolicyDigest.Clone();
        internal bool HasCompletePinnedSemanticInventory => apis.Count == 4;
        internal bool LoaderLocalRuntimeReceiptComplete => false;
        internal bool PhysicalBehaviorReceiptComplete => false;
        internal bool RuntimeCapabilityComplete =>
            HasCompletePinnedSemanticInventory &&
            LoaderLocalRuntimeReceiptComplete &&
            PhysicalBehaviorReceiptComplete;
    }

    internal static class NativeConstructionApiRegistry
    {
        internal static ReadOnlyCollection<NativeConstructionApiDescriptor>
            CreatePinnedApis()
        {
            NativeConstructionApiDescriptor[] values =
            {
                Api(
                    "Unity.AddComponentExactNativeGeneric",
                    NativeConstructionApiKind.ExactNativeGenericAddComponent,
                    "instance T UnityEngine.GameObject::AddComponent<T>()",
                    0x060015C8,
                    0x01B03F00,
                    0x0086,
                    0xFFFF,
                    0,
                    "2017B90683E7A6C5C6498936AFF8C304228085223FF96E5453C2BF5F5D41C62D",
                    true),
                Api(
                    "Unity.DontDestroyOnLoad",
                    NativeConstructionApiKind.MoveBareRootToPersistentScene,
                    "static System.Void UnityEngine.Object::DontDestroyOnLoad(UnityEngine.Object)",
                    0x060016EB,
                    0x04A6B810,
                    0x0096,
                    0xFFFF,
                    1,
                    "5FAA7D6CE7B497E01B3ED959FDF35BB4911DD61C9BB2559CDFA384F2C5CA93F8",
                    false),
                Api(
                    "Unity.GameObjectCtorString",
                    NativeConstructionApiKind.BareGameObjectConstructor,
                    "instance System.Void UnityEngine.GameObject::.ctor(System.String)",
                    0x060015EC,
                    0x04A67180,
                    0x1886,
                    0xFFFF,
                    1,
                    "D8DAF0B12E4DCA5501A109733AE74D210D7853476BF2252946F7D0B9A33EB02E",
                    false),
                Api(
                    "Unity.InstantiateObjectIntoScene",
                    NativeConstructionApiKind.InstantiateObjectIntoExplicitScene,
                    "static UnityEngine.Object UnityEngine.Object::Instantiate(UnityEngine.Object,UnityEngine.SceneManagement.Scene)",
                    0x060016D9,
                    0x04A6D300,
                    0x0096,
                    0xFFFF,
                    2,
                    "3E088E572CA4CBF8CABB7F8A405665BB249454543260936E76FE3D4CC5F26C8F",
                    false)
            };
            return Array.AsReadOnly(values.OrderBy(
                value => value.ApiId,
                StringComparer.Ordinal).ToArray());
        }

        private static NativeConstructionApiDescriptor Api(
            string id,
            NativeConstructionApiKind kind,
            string signature,
            uint token,
            uint rva,
            ushort flags,
            ushort slot,
            byte parameters,
            string sha256,
            bool closedGeneric)
        {
            return new NativeConstructionApiDescriptor(
                id,
                kind,
                signature,
                token,
                rva,
                flags,
                slot,
                parameters,
                NativePinnedPolicyRegistry.Hex(sha256),
                closedGeneric);
        }
    }

    internal static class NativeConstructionApiCodec
    {
        internal const ushort Schema = 1;
        internal const ushort Revision = 1;
        internal const int ExpectedApiCount = 4;
        internal const int MaximumPreimageBytes = 4096;

        internal static NativeConstructionApiManifest CreatePinnedManifest(
            byte[] gameContentIdentityDigest)
        {
            ReadOnlyCollection<NativeConstructionApiDescriptor> apis =
                NativeConstructionApiRegistry.CreatePinnedApis();
            byte[] digest = ComputeDigest(gameContentIdentityDigest, apis);
            return new NativeConstructionApiManifest(
                Schema,
                Revision,
                gameContentIdentityDigest,
                apis,
                digest);
        }

        internal static bool TryCreateManifest(
            byte[] gameContentIdentityDigest,
            IReadOnlyList<NativeConstructionApiDescriptor> apis,
            out NativeConstructionApiManifest manifest,
            out string error)
        {
            manifest = null;
            error = string.Empty;
            try
            {
                byte[] identity = NativePolicyValue.CloneDigest(
                    gameContentIdentityDigest,
                    nameof(gameContentIdentityDigest));
                NativeConstructionApiDescriptor[] copied = (apis ??
                    throw new ArgumentNullException(nameof(apis))).ToArray();
                if (!TryValidateRows(copied, out error))
                    return false;
                manifest = new NativeConstructionApiManifest(
                    Schema,
                    Revision,
                    identity,
                    copied,
                    ComputeDigest(identity, copied));
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException ||
                ex is InvalidDataException || ex is OverflowException)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static bool TryValidateManifest(
            NativeConstructionApiManifest manifest,
            out string error)
        {
            error = string.Empty;
            try
            {
                if (manifest == null || manifest.Schema != Schema ||
                    manifest.Revision != Revision ||
                    !TryValidateRows(manifest.Apis, out error))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "native construction manifest is noncanonical";
                    return false;
                }
                byte[] expected = ComputeDigest(
                    manifest.GameContentIdentityDigest,
                    manifest.Apis);
                if (!NativePolicyValue.FixedEquals(
                        expected,
                        manifest.ConstructionPolicyDigest))
                {
                    error = "native construction manifest digest mismatch";
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

        internal static byte[] ComputeDigest(
            byte[] gameContentIdentityDigest,
            IReadOnlyList<NativeConstructionApiDescriptor> apis)
        {
            using var writer = new NativePolicyCanonicalWriter(
                MaximumPreimageBytes);
            writer.WriteDomain("operator-native-construction-policy-v1\0");
            writer.WriteUInt16(Schema);
            writer.WriteUInt16(Revision);
            writer.WriteBytes(NativePolicyValue.CloneDigest(
                gameContentIdentityDigest,
                nameof(gameContentIdentityDigest)));
            writer.WriteByte(checked((byte)apis.Count));
            foreach (NativeConstructionApiDescriptor api in apis)
            {
                writer.WriteString(api.ApiId);
                writer.WriteByte((byte)api.Kind);
                writer.WriteString(api.CanonicalSignature);
                writer.WriteUInt32(api.OriginalMetadataToken);
                writer.WriteUInt32(api.NativeRva);
                writer.WriteUInt16(api.OriginalFlags);
                writer.WriteUInt16(api.OriginalSlot);
                writer.WriteByte(api.ParameterCount);
                writer.WriteBytes(api.First32NativeSha256);
                writer.WriteBoolean(api.RequiresLoaderClosedGenericReceipt);
            }
            using SHA256 algorithm = SHA256.Create();
            return algorithm.ComputeHash(writer.Complete());
        }

        private static bool TryValidateRows(
            IReadOnlyList<NativeConstructionApiDescriptor> apis,
            out string error)
        {
            error = string.Empty;
            if (apis == null || apis.Count != ExpectedApiCount)
            {
                error = "native construction API row count is not exact";
                return false;
            }
            ReadOnlyCollection<NativeConstructionApiDescriptor> expected =
                NativeConstructionApiRegistry.CreatePinnedApis();
            for (int index = 0; index < apis.Count; index++)
            {
                if (apis[index] == null ||
                    (index > 0 && string.CompareOrdinal(
                        apis[index - 1].ApiId,
                        apis[index].ApiId) >= 0) ||
                    !apis[index].SemanticEquals(expected[index]))
                {
                    error =
                        "native construction rows are changed, duplicated, or unordered";
                    return false;
                }
            }
            return true;
        }
    }

    internal static class NativePatchCapabilityPolicy
    {
        internal const bool ExternalRuntimeKillSwitchAuthorized = false;

        internal static bool AreNeutralManifestsComplete(
            NativeTargetPolicyManifest targets,
            NativeConstructionApiManifest construction,
            out string error)
        {
            if (!NativeTargetPolicyCodec.TryValidateTargetPolicyManifest(
                    targets,
                    out error))
            {
                return false;
            }
            if (!NativeConstructionApiCodec.TryValidateManifest(
                    construction,
                    out error))
            {
                return false;
            }
            if (!NativePolicyValue.FixedEquals(
                    targets.GameContentIdentityDigest,
                    construction.GameContentIdentityDigest))
            {
                error = "native target and construction game identities differ";
                return false;
            }
            return true;
        }

        internal static bool TryAuthorizeOperationalEntry(
            NativeOperationalCapabilityReceipt capabilityReceipt,
            NativeOperationalCapabilityOwner capabilityOwner,
            NativeTargetPolicyManifest targets,
            NativeConstructionApiManifest construction,
            NativeBackendReceipt backendReceipt,
            NativeConstructionPhysicalReceipt physicalReceipt,
            bool externalRuntimeKillSwitchAuthorization,
            out string error)
        {
            if (!NativeOperationalCapabilityReceiptCodec
                .TryValidateEvidenceReceipt(
                    capabilityReceipt,
                    capabilityOwner,
                    targets,
                    construction,
                    backendReceipt,
                    physicalReceipt,
                    out error))
            {
                return false;
            }
            if (!externalRuntimeKillSwitchAuthorization ||
                !ExternalRuntimeKillSwitchAuthorized)
            {
                error = "external native runtime kill switch is not authorized";
                return false;
            }
            error = "native runtime integration remains incomplete";
            return false;
        }
    }
}
