using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;

namespace OperatorModdedOperations.NativePatching
{
    internal enum NativePatchAdapterState : byte
    {
        Cold = 0,
        InventoryAttested = 1,
        PolicyResolved = 2,
        TargetsResolved = 3,
        Installing = 4,
        InstalledNativeVerified = 5,
        BackendProbe = 6,
        Operational = 7,
        PreDeinitRequested = 8,
        DrainingRootsAndHandlers = 9,
        DetachingCallbacks = 10,
        CallbackDetachedBackendPassThroughResident = 11,
        RestartRequiredResidentFault = 250,
        RestartRequiredResidentQuarantine = 251
    }

    internal enum NativePatchLeaseKind : byte
    {
        PersistentTemplate = 1,
        PendingCreationClaim = 2,
        HostRuntimeRoot = 3,
        RemoteRuntimeRoot = 4,
        PlacementIterator = 5,
        ClockIterator = 6,
        MirrorHandler = 7,
        RetiringOrTombstonedLiveRoot = 8,
        HookInvocation = 9
    }

    internal enum NativeSemanticTargetKind : byte
    {
        OperationalHook = 1,
        InvokeDependency = 2
    }

    internal enum NativePatchBundleKind : byte
    {
        PrefixPostfixFinalizer = 1,
        InvokeOnly = 2
    }

    internal enum NativeSemanticInvocationKind : byte
    {
        Instance = 1,
        Static = 2,
        PrivateInstance = 3,
        PrivateStatic = 4,
        GeneratedStaticRpcInvoker = 5,
        GeneratedIteratorMoveNext = 6,
        StaticExtension = 7,
        UnityEngineInstance = 8,
        UnityEngineStatic = 9
    }

    internal enum NativeRvaIdentityPolicy : byte
    {
        ExactTokenSignatureAndRva = 1,
        SharedRvaRequiresExactTokenAndSignature = 2
    }

    internal enum NativeSemanticBodyPolicy : byte
    {
        SuppressOwnedForLifetime = 1,
        PermitOneGenerationInitialize = 2,
        PermitActualRuntimeAwakeAndValidateSingleton = 3,
        SubstituteInitializeAndSuppressOriginal = 4,
        SuppressOwnedOnStartServer = 5,
        SuppressOwnedUpdateForLifetime = 6,
        LatchAllPlayersAndSuppressOriginal = 7,
        SubstitutePlacementReceiverAndSuppressOriginal = 8,
        HoldUpdateUntilBeginThenPermitPlaying = 9,
        CoalesceAllPlayersThenPermitOnce = 10,
        HoldFirstRoundThenPermitBeginAndPlaying = 11,
        FatalBeforeBeginThenPermitBoundClock = 12,
        PermitBoundPlacementIteratorOnly = 13,
        PermitCloneAwakeAndValidateGraph = 14,
        PermitRemoteDeserializeAndValidateGraph = 15,
        PermitRouteSpecificStopClient = 16,
        PermitHostStopServer = 17,
        PermitRouteSpecificReset = 18,
        PermitHostSpawnObjectSingleActivation = 19,
        PermitOneHostUnspawn = 20,
        PermitOneHostClientAdoption = 21,
        InvokeOnly = 22
    }

    internal enum NativeOwnedRootRole : byte
    {
        Template = 1,
        PreflightProbe = 2,
        HostRuntime = 3,
        RemoteRuntime = 4
    }

    internal enum NativeRootSceneResidence : byte
    {
        PersistentStagingOutsidePackageScene = 1,
        ExactFrozenPackageSceneBeforeNativeBoundary = 2,
        ExactFrozenPackageSceneBeforeHandlerReturn = 3
    }

    internal enum NativeRootNetworkRoute : byte
    {
        NeverNetworked = 1,
        DirectServerSpawn = 2,
        CustomSpawnHandlerReturn = 3
    }

    internal enum NativeRootConstructionPolicy : byte
    {
        ExactNativeAddComponent = 1,
        ActualInactiveCloneOfFrozenTemplate = 2
    }

    internal enum NativeConstructionApiKind : byte
    {
        BareGameObjectConstructor = 1,
        ExactNativeGenericAddComponent = 2,
        InstantiateObjectIntoExplicitScene = 3,
        MoveBareRootToPersistentScene = 4
    }

    internal enum NativeRootActivationBoundary : byte
    {
        NeverActivatedForGameplay = 1,
        NativeSpawnObjectSingleActivation = 2,
        MirrorApplySpawnPayloadSingleActivation = 3
    }

    internal enum NativeRootDisposalPolicy : byte
    {
        DestroyAfterHandlerAndGenerationDrain = 1,
        DestroyBeforeAnyRuntimeClone = 2,
        NetworkUnspawnThenUnityDestroy = 3,
        UnspawnCallbackThenDeferredUnityDestroy = 4
    }

    internal enum NativePolicyOperationMode : byte
    {
        Pve = 1,
        Pvp = 2
    }

    internal enum NativeSemanticRuleKind : byte
    {
        HandlerDictionaryOwnership = 1,
        MovePlayerIteratorLayout = 2,
        PvpClockIteratorBinding = 3,
        NetworkIdentityGraph = 4
    }

