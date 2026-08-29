using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace OperatorModdedOperations.NativePatching
{
    internal static class NativePolicyValue
    {
        internal const int DigestBytes = 32;
        internal const int ProcessNonceBytes = 16;
        internal const int MaximumStringBytes = 256;
        internal const int MaximumTargets = 64;
        internal const int MaximumBackendComponents = 16;
        internal static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(false, true);

        internal static string RequireString(string value, string name)
        {
            if (string.IsNullOrEmpty(value) ||
                value.IndexOf('\0') >= 0 || value.IndexOf('\r') >= 0 ||
                value.IndexOf('\n') >= 0)
            {
                throw new ArgumentException(
                    name + " is empty or contains a control separator",
                    name);
            }
            byte[] encoded;
            try
            {
                encoded = StrictUtf8.GetBytes(value);
            }
            catch (EncoderFallbackException ex)
            {
                throw new ArgumentException(name + " is not valid UTF-16", name, ex);
            }
            if (encoded.Length > MaximumStringBytes)
            {
                throw new ArgumentException(
                    name + " exceeds the bounded UTF-8 size",
                    name);
            }
            return value;
        }

        internal static byte[] CloneFixed(
            byte[] value,
            int length,
            string name,
            bool requireNonzero)
        {
            if (value == null || value.Length != length ||
                (requireNonzero && value.All(item => item == 0)))
            {
                throw new ArgumentException(
                    name + " has the wrong length or is all zero",
                    name);
            }
            return (byte[])value.Clone();
        }

        internal static byte[] CloneDigest(byte[] value, string name)
        {
            return CloneFixed(value, DigestBytes, name, requireNonzero: true);
        }

        internal static bool FixedEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            int difference = 0;
            for (int index = 0; index < left.Length; index++)
                difference |= left[index] ^ right[index];
            return difference == 0;
        }
    }

    internal sealed class NativeSemanticTargetDescriptor
    {
        private readonly byte[] first32NativeSha256;

        internal NativeSemanticTargetDescriptor(
            string targetId,
            NativeSemanticTargetKind kind,
            string canonicalSignature,
            uint originalMetadataToken,
            uint nativeRva,
            ushort originalFlags,
            ushort originalSlot,
            byte parameterCount,
            byte[] first32NativeSha256,
            NativePatchBundleKind bundleKind,
            NativeSemanticInvocationKind invocationKind,
            NativeSemanticBodyPolicy bodyPolicy,
            NativeRvaIdentityPolicy rvaIdentityPolicy)
        {
            TargetId = NativePolicyValue.RequireString(targetId, nameof(targetId));
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
            BundleKind = bundleKind;
            InvocationKind = invocationKind;
            BodyPolicy = bodyPolicy;
            RvaIdentityPolicy = rvaIdentityPolicy;
            if (!Enum.IsDefined(typeof(NativeSemanticTargetKind), kind) ||
                !Enum.IsDefined(typeof(NativePatchBundleKind), bundleKind) ||
                !Enum.IsDefined(
                    typeof(NativeSemanticInvocationKind),
                    invocationKind) ||
                !Enum.IsDefined(typeof(NativeSemanticBodyPolicy), bodyPolicy) ||
                !Enum.IsDefined(
                    typeof(NativeRvaIdentityPolicy),
                    rvaIdentityPolicy) ||
                originalMetadataToken == 0 || nativeRva == 0 ||
                originalFlags == 0 || parameterCount > 16 ||
                (kind == NativeSemanticTargetKind.OperationalHook &&
                    (bundleKind != NativePatchBundleKind.PrefixPostfixFinalizer ||
                     bodyPolicy == NativeSemanticBodyPolicy.InvokeOnly)) ||
                (kind == NativeSemanticTargetKind.InvokeDependency &&
                    (bundleKind != NativePatchBundleKind.InvokeOnly ||
                     bodyPolicy != NativeSemanticBodyPolicy.InvokeOnly)))
            {
                throw new ArgumentException(
                    "native semantic target descriptor is noncanonical");
            }
        }

        internal string TargetId { get; }
        internal NativeSemanticTargetKind Kind { get; }
        internal string CanonicalSignature { get; }
        internal uint OriginalMetadataToken { get; }
        internal uint NativeRva { get; }
        internal ushort OriginalFlags { get; }
        internal ushort OriginalSlot { get; }
        internal byte ParameterCount { get; }
        internal byte[] First32NativeSha256 =>
            (byte[])first32NativeSha256.Clone();
        internal NativePatchBundleKind BundleKind { get; }
        internal NativeSemanticInvocationKind InvocationKind { get; }
        internal NativeSemanticBodyPolicy BodyPolicy { get; }
        internal NativeRvaIdentityPolicy RvaIdentityPolicy { get; }

        internal bool SemanticEquals(NativeSemanticTargetDescriptor other)
        {
            return other != null &&
                string.Equals(TargetId, other.TargetId, StringComparison.Ordinal) &&
                Kind == other.Kind &&
                string.Equals(
                    CanonicalSignature,
                    other.CanonicalSignature,
                    StringComparison.Ordinal) &&
                OriginalMetadataToken == other.OriginalMetadataToken &&
                NativeRva == other.NativeRva &&
                OriginalFlags == other.OriginalFlags &&
                OriginalSlot == other.OriginalSlot &&
                ParameterCount == other.ParameterCount &&
                NativePolicyValue.FixedEquals(
                    first32NativeSha256,
                    other.first32NativeSha256) &&
                BundleKind == other.BundleKind &&
                InvocationKind == other.InvocationKind &&
                BodyPolicy == other.BodyPolicy &&
                RvaIdentityPolicy == other.RvaIdentityPolicy;
        }
    }

    internal sealed class NativeRootOwnershipPolicy
    {
        internal NativeRootOwnershipPolicy(
            NativeOwnedRootRole role,
            NativeRootSceneResidence sceneResidence,
            NativeRootConstructionPolicy constructionPolicy,
            byte manualInitializeNetworkBehavioursCalls,
            NativeRootNetworkRoute networkRoute,
            NativeRootActivationBoundary activationBoundary,
            NativeRootDisposalPolicy disposalPolicy,
            bool isPersistentTemplateSource)
        {
            Role = role;
            SceneResidence = sceneResidence;
            ConstructionPolicy = constructionPolicy;
            ManualInitializeNetworkBehavioursCalls =
                manualInitializeNetworkBehavioursCalls;
            NetworkRoute = networkRoute;
            ActivationBoundary = activationBoundary;
            DisposalPolicy = disposalPolicy;
            IsPersistentTemplateSource = isPersistentTemplateSource;
            if (!Enum.IsDefined(typeof(NativeOwnedRootRole), role) ||
                !Enum.IsDefined(
                    typeof(NativeRootSceneResidence),
                    sceneResidence) ||
                !Enum.IsDefined(
                    typeof(NativeRootConstructionPolicy),
                    constructionPolicy) ||
                manualInitializeNetworkBehavioursCalls > 1 ||
                !Enum.IsDefined(typeof(NativeRootNetworkRoute), networkRoute) ||
                !Enum.IsDefined(
                    typeof(NativeRootActivationBoundary),
                    activationBoundary) ||
                !Enum.IsDefined(
                    typeof(NativeRootDisposalPolicy),
                    disposalPolicy))
            {
                throw new ArgumentException("native root policy is noncanonical");
            }
        }

        internal NativeOwnedRootRole Role { get; }
        internal NativeRootSceneResidence SceneResidence { get; }
        internal NativeRootConstructionPolicy ConstructionPolicy { get; }
        internal byte ManualInitializeNetworkBehavioursCalls { get; }
        internal NativeRootNetworkRoute NetworkRoute { get; }
        internal NativeRootActivationBoundary ActivationBoundary { get; }
        internal NativeRootDisposalPolicy DisposalPolicy { get; }
        internal bool IsPersistentTemplateSource { get; }

        internal bool SemanticEquals(NativeRootOwnershipPolicy other)
        {
            return other != null && Role == other.Role &&
                SceneResidence == other.SceneResidence &&
                ConstructionPolicy == other.ConstructionPolicy &&
                ManualInitializeNetworkBehavioursCalls ==
                    other.ManualInitializeNetworkBehavioursCalls &&
                NetworkRoute == other.NetworkRoute &&
                ActivationBoundary == other.ActivationBoundary &&
                DisposalPolicy == other.DisposalPolicy &&
                IsPersistentTemplateSource == other.IsPersistentTemplateSource;
        }
    }

    internal sealed class NativeModeLifecyclePolicy
    {
        internal NativeModeLifecyclePolicy(
            NativePolicyOperationMode mode,
            ushort lifecyclePolicyRevision,
            NativeLifecycleSemanticFlags flags,
            byte gameModeInitializeSuccessfulReturns,
            byte gameModeStartOriginalBodyMaximum,
            byte originalOnStartClientBodyMaximum,
            byte preBeginClockMoveNextMaximum,
            string runnerRevision,
            string beginReleaseRevision)
        {
            Mode = mode;
            LifecyclePolicyRevision = lifecyclePolicyRevision;
            Flags = flags;
            GameModeInitializeSuccessfulReturns =
                gameModeInitializeSuccessfulReturns;
            GameModeStartOriginalBodyMaximum = gameModeStartOriginalBodyMaximum;
            OriginalOnStartClientBodyMaximum = originalOnStartClientBodyMaximum;
            PreBeginClockMoveNextMaximum = preBeginClockMoveNextMaximum;
            RunnerRevision = NativePolicyValue.RequireString(
                runnerRevision,
                nameof(runnerRevision));
            BeginReleaseRevision = NativePolicyValue.RequireString(
                beginReleaseRevision,
                nameof(beginReleaseRevision));
            if (!Enum.IsDefined(typeof(NativePolicyOperationMode), mode) ||
                lifecyclePolicyRevision == 0 ||
                flags == 0 || gameModeInitializeSuccessfulReturns != 1 ||
                gameModeStartOriginalBodyMaximum != 0 ||
                originalOnStartClientBodyMaximum != 0 ||
                preBeginClockMoveNextMaximum != 0)
            {
                throw new ArgumentException(
                    "native mode lifecycle policy is noncanonical");
            }
        }

        internal NativePolicyOperationMode Mode { get; }
        internal ushort LifecyclePolicyRevision { get; }
        internal NativeLifecycleSemanticFlags Flags { get; }
        internal byte GameModeInitializeSuccessfulReturns { get; }
        internal byte GameModeStartOriginalBodyMaximum { get; }
        internal byte OriginalOnStartClientBodyMaximum { get; }
        internal byte PreBeginClockMoveNextMaximum { get; }
        internal string RunnerRevision { get; }
        internal string BeginReleaseRevision { get; }

        internal bool SemanticEquals(NativeModeLifecyclePolicy other)
        {
            return other != null && Mode == other.Mode &&
                LifecyclePolicyRevision == other.LifecyclePolicyRevision &&
                Flags == other.Flags &&
                GameModeInitializeSuccessfulReturns ==
                    other.GameModeInitializeSuccessfulReturns &&
                GameModeStartOriginalBodyMaximum ==
                    other.GameModeStartOriginalBodyMaximum &&
                OriginalOnStartClientBodyMaximum ==
                    other.OriginalOnStartClientBodyMaximum &&
                PreBeginClockMoveNextMaximum ==
                    other.PreBeginClockMoveNextMaximum &&
                string.Equals(
                    RunnerRevision,
                    other.RunnerRevision,
                    StringComparison.Ordinal) &&
                string.Equals(
                    BeginReleaseRevision,
                    other.BeginReleaseRevision,
                    StringComparison.Ordinal);
        }
    }

    internal readonly struct NativeSemanticRuleScalar
    {
        internal NativeSemanticRuleScalar(string key, long value)
        {
            Key = NativePolicyValue.RequireString(key, nameof(key));
            Value = value;
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
        }

        internal string Key { get; }
        internal long Value { get; }
    }

    internal sealed class NativeSemanticPolicyRule
    {
        private readonly ReadOnlyCollection<NativeSemanticRuleScalar> scalars;

        internal NativeSemanticPolicyRule(
            string ruleId,
            NativeSemanticRuleKind kind,
            ushort revision,
            NativeSemanticRuleFlags flags,
            IList<NativeSemanticRuleScalar> scalars)
        {
            RuleId = NativePolicyValue.RequireString(ruleId, nameof(ruleId));
            Kind = kind;
            Revision = revision;
            Flags = flags;
            NativeSemanticRuleScalar[] copied = (scalars ??
                throw new ArgumentNullException(nameof(scalars))).ToArray();
            if (!Enum.IsDefined(typeof(NativeSemanticRuleKind), kind) ||
                revision == 0 || flags == 0 || copied.Length > 32)
            {
                throw new ArgumentException(
                    "native semantic policy rule is noncanonical");
            }
            for (int index = 0; index < copied.Length; index++)
            {
                if (index > 0 && string.CompareOrdinal(
                        copied[index - 1].Key,
                        copied[index].Key) >= 0)
                {
                    throw new ArgumentException(
                        "native semantic rule scalars are duplicated or unordered");
                }
            }
            this.scalars = Array.AsReadOnly(copied);
        }

        internal string RuleId { get; }
        internal NativeSemanticRuleKind Kind { get; }
        internal ushort Revision { get; }
        internal NativeSemanticRuleFlags Flags { get; }
        internal ReadOnlyCollection<NativeSemanticRuleScalar> Scalars => scalars;

        internal bool SemanticEquals(NativeSemanticPolicyRule other)
        {
            if (other == null ||
                !string.Equals(RuleId, other.RuleId, StringComparison.Ordinal) ||
                Kind != other.Kind || Revision != other.Revision ||
                Flags != other.Flags || scalars.Count != other.scalars.Count)
            {
                return false;
            }
            for (int index = 0; index < scalars.Count; index++)
            {
                if (!string.Equals(
                        scalars[index].Key,
                        other.scalars[index].Key,
                        StringComparison.Ordinal) ||
                    scalars[index].Value != other.scalars[index].Value)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
