using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OperatorModdedOperations.NativePatching
{
    internal static class NativePinnedPolicyRegistry
    {
        internal static ReadOnlyCollection<NativeSemanticTargetDescriptor>
            CreatePinnedTargets()
        {
            var targets = new List<NativeSemanticTargetDescriptor>
            {
                Hook("GM.Start", "instance System.Void GameMode::Start()",
                    0x0600074B, 0x00EF3640, 0x0081, 0xFFFF, 0,
                    "4B58930185EC2185A91EB85AC370E34553EF66534E5E59196BCD9830549B51CD",
                    NativeSemanticInvocationKind.PrivateInstance,
                    NativeSemanticBodyPolicy.SuppressOwnedForLifetime),
                Hook("GM.Initialize", "instance System.Void GameMode::Initialize()",
                    0x0600074C, 0x00EF3280, 0x0084, 0xFFFF, 0,
                    "703BF1B1DF01F6811F20CDDDB5E0CBD4BED064EE9056AC45AF90C9AA0424C18D",
                    NativeSemanticInvocationKind.Instance,
                    NativeSemanticBodyPolicy.PermitOneGenerationInitialize),
                Hook("PVE.Awake", "instance System.Void InfiltrationManager::Awake()",
                    0x0600076E, 0x00EF3800, 0x0081, 0xFFFF, 0,
                    "6A199D12ECF0536F01A19A159A57E7BA7F03720BE312372D4A4FDD0F13078EDE",
                    NativeSemanticInvocationKind.PrivateInstance,
                    NativeSemanticBodyPolicy.PermitActualRuntimeAwakeAndValidateSingleton),
                Hook("PVE.OnStartClient",
                    "instance System.Void InfiltrationManager::OnStartClient()",
                    0x0600076F, 0x00EF4720, 0x00C6, 0x000B, 0,
                    "33451B9FD29723589FE7AEB60AEABDDD27C4297C41347467A2A619422686D738",
                    NativeSemanticInvocationKind.Instance,
                    NativeSemanticBodyPolicy.SubstituteInitializeAndSuppressOriginal),
                Hook("PVE.OnStartServer",
                    "instance System.Void InfiltrationManager::OnStartServer()",
                    0x06000770, 0x00EF4B80, 0x00C6, 0x0009, 0,
                    "5AA50555AFB31FB9AEAFD4A8F80B307D8CDDCF2CBDF762B33FA8AF697D029A95",
                    NativeSemanticInvocationKind.Instance,
                    NativeSemanticBodyPolicy.SuppressOwnedOnStartServer),
                Hook("PVE.Update", "instance System.Void InfiltrationManager::Update()",
                    0x06000771, 0x00EF67C0, 0x0081, 0xFFFF, 0,
                    "ECD35DED1B870CFA0BDB8F12EAEB0E00D15FF02285F954FB4C43DD69C2348775",
                    NativeSemanticInvocationKind.PrivateInstance,
                    NativeSemanticBodyPolicy.SuppressOwnedUpdateForLifetime),
                Hook("PVE.AllPlayers",
                    "instance System.Void InfiltrationManager::Server_AllPlayersLoaded()",
                    0x06000772, 0x00EF54D0, 0x00C6, 0x0012, 0,
                    "AE9FC85A7B38AD40F51D2F5B9BD214C27708A6ADF0127AF92B001513EC356BCD",
                    NativeSemanticInvocationKind.Instance,
                    NativeSemanticBodyPolicy.LatchAllPlayersAndSuppressOriginal),
                Hook("PVE.SpawnRpcInvoke",
                    "static System.Void InfiltrationManager::InvokeUserCode_RPC_SetSpawnPosition(Mirror.NetworkBehaviour,Mirror.NetworkReader,Mirror.NetworkConnectionToClient)",
                    0x06000783, 0x00EF3D20, 0x0094, 0xFFFF, 3,
                    "80900BE32FC3BB04BFCFD713C550CAF1F5CF0B3E00580EE0087680090C26D613",
                    NativeSemanticInvocationKind.GeneratedStaticRpcInvoker,
                    NativeSemanticBodyPolicy.SubstitutePlacementReceiverAndSuppressOriginal),
                Hook("PVP.OnStartClient",
                    "instance System.Void PvpGameode::OnStartClient()",
                    0x06000792, 0x00F1AF10, 0x00C6, 0x000B, 0,
                    "EB4589803D8C5DF8DBF06B2CEBF8215C08F2AE08B4757724C75839B22CB426EA",
                    NativeSemanticInvocationKind.Instance,
                    NativeSemanticBodyPolicy.SubstituteInitializeAndSuppressOriginal),
                Hook("PVP.Update", "instance System.Void PvpGameode::Update()",
                    0x06000794, 0x00F1CF90, 0x0081, 0xFFFF, 0,
                    "DD2B13B4A1FF25C84BA385E2E58A14796D332E427B475848C959975F06160482",
                    NativeSemanticInvocationKind.PrivateInstance,
                    NativeSemanticBodyPolicy.HoldUpdateUntilBeginThenPermitPlaying),
                Hook("PVP.AllPlayers",
                    "instance System.Void PvpGameode::Server_AllPlayersLoaded()",
                    0x06000796, 0x00F1BC60, 0x00C6, 0x0012, 0,
                    "C2DED1BE30AA8BD5AADE6677868FDA27308DA0B11D775D37D60D39A7B4116043",
                    NativeSemanticInvocationKind.Instance,
                    NativeSemanticBodyPolicy.CoalesceAllPlayersThenPermitOnce),
                Hook("PVP.StartNewRound",
                    "instance System.Void PvpGameode::StartNewRound()",
                    0x06000799, 0x00F1C0E0, 0x0081, 0xFFFF, 0,
                    "B19F90C5F4E3C84DA06E4F1F500749054751527E7D06C584C8A7E47DEB61152B",
                    NativeSemanticInvocationKind.PrivateInstance,
                    NativeSemanticBodyPolicy.HoldFirstRoundThenPermitBeginAndPlaying),
                Hook("PVP.ClockMoveNext",
                    "instance System.Boolean PvpGameode+<ClockCountDown>d__61::MoveNext()",
                    0x060007C0, 0x00F1DF60, 0x01E1, 0x0006, 0,
                    "2D3FD6D92C0D1012B779FEB5855CF6C5D17D76639F5E59598A0026BA3FD77C37",
                    NativeSemanticInvocationKind.GeneratedIteratorMoveNext,
                    NativeSemanticBodyPolicy.FatalBeforeBeginThenPermitBoundClock),
                Hook("PVE.MovePlayerMoveNext",
                    "instance System.Boolean GameManager+<MovePlayerToSpawn>d__277::MoveNext()",
                    0x060006F5, 0x00EF7AA0, 0x01E1, 0x0006, 0,
                    "3E0C261F5E166AB27052FCE394D8A5163B423F24F40B2933E3B85C1B9850FA8F",
                    NativeSemanticInvocationKind.GeneratedIteratorMoveNext,
                    NativeSemanticBodyPolicy.PermitBoundPlacementIteratorOnly),
                Hook("NI.Awake", "instance System.Void Mirror.NetworkIdentity::Awake()",
                    0x06000172, 0x03623070, 0x0083, 0xFFFF, 0,
                    "DAF314AACFDB785AD20EEE2E5C9C891F355FFCF387E497C1A5CD2F446B5E0BCF",
                    NativeSemanticInvocationKind.Instance,
                    NativeSemanticBodyPolicy.PermitCloneAwakeAndValidateGraph),
                Hook("NI.DeserializeClient",
                    "instance System.Void Mirror.NetworkIdentity::DeserializeClient(Mirror.NetworkReader,System.Boolean)",
                    0x06000182, 0x03623580, 0x0083, 0xFFFF, 2,
                    "D78A6428BB925BCAEE78108D250CD8E9C6E5722C76290CC5C45FA5DC38F9FA49",
                    NativeSemanticInvocationKind.Instance,
                    NativeSemanticBodyPolicy.PermitRemoteDeserializeAndValidateGraph),
                Hook("NI.OnStopClient",
                    "instance System.Void Mirror.NetworkIdentity::OnStopClient()",
                    0x06000179, 0x03624570, 0x0083, 0xFFFF, 0,
                    "E823256A0115823CF7807865D73CED5600B1EEED589D9879499717977F7480EE",
                    NativeSemanticInvocationKind.Instance,
                    NativeSemanticBodyPolicy.PermitRouteSpecificStopClient),
                Hook("NI.OnStopServer",
                    "instance System.Void Mirror.NetworkIdentity::OnStopServer()",
                    0x06000177, 0x03624730, 0x0083, 0xFFFF, 0,
                    "0ACEDDA6CDF0AA7CA52DA68DAFD694F63010CBAC71E6137C08D6D82E2881BA26",
                    NativeSemanticInvocationKind.Instance,
                    NativeSemanticBodyPolicy.PermitHostStopServer),
                Hook("NI.ResetState",
                    "instance System.Void Mirror.NetworkIdentity::ResetState()",
                    0x0600018A, 0x03624B70, 0x0083, 0xFFFF, 0,
                    "92B743621F0819F08A94971266FE70D13FE71A0A59DDF416F8854E4C0F05F45A",
                    NativeSemanticInvocationKind.Instance,
                    NativeSemanticBodyPolicy.PermitRouteSpecificReset),
                Hook("NS.SpawnObject",
                    "static System.Void Mirror.NetworkServer::SpawnObject(UnityEngine.GameObject,Mirror.NetworkConnection)",
                    0x060002B1, 0x03639110, 0x0091, 0xFFFF, 2,
                    "45BD56723BF24669BF9E04A0AF0998018860BCDAA8E527CBF9BBCC4DD997908A",
                    NativeSemanticInvocationKind.PrivateStatic,
                    NativeSemanticBodyPolicy.PermitHostSpawnObjectSingleActivation),
                Hook("NS.UnSpawn",
                    "static System.Void Mirror.NetworkServer::UnSpawn(UnityEngine.GameObject)",
                    0x060002B3, 0x0363A6A0, 0x0096, 0xFFFF, 1,
                    "2E2BB48AA96C4B5FC3B7DBC799A37C68E351C3C9A8758E7F7D801A20AD9F8286",
                    NativeSemanticInvocationKind.Static,
                    NativeSemanticBodyPolicy.PermitOneHostUnspawn),
                Hook("NC.OnHostClientSpawn",
                    "static System.Void Mirror.NetworkClient::OnHostClientSpawn(Mirror.SpawnMessage)",
                    0x060000F2, 0x0361A480, 0x0093, 0xFFFF, 1,
                    "7B1FC62BAC925716C6CE00FBE1504839668D3F3EB4ADF9DE2057630471D400C6",
                    NativeSemanticInvocationKind.PrivateStatic,
                    NativeSemanticBodyPolicy.PermitOneHostClientAdoption),
                Invoke("PVP.ClockFactory",
                    "instance Il2CppSystem.Collections.IEnumerator PvpGameode::ClockCountDown()",
                    0x06000793, 0x00F1A3C0, 0x0081, 0xFFFF, 0,
                    "84C26F0AAD07B7744F3EBF40A696D669B82A85CB9D02268BBD612376AF3FD5D4",
                    NativeSemanticInvocationKind.PrivateInstance),
                Invoke("NI.InitializeBehaviours",
                    "instance System.Void Mirror.NetworkIdentity::InitializeNetworkBehaviours()",
                    0x06000170, 0x03623CC0, 0x0083, 0xFFFF, 0,
                    "B5432EDBB894E58B937540E75EF42DB831E96A96E1A80EBA3C8B3C641EE2E72B",
                    NativeSemanticInvocationKind.Instance),
                Invoke("PVE.MoveFactory",
                    "instance Il2CppSystem.Collections.IEnumerator GameManager::MovePlayerToSpawn(UnityEngine.Vector3,UnityEngine.Quaternion)",
                    0x060006A8, 0x00EEADC0, 0x0086, 0xFFFF, 2,
                    "660354951DA909999672AE4D407D2FA5BD57CADA41C4FEFCAE03A917BD463C0D",
                    NativeSemanticInvocationKind.Instance),
                Invoke("Mirror.ReadVector3",
                    "static UnityEngine.Vector3 Mirror.NetworkReaderExtensions::ReadVector3(Mirror.NetworkReader)",
                    0x06000238, 0x03630970, 0x0096, 0xFFFF, 1,
                    "03F9DEC9BB911D647948528B191C82CF86B8CAC3F1B47B13AD88129D85378E9C",
                    NativeSemanticInvocationKind.StaticExtension),
                Invoke("Mirror.ReadQuaternion",
                    "static UnityEngine.Quaternion Mirror.NetworkReaderExtensions::ReadQuaternion(Mirror.NetworkReader)",
                    0x06000244, 0x0362F110, 0x0096, 0xFFFF, 1,
                    "198AB16EBBCF52F2C6FF77F31A797A0211C51C1F2AC64950EC7CC979F5CFB175",
                    NativeSemanticInvocationKind.StaticExtension),
                Invoke("NS.Spawn",
                    "static System.Void Mirror.NetworkServer::Spawn(UnityEngine.GameObject,System.UInt32,Mirror.NetworkConnection)",
                    0x060002B0, 0x03639DE0, 0x0096, 0xFFFF, 3,
                    "0A256C64E89742076E84892C01342DDD9B1134B4A10FFC4FB66A5EE8C614E6B4",
                    NativeSemanticInvocationKind.Static),
                Invoke("NC.RegisterSpawnHandler",
                    "static System.Void Mirror.NetworkClient::RegisterSpawnHandler(System.UInt32,Mirror.SpawnHandlerDelegate,Mirror.UnSpawnDelegate)",
                    0x060000E0, 0x0361E540, 0x0096, 0xFFFF, 3,
                    "EC8AB2727851FA96A08A2718E4BBA18D7DF571F8EE98B00A3A35B37955E670A5",
                    NativeSemanticInvocationKind.Static),
                Invoke("Unity.SetActive",
                    "instance System.Void UnityEngine.GameObject::SetActive(System.Boolean)",
                    0x060015D4, 0x04A66950, 0x0086, 0xFFFF, 1,
                    "4F621667C4592E1E2D2A2C4CD9FC6590811E3C6F3F6F36008EB1AD0FD3931AE8",
                    NativeSemanticInvocationKind.UnityEngineInstance),
                Invoke("Unity.Destroy",
                    "static System.Void UnityEngine.Object::Destroy(UnityEngine.Object)",
                    0x060016E4, 0x04A6B660, 0x0096, 0xFFFF, 1,
                    "8CB49EF69B07BEDEBF8A30591A87EEC2ACA7F91C23DD465FC51B9CADB1060F5E",
                    NativeSemanticInvocationKind.UnityEngineStatic),
                Invoke("Unity.StartCoroutineIEnumerator",
                    "instance UnityEngine.Coroutine UnityEngine.MonoBehaviour::StartCoroutine(Il2CppSystem.Collections.IEnumerator)",
                    0x06001648, 0x04A69BE0, 0x0086, 0xFFFF, 1,
                    "97AD0FB68C0348882FF76053A10B56B561B44257E02CD9662B6CFD9EA313E646",
                    NativeSemanticInvocationKind.UnityEngineInstance,
                    NativeRvaIdentityPolicy.SharedRvaRequiresExactTokenAndSignature)
            };
            return Array.AsReadOnly(targets
                .OrderBy(value => value.TargetId, StringComparer.Ordinal)
                .ToArray());
        }

        internal static ReadOnlyCollection<NativeRootOwnershipPolicy>
            CreateRootPolicies()
        {
            return Array.AsReadOnly(new[]
            {
                new NativeRootOwnershipPolicy(
                    NativeOwnedRootRole.Template,
                    NativeRootSceneResidence.PersistentStagingOutsidePackageScene,
                    NativeRootConstructionPolicy.ExactNativeAddComponent,
                    0,
                    NativeRootNetworkRoute.NeverNetworked,
                    NativeRootActivationBoundary.NeverActivatedForGameplay,
                    NativeRootDisposalPolicy.DestroyAfterHandlerAndGenerationDrain,
                    true),
                new NativeRootOwnershipPolicy(
                    NativeOwnedRootRole.PreflightProbe,
                    NativeRootSceneResidence.PersistentStagingOutsidePackageScene,
                    NativeRootConstructionPolicy.ActualInactiveCloneOfFrozenTemplate,
                    1,
                    NativeRootNetworkRoute.NeverNetworked,
                    NativeRootActivationBoundary.NeverActivatedForGameplay,
                    NativeRootDisposalPolicy.DestroyBeforeAnyRuntimeClone,
                    false),
                new NativeRootOwnershipPolicy(
                    NativeOwnedRootRole.HostRuntime,
                    NativeRootSceneResidence.ExactFrozenPackageSceneBeforeNativeBoundary,
                    NativeRootConstructionPolicy.ActualInactiveCloneOfFrozenTemplate,
                    1,
                    NativeRootNetworkRoute.DirectServerSpawn,
                    NativeRootActivationBoundary.NativeSpawnObjectSingleActivation,
                    NativeRootDisposalPolicy.NetworkUnspawnThenUnityDestroy,
                    false),
                new NativeRootOwnershipPolicy(
                    NativeOwnedRootRole.RemoteRuntime,
                    NativeRootSceneResidence.ExactFrozenPackageSceneBeforeHandlerReturn,
                    NativeRootConstructionPolicy.ActualInactiveCloneOfFrozenTemplate,
                    1,
                    NativeRootNetworkRoute.CustomSpawnHandlerReturn,
                    NativeRootActivationBoundary.MirrorApplySpawnPayloadSingleActivation,
                    NativeRootDisposalPolicy.UnspawnCallbackThenDeferredUnityDestroy,
                    false)
            });
        }

        internal static ReadOnlyCollection<NativeModeLifecyclePolicy>
            CreateModePolicies()
        {
            NativeLifecycleSemanticFlags common =
                NativeLifecycleSemanticFlags.ExactNativeOwner |
                NativeLifecycleSemanticFlags.OwnerEnabledThroughoutGeneration |
                NativeLifecycleSemanticFlags.GameModeStartSuppressedForLifetime |
                NativeLifecycleSemanticFlags.GameModeInitializeExactlyOnce |
                NativeLifecycleSemanticFlags.OriginalOnStartClientBodyZero |
                NativeLifecycleSemanticFlags.HandlerOnlyRegistration |
                NativeLifecycleSemanticFlags.NoPrefabEntry |
                NativeLifecycleSemanticFlags.NoSyncObjectRepairOrSynthesis |
                NativeLifecycleSemanticFlags.OneOwnerSpawnAttempt;
            NativeLifecycleSemanticFlags pve = common |
                NativeLifecycleSemanticFlags.PveAwakeActualRuntimeOnce |
                NativeLifecycleSemanticFlags.PveOnStartServerSuppressed |
                NativeLifecycleSemanticFlags.PveAllPlayersSuppressed |
                NativeLifecycleSemanticFlags.PveUpdateSuppressedForLifetime |
                NativeLifecycleSemanticFlags.PveRaidManagerDisabled |
                NativeLifecycleSemanticFlags.PveExfilEnabledAndLocked |
                NativeLifecycleSemanticFlags.PveRunnerPausedBeforeBegin |
                NativeLifecycleSemanticFlags.PveRunnerRaidTimerOnlyAfterBegin;
            NativeLifecycleSemanticFlags pvp = common |
                NativeLifecycleSemanticFlags.PvpUpdateHeldUntilBegin |
                NativeLifecycleSemanticFlags.PvpAllPlayersHeldUntilRuntimeReady |
                NativeLifecycleSemanticFlags.PvpFirstStartNewRoundHeld |
                NativeLifecycleSemanticFlags.PvpNoClockBeforeBegin |
                NativeLifecycleSemanticFlags.PvpEarlyClockMoveNextFatalFalse |
                NativeLifecycleSemanticFlags.PvpOneFreshClockAtBegin |
                NativeLifecycleSemanticFlags.PvpLaterRoundsOnlyWhilePlaying;
            return Array.AsReadOnly(new[]
            {
                new NativeModeLifecyclePolicy(
                    NativePolicyOperationMode.Pve,
                    2,
                    pve,
                    1,
                    0,
                    0,
                    0,
                    "pve-network-raid-timer-runner-v1",
                    "pve-runtime-ready-place-population-begin-v1"),
                new NativeModeLifecyclePolicy(
                    NativePolicyOperationMode.Pvp,
                    2,
                    pvp,
                    1,
                    0,
                    0,
                    0,
                    "pvp-native-clock-runner-v1",
                    "pvp-runtime-ready-player-begin-v1")
            });
        }

        internal static ReadOnlyCollection<NativeSemanticPolicyRule>
            CreateSemanticRules()
        {
            return Array.AsReadOnly(new[]
            {
                new NativeSemanticPolicyRule(
                    "handler-dictionary-ownership-v1",
                    NativeSemanticRuleKind.HandlerDictionaryOwnership,
                    1,
                    NativeSemanticRuleFlags.ExactCaseSensitiveIdentity |
                    NativeSemanticRuleFlags.TryGetValueBeforeMutation |
                    NativeSemanticRuleFlags.ExactOwnedDelegateIdentity |
                    NativeSemanticRuleFlags.RemoveOnlySpawnAndUnspawn |
                    NativeSemanticRuleFlags.PrefabDictionaryReadOnly |
                    NativeSemanticRuleFlags.BroadUnregisterForbidden |
                    NativeSemanticRuleFlags.ClearSpawnersForbidden |
                    NativeSemanticRuleFlags.PartialRemovalQuarantines,
                    new[]
                    {
                        new NativeSemanticRuleScalar("dictionary-count", 3),
                        new NativeSemanticRuleScalar("exact-remove-count", 2),
                        new NativeSemanticRuleScalar("prefab-mutation-count", 0),
                        new NativeSemanticRuleScalar("registration-nonce-count", 1)
                    }),
                new NativeSemanticPolicyRule(
                    "move-player-iterator-layout-v1",
                    NativeSemanticRuleKind.MovePlayerIteratorLayout,
                    1,
                    NativeSemanticRuleFlags.ExactGeneratedClassRequired |
                    NativeSemanticRuleFlags.InitialStateZero |
                    NativeSemanticRuleFlags.InitialCurrentNull |
                    NativeSemanticRuleFlags.ExactPoseFieldsRequired |
                    NativeSemanticRuleFlags.InitialPlayerReferenceNull |
                    NativeSemanticRuleFlags.BindBeforeSchedule |
                    NativeSemanticRuleFlags.SingleScheduleNoRetry,
                    new[]
                    {
                        new NativeSemanticRuleScalar("current-offset", 0x18),
                        new NativeSemanticRuleScalar("player-reference-offset", 0x40),
                        new NativeSemanticRuleScalar("position-offset", 0x20),
                        new NativeSemanticRuleScalar("rotation-offset", 0x2C),
                        new NativeSemanticRuleScalar("state-offset", 0x10)
                    }),
                new NativeSemanticPolicyRule(
                    "network-identity-graph-v1",
                    NativeSemanticRuleKind.NetworkIdentityGraph,
                    1,
                    NativeSemanticRuleFlags.AuthoritativeArrayInitiallyNull |
                    NativeSemanticRuleFlags.ExactManualInitializeCounts |
                    NativeSemanticRuleFlags.IncludeInactiveOrderAndIndexes |
                    NativeSemanticRuleFlags.NonNullConstructorOwnedSyncLists |
                    NativeSemanticRuleFlags.MutableGraphNonAliased |
                    NativeSemanticRuleFlags.AwakeRepeatMustPreserveGraph |
                    NativeSemanticRuleFlags.SyncObjectRepairForbidden,
                    new[]
                    {
                        new NativeSemanticRuleScalar("host-manual-initialize-count", 1),
                        new NativeSemanticRuleScalar("maximum-behaviours", 64),
                        new NativeSemanticRuleScalar("maximum-sync-objects", 256),
                        new NativeSemanticRuleScalar("probe-manual-initialize-count", 1),
                        new NativeSemanticRuleScalar("remote-manual-initialize-count", 1),
                        new NativeSemanticRuleScalar("template-manual-initialize-count", 0)
                    }),
                new NativeSemanticPolicyRule(
                    "pvp-clock-iterator-binding-v1",
                    NativeSemanticRuleKind.PvpClockIteratorBinding,
                    1,
                    NativeSemanticRuleFlags.NoIteratorBeforeBegin |
                    NativeSemanticRuleFlags.EarlyMoveNextFatalFalse |
                    NativeSemanticRuleFlags.OneFreshIteratorAtBegin |
                    NativeSemanticRuleFlags.GateOpenBeforeSchedule |
                    NativeSemanticRuleFlags.ExactPointerGenerationBinding,
                    new[]
                    {
                        new NativeSemanticRuleScalar("begin-fresh-clock-count", 1),
                        new NativeSemanticRuleScalar("pre-begin-clock-factory-maximum", 0),
                        new NativeSemanticRuleScalar("pre-begin-move-next-maximum", 0)
                    })
            });
        }

        private static NativeSemanticTargetDescriptor Hook(
            string id,
            string signature,
            uint token,
            uint rva,
            ushort flags,
            ushort slot,
            byte parameters,
            string hash,
            NativeSemanticInvocationKind invocation,
            NativeSemanticBodyPolicy policy)
        {
            return new NativeSemanticTargetDescriptor(
                id,
                NativeSemanticTargetKind.OperationalHook,
                signature,
                token,
                rva,
                flags,
                slot,
                parameters,
                Hex(hash),
                NativePatchBundleKind.PrefixPostfixFinalizer,
                invocation,
                policy,
                NativeRvaIdentityPolicy.ExactTokenSignatureAndRva);
        }

        private static NativeSemanticTargetDescriptor Invoke(
            string id,
            string signature,
            uint token,
            uint rva,
            ushort flags,
            ushort slot,
            byte parameters,
            string hash,
            NativeSemanticInvocationKind invocation,
            NativeRvaIdentityPolicy rvaIdentityPolicy =
                NativeRvaIdentityPolicy.ExactTokenSignatureAndRva)
        {
            return new NativeSemanticTargetDescriptor(
                id,
                NativeSemanticTargetKind.InvokeDependency,
                signature,
                token,
                rva,
                flags,
                slot,
                parameters,
                Hex(hash),
                NativePatchBundleKind.InvokeOnly,
                invocation,
                NativeSemanticBodyPolicy.InvokeOnly,
                rvaIdentityPolicy);
        }

        internal static byte[] Hex(string value)
        {
            if (value == null || value.Length != 64)
                throw new ArgumentException("SHA-256 hex must contain 64 digits");
            var result = new byte[32];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = Convert.ToByte(
                    value.Substring(index * 2, 2),
                    16);
            }
            return result;
        }
    }
}