    [Flags]
    internal enum NativeSemanticRuleFlags : ulong
    {
        ExactCaseSensitiveIdentity = 1UL << 0,
        TryGetValueBeforeMutation = 1UL << 1,
        ExactOwnedDelegateIdentity = 1UL << 2,
        RemoveOnlySpawnAndUnspawn = 1UL << 3,
        PrefabDictionaryReadOnly = 1UL << 4,
        BroadUnregisterForbidden = 1UL << 5,
        ClearSpawnersForbidden = 1UL << 6,
        PartialRemovalQuarantines = 1UL << 7,
        ExactGeneratedClassRequired = 1UL << 8,
        InitialStateZero = 1UL << 9,
        InitialCurrentNull = 1UL << 10,
        ExactPoseFieldsRequired = 1UL << 11,
        InitialPlayerReferenceNull = 1UL << 12,
        BindBeforeSchedule = 1UL << 13,
        SingleScheduleNoRetry = 1UL << 14,
        NoIteratorBeforeBegin = 1UL << 15,
        EarlyMoveNextFatalFalse = 1UL << 16,
        OneFreshIteratorAtBegin = 1UL << 17,
        GateOpenBeforeSchedule = 1UL << 18,
        ExactPointerGenerationBinding = 1UL << 19,
        AuthoritativeArrayInitiallyNull = 1UL << 20,
        ExactManualInitializeCounts = 1UL << 21,
        IncludeInactiveOrderAndIndexes = 1UL << 22,
        NonNullConstructorOwnedSyncLists = 1UL << 23,
        MutableGraphNonAliased = 1UL << 24,
        AwakeRepeatMustPreserveGraph = 1UL << 25,
        SyncObjectRepairForbidden = 1UL << 26
    }

    [Flags]
    internal enum NativeLifecycleSemanticFlags : ulong
    {
        ExactNativeOwner = 1UL << 0,
        OwnerEnabledThroughoutGeneration = 1UL << 1,
        GameModeStartSuppressedForLifetime = 1UL << 2,
        GameModeInitializeExactlyOnce = 1UL << 3,
        OriginalOnStartClientBodyZero = 1UL << 4,
        HandlerOnlyRegistration = 1UL << 5,
        NoPrefabEntry = 1UL << 6,
        NoSyncObjectRepairOrSynthesis = 1UL << 7,
        OneOwnerSpawnAttempt = 1UL << 8,
        PveAwakeActualRuntimeOnce = 1UL << 9,
        PveOnStartServerSuppressed = 1UL << 10,
        PveAllPlayersSuppressed = 1UL << 11,
        PveUpdateSuppressedForLifetime = 1UL << 12,
        PveRaidManagerDisabled = 1UL << 13,
        PveExfilEnabledAndLocked = 1UL << 14,
        PveRunnerPausedBeforeBegin = 1UL << 15,
        PveRunnerRaidTimerOnlyAfterBegin = 1UL << 16,
        PvpUpdateHeldUntilBegin = 1UL << 17,
        PvpAllPlayersHeldUntilRuntimeReady = 1UL << 18,
        PvpFirstStartNewRoundHeld = 1UL << 19,
        PvpNoClockBeforeBegin = 1UL << 20,
        PvpEarlyClockMoveNextFatalFalse = 1UL << 21,
        PvpOneFreshClockAtBegin = 1UL << 22,
        PvpLaterRoundsOnlyWhilePlaying = 1UL << 23
    }

    [Flags]
    internal enum NativeTargetPolicyFlags : uint
    {
        AllOrNothingTargetSet = 1U << 0,
        ExactTargetSpecificCallbackTriplets = 1U << 1,
        ForeignPatchCollisionFailsClosed = 1U << 2,
        SameCallbackForeignOwnerQuarantines = 1U << 3,
        UnityMainThreadNoYieldTransitions = 1U << 4,
        HandlerOnlyNoPrefabRegistration = 1U << 5,
        HotUnloadUnsupported = 1U << 6,
        HotReloadUnsupported = 1U << 7,
        BackendPassThroughResidentTerminal = 1U << 8,
        LoaderNeutralPolicyOnly = 1U << 9,
        LoaderLocalReceiptsExcluded = 1U << 10,
        CrossLoaderPhysicalParityUnproven = 1U << 11,
        ExactInactiveSceneResidenceRequired = 1U << 12,
        ExactNativeConstructionRequired = 1U << 13,
        ActualCloneNonAliasValidationRequired = 1U << 14,
        ConstructionApiAnchorsRequired = 1U << 15
    }

    internal enum NativePatchLoaderKind : byte
    {
        BepInEx = 1,
        MelonLoader = 2
    }

    internal enum NativeBackendComponentKind : byte
    {
        Loader = 1,
        Harmony = 2,
        InteropRuntime = 3,
        HarmonySupport = 4,
        WrapperManifest = 5,
        CallbackBridge = 6
    }

    [Flags]
    internal enum NativeBackendReceiptFlags : uint
    {
        ExactHarmonyApiBinding = 1U << 0,
        ExactWrapperManifest = 1U << 1,
        ExactCallbackBridge = 1U << 2,
        CompleteResolvedTargetReceipts = 1U << 3,
        NativeDetourReceipts = 1U << 4,
        BackendProbeReceipt = 1U << 5,
        CriticalCanaryReceipt = 1U << 6,
        UnityMainThreadObserved = 1U << 7,
        ManagedWrapperDetourExpected = 1U << 8,
        ExactConstructionApiBinding = 1U << 9,
        ExactConstructionRuntimePhysicalReceipt = 1U << 10
    }

    [Flags]
    internal enum NativeConstructionPhysicalReceiptFlags : uint
    {
        BareRootPersistentBeforeComponents = 1U << 0,
        ExactClosedGenericNativeComponents = 1U << 1,
        ProbeCloneInactiveNoEarlyLifecycle = 1U << 2,
        HostRemoteExactFrozenPackageScene = 1U << 3,
        ActualCloneFreshAndNonAliased = 1U << 4,
        PriorProbeDisposedBeforeRuntimeClone = 1U << 5,
        RestartUsesFreshRootSet = 1U << 6,
        MoveGameObjectToSceneFallbackForbidden = 1U << 7
    }

    internal enum NativeBackendTeardownDisposition : byte
    {
        NotRequested = 1,
        DrainPending = 2,
        CallbackDetachedBackendPassThroughResident = 3,
        QuarantinedRestartRequired = 4
    }

    internal sealed class NativePatchLease
    {
        private readonly object issuerKey;
        private readonly NativeOperationalCapabilityReceipt capabilityReceipt;
        private readonly NativePatchGenerationToken generationToken;
        private readonly byte[] adapterInstanceNonce;
        private readonly byte[] processNonce;
        private readonly byte[] targetPolicyDigest;
        private readonly byte[] constructionPolicyDigest;

        internal ulong LeaseId { get; }
        internal ulong Generation { get; }
        internal NativePatchLeaseKind Kind { get; }
        internal NativePatchAdapterState AcquiredState { get; }
        internal string OwnerKey { get; }
        internal byte[] AdapterInstanceNonce =>
            (byte[])adapterInstanceNonce.Clone();
        internal byte[] ProcessNonce => (byte[])processNonce.Clone();
        internal byte[] TargetPolicyDigest => (byte[])targetPolicyDigest.Clone();
        internal byte[] ConstructionPolicyDigest =>
            (byte[])constructionPolicyDigest.Clone();

        private NativePatchLease(
            ulong leaseId,
            NativePatchLeaseKind kind,
            string ownerKey,
            object issuerKey,
            NativePatchGenerationToken generationToken,
            NativeOperationalCapabilityReceipt capabilityReceipt)
        {
            if (!NativePatchAdapterStateMachine.IsLeaseIssuerKey(issuerKey))
                throw new InvalidOperationException("lease issuer is invalid");
            this.issuerKey = issuerKey;
            LeaseId = leaseId;
            this.generationToken = generationToken ??
                throw new ArgumentNullException(nameof(generationToken));
            Generation = generationToken.Generation;
            Kind = kind;
            AcquiredState = NativePatchAdapterState.Operational;
            OwnerKey = NativePolicyValue.RequireString(ownerKey, nameof(ownerKey));
            this.capabilityReceipt = capabilityReceipt ??
                throw new ArgumentNullException(nameof(capabilityReceipt));
            adapterInstanceNonce = NativePolicyValue.CloneFixed(
                capabilityReceipt.AdapterInstanceNonce,
                NativePolicyValue.ProcessNonceBytes,
                nameof(capabilityReceipt.AdapterInstanceNonce),
                requireNonzero: true);
            processNonce = NativePolicyValue.CloneFixed(
                capabilityReceipt.ProcessNonce,
                NativePolicyValue.ProcessNonceBytes,
                nameof(capabilityReceipt.ProcessNonce),
                requireNonzero: true);
            targetPolicyDigest = NativePolicyValue.CloneDigest(
                capabilityReceipt.TargetPolicyDigest,
                nameof(capabilityReceipt.TargetPolicyDigest));
            constructionPolicyDigest = NativePolicyValue.CloneDigest(
                capabilityReceipt.ConstructionPolicyDigest,
                nameof(capabilityReceipt.ConstructionPolicyDigest));
        }

        internal static NativePatchLease IssueForStateMachine(
            object issuerKey,
            ulong leaseId,
            NativePatchLeaseKind kind,
            string ownerKey,
            NativePatchGenerationToken generationToken,
            NativeOperationalCapabilityReceipt capabilityReceipt)
        {
            return new NativePatchLease(
                leaseId,
                kind,
                ownerKey,
                issuerKey,
                generationToken,
                capabilityReceipt);
        }

        internal bool IsBoundTo(
            NativeOperationalCapabilityReceipt candidate)
        {
            return ReferenceEquals(capabilityReceipt, candidate);
        }

        internal bool IsBoundToGeneration(
            NativePatchGenerationToken candidate)
        {
            return ReferenceEquals(generationToken, candidate);
        }

        internal bool IsIssuedBy(object candidate)
        {
            return ReferenceEquals(issuerKey, candidate);
        }
    }

    internal sealed class NativePatchGenerationToken
    {
        private readonly object issuerKey;
        private readonly NativePatchAdapterStateMachine owner;
        private readonly byte[] adapterInstanceNonce;
        private readonly byte[] processNonce;

        private NativePatchGenerationToken(
            object issuerKey,
            NativePatchAdapterStateMachine owner,
            ulong generation,
            byte[] adapterInstanceNonce,
            byte[] processNonce)
        {
            if (!NativePatchAdapterStateMachine.IsLeaseIssuerKey(issuerKey))
                throw new InvalidOperationException("generation issuer is invalid");
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (generation == 0 ||
                generation > NativePatchAdapterStateMachine.MaximumGeneration)
            {
                throw new ArgumentOutOfRangeException(nameof(generation));
            }
            this.issuerKey = issuerKey;
            this.owner = owner;
            Generation = generation;
            this.adapterInstanceNonce = NativePolicyValue.CloneFixed(
                adapterInstanceNonce,
                NativePolicyValue.ProcessNonceBytes,
                nameof(adapterInstanceNonce),
                requireNonzero: true);
            this.processNonce = NativePolicyValue.CloneFixed(
                processNonce,
                NativePolicyValue.ProcessNonceBytes,
                nameof(processNonce),
                requireNonzero: true);
        }

        internal ulong Generation { get; }
        internal byte[] AdapterInstanceNonce =>
            (byte[])adapterInstanceNonce.Clone();
        internal byte[] ProcessNonce => (byte[])processNonce.Clone();

        internal static NativePatchGenerationToken IssueForStateMachine(
            object issuerKey,
            NativePatchAdapterStateMachine owner,
            ulong generation,
            byte[] adapterInstanceNonce,
            byte[] processNonce)
        {
            return new NativePatchGenerationToken(
                issuerKey,
                owner,
                generation,
                adapterInstanceNonce,
                processNonce);
        }

        internal bool IsOwnedBy(
            NativePatchAdapterStateMachine candidateOwner,
            object candidateIssuerKey)
        {
            return ReferenceEquals(owner, candidateOwner) &&
                ReferenceEquals(issuerKey, candidateIssuerKey);
        }
    }

    internal sealed class NativePatchLeaseLedger
    {
        internal const int MaximumActiveLeaseCount = 64;
        internal const int MaximumOwnerKeyBytes = 128;

        private readonly Dictionary<ulong, NativePatchLease> leases =
            new Dictionary<ulong, NativePatchLease>();
        private readonly object issuerKey;
        private ulong nextLeaseId = 1;

        private NativePatchLeaseLedger(object issuerKey)
        {
            if (!NativePatchAdapterStateMachine.IsLeaseIssuerKey(issuerKey))
                throw new InvalidOperationException("lease ledger issuer is invalid");
            this.issuerKey = issuerKey;
        }

        internal int Count => leases.Count;

        internal static NativePatchLeaseLedger IssueForStateMachine(
            object issuerKey)
        {
            return new NativePatchLeaseLedger(issuerKey);
        }

        internal ReadOnlyCollection<NativePatchLease> Snapshot()
        {
            return Array.AsReadOnly(leases.Values
                .OrderBy(value => value.LeaseId)
                .ToArray());
        }

        internal bool TryAcquire(
            object candidateIssuerKey,
            NativePatchGenerationToken generationToken,
            NativeOperationalCapabilityReceipt capabilityReceipt,
            NativePatchLeaseKind kind,
            string ownerKey,
            out NativePatchLease lease,
            out string error)
        {
            lease = null;
            error = string.Empty;
            string boundedOwnerKey;
            try
            {
                boundedOwnerKey = NativePolicyValue.RequireString(
                    ownerKey,
                    nameof(ownerKey));
                if (NativePolicyValue.StrictUtf8.GetByteCount(boundedOwnerKey) >
                    MaximumOwnerKeyBytes)
                {
                    error = "native adapter lease owner key exceeds its bound";
                    return false;
                }
            }
            catch (ArgumentException ex)
            {
                error = ex.Message;
                return false;
            }

            if (!ReferenceEquals(issuerKey, candidateIssuerKey) ||
                generationToken == null || capabilityReceipt == null ||
                capabilityReceipt.Phase !=
                    NativePatchAdapterState.Operational ||
                !Enum.IsDefined(typeof(NativePatchLeaseKind), kind) ||
                nextLeaseId == 0 || nextLeaseId == ulong.MaxValue ||
                leases.Count >= MaximumActiveLeaseCount)
            {
                error = "native adapter lease request is invalid";
                return false;
            }

            foreach (NativePatchLease existing in leases.Values)
            {
                if (existing.Generation != generationToken.Generation ||
                    existing.Kind != kind)
                {
                    continue;
                }
                if (HasSingletonCardinality(kind) || string.Equals(
                        existing.OwnerKey,
                        boundedOwnerKey,
                        StringComparison.Ordinal))
                {
                    error =
                        "native adapter lease duplicates generation ownership";
                    return false;
                }
            }

            lease = NativePatchLease.IssueForStateMachine(
                issuerKey,
                nextLeaseId++,
                kind,
                boundedOwnerKey,
                generationToken,
                capabilityReceipt);
            leases.Add(lease.LeaseId, lease);
            return true;
        }

        internal bool HasLeasesForGeneration(
            NativePatchGenerationToken generationToken)
        {
            return generationToken != null && leases.Values.Any(
                value => value.IsBoundToGeneration(generationToken));
        }

        internal bool TryRelease(NativePatchLease lease, out string error)
        {
            error = string.Empty;
            if (lease == null ||
                !leases.TryGetValue(
                    lease.LeaseId,
                    out NativePatchLease owned) ||
                !ReferenceEquals(owned, lease))
            {
                error = "native adapter lease is missing, forged, or released";
                return false;
            }
            leases.Remove(lease.LeaseId);
            return true;
        }

        private static bool HasSingletonCardinality(
            NativePatchLeaseKind kind)
        {
            return kind == NativePatchLeaseKind.PersistentTemplate ||
                kind == NativePatchLeaseKind.HostRuntimeRoot ||
                kind == NativePatchLeaseKind.RemoteRuntimeRoot ||
                kind == NativePatchLeaseKind.MirrorHandler;
        }
    }

    internal sealed partial class NativePatchAdapterStateMachine
    {
        internal const ulong MaximumGeneration = ulong.MaxValue - 1;

        private sealed class LeaseIssuerKey
        {
        }

        internal sealed class BackendProbeEvidence
        {
            private readonly NativePatchAdapterStateMachine owner;
            private readonly byte[] processNonce;
            private readonly byte[] adapterInstanceNonce;
            private readonly byte[] evidenceDigest;

            internal BackendProbeEvidence(
                object issuerKey,
                NativePatchAdapterStateMachine owner,
                byte[] processNonce,
                byte[] adapterInstanceNonce,
                ulong phaseSerial)
            {
                if (!NativePatchAdapterStateMachine.IsLeaseIssuerKey(issuerKey))
                {
                    throw new InvalidOperationException(
                        "backend probe evidence issuer is invalid");
                }
                this.owner = owner ??
                    throw new ArgumentNullException(nameof(owner));
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
                if (phaseSerial == 0)
                    throw new ArgumentOutOfRangeException(nameof(phaseSerial));
                PhaseSerial = phaseSerial;
                evidenceDigest = NativeBackendProbeEvidenceCodec.ComputeDigest(
                    this.processNonce,
                    this.adapterInstanceNonce,
                    phaseSerial);
            }

            internal byte[] ProcessNonce => (byte[])processNonce.Clone();
            internal byte[] AdapterInstanceNonce =>
                (byte[])adapterInstanceNonce.Clone();
            internal ulong PhaseSerial { get; }
            internal byte[] EvidenceDigest => (byte[])evidenceDigest.Clone();
            internal bool IsCanonical =>
                NativeBackendProbeEvidenceCodec.TryValidateFields(
                    processNonce,
                    adapterInstanceNonce,
                    PhaseSerial,
                    evidenceDigest);

            internal bool IsIssuedBy(
                NativePatchAdapterStateMachine candidate)
            {
                return ReferenceEquals(owner, candidate);
            }
        }

        private readonly object gate = new object();
        private readonly object leaseIssuerKey = new LeaseIssuerKey();
        private readonly NativePatchLeaseLedger leaseLedger;
        private readonly NativeOperationalCapabilityOwner capabilityOwner;
        private readonly byte[] processNonce;
        private BackendProbeEvidence backendProbeEvidence;
        private bool backendProbeEvidenceConsumed;
        private ulong backendProbePhaseSerial;
        private NativeTargetPolicyManifest issuedTargetPolicy;
        private NativeConstructionApiManifest issuedConstructionPolicy;
        private NativeBackendReceipt issuedBackendReceipt;
        private NativeConstructionPhysicalReceipt issuedPhysicalReceipt;
        private NativeOperationalCapabilityReceipt
            issuedOperationalCapabilityReceipt;
        private NativeOperationalCapabilityReceipt operationalCapabilityReceipt;
        private NativePatchGenerationToken activeGeneration;
        private ulong highestClosedGeneration;
        private NativePatchAdapterState state = NativePatchAdapterState.Cold;

        internal NativePatchAdapterStateMachine()
            : this(CreateNonce(), CreateNonce())
        {
        }

        internal NativePatchAdapterStateMachine(byte[] adapterInstanceNonce)
            : this(adapterInstanceNonce, CreateNonce())
        {
        }

        internal NativePatchAdapterStateMachine(
            byte[] adapterInstanceNonce,
            byte[] processNonce)
        {
            capabilityOwner = new NativeOperationalCapabilityOwner(
                adapterInstanceNonce);
            this.processNonce = NativePolicyValue.CloneFixed(
                processNonce,
                NativePolicyValue.ProcessNonceBytes,
                nameof(processNonce),
                requireNonzero: true);
            leaseLedger = NativePatchLeaseLedger.IssueForStateMachine(
                leaseIssuerKey);
        }

        internal byte[] AdapterInstanceNonce =>
            capabilityOwner.AdapterInstanceNonce;

        internal byte[] ProcessNonce => (byte[])processNonce.Clone();

        internal ulong HighestClosedGeneration
        {
            get
            {
                lock (gate)
                    return highestClosedGeneration;
            }
        }

        internal bool RequiresProcessRestart
        {
            get
            {
                lock (gate)
                    return IsRestartRequiredResidentState(state);
            }
        }

        internal bool BackendPassThroughResident
        {
            get
            {
                lock (gate)
                {
                    return IsRestartRequiredResidentState(state) ||
                        state == NativePatchAdapterState
                            .CallbackDetachedBackendPassThroughResident;
                }
            }
        }

        internal static bool IsLeaseIssuerKey(object candidate)
        {
            return candidate is LeaseIssuerKey;
        }

        internal NativePatchAdapterState State
        {
            get
            {
                lock (gate)
                    return state;
            }
        }

        internal int ActiveLeaseCount
        {
            get
            {
                lock (gate)
                    return leaseLedger.Count;
            }
        }

        internal ReadOnlyCollection<NativePatchLease> SnapshotLeases()
        {
            lock (gate)
            {
                return leaseLedger.Snapshot();
            }
        }

        internal bool TryAdvance(
            NativePatchAdapterState expected,
            NativePatchAdapterState next,
            out string error)
        {
            lock (gate)
            {
                error = string.Empty;
                if (state != expected)
                {
                    error = "native adapter state changed before transition";
                    return false;
                }
                if (!IsExactNextState(state, next))
                {
                    error = "native adapter transition is not monotonic/exact";
                    return false;
                }
                if (next == NativePatchAdapterState.DetachingCallbacks &&
                    (leaseLedger.Count != 0 || activeGeneration != null))
                {
                    error =
                        "native adapter cannot detach with a live generation";
                    return false;
                }
                state = next;
                return true;
            }
        }

        internal bool TryEnterBackendProbe(
            out BackendProbeEvidence evidence,
            out string error)
        {
            lock (gate)
            {
                evidence = null;
                error = string.Empty;
                if (state != NativePatchAdapterState.InstalledNativeVerified ||
                    backendProbeEvidence != null ||
                    backendProbePhaseSerial == ulong.MaxValue)
                {
                    error =
                        "backend probe evidence cannot be issued in this phase";
                    return false;
                }
                backendProbePhaseSerial++;
                evidence = new BackendProbeEvidence(
                    leaseIssuerKey,
                    this,
                    processNonce,
                    capabilityOwner.AdapterInstanceNonce,
                    backendProbePhaseSerial);
                backendProbeEvidence = evidence;
                state = NativePatchAdapterState.BackendProbe;
                return true;
            }
        }

        internal bool TryFinalizeBackendProbe(
            BackendProbeEvidence evidence,
            NativeTargetPolicyManifest targetPolicy,
            NativeConstructionApiManifest constructionPolicy,
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
            lock (gate)
            {
                receipt = null;
                error = string.Empty;
                if (state != NativePatchAdapterState.BackendProbe ||
                    backendProbeEvidenceConsumed ||
                    issuedBackendReceipt != null ||
                    evidence == null ||
                    !ReferenceEquals(backendProbeEvidence, evidence) ||
                    !evidence.IsIssuedBy(this) || !evidence.IsCanonical)
                {
                    error =
                        "backend probe evidence is missing, copied, or consumed";
                    return false;
                }
                backendProbeEvidenceConsumed = true;
                if (!NativeTargetPolicyCodec.TryCreateBackendReceipt(
                        leaseIssuerKey,
                        targetPolicy,
                        constructionPolicy,
                        evidence,
                        loaderKind,
                        receiptFlags,
                        unityMainThreadId,
                        components,
                        harmonyApiBindingDigest,
                        wrapperTargetReceiptSetDigest,
                        nativeDetourReceiptSetDigest,
                        backendProbeDigest,
                        criticalCanaryDigest,
                        constructionApiBindingReceiptDigest,
                        constructionRuntimePhysicalReceiptDigest,
                        out receipt,
                        out error))
                {
                    state =
                        NativePatchAdapterState.RestartRequiredResidentFault;
                    return false;
                }
                issuedTargetPolicy = targetPolicy;
                issuedConstructionPolicy = constructionPolicy;
                issuedBackendReceipt = receipt;
                return true;
            }
        }

        internal bool TryFinalizeConstructionPhysicalReceipt(
            NativeTargetPolicyManifest targetPolicy,
            NativeConstructionApiManifest constructionPolicy,
            NativeBackendReceipt backendReceipt,
            NativeConstructionPhysicalReceiptFlags flags,
            out NativeConstructionPhysicalReceipt receipt,
            out string error)
        {
            lock (gate)
            {
                receipt = null;
                error = string.Empty;
                if (!IsExactIssuedBackendInputs(
                        targetPolicy,
                        constructionPolicy,
                        backendReceipt) ||
                    issuedPhysicalReceipt != null)
                {
                    error =
                        "construction receipt inputs are not the issued probe";
                    return false;
                }
                if (!NativeConstructionPhysicalReceiptCodec.TryCreate(
                        targetPolicy,
                        constructionPolicy,
                        backendReceipt,
                        flags,
                        out receipt,
                        out error))
                {
                    state =
                        NativePatchAdapterState.RestartRequiredResidentFault;
                    return false;
                }
                issuedPhysicalReceipt = receipt;
                return true;
            }
        }

        internal bool TryFinalizeOperationalCapabilityReceipt(
            NativeTargetPolicyManifest targetPolicy,
            NativeConstructionApiManifest constructionPolicy,
            NativeBackendReceipt backendReceipt,
            NativeConstructionPhysicalReceipt physicalReceipt,
            out NativeOperationalCapabilityReceipt receipt,
            out string error)
        {
            lock (gate)
            {
                receipt = null;
                error = string.Empty;
                if (!IsExactIssuedBackendInputs(
                        targetPolicy,
                        constructionPolicy,
                        backendReceipt) ||
                    !ReferenceEquals(
                        issuedPhysicalReceipt,
                        physicalReceipt) ||
                    issuedOperationalCapabilityReceipt != null)
                {
                    error =
                        "operational capability inputs are not state-issued";
                    return false;
                }
                if (!NativeOperationalCapabilityReceiptCodec
                    .TryCreateEvidenceReceipt(
                        capabilityOwner,
                        targetPolicy,
                        constructionPolicy,
                        backendReceipt,
                        physicalReceipt,
                        out receipt,
                        out error))
                {
                    state =
                        NativePatchAdapterState.RestartRequiredResidentFault;
                    return false;
                }
                issuedOperationalCapabilityReceipt = receipt;
                return true;
            }
        }

        internal bool TryEnterOperationalComposite(
            NativeOperationalCapabilityReceipt capabilityReceipt,
            NativeTargetPolicyManifest targetPolicy,
            NativeConstructionApiManifest constructionPolicy,
            NativeBackendReceipt backendReceipt,
            NativeConstructionPhysicalReceipt physicalReceipt,
            bool externalRuntimeKillSwitchAuthorization,
            out string error)
        {
            lock (gate)
            {
                error = string.Empty;
                if (state == NativePatchAdapterState.Operational)
                {
                    if (ReferenceEquals(
                        operationalCapabilityReceipt,
                        capabilityReceipt))
                    {
                        return true;
                    }
                    error = "native adapter received a copied capability receipt";
                    return false;
                }
                if (!IsExactIssuedCapabilityInputs(
                        capabilityReceipt,
                        targetPolicy,
                        constructionPolicy,
                        backendReceipt,
                        physicalReceipt))
                {
                    error =
                        "native adapter capability is not the issued receipt";
                    return false;
                }
                if (!NativePatchCapabilityPolicy.TryAuthorizeOperationalEntry(
                        capabilityReceipt,
                        capabilityOwner,
                        targetPolicy,
                        constructionPolicy,
                        backendReceipt,
                        physicalReceipt,
                        externalRuntimeKillSwitchAuthorization,
                        out error))
                {
                    return false;
                }
                operationalCapabilityReceipt = capabilityReceipt;
                state = NativePatchAdapterState.Operational;
                return true;
            }
        }

        internal bool TryEnterRestartRequiredResidentQuarantine(
            bool quarantine,
            out string error)
        {
            lock (gate)
            {
                error = string.Empty;
                NativePatchAdapterState terminal = quarantine
                    ? NativePatchAdapterState
                        .RestartRequiredResidentQuarantine
                    : NativePatchAdapterState.RestartRequiredResidentFault;
                if (IsRestartRequiredResidentState(state))
                {
                    error =
                        "native adapter is already restart-required resident";
                    return false;
                }
                state = terminal;
                return true;
            }
        }

        internal bool TryOpenGeneration(
            NativeOperationalCapabilityReceipt capabilityReceipt,
            ulong requestedGeneration,
            out NativePatchGenerationToken generationToken,
            out string error)
        {
            lock (gate)
            {
                generationToken = null;
                error = string.Empty;
                if (state != NativePatchAdapterState.Operational ||
                    !ReferenceEquals(
                        operationalCapabilityReceipt,
                        capabilityReceipt) ||
                    activeGeneration != null ||
                    requestedGeneration == 0 ||
                    requestedGeneration > MaximumGeneration ||
                    highestClosedGeneration >= MaximumGeneration ||
                    requestedGeneration != highestClosedGeneration + 1)
                {
                    error =
                        "generation is not the exact bounded successor";
                    return false;
                }
                generationToken =
                    NativePatchGenerationToken.IssueForStateMachine(
                        leaseIssuerKey,
                        this,
                        requestedGeneration,
                        capabilityOwner.AdapterInstanceNonce,
                        processNonce);
                activeGeneration = generationToken;
                return true;
            }
        }

        internal bool TryAcquireLease(
            NativeOperationalCapabilityReceipt capabilityReceipt,
            NativePatchGenerationToken generationToken,
            NativePatchLeaseKind kind,
            string ownerKey,
            out NativePatchLease lease,
            out string error)
        {
            lock (gate)
            {
                if (state != NativePatchAdapterState.Operational ||
                    !ReferenceEquals(
                        operationalCapabilityReceipt,
                        capabilityReceipt) ||
                    generationToken == null ||
                    !ReferenceEquals(activeGeneration, generationToken) ||
                    !generationToken.IsOwnedBy(this, leaseIssuerKey))
                {
                    lease = null;
                    error = "lease generation or capability is not active";
                    return false;
                }
                return leaseLedger.TryAcquire(
                    leaseIssuerKey,
                    generationToken,
                    capabilityReceipt,
                    kind,
                    ownerKey,
                    out lease,
                    out error);
            }
        }

        internal bool TryReleaseLease(
            NativePatchLease lease,
            out string error)
        {
            lock (gate)
            {
                return leaseLedger.TryRelease(lease, out error);
            }
        }

        internal bool TryCloseGeneration(
            NativeOperationalCapabilityReceipt capabilityReceipt,
            NativePatchGenerationToken generationToken,
            out string error)
        {
            lock (gate)
            {
                error = string.Empty;
                if (!IsGenerationDrainState(state) ||
                    !ReferenceEquals(
                        operationalCapabilityReceipt,
                        capabilityReceipt) ||
                    generationToken == null ||
                    !ReferenceEquals(activeGeneration, generationToken) ||
                    !generationToken.IsOwnedBy(this, leaseIssuerKey) ||
                    generationToken.Generation !=
                        highestClosedGeneration + 1 ||
                    leaseLedger.HasLeasesForGeneration(generationToken))
                {
                    error =
                        "generation cannot close before its exact lease drain";
                    return false;
                }
                highestClosedGeneration = generationToken.Generation;
                activeGeneration = null;
                return true;
            }
        }

        private bool IsExactIssuedBackendInputs(
            NativeTargetPolicyManifest targetPolicy,
            NativeConstructionApiManifest constructionPolicy,
            NativeBackendReceipt backendReceipt)
        {
            return state == NativePatchAdapterState.BackendProbe &&
                ReferenceEquals(issuedTargetPolicy, targetPolicy) &&
                ReferenceEquals(issuedConstructionPolicy, constructionPolicy) &&
                ReferenceEquals(issuedBackendReceipt, backendReceipt);
        }

        private bool IsExactIssuedCapabilityInputs(
            NativeOperationalCapabilityReceipt capabilityReceipt,
            NativeTargetPolicyManifest targetPolicy,
            NativeConstructionApiManifest constructionPolicy,
            NativeBackendReceipt backendReceipt,
            NativeConstructionPhysicalReceipt physicalReceipt)
        {
            return IsExactIssuedBackendInputs(
                    targetPolicy,
                    constructionPolicy,
                    backendReceipt) &&
                ReferenceEquals(issuedPhysicalReceipt, physicalReceipt) &&
                ReferenceEquals(
                    issuedOperationalCapabilityReceipt,
                    capabilityReceipt);
        }

        private static bool IsGenerationDrainState(
            NativePatchAdapterState candidate)
        {
            return candidate == NativePatchAdapterState.Operational ||
                candidate == NativePatchAdapterState.PreDeinitRequested ||
                candidate ==
                    NativePatchAdapterState.DrainingRootsAndHandlers ||
                IsRestartRequiredResidentState(candidate);
        }

        private static bool IsRestartRequiredResidentState(
            NativePatchAdapterState candidate)
        {
            return candidate ==
                    NativePatchAdapterState.RestartRequiredResidentFault ||
                candidate == NativePatchAdapterState
                    .RestartRequiredResidentQuarantine;
        }

        private static byte[] CreateNonce()
        {
            byte[] value = new byte[NativePolicyValue.ProcessNonceBytes];
            do
            {
                RandomNumberGenerator.Fill(value);
            }
            while (value.All(item => item == 0));
            return value;
        }

        private static bool IsExactNextState(
            NativePatchAdapterState current,
            NativePatchAdapterState next)
        {
            switch (current)
            {
                case NativePatchAdapterState.Cold:
                    return next == NativePatchAdapterState.InventoryAttested;
                case NativePatchAdapterState.InventoryAttested:
                    return next == NativePatchAdapterState.PolicyResolved;
                case NativePatchAdapterState.PolicyResolved:
                    return next == NativePatchAdapterState.TargetsResolved;
                case NativePatchAdapterState.TargetsResolved:
                    return next == NativePatchAdapterState.Installing;
                case NativePatchAdapterState.Installing:
                    return next ==
                        NativePatchAdapterState.InstalledNativeVerified;
                case NativePatchAdapterState.InstalledNativeVerified:
                    return false;
                case NativePatchAdapterState.BackendProbe:
                    return false;
                case NativePatchAdapterState.Operational:
                    return next == NativePatchAdapterState.PreDeinitRequested;
                case NativePatchAdapterState.PreDeinitRequested:
                    return next ==
                        NativePatchAdapterState.DrainingRootsAndHandlers;
                case NativePatchAdapterState.DrainingRootsAndHandlers:
                    return next == NativePatchAdapterState.DetachingCallbacks;
                case NativePatchAdapterState.DetachingCallbacks:
                    return next == NativePatchAdapterState
                        .CallbackDetachedBackendPassThroughResident;
                default:
                    return false;
            }
        }
    }
}
