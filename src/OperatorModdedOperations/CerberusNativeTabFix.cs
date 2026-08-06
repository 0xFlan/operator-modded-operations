using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Michsky.DreamOS;
using Mirror;
using OperatorModAPI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Object = UnityEngine.Object;

[BepInPlugin("operator.modded-operations", "OPERATOR: Modded Operations", "0.3.22")]
[BepInProcess("OPERATOR.exe")]
[BepInDependency("operator.modapi", CerberusNativeTabFix.RequiredApiVersion)]
public sealed class CerberusNativeTabFix : BasePlugin
{
    internal const string RequiredApiVersion = "0.2.0-alpha.3";
    // These IDs identify the two runtime templates that every peer builds from
    // the same accepted package operation. They are collision-checked against
    // Mirror's current client registry before use.
    private const uint StandalonePveGameModeAssetId = 0x4D4F5001;
    private const uint StandalonePvpGameModeAssetId = 0x4D4F5002;
    private const string StandalonePveExfilMarkerPrefix = "PVE_ExfilZone_";
    // Installed level16 RaidManager serialized value. GameManagerNetwork uses
    // this value for its shipped server-side extraction countdown.
    private const float StandalonePveExtractionSeconds = 15f;
    private static readonly float[] ProfiledPveAiDiagnosticSnapshotSeconds =
    {
        0f, 10f, 30f, 60f, 90f, 120f
    };
    private static CerberusNativeTabFix instance;
    private ManualLogSource log;
    private FixRunner runner;
    private readonly Dictionary<string, LoadedMapBundles> loadedMapBundles =
        new Dictionary<string, LoadedMapBundles>(StringComparer.Ordinal);
    private readonly Dictionary<string, Sprite> previewSprites =
        new Dictionary<string, Sprite>(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> previewTextures =
        new Dictionary<string, Texture2D>(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture3D> packageTonemapLuts =
        new Dictionary<string, Texture3D>(StringComparer.Ordinal);
    private GameObject operationBoardVisualTemplate;
    private readonly Dictionary<int, CatalogPresentation> catalogPresentations =
        new Dictionary<int, CatalogPresentation>();
    private PendingMapLaunch pendingLaunch;
    private ActiveMapOperation activeOperation;
    private UnityAction<Scene, LoadSceneMode> sceneLoadedCallback;
    private UnityAction<Scene> sceneUnloadedCallback;
    private readonly HashSet<int> attachedLaptops = new HashSet<int>();
    private readonly HashSet<int> deferredSetupLoggedLaptops = new HashSet<int>();
    private readonly HashSet<string> lutDiagnostics =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<int> nativeBackBoundBoards = new HashSet<int>();
    private MethodInfo directServerPlayerSpawnMethod;
    private sealed class NativePresentationBinding
    {
        public int LaptopId;
        public MissionLaptop Laptop;
        public GameObject Page;
        public GameObject PreparationPanel;
        public GameObject ModdedButton;
        public GameObject ActiveButton;
        public GameObject SimulationButton;
        public int LastModdedOpenFrame = -1;
    }
    private readonly List<NativePresentationBinding> nativePresentationBindings =
        new List<NativePresentationBinding>();
    private sealed class PendingTransitionSnapshot
    {
        public int RequestedFrame;
        public MissionLaptop Laptop;
        public GameObject Page;
        public string Source;
    }
    private readonly List<PendingTransitionSnapshot> pendingTransitionSnapshots =
        new List<PendingTransitionSnapshot>();

    private sealed class CatalogPresentation
    {
        public MissionLaptop Laptop;
        public GameObject Page;
        public GameObject HomeShell;
        public GameObject PreparationPanel;
        public OperationBoardUI Board;
        public CerebusOpboard NativeBoardData;
        public CerebusTargetPackage NativeTargetData;
        public GameObject NativeInfiltrationMapPrefab;
        public TMP_Text HomeBriefing;
        public TMP_Text SituationReport;
        public ModdedOperationDefinition SelectedOperation;
        public string SelectedTimeCode;
    }

    private sealed class LoadedMapBundles
    {
        public ModdedMapDefinition Map;
        public readonly List<AssetBundle> Dependencies = new List<AssetBundle>();
        public readonly Dictionary<string, AssetBundle> DependenciesByPath =
            new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);
        public AssetBundle SceneBundle;
    }

    private sealed class PendingMapLaunch
    {
        public CatalogPresentation Presentation;
        public ModdedMapDefinition Map;
        public ModdedOperationDefinition Operation;
        public string TimeCode;
        public MissionLaptop LaunchLaptop;
        public PlayerNetworking LaunchPlayer;
        public LoadedMapBundles LoadingBundles;
        public readonly List<string> BundlePaths = new List<string>();
        public AssetBundleCreateRequest CurrentRequest;
        public int RequestIndex;
        public bool LaunchRequested;
        public long LoadStartedTimestamp;
        public long CurrentRequestStartedTimestamp;
        public long LaunchRequestedTimestamp;
    }

    private sealed class ActiveMapOperation
    {
        public ModdedMapDefinition Map;
        public ModdedOperationDefinition Operation;
        public string TimeCode;
        public int SceneHandle;
        public bool BootstrapCreated;
        public GameObject BootstrapRoot;
        public NetworkIdentity BootstrapIdentity;
        public GameObject BootstrapPrefabRoot;
        public NetworkIdentity BootstrapPrefabIdentity;
        public uint BootstrapAssetId;
        public bool BootstrapPrefabRegistered;
        public global::GameMode GameModeComponent;
        public bool NetworkSpawnRequested;
        public bool ReadinessInitializationClaimed;
        public bool ReadinessInitialized;
        public bool AllPlayersLoaded;
        public bool NativePvpLifecycleActive;
        public int AllPlayersLoadedFrame;
        public int BootstrapFrame;
        public int LastMaintenanceFrame;
        public int SpawnCursor;
        public bool ScenePreparationComplete;
        public bool ScenePreparationStarted;
        public int ScenePreparationEarliestFrame;
        public bool TerrainReady;
        public TerrainData RuntimeTerrainData;
        public readonly List<TerrainLayer> RuntimeTerrainLayers =
            new List<TerrainLayer>();
        public readonly Dictionary<int, int> PositionedPlayerObjects =
            new Dictionary<int, int>();
        public readonly Dictionary<int, string> PlayerMarkerNames =
            new Dictionary<int, string>();
        public readonly Dictionary<int, int> PlayerSpawnRequestFrames =
            new Dictionary<int, int>();
        public readonly Dictionary<int, int> PlayerSpawnRequestCounts =
            new Dictionary<int, int>();
        public readonly HashSet<int> CompletedPlayerSpawnIds = new HashSet<int>();
        public readonly Dictionary<int, int> PlayerMoveRequestFrames =
            new Dictionary<int, int>();
        public Il2CppSystem.Collections.Generic.List<SpawnPoint> PreviousSpawnPoints;
        public Il2CppSystem.Collections.Generic.List<SpawnPoint> OwnedSpawnPoints;
        public Il2CppReferenceArray<GameObject> PreviousFallbackSpawns;
        public Il2CppReferenceArray<GameObject> OwnedFallbackSpawns;
        public int PreviousNextSpawnIndex;
        public bool PreviousRandomSpawns;
        public bool OwnedRandomSpawns;
        public bool RandomSpawnsCaptured;
        public bool SpawnContractInstalled;
        public bool NvgColorCaptured;
        public int PreviousNvgColor;
        public bool WhitePhosphorApplied;
        public readonly List<VolumeProfile> RuntimeRenderProfiles =
            new List<VolumeProfile>();
        public bool PveSpawnAttempted;
        public int PveEnemyCount;
        public RaidManager PveRaidManager;
        public ExfilZone PveExfilZone;
        public BoxCollider PveExfilCollider;
        // Read-only acceptance evidence for a schema-v2 PVE AI profile. The
        // framework samples only the BrainAI instances that its native
        // RaidManager call added. It never changes their state.
        public readonly HashSet<int> ProfiledPvePreexistingBrainIds =
            new HashSet<int>();
        public readonly List<BrainAI> ProfiledPveDiagnosticBrains =
            new List<BrainAI>();
        public readonly Dictionary<int, Vector3> ProfiledPveInitialBrainPositions =
            new Dictionary<int, Vector3>();
        public bool ProfiledPveInitialPlayerPositionCaptured;
        public Vector3 ProfiledPveInitialPlayerPosition;
        public float ProfiledPveAiDiagnosticStartedAt = -1f;
        public float ProfiledPveAiDiagnosticNextProbeAt = -1f;
        public int ProfiledPveAiDiagnosticSnapshotIndex;
        public bool ProfiledPveAiDiagnosticComplete;
        public bool ProfiledPveAiDiagnosticAwaitingBrains;
        public bool ProfiledPveNativeContractLogged;
        public readonly List<Object> RuntimePveAssets = new List<Object>();
        public readonly List<Object> RuntimePvpAssets = new List<Object>();
    }

    public override void Load()
    {
        if (!string.Equals(
                OperatorApi.ApiVersion,
                RequiredApiVersion,
                StringComparison.Ordinal))
        {
            Log.LogError("Modded Operations requires Operator Mod API " +
                RequiredApiVersion + ", but Core exposes " +
                OperatorApi.ApiVersion + ". Adapter startup was refused.");
            return;
        }
        directServerPlayerSpawnMethod = typeof(PlayerMaster).GetMethod(
            "UserCode_CMDSpawnPlayer__NetworkIdentity",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(NetworkIdentity) },
            null);
        if (directServerPlayerSpawnMethod == null)
        {
            Log.LogError("Modded Operations could not resolve the exact current-build " +
                "PlayerMaster generated server spawn body. Adapter startup was refused.");
            return;
        }
        instance = this;
        log = Log;
        ClassInjector.RegisterTypeInIl2Cpp<FixRunner>();
        ClassInjector.RegisterTypeInIl2Cpp<StandalonePvpGameMode>();
        ClassInjector.RegisterTypeInIl2Cpp<StandalonePveGameMode>();
        var runnerObject = new GameObject("Operator_Cerberus_Native_Tab_Fix_Runner");
        Object.DontDestroyOnLoad(runnerObject);
        runner = runnerObject.AddComponent<FixRunner>();
        // Resource discovery is an infrequent lobby operation. Do not run a
        // typed FindObjectsOfTypeAll scan from MonoBehaviour.Update: doing so
        // every frame on the splash/armory path creates a measurable hitch and
        // can make the otherwise idle game appear to leak memory.
        runner.InvokeRepeating(nameof(FixRunner.Tick), 1f, 1f);
        sceneLoadedCallback = DelegateSupport.ConvertDelegate<UnityAction<Scene, LoadSceneMode>>(
            new Action<Scene, LoadSceneMode>(OnSceneLoaded));
        sceneUnloadedCallback = DelegateSupport.ConvertDelegate<UnityAction<Scene>>(
            new Action<Scene>(OnSceneUnloaded));
        SceneManager.sceneLoaded += sceneLoadedCallback;
        SceneManager.sceneUnloaded += sceneUnloadedCallback;
        log.LogInfo("Modded Operations infrastructure loaded; catalog=" +
            OperatorApi.ModdedOperations.CatalogId + ", maps=" +
            OperatorApi.ModdedOperations.Maps.Count + ", operations=" +
            OperatorApi.ModdedOperations.Operations.Count + ".");
    }

    /// <summary>
    /// Loads one asset from the dependency bundles that Modded Operations
    /// already hash-verified and retained for <paramref name="mapId"/>.
    /// The caller borrows the returned Unity object. The framework remains the
    /// sole owner of the AssetBundle and its unload lifetime.
    /// </summary>
    public static T LoadVerifiedMapDependencyAsset<T>(string mapId, string assetPath)
        where T : Object
    {
        if (instance == null || string.IsNullOrWhiteSpace(mapId) ||
            string.IsNullOrWhiteSpace(assetPath))
            return null;

        return instance.LoadVerifiedMapDependencyAssetInternal<T>(mapId, assetPath);
    }

    private T LoadVerifiedMapDependencyAssetInternal<T>(string mapId, string assetPath)
        where T : Object
    {
        if (!loadedMapBundles.TryGetValue(mapId, out LoadedMapBundles bundles) ||
            bundles == null)
            return null;

        foreach (AssetBundle dependency in bundles.Dependencies)
        {
            if (dependency == null)
                continue;
            try
            {
                T asset = dependency.LoadAsset<T>(assetPath);
                if (asset != null)
                    return asset;
            }
            catch
            {
                // Another verified dependency can own this exact asset path.
            }
        }

        return null;
    }

    public override bool Unload()
    {
        try
        {
            if (sceneLoadedCallback != null)
                SceneManager.sceneLoaded -= sceneLoadedCallback;
            if (sceneUnloadedCallback != null)
                SceneManager.sceneUnloaded -= sceneUnloadedCallback;
            sceneLoadedCallback = null;
            sceneUnloadedCallback = null;

            foreach (var presentation in catalogPresentations.Values)
            {
                if (presentation?.PreparationPanel != null)
                    Object.Destroy(presentation.PreparationPanel);
                if (presentation?.Page != null)
                    Object.Destroy(presentation.Page);
                if (presentation?.NativeBoardData != null)
                    Object.Destroy(presentation.NativeBoardData);
                if (presentation?.NativeTargetData != null)
                    Object.Destroy(presentation.NativeTargetData);
                if (presentation?.NativeInfiltrationMapPrefab != null)
                    Object.Destroy(presentation.NativeInfiltrationMapPrefab);
            }
            catalogPresentations.Clear();
            nativePresentationBindings.Clear();
            attachedLaptops.Clear();
            nativeBackBoundBoards.Clear();

            foreach (var bundles in loadedMapBundles.Values)
            {
                try { bundles?.SceneBundle?.Unload(false); } catch { }
                if (bundles == null)
                    continue;
                foreach (var dependency in bundles.Dependencies)
                {
                    try { dependency?.Unload(false); } catch { }
                }
            }
            loadedMapBundles.Clear();
            foreach (var sprite in previewSprites.Values)
                if (sprite != null) Object.Destroy(sprite);
            foreach (var texture in previewTextures.Values)
                if (texture != null) Object.Destroy(texture);
            previewSprites.Clear();
            previewTextures.Clear();
            foreach (var lut in packageTonemapLuts.Values)
                if (lut != null) Object.Destroy(lut);
            packageTonemapLuts.Clear();

            ReleaseStandaloneSceneContracts(activeOperation);
            ReleaseRuntimeTerrain(activeOperation);
            if (activeOperation?.BootstrapRoot != null)
                Object.Destroy(activeOperation.BootstrapRoot);
            activeOperation = null;
            pendingLaunch = null;
            if (runner != null)
                Object.Destroy(runner.gameObject);
            runner = null;
            instance = null;
            return true;
        }
        catch (Exception ex)
        {
            log?.LogError("Modded Operations unload failed: " + ex);
            return false;
        }
    }

    private sealed class FixRunner : MonoBehaviour
    {

        public FixRunner(IntPtr pointer) : base(pointer) { }

        public void Tick()
        {
            instance?.TryAttachAll();
        }

        public void Update()
        {
            instance?.ProcessPendingTransitionSnapshots();
            instance?.MaintainNativePresentationIsolation();
            instance?.ProcessPendingLaunch();
            instance?.MaintainStandaloneGameplay();
        }
    }

    private sealed class StandalonePvpGameMode : PvpGameode
    {
        public StandalonePvpGameMode(IntPtr pointer) : base(pointer)
        {
        }

        public override void OnStartClient()
        {
            EnterNativeReadiness("OnStartClient");
        }

        public override void Server_AllPlayersLoaded()
        {
            try
            {
                // StandardPVP is owned by the shipped PvpGameode body. This
                // creates the native team-player caches, applies the lobby's
                // round settings, freezes the round, and starts the shipped
                // RespawnPlayers coroutine.
                base.Server_AllPlayersLoaded();
                CerberusNativeTabFix.instance?.OnStandaloneAllPlayersLoaded(true);
            }
            catch (Exception ex)
            {
                CerberusNativeTabFix.instance?.OnStandalonePvpAllPlayersLoadedFailed(ex);
            }
        }

        public void EnsureStandaloneReadiness(string source)
        {
            EnterNativeReadiness(source);
        }

        private void EnterNativeReadiness(string source)
        {
            if (CerberusNativeTabFix.instance == null ||
                !CerberusNativeTabFix.instance.TryClaimStandaloneReadinessInitialization(this))
            {
                return;
            }
            try
            {
                // The shipped override sets PvpGameode.instance, calls
                // GameMode.Initialize, starts the native round clock on the
                // server, and disables team spawn-marker render objects on a
                // non-PVP route. Calling only GameMode.Initialize here would
                // omit those StandardPVP behaviors.
                base.OnStartClient();
                CerberusNativeTabFix.instance.MarkStandaloneReadinessInitialized(this, source);
            }
            catch (Exception ex)
            {
                CerberusNativeTabFix.instance.MarkStandaloneReadinessInitializationFailed(this, source, ex);
            }
        }
    }

    private sealed class StandalonePveGameMode : InfiltrationManager
    {
        public StandalonePveGameMode(IntPtr pointer) : base(pointer)
        {
        }

        public override void OnStartServer()
        {
            // The retail InfiltrationManager implementation expects an official
            // operation scene graph. Cerberus owns the bounded standalone bridge.
        }

        public override void OnStartClient()
        {
            EnsureStandaloneReadiness("OnStartClient");
        }

        public override void Server_AllPlayersLoaded()
        {
            CerberusNativeTabFix.instance?.OnStandaloneAllPlayersLoaded(false);
        }

        public void EnsureStandaloneReadiness(string source)
        {
            if (CerberusNativeTabFix.instance == null ||
                !CerberusNativeTabFix.instance.TryClaimStandaloneReadinessInitialization(this))
            {
                return;
            }
            try
            {
                Initialize();
                CerberusNativeTabFix.instance.MarkStandaloneReadinessInitialized(this, source);
            }
            catch (Exception ex)
            {
                CerberusNativeTabFix.instance.MarkStandaloneReadinessInitializationFailed(this, source, ex);
            }
        }
    }

    private void TryAttachAll()
    {
        var laptopType = ResolveMissionLaptopType();
        if (laptopType == null)
        {
            LogOnce("MissionLaptop type was not found in the loaded interop assemblies.", true);
            return;
        }

        var candidates = FindMissionLaptopComponents(laptopType);
        foreach (var candidate in candidates)
        {
            var laptop = candidate as MissionLaptop;
            if (laptop == null || laptop.gameObject == null ||
                laptop.cerberusWindowPanelManager == null)
                continue;
            Scene ownerScene = laptop.gameObject.scene;
            if (!ownerScene.IsValid() || !ownerScene.isLoaded ||
                string.IsNullOrWhiteSpace(ownerScene.path))
                continue;
            int id = laptop.GetInstanceID();
            if (attachedLaptops.Contains(id))
            {
                // A scene reload can destroy the old clone while Unity reuses
                // the laptop instance ID. Re-enter the attach path when the
                // native button is no longer present instead of permanently
                // suppressing recovery.
                var operationSelection = laptop.ActiveOperationsTab == null
                    ? null
                    : laptop.ActiveOperationsTab.transform.parent;
                var existingNativeTab = FindDeep(operationSelection, "MODDED_OPS_NATIVE_TAB") ??
                    FindDeep(operationSelection, "MODDED_OPS_TAB");
                if (existingNativeTab != null)
                {
                    var page = FindChild(operationSelection, "MODDED_OPERATIONS_PAGE");
                    var nativeHome = page == null ? null : FindDeep(page.transform, "MODDED_NATIVE_HOME");
                    var nativeBoard = FindNativeModdedPreparationPanel(laptop);
                    bool catalogIsEmpty =
                        OperatorApi.ModdedOperations.Operations.Count == 0;
                    if (nativeHome == null || (!catalogIsEmpty && nativeBoard == null))
                    {
                        // Vanilla can rebuild/clear Operation Preparation content
                        // after an official mission is opened. Re-enter the attach
                        // path so the modded clone is restored without duplicating
                        // the already-present tab.
                        attachedLaptops.Remove(id);
                    }
                    else
                    {
                    // DreamOS/localization refreshes cloned controls when the
                    // laptop is reopened. Reassert the independent tab title on
                    // the existing shipped control during the normal one-second
                    // maintenance tick so it can never fall back to the source
                    // ACTIVE OPERATIONS text.
                    SetButtonText(existingNativeTab, "MODDED OPERATIONS");
                    continue;
                    }
                }
                attachedLaptops.Remove(id);
            }
            if (TryAttachNativeTab(laptop))
                attachedLaptops.Add(id);
        }
    }

    private string lastDiagnostic;

    private void LogOnce(string message, bool warning)
    {
        if (string.Equals(lastDiagnostic, message, StringComparison.Ordinal))
            return;
        lastDiagnostic = message;
        if (warning)
            log?.LogWarning("Cerberus native tab fix: " + message);
        else
            log?.LogInfo("Cerberus native tab fix: " + message);
    }

    private static List<Component> FindMissionLaptopComponents(Type laptopType)
    {
        var result = new List<Component>(4);
        if (laptopType == null)
            return result;
        try
        {
            MethodInfo findAll = null;
            foreach (var method in typeof(Resources).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name == "FindObjectsOfTypeAll" && method.IsGenericMethodDefinition &&
                    method.GetGenericArguments().Length == 1 && method.GetParameters().Length == 0)
                {
                    findAll = method;
                    break;
                }
            }
            if (findAll == null)
                return result;
            var typedObjects = findAll.MakeGenericMethod(laptopType).Invoke(null, null) as IEnumerable;
            if (typedObjects == null)
                return result;
            foreach (var raw in typedObjects)
            {
                if (raw is Component component && !result.Contains(component))
                {
                    result.Add(component);
                    continue;
                }
                var gameObject = ExtractGameObject(raw);
                var typedComponent = GetExistingComponentByManagedType(gameObject, laptopType);
                if (typedComponent != null && !result.Contains(typedComponent))
                    result.Add(typedComponent);
            }
        }
        catch (Exception ex)
        {
            if (instance != null)
                instance.LogOnce("MissionLaptop discovery failed: " + ex.GetType().Name + ": " + ex.Message, true);
        }
        return result;
    }

    private static Type ResolveMissionLaptopType()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType("MissionLaptop", false, false) ??
                       assembly.GetType("Il2Cpp.MissionLaptop", false, false);
            if (type != null)
                return type;
        }
        return null;
    }

    private static Component GetExistingComponentByManagedType(GameObject gameObject, Type componentType)
    {
        if (gameObject == null || componentType == null)
            return null;
        foreach (var method in typeof(GameObject).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.Name == "GetComponent" && method.IsGenericMethodDefinition &&
                method.GetGenericArguments().Length == 1 && method.GetParameters().Length == 0)
            {
                return method.MakeGenericMethod(componentType).Invoke(gameObject, null) as Component;
            }
        }
        return null;
    }

    private static GameObject ExtractGameObject(object value)
    {
        if (value is GameObject gameObject)
            return gameObject;
        if (value is Component component)
            return component.gameObject;
        if (value == null)
            return null;
        try
        {
            var type = value.GetType();
            var property = type.GetProperty("gameObject", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                           type.GetProperty("GameObject", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return property?.GetValue(value) as GameObject;
        }
        catch { return null; }
    }

    private bool TryAttachNativeTab(MissionLaptop laptop)
    {
        try
        {
            int laptopId = laptop == null ? 0 : laptop.GetInstanceID();
            Transform operationSelection = laptop.ActiveOperationsTab == null
                ? null
                : laptop.ActiveOperationsTab.transform.parent;
            GameObject sourceButton = FindNativeActiveOperationsButton(operationSelection);
            if (sourceButton == null || sourceButton.transform.parent == null)
            {
                log.LogWarning("Cerberus native tab fix could not find the in-panel ACTIVE OPERATIONS BUTTON.");
                return false;
            }

            // Every MissionLaptop receives an infrastructure-owned page. Map
            // packages remain data-only and never create or mutate laptop UI.
            GameObject page = GetOrCreateCatalogPage(laptop, operationSelection);
            if (page == null)
            {
                LogOnce("waiting for same-owner Modded Operations page under laptopId=" +
                    laptopId + ", parent=" + HierarchyPath(operationSelection) + ".", false);
                return false;
            }

            Transform parent = sourceButton.transform.parent;
            GameObject oldRaw = FindDeep(parent, "MODDED_OPS_TAB_BUTTON");
            if (oldRaw != null)
                Object.Destroy(oldRaw);
            GameObject oldNative = FindDeep(parent, "MODDED_OPS_TAB");
            if (oldNative != null)
                Object.Destroy(oldNative);
            GameObject simulationButton = FindNativeSimulationOperationsButton(operationSelection);
            GameObject duplicate = FindDeep(parent, "MODDED_OPS_NATIVE_TAB");
            if (duplicate != null)
            {
                if (!BuildNativeModdedPresentation(laptop, page, sourceButton))
                    return false;
                SetButtonText(duplicate, "MODDED OPERATIONS");
                RegisterNativePresentationIsolation(laptop, page, duplicate, sourceButton,
                    simulationButton);
                log.LogInfo("Cerberus native tab already present; rebuilt or retained its native content.");
                return true;
            }

            var pageRect = page.GetComponent<RectTransform>();
            if (pageRect != null)
            {
                CopyContentRect(laptop.ActiveOperationsTab, pageRect);
                int pageIndex = laptop.ActiveOperationsTab == null
                    ? parent.childCount - 1
                    : laptop.ActiveOperationsTab.transform.GetSiblingIndex();
                page.transform.SetSiblingIndex(Mathf.Clamp(pageIndex, 0, page.transform.parent.childCount - 1));
            }
            // Operation rows can be spawned shortly after the MissionLaptop
            // itself appears. Leave the official two-tab geometry untouched
            // and retry on the next one-second tick until a shipped row is
            // available to serve as the visual template.
            if (!BuildNativeModdedPresentation(laptop, page, sourceButton))
                return false;

            duplicate = Object.Instantiate(sourceButton, parent);
            duplicate.name = "MODDED_OPS_NATIVE_TAB";
            duplicate.SetActive(true);
            SetButtonText(duplicate, "MODDED OPERATIONS");

            PositionAsThirdTab(parent, sourceButton, simulationButton, duplicate);
            FitTabTitleText(sourceButton, "ACTIVE OPERATIONS");
            FitTabTitleText(simulationButton, "OPERATION SIMULATION");
            FitTabTitleText(duplicate, "MODDED OPERATIONS");
            page.SetActive(false);
            Animator pageAnimator = page.GetComponent<Animator>();
            if (pageAnimator == null)
                pageAnimator = page.AddComponent<Animator>();
            var clonedPanelButton = duplicate.GetComponent<PanelButton>();
            var clonedButtonManager = duplicate.GetComponent<ButtonManager>();

            UnityEvent clickEvent = null;
            if (clonedPanelButton != null)
            {
                // Instantiation can retain persistent listeners pointing at the Active Operations page.
                // Replace the clone's event object so the new tab has an independent native click route.
                clonedPanelButton.onClick = new UnityEvent();
                clickEvent = clonedPanelButton.onClick;
            }
            else if (clonedButtonManager != null)
            {
                clonedButtonManager.onClick = new UnityEvent();
                clickEvent = clonedButtonManager.onClick;
            }
            // PanelButton/ButtonManager already are the shipped pointer-event
            // surfaces and own the normal/highlight/press/select animation.
            // Preserve them. Bind an existing Unity Button only when the
            // shipped hierarchy actually contains one; never add an invisible
            // overlay or a second Selectable to the cloned native tab.
            Button unityButton = null;
            try { unityButton = duplicate.GetComponent<Button>(); }
            catch { unityButton = null; }
            if (unityButton == null)
            {
                try { unityButton = duplicate.GetComponentInChildren<Button>(true); }
                catch { unityButton = null; }
            }
            if (clickEvent == null && unityButton == null)
            {
                log.LogWarning("Cerberus native tab clone had no DreamOS click event.");
                return false;
            }
            int lastLogicalOpenFrame = -1;
            Action<string> openModdedPage = eventSource =>
            {
                try
                {
                    int frame = Time.frameCount;
                    if (frame == lastLogicalOpenFrame)
                    {
                        log.LogInfo("Cerberus native MODDED OPS duplicate click surface suppressed: laptopId=" +
                            laptopId + ", source=" + eventSource + ", frame=" + frame + ".");
                        return;
                    }
                    lastLogicalOpenFrame = frame;
                    log.LogInfo(CaptureLaptopTransitionState(laptop, page,
                        "before MODDED OPS", eventSource, frame));
                    SetTabSelectedState(sourceButton, false);
                    SetTabSelectedState(simulationButton, false);
                    SetButtonText(duplicate, "MODDED OPERATIONS");
                    SetTabSelectedState(duplicate, true);
                    OpenModdedPage(laptop, page);
                    MarkNativeModdedPageOpened(laptopId, frame);
                    log.LogInfo("Cerberus native MODDED OPS logical transition: laptopId=" + laptopId +
                        ", source=" + eventSource + ", frame=" + frame +
                        ", pageActiveSelf=" + page.activeSelf +
                        ", pageActiveInHierarchy=" + page.activeInHierarchy +
                        ", ownerCanvas=" + DescribeActivity(laptop.osCanvas) +
                        ", pageParent=" + HierarchyPath(page.transform.parent) + ".");
                    QueueTransitionSnapshot(laptop, page, "after MODDED OPS via " + eventSource, frame);
                }
                catch (Exception ex)
                {
                    log.LogWarning("Cerberus native MODDED OPS click failed: " + ex.GetType().Name + ": " + ex.Message);
                }
            };
            if (clickEvent != null)
            {
                clickEvent.RemoveAllListeners();
                clickEvent.AddListener((UnityAction)(() => openModdedPage("DreamOS.PanelButton")));
            }
            if (unityButton != null)
            {
                unityButton.onClick.RemoveAllListeners();
                unityButton.onClick.AddListener((UnityAction)(() => openModdedPage("UnityEngine.UI.Button")));
                unityButton.interactable = true;
            }
            if (clonedPanelButton != null)
            {
                clonedPanelButton.isInteractable = true;
                // UpdateUI is cosmetic; its IL2CPP wrapper is stripped in some
                // retail builds, so leave the cloned native state intact rather
                // than making the whole tab attach fail.
            }

            // Do not append listeners to either shipped tab. A lightweight
            // read-only selected-state guard hides only the mod-owned overlay
            // when DreamOS selects an official tab, leaving every vanilla
            // callback and double-click route exactly as shipped.
            RegisterNativePresentationIsolation(laptop, page, duplicate, sourceButton,
                simulationButton);

            int graphicCount = 0;
            try { graphicCount = duplicate.GetComponentsInChildren<Graphic>(true).Length; } catch { }
            log.LogInfo("Cerberus native MODDED OPS tab inserted as third native Operation Selection tab; source=" + sourceButton.name + ", simulation=" + (simulationButton == null ? "null" : simulationButton.name) + ", visible=" + duplicate.activeInHierarchy + ", unityButton=" + (unityButton != null) + ", targetGraphic=" + (unityButton == null || unityButton.targetGraphic == null ? "null" : unityButton.targetGraphic.gameObject.name) + ", graphics=" + graphicCount + ", rect=" + RectSummary(duplicate) + ".");
            return true;
        }
        catch (Exception ex)
        {
            log.LogWarning("Cerberus native tab fix failed: " + ex.ToString());
            return false;
        }
    }

    private void RegisterNativePresentationIsolation(MissionLaptop laptop, GameObject page,
        GameObject moddedButton, GameObject activeButton, GameObject simulationButton)
    {
        if (laptop == null || page == null || moddedButton == null)
            return;
        int laptopId = laptop.GetInstanceID();
        foreach (var binding in nativePresentationBindings)
        {
            if (binding.LaptopId != laptopId)
                continue;
            binding.Laptop = laptop;
            binding.Page = page;
            binding.PreparationPanel = FindNativeModdedPreparationPanel(laptop);
            binding.ModdedButton = moddedButton;
            binding.ActiveButton = activeButton;
            binding.SimulationButton = simulationButton;
            return;
        }
        nativePresentationBindings.Add(new NativePresentationBinding
        {
            LaptopId = laptopId,
            Laptop = laptop,
            Page = page,
            PreparationPanel = FindNativeModdedPreparationPanel(laptop),
            ModdedButton = moddedButton,
            ActiveButton = activeButton,
            SimulationButton = simulationButton,
        });
    }

    private void MarkNativeModdedPageOpened(int laptopId, int frame)
    {
        foreach (var binding in nativePresentationBindings)
        {
            if (binding.LaptopId == laptopId)
            {
                binding.LastModdedOpenFrame = frame;
                return;
            }
        }
    }

    private void MaintainNativePresentationIsolation()
    {
        for (int index = nativePresentationBindings.Count - 1; index >= 0; index--)
        {
            var binding = nativePresentationBindings[index];
            if (binding == null || binding.Laptop == null || binding.Page == null ||
                binding.ModdedButton == null)
            {
                nativePresentationBindings.RemoveAt(index);
                continue;
            }
            // On this retail build the two inner operation tabs do not always
            // publish PanelButton.isSelected even though their untouched click
            // route has activated the corresponding official page. Treat the
            // official page's own activeSelf as the authoritative transition
            // signal, with isSelected retained as a compatibility fallback.
            bool officialPageActive =
                (binding.Laptop.ActiveOperationsTab != null && binding.Laptop.ActiveOperationsTab.activeSelf) ||
                (binding.Laptop.SimulationOperationsTab != null && binding.Laptop.SimulationOperationsTab.activeSelf);
            bool officialSelected = officialPageActive ||
                IsNativePanelButtonSelected(binding.ActiveButton) ||
                IsNativePanelButtonSelected(binding.SimulationButton);
            if (!binding.Page.activeSelf || !officialSelected ||
                Time.frameCount <= binding.LastModdedOpenFrame + 1)
                continue;

            // Only mod-owned objects are changed here. The official PanelButton
            // has already performed its untouched retail transition underneath.
            binding.Page.SetActive(false);
            if (binding.PreparationPanel != null)
                binding.PreparationPanel.SetActive(false);
            SetTabSelectedState(binding.ModdedButton, false);
            log.LogInfo("Cerberus isolated modded overlay closed after an official tab became selected; laptopId=" +
                binding.LaptopId + ", officialPageActive=" + officialPageActive + ".");
        }
    }

    private static bool IsNativePanelButtonSelected(GameObject button)
    {
        if (button == null)
            return false;
        try
        {
            var panelButton = button.GetComponent<PanelButton>();
            return panelButton != null && panelButton.isSelected;
        }
        catch { return false; }
    }

    private static GameObject FindNativeModdedPreparationPanel(MissionLaptop laptop)
    {
        if (laptop == null || laptop.opBoardParent == null)
            return null;
        Transform officialPanel = laptop.opBoardParent.parent;
        Transform mainContent = officialPanel == null ? null : officialPanel.parent;
        return FindDeep(mainContent ?? officialPanel ?? laptop.opBoardParent,
            "MODDED_NATIVE_OPERATION_PREPARATION");
    }

    private static GameObject GetOrCreateCatalogPage(
        MissionLaptop laptop,
        Transform operationSelection)
    {
        if (laptop == null || operationSelection == null ||
            laptop.ActiveOperationsTab == null)
        {
            return null;
        }

        var existing = FindChild(operationSelection, "MODDED_OPERATIONS_PAGE");
        if (existing != null)
            return existing;

        var page = new GameObject("MODDED_OPERATIONS_PAGE");
        var rect = page.AddComponent<RectTransform>();
        page.transform.SetParent(operationSelection, false);
        CopyContentRect(laptop.ActiveOperationsTab, rect);
        page.transform.SetSiblingIndex(laptop.ActiveOperationsTab.transform.GetSiblingIndex());
        page.SetActive(false);
        return page;
    }

    private bool BuildNativeModdedPresentation(MissionLaptop laptop, GameObject page,
        GameObject nativeButtonTemplate)
    {
        if (laptop == null || page == null || laptop.ActiveOperationsTab == null)
            return false;

        int laptopId = laptop.GetInstanceID();
        bool catalogIsEmpty = OperatorApi.ModdedOperations.Operations.Count == 0;
        if (catalogPresentations.TryGetValue(laptopId, out var existing) &&
            existing != null && existing.Page == page && existing.HomeShell != null &&
            (catalogIsEmpty || existing.PreparationPanel != null))
        {
            return true;
        }
        if (existing != null)
        {
            if (existing.HomeShell != null)
                Object.Destroy(existing.HomeShell);
            if (existing.PreparationPanel != null)
                Object.Destroy(existing.PreparationPanel);
            if (existing.NativeBoardData != null)
                Object.Destroy(existing.NativeBoardData);
            if (existing.NativeTargetData != null)
                Object.Destroy(existing.NativeTargetData);
            if (existing.NativeInfiltrationMapPrefab != null)
                Object.Destroy(existing.NativeInfiltrationMapPrefab);
        }
        catalogPresentations.Remove(laptopId);

        var existingHomeShell = FindDeep(page.transform, "MODDED_NATIVE_HOME");
        var existingBriefingShell = FindNativeModdedPreparationPanel(laptop);
        if (existingHomeShell != null)
            Object.Destroy(existingHomeShell);
        if (existingBriefingShell != null)
            Object.Destroy(existingBriefingShell);

        var activeList = ExtractGameObject(ReadMember(laptop, "ActiveOperationsList"));
        var simulationList = ExtractGameObject(ReadMember(laptop, "SimulationOperationList"));
        var rowTemplate = FindNativeOperationRowTemplate(activeList == null ? null : activeList.transform) ??
            FindNativeOperationRowTemplate(simulationList == null ? null : simulationList.transform);
        if (activeList == null || rowTemplate == null)
        {
            log.LogWarning("Cerberus native Modded Ops presentation skipped because no shipped operation-row visual was available.");
            return false;
        }

        var relativeListPath = BuildRelativeChildIndexPath(
            laptop.ActiveOperationsTab.transform, activeList.transform);
        if (relativeListPath == null)
        {
            log.LogWarning("Cerberus native Modded Ops presentation skipped because the active-list path was outside its page.");
            return false;
        }

        var presentation = new CatalogPresentation
        {
            Laptop = laptop,
            Page = page
        };
        if (catalogIsEmpty)
        {
            presentation.HomeShell = CreateEmptyCatalogShell(
                page.transform,
                laptop.ActiveOperationsTab,
                relativeListPath,
                out var emptyBriefing);
            presentation.HomeBriefing = emptyBriefing;
            if (presentation.HomeShell == null)
                return false;
            catalogPresentations[laptopId] = presentation;
            deferredSetupLoggedLaptops.Remove(laptopId);
            Canvas.ForceUpdateCanvases();
            log.LogInfo("Modded Operations framework attached with an empty catalog; " +
                "install one or more valid data-only map packages under " +
                "BepInEx\\OperatorMods.");
            return true;
        }
        presentation.SelectedOperation = OperatorApi.ModdedOperations.Operations[0];
        presentation.SelectedTimeCode = presentation.SelectedOperation.DefaultTimeCode;
        var briefingShell = CreateCatalogOperationBoardShell(
            laptop,
            presentation);
        presentation.PreparationPanel = briefingShell;
        GameObject homeShell = null;
        TMP_Text homeBriefing = null;
        if (briefingShell != null)
        {
            homeShell = CreateCatalogOperationShell(
                page.transform,
                laptop.ActiveOperationsTab,
                relativeListPath,
                rowTemplate,
                presentation,
                out homeBriefing);
            presentation.HomeShell = homeShell;
            presentation.HomeBriefing = homeBriefing;
        }
        if (homeShell == null || briefingShell == null)
        {
            if (homeShell != null)
                Object.Destroy(homeShell);
            if (briefingShell != null)
                Object.Destroy(briefingShell);
            if (deferredSetupLoggedLaptops.Add(laptopId))
            {
                log.LogInfo("Modded Operations deferred one hidden laptop replica " +
                    "until its shipped list and operation-information surfaces are ready; " +
                    "laptopId=" + laptopId + ".");
            }
            return false;
        }

        var pageImage = page.GetComponent<Image>();
        if (pageImage != null)
        {
            pageImage.color = new Color(pageImage.color.r, pageImage.color.g, pageImage.color.b, 0f);
            pageImage.raycastTarget = false;
        }

        briefingShell.SetActive(false);
        catalogPresentations[laptopId] = presentation;

        Canvas.ForceUpdateCanvases();
        log.LogInfo("Modded Operations presentation built from shipped operation-row and OperationBoardUI visuals; " +
            "laptopId=" + laptop.GetInstanceID() + ", rows=" +
            OperatorApi.ModdedOperations.Operations.Count + ", catalog=" +
            OperatorApi.ModdedOperations.CatalogId + ".");
        return true;
    }

    private static GameObject CreateEmptyCatalogShell(
        Transform parent,
        GameObject nativePageTemplate,
        int[] relativeListPath,
        out TMP_Text briefing)
    {
        briefing = null;
        if (parent == null || nativePageTemplate == null || relativeListPath == null)
            return null;
        var shell = Object.Instantiate(nativePageTemplate, parent);
        if (shell == null)
            return null;
        shell.name = "MODDED_NATIVE_HOME";
        SetFullStretch(shell.GetComponent<RectTransform>());
        shell.SetActive(true);
        RewriteClonedPageHeading(shell);
        var clonedList = FollowRelativeChildIndexPath(shell.transform, relativeListPath);
        if (clonedList == null)
        {
            Object.Destroy(shell);
            return null;
        }
        SetChildrenActive(clonedList, false);
        briefing = FindNativeBriefingText(shell);
        if (briefing != null)
        {
            DisableLocalizationComponent(briefing.gameObject);
            if (briefing.transform.parent != null)
                DisableLocalizationComponent(briefing.transform.parent.gameObject);
            briefing.text = "NO MODDED OPERATIONS INSTALLED\n\n" +
                "Install a valid map package in BepInEx\\OperatorMods, then restart OPERATOR.";
            briefing.enableWordWrapping = true;
        }
        foreach (var selectionUi in shell.GetComponentsInChildren<OperationSelectionUI>(true))
        {
            if (selectionUi == null)
                continue;
            selectionUi.enabled = false;
            Object.Destroy(selectionUi);
        }
        return shell;
    }

    private GameObject CreateCatalogOperationShell(
        Transform parent,
        GameObject nativePageTemplate,
        int[] relativeListPath,
        GameObject rowTemplate,
        CatalogPresentation presentation,
        out TMP_Text briefing)
    {
        briefing = null;
        if (parent == null || nativePageTemplate == null || rowTemplate == null ||
            presentation == null)
        {
            return null;
        }

        var shell = Object.Instantiate(nativePageTemplate, parent);
        if (shell == null)
            return null;
        shell.name = "MODDED_NATIVE_HOME";
        SetFullStretch(shell.GetComponent<RectTransform>());
        shell.SetActive(true);
        RewriteClonedPageHeading(shell);

        var clonedList = FollowRelativeChildIndexPath(shell.transform, relativeListPath);
        if (clonedList == null)
        {
            Object.Destroy(shell);
            return null;
        }
        SetChildrenActive(clonedList, false);

        var rows = new List<GameObject>();
        int index = 0;
        foreach (var operation in OperatorApi.ModdedOperations.Operations)
        {
            var row = Object.Instantiate(rowTemplate, clonedList);
            if (row == null)
                continue;
            row.name = "MODDED_NATIVE_ROW_" + index.ToString("D3");
            row.SetActive(true);
            string mode = operation.Mode == ModdedOperationMode.PlayerVersusEnvironment
                ? "PVE"
                : "PVP";
            string threat = operation.Mode == ModdedOperationMode.PlayerVersusEnvironment
                ? "HIGH"
                : "VARIABLE";
            RewriteNativeOperationRow(
                row,
                operation.DisplayName,
                "30-45 MIN",
                threat,
                mode,
                operation.AreaOfOperation);
            rows.Add(row);
            var captured = operation;
            RebindNativeRow(
                row,
                () => SelectCatalogOperation(presentation, captured, false),
                () => SelectCatalogOperation(presentation, captured, true));
            index++;
        }

        if (rows.Count == 0)
        {
            Object.Destroy(shell);
            return null;
        }
        if (clonedList.GetComponent<VerticalLayoutGroup>() == null)
            StackNativeRows(rows);

        briefing = FindNativeBriefingText(shell, rows.ToArray());
        if (briefing != null)
        {
            DisableLocalizationComponent(briefing.gameObject);
            if (briefing.transform.parent != null)
                DisableLocalizationComponent(briefing.transform.parent.gameObject);
            briefing.text = FormatCatalogBriefing(presentation.SelectedOperation);
            briefing.enableWordWrapping = true;
        }

        foreach (var selectionUi in shell.GetComponentsInChildren<OperationSelectionUI>(true))
        {
            if (selectionUi == null)
                continue;
            selectionUi.enabled = false;
            Object.Destroy(selectionUi);
        }
        return shell;
    }

    private void SelectCatalogOperation(
        CatalogPresentation presentation,
        ModdedOperationDefinition operation,
        bool openPreparation)
    {
        if (presentation == null || operation == null)
            return;
        presentation.SelectedOperation = operation;
        presentation.SelectedTimeCode = operation.DefaultTimeCode;
        if (presentation.HomeBriefing != null)
            presentation.HomeBriefing.text = FormatCatalogBriefing(operation);
        UpdateCatalogOperationBoard(presentation);
        BeginSelectedMapPrefetch(operation);
        log.LogInfo("Modded Operations row " +
            (openPreparation ? "double-click" : "single-click") +
            ": operation=" + operation.Id + ".");
        if (openPreparation)
        {
            OpenNativeOperationPreparation(
                presentation.Laptop,
                presentation.PreparationPanel,
                null);
        }
    }

    private void BeginSelectedMapPrefetch(ModdedOperationDefinition operation)
    {
        if (operation == null ||
            !OperatorApi.ModdedOperations.TryGetMap(operation.MapId, out var map) ||
            map == null)
        {
            return;
        }

        if (loadedMapBundles.TryGetValue(map.Id, out var loaded) &&
            loaded != null && loaded.SceneBundle != null && loaded.Map != null &&
            string.Equals(
                loaded.Map.PackageContentId,
                map.PackageContentId,
                StringComparison.Ordinal))
        {
            return;
        }
        if (loaded != null)
        {
            try { loaded.SceneBundle?.Unload(false); } catch { }
            foreach (var dependency in loaded.Dependencies)
            {
                try { dependency?.Unload(false); } catch { }
            }
            loadedMapBundles.Remove(map.Id);
            log.LogWarning("Modded Operations discarded an incomplete or stale " +
                "bundle cache entry before selected-map prefetch: map=" +
                map.Id + ".");
        }

        if (pendingLaunch != null)
        {
            if (string.Equals(pendingLaunch.Map?.Id, map.Id, StringComparison.Ordinal) &&
                string.Equals(
                    pendingLaunch.Map?.PackageContentId,
                    map.PackageContentId,
                    StringComparison.Ordinal))
            {
                return;
            }

            // Unity does not provide a safe cancellation primitive for an active
            // AssetBundleCreateRequest. Finish the one selected-map prefetch and
            // do not start an unbounded set of speculative map loads.
            log.LogInfo("Modded Operations deferred selected-map prefetch for " +
                map.Id + " while " + pendingLaunch.Map?.Id + " is loading.");
            return;
        }

        var loading = new LoadedMapBundles { Map = map };
        var pending = new PendingMapLaunch
        {
            Map = map,
            LoadingBundles = loading,
            LoadStartedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp()
        };
        foreach (string path in map.DependencyBundlePaths)
            pending.BundlePaths.Add(path);
        pending.BundlePaths.Add(map.SceneBundlePath);
        pendingLaunch = pending;
        log.LogInfo("Modded Operations began selected-map bundle prefetch: map=" +
            map.Id + ", bundles=" + pending.BundlePaths.Count +
            ", bytes=" + GetBundleByteTotal(pending.BundlePaths) + ".");
    }

    private static string FormatCatalogBriefing(ModdedOperationDefinition operation)
    {
        if (operation == null)
            return string.Empty;
        return operation.DisplayName + "\n\nAREA OF OPERATION // " +
            operation.AreaOfOperation + "\n\n" + operation.Sitrep;
    }

    private GameObject CreateCatalogOperationBoardShell(
        MissionLaptop laptop,
        CatalogPresentation presentation)
    {
        GameObject boardVisualTemplate = ResolveNativeOperationBoardVisualTemplate();
        if (laptop == null || laptop.opBoardParent == null ||
            presentation == null || boardVisualTemplate == null)
        {
            return null;
        }

        Transform officialContent = laptop.opBoardParent;
        Transform officialPanel = officialContent.parent;
        Transform mainContent = officialPanel == null ? null : officialPanel.parent;
        if (officialPanel == null || mainContent == null)
            return null;
        var contentPath = BuildRelativeChildIndexPath(officialPanel, officialContent);
        if (contentPath == null)
            return null;

        var preparationPanel = Object.Instantiate(officialPanel.gameObject, mainContent);
        if (preparationPanel == null)
            return null;
        preparationPanel.name = "MODDED_NATIVE_OPERATION_PREPARATION";
        preparationPanel.SetActive(true);
        var privateContent = FollowRelativeChildIndexPath(
            preparationPanel.transform,
            contentPath);
        if (privateContent == null)
        {
            Object.Destroy(preparationPanel);
            return null;
        }
        SetChildrenActive(privateContent, false);

        var shell = Object.Instantiate(boardVisualTemplate, privateContent);
        if (shell == null)
        {
            Object.Destroy(preparationPanel);
            return null;
        }
        shell.name = "MODDED_NATIVE_OPERATION_INFORMATION";
        SetFullStretch(shell.GetComponent<RectTransform>());
        shell.SetActive(false);

        var board = shell.GetComponent<OperationBoardUI>() ??
            shell.GetComponentInChildren<OperationBoardUI>(true);
        if (board == null || board.SituationReportText == null ||
            board.SituationReportText.TMP == null ||
            board.ExecuteOperationButton == null || board.ConfirmationWindow == null ||
            board.InfilTimeSlider == null || board.PrearationTimeSlider == null)
        {
            Object.Destroy(preparationPanel);
            return null;
        }

        CerebusOpboard nativeBoardData = null;
        try
        {
            nativeBoardData = ScriptableObject.CreateInstance<CerebusOpboard>();
            nativeBoardData.name = "MODDED_OPERATIONS_PRIVATE_OPBOARD_DATA";
            var privateTarget = ScriptableObject.CreateInstance<CerebusTargetPackage>();
            if (nativeBoardData == null || privateTarget == null)
                throw new InvalidOperationException(
                    "package-owned operation data could not be allocated");
            privateTarget.name = "MODDED_OPERATIONS_PRIVATE_TARGET_PACKAGE";
            privateTarget.TargetPackage = nativeBoardData;
            privateTarget.TargetPackageIndex = 0;
            privateTarget.isLocked = false;
            privateTarget.isSimulation = false;
            privateTarget.isRegion = false;
            privateTarget.MissionRequiredForUnlock = null;
            privateTarget.unlockCode = string.Empty;
            privateTarget.progressionUnlockCode = string.Empty;
            privateTarget.OperationType = new UnityEngine.Localization.LocalizedString();
            privateTarget.EnemyCount = new UnityEngine.Localization.LocalizedString();
            privateTarget.OperationDescription =
                new UnityEngine.Localization.LocalizedString();
            nativeBoardData.ThisMissionTargetPackage = privateTarget;
            presentation.NativeTargetData = privateTarget;
            nativeBoardData.MapPrefab = null;
            nativeBoardData.SituationReport =
                new UnityEngine.Localization.LocalizedString();
            nativeBoardData.requireEnoughExfils = false;
            nativeBoardData.requireEnoughInfils = false;
            nativeBoardData.isLocked = false;
            nativeBoardData.unlockCode = string.Empty;
            nativeBoardData.exfilUnlockingKey = string.Empty;
            nativeBoardData.TargetRaidTime = 0f;
            nativeBoardData.AchievementName = string.Empty;
            nativeBoardData.achievement_min_ai_amount = 0;
            nativeBoardData.INFILTRATION_TARGET = string.Empty;
            nativeBoardData.INFILTRATION_TIME = string.Empty;
            nativeBoardData.SelectedInfiltrationTime = 0;
            nativeBoardData.PrepTimeInSeconds = 5;
            nativeBoardData.isCompleted = false;
            nativeBoardData.UnlockOnExfil = string.Empty;
            nativeBoardData.HVTSpawnIsRandom = false;
            nativeBoardData.isSimulation = false;
            nativeBoardData.missionLaptop = laptop;
            nativeBoardData.OperationBoardUI = board;
            // Setup dereferences retail mission state. Replace both data
            // pointers directly before this visual is activated.
            board.CerebusOpboard = nativeBoardData;
            board.Laptop = laptop;
            deferredSetupLoggedLaptops.Remove(laptop.GetInstanceID());
        }
        catch (Exception ex)
        {
            int laptopId = laptop.GetInstanceID();
            if (deferredSetupLoggedLaptops.Add(laptopId))
            {
                log.LogInfo("Modded Operations deferred one hidden laptop replica " +
                    "until its shipped OperationBoardUI ownership graph is ready; " +
                    "laptopId=" + laptopId + ", reason=" + ex.GetType().Name + ".");
            }
            if (nativeBoardData != null)
                Object.Destroy(nativeBoardData);
            if (presentation.NativeTargetData != null)
                Object.Destroy(presentation.NativeTargetData);
            presentation.NativeTargetData = null;
            if (presentation.NativeInfiltrationMapPrefab != null)
                Object.Destroy(presentation.NativeInfiltrationMapPrefab);
            presentation.NativeInfiltrationMapPrefab = null;
            Object.Destroy(preparationPanel);
            return null;
        }

        presentation.Board = board;
        presentation.NativeBoardData = nativeBoardData;
        presentation.SituationReport = board.SituationReportText.TMP;
        presentation.SituationReport.gameObject.name = "MODDED_NATIVE_SITREP";
        DisableLocalizationComponent(board.SituationReportText.gameObject);
        DisableLocalizationComponent(presentation.SituationReport.gameObject);
        if (presentation.SituationReport.transform.parent != null)
            DisableLocalizationComponent(
                presentation.SituationReport.transform.parent.gameObject);
        presentation.SituationReport.enableWordWrapping = true;

        SetActiveSafe(board.SimulationButton, false);
        SetActiveSafe(board.ActiveOperationButton, false);
        SetActiveSafe(board.NotEnoughInfilsButton, false);
        SetActiveSafe(board.NotEnoughExfilsButton, false);
        SetNativeMapFullscreen(board, false);
        SetActiveSafe(board.AbortOperationButton, false);
        SetActiveSafe(board.ExecuteOperationButton, true);
        SetActiveSafe(board.InfiltrationTargetParent, false);
        SetComponentActiveSafe(board.InfilTargetSlider, false);
        SetComponentActiveSafe(board.InfilTimeSlider, true);
        SetComponentActiveSafe(board.PrearationTimeSlider, true);
        SetComponentActiveSafe(board.OpforCountSlider, false);
        SetComponentActiveSafe(board.EnemyCountSlider, false);
        SetComponentActiveSafe(board.OpforDifficultySlider, false);
        SetComponentActiveSafe(board.HVT_OpforCountSlider, false);
        SetComponentActiveSafe(board.HVTEnemyCountSlider, false);
        SetComponentActiveSafe(board.HVTOpforDifficultySlider, false);
        SetComponentActiveSafe(board.HVTCountSlider, false);
        SetComponentActiveSafe(board.HVTDifficultySlider, false);
        SetGameObjectsActive(board.SimulationParameters, false);
        SetGameObjectsActive(board.HVTSimulationParameters, false);
        SetGameObjectsActive(board.PVPParameters, false);
        SetGameObjectsActive(board.FFAParameters, false);
        ConfigureNativeSelector(
            board.PrearationTimeSlider,
            new[] { "5 SECONDS" },
            null);

        var confirmation = board.ConfirmationWindow;
        confirmation.useLocalization = false;
        confirmation.useCustomContent = true;
        confirmation.titleText = "Start Operation";
        confirmation.descriptionText =
            "Are you sure you want to start this operation?";
        confirmation.showConfirmButton = true;
        confirmation.showCancelButton = true;
        confirmation.closeOnConfirm = true;
        confirmation.closeOnCancel = true;
        confirmation.onOpen = new UnityEvent();
        confirmation.onClose = new UnityEvent();
        confirmation.onConfirm = new UnityEvent();
        confirmation.onConfirm.AddListener((UnityAction)(() =>
            BeginCatalogOperationLaunch(presentation)));
        confirmation.onCancel = new UnityEvent();
        confirmation.onCancel.AddListener((UnityAction)(() =>
            CloseNativeMapConfirmation(board, true)));
        if (confirmation.windowTitle != null)
        {
            DisableLocalizationComponent(confirmation.windowTitle.gameObject);
            confirmation.windowTitle.text = confirmation.titleText;
        }
        if (confirmation.windowDescription != null)
        {
            DisableLocalizationComponent(confirmation.windowDescription.gameObject);
            confirmation.windowDescription.text = confirmation.descriptionText;
        }
        try { confirmation.UpdateUI(); } catch { }

        if (!ReplaceNativeButtonAction(
                confirmation.confirmButton,
                () => BeginCatalogOperationLaunch(presentation)) ||
            !ReplaceNativeButtonAction(
                confirmation.cancelButton,
                () => confirmation.onCancel.Invoke()) ||
            !ReplaceNativeButtonAction(
                board.ExecuteOperationButton,
                () => confirmation.OpenWindow()))
        {
            Object.Destroy(nativeBoardData);
            Object.Destroy(presentation.NativeTargetData);
            Object.Destroy(presentation.NativeInfiltrationMapPrefab);
            presentation.NativeBoardData = null;
            presentation.NativeTargetData = null;
            presentation.NativeInfiltrationMapPrefab = null;
            Object.Destroy(preparationPanel);
            return null;
        }

        RebindNativeFullscreenControls(board, preparationPanel);
        BindNativePreparationBack(laptop, preparationPanel, null, false);
        UpdateCatalogOperationBoard(presentation);
        shell.SetActive(true);
        preparationPanel.SetActive(false);
        return preparationPanel;
    }

    private GameObject ResolveNativeOperationBoardVisualTemplate()
    {
        if (operationBoardVisualTemplate != null)
            return operationBoardVisualTemplate;

        if (OperationsManager.singleton != null &&
            OperationsManager.singleton.OperationBoardUIPrefab != null)
        {
            operationBoardVisualTemplate =
                OperationsManager.singleton.OperationBoardUIPrefab;
            return operationBoardVisualTemplate;
        }

        foreach (var manager in Resources.FindObjectsOfTypeAll<OperationsManager>())
        {
            if (manager == null || manager.OperationBoardUIPrefab == null)
                continue;
            operationBoardVisualTemplate = manager.OperationBoardUIPrefab;
            return operationBoardVisualTemplate;
        }

        foreach (var board in Resources.FindObjectsOfTypeAll<OperationBoardUI>())
        {
            if (board == null || board.gameObject == null ||
                board.gameObject.name.StartsWith(
                    "MODDED_NATIVE_", StringComparison.Ordinal) ||
                board.SituationReportText == null ||
                board.SituationReportText.TMP == null ||
                board.ExecuteOperationButton == null ||
                board.ConfirmationWindow == null ||
                board.InfilTimeSlider == null ||
                board.PrearationTimeSlider == null)
            {
                continue;
            }
            operationBoardVisualTemplate = board.gameObject;
            return operationBoardVisualTemplate;
        }
        return null;
    }

    private void UpdateCatalogOperationBoard(CatalogPresentation presentation)
    {
        if (presentation == null || presentation.SelectedOperation == null ||
            presentation.Board == null || presentation.NativeBoardData == null)
        {
            return;
        }

        var operation = presentation.SelectedOperation;
        if (!OperatorApi.ModdedOperations.TryGetMap(operation.MapId, out var map) ||
            map == null)
        {
            return;
        }

        if (!operation.SupportedTimeCodes.Contains(
                presentation.SelectedTimeCode,
                StringComparer.Ordinal))
        {
            presentation.SelectedTimeCode = operation.DefaultTimeCode;
        }

        if (presentation.SituationReport != null)
            presentation.SituationReport.text = FormatCatalogBriefing(operation);
        var preview = GetOrLoadPreviewSprite(map);
        if (preview != null)
        {
            ReplaceNativeMapPreview(
                presentation.Board.MapParent,
                preview,
                "MODDED_NATIVE_MAP_PREVIEW");
            ReplaceNativeMapPreview(
                presentation.Board.FullscreenMapParent,
                preview,
                "MODDED_NATIVE_MAP_FULLSCREEN");
        }
        else
        {
            SetGameObjectsActive(
                CaptureDirectChildren(presentation.Board.MapParent), false);
            SetGameObjectsActive(
                CaptureDirectChildren(presentation.Board.FullscreenMapParent), false);
        }

        var available = new Il2CppStringArray(operation.Infiltrations.Count);
        var targets = new Il2CppReferenceArray<TARGETPACKAGE_DETAILS>(
            operation.SupportedTimeCodes.Count);
        int selectedIndex = 0;
        for (int index = 0; index < operation.SupportedTimeCodes.Count; index++)
        {
            string timeCode = operation.SupportedTimeCodes[index];
            var target = new TARGETPACKAGE_DETAILS
            {
                OPERATION_SCENE = map.ScenePath,
                DISPLAY_NAME = operation.DisplayName,
                INFILTRATION_TIME = timeCode
            };
            targets[index] = target;
            if (string.Equals(
                    timeCode,
                    presentation.SelectedTimeCode,
                    StringComparison.Ordinal))
            {
                selectedIndex = index;
            }
        }
        for (int index = 0; index < operation.Infiltrations.Count; index++)
            available[index] = operation.Infiltrations[index].DisplayName;

        if (presentation.NativeInfiltrationMapPrefab != null)
            Object.Destroy(presentation.NativeInfiltrationMapPrefab);
        presentation.NativeInfiltrationMapPrefab = BuildPackageInfiltrationMapPrefab(
            presentation,
            operation,
            preview);
        if (presentation.NativeInfiltrationMapPrefab == null)
        {
            log.LogError("Modded Operations refused to arm operation '" +
                operation.Id + "' because no sanitized native infiltration " +
                "selector map could be built.");
            return;
        }

        var data = presentation.NativeBoardData;
        data.requireEnoughExfils = false;
        data.requireEnoughInfils = false;
        data.AffectGamemode = true;
        data.GameModeOverride = operation.Mode ==
            ModdedOperationMode.PlayerVersusEnvironment
                ? OperationsManager.GameMode.StandardPVE
                : OperationsManager.GameMode.StandardPVP;
        if (presentation.NativeTargetData != null)
        {
            presentation.NativeTargetData.TargetPackage = data;
            presentation.NativeTargetData.OPERATION_AREA_OF_OPERATION =
                operation.AreaOfOperation;
            presentation.NativeTargetData.isRegion = false;
            presentation.NativeTargetData.isLocked = false;
            presentation.NativeTargetData.MissionRequiredForUnlock = null;
            presentation.NativeTargetData.unlockCode = string.Empty;
            presentation.NativeTargetData.progressionUnlockCode = string.Empty;
            presentation.NativeTargetData.TargetPackageIndex = 0;
            presentation.NativeTargetData.isSimulation = false;
            data.ThisMissionTargetPackage = presentation.NativeTargetData;
        }
        data.TARGETPACKAGE = targets;
        data.AvailableInfils = available;
        data.MinAI = operation.Mode == ModdedOperationMode.PlayerVersusEnvironment
            ? 8
            : 0;
        data.MaxAI = operation.Mode == ModdedOperationMode.PlayerVersusEnvironment
            ? 16
            : 0;
        data.MapPrefab = presentation.NativeInfiltrationMapPrefab;
        data.isLocked = false;
        data.unlockCode = string.Empty;
        data.exfilUnlockingKey = string.Empty;
        data.TargetRaidTime = 0f;
        data.AchievementName = string.Empty;
        data.achievement_min_ai_amount = 0;
        data.INFILTRATION_TARGET = operation.AreaOfOperation;
        data.INFILTRATION_TIME = presentation.SelectedTimeCode;
        data.SelectedInfiltrationTime = selectedIndex;
        data.PrepTimeInSeconds = 5;
        data.isCompleted = false;
        data.UnlockOnExfil = string.Empty;
        data.HVTSpawnIsRandom = false;
        data.isSimulation = false;
        data.missionLaptop = presentation.Laptop;
        data.OperationBoardUI = presentation.Board;

        var confirmation = presentation.Board.ConfirmationWindow;
        if (confirmation != null)
        {
            confirmation.useLocalization = false;
            confirmation.useCustomContent = true;
            confirmation.titleText = "Start Operation";
            confirmation.descriptionText = "Start " + operation.DisplayName +
                " at " + presentation.SelectedTimeCode + "?";
            if (confirmation.windowTitle != null)
                confirmation.windowTitle.text = confirmation.titleText;
            if (confirmation.windowDescription != null)
                confirmation.windowDescription.text = confirmation.descriptionText;
            try { confirmation.UpdateUI(); } catch { }
        }

        ConfigureNativeSelector(
            presentation.Board.InfilTimeSlider,
            operation.SupportedTimeCodes.ToArray(),
            index =>
            {
                if (index < 0 || index >= operation.SupportedTimeCodes.Count)
                    return;
                presentation.SelectedTimeCode = operation.SupportedTimeCodes[index];
                data.SelectedInfiltrationTime = index;
                data.INFILTRATION_TIME = presentation.SelectedTimeCode;
                if (confirmation != null)
                {
                    confirmation.descriptionText = "Start " + operation.DisplayName +
                        " at " + presentation.SelectedTimeCode + "?";
                    if (confirmation.windowDescription != null)
                        confirmation.windowDescription.text = confirmation.descriptionText;
                    try { confirmation.UpdateUI(); } catch { }
                }
            },
            selectedIndex);
    }

    private Sprite GetOrLoadPreviewSprite(ModdedMapDefinition map)
    {
        if (map == null)
            return null;
        if (previewSprites.TryGetValue(map.Id, out var existing) && existing != null)
            return existing;
        try
        {
            byte[] bytes = File.ReadAllBytes(map.PreviewImagePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true)
            {
                name = "MODDED_OPERATIONS_PREVIEW_" + map.Id
            };
            if (!ImageConversion.LoadImage(texture, bytes, false))
            {
                Object.Destroy(texture);
                return null;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = "MODDED_OPERATIONS_PREVIEW_" + map.Id;
            previewTextures[map.Id] = texture;
            previewSprites[map.Id] = sprite;
            return sprite;
        }
        catch (Exception ex)
        {
            log.LogWarning("Modded Operations preview load failed for " + map.Id +
                ": " + ex.GetType().Name + ": " + ex.Message);
            return null;
        }
    }

    private GameObject BuildPackageInfiltrationMapPrefab(
        CatalogPresentation presentation,
        ModdedOperationDefinition operation,
        Sprite preview)
    {
        if (presentation == null || presentation.Board == null ||
            operation == null || preview == null ||
            operation.Infiltrations == null || operation.Infiltrations.Count == 0)
        {
            return null;
        }

        MapInfilMarker markerTemplate = ResolveNativeInfiltrationMarkerTemplate();
        if (markerTemplate == null || markerTemplate.gameObject == null)
            return null;

        GameObject mapRoot = null;
        try
        {
            mapRoot = new GameObject("MODDED_OPERATIONS_PACKAGE_INFILTRATION_MAP");
            RectTransform mapRect = mapRoot.AddComponent<RectTransform>();
            SetFullStretch(mapRect);

            var background = new GameObject("PACKAGE_MAP_PREVIEW");
            background.transform.SetParent(mapRoot.transform, false);
            SetFullStretch(background.AddComponent<RectTransform>());
            var backgroundImage = background.AddComponent<Image>();
            backgroundImage.sprite = preview;
            backgroundImage.color = Color.white;
            backgroundImage.preserveAspect = true;
            backgroundImage.raycastTarget = false;

            for (int index = 0; index < operation.Infiltrations.Count; index++)
            {
                ModdedInfiltrationDefinition infiltration =
                    operation.Infiltrations[index];
                GameObject markerObject = Object.Instantiate(
                    markerTemplate.gameObject,
                    mapRoot.transform);
                if (markerObject == null)
                    throw new InvalidOperationException(
                        "the shipped infiltration marker visual could not be duplicated");
                markerObject.name = "PACKAGE_INFIL_" + infiltration.Id;
                MapInfilMarker marker = markerObject.GetComponent<MapInfilMarker>() ??
                    markerObject.GetComponentInChildren<MapInfilMarker>(true);
                if (marker == null)
                    throw new InvalidOperationException(
                        "the duplicated infiltration marker lost its native component");

                // Retain the shipped marker's graphics and interaction code,
                // but replace every mission-bearing field with package data.
                marker.MaxPlayers = infiltration.MaximumPlayers;
                marker.InfilName = infiltration.DisplayName;
                marker.IsGroundInfil = true;
                marker.IsHeliInfil = false;
                marker.IsExfil = false;
                marker.OpboardUI = presentation.Board;
                marker.MarkerIndex = index;
                marker.individualSelectMode = false;
                marker.CurrentNumPlayers = 0;
                marker.isSelectedLocal = false;

                RectTransform markerRect = markerObject.GetComponent<RectTransform>() ??
                    marker.GetComponent<RectTransform>();
                if (markerRect == null)
                    throw new InvalidOperationException(
                        "the shipped infiltration marker visual has no RectTransform");
                Vector2 anchor = new Vector2(
                    infiltration.MapPositionX,
                    infiltration.MapPositionY);
                markerRect.anchorMin = anchor;
                markerRect.anchorMax = anchor;
                markerRect.anchoredPosition = Vector2.zero;
                markerRect.localScale = Vector3.one;
                markerObject.SetActive(true);
            }

            mapRoot.SetActive(false);
            log.LogInfo("Modded Operations built a package-owned native " +
                "infiltration selector map: operation=" + operation.Id +
                ", markers=" + operation.Infiltrations.Count +
                ", visualSource=" + markerTemplate.gameObject.name + ".");
            return mapRoot;
        }
        catch (Exception ex)
        {
            if (mapRoot != null)
                Object.Destroy(mapRoot);
            log.LogError("Modded Operations could not build the package-owned " +
                "infiltration selector map: " + ex.GetType().Name + ": " +
                ex.Message);
            return null;
        }
    }

    private static MapInfilMarker ResolveNativeInfiltrationMarkerTemplate()
    {
        try
        {
            foreach (MapInfilMarker marker in
                     Resources.FindObjectsOfTypeAll<MapInfilMarker>())
            {
                if (IsUsableNativeInfiltrationMarkerTemplate(marker))
                    return marker;
            }
            foreach (CerebusOpboard board in
                     Resources.FindObjectsOfTypeAll<CerebusOpboard>())
            {
                if (board == null || board.MapPrefab == null ||
                    board.MapPrefab.name.StartsWith(
                        "MODDED_OPERATIONS_",
                        StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (MapInfilMarker marker in
                         board.MapPrefab.GetComponentsInChildren<MapInfilMarker>(true))
                {
                    if (IsUsableNativeInfiltrationMarkerTemplate(marker))
                        return marker;
                }
            }
        }
        catch
        {
        }
        return null;
    }

    private static bool IsUsableNativeInfiltrationMarkerTemplate(
        MapInfilMarker marker)
    {
        return marker != null && marker.gameObject != null &&
            !marker.gameObject.name.StartsWith(
                "PACKAGE_INFIL_",
                StringComparison.Ordinal) &&
            marker.SelectedParent != null && marker.DeselectedParent != null &&
            marker.canvasGroup != null;
    }

    private void BeginCatalogOperationLaunch(CatalogPresentation presentation)
    {
        if (presentation == null || presentation.SelectedOperation == null)
            return;
        var operation = presentation.SelectedOperation;
        if (!OperatorApi.ModdedOperations.TryGetMap(operation.MapId, out var map) ||
            map == null)
        {
            log.LogError("Modded Operations launch rejected because map " +
                operation.MapId + " is no longer in the frozen catalog.");
            return;
        }
        if (!map.Operations.Any(candidate =>
                string.Equals(candidate.Id, operation.Id, StringComparison.Ordinal)) ||
            !operation.SupportedTimeCodes.Contains(
                presentation.SelectedTimeCode,
                StringComparer.Ordinal))
        {
            log.LogError("Modded Operations launch rejected because the selected " +
                "operation/time pair is not owned by its frozen package map.");
            return;
        }

        log.LogInfo("Modded Operations launch request captured: operation=" +
            operation.Id + ", laptopId=" + presentation.Laptop.GetInstanceID() +
            ", time=" + presentation.SelectedTimeCode + ".");

        // A retail operation starts in the same frame as Confirm. A package can
        // first require asynchronous bundle I/O, so preserve the exact
        // player-owned laptop graph while that work completes. Closing the
        // modal before the I/O finished allowed MissionLaptop to release its
        // playerNetworking reference and made the first Confirm fail closed.
        var launchLaptop = ResolveLaunchLaptop(presentation.Laptop);
        var launchPlayer = launchLaptop == null ? null : launchLaptop.playerNetworking;
        if (!IsPlayerOwnedLaunchLaptop(launchLaptop) || launchPlayer == null)
        {
            log.LogError("Modded Operations launch rejected before package loading " +
                "because no player-owned mission laptop was available.");
            return;
        }
        SetNativeConfirmationLoadingState(presentation, true);
        if (loadedMapBundles.TryGetValue(map.Id, out var loaded) &&
            loaded != null && loaded.SceneBundle != null && loaded.Map != null &&
            string.Equals(
                loaded.Map.PackageContentId,
                map.PackageContentId,
                StringComparison.Ordinal))
        {
            InvokeNativeCatalogLaunch(
                presentation,
                map,
                operation,
                presentation.SelectedTimeCode,
                launchLaptop,
                launchPlayer);
            return;
        }

        if (pendingLaunch != null)
        {
            if (!string.Equals(pendingLaunch.Map?.Id, map.Id, StringComparison.Ordinal) ||
                !string.Equals(
                    pendingLaunch.Map?.PackageContentId,
                    map.PackageContentId,
                    StringComparison.Ordinal))
            {
                SetNativeConfirmationLoadingState(presentation, false);
                log.LogWarning("Modded Operations ignored a launch while another " +
                    "selected map is still loading: requested=" + map.Id +
                    ", loading=" + pendingLaunch.Map?.Id + ".");
                return;
            }

            pendingLaunch.Presentation = presentation;
            pendingLaunch.Operation = operation;
            pendingLaunch.TimeCode = presentation.SelectedTimeCode;
            pendingLaunch.LaunchLaptop = launchLaptop;
            pendingLaunch.LaunchPlayer = launchPlayer;
            pendingLaunch.LaunchRequested = true;
            pendingLaunch.LaunchRequestedTimestamp =
                System.Diagnostics.Stopwatch.GetTimestamp();
            log.LogInfo("Modded Operations attached Confirm to the selected-map " +
                "prefetch: map=" + map.Id + ", completedBundles=" +
                pendingLaunch.RequestIndex + "/" +
                pendingLaunch.BundlePaths.Count + ".");
            return;
        }
        if (loaded != null)
        {
            try { loaded.SceneBundle?.Unload(false); } catch { }
            foreach (var dependency in loaded.Dependencies)
            {
                try { dependency?.Unload(false); } catch { }
            }
            loadedMapBundles.Remove(map.Id);
            log.LogWarning("Modded Operations discarded an incomplete or stale " +
                "bundle cache entry before loading map=" + map.Id + ".");
        }

        var loading = new LoadedMapBundles { Map = map };
        var pending = new PendingMapLaunch
        {
            Presentation = presentation,
            Map = map,
            Operation = operation,
            TimeCode = presentation.SelectedTimeCode,
            LaunchLaptop = launchLaptop,
            LaunchPlayer = launchPlayer,
            LoadingBundles = loading,
            LaunchRequested = true,
            LoadStartedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp(),
            LaunchRequestedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp()
        };
        foreach (string path in map.DependencyBundlePaths)
            pending.BundlePaths.Add(path);
        pending.BundlePaths.Add(map.SceneBundlePath);
        pendingLaunch = pending;
        log.LogInfo("Modded Operations began asynchronous package load: map=" +
            map.Id + ", bundles=" + pending.BundlePaths.Count + ".");
    }

    private void ProcessPendingLaunch()
    {
        var pending = pendingLaunch;
        if (pending == null)
            return;
        try
        {
            if (pending.CurrentRequest == null)
            {
                if (pending.RequestIndex >= pending.BundlePaths.Count)
                {
                    if (!ValidateLoadedSceneBundle(pending.Map, pending.LoadingBundles))
                    {
                        FailPendingLaunch(
                            "scene bundle is missing its declared scene address or " +
                            "contains an undeclared scene address");
                        return;
                    }
                    loadedMapBundles[pending.Map.Id] = pending.LoadingBundles;
                    pendingLaunch = null;
                    log.LogInfo("Modded Operations completed verified bundle " +
                        "registration: map=" + pending.Map.Id + ", seconds=" +
                        FormatElapsedSeconds(pending.LoadStartedTimestamp) +
                        ", launchRequested=" + pending.LaunchRequested + ".");
                    if (pending.LaunchRequested)
                    {
                        log.LogInfo("Modded Operations Confirm waited " +
                            FormatElapsedSeconds(pending.LaunchRequestedTimestamp) +
                            " seconds for remaining selected-map bundle work.");
                        InvokeNativeCatalogLaunch(
                            pending.Presentation,
                            pending.Map,
                            pending.Operation,
                            pending.TimeCode,
                            pending.LaunchLaptop,
                            pending.LaunchPlayer);
                    }
                    return;
                }
                string path = pending.BundlePaths[pending.RequestIndex];
                pending.CurrentRequestStartedTimestamp =
                    System.Diagnostics.Stopwatch.GetTimestamp();
                pending.CurrentRequest = AssetBundle.LoadFromFileAsync(path);
                if (pending.CurrentRequest == null)
                    FailPendingLaunch("Unity rejected bundle request for " + path);
                return;
            }

            if (!pending.CurrentRequest.isDone)
                return;
            var bundle = pending.CurrentRequest.assetBundle;
            pending.CurrentRequest = null;
            if (bundle == null)
            {
                FailPendingLaunch("Unity could not load bundle " +
                    pending.BundlePaths[pending.RequestIndex]);
                return;
            }
            long bundleBytes = 0;
            try { bundleBytes = new FileInfo(
                pending.BundlePaths[pending.RequestIndex]).Length; } catch { }
            log.LogInfo("Modded Operations loaded verified bundle " +
                (pending.RequestIndex + 1) + "/" + pending.BundlePaths.Count +
                ": file=" + Path.GetFileName(
                    pending.BundlePaths[pending.RequestIndex]) +
                ", bytes=" + bundleBytes + ", seconds=" +
                FormatElapsedSeconds(pending.CurrentRequestStartedTimestamp) + ".");
            bool isSceneBundle = pending.RequestIndex == pending.BundlePaths.Count - 1;
            if (isSceneBundle)
                pending.LoadingBundles.SceneBundle = bundle;
            else
            {
                string[] dependencyScenes = null;
                try { dependencyScenes = bundle.GetAllScenePaths(); } catch { }
                if (dependencyScenes != null && dependencyScenes.Length > 0)
                {
                    try { bundle.Unload(false); } catch { }
                    FailPendingLaunch("dependency bundle contains a streamed scene: " +
                        pending.BundlePaths[pending.RequestIndex]);
                    return;
                }
                pending.LoadingBundles.Dependencies.Add(bundle);
                pending.LoadingBundles.DependenciesByPath[
                    pending.BundlePaths[pending.RequestIndex]] = bundle;
            }
            pending.RequestIndex++;
        }
        catch (Exception ex)
        {
            FailPendingLaunch(ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static long GetBundleByteTotal(IEnumerable<string> paths)
    {
        long total = 0;
        foreach (string path in paths ?? Enumerable.Empty<string>())
        {
            try { total = checked(total + new FileInfo(path).Length); }
            catch { }
        }
        return total;
    }

    private static string FormatElapsedSeconds(long startedTimestamp)
    {
        if (startedTimestamp <= 0)
            return "0.000";
        double seconds =
            (System.Diagnostics.Stopwatch.GetTimestamp() - startedTimestamp) /
            (double)System.Diagnostics.Stopwatch.Frequency;
        return seconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool ValidateLoadedSceneBundle(
        ModdedMapDefinition map,
        LoadedMapBundles bundles)
    {
        if (map == null || bundles == null || bundles.SceneBundle == null)
            return false;
        try
        {
            var allowedScenes = new HashSet<string>(
                OperatorApi.ModdedOperations.Maps
                    .Where(candidate => candidate != null &&
                        string.Equals(
                            candidate.PackageContentId,
                            map.PackageContentId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            candidate.SceneBundlePath,
                            map.SceneBundlePath,
                            StringComparison.OrdinalIgnoreCase))
                    .Select(candidate => candidate.ScenePath),
                StringComparer.OrdinalIgnoreCase);
            if (!allowedScenes.Contains(map.ScenePath))
                return false;
            bool declaredSceneFound = false;
            foreach (string scenePath in bundles.SceneBundle.GetAllScenePaths())
            {
                if (!allowedScenes.Contains(scenePath))
                    return false;
                if (string.Equals(
                        scenePath,
                        map.ScenePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    declaredSceneFound = true;
                }
            }
            return declaredSceneFound;
        }
        catch { }
        return false;
    }

    private void FailPendingLaunch(string reason)
    {
        var pending = pendingLaunch;
        pendingLaunch = null;
        if (pending?.LoadingBundles != null)
        {
            try { pending.LoadingBundles.SceneBundle?.Unload(false); } catch { }
            foreach (var bundle in pending.LoadingBundles.Dependencies)
            {
                try { bundle?.Unload(false); } catch { }
            }
        }
        log.LogError("Modded Operations package " +
            (pending?.LaunchRequested == true ? "launch" : "prefetch") +
            " failed closed: " + reason + ".");
        if (pending?.Presentation != null)
            SetNativeConfirmationLoadingState(pending.Presentation, false);
    }

    private void InvokeNativeCatalogLaunch(
        CatalogPresentation presentation,
        ModdedMapDefinition map,
        ModdedOperationDefinition operation,
        string timeCode,
        MissionLaptop launchLaptop,
        PlayerNetworking launchPlayer)
    {
        if (presentation == null || presentation.NativeBoardData == null ||
            map == null || operation == null)
        {
            return;
        }
        try
        {
            presentation.SelectedOperation = operation;
            presentation.SelectedTimeCode = timeCode;
            UpdateCatalogOperationBoard(presentation);
            if (!string.Equals(operation.MapId, map.Id, StringComparison.Ordinal) ||
                !operation.SupportedTimeCodes.Contains(timeCode, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    "the requested operation/time pair does not belong to the selected map");
            activeOperation = new ActiveMapOperation
            {
                Map = map,
                Operation = operation,
                TimeCode = timeCode,
                SceneHandle = 0
            };
            log.LogInfo("Modded Operations accepted an isolated package launch: " +
                "operation=" + operation.Id + ", map=" + map.Id +
                ", content=" + map.PackageContentId + ", scene=" + map.ScenePath +
                ", time=" + timeCode + ".");
            LogNativeLaunchContract(presentation.NativeBoardData);
            InvokeNativeBoardStart(
                presentation,
                map,
                operation,
                timeCode,
                launchLaptop,
                launchPlayer);
        }
        catch (Exception ex)
        {
            activeOperation = null;
            SetNativeConfirmationLoadingState(presentation, false);
            log.LogError("Modded Operations native start failed closed: " +
                ex.GetType().Name + ": " + ex.Message);
        }
    }

    private void InvokeNativeBoardStart(
        CatalogPresentation presentation,
        ModdedMapDefinition map,
        ModdedOperationDefinition operation,
        string timeCode,
        MissionLaptop capturedLaptop,
        PlayerNetworking capturedPlayer)
    {
        var manager = OperationsManager.singleton;
        var board = presentation?.NativeBoardData;
        if (manager == null || board == null || map == null || operation == null)
            throw new InvalidOperationException("native operation manager was unavailable");
        if (!string.Equals(operation.MapId, map.Id, StringComparison.Ordinal) ||
            !operation.SupportedTimeCodes.Contains(timeCode, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "package operation ownership changed before native start");
        var launchLaptop = RestoreCapturedLaunchLaptop(
            capturedLaptop,
            capturedPlayer,
            presentation.Laptop);
        if (launchLaptop == null)
            throw new InvalidOperationException(
                "no player-owned mission laptop was available for native launch");
        if (manager.currentOpInfo == null)
            manager.currentOpInfo = new CurrentOpInfo();
        if (manager.currentOpInfo == null)
            throw new InvalidOperationException(
                "native current-operation state could not be allocated");
        board.missionLaptop = launchLaptop;
        manager.activeMissionLaptop = launchLaptop;
        if (board.TARGETPACKAGE == null || board.TARGETPACKAGE.Length == 0 ||
            board.SelectedInfiltrationTime < 0 ||
            board.SelectedInfiltrationTime >= board.TARGETPACKAGE.Length ||
            board.missionLaptop == null || board.OperationBoardUI == null)
        {
            throw new InvalidOperationException(
                "private operation board launch contract was incomplete");
        }

        // Use the same purely local board entry point as a shipped operation.
        // This method transfers the private board data to CurrentOpInfo, applies
        // the declared game mode and AI values, starts the native command/server
        // sequence, and exits the laptop. Do not invoke the quarantined manager
        // command methods from adapter code.
        log.LogInfo("Modded Operations completed the native board ownership graph: " +
            "sourceLaptop=" + presentation.Laptop.GetInstanceID() +
            ", launchLaptop=" + launchLaptop.GetInstanceID() +
            ", playerOwned=" + (launchLaptop.playerNetworking != null) +
            ", currentOperationInfo=" + (manager.currentOpInfo != null) + ".");
        // Now close and start in one frame, which matches the retail Confirm
        // transition and leaves no asynchronous laptop-ownership gap.
        CloseNativeMapConfirmation(presentation.Board, false);
        PrimeNativeInfiltrationSelector(board, operation);
        board.Start_Operation();
        log.LogInfo("Modded Operations entered the shipped board launch pipeline " +
            "through CerebusOpboard.Start_Operation: operation=" +
            operation.Id + ", scene=" + map.ScenePath + ".");
    }

    private void PrimeNativeInfiltrationSelector(
        CerebusOpboard board,
        ModdedOperationDefinition operation)
    {
        if (board == null || board.MapPrefab == null || operation == null ||
            operation.Infiltrations == null || operation.Infiltrations.Count == 0)
        {
            throw new InvalidOperationException(
                "package infiltration selector data was unavailable");
        }

        InfilSelectorDisplayer selector = InfilSelectorDisplayer.instance ??
            Resources.FindObjectsOfTypeAll<InfilSelectorDisplayer>()
                .FirstOrDefault(item => item != null && item.MapParent != null &&
                    item.SelectInfilUI != null);
        if (selector == null)
            throw new InvalidOperationException(
                "the shipped infiltration selector was unavailable");

        // OperationBoardUI.Setup is a retail mission initializer and cannot
        // safely consume private package data. Its one required selector step
        // is the shipped SpawnMap call, so invoke that step directly only after
        // the private board ownership graph has been validated.
        bool sourceMapWasActive = board.MapPrefab.activeSelf;
        try
        {
            // SpawnMap uses GetComponentsInChildrenOrdered with
            // includeInactive=false. Keep the template hidden between uses,
            // but make it active for the Instantiate call so the native clone
            // and its package markers participate in selector discovery.
            if (!sourceMapWasActive)
                board.MapPrefab.SetActive(true);
            selector.SpawnMap(board.MapPrefab);
        }
        finally
        {
            if (board.MapPrefab != null &&
                board.MapPrefab.activeSelf != sourceMapWasActive)
            {
                board.MapPrefab.SetActive(sourceMapWasActive);
            }
        }
        MapInfilMarker[] markers = selector.MapInfilMarkers == null
            ? Array.Empty<MapInfilMarker>()
            : selector.MapInfilMarkers.ToArray();
        if (selector.ActiveMap == null ||
            !selector.ActiveMap.name.StartsWith(
                "MODDED_OPERATIONS_PACKAGE_INFILTRATION_MAP",
                StringComparison.Ordinal) ||
            markers.Length != operation.Infiltrations.Count)
        {
            throw new InvalidOperationException(
                "the shipped infiltration selector did not instantiate the package map " +
                "(selector=" + selector.GetInstanceID() +
                ", activeMap=" +
                (selector.ActiveMap == null ? "null" : selector.ActiveMap.name) +
                ", markers=" + markers.Length +
                ", expectedMarkers=" + operation.Infiltrations.Count + ")");
        }
        for (int index = 0; index < markers.Length; index++)
        {
            MapInfilMarker marker = markers[index];
            ModdedInfiltrationDefinition infiltration =
                operation.Infiltrations[index];
            if (marker == null || marker.MarkerIndex != index ||
                !string.Equals(marker.InfilName, infiltration.DisplayName,
                    StringComparison.Ordinal) ||
                marker.MaxPlayers != infiltration.MaximumPlayers ||
                !marker.IsGroundInfil || marker.IsHeliInfil || marker.IsExfil)
            {
                throw new InvalidOperationException(
                    "the shipped infiltration selector retained non-package marker data");
            }
        }

        log.LogInfo("Modded Operations primed the shipped infiltration selector " +
            "through InfilSelectorDisplayer.SpawnMap: operation=" +
            operation.Id + ", activeMap=" + selector.ActiveMap.name +
            ", markers=" + markers.Length + ".");
    }

    private static MissionLaptop ResolveLaunchLaptop(MissionLaptop preferred)
    {
        var managerOwned = OperationsManager.singleton == null
            ? null
            : OperationsManager.singleton.activeMissionLaptop;
        if (IsPlayerOwnedLaunchLaptop(managerOwned))
            return managerOwned;
        if (IsPlayerOwnedLaunchLaptop(preferred))
            return preferred;
        MissionLaptop inactiveFallback = null;
        Type laptopType = ResolveMissionLaptopType();
        foreach (Component candidate in FindMissionLaptopComponents(laptopType))
        {
            var laptop = candidate as MissionLaptop;
            if (!IsPlayerOwnedLaunchLaptop(laptop))
                continue;
            if (laptop.gameObject.activeInHierarchy)
                return laptop;
            inactiveFallback ??= laptop;
        }
        return inactiveFallback;
    }

    private static MissionLaptop RestoreCapturedLaunchLaptop(
        MissionLaptop capturedLaptop,
        PlayerNetworking capturedPlayer,
        MissionLaptop preferred)
    {
        if (IsPlayerOwnedLaunchLaptop(capturedLaptop))
            return capturedLaptop;
        if (capturedLaptop != null && capturedPlayer != null &&
            capturedPlayer.gameObject != null && capturedPlayer.isOwned)
        {
            // This restores only the reference that the adapter's asynchronous
            // delay allowed the laptop UI to release. The captured player and
            // laptop were both validated at the physical Confirm transition.
            capturedLaptop.playerNetworking = capturedPlayer;
            if (IsPlayerOwnedLaunchLaptop(capturedLaptop))
                return capturedLaptop;
        }
        return ResolveLaunchLaptop(preferred);
    }

    private static bool IsPlayerOwnedLaunchLaptop(MissionLaptop laptop)
    {
        return laptop != null && laptop.gameObject != null &&
            laptop.playerNetworking != null && laptop.LaptopPlayerCamera != null &&
            laptop.computerManager != null && laptop.uiRaycaster != null &&
            laptop.PlayerBlocker != null && laptop.ShitToKillWhenNotUsing != null;
    }

    private void LogNativeLaunchContract(CerebusOpboard board)
    {
        if (board == null)
            return;
        try
        {
            var target = board.ThisMissionTargetPackage;
            var laptop = board.missionLaptop;
            log.LogInfo("Modded Operations private UI launch contract: mapPrefab=" +
                (board.MapPrefab == null ? "null" : board.MapPrefab.name) +
                ", targetPackage=" + (target == null ? "null" : target.name) +
                ", targetBackref=" +
                (target == null || target.TargetPackage == null
                    ? "null"
                    : target.TargetPackage.name) +
                ", targets=" + (board.TARGETPACKAGE == null ? -1 : board.TARGETPACKAGE.Length) +
                ", selectedInfil=" + board.SelectedInfiltrationTime +
                ", operationBoard=" + (board.OperationBoardUI != null) +
                ", laptop=" + (laptop != null) +
                ", laptopPlayer=" +
                (laptop != null && laptop.playerNetworking != null) +
                ", operationsManager=" + (OperationsManager.singleton != null) +
                ", gameManagerNetwork=" + (GameManagerNetwork.instance != null) + ".");
        }
        catch (Exception ex)
        {
            log.LogWarning("Modded Operations could not describe the native launch " +
                "contract: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var operation = activeOperation;
        if (operation == null || operation.Map == null || !scene.IsValid() ||
            !scene.isLoaded || !SceneMatchesMap(scene, operation.Map))
        {
            return;
        }
        if (!ValidateStandaloneSceneContract(scene, operation, out string contractError))
        {
            log.LogError("Standalone package scene contract rejected map=" +
                operation.Map.Id + ": " + contractError + ".");
            return;
        }
        ReleaseStandaloneSceneContracts(operation);
        ShowNativeLoadingScreenForPackageScene(operation);
        operation.SceneHandle = scene.handle;
        operation.BootstrapRoot = null;
        operation.BootstrapIdentity = null;
        operation.BootstrapPrefabRoot = null;
        operation.BootstrapPrefabIdentity = null;
        operation.BootstrapAssetId = 0;
        operation.BootstrapPrefabRegistered = false;
        operation.GameModeComponent = null;
        operation.BootstrapCreated = false;
        operation.NetworkSpawnRequested = false;
        operation.ReadinessInitializationClaimed = false;
        operation.ReadinessInitialized = false;
        operation.AllPlayersLoaded = false;
        operation.NativePvpLifecycleActive = false;
        operation.AllPlayersLoadedFrame = -1;
        operation.BootstrapFrame = Time.frameCount;
        operation.LastMaintenanceFrame = -1;
        operation.SpawnCursor = 0;
        operation.ScenePreparationComplete = false;
        operation.ScenePreparationStarted = false;
        operation.ScenePreparationEarliestFrame = Time.frameCount + 1;
        operation.TerrainReady = false;
        ReleaseRuntimeTerrain(operation);
        operation.PositionedPlayerObjects.Clear();
        operation.PlayerMarkerNames.Clear();
        operation.PlayerSpawnRequestFrames.Clear();
        operation.PlayerSpawnRequestCounts.Clear();
        operation.CompletedPlayerSpawnIds.Clear();
        operation.PlayerMoveRequestFrames.Clear();
        operation.PveSpawnAttempted = false;
        operation.PveEnemyCount = 0;
        operation.PveRaidManager = null;
        operation.PveExfilZone = null;
        operation.PveExfilCollider = null;
    }

    private void ShowNativeLoadingScreenForPackageScene(
        ActiveMapOperation operation)
    {
        // Vanilla GameManagerNetwork.OnAllPlayersLoaded(false) enters this
        // exact method at RVA 0x00916210. It activates the shipped loading
        // canvas, freezes the current player body, clears velocity, and closes
        // infiltration UI. A standalone package creates its replacement
        // GameMode on the next Unity frame, so call the same route here to
        // close the one-frame gap in which the package's authored proxy terrain
        // could otherwise be visible before runtime terrain/material services.
        // GameManagerNetwork keeps ownership of the matching hide transition.
        try
        {
            var manager = GameManagerNetwork.instance;
            if (manager == null || manager.LoadingScreen == null)
            {
                log.LogWarning("Standalone package scene could not enter the " +
                    "shipped loading presentation because GameManagerNetwork " +
                    "or its LoadingScreen was unavailable.");
                return;
            }
            manager.ShowLoadingScreen();
            log.LogInfo("Standalone package scene entered the shipped " +
                "GameManagerNetwork loading presentation before runtime " +
                "terrain/material preparation: map=" + operation.Map.Id +
                ", loadingScreenActiveSelf=" + manager.LoadingScreen.activeSelf +
                ", loadingScreenActiveInHierarchy=" +
                manager.LoadingScreen.activeInHierarchy +
                ", nativeHideSoonFlag=" + manager.LoadingScreenVisible + ".");
        }
        catch (Exception ex)
        {
            log.LogWarning("Standalone package scene could not enter the " +
                "shipped loading presentation: " + ex.GetType().Name + ": " +
                ex.Message);
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        var operation = activeOperation;
        if (operation == null || scene.handle != operation.SceneHandle)
            return;
        ReleaseStandaloneSceneContracts(operation);
        operation.SceneHandle = 0;
        operation.BootstrapRoot = null;
        operation.BootstrapIdentity = null;
        operation.BootstrapPrefabRoot = null;
        operation.BootstrapPrefabIdentity = null;
        operation.BootstrapAssetId = 0;
        operation.BootstrapPrefabRegistered = false;
        operation.GameModeComponent = null;
        operation.BootstrapCreated = false;
        operation.NetworkSpawnRequested = false;
        operation.ReadinessInitializationClaimed = false;
        operation.ReadinessInitialized = false;
        operation.AllPlayersLoaded = false;
        operation.NativePvpLifecycleActive = false;
        operation.AllPlayersLoadedFrame = -1;
        operation.ScenePreparationComplete = false;
        operation.ScenePreparationStarted = false;
        operation.ScenePreparationEarliestFrame = -1;
        operation.TerrainReady = false;
        ReleaseRuntimeTerrain(operation);
        operation.PositionedPlayerObjects.Clear();
        operation.PlayerMarkerNames.Clear();
        operation.PlayerSpawnRequestFrames.Clear();
        operation.PlayerSpawnRequestCounts.Clear();
        operation.CompletedPlayerSpawnIds.Clear();
        operation.PlayerMoveRequestFrames.Clear();
        operation.PveSpawnAttempted = false;
        operation.PveEnemyCount = 0;
        operation.PveRaidManager = null;
        operation.PveExfilZone = null;
        operation.PveExfilCollider = null;
        log.LogInfo("Modded Operations map scene unloaded; package bundles remain " +
            "resident so the shipped Restart Operation route can reload the same scene.");
    }

    private void PrepareStandaloneScene(
        Scene scene,
        ActiveMapOperation operation)
    {
        if (activeOperation != operation || operation.SceneHandle != scene.handle ||
            !scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        if (!TryPrepareRuntimeTerrain(scene, operation, out string terrainError))
        {
            log.LogError("Standalone package terrain preparation failed closed: map=" +
                operation.Map.Id + ", reason=" + terrainError + ".");
            return;
        }
        Physics.SyncTransforms();
        if (!ValidateWalkableGroundContract(scene, operation, out string groundError))
        {
            log.LogError("Standalone package walkable-ground contract failed closed: map=" +
                operation.Map.Id + ", reason=" + groundError + ".");
            ReleaseRuntimeTerrain(operation);
            return;
        }

        operation.TerrainReady = true;
        ConfigureStandalonePlayerSpawnContract(scene, operation);
        CreateStandaloneGameplayBootstrap(scene, operation);
        ApplyStandaloneRenderContract(scene, operation);
        operation.ScenePreparationComplete = operation.BootstrapCreated;
        log.LogInfo("Standalone package scene services are ready before native " +
            "player spawn: map=" + operation.Map.Id +
            ", terrain=" + (operation.Map.RuntimeTerrain != null) +
            ", walkableGround=true, bootstrap=" + operation.BootstrapCreated + ".");
    }

    private static bool SceneMatchesMap(Scene scene, ModdedMapDefinition map)
    {
        if (map == null)
            return false;
        if (!string.IsNullOrEmpty(scene.path) &&
            string.Equals(scene.path, map.ScenePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return string.Equals(
            scene.name,
            Path.GetFileNameWithoutExtension(map.ScenePath),
            StringComparison.OrdinalIgnoreCase);
    }

    private bool TryPrepareRuntimeTerrain(
        Scene scene,
        ActiveMapOperation operation,
        out string error)
    {
        error = string.Empty;
        ModdedRuntimeTerrainDefinition descriptor = operation?.Map?.RuntimeTerrain;
        if (descriptor == null)
            return true;
        if (!loadedMapBundles.TryGetValue(operation.Map.Id, out LoadedMapBundles bundles) ||
            bundles == null || bundles.Map == null ||
            !string.Equals(
                bundles.Map.PackageContentId,
                operation.Map.PackageContentId,
                StringComparison.Ordinal) ||
            !bundles.DependenciesByPath.TryGetValue(
                descriptor.VerifiedDependencyBundlePath,
                out AssetBundle dependency) || dependency == null)
        {
            error = "the verified terrain dependency bundle is not resident";
            return false;
        }

        Transform ground = FindExactSceneTransform(
            scene,
            descriptor.RootObjectName,
            out int groundMatches);
        if (ground == null || groundMatches != 1)
        {
            error = "runtimeTerrain rootObject must resolve to exactly one scene object";
            return false;
        }

        TerrainData data = null;
        var createdLayers = new List<TerrainLayer>();
        try
        {
            Texture2D heightPayload = LoadRequiredTerrainTexture(
                dependency,
                descriptor.HeightPayloadAssetPath,
                true);
            Texture2D weightsPayload = LoadRequiredTerrainTexture(
                dependency,
                descriptor.SurfaceWeightsPayloadAssetPath,
                true);
            if (heightPayload == null || weightsPayload == null)
            {
                error = "one or more numerical terrain payloads could not be loaded";
                return false;
            }
            if (heightPayload.width != descriptor.HeightmapResolution ||
                heightPayload.height != descriptor.HeightmapResolution ||
                weightsPayload.width != descriptor.AlphamapResolution ||
                weightsPayload.height != descriptor.AlphamapResolution)
            {
                error = "terrain payload dimensions do not match the frozen manifest";
                return false;
            }

            var diffuseTextures = new Texture2D[descriptor.Layers.Count];
            var normalTextures = new Texture2D[descriptor.Layers.Count];
            var maskTextures = new Texture2D[descriptor.Layers.Count];
            for (int index = 0; index < descriptor.Layers.Count; index++)
            {
                ModdedRuntimeTerrainLayerDefinition layer = descriptor.Layers[index];
                diffuseTextures[index] = LoadRequiredTerrainTexture(
                    dependency,
                    layer.DiffuseAssetPath,
                    false);
                normalTextures[index] = LoadRequiredTerrainTexture(
                    dependency,
                    layer.NormalAssetPath,
                    false);
                maskTextures[index] = LoadRequiredTerrainTexture(
                    dependency,
                    layer.MaskAssetPath,
                    false);
                if (diffuseTextures[index] == null || normalTextures[index] == null ||
                    maskTextures[index] == null)
                {
                    error = "one or more terrain-layer textures could not be loaded";
                    return false;
                }
            }

            data = new TerrainData
            {
                name = "MODDED_OPERATIONS_RUNTIME_TERRAIN_" + operation.Map.Id,
                heightmapResolution = descriptor.HeightmapResolution,
                alphamapResolution = descriptor.AlphamapResolution,
                baseMapResolution = descriptor.BaseMapResolution,
                size = new Vector3(
                    descriptor.Width,
                    descriptor.Height,
                    descriptor.Length)
            };
            data.SetDetailResolution(
                descriptor.DetailResolution,
                descriptor.DetailResolutionPerPatch);

            var encodedHeights = heightPayload.GetPixels32();
            var heights = AllocateIl2CppFloatArray(
                descriptor.HeightmapResolution,
                descriptor.HeightmapResolution);
            var heightSamples = heights.AsSpan();
            for (int z = 0; z < descriptor.HeightmapResolution; z++)
            {
                for (int x = 0; x < descriptor.HeightmapResolution; x++)
                {
                    Color32 sample = encodedHeights[
                        z * descriptor.HeightmapResolution + x];
                    heightSamples[z * descriptor.HeightmapResolution + x] =
                        ((sample.r << 8) | sample.g) / 65535f;
                }
            }
            data.SetHeights(0, 0, heights);

            var terrainLayers =
                new Il2CppReferenceArray<TerrainLayer>(descriptor.Layers.Count);
            for (int index = 0; index < descriptor.Layers.Count; index++)
            {
                ModdedRuntimeTerrainLayerDefinition source = descriptor.Layers[index];
                var layer = new TerrainLayer
                {
                    name = source.Name,
                    diffuseTexture = diffuseTextures[index],
                    normalMapTexture = normalTextures[index],
                    maskMapTexture = maskTextures[index],
                    tileSize = new Vector2(source.TileSizeX, source.TileSizeZ),
                    tileOffset = Vector2.zero,
                    normalScale = source.NormalScale,
                    metallic = source.Metallic,
                    smoothness = source.Smoothness
                };
                createdLayers.Add(layer);
                terrainLayers[index] = layer;
            }
            data.terrainLayers = terrainLayers;

            var encodedWeights = weightsPayload.GetPixels32();
            var alphamaps = AllocateIl2CppFloatArray(
                descriptor.AlphamapResolution,
                descriptor.AlphamapResolution,
                descriptor.Layers.Count);
            var alphaSamples = alphamaps.AsSpan();
            for (int z = 0; z < descriptor.AlphamapResolution; z++)
            {
                for (int x = 0; x < descriptor.AlphamapResolution; x++)
                {
                    Color32 sample = encodedWeights[
                        z * descriptor.AlphamapResolution + x];
                    float first = sample.r / 255f;
                    float second = sample.g / 255f;
                    float third = sample.b / 255f;
                    float total = first + second + third;
                    if (total <= 0.00001f)
                    {
                        first = 1f;
                        second = 0f;
                        third = 0f;
                    }
                    else
                    {
                        first /= total;
                        second /= total;
                        third /= total;
                    }
                    int offset = ((z * descriptor.AlphamapResolution) + x) * 3;
                    alphaSamples[offset] = first;
                    alphaSamples[offset + 1] = second;
                    alphaSamples[offset + 2] = third;
                }
            }
            data.SetAlphamaps(0, 0, alphamaps);

            Terrain terrain = ground.GetComponent<Terrain>();
            if (terrain == null)
                terrain = ground.gameObject.AddComponent<Terrain>();
            TerrainCollider collider = ground.GetComponent<TerrainCollider>();
            if (collider == null)
                collider = ground.gameObject.AddComponent<TerrainCollider>();
            ground.position = new Vector3(
                descriptor.OriginX,
                descriptor.OriginY,
                descriptor.OriginZ);
            terrain.terrainData = data;
            collider.terrainData = data;
            terrain.materialType = Terrain.MaterialType.Custom;
            terrain.drawInstanced = true;
            terrain.drawTreesAndFoliage = false;
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 1000f;
            terrain.treeDistance = 5000f;
            terrain.treeBillboardDistance = 50f;
            terrain.detailObjectDistance = 200f;
            terrain.detailObjectDensity = 1f;
            terrain.Flush();
            // This mesh is only a serialized preview/fallback for tools that
            // cannot load TerrainData from the package. The reconstructed
            // Terrain now owns rendering and collision. Leaving both visible
            // creates the flat brown plane reported below the live hills and
            // can put a camera or physics query on the wrong surface.
            Transform renderFallback = ground.Find("NATIVE_Ground_HillyTerrain_RenderFallback");
            if (renderFallback != null && renderFallback.gameObject.activeSelf)
            {
                renderFallback.gameObject.SetActive(false);
                log.LogInfo("Disabled package terrain render fallback after TerrainData bind: " +
                    renderFallback.name + ".");
            }
            Physics.SyncTransforms();
            if (terrain.terrainData != data || collider.terrainData != data)
            {
                error = "Unity did not bind the reconstructed TerrainData to rendering and collision";
                return false;
            }

            operation.RuntimeTerrainData = data;
            operation.RuntimeTerrainLayers.AddRange(createdLayers);
            log.LogInfo("Standalone reconstructed package-owned runtime terrain: map=" +
                operation.Map.Id + ", root=" + descriptor.RootObjectName +
                ", size=" + data.size + ", heightmap=" + data.heightmapResolution +
                ", alphamap=" + data.alphamapResolution +
                ", layers=" + data.terrainLayers.Length +
                ", colliderBound=true.");
            return true;
        }
        catch (Exception exception)
        {
            error = exception.GetType().Name + ": " + exception.Message;
            return false;
        }
        finally
        {
            if (operation.RuntimeTerrainData != data)
            {
                if (data != null)
                    Object.Destroy(data);
                foreach (TerrainLayer layer in createdLayers)
                {
                    if (layer != null)
                        Object.Destroy(layer);
                }
            }
        }
    }

    private Texture2D LoadRequiredTerrainTexture(
        AssetBundle bundle,
        string assetPath,
        bool requireReadable)
    {
        Texture2D texture = NativeBundleAssetLoader.LoadTexture2D(
            bundle,
            assetPath,
            out string diagnostic);
        if (texture == null)
        {
            log.LogError("Required package terrain Texture2D could not be loaded: asset=" +
                assetPath + ", diagnostic=" + diagnostic);
            return null;
        }
        if (requireReadable && !texture.isReadable)
        {
            log.LogError("Required numerical package terrain texture is not readable: " +
                assetPath + ".");
            return null;
        }
        return texture;
    }

    private static Transform FindExactSceneTransform(
        Scene scene,
        string exactName,
        out int matches)
    {
        matches = 0;
        Transform result = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                if (item != null && string.Equals(
                        item.name,
                        exactName,
                        StringComparison.Ordinal))
                {
                    matches++;
                    result ??= item;
                }
            }
        }
        return result;
    }

    private static bool ValidateWalkableGroundContract(
        Scene scene,
        ActiveMapOperation operation,
        out string error)
    {
        error = string.Empty;
        List<Transform> markers = FindStandalonePlayerMarkers(
            scene,
            operation.Operation.Mode);
        if (markers.Count == 0)
        {
            error = "no compatible player markers were available for ground checks";
            return false;
        }

        var colliders = new List<Collider>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            colliders.AddRange(root.GetComponentsInChildren<Collider>(true).Where(
                collider => collider != null && collider.enabled &&
                    collider.gameObject.activeInHierarchy && !collider.isTrigger));
        }
        if (colliders.Count == 0)
        {
            error = "the package scene contains no active non-trigger collider";
            return false;
        }

        if (operation.Map.RuntimeTerrain != null)
        {
            Transform ground = FindExactSceneTransform(
                scene,
                operation.Map.RuntimeTerrain.RootObjectName,
                out int matches);
            Terrain terrain = ground == null ? null : ground.GetComponent<Terrain>();
            TerrainCollider terrainCollider =
                ground == null ? null : ground.GetComponent<TerrainCollider>();
            if (matches != 1 || terrain == null || terrainCollider == null ||
                terrain.terrainData == null ||
                terrain.terrainData != terrainCollider.terrainData ||
                terrain.terrainData != operation.RuntimeTerrainData)
            {
                error = "the declared terrain root does not own one shared render/collision TerrainData";
                return false;
            }
        }

        int grounded = 0;
        foreach (Transform marker in markers)
        {
            var ray = new Ray(marker.position + Vector3.up * 64f, Vector3.down);
            bool hitGround = false;
            foreach (Collider collider in colliders)
            {
                if (collider.Raycast(ray, out RaycastHit hit, 256f))
                {
                    hitGround = true;
                    break;
                }
            }
            if (hitGround)
                grounded++;
        }
        if (grounded != markers.Count)
        {
            error = "only " + grounded + " of " + markers.Count +
                " player markers raycast to package-owned walkable collision";
            return false;
        }
        return true;
    }

    private static void ReleaseRuntimeTerrain(ActiveMapOperation operation)
    {
        if (operation == null)
            return;
        if (operation.RuntimeTerrainData != null)
            Object.Destroy(operation.RuntimeTerrainData);
        operation.RuntimeTerrainData = null;
        foreach (TerrainLayer layer in operation.RuntimeTerrainLayers)
        {
            if (layer != null)
                Object.Destroy(layer);
        }
        operation.RuntimeTerrainLayers.Clear();
    }

    private static unsafe Il2CppStructArray<float> AllocateIl2CppFloatArray(
        params int[] dimensions)
    {
        if (dimensions == null || dimensions.Length < 2 || dimensions.Length > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dimensions),
                "Terrain payload arrays must have rank 2 or 3.");
        }
        var lengths = new ulong[dimensions.Length];
        var lowerBounds = new ulong[dimensions.Length];
        for (int index = 0; index < dimensions.Length; index++)
        {
            if (dimensions[index] <= 0)
                throw new ArgumentOutOfRangeException(nameof(dimensions));
            lengths[index] = (ulong)dimensions[index];
        }

        var probe = new Il2CppStructArray<float>(1);
        IntPtr floatClass = IL2CPP.il2cpp_class_get_element_class(probe.ObjectClass);
        IntPtr arrayClass = IL2CPP.il2cpp_array_class_get(
            floatClass,
            (uint)dimensions.Length);
        if (arrayClass == IntPtr.Zero)
            throw new InvalidOperationException("Could not resolve the native float array class.");

        IntPtr arrayPointer;
        fixed (ulong* lengthsPointer = lengths)
        fixed (ulong* lowerBoundsPointer = lowerBounds)
        {
            arrayPointer = IL2CPP.il2cpp_array_new_full(
                arrayClass,
                ref lengthsPointer[0],
                ref lowerBoundsPointer[0]);
        }
        if (arrayPointer == IntPtr.Zero)
            throw new InvalidOperationException("IL2CPP could not allocate a terrain array.");
        return new Il2CppStructArray<float>(arrayPointer);
    }

    private void ConfigureStandalonePlayerSpawnContract(
        Scene scene,
        ActiveMapOperation operation)
    {
        if (operation == null || !scene.IsValid() || !scene.isLoaded)
            return;
        try
        {
            List<Transform> markers = FindStandalonePlayerMarkers(
                scene,
                operation.Operation.Mode);
            if (markers.Count == 0)
                throw new InvalidOperationException(
                    "package scene has no compatible player spawn markers");

            // PlayerMaster.UserCode_CMDSpawnPlayer does not use LastSpawnPoint as
            // its input. It asks GameManager.nextSpawnPosition for a shipped
            // SpawnPoint. Register only package-owned markers in that native
            // list so a prior scene cannot supply a stale or foreign spawn.
            var nativeSpawns =
                new Il2CppSystem.Collections.Generic.List<SpawnPoint>();
            var fallbackObjects = new Il2CppReferenceArray<GameObject>(markers.Count);
            int teamOneCount = 0;
            int teamTwoCount = 0;
            for (int index = 0; index < markers.Count; index++)
            {
                Transform marker = markers[index];
                var spawn = marker.GetComponent<SpawnPoint>() ??
                    marker.gameObject.AddComponent<SpawnPoint>();
                bool teamTwo = IsTeamTwoPlayerMarker(marker.name);
                spawn.CanSpawnPlayer = true;
                // The shipped PVP contract is one-based. PvpGameode assigns
                // Team=1 to Team1SpawnPoints and Team=2 to Team2SpawnPoints,
                // while PlayerMaster passes TeamIdentifier.TeamID unchanged to
                // GameManager.nextSpawnPosition(useTeams: true, teamId).
                spawn.Team = teamTwo ? 2 : 1;
                if (teamTwo)
                    teamTwoCount++;
                else
                    teamOneCount++;
                nativeSpawns.Add(spawn);
                fallbackObjects[index] = marker.gameObject;
            }

            RestoreStandalonePlayerSpawnContract(operation);
            operation.PreviousSpawnPoints = GameManager.SpawnPointsInScene;
            operation.OwnedSpawnPoints = nativeSpawns;
            operation.SpawnContractInstalled = true;
            GameManager.SpawnPointsInScene = nativeSpawns;
            if (GameManager.instance != null)
            {
                operation.PreviousFallbackSpawns = GameManager.instance.Pspawns;
                operation.PreviousNextSpawnIndex = GameManager.instance.PnextSpawnIndex;
                operation.PreviousRandomSpawns = GameManager.instance.RandomSpawns;
                operation.OwnedRandomSpawns = false;
                operation.RandomSpawnsCaptured = true;
                operation.OwnedFallbackSpawns = fallbackObjects;
                GameManager.instance.Pspawns = fallbackObjects;
                // nextSpawnPosition reads the current index, then advances it.
                // A -1 start reaches List<T>.get_Item(-1) in the shipped body.
                GameManager.instance.PnextSpawnIndex = 0;
                // PlayerMaster.UserCode_CMDSpawnPlayer selects
                // nextSpawnPositionRandom when this process-global flag is true.
                // That shipped method ignores SpawnPointsInScene/Pspawns and
                // performs FindGameObjectsWithTag("PlayerSpawn"). Standalone
                // package markers are registered objects, not tag-authored
                // retail scene objects, so the random route returns null. Own
                // false for this operation and restore the prior value on
                // unload/restart.
                GameManager.instance.RandomSpawns = operation.OwnedRandomSpawns;
            }
            log.LogInfo("Standalone registered the package-owned shipped player " +
                "spawn contract: total=" + markers.Count +
                ", team1=" + teamOneCount +
                ", team2=" + teamTwoCount +
                ", gameManager=" + (GameManager.instance != null) +
                ", randomSpawns=" +
                (GameManager.instance == null
                    ? "unavailable"
                    : GameManager.instance.RandomSpawns.ToString()) + ".");
        }
        catch (Exception ex)
        {
            RestoreStandalonePlayerSpawnContract(operation);
            log.LogError("Standalone package player spawn contract failed: " +
                ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool IsTeamTwoPlayerMarker(string markerName)
    {
        string name = markerName ?? string.Empty;
        return name.StartsWith("Team2_Spawn_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Team2_Backup_Spawn_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("PVP_Team2Spawn_", StringComparison.OrdinalIgnoreCase);
    }

    private static void RestoreStandalonePlayerSpawnContract(
        ActiveMapOperation operation)
    {
        if (operation == null || !operation.SpawnContractInstalled)
            return;
        try
        {
            var current = GameManager.SpawnPointsInScene;
            if (SameNativeSpawnList(current, operation.OwnedSpawnPoints))
            {
                GameManager.SpawnPointsInScene =
                    IsUsableSpawnList(operation.PreviousSpawnPoints)
                        ? operation.PreviousSpawnPoints
                        : new Il2CppSystem.Collections.Generic.List<SpawnPoint>();
            }

            var gameManager = GameManager.instance;
            if (gameManager != null &&
                SameNativeGameObjectArray(
                    gameManager.Pspawns,
                    operation.OwnedFallbackSpawns))
            {
                gameManager.Pspawns = IsUsableSpawnArray(operation.PreviousFallbackSpawns)
                    ? operation.PreviousFallbackSpawns
                    : new Il2CppReferenceArray<GameObject>(0);
                gameManager.PnextSpawnIndex = Math.Max(0, operation.PreviousNextSpawnIndex);
            }
            if (gameManager != null && operation.RandomSpawnsCaptured &&
                gameManager.RandomSpawns == operation.OwnedRandomSpawns)
            {
                gameManager.RandomSpawns = operation.PreviousRandomSpawns;
            }
        }
        catch
        {
            // Teardown must not retain package-scene objects merely because a
            // prior native owner was destroyed during the scene transition.
            try
            {
                GameManager.SpawnPointsInScene =
                    new Il2CppSystem.Collections.Generic.List<SpawnPoint>();
                if (GameManager.instance != null)
                {
                    GameManager.instance.Pspawns =
                        new Il2CppReferenceArray<GameObject>(0);
                    GameManager.instance.PnextSpawnIndex = 0;
                    // False is the fail-safe player route. The true route
                    // searches retail PlayerSpawn tags and can return null in
                    // a standalone package scene.
                    GameManager.instance.RandomSpawns = false;
                }
            }
            catch { }
        }
        finally
        {
            operation.PreviousSpawnPoints = null;
            operation.OwnedSpawnPoints = null;
            operation.PreviousFallbackSpawns = null;
            operation.OwnedFallbackSpawns = null;
            operation.PreviousNextSpawnIndex = 0;
            operation.PreviousRandomSpawns = false;
            operation.OwnedRandomSpawns = false;
            operation.RandomSpawnsCaptured = false;
            operation.SpawnContractInstalled = false;
        }
    }

    private static bool SameNativeSpawnList(
        Il2CppSystem.Collections.Generic.List<SpawnPoint> left,
        Il2CppSystem.Collections.Generic.List<SpawnPoint> right)
    {
        if (ReferenceEquals(left, right))
            return true;
        try { return left != null && right != null && left.Pointer == right.Pointer; }
        catch { return false; }
    }

    private static bool SameNativeGameObjectArray(
        Il2CppReferenceArray<GameObject> left,
        Il2CppReferenceArray<GameObject> right)
    {
        if (ReferenceEquals(left, right))
            return true;
        try { return left != null && right != null && left.Pointer == right.Pointer; }
        catch { return false; }
    }

    private static bool IsUsableSpawnList(
        Il2CppSystem.Collections.Generic.List<SpawnPoint> spawns)
    {
        if (spawns == null)
            return false;
        try
        {
            for (int index = 0; index < spawns.Count; index++)
            {
                SpawnPoint spawn = spawns[index];
                if (spawn == null || spawn.gameObject == null ||
                    !spawn.gameObject.scene.IsValid() || !spawn.gameObject.scene.isLoaded)
                    return false;
            }
            return true;
        }
        catch { return false; }
    }

    private static bool IsUsableSpawnArray(
        Il2CppReferenceArray<GameObject> spawns)
    {
        if (spawns == null)
            return false;
        try
        {
            for (int index = 0; index < spawns.Length; index++)
            {
                GameObject spawn = spawns[index];
                if (spawn == null || !spawn.scene.IsValid() || !spawn.scene.isLoaded)
                    return false;
            }
            return true;
        }
        catch { return false; }
    }

    private static bool ValidateStandaloneSceneContract(
        Scene scene,
        ActiveMapOperation operation,
        out string error)
    {
        error = string.Empty;
        if (!HasExactSceneMarker(scene, "MAP_ID_" + operation.Map.Id))
        {
            error = "missing exact MAP_ID_ metadata marker";
            return false;
        }
        if (!HasExactSceneMarker(
                scene,
                "SPAWN_SET_" + operation.Operation.SpawnSetId))
        {
            error = "missing declared SPAWN_SET_ metadata marker";
            return false;
        }

        List<Transform> playerMarkers = FindStandalonePlayerMarkers(
            scene,
            operation.Operation.Mode);
        if (playerMarkers.Count == 0)
        {
            error = "no compatible player spawn markers";
            return false;
        }
        if (operation.Operation.Mode == ModdedOperationMode.PlayerVersusEnvironment &&
            FindSceneMarkers(scene, "PVE_EnemySpawn_").Count == 0)
        {
            error = "PVE mode has no PVE_EnemySpawn_ markers";
            return false;
        }
        if (operation.Operation.Mode == ModdedOperationMode.PlayerVersusEnvironment)
        {
            List<Transform> exfilMarkers = FindSceneMarkers(
                scene,
                StandalonePveExfilMarkerPrefix);
            if (exfilMarkers.Count != 1)
            {
                error = "PVE mode requires exactly one PVE_ExfilZone_ marker; found=" +
                    exfilMarkers.Count;
                return false;
            }
            BoxCollider authoredExfil = exfilMarkers[0].GetComponent<BoxCollider>();
            if (authoredExfil == null || !authoredExfil.isTrigger ||
                authoredExfil.size.x <= 0f || authoredExfil.size.y <= 0f ||
                authoredExfil.size.z <= 0f)
            {
                error = "PVE_ExfilZone_ marker requires a positive BoxCollider trigger";
                return false;
            }
        }
        if (operation.Operation.Mode == ModdedOperationMode.PlayerVersusPlayer)
        {
            bool team1 = playerMarkers.Any(marker =>
                marker.name.StartsWith("Team1", StringComparison.OrdinalIgnoreCase) ||
                marker.name.StartsWith("PVP_Team1", StringComparison.OrdinalIgnoreCase));
            bool team2 = playerMarkers.Any(marker =>
                marker.name.StartsWith("Team2", StringComparison.OrdinalIgnoreCase) ||
                marker.name.StartsWith("PVP_Team2", StringComparison.OrdinalIgnoreCase));
            if (!team1 || !team2)
            {
                error = "PVP mode requires separated Team1 and Team2 spawn markers";
                return false;
            }
        }
        bool hasDirectionalLight = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponentsInChildren<Light>(true).Any(light =>
                    light != null && light.type == LightType.Directional))
            {
                hasDirectionalLight = true;
                break;
            }
        }
        if (!hasDirectionalLight)
        {
            error = "no package-owned fallback directional light";
            return false;
        }
        return true;
    }

    private static bool HasExactSceneMarker(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
            {
                if (item != null && string.Equals(
                        item.name,
                        name,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void CreateStandaloneGameplayBootstrap(
        Scene scene,
        ActiveMapOperation operation)
    {
        if (operation == null || operation.BootstrapCreated)
            return;
        try
        {
            // Every peer constructs the same inactive template before the host
            // publishes it. A bare runtime NetworkIdentity with assetId=0 works
            // on a host client but gives a remote client no prefab or sceneId to
            // instantiate. Registering this deterministic template makes the
            // shipped GameMode/PvpGameode SyncVars and RPCs real peer state.
            var root = new GameObject("MODDED_OPERATIONS_GAME_MODE_TEMPLATE");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var identity = root.AddComponent<NetworkIdentity>();
            global::GameMode gameMode;
            uint assetId;
            if (operation.Operation.Mode ==
                ModdedOperationMode.PlayerVersusEnvironment)
            {
                var pveGameMode = root.AddComponent<StandalonePveGameMode>();
                // Suppress the retail component's scene-specific Update. The
                // persistent Cerberus runner maintains only the state required
                // by the shipped standalone lifecycle.
                pveGameMode.enabled = false;
                pveGameMode.AllowRespawns = false;
                pveGameMode.NetworkRaidTimer = 0f;
                InfiltrationManager.instance = pveGameMode;
                ConfigureStandalonePveController(
                    scene,
                    operation,
                    root,
                    pveGameMode);
                gameMode = pveGameMode;
                assetId = StandalonePveGameModeAssetId;
            }
            else
            {
                var pvpGameMode = root.AddComponent<StandalonePvpGameMode>();
                ConfigureStandalonePvpController(scene, operation, root, pvpGameMode);
                gameMode = pvpGameMode;
                assetId = StandalonePvpGameModeAssetId;
            }
            gameMode.isNight = ParseTimeHour(operation.TimeCode) < 6;
            global::GameMode.singleton = gameMode;
            operation.BootstrapRoot = root;
            operation.BootstrapIdentity = identity;
            operation.BootstrapPrefabRoot = root;
            operation.BootstrapPrefabIdentity = identity;
            operation.BootstrapAssetId = assetId;
            operation.GameModeComponent = gameMode;
            operation.BootstrapCreated = true;
            operation.BootstrapFrame = Time.frameCount;
            identity.assetId = assetId;
            // Runtime-added IL2CPP NetworkBehaviours complete part of their
            // Mirror/native initialization on their first activation. Vanilla
            // serialized prefabs have already passed this lifecycle before
            // registration. Give the deterministic runtime template the same
            // activation edge, then return it inactive before RegisterPrefab.
            root.SetActive(true);
            root.SetActive(false);
            var mirrorStates = root.GetComponents<NetworkBehaviour>()
                .Select(item => item == null
                    ? "<null-component>"
                    : item.GetType().Name + ":syncObjects=" +
                        (item.syncObjects == null
                            ? "null"
                            : item.syncObjects.Count.ToString()))
                .ToArray();
            log.LogInfo("Standalone runtime Mirror prefab prewarm state: " +
                string.Join(", ", mirrorStates) + ".");
            EnsureStandaloneBootstrapPrefabRegistered(operation);

            if (OperationsManager.singleton != null)
            {
                // A package operation selected from Modded Operations is an
                // active operation unless its own catalog mode says otherwise.
                // Clear the persistent retail simulation flag before assigning
                // the shipped game mode. StandardPVE.UpdateAICount suppresses
                // extraction completion while IsSimulation is true.
                OperationsManager.singleton.IsSimulation = false;
                if (operation.Operation.Mode ==
                    ModdedOperationMode.PlayerVersusEnvironment)
                {
                    OperationsManager.singleton.NetworkCurrentGameMode =
                        OperationsManager.GameMode.StandardPVE;
                    OperationsManager.singleton.AssignTeamsPVE();
                }
                else
                {
                    OperationsManager.singleton.AssignTeamsTDM();
                }
            }
            log.LogInfo("Standalone gameplay bootstrap created in package scene: map=" +
                operation.Map.Id + ", mode=" + operation.Operation.Mode +
                ", owner=" + gameMode.GetType().Name +
                ", mirrorAssetId=0x" + assetId.ToString("X8") +
                ", prefabRegistered=" + operation.BootstrapPrefabRegistered +
                ", donorScene=false, sceneHandle=" + scene.handle + ".");
        }
        catch (Exception ex)
        {
            log.LogError("Standalone gameplay bootstrap failed: " +
                ex.GetType().Name + ": " + ex.Message);
        }
    }

    private void ConfigureStandalonePveController(
        Scene scene,
        ActiveMapOperation operation,
        GameObject bootstrapRoot,
        StandalonePveGameMode pve)
    {
        if (operation == null || bootstrapRoot == null || pve == null)
            throw new ArgumentNullException(nameof(pve));

        List<Transform> markers = FindSceneMarkers(
            scene,
            StandalonePveExfilMarkerPrefix);
        if (markers.Count != 1)
        {
            throw new InvalidOperationException(
                "StandardPVE requires exactly one package-authored " +
                "PVE_ExfilZone_ marker; found=" + markers.Count);
        }
        Transform marker = markers[0];
        BoxCollider authoredCollider = marker.GetComponent<BoxCollider>();
        if (authoredCollider == null || !authoredCollider.isTrigger)
        {
            throw new InvalidOperationException(
                marker.name + " requires a BoxCollider trigger");
        }

        bootstrapRoot.layer = marker.gameObject.layer;
        bootstrapRoot.transform.SetPositionAndRotation(
            marker.position,
            marker.rotation);

        var trigger = bootstrapRoot.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = authoredCollider.center;
        trigger.size = authoredCollider.size;

        var lockedMarker = new GameObject("MODDED_PVE_EXFIL_LOCKED_MARKER");
        lockedMarker.transform.SetParent(bootstrapRoot.transform, false);
        lockedMarker.SetActive(false);
        GameObject availableMarker = CreateNativeAtakExfilMarker(
            operation,
            bootstrapRoot.transform);
        availableMarker.SetActive(false);

        var exfil = bootstrapRoot.AddComponent<ExfilZone>();
        exfil.unlockingKey = string.Empty;
        exfil.notificationCooldown = 20f;
        exfil._notifT = 0f;
        exfil.isHelicopter = false;
        exfil.InfiltrationAnimationPrefab = null;
        exfil.exfilName = operation.Operation.DisplayName + " Extraction";
        exfil.ExfilAnimationName = string.Empty;
        exfil.exfilSpawned = false;
        exfil.InfilMarker = lockedMarker;
        exfil.ExfilMarker = availableMarker;
        exfil._occupants = new Il2CppSystem.Collections.Generic.HashSet<int>();
        exfil.NetworkPlayersInExfil = 0;
        exfil.NetworkcanExtract = false;
        exfil.linkedInfils = new Il2CppSystem.Collections.Generic.List<string>();
        if (operation.Operation.Infiltrations != null)
        {
            for (int index = 0;
                 index < operation.Operation.Infiltrations.Count;
                 index++)
            {
                string infilName =
                    operation.Operation.Infiltrations[index].DisplayName;
                if (!string.IsNullOrWhiteSpace(infilName))
                    exfil.linkedInfils.Add(infilName);
            }
        }

        var raid = bootstrapRoot.AddComponent<RaidManager>();
        raid.enabled = false;
        raid.infiltrationManager = pve;
        raid.EXTRACT_TIMER = StandalonePveExtractionSeconds;
        raid.objectives = new Il2CppReferenceArray<ObjectiveSetter>(0);
        raid.missionAssets =
            new Il2CppSystem.Collections.Generic.List<VehicleHealth>();
        raid.standardAI = new Il2CppReferenceArray<GameObject>(0);
        raid.customAI =
            new Il2CppSystem.Collections.Generic.List<GameObject>();
        raid.hvtSpawnPoints = new Il2CppReferenceArray<GameObject>(0);
        raid.hvtAI = new Il2CppReferenceArray<GameObject>(0);
        raid.staticVehicleSpawnPoints = new Il2CppReferenceArray<GameObject>(0);
        raid.staticVehicleAI = new Il2CppReferenceArray<GameObject>(0);
        raid.Reinforcements = new Il2CppReferenceArray<aiReinforcement>(0);
        raid.prohibitedWeapons = new Il2CppReferenceArray<PuppetWeapon>(0);
        raid.mapSpecificWeapons = new Il2CppReferenceArray<PuppetWeapon>(0);
        raid.IED_locations = new Il2CppReferenceArray<Transform>(0);
        raid.botSpawnPoints =
            new Il2CppSystem.Collections.Generic.List<GameObject>();
        raid.allHelicopters =
            new Il2CppSystem.Collections.Generic.List<HelicopterV2>();
        raid.objectiveObjects =
            new Il2CppSystem.Collections.Generic.List<ObjectiveObject>();
        raid.ImportantObjectiveObjects =
            new Il2CppSystem.Collections.Generic.List<ObjectiveObject>();
        raid.spawnVehicleAI = false;
        raid.hasReinforcements = false;
        raid.timedBackup = false;
        raid.hasIEDs = false;
        raid.hasNotified = false;
        raid.hasNotifiedEnemiesDead = false;
        raid.exfilZones = new Il2CppSystem.Collections.Generic.List<ExfilZone>();
        raid.exfilZones.Add(exfil);
        RaidManager.singleton = raid;

        operation.PveRaidManager = raid;
        operation.PveExfilZone = exfil;
        operation.PveExfilCollider = trigger;
        ResetStandalonePveExtractionState();
        log.LogInfo("Standalone StandardPVE owner wired to shipped RaidManager and " +
            "ExfilZone: marker=" + marker.name +
            ", position=" + marker.position +
            ", triggerCenter=" + trigger.center +
            ", triggerSize=" + trigger.size +
            ", extractionSeconds=" + StandalonePveExtractionSeconds + ".");
    }

    private static GameObject CreateNativeAtakExfilMarker(
        ActiveMapOperation operation,
        Transform parent)
    {
        if (operation == null || parent == null)
            throw new ArgumentNullException(nameof(parent));

        Texture2D exfilTexture = Resources.FindObjectsOfTypeAll<Texture2D>()
            .FirstOrDefault(texture =>
                texture != null &&
                string.Equals(texture.name, "ExfilZone", StringComparison.Ordinal) &&
                texture.width == 512 && texture.height == 512);
        if (exfilTexture == null)
        {
            throw new InvalidOperationException(
                "The resident vanilla 512x512 ExfilZone texture is unavailable");
        }
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
            throw new InvalidOperationException("The resident HDRP/Unlit shader is unavailable");

        var marker = new GameObject("ATAK Exfil Marker");
        marker.layer = 17;
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = Vector3.zero;
        marker.transform.localRotation = new Quaternion(
            -3.0159049e-7f,
            -0.70710683f,
            -0.70710677f,
            3.2782552e-7f);
        marker.transform.localScale = Vector3.one * 0.65f;

        var mesh = new Mesh
        {
            name = "Marker"
        };
        mesh.vertices = new[]
        {
            new Vector3(-9.59999943f, -5.40000010f, 0f),
            new Vector3(-9.59999943f,  5.40000010f, 0f),
            new Vector3( 9.59999943f,  5.40000010f, 0f),
            new Vector3( 9.59999943f, -5.40000010f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0.000227630138f, 0.437887728f),
            new Vector2(0.000227630138f, 1.00013173f),
            new Vector2(0.999772370000f, 1.00013173f),
            new Vector2(0.999772370000f, 0.437887728f)
        };
        mesh.normals = new[]
        {
            Vector3.forward,
            Vector3.forward,
            Vector3.forward,
            Vector3.forward
        };
        mesh.triangles = new[] { 2, 1, 0, 3, 2, 0 };
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);

        var material = new Material(shader)
        {
            name = "ExfilZone",
            enableInstancing = true,
            renderQueue = 2501
        };
        material.SetTexture("_MainTex", exfilTexture);
        material.SetTexture("_UnlitColorMap", exfilTexture);
        material.SetTextureOffset("_MainTex", new Vector2(0f, -0.22f));
        material.SetTextureOffset("_UnlitColorMap", new Vector2(0f, -0.22f));
        material.SetColor("_Color", Color.white);
        material.SetColor("_UnlitColor", Color.white);
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_EmissionColor", Color.white);
        material.SetFloat("_SurfaceType", 0f);
        material.SetFloat("_BlendMode", 0f);
        material.SetFloat("_ZWrite", 1f);
        material.SetFloat("_CullMode", 2f);
        material.SetFloat("_OpaqueCullMode", 2f);
        material.SetFloat("_AlphaCutoffEnable", 0f);
        material.SetFloat("_Smoothness", 1f);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.SetShaderPassEnabled("DistortionVectors", false);
        material.SetShaderPassEnabled("MOTIONVECTORS", false);

        MeshFilter filter = marker.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = marker.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;

        operation.RuntimePveAssets.Add(mesh);
        operation.RuntimePveAssets.Add(material);
        return marker;
    }

    private static void ResetStandalonePveExtractionState()
    {
        if (!NetworkServer.active)
            return;
        GameManagerNetwork manager = GameManagerNetwork.instance;
        if (manager == null)
            return;
        if (manager._globalExfilOccupants == null)
        {
            manager._globalExfilOccupants =
                new Il2CppSystem.Collections.Generic.HashSet<int>();
        }
        else
        {
            manager._globalExfilOccupants.Clear();
        }
        manager.NetworkPlayersInAnyExfil = 0;
        manager.NetworkcanExtract = false;
        manager.NetworkisExtracting = false;
        manager.NetworkextractionStartTime = 0d;
        manager.ExfilTime = StandalonePveExtractionSeconds;
        manager.SuccessfulOperation = false;
    }

    private void EnsureStandaloneBootstrapPrefabRegistered(
        ActiveMapOperation operation)
    {
        if (operation == null || operation.BootstrapPrefabRegistered ||
            !NetworkClient.active || operation.BootstrapPrefabRoot == null ||
            operation.BootstrapPrefabIdentity == null ||
            operation.BootstrapAssetId == 0)
        {
            return;
        }

        var prefabs = NetworkClient.prefabs;
        if (prefabs != null &&
            prefabs.TryGetValue(operation.BootstrapAssetId, out GameObject existing))
        {
            // Unity's destroyed-object wrapper compares equal to null but is
            // still a managed dictionary value. Mirror dereferences that stale
            // value while replacing the registration and throws from
            // UnityEngine.Object.GetName. Scene unload can destroy the
            // scene-owned template before our callback runs, so remove this
            // exact package-owned key without touching any vanilla prefab.
            if (existing == null)
            {
                prefabs.Remove(operation.BootstrapAssetId);
                NetworkClient.UnregisterSpawnHandler(operation.BootstrapAssetId);
                instance?.log?.LogInfo(
                    "Removed destroyed standalone game-mode Mirror prefab before repeat registration: " +
                    "assetId=0x" + operation.BootstrapAssetId.ToString("X8") + ".");
            }
            else if (existing == operation.BootstrapPrefabRoot)
            {
                operation.BootstrapPrefabRegistered = true;
                operation.BootstrapPrefabIdentity.assetId = operation.BootstrapAssetId;
                return;
            }
            else
            {
                throw new InvalidOperationException(
                    "Mirror prefab asset ID collision for standalone game mode 0x" +
                    operation.BootstrapAssetId.ToString("X8") +
                    ": existing=" + existing.name + ".");
            }
        }

        NetworkClient.RegisterPrefab(
            operation.BootstrapPrefabRoot,
            operation.BootstrapAssetId);
        operation.BootstrapPrefabIdentity.assetId = operation.BootstrapAssetId;
        operation.BootstrapPrefabRegistered = true;
        log.LogInfo("Standalone game-mode Mirror prefab registered on this peer: " +
            "assetId=0x" + operation.BootstrapAssetId.ToString("X8") +
            ", mode=" + operation.Operation.Mode + ".");
    }

    private void ConfigureStandalonePvpController(
        Scene scene,
        ActiveMapOperation operation,
        GameObject bootstrapRoot,
        StandalonePvpGameMode pvp)
    {
        if (operation == null || bootstrapRoot == null || pvp == null)
            throw new ArgumentNullException(nameof(pvp));

        var team1 = new Il2CppSystem.Collections.Generic.List<SpawnPoint>();
        var team2 = new Il2CppSystem.Collections.Generic.List<SpawnPoint>();
        foreach (Transform marker in FindStandalonePlayerMarkers(
                     scene,
                     ModdedOperationMode.PlayerVersusPlayer))
        {
            if (marker == null)
                continue;
            SpawnPoint spawn = marker.GetComponent<SpawnPoint>() ??
                marker.gameObject.AddComponent<SpawnPoint>();
            bool isTeam2 = IsTeamTwoPlayerMarker(marker.name);
            spawn.CanSpawnPlayer = true;
            spawn.Team = isTeam2 ? 2 : 1;
            if (isTeam2)
                team2.Add(spawn);
            else
                team1.Add(spawn);
        }
        if (team1.Count == 0 || team2.Count == 0)
        {
            throw new InvalidOperationException(
                "PvpGameode requires non-empty Team1SpawnPoints and Team2SpawnPoints lists");
        }

        // Exact serialized defaults from the current-build StandardPVP scene,
        // build index 30, PvpGameode path ID 34510. The retail server overwrites
        // MaxRounds and RoundTime from its PVP lobby settings in
        // Server_AllPlayersLoaded; the other values remain the authored seed.
        pvp.Team1SpawnPoints = team1;
        pvp.Team2SpawnPoints = team2;
        pvp.MaxRounds = 13;
        pvp.RoundsToWin = 7;
        pvp.currentRound = 0;
        pvp.RoundTime = 120;
        pvp.RoundTimer = 0;
        pvp.Team1Score = 0;
        pvp.Team2Score = 0;
        pvp.CurrentScoreUI = 0;
        pvp.Team1Players = new Il2CppSystem.Collections.Generic.List<TeamIdentifier>();
        pvp.Team2Players = new Il2CppSystem.Collections.Generic.List<TeamIdentifier>();
        pvp._roundEnded = false;
        pvp._roundEndTimer = 0f;
        pvp._waitingForPlayerSpawn = false;
        pvp._waitingForPlayerSpawnTimer = 0f;
        pvp._respawningPlayers = false;
        pvp._freezeTimer = 0f;
        pvp._isFreezeTime = false;
        pvp._roundActive = false;

        ConfigureStandalonePvpPresentation(operation, bootstrapRoot, pvp);
        PvpGameode.instance = pvp;
        log.LogInfo("Standalone StandardPVP owner wired to shipped PvpGameode: " +
            "team1Spawns=" + team1.Count + ", team2Spawns=" + team2.Count +
            ", MaxRounds=13, RoundsToWin=7, RoundTime=120.");
    }

    private static void ConfigureStandalonePvpPresentation(
        ActiveMapOperation operation,
        GameObject bootstrapRoot,
        StandalonePvpGameMode pvp)
    {
        var musicSource = bootstrapRoot.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = false;
        var announcerSource = bootstrapRoot.AddComponent<AudioSource>();
        announcerSource.playOnAwake = false;
        announcerSource.loop = false;
        pvp.MusicSource = musicSource;
        pvp.AnnouncerSource = announcerSource;

        AudioClip silence = AudioClip.Create(
            "MODDED_PVP_SILENT_ANNOUNCER",
            1,
            1,
            48000,
            false);
        operation.RuntimePvpAssets.Add(silence);
        pvp.bluforSpawn = CreatePvpClipArray(silence, 3);
        pvp.bluforSpawnShort = CreatePvpClipArray(silence, 3);
        pvp.bluforRoundWin = CreatePvpClipArray(silence, 3);
        pvp.bluforGameWin = CreatePvpClipArray(silence, 3);
        pvp.bluforGameLose = CreatePvpClipArray(silence, 3);
        pvp.bluforRoundLose = CreatePvpClipArray(silence, 3);
        pvp.bluforGameDraw = CreatePvpClipArray(silence, 1);
        pvp.bluforRoundDraw = CreatePvpClipArray(silence, 1);
        pvp.opforSpawn = CreatePvpClipArray(silence, 3);
        pvp.opforSpawnShort = CreatePvpClipArray(silence, 3);
        pvp.opforRoundWin = CreatePvpClipArray(silence, 3);
        pvp.opforGameWin = CreatePvpClipArray(silence, 3);
        pvp.opforGameLose = CreatePvpClipArray(silence, 3);
        pvp.opforRoundLose = CreatePvpClipArray(silence, 3);
        pvp.opforGameDraw = CreatePvpClipArray(silence, 1);
        pvp.opforRoundDraw = CreatePvpClipArray(silence, 1);

        var uiRoot = new GameObject("MODDED_PVP_NATIVE_UI");
        uiRoot.layer = 5;
        uiRoot.transform.SetParent(bootstrapRoot.transform, false);
        var canvas = uiRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 160;
        var scaler = uiRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        TextMeshProUGUI bluforScore = CreatePvpLabel(
            uiRoot.transform,
            "BLUFOR_SCORE",
            "0",
            new Vector2(0.42f, 0.94f),
            new Vector2(0.48f, 0.99f),
            28f);
        TextMeshProUGUI clock = CreatePvpLabel(
            uiRoot.transform,
            "ROUND_CLOCK",
            "02:00",
            new Vector2(0.48f, 0.94f),
            new Vector2(0.52f, 0.99f),
            28f);
        TextMeshProUGUI opforScore = CreatePvpLabel(
            uiRoot.transform,
            "OPFOR_SCORE",
            "0",
            new Vector2(0.52f, 0.94f),
            new Vector2(0.58f, 0.99f),
            28f);
        pvp.bluforScore = bluforScore;
        pvp.opforScore = opforScore;
        pvp.clock = clock;
        pvp.GameUI_bluforScore = bluforScore;
        pvp.GameUI_opforScore = opforScore;

        TextMeshProUGUI causeLabel = CreatePvpLabel(
            uiRoot.transform,
            "ROUND_CAUSE",
            string.Empty,
            new Vector2(0.25f, 0.38f),
            new Vector2(0.75f, 0.48f),
            30f);
        var cause = causeLabel.gameObject.AddComponent<TMPro.Examples.TeleType>();
        cause.m_textMeshPro = causeLabel;
        cause.autoReveal = false;
        cause.label01 = string.Empty;
        cause.label02 = string.Empty;
        pvp.Cause = cause;

        var roundUi = CreatePvpUiGroup(uiRoot.transform, "ROUND_END_UI");
        pvp.RoundEndAnimator = roundUi.AddComponent<Animator>();
        pvp.RoundWinImage = CreatePvpOutcome(
            roundUi.transform,
            "ROUND_WIN",
            "ROUND WON");
        pvp.RoundLoseImage = CreatePvpOutcome(
            roundUi.transform,
            "ROUND_LOSS",
            "ROUND LOST");
        pvp.RoundDrawImage = CreatePvpOutcome(
            roundUi.transform,
            "ROUND_DRAW",
            "ROUND DRAW");

        var gameUi = CreatePvpUiGroup(uiRoot.transform, "GAME_END_UI");
        pvp.GameUiAnimator = gameUi.AddComponent<Animator>();
        pvp.GameWinImage = CreatePvpOutcome(
            gameUi.transform,
            "GAME_VICTORY",
            "VICTORY");
        pvp.GameLoseImage = CreatePvpOutcome(
            gameUi.transform,
            "GAME_DEFEAT",
            "DEFEAT");
        pvp.GameDrawImage = CreatePvpOutcome(
            gameUi.transform,
            "GAME_DRAW",
            "GAME DRAW");
        pvp.GameUI_Winning = CreatePvpLabel(
            gameUi.transform,
            "GAME_UI_WINNING",
            "WINNING",
            new Vector2(0.35f, 0.80f),
            new Vector2(0.65f, 0.87f),
            26f);
        pvp.GameUI_Losing = CreatePvpLabel(
            gameUi.transform,
            "GAME_UI_LOSING",
            "LOSING",
            new Vector2(0.35f, 0.80f),
            new Vector2(0.65f, 0.87f),
            26f);
        pvp.GameUI_Tie = CreatePvpLabel(
            gameUi.transform,
            "GAME_UI_TIE",
            "TIED",
            new Vector2(0.35f, 0.80f),
            new Vector2(0.65f, 0.87f),
            26f);
        pvp.GameUI_Winning.gameObject.SetActive(false);
        pvp.GameUI_Losing.gameObject.SetActive(false);
        pvp.GameUI_Tie.gameObject.SetActive(false);
        pvp.FadeOut = "FadeOut";
        pvp.FadeIn = "FadeIn";
    }

    private static Il2CppReferenceArray<AudioClip> CreatePvpClipArray(
        AudioClip clip,
        int count)
    {
        var clips = new Il2CppReferenceArray<AudioClip>(count);
        for (int index = 0; index < count; index++)
            clips[index] = clip;
        return clips;
    }

    private static GameObject CreatePvpUiGroup(Transform parent, string name)
    {
        var group = new GameObject(name);
        group.layer = 5;
        group.transform.SetParent(parent, false);
        return group;
    }

    private static GameObject CreatePvpOutcome(
        Transform parent,
        string name,
        string text)
    {
        var root = CreatePvpUiGroup(parent, name);
        CreatePvpLabel(
            root.transform,
            name + "_TEXT",
            text,
            new Vector2(0.30f, 0.50f),
            new Vector2(0.70f, 0.62f),
            48f);
        root.SetActive(false);
        return root;
    }

    private static TextMeshProUGUI CreatePvpLabel(
        Transform parent,
        string name,
        string text,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize)
    {
        var root = new GameObject(name);
        root.layer = 5;
        root.transform.SetParent(parent, false);
        var label = root.AddComponent<TextMeshProUGUI>();
        label.text = text ?? string.Empty;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        return label;
    }

    private static int ParseTimeHour(string timeCode)
    {
        if (string.IsNullOrEmpty(timeCode) || timeCode.Length != 4 ||
            !int.TryParse(timeCode.Substring(0, 2), out int hour))
        {
            return 11;
        }
        return hour;
    }

    private void ApplyStandaloneRenderContract(
        Scene scene,
        ActiveMapOperation operation)
    {
        if (operation == null || !scene.IsValid() || !scene.isLoaded)
            return;
        if (!HasExactSceneMarker(scene, "RENDER_PROFILE_NATIVE_OUTDOOR_V1"))
        {
            ApplySceneAuthoredRenderContract(scene, operation);
            return;
        }
        try
        {
            Light sun = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light != null && light.type == LightType.Directional)
                    {
                        sun = light;
                        break;
                    }
                }
                if (sun != null)
                    break;
            }
            if (sun == null)
            {
                var sunRoot = new GameObject("MODDED_OPERATIONS_DIRECTIONAL_LIGHT");
                SceneManager.MoveGameObjectToScene(sunRoot, scene);
                sun = sunRoot.AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            bool night = ParseTimeHour(operation.TimeCode) < 6;
            if (night && GameManager.instance != null)
            {
                if (!operation.NvgColorCaptured)
                {
                    operation.PreviousNvgColor = GameManager.instance.NVGColor;
                    operation.NvgColorCaptured = true;
                }
                // Current-build GameManager.SetNVGColor maps 0 to the shipped
                // WhitePhosper Color32 and 1 to GreenPhosper. Keep the mission
                // override scoped to this operation and restore it on unload.
                GameManager.instance.SetNVGColor(0);
                operation.WhitePhosphorApplied = true;
            }
            sun.transform.rotation = night
                ? new Quaternion(0.11686850f, 0.63860989f, -0.67745668f, 0.34579131f)
                : new Quaternion(0.115319036f, 0.019461127f, 0.118037127f, 0.986098409f);
            sun.color = Color.white;
            sun.useColorTemperature = true;
            sun.colorTemperature = night ? 9754f : 5500f;
            sun.intensity = night ? 40f : 30000f;
            sun.bounceIntensity = night ? 1f : 5f;
            sun.shadows = LightShadows.Soft;
            sun.shadowResolution = LightShadowResolution.VeryHigh;
            sun.shadowStrength = 1f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            sun.shadowNearPlane = 0.2f;
            var hdSun = sun.GetComponent<HDAdditionalLightData>() ??
                sun.gameObject.AddComponent<HDAdditionalLightData>();
            hdSun.lightUnit = LightUnit.Lux;
            hdSun.intensity = sun.intensity;
            hdSun.volumetricDimmer = 1f;
            hdSun.angularDiameter = 0.5f;

            if (night)
            {
                var ambientRoot = new GameObject("MODDED_OPERATIONS_NIGHT_AMBIENT");
                SceneManager.MoveGameObjectToScene(ambientRoot, scene);
                ambientRoot.transform.rotation = new Quaternion(
                    0.96095735f,
                    0.14374454f,
                    0.22723518f,
                    -0.06528974f);
                var ambient = ambientRoot.AddComponent<Light>();
                ambient.type = LightType.Directional;
                ambient.color = Color.white;
                ambient.useColorTemperature = true;
                ambient.colorTemperature = 6570f;
                ambient.intensity = 3500f;
                ambient.bounceIntensity = 1f;
                ambient.shadows = LightShadows.None;
                var hdAmbient = ambientRoot.AddComponent<HDAdditionalLightData>();
                hdAmbient.lightUnit = LightUnit.Lux;
                hdAmbient.intensity = 3500f;
                hdAmbient.volumetricDimmer = 1f;
            }

            var environment = new GameObject("MODDED_OPERATIONS_OUTDOOR_ENVIRONMENT");
            SceneManager.MoveGameObjectToScene(environment, scene);
            var volume = environment.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 500000f;
            volume.weight = 1f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "MODDED_OPERATIONS_OUTDOOR_PROFILE";
            volume.sharedProfile = profile;
            operation.RuntimeRenderProfiles.Add(profile);

            var visual = profile.Add<VisualEnvironment>(true);
            visual.skyType.Override((int)SkyType.PhysicallyBased);
            visual.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            visual.renderingSpace.Override(RenderingSpace.Camera);
            visual.windOrientation.Override(18f);
            visual.windSpeed.Override(14f);
            var sky = profile.Add<PhysicallyBasedSky>(true);
            sky.type.Override(PhysicallyBasedSkyModel.EarthSimple);
            sky.atmosphericScattering.Override(true);
            sky.aerosolDensity.Override(0.18f);
            sky.groundTint.Override(new Color(0.13f, 0.105f, 0.075f, 1f));
            sky.horizonTint.Override(new Color(0.96f, 0.98f, 1f, 1f));
            sky.zenithTint.Override(new Color(0.84f, 0.91f, 1f, 1f));

            var exposure = profile.Add<Exposure>(false);
            exposure.mode.Override(ExposureMode.AutomaticHistogram);
            exposure.meteringMode.Override(
                night ? (MeteringMode)4 : MeteringMode.CenterWeighted);
            exposure.fixedExposure.Override(night ? 8.32f : 10f);
            // level7/PVP-map `PVP map NIight VOLUME` path 440 is the
            // authoritative night exposure source. Do not combine the day
            // histogram limits with another negative exposure adjustment.
            exposure.compensation.Override(night ? 1.16f : 0f);
            exposure.limitMin.Override(night ? 5.0652819f : 8.5f);
            exposure.limitMax.Override(night ? 9.3485708f : 11f);
            exposure.adaptationSpeedDarkToLight.Override(night ? 3f : 0.5f);
            exposure.adaptationSpeedLightToDark.Override(night ? 3f : 0.5f);

            var tonemapping = profile.Add<Tonemapping>(false);
            Texture3D lut = night ? null : LoadPackageTonemapLut(operation.Map);
            if (night)
            {
                // The installed level7 night profile uses ACES (enum value 2)
                // and no external LUT. The day AgX LUT crushes the low-light
                // signal before OPERATOR's NVG custom pass can amplify it.
                tonemapping.mode.Override(TonemappingMode.ACES);
                tonemapping.useFullACES.Override(true);
            }
            else if (lut != null)
            {
                tonemapping.mode.Override(TonemappingMode.External);
                tonemapping.useFullACES.Override(false);
                tonemapping.lutTexture.Override(lut);
            }
            else
            {
                tonemapping.mode.Override(TonemappingMode.ACES);
                tonemapping.useFullACES.Override(true);
            }
            var bloom = profile.Add<Bloom>(false);
            bloom.quality.Override(night ? 1 : 3);
            bloom.intensity.Override(night ? 0.3f : 0.03f);
            bloom.threshold.Override(0.900f);
            bloom.scatter.Override(night ? 0.2f : 0.893f);
            bloom.anamorphic.Override(false);
            if (night)
                bloom.m_Resolution.Override(BloomResolution.Half);
            var lensFlare = profile.Add<ScreenSpaceLensFlare>(false);
            lensFlare.intensity.Override(night ? 1f : 0.5f);
            lensFlare.streaksIntensity.Override(night ? 1f : 1.55f);
            lensFlare.streaksLength.Override(night ? 0.091f : 0.022f);
            if (!night)
            {
                lensFlare.streaksOrientation.Override(0f);
                lensFlare.chromaticAbberationIntensity.Override(0.6f);
            }
            var color = profile.Add<ColorAdjustments>(false);
            color.postExposure.Override(night ? 0f : -0.3f);
            color.contrast.Override(night ? 17.3f : 30f);
            color.saturation.Override(night ? 22f : -15f);
            if (!night)
            {
                var whiteBalance = profile.Add<WhiteBalance>(false);
                whiteBalance.temperature.Override(-3.6f);
                whiteBalance.tint.Override(-8.6f);

                var liftGammaGain = profile.Add<LiftGammaGain>(false);
                liftGammaGain.lift.Override(new Vector4(1f, 1f, 1f, 0.00827304f));
                liftGammaGain.gamma.Override(new Vector4(1f, 1f, 1f, -0.09100296f));
                liftGammaGain.gain.Override(new Vector4(1f, 1f, 1f, 0.09100296f));
            }
            if (night)
            {
                var indirect = profile.Add<IndirectLightingController>(false);
                indirect.indirectDiffuseLightingMultiplier.Override(1f);
                indirect.reflectionLightingMultiplier.Override(1f);
                indirect.reflectionProbeIntensityMultiplier.Override(1f);
            }
            var shadows = profile.Add<HDShadowSettings>(false);
            shadows.maxShadowDistance.Override(night ? 200f : 125f);
            if (night)
                shadows.cascadeShadowSplit0.Override(0.05f);

            log.LogInfo("Standalone render contract applied from package-owned " +
                "scene plus its explicit native-outdoor-v1 profile marker: time=" +
                operation.TimeCode + ", sunLux=" + sun.intensity +
                ", sunTemperature=" + sun.colorTemperature +
                ", sunBounce=" + sun.bounceIntensity +
                ", profileSource=" + (night ? "PVP-map night" : "PVP Woods Warehouse day") +
                ", bloom=" + (night ? 0.3f : 0.03f) +
                ", lensFlare=" + (night ? 1f : 0.5f) +
                ", nightAmbient=" + night +
                ", whitePhosphor=" + operation.WhitePhosphorApplied +
                ", externalLut=" + (lut != null) + ".");
        }
        catch (Exception ex)
        {
            log.LogWarning("Standalone HDRP render contract fell back to the " +
                "scene-authored light: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private void ApplySceneAuthoredRenderContract(
        Scene scene,
        ActiveMapOperation operation)
    {
        try
        {
            Texture3D lut = LoadPackageTonemapLut(operation.Map);
            if (lut != null)
            {
                var environment = new GameObject(
                    "MODDED_OPERATIONS_PACKAGE_EXTERNAL_TONEMAP");
                SceneManager.MoveGameObjectToScene(environment, scene);
                var volume = environment.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 500000f;
                volume.weight = 1f;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "MODDED_OPERATIONS_PACKAGE_TONEMAP_PROFILE";
                volume.sharedProfile = profile;
                operation.RuntimeRenderProfiles.Add(profile);
                var tonemapping = profile.Add<Tonemapping>(false);
                tonemapping.mode.Override(TonemappingMode.External);
                tonemapping.useFullACES.Override(true);
                tonemapping.lutTexture.Override(lut);
            }
            log.LogInfo("Standalone render contract retained package scene lighting: " +
                "map=" + operation.Map.Id + ", time=" + operation.TimeCode +
                ", externalLut=" + (lut != null) +
                ", adapterPreset=false.");
        }
        catch (Exception ex)
        {
            log.LogWarning("Package scene lighting was retained, but its optional " +
                "external tonemap LUT could not be applied: " +
                ex.GetType().Name + ": " + ex.Message);
        }
    }

    private Texture3D LoadPackageTonemapLut(ModdedMapDefinition map)
    {
        if (map == null || map.ExternalTonemapLut == null)
            return null;
        if (packageTonemapLuts.TryGetValue(map.Id, out var cached) && cached != null)
            return cached;

        try
        {
            var descriptor = map.ExternalTonemapLut;
            byte[] raw = File.ReadAllBytes(descriptor.VerifiedPath);
            long expected = (long)descriptor.Dimension * descriptor.Dimension *
                descriptor.Dimension * 8L;
            if (raw.LongLength != expected)
            {
                throw new InvalidDataException(
                    "verified LUT length changed after catalog freeze");
            }

            var lut = new Texture3D(
                descriptor.Dimension,
                descriptor.Dimension,
                descriptor.Dimension,
                TextureFormat.RGBAHalf,
                false)
            {
                name = "PACKAGE_EXTERNAL_TONEMAP_LUT_" + map.Id,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            var il2CppRaw = new Il2CppStructArray<byte>(raw);
            lut.SetPixelData<byte>(il2CppRaw, 0, 0);
            lut.Apply(false, false);
            packageTonemapLuts[map.Id] = lut;
            return lut;
        }
        catch (Exception ex)
        {
            string diagnostic = map.Id + "|" + ex.GetType().FullName + "|" + ex.Message;
            if (lutDiagnostics.Add(diagnostic))
            {
                log.LogWarning("Package tonemap LUT reconstruction failed for map=" +
                    map.Id + ": " + ex.GetType().Name + ": " + ex.Message);
            }
        }
        return null;
    }

    private bool TryClaimStandaloneReadinessInitialization(global::GameMode gameMode)
    {
        var operation = activeOperation;
        if (operation == null || gameMode == null)
            return false;
        if (operation.GameModeComponent != gameMode &&
            !TryAdoptNetworkSpawnedGameMode(operation, gameMode))
        {
            return false;
        }
        if (operation.ReadinessInitializationClaimed)
        {
            return false;
        }
        operation.ReadinessInitializationClaimed = true;
        return true;
    }

    private bool TryAdoptNetworkSpawnedGameMode(
        ActiveMapOperation operation,
        global::GameMode gameMode)
    {
        if (operation == null || gameMode == null ||
            operation.BootstrapAssetId == 0 || gameMode.gameObject == null)
        {
            return false;
        }
        NetworkIdentity identity = gameMode.GetComponent<NetworkIdentity>();
        if (identity == null || identity.assetId != operation.BootstrapAssetId)
            return false;
        bool expectedMode = operation.Operation.Mode ==
            ModdedOperationMode.PlayerVersusEnvironment
            ? gameMode is StandalonePveGameMode
            : gameMode is StandalonePvpGameMode;
        if (!expectedMode)
            return false;

        operation.BootstrapRoot = gameMode.gameObject;
        operation.BootstrapIdentity = identity;
        operation.GameModeComponent = gameMode;
        global::GameMode.singleton = gameMode;
        if (gameMode is StandalonePveGameMode pve)
        {
            var raid = gameMode.GetComponent<RaidManager>();
            var exfil = gameMode.GetComponent<ExfilZone>();
            var trigger = gameMode.GetComponent<BoxCollider>();
            if (raid == null || exfil == null || trigger == null ||
                !trigger.isTrigger)
            {
                return false;
            }
            InfiltrationManager.instance = pve;
            RaidManager.singleton = raid;
            raid.infiltrationManager = pve;
            raid.EXTRACT_TIMER = StandalonePveExtractionSeconds;
            raid.exfilZones =
                new Il2CppSystem.Collections.Generic.List<ExfilZone>();
            raid.exfilZones.Add(exfil);
            operation.PveRaidManager = raid;
            operation.PveExfilZone = exfil;
            operation.PveExfilCollider = trigger;
        }
        if (gameMode is StandalonePvpGameMode pvp)
            PvpGameode.instance = pvp;
        log.LogInfo("Standalone game-mode Mirror spawn adopted on this peer: " +
            "assetId=0x" + operation.BootstrapAssetId.ToString("X8") +
            ", netId=" + identity.netId + ", mode=" +
            operation.Operation.Mode + ".");
        return true;
    }

    private void MarkStandaloneReadinessInitialized(
        global::GameMode gameMode,
        string source)
    {
        var operation = activeOperation;
        if (operation == null || operation.GameModeComponent != gameMode)
            return;
        operation.ReadinessInitialized = true;
        log.LogInfo("Standalone game mode entered the shipped readiness coroutines " +
            "through GameMode.Initialize(): source=" + source + ".");
    }

    private void MarkStandaloneReadinessInitializationFailed(
        global::GameMode gameMode,
        string source,
        Exception exception)
    {
        var operation = activeOperation;
        if (operation == null || operation.GameModeComponent != gameMode)
            return;
        operation.ReadinessInitializationClaimed = false;
        operation.ReadinessInitialized = false;
        log.LogError("Standalone readiness initialization failed closed: source=" +
            source + ", " + exception.GetType().Name + ": " + exception.Message);
    }

    private void OnStandaloneAllPlayersLoaded(bool nativePvpLifecycle)
    {
        var operation = activeOperation;
        if (operation == null)
            return;
        operation.AllPlayersLoaded = true;
        operation.NativePvpLifecycleActive = nativePvpLifecycle;
        operation.AllPlayersLoadedFrame = Time.frameCount;
        log.LogInfo("Standalone game mode received the shipped all-players-loaded " +
            "barrier for operation=" + operation.Operation.Id +
            ", nativePvpLifecycle=" + nativePvpLifecycle + ".");
        if (!nativePvpLifecycle)
            SpawnAndPositionStandalonePlayers(operation, true);
    }

    private void OnStandalonePvpAllPlayersLoadedFailed(Exception exception)
    {
        var operation = activeOperation;
        if (operation == null)
            return;
        operation.AllPlayersLoaded = true;
        operation.NativePvpLifecycleActive = false;
        operation.AllPlayersLoadedFrame = Time.frameCount;
        log.LogError("Shipped PvpGameode.Server_AllPlayersLoaded failed; " +
            "using the bounded position-only fallback for this session: " +
            exception.GetType().Name + ": " + exception.Message);
        SpawnAndPositionStandalonePlayers(operation, true);
    }

    private void MaintainStandaloneGameplay()
    {
        var operation = activeOperation;
        if (operation == null || operation.SceneHandle == 0)
            return;

        // GameManagerNetwork.FailOperation reads this exact retail PVE owner and
        // field. Its normal InfiltrationManager.Update cannot run safely in a
        // standalone package scene, so Cerberus advances only the required timer.
        var pveGameMode = operation.GameModeComponent as StandalonePveGameMode;
        if (NetworkServer.active && operation.AllPlayersLoaded && pveGameMode != null)
            pveGameMode.NetworkRaidTimer += Time.deltaTime;

        if (!operation.ScenePreparationComplete &&
            !operation.ScenePreparationStarted &&
            Time.frameCount >= operation.ScenePreparationEarliestFrame)
        {
            operation.ScenePreparationStarted = true;
            Scene pendingScene = FindLoadedSceneByHandle(operation.SceneHandle);
            PrepareStandaloneScene(pendingScene, operation);
        }
        if (
            !operation.ScenePreparationComplete || !operation.TerrainReady ||
            operation.BootstrapRoot == null)
        {
            return;
        }
        if (Time.frameCount < operation.LastMaintenanceFrame + 15)
            return;
        operation.LastMaintenanceFrame = Time.frameCount;

        try
        {
            EnsureStandaloneBootstrapPrefabRegistered(operation);
        }
        catch (Exception ex)
        {
            log.LogError("Standalone game-mode Mirror prefab registration failed " +
                "closed: " + ex.GetType().Name + ": " + ex.Message);
            return;
        }

        if (NetworkServer.active && !operation.NetworkSpawnRequested &&
            operation.BootstrapIdentity != null &&
            operation.BootstrapAssetId != 0)
        {
            try
            {
                operation.BootstrapRoot.SetActive(true);
                NetworkServer.Spawn(
                    operation.BootstrapRoot,
                    operation.BootstrapAssetId,
                    (NetworkConnection)null);
                operation.NetworkSpawnRequested = true;
                log.LogInfo("Standalone game mode network identity spawned by the host: " +
                    "assetId=0x" + operation.BootstrapAssetId.ToString("X8") + ".");
            }
            catch (Exception ex)
            {
                operation.BootstrapRoot.SetActive(false);
                log.LogWarning("Standalone game mode network spawn is waiting: " +
                    ex.GetType().Name + ": " + ex.Message);
            }
        }

        if (!NetworkServer.active)
            return;
        if (!operation.ReadinessInitialized && operation.NetworkSpawnRequested &&
            NetworkClient.active && operation.GameModeComponent != null &&
            Time.frameCount >= operation.BootstrapFrame + 30)
        {
            EnsureStandaloneReadiness(
                operation.GameModeComponent,
                "bounded host fallback after network spawn");
        }
        if (!operation.AllPlayersLoaded)
            return;

        if (!operation.NativePvpLifecycleActive)
            SpawnAndPositionStandalonePlayers(operation, true);
        if (operation.Operation.Mode ==
                ModdedOperationMode.PlayerVersusEnvironment &&
            !operation.PveSpawnAttempted &&
            operation.AllPlayersLoadedFrame >= 0 &&
            Time.frameCount >= operation.AllPlayersLoadedFrame + 180)
        {
            TrySpawnStandalonePveEnemies(operation);
        }
        ProcessProfiledPveAiDiagnostics(operation);
    }

    private static void EnsureStandaloneReadiness(
        global::GameMode gameMode,
        string source)
    {
        var pveGameMode = gameMode as StandalonePveGameMode;
        if (pveGameMode != null)
        {
            pveGameMode.EnsureStandaloneReadiness(source);
            return;
        }
        var pvpGameMode = gameMode as StandalonePvpGameMode;
        pvpGameMode?.EnsureStandaloneReadiness(source);
    }

    private static void ReleaseStandaloneSceneContracts(ActiveMapOperation operation)
    {
        if (operation == null)
            return;
        RestoreStandalonePlayerSpawnContract(operation);
        ReleaseStandaloneRenderContract(operation);
        ReleaseStandaloneGameMode(operation);
    }

    private static void ReleaseStandaloneRenderContract(ActiveMapOperation operation)
    {
        if (operation == null)
            return;
        if (operation.NvgColorCaptured)
        {
            try
            {
                if (GameManager.instance != null)
                    GameManager.instance.SetNVGColor(operation.PreviousNvgColor);
            }
            catch { }
        }
        operation.NvgColorCaptured = false;
        operation.PreviousNvgColor = 0;
        operation.WhitePhosphorApplied = false;
        foreach (VolumeProfile profile in operation.RuntimeRenderProfiles)
        {
            if (profile != null)
                Object.Destroy(profile);
        }
        operation.RuntimeRenderProfiles.Clear();
    }

    private static void ReleaseStandaloneGameMode(ActiveMapOperation operation)
    {
        if (operation == null)
            return;
        var gameMode = operation.GameModeComponent;
        if (gameMode != null && InfiltrationManager.instance == gameMode)
            InfiltrationManager.instance = null;
        if (gameMode != null && PvpGameode.instance == gameMode)
            PvpGameode.instance = null;
        if (gameMode != null && global::GameMode.singleton == gameMode)
            global::GameMode.singleton = null;
        var bootstrapAssetId = operation.BootstrapAssetId;
        var bootstrapPrefabRoot = operation.BootstrapPrefabRoot;
        if (bootstrapPrefabRoot != null)
        {
            try
            {
                NetworkClient.UnregisterPrefab(bootstrapPrefabRoot);
            }
            catch { }
        }
        // Always remove by asset ID as well. The scene unload callback can run
        // after Unity destroyed BootstrapPrefabRoot; in that state the wrapper
        // is a fake null and UnregisterPrefab(GameObject) cannot recover its
        // assetId. Leaving the dictionary entry causes every later operation
        // to loop at the native MAP LOADED !BUG! readiness gate.
        if (bootstrapAssetId != 0)
        {
            try
            {
                var prefabs = NetworkClient.prefabs;
                if (prefabs != null)
                    prefabs.Remove(bootstrapAssetId);
                NetworkClient.UnregisterSpawnHandler(bootstrapAssetId);
            }
            catch { }
        }
        operation.BootstrapPrefabRegistered = false;
        operation.BootstrapAssetId = 0;
        operation.BootstrapPrefabIdentity = null;
        operation.BootstrapPrefabRoot = null;
        foreach (Object runtimeAsset in operation.RuntimePveAssets)
        {
            if (runtimeAsset != null)
                Object.Destroy(runtimeAsset);
        }
        operation.RuntimePveAssets.Clear();
        foreach (Object runtimeAsset in operation.RuntimePvpAssets)
        {
            if (runtimeAsset != null)
                Object.Destroy(runtimeAsset);
        }
        operation.RuntimePvpAssets.Clear();
        if (operation.PveRaidManager != null &&
            RaidManager.singleton == operation.PveRaidManager)
        {
            RaidManager.singleton = null;
        }
        GameManagerNetwork network = GameManagerNetwork.instance;
        if (NetworkServer.active && network != null &&
            !network.SuccessfulOperation)
        {
            if (network._globalExfilOccupants != null)
                network._globalExfilOccupants.Clear();
            network.NetworkPlayersInAnyExfil = 0;
            network.NetworkcanExtract = false;
            network.NetworkisExtracting = false;
            network.NetworkextractionStartTime = 0d;
        }
        // Do not clear SuccessfulOperation here. The shipped Operation Room
        // reads that persistent result after the additive map scene unloads.
        operation.PveRaidManager = null;
        operation.PveExfilZone = null;
        operation.PveExfilCollider = null;
    }

    private void TrySpawnStandalonePveEnemies(ActiveMapOperation operation)
    {
        if (operation == null || operation.PveSpawnAttempted || !NetworkServer.active)
            return;
        operation.PveSpawnAttempted = true;
        Scene scene = FindLoadedSceneByHandle(operation.SceneHandle);
        if (!scene.IsValid() || !scene.isLoaded)
            return;
        var markers = FindSceneMarkers(scene, "PVE_EnemySpawn_");
        if (markers.Count == 0)
        {
            log.LogError("Standalone PVE package has no PVE_EnemySpawn_ markers.");
            return;
        }
        int minimumEnemies = operation.Operation.MinimumEnemies;
        int maximumEnemies = operation.Operation.MaximumEnemies;
        if (minimumEnemies < 1 || maximumEnemies < minimumEnemies)
        {
            log.LogError("Standalone PVE package has invalid enemy bounds: operation=" +
                operation.Operation.Id + ", requestedRange=" + minimumEnemies + "-" +
                maximumEnemies + ".");
            return;
        }
        if (markers.Count < minimumEnemies)
        {
            log.LogError("Standalone PVE package does not author enough enemy markers for its declared minimum: operation=" +
                operation.Operation.Id + ", requestedRange=" + minimumEnemies + "-" +
                maximumEnemies + ", markers=" + markers.Count + ".");
            return;
        }

        var gameManager = GameManager.instance;
        var registeredPrefabs = gameManager == null ? null : gameManager.AllAITypes;
        var prefabs = new List<GameObject>();
        if (registeredPrefabs != null)
        {
            for (int index = 0; index < registeredPrefabs.Count; index++)
            {
                var prefab = registeredPrefabs[index];
                var brain = prefab == null ? null : prefab.GetComponent<BrainAI>();
                var identity = prefab == null ? null : prefab.GetComponent<NetworkIdentity>();
                var weapons = brain == null
                    ? null
                    : brain.weapons ?? prefab.GetComponentInChildren<WeaponsAI>(true);
                if (prefab == null || brain == null || identity == null ||
                    weapons == null || !weapons.SpawnWeapon ||
                    weapons.weaponList == null || weapons.weaponList.Count == 0)
                {
                    continue;
                }
                prefabs.Add(prefab);
            }
        }
        if (prefabs.Count == 0)
        {
            log.LogError("Standalone PVE could not find a server-registered AI prefab " +
                "in persistent GameManager.AllAITypes.");
            return;
        }

        try
        {
            var raid = operation.PveRaidManager;
            if (raid == null || operation.PveExfilZone == null)
            {
                throw new InvalidOperationException(
                    "StandardPVE network owner is missing RaidManager or ExfilZone");
            }
            RaidManager.singleton = raid;

            int requestedCount = ChooseStandalonePveEnemyCount(operation);
            int targetCount = Math.Min(requestedCount, markers.Count);
            raid.infiltrationManager =
                operation.GameModeComponent as InfiltrationManager;
            raid.spawnVehicleAI = false;
            raid.hasIEDs = false;
            raid.hasReinforcements = false;
            raid.timedBackup = false;
            raid.standardAI = new Il2CppReferenceArray<GameObject>(prefabs.Count);
            for (int index = 0; index < prefabs.Count; index++)
                raid.standardAI[index] = prefabs[index];
            raid.prohibitedWeapons = new Il2CppReferenceArray<PuppetWeapon>(0);
            raid.mapSpecificWeapons = new Il2CppReferenceArray<PuppetWeapon>(0);
            raid.EXTRACT_TIMER = StandalonePveExtractionSeconds;
            raid.exfilZones =
                new Il2CppSystem.Collections.Generic.List<ExfilZone>();
            raid.exfilZones.Add(operation.PveExfilZone);
            raid.botSpawnPoints =
                new Il2CppSystem.Collections.Generic.List<GameObject>();
            ModdedPveAiProfileDefinition pveAiProfile =
                operation.Operation.PveAiProfile;
            foreach (Transform marker in markers)
            {
                var details = marker.GetComponent<BotSpawnDetails>() ??
                    marker.gameObject.AddComponent<BotSpawnDetails>();
                ConfigureStandaloneBotDetails(details, pveAiProfile);
                raid.botSpawnPoints.Add(marker.gameObject);
            }

            // Use OPERATOR's shipped population method. Its current native body
            // selects a standardAI prefab, instantiates it, calls
            // NetworkServer.Spawn(bot, GameManager.instance.gameObject), and
            // only then applies BotSpawnDetails. The previous adapter reversed
            // that order and omitted the native owner argument, which left the
            // firearm lifecycle incomplete even though server-side grenades
            // could still work.
            gameManager.botAmount = targetCount;
            gameManager.botHVTAmount = 0;
            // Current-build native inspection proves ServerSpawnAI(bool) is a
            // synchronous loop over RaidManager.botSpawnPoints. It does not
            // read GameManager.RandomSpawns. Do not mutate that process-global
            // player-selector flag here: true makes a later PlayerMaster use
            // the retail PlayerSpawn-tag search instead of the registered
            // standalone SpawnPointsInScene list.
            CaptureProfiledPvePreexistingBrains(operation, gameManager);
            raid.ServerSpawnAI(false);
            // ServerSpawnAI runs the remaining shipped RaidManager startup
            // work. That native path can repopulate exfilZones from persistent
            // donor objects that are present in Resources. A vanilla scene
            // finishes with its own serialized raid list; restore the same
            // map-local final state after native population initialization.
            raid.exfilZones =
                new Il2CppSystem.Collections.Generic.List<ExfilZone>();
            raid.exfilZones.Add(operation.PveExfilZone);
            operation.PveEnemyCount = targetCount;
            StartProfiledPveAiDiagnostics(operation, gameManager);
            log.LogInfo("Standalone PVE released a server-owned AI population " +
                "through shipped RaidManager.ServerSpawnAI: count=" +
                targetCount + ", requestedRange=" + minimumEnemies + "-" +
                maximumEnemies + ", chosen=" + requestedCount + ", markers=" +
                markers.Count + ", firearmCapablePrefabs=" + prefabs.Count +
                ", raidExfilsAfterNativeSpawn=" + raid.exfilZones.Count +
                ", aiProfile=" + FormatPveAiProfile(pveAiProfile) + ".");
        }
        catch (Exception ex)
        {
            log.LogError("Standalone PVE spawn failed closed after " +
                operation.PveEnemyCount + " confirmed AI: " +
                ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool IsProfiledPveDiagnosticOperation(
        ActiveMapOperation operation)
    {
        return operation?.Operation != null &&
            operation.Operation.Mode ==
                ModdedOperationMode.PlayerVersusEnvironment &&
            operation.Operation.PveAiProfile != null;
    }

    private static void CaptureProfiledPvePreexistingBrains(
        ActiveMapOperation operation,
        GameManager gameManager)
    {
        if (!IsProfiledPveDiagnosticOperation(operation) || gameManager?.allAI == null)
            return;
        operation.ProfiledPvePreexistingBrainIds.Clear();
        for (int index = 0; index < gameManager.allAI.Count; index++)
        {
            BrainAI brain = gameManager.allAI[index];
            if (brain != null)
                operation.ProfiledPvePreexistingBrainIds.Add(brain.GetInstanceID());
        }
    }

    private void StartProfiledPveAiDiagnostics(
        ActiveMapOperation operation,
        GameManager gameManager)
    {
        if (!IsProfiledPveDiagnosticOperation(operation))
            return;
        operation.ProfiledPveDiagnosticBrains.Clear();
        operation.ProfiledPveInitialBrainPositions.Clear();
        operation.ProfiledPveAiDiagnosticStartedAt = -1f;
        operation.ProfiledPveAiDiagnosticNextProbeAt = Time.realtimeSinceStartup;
        operation.ProfiledPveAiDiagnosticSnapshotIndex = 0;
        operation.ProfiledPveAiDiagnosticComplete = false;
        operation.ProfiledPveAiDiagnosticAwaitingBrains = true;
        operation.ProfiledPveNativeContractLogged = false;
        GameObject player = gameManager == null ? null : GameManager.myPlayer;
        operation.ProfiledPveInitialPlayerPositionCaptured = player != null;
        if (player != null)
            operation.ProfiledPveInitialPlayerPosition = player.transform.position;
        if (!TryBeginProfiledPveAiDiagnostics(operation, gameManager, "spawn"))
        {
            log.LogInfo("Profiled PVE AI diagnostic is waiting for the native " +
                "NetworkServer spawn callback for operation=" +
                operation.Operation.Id + ".");
        }
    }

    private bool TryBeginProfiledPveAiDiagnostics(
        ActiveMapOperation operation,
        GameManager gameManager,
        string source)
    {
        RefreshProfiledPveDiagnosticBrains(operation, gameManager);
        if (operation.ProfiledPveDiagnosticBrains.Count == 0)
            return false;

        float now = Time.realtimeSinceStartup;
        operation.ProfiledPveAiDiagnosticAwaitingBrains = false;
        operation.ProfiledPveAiDiagnosticStartedAt = now;
        operation.ProfiledPveAiDiagnosticNextProbeAt = now;
        operation.ProfiledPveAiDiagnosticSnapshotIndex = 0;
        operation.ProfiledPveInitialBrainPositions.Clear();
        foreach (BrainAI brain in operation.ProfiledPveDiagnosticBrains)
        {
            if (brain != null)
            {
                operation.ProfiledPveInitialBrainPositions[brain.GetInstanceID()] =
                    GetProfiledPveNavigationPosition(brain);
            }
        }
        GameObject player = gameManager == null ? null : GameManager.myPlayer;
        if (player != null)
        {
            operation.ProfiledPveInitialPlayerPositionCaptured = true;
            operation.ProfiledPveInitialPlayerPosition = player.transform.position;
        }
        LogProfiledPveNativeAiContract(operation, source);
        operation.ProfiledPveNativeContractLogged = true;
        return true;
    }

    private static void RefreshProfiledPveDiagnosticBrains(
        ActiveMapOperation operation,
        GameManager gameManager)
    {
        if (operation == null || gameManager?.allAI == null)
            return;
        var known = new HashSet<int>();
        foreach (BrainAI brain in operation.ProfiledPveDiagnosticBrains)
        {
            if (brain != null)
                known.Add(brain.GetInstanceID());
        }
        for (int index = 0; index < gameManager.allAI.Count; index++)
        {
            BrainAI brain = gameManager.allAI[index];
            if (brain == null)
                continue;
            int instanceId = brain.GetInstanceID();
            if (operation.ProfiledPvePreexistingBrainIds.Contains(instanceId) ||
                !known.Add(instanceId))
            {
                continue;
            }
            operation.ProfiledPveDiagnosticBrains.Add(brain);
            operation.ProfiledPveInitialBrainPositions[instanceId] =
                GetProfiledPveNavigationPosition(brain);
        }
    }

    private static Vector3 GetProfiledPveNavigationPosition(BrainAI brain)
    {
        if (brain == null)
            return Vector3.zero;
        try
        {
            AgentController controller = brain.agent;
            if (controller != null && controller.entityExists)
                return controller.position;
        }
        catch
        {
            // Fall back to the network root while a native entity registers
            // or unregisters. The shipped BOT V2 hierarchy keeps BrainAI on
            // the network root and FollowerEntity on the moving model child.
        }
        return brain.transform.position;
    }

    private void LogProfiledPveNativeAiContract(
        ActiveMapOperation operation,
        string source)
    {
        if (operation == null)
            return;
        int count = 0;
        float minimumDelay = float.MaxValue;
        float maximumDelay = float.MinValue;
        float minimumDetection = float.MaxValue;
        float maximumDetection = float.MinValue;
        int minimumWander = int.MaxValue;
        int maximumWander = int.MinValue;
        float minimumFov = float.MaxValue;
        float maximumFov = float.MinValue;
        int idleWanderEnabled = 0;
        int commsEnabled = 0;
        foreach (BrainAI brain in operation.ProfiledPveDiagnosticBrains)
        {
            if (brain == null)
                continue;
            try
            {
                float nativeDelay = brain.WanderTimer * brain.Patience;
                minimumDelay = Mathf.Min(minimumDelay, nativeDelay);
                maximumDelay = Mathf.Max(maximumDelay, nativeDelay);
                minimumDetection = Mathf.Min(minimumDetection, brain.DetectionRange);
                maximumDetection = Mathf.Max(maximumDetection, brain.DetectionRange);
                minimumWander = Math.Min(minimumWander, brain.WanderDistance);
                maximumWander = Math.Max(maximumWander, brain.WanderDistance);
                minimumFov = Mathf.Min(minimumFov, brain.EyesFOVAngle);
                maximumFov = Mathf.Max(maximumFov, brain.EyesFOVAngle);
                if (brain.idleStates == BrainAI.IdleStates.Wander)
                    idleWanderEnabled++;
                if (brain.useComms)
                    commsEnabled++;
                count++;
            }
            catch
            {
                // A native object can unregister while a scene is closing.
            }
        }
        if (count == 0)
        {
            log.LogWarning("Profiled PVE AI diagnostic found no new BrainAI " +
                "instances for operation=" + operation.Operation.Id +
                " at " + source + "; the bounded snapshots will retry.");
            return;
        }
        string message = "Profiled PVE native AI contract: operation=" +
            operation.Operation.Id + ", profile=" +
            operation.Operation.PveAiProfile.Id + ", source=" + source +
            ", brains=" + count +
            ", nativeInitialWanderDelay=" +
            minimumDelay.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
            ".." + maximumDelay.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
            "s, detection=" +
            minimumDetection.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
            ".." + maximumDetection.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
            "m, fov=" +
            minimumFov.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
            ".." + maximumFov.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
            ", wander=" + minimumWander + ".." + maximumWander +
            "m, idleWander=" + idleWanderEnabled + "/" + count +
            ", comms=" + commsEnabled + "/" + count + ".";
        if (minimumDelay <= 0f)
            log.LogWarning(message + " At least one native prefab has no initial wander delay.");
        else
            log.LogInfo(message);
    }

    private void ProcessProfiledPveAiDiagnostics(ActiveMapOperation operation)
    {
        if (!IsProfiledPveDiagnosticOperation(operation) ||
            operation.ProfiledPveAiDiagnosticComplete ||
            operation.ProfiledPveAiDiagnosticSnapshotIndex >=
                ProfiledPveAiDiagnosticSnapshotSeconds.Length)
        {
            return;
        }

        float now = Time.realtimeSinceStartup;
        if (now < operation.ProfiledPveAiDiagnosticNextProbeAt)
            return;
        if (operation.ProfiledPveAiDiagnosticAwaitingBrains)
        {
            if (!TryBeginProfiledPveAiDiagnostics(
                    operation,
                    GameManager.instance,
                    "native-network-spawn"))
            {
                operation.ProfiledPveAiDiagnosticNextProbeAt = now + 0.25f;
                return;
            }
            now = Time.realtimeSinceStartup;
        }
        if (operation.ProfiledPveAiDiagnosticStartedAt < 0f)
            return;
        float elapsed = now - operation.ProfiledPveAiDiagnosticStartedAt;
        float scheduled = ProfiledPveAiDiagnosticSnapshotSeconds[
            operation.ProfiledPveAiDiagnosticSnapshotIndex];
        if (elapsed + 0.05f < scheduled)
        {
            operation.ProfiledPveAiDiagnosticNextProbeAt =
                operation.ProfiledPveAiDiagnosticStartedAt + scheduled;
            return;
        }

        GameManager gameManager = GameManager.instance;
        RefreshProfiledPveDiagnosticBrains(operation, gameManager);
        LogProfiledPveAiSnapshot(operation, gameManager, scheduled, elapsed);
        operation.ProfiledPveAiDiagnosticSnapshotIndex++;
        if (operation.ProfiledPveAiDiagnosticSnapshotIndex >=
            ProfiledPveAiDiagnosticSnapshotSeconds.Length)
        {
            operation.ProfiledPveAiDiagnosticComplete = true;
            log.LogInfo("Profiled PVE AI diagnostic completed its bounded " +
                "120-second read-only acceptance window for operation=" +
                operation.Operation.Id + ".");
            return;
        }
        operation.ProfiledPveAiDiagnosticNextProbeAt =
            operation.ProfiledPveAiDiagnosticStartedAt +
            ProfiledPveAiDiagnosticSnapshotSeconds[
                operation.ProfiledPveAiDiagnosticSnapshotIndex];
    }

    private void LogProfiledPveAiSnapshot(
        ActiveMapOperation operation,
        GameManager gameManager,
        float scheduled,
        float elapsed)
    {
        int live = 0;
        int movedOneMeter = 0;
        int movedTowardInsertionFiveMeters = 0;
        int hasSeenTarget = 0;
        int sightBlockedByVegetation = 0;
        int sightBlockedByOther = 0;
        int sightReachedPlayer = 0;
        int brainEnabled = 0;
        int controllerAssigned = 0;
        int controllerEnabled = 0;
        int controllerActive = 0;
        int entityExists = 0;
        int updatePositionEnabled = 0;
        int pathPending = 0;
        int hasPath = 0;
        int destinationAtLeastOneMeter = 0;
        int followerAssigned = 0;
        int followerEnabled = 0;
        int followerActive = 0;
        int followerEntityExists = 0;
        int followerCanMove = 0;
        int followerCanSearch = 0;
        int followerSimulatesMovement = 0;
        int followerStopped = 0;
        int velocityAboveOneCentimeterPerSecond = 0;
        int responding = 0;
        int currentCover = 0;
        float minimumWanderClock = float.MaxValue;
        float maximumWanderClock = float.MinValue;
        float minimumMaxSpeed = float.MaxValue;
        float maximumMaxSpeed = float.MinValue;
        float maximumVelocity = 0f;
        float maximumControllerPositionOffset = 0f;
        float movementTotal = 0f;
        float movementMaximum = 0f;
        var states = new Dictionary<string, int>(StringComparer.Ordinal);
        var movementTypes = new Dictionary<int, int>();
        GameObject player = gameManager == null ? null : GameManager.myPlayer;
        Vector3 playerAimPoint = player == null
            ? Vector3.zero
            : player.transform.position + Vector3.up * 1.35f;

        foreach (BrainAI brain in operation.ProfiledPveDiagnosticBrains)
        {
            if (brain == null)
                continue;
            try
            {
                int instanceId = brain.GetInstanceID();
                if (!operation.ProfiledPveInitialBrainPositions.TryGetValue(
                        instanceId,
                        out Vector3 initial))
                {
                    initial = GetProfiledPveNavigationPosition(brain);
                    operation.ProfiledPveInitialBrainPositions[instanceId] = initial;
                }
                Vector3 current = GetProfiledPveNavigationPosition(brain);
                Vector2 planarDelta = new Vector2(
                    current.x - initial.x,
                    current.z - initial.z);
                float movement = planarDelta.magnitude;
                movementTotal += movement;
                movementMaximum = Mathf.Max(movementMaximum, movement);
                if (movement >= 1f)
                    movedOneMeter++;
                if (operation.ProfiledPveInitialPlayerPositionCaptured)
                {
                    Vector2 initialToInsertion = new Vector2(
                        initial.x - operation.ProfiledPveInitialPlayerPosition.x,
                        initial.z - operation.ProfiledPveInitialPlayerPosition.z);
                    Vector2 currentToInsertion = new Vector2(
                        current.x - operation.ProfiledPveInitialPlayerPosition.x,
                        current.z - operation.ProfiledPveInitialPlayerPosition.z);
                    if (initialToInsertion.magnitude - currentToInsertion.magnitude >= 5f)
                        movedTowardInsertionFiveMeters++;
                }
                if (brain.CurrentSeenTarget != null)
                    hasSeenTarget++;
                string state = brain.CurrentState.ToString();
                states[state] = states.TryGetValue(state, out int stateCount)
                    ? stateCount + 1
                    : 1;

                if (brain.enabled)
                    brainEnabled++;
                if (brain.responding)
                    responding++;
                if (brain._currentCover != null)
                    currentCover++;
                minimumWanderClock = Mathf.Min(
                    minimumWanderClock,
                    brain.wanderTime);
                maximumWanderClock = Mathf.Max(
                    maximumWanderClock,
                    brain.wanderTime);
                int movementType = brain.movementType;
                movementTypes[movementType] = movementTypes.TryGetValue(
                        movementType,
                        out int movementTypeCount)
                    ? movementTypeCount + 1
                    : 1;

                AgentController controller = brain.agent;
                if (controller != null)
                {
                    controllerAssigned++;
                    if (controller.enabled)
                        controllerEnabled++;
                    if (controller.isActiveAndEnabled)
                        controllerActive++;
                    if (controller.entityExists)
                    {
                        entityExists++;
                        if (controller.updatePosition)
                            updatePositionEnabled++;
                        if (controller.pathPending)
                            pathPending++;
                        if (controller.hasPath)
                            hasPath++;
                        float maxSpeed = controller.maxSpeed;
                        minimumMaxSpeed = Mathf.Min(minimumMaxSpeed, maxSpeed);
                        maximumMaxSpeed = Mathf.Max(maximumMaxSpeed, maxSpeed);
                        Vector3 velocity = controller.velocity;
                        float velocityMagnitude = velocity.magnitude;
                        maximumVelocity = Mathf.Max(
                            maximumVelocity,
                            velocityMagnitude);
                        if (velocityMagnitude >= 0.01f)
                            velocityAboveOneCentimeterPerSecond++;
                        Vector3 controllerPosition = controller.position;
                        maximumControllerPositionOffset = Mathf.Max(
                            maximumControllerPositionOffset,
                            Vector3.Distance(controllerPosition, current));
                        Vector3 destination = controller.destination;
                        Vector2 destinationDelta = new Vector2(
                            destination.x - current.x,
                            destination.z - current.z);
                        if (destinationDelta.magnitude >= 1f)
                            destinationAtLeastOneMeter++;
                    }
                    var follower = controller.Agent;
                    if (follower != null)
                    {
                        followerAssigned++;
                        if (follower.enabled)
                            followerEnabled++;
                        if (follower.isActiveAndEnabled)
                            followerActive++;
                        if (follower.entityExists)
                            followerEntityExists++;
                        if (follower.canMove)
                            followerCanMove++;
                        if (follower.canSearch)
                            followerCanSearch++;
                        if (follower.simulateMovement)
                            followerSimulatesMovement++;
                        if (follower.isStopped)
                            followerStopped++;
                    }
                }

                if (player != null && brain.eyesAI != null)
                {
                    GameObject eyesObject = brain.eyesAI.EyesTransform;
                    Vector3 eyePosition = eyesObject == null
                        ? current + Vector3.up * 1.6f
                        : eyesObject.transform.position;
                    if (Physics.Linecast(
                            eyePosition,
                            playerAimPoint,
                            out RaycastHit hit,
                            brain.eyesAI.DetectionLayerMask,
                            QueryTriggerInteraction.Collide))
                    {
                        Transform hitTransform = hit.collider == null
                            ? null
                            : hit.collider.transform;
                        if (hitTransform != null &&
                            hitTransform.root == player.transform.root)
                        {
                            sightReachedPlayer++;
                        }
                        else if (hit.collider != null &&
                                 hit.collider.gameObject.layer == 18)
                        {
                            sightBlockedByVegetation++;
                        }
                        else
                        {
                            sightBlockedByOther++;
                        }
                    }
                    else
                    {
                        // A clear same-mask line has no accepted collider.
                        // Record it with reached-player because neither map
                        // geometry nor a foliage blocker stopped the probe.
                        sightReachedPlayer++;
                    }
                }
                live++;
            }
            catch
            {
                // Keep the bounded report alive if one bot is destroyed.
            }
        }

        string stateSummary = states.Count == 0
            ? "none"
            : string.Join(",", states.OrderBy(pair => pair.Key)
                .Select(pair => pair.Key + "=" + pair.Value));
        float movementMean = live == 0 ? 0f : movementTotal / live;
        log.LogInfo("Profiled PVE AI snapshot: operation=" +
            operation.Operation.Id + ", profile=" +
            operation.Operation.PveAiProfile.Id + ", scheduled=" +
            scheduled.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) +
            "s, elapsed=" +
            elapsed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
            "s, live=" + live +
            ", moved>=1m=" + movedOneMeter +
            ", movedTowardInsertion>=5m=" + movedTowardInsertionFiveMeters +
            ", movementMean=" +
            movementMean.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
            "m, movementMax=" +
            movementMaximum.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
            "m, actualSeenTarget=" + hasSeenTarget +
            ", sameMaskSightProbe(vegetation=" + sightBlockedByVegetation +
            ",other=" + sightBlockedByOther +
            ",clearOrPlayer=" + sightReachedPlayer +
            "), states=" + stateSummary + ".");

        string movementTypeSummary = movementTypes.Count == 0
            ? "none"
            : string.Join(",", movementTypes.OrderBy(pair => pair.Key)
                .Select(pair => pair.Key + "=" + pair.Value));
        string wanderClockSummary = live == 0
            ? "none"
            : minimumWanderClock.ToString(
                    "F2",
                    System.Globalization.CultureInfo.InvariantCulture) +
                ".." + maximumWanderClock.ToString(
                    "F2",
                    System.Globalization.CultureInfo.InvariantCulture) + "s";
        string maxSpeedSummary = entityExists == 0
            ? "none"
            : minimumMaxSpeed.ToString(
                    "F2",
                    System.Globalization.CultureInfo.InvariantCulture) +
                ".." + maximumMaxSpeed.ToString(
                    "F2",
                    System.Globalization.CultureInfo.InvariantCulture) + "mps";
        log.LogInfo("Profiled PVE native agent diagnostic: operation=" +
            operation.Operation.Id + ", scheduled=" +
            scheduled.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) +
            "s, brainEnabled=" + brainEnabled + "/" + live +
            ", controllerAssigned=" + controllerAssigned + "/" + live +
            ", controllerEnabled=" + controllerEnabled + "/" + live +
            ", controllerActive=" + controllerActive + "/" + live +
            ", entityExists=" + entityExists + "/" + live +
            ", updatePosition=" + updatePositionEnabled + "/" + live +
            ", pathPending=" + pathPending +
            ", hasPath=" + hasPath +
            ", destination>=1m=" + destinationAtLeastOneMeter +
            ", followerAssigned=" + followerAssigned + "/" + live +
            ", followerEnabled=" + followerEnabled + "/" + live +
            ", followerActive=" + followerActive + "/" + live +
            ", followerEntityExists=" + followerEntityExists + "/" + live +
            ", canMove=" + followerCanMove + "/" + live +
            ", canSearch=" + followerCanSearch + "/" + live +
            ", simulateMovement=" + followerSimulatesMovement + "/" + live +
            ", isStopped=" + followerStopped + "/" + live +
            ", maxSpeed=" + maxSpeedSummary +
            ", velocity>=0.01mps=" +
                velocityAboveOneCentimeterPerSecond +
            ", velocityMax=" + maximumVelocity.ToString(
                "F2",
                System.Globalization.CultureInfo.InvariantCulture) + "mps" +
            ", controllerPositionOffsetMax=" +
                maximumControllerPositionOffset.ToString(
                    "F2",
                    System.Globalization.CultureInfo.InvariantCulture) + "m" +
            ", responding=" + responding +
            ", currentCover=" + currentCover +
            ", wanderClock=" + wanderClockSummary +
            ", movementTypes=" + movementTypeSummary + ".");
    }

    private static int ChooseStandalonePveEnemyCount(ActiveMapOperation operation)
    {
        int minimum = operation.Operation.MinimumEnemies;
        int maximum = operation.Operation.MaximumEnemies;
        if (maximum <= minimum)
            return minimum;

        // Only the server executes this path. FNV-1a over immutable launch
        // identity produces a bounded, reproducible population without
        // mutating UnityEngine.Random's global state.
        string identity = operation.Operation.Id + "|" + operation.TimeCode + "|" +
            operation.SceneHandle;
        uint hash = 2166136261u;
        for (int index = 0; index < identity.Length; index++)
        {
            hash ^= identity[index];
            hash *= 16777619u;
        }
        return minimum + (int)(hash % (uint)(maximum - minimum + 1));
    }

    private static void ConfigureStandaloneBotDetails(
        BotSpawnDetails details,
        ModdedPveAiProfileDefinition profile)
    {
        if (details == null)
            return;
        // The current ServerSpawnAI body consumes range, FOV, maximum
        // effective range, communications, and counter-suppression. It does
        // not consume DetectionTimeMultiplier or HearingRange. Keep those two
        // fields at the common outdoor marker baseline so a future
        // compatible native implementation does not inherit the old custom
        // 1.15/52 values by accident.
        details.DetectionTimeMultiplier = profile == null ? 1.15f : 1f;
        details.HearingRange = profile == null ? 52f : 20f;
        details.DetectionRange = profile?.DetectionRangeMeters ?? 72f;
        details.FOV = profile?.FieldOfViewDegrees ?? 105f;
        details.maxEffectiveRange = profile?.MaximumEffectiveRangeMeters ?? 90f;
        details.useComms = profile?.UseComms ?? true;
        details.DoesCounterSuppression = profile?.CounterSuppression ?? true;
        // RaidManager.ApplyBotSpawnSettings copies BotSpawnDetails.idleState
        // at marker offset 0x20 to BrainAI.idleStates at offset 0x2D4. The
        // native BrainAI.UpdateStateMachine dispatches CurrentState.Idle to
        // Wander(dt) only when this substate is Wander. A radius by itself
        // does not start movement. Restrict this vanilla marker choice to
        // schema-v2 profiled PVE operations. Schema-v1, PVP, vanilla, and
        // other packages keep their existing marker state.
        if (profile != null)
            details.idleState = BrainAI.IdleStates.Wander;
        // BrainAI.Wander waits for its prefab-owned WanderTimer multiplied by
        // Patience before it calls RandomNavSphere(currentPosition, 5,
        // WanderDistance). The package changes only the radius. It preserves
        // the native delay and therefore does not release all bots at launch.
        details.WanderDistance = profile?.WanderDistanceMeters ?? 18;
        details.modifyStance = true;
        details.doesCrouch = true;
        details.doesProne = false;
        details.crouchDistance = new Vector2(12f, 35f);
        details.proneDistance = new Vector2(0f, 0f);
        details.isSpawnGroup = false;
        details.PatrolSpeed = 1.25f;
        details.PatrolLooping = false;
        details.patrolWaitTime = 2f;
        details.disableNavmesh = false;
    }

    private static string FormatPveAiProfile(ModdedPveAiProfileDefinition profile)
    {
        if (profile == null)
            return "framework-legacy(range=72m,fov=105,maxEffective=90m,wander=18m,comms=true,counterSuppression=true)";
        return profile.Id + "(range=" +
            profile.DetectionRangeMeters.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
            "m,fov=" +
            profile.FieldOfViewDegrees.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
            ",maxEffective=" +
            profile.MaximumEffectiveRangeMeters.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
            "m,wander=" + profile.WanderDistanceMeters +
            "m,comms=" + profile.UseComms +
            ",counterSuppression=" + profile.CounterSuppression + ")";
    }

    private static List<Transform> FindSceneMarkers(Scene scene, string prefix)
    {
        var markers = new List<Transform>();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
            {
                if ((item.name ?? string.Empty).StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    markers.Add(item);
                }
            }
        }
        markers.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        return markers;
    }

    private void SpawnAndPositionStandalonePlayers(
        ActiveMapOperation operation,
        bool allowSpawnRequest)
    {
        if (operation == null || operation.SceneHandle == 0)
            return;
        Scene scene = FindLoadedSceneByHandle(operation.SceneHandle);
        if (!scene.IsValid() || !scene.isLoaded)
            return;
        var markers = FindStandalonePlayerMarkers(scene, operation.Operation.Mode);
        if (markers.Count == 0)
        {
            log.LogError("Standalone package scene has no compatible player spawn " +
                "markers for spawnSet=" + operation.Operation.SpawnSetId + ".");
            return;
        }

        PlayerMaster[] players;
        try { players = Resources.FindObjectsOfTypeAll<PlayerMaster>(); }
        catch { return; }
        foreach (var player in players)
        {
            if (player == null || !player.gameObject.scene.IsValid())
                continue;
            int playerId = player.GetInstanceID();
            Transform marker = SelectPlayerMarker(operation, player, markers);
            if (marker == null)
                continue;
            try
            {
                player.LastSpawnPoint = marker;
                player.spawnRotation = marker.eulerAngles;
            }
            catch { }

            PlayerNetworking spawned = null;
            try { spawned = player.PlayerSpawnedObject; } catch { }
            if (spawned != null)
                operation.CompletedPlayerSpawnIds.Add(playerId);
            bool currentlyAlive = false;
            try { currentlyAlive = player.currentlySpawnedAndAlive; } catch { }
            if (spawned == null && currentlyAlive)
            {
                // The shipped SyncVar can clear its managed wrapper after the
                // host has completed ClientSpawnBS. Alive is still a terminal
                // success signal for this scene generation; never instantiate
                // a second avatar for the same PlayerMaster.
                operation.CompletedPlayerSpawnIds.Add(playerId);
                continue;
            }
            if (spawned == null && operation.CompletedPlayerSpawnIds.Contains(playerId))
                continue;
            if (spawned == null && allowSpawnRequest)
            {
                bool spawnOwned = false;
                try { spawnOwned = player.isOwned; } catch { }
                int requestCount = operation.PlayerSpawnRequestCounts.TryGetValue(
                    playerId,
                    out int priorRequestCount) ? priorRequestCount : 0;
                int maximumRequests = spawnOwned && NetworkServer.active ? 2 : 3;
                if (requestCount >= maximumRequests)
                    continue;
                bool canRequest = !operation.PlayerSpawnRequestFrames.TryGetValue(
                    playerId,
                    out int lastRequestFrame) ||
                    Time.frameCount >= lastRequestFrame + 300;
                if (!canRequest)
                    continue;
                try
                {
                    if (!currentlyAlive)
                    {
                        // Record the bounded attempt before entering native
                        // code. An exception must not turn the maintenance
                        // pass into one spawn call every frame.
                        operation.PlayerSpawnRequestFrames[playerId] = Time.frameCount;
                        operation.PlayerSpawnRequestCounts[playerId] = requestCount + 1;
                        string spawnRoute = RequestStandalonePlayerSpawn(
                            player,
                            spawnOwned,
                            requestCount);
                        bool producedPlayerObject = false;
                        try { producedPlayerObject = player.PlayerSpawnedObject != null; }
                        catch { }
                        if (producedPlayerObject)
                            operation.CompletedPlayerSpawnIds.Add(playerId);
                        log.LogInfo("Standalone requested the shipped player spawn " +
                            "pipeline: playerMaster=" + playerId +
                            ", owned=" + spawnOwned +
                            ", serverActive=" + NetworkServer.active +
                            ", route=" + spawnRoute +
                            ", attempt=" + (requestCount + 1) + "/" + maximumRequests +
                            ", producedPlayerObject=" + producedPlayerObject + ".");
                    }
                }
                catch (Exception ex)
                {
                    Exception detail = ex is TargetInvocationException &&
                        ex.InnerException != null ? ex.InnerException : ex;
                    log.LogWarning("Standalone player spawn request is waiting for " +
                        playerId + ": " + detail.GetType().Name + ": " +
                        detail.Message);
                }
                continue;
            }
            int spawnedObjectId = spawned == null ? 0 : spawned.GetInstanceID();
            if (spawned == null ||
                (operation.PositionedPlayerObjects.TryGetValue(
                    playerId,
                    out int positionedObjectId) &&
                 positionedObjectId == spawnedObjectId))
                continue;

            bool owned = false;
            try
            {
                owned = spawned.isOwned || spawned.isLocalPlayer || player.isOwned;
            }
            catch { }
            Vector3 target = marker.position + Vector3.up * 0.25f;
            if (IsPlayerAtPackageSpawn(spawned, target, owned, out string positionState))
            {
                operation.PositionedPlayerObjects[playerId] = spawnedObjectId;
                log.LogInfo("Standalone player reached package marker through the " +
                    "shipped movement contract: marker=" + marker.name +
                    ", playerMaster=" + playerId + ", owned=" + owned +
                    ", state=" + positionState + ".");
                continue;
            }

            bool canMove = !operation.PlayerMoveRequestFrames.TryGetValue(
                playerId,
                out int lastMoveFrame) || Time.frameCount >= lastMoveFrame + 300;
            if (!canMove)
                continue;

            if (owned && GameManager.instance != null)
            {
                // This is the retail movement path used by the game after it
                // has obtained the owned PlayerNetworking object. It moves the
                // network root, coordinates masterController physics, snaps
                // height, and updates the local player/controller transform.
                // Calling this is required because PlayerNetworking owns more
                // than one transform; moving only spawned.gameObject leaves
                // the first-person view at the stale/default scene position.
                GameManager.instance.StartCoroutine(
                    GameManager.instance.MovePlayerToSpawn(target, marker.rotation));
                operation.PlayerMoveRequestFrames[playerId] = Time.frameCount;
                log.LogInfo("Standalone invoked shipped GameManager." +
                    "MovePlayerToSpawn for owned player: marker=" + marker.name +
                    ", playerMaster=" + playerId + ", priorState=" +
                    positionState + ".");
            }
            else
            {
                MoveRemotePlayerRoot(spawned.gameObject, target, marker.rotation);
                operation.PlayerMoveRequestFrames[playerId] = Time.frameCount;
                log.LogInfo("Standalone moved server-owned remote player root to " +
                    "package marker=" + marker.name + ", playerMaster=" +
                    playerId + ".");
            }
        }
    }

    private string RequestStandalonePlayerSpawn(
        PlayerMaster player,
        bool owned,
        int priorRequestCount)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));

        if (owned)
        {
            if (NetworkServer.active && priorRequestCount > 0)
            {
                // A repeat additive-scene launch can retain the host-owned
                // PlayerMaster while its previous PlayerNetworking object has
                // already been destroyed with the old map. The first attempt
                // must still use SpawnPlayer so ClientSpawnBS runs. If that
                // bounded kickoff has not produced the new scene generation's
                // PlayerSpawnedObject after 300 frames, execute Mirror's exact
                // generated server body once more on the host. It creates the
                // owner-aware network player, assigns the shipped SyncVar, and
                // sends the retail spawn RPC without relying on stale command
                // sender state from the prior additive scene.
                InvokeGeneratedServerPlayerSpawnBody(player);
                return "owned-host-generated-server-recovery";
            }
            // This is the retail local-player kickoff. In addition to the
            // authority-checked CMDSpawnPlayer sender it runs ClientSpawnBS,
            // which the direct generated server body does not do.
            player.SpawnPlayer();
            return NetworkServer.active
                ? "owned-native-kickoff-host"
                : "owned-native-kickoff-client";
        }

        if (NetworkServer.active)
        {
            // SpawnPlayer and SpawnPlayerServer both enter Mirror's generated
            // CMDSpawnPlayer sender. A host that is already inside the server
            // readiness barrier must execute the same generated server body
            // that the command handler would call. That body uses the shipped
            // GameManager spawn list, owner-aware NetworkServer.Spawn, the
            // PlayerSpawnedObject SyncVar, and the retail spawn RPC.
            InvokeGeneratedServerPlayerSpawnBody(player);
            return "generated-server-body";
        }

        player.SpawnPlayerServer();
        return "server-command-wrapper";
    }

    private void InvokeGeneratedServerPlayerSpawnBody(PlayerMaster player)
    {
        if (directServerPlayerSpawnMethod == null)
        {
            throw new MissingMethodException(
                typeof(PlayerMaster).FullName,
                "UserCode_CMDSpawnPlayer__NetworkIdentity");
        }
        NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
        if (identity == null)
            throw new InvalidOperationException(
                "PlayerMaster has no NetworkIdentity for the shipped server spawn body");
        directServerPlayerSpawnMethod.Invoke(player, new object[] { identity });
    }

    private static Scene FindLoadedSceneByHandle(int handle)
    {
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (scene.handle == handle)
                return scene;
        }
        return default;
    }

    private static List<Transform> FindStandalonePlayerMarkers(
        Scene scene,
        ModdedOperationMode mode)
    {
        var team1 = new List<Transform>();
        var team2 = new List<Transform>();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
            {
                string name = item.name ?? string.Empty;
                if (name.StartsWith("Team1_Spawn_", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Team1_Backup_Spawn_", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("PVP_Team1Spawn_", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("PVE_PlayerSpawn_", StringComparison.OrdinalIgnoreCase))
                {
                    team1.Add(item);
                }
                else if (name.StartsWith("Team2_Spawn_", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Team2_Backup_Spawn_", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("PVP_Team2Spawn_", StringComparison.OrdinalIgnoreCase))
                {
                    team2.Add(item);
                }
            }
        }
        team1.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        team2.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        if (mode == ModdedOperationMode.PlayerVersusEnvironment)
            return team1;
        team1.AddRange(team2);
        return team1;
    }

    private static Transform SelectPlayerMarker(
        ActiveMapOperation operation,
        PlayerMaster player,
        List<Transform> markers)
    {
        if (operation == null || markers == null || markers.Count == 0)
            return null;
        int playerId = player.GetInstanceID();
        int pvpTeamId = 0;
        if (operation.Operation.Mode == ModdedOperationMode.PlayerVersusPlayer)
        {
            try { pvpTeamId = player.MyTeamIdentifier?.TeamID ?? 0; } catch { }
            if (pvpTeamId != 1 && pvpTeamId != 2)
                return null;
        }
        if (operation.PlayerMarkerNames.TryGetValue(
                playerId,
                out string assignedName))
        {
            Transform assigned = markers.FirstOrDefault(marker =>
                string.Equals(marker.name, assignedName, StringComparison.Ordinal));
            if (assigned != null &&
                (operation.Operation.Mode != ModdedOperationMode.PlayerVersusPlayer ||
                 PvpMarkerMatchesTeam(assigned, pvpTeamId)))
                return assigned;
            operation.PlayerMarkerNames.Remove(playerId);
        }
        Transform selected;
        if (operation.Operation.Mode == ModdedOperationMode.PlayerVersusPlayer)
        {
            var subset = markers.Where(marker => PvpMarkerMatchesTeam(marker, pvpTeamId))
                .ToList();
            if (subset.Count > 0)
            {
                selected = subset[operation.SpawnCursor++ % subset.Count];
                operation.PlayerMarkerNames[playerId] = selected.name;
                return selected;
            }
        }
        selected = markers[operation.SpawnCursor++ % markers.Count];
        operation.PlayerMarkerNames[playerId] = selected.name;
        return selected;
    }

    private static bool PvpMarkerMatchesTeam(Transform marker, int teamId)
    {
        string name = marker?.name ?? string.Empty;
        if (teamId == 1)
        {
            return name.StartsWith("Team1", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("PVP_Team1", StringComparison.OrdinalIgnoreCase);
        }
        if (teamId == 2)
        {
            return name.StartsWith("Team2", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("PVP_Team2", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private static bool IsPlayerAtPackageSpawn(
        PlayerNetworking player,
        Vector3 target,
        bool owned,
        out string state)
    {
        if (player == null)
        {
            state = "player=null";
            return false;
        }
        float networkDistance = Vector3.Distance(player.transform.position, target);
        if (!owned)
        {
            state = "network=" + networkDistance.ToString("F2");
            return networkDistance <= 3f;
        }

        PlayerNetworking managerNetworking = null;
        GameObject managerPlayer = null;
        FirstPersonController managerController = null;
        try
        {
            managerNetworking = GameManager.myPlayerNetworking;
            managerPlayer = GameManager.myPlayer;
            managerController = GameManager.myPlayerController;
        }
        catch { }
        float managerDistance = managerPlayer == null
            ? float.MaxValue
            : Vector3.Distance(managerPlayer.transform.position, target);
        float controllerDistance = managerController == null
            ? float.MaxValue
            : Vector3.Distance(managerController.transform.position, target);
        GameObject cameraObject = null;
        try { cameraObject = player.Camera; } catch { }
        float cameraDistance = cameraObject == null
            ? float.MaxValue
            : Vector3.Distance(cameraObject.transform.position, target);
        bool sameOwner = managerNetworking == null || managerNetworking == player;
        bool localRootReady = managerDistance <= 8f || controllerDistance <= 8f;
        bool cameraReady = cameraDistance <= 12f;
        state = "network=" + networkDistance.ToString("F2") +
            ", managerPlayer=" + DescribeDistance(managerDistance) +
            ", controller=" + DescribeDistance(controllerDistance) +
            ", camera=" + DescribeDistance(cameraDistance) +
            ", sameOwner=" + sameOwner;
        return networkDistance <= 3f && sameOwner && localRootReady && cameraReady;
    }

    private static string DescribeDistance(float distance)
    {
        return distance == float.MaxValue ? "null" : distance.ToString("F2");
    }

    private static void MoveRemotePlayerRoot(
        GameObject player,
        Vector3 target,
        Quaternion rotation)
    {
        if (player == null)
            return;
        var controller = player.GetComponent<CharacterController>() ??
            player.GetComponentInChildren<CharacterController>(true);
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
            controller.enabled = false;
        player.transform.SetPositionAndRotation(target, rotation);
        foreach (var body in player.GetComponentsInChildren<Rigidbody>(true))
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        if (controllerWasEnabled)
            controller.enabled = true;
        Physics.SyncTransforms();
    }

    private static void ReplaceNativeMapPreview(Transform parent, Sprite previewSprite, string name)
    {
        if (parent == null || previewSprite == null)
            return;
        SetGameObjectsActive(CaptureDirectChildren(parent), false);
        var preview = new GameObject(name);
        preview.transform.SetParent(parent, false);
        SetFullStretch(preview.AddComponent<RectTransform>());
        var image = preview.AddComponent<Image>();
        image.sprite = previewSprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void RebindNativeFullscreenControls(OperationBoardUI board, GameObject preparationPanel)
    {
        if (board == null || preparationPanel == null)
            return;
        int enterCount = 0;
        int exitCount = 0;

        foreach (var control in preparationPanel.GetComponentsInChildren<PanelButton>(true))
        {
            int direction = GetFullscreenEventDirection(control == null ? null : control.onClick,
                control == null ? null : control.gameObject, board);
            if (direction == 0)
                continue;
            control.onClick = new UnityEvent();
            bool fullscreen = direction > 0;
            control.onClick.AddListener((UnityAction)(() => SetNativeMapFullscreen(board, fullscreen)));
            control.isInteractable = true;
            if (fullscreen) enterCount++; else exitCount++;
        }
        foreach (var control in preparationPanel.GetComponentsInChildren<ButtonManager>(true))
        {
            int direction = GetFullscreenEventDirection(control == null ? null : control.onClick,
                control == null ? null : control.gameObject, board);
            if (direction == 0)
                continue;
            control.onClick = new UnityEvent();
            control.onDoubleClick = new UnityEvent();
            control.checkForDoubleClick = false;
            bool fullscreen = direction > 0;
            control.onClick.AddListener((UnityAction)(() => SetNativeMapFullscreen(board, fullscreen)));
            control.isInteractable = true;
            try
            {
                if (control.targetButton != null)
                    control.targetButton.interactable = true;
            }
            catch { }
            if (fullscreen) enterCount++; else exitCount++;
        }
        foreach (var control in preparationPanel.GetComponentsInChildren<Button>(true))
        {
            int direction = GetFullscreenEventDirection(control == null ? null : control.onClick,
                control == null ? null : control.gameObject, board);
            if (direction == 0)
                continue;
            control.onClick = new Button.ButtonClickedEvent();
            bool fullscreen = direction > 0;
            control.onClick.AddListener((UnityAction)(() => SetNativeMapFullscreen(board, fullscreen)));
            control.interactable = true;
            if (fullscreen) enterCount++; else exitCount++;
        }
        log.LogInfo("Modded Operations clone-local fullscreen controls rebound: enter=" + enterCount +
            ", exit=" + exitCount + ".");
    }

    private static int GetFullscreenEventDirection(UnityEventBase clickEvent, GameObject control,
        OperationBoardUI board)
    {
        if (EventInvokesMethod(clickEvent, "EnterFullscreenButton"))
            return 1;
        if (EventInvokesMethod(clickEvent, "ExitFullscreenButton"))
            return -1;
        if (control == null || board == null)
            return 0;
        string name = control.name ?? string.Empty;
        bool namedForFullscreen = name.IndexOf("FULLSCREEN", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("EXPAND", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("MAXIMIZE", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("REDUCE", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("MINIMIZE", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!namedForFullscreen)
            return 0;
        if (board.FullscreenMapObject != null &&
            control.transform.IsChildOf(board.FullscreenMapObject.transform))
            return -1;
        return 1;
    }

    private static bool EventInvokesMethod(UnityEventBase clickEvent, string methodName)
    {
        if (clickEvent == null || string.IsNullOrEmpty(methodName))
            return false;
        try
        {
            int count = clickEvent.GetPersistentEventCount();
            for (int index = 0; index < count; index++)
                if (string.Equals(clickEvent.GetPersistentMethodName(index), methodName,
                    StringComparison.Ordinal))
                    return true;
        }
        catch { }
        return false;
    }

    private static void SetNativeMapFullscreen(OperationBoardUI board, bool fullscreen)
    {
        if (board == null)
            return;
        try { board.SetFullscreenImage(fullscreen); }
        catch { }
        SetActiveSafe(board.FullscreenMapObject, fullscreen);
    }

    private static void ConfigureNativeSelector(HorizontalSelector selector, string[] values,
        Action<int> onChanged, int initialIndex = 0)
    {
        if (selector == null || values == null || values.Length == 0)
            return;
        selector.useLocalization = false;
        selector.saveSelected = false;
        if (selector.localizedObject != null)
            selector.localizedObject.enabled = false;
        selector.items.Clear();
        foreach (var value in values)
            selector.CreateNewItem(value);
        initialIndex = Mathf.Clamp(initialIndex, 0, values.Length - 1);
        selector.defaultIndex = initialIndex;
        selector.index = initialIndex;
        selector.onValueChanged = new HorizontalSelector.HorizontalSelectorEvent();
        if (onChanged != null)
            selector.onValueChanged.AddListener((UnityAction<int>)(index => onChanged(index)));
        try { selector.InitializeSelector(); }
        catch
        {
            try { selector.UpdateUI(); } catch { }
        }
    }

    private static bool ReplaceNativeButtonAction(Object controlObject, Action action)
    {
        if (controlObject == null || action == null)
            return false;

        var control = controlObject as GameObject;
        if (control == null)
        {
            var component = controlObject as Component;
            if (component != null)
                control = component.gameObject;
        }
        if (control == null)
            return false;

        // A shipped DreamOS action can expose the same physical click through
        // more than one component: PanelButton/ButtonManager owns the visual
        // state and a nested Unity Button can retain the serialized gameplay
        // listener. Rebinding only the first component leaves that nested
        // listener free to launch the source vanilla operation. Replace every
        // event surface inside this private cloned control and collapse their
        // callbacks to one logical action per frame.
        int lastLogicalFrame = -1;
        Action invokeOnce = () =>
        {
            int frame = Time.frameCount;
            if (frame == lastLogicalFrame)
                return;
            lastLogicalFrame = frame;
            action();
        };

        bool rebound = false;
        foreach (var panelButton in control.GetComponentsInChildren<PanelButton>(true))
        {
            if (panelButton == null)
                continue;
            panelButton.onClick = new UnityEvent();
            panelButton.onClick.AddListener((UnityAction)(() => invokeOnce()));
            panelButton.isInteractable = true;
            rebound = true;
        }
        foreach (var manager in control.GetComponentsInChildren<ButtonManager>(true))
        {
            if (manager == null)
                continue;
            manager.onClick = new UnityEvent();
            manager.onDoubleClick = new UnityEvent();
            manager.checkForDoubleClick = false;
            manager.onClick.AddListener((UnityAction)(() => invokeOnce()));
            manager.isInteractable = true;
            try
            {
                if (manager.targetButton != null)
                    manager.targetButton.interactable = true;
            }
            catch { }
            rebound = true;
        }
        foreach (var button in control.GetComponentsInChildren<Button>(true))
        {
            if (button == null)
                continue;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener((UnityAction)(() => invokeOnce()));
            button.interactable = true;
            rebound = true;
        }
        return rebound;
    }

    private bool BindNativePreparationBack(MissionLaptop laptop, GameObject privateBoard, Button authoredBack,
        bool warnIfMissing)
    {
        if (laptop == null || privateBoard == null)
            return false;

        int boardId = privateBoard.GetInstanceID();
        if (nativeBackBoundBoards.Contains(boardId))
            return true;

        // privateBoard is the complete private Operation Preparation clone, so
        // this search cannot discover or append a listener to vanilla BACK.
        var searchRoots = new List<Transform>(1) { privateBoard.transform };

        PanelButton panelButton = null;
        foreach (var root in searchRoots)
        {
            foreach (var candidate in root.GetComponentsInChildren<PanelButton>(true))
            {
                if (IsNativeBackCandidate(candidate == null ? null : candidate.gameObject, authoredBack))
                {
                    panelButton = candidate;
                    break;
                }
            }
            if (panelButton != null)
                break;
        }

        ButtonManager manager = null;
        if (panelButton == null)
        {
            foreach (var root in searchRoots)
            {
                foreach (var candidate in root.GetComponentsInChildren<ButtonManager>(true))
                {
                    if (IsNativeBackCandidate(candidate == null ? null : candidate.gameObject, authoredBack))
                    {
                        manager = candidate;
                        break;
                    }
                }
                if (manager != null)
                    break;
            }
        }

        Button button = null;
        if (panelButton == null && manager == null)
        {
            foreach (var root in searchRoots)
            {
                foreach (var candidate in root.GetComponentsInChildren<Button>(true))
                {
                    if (IsNativeBackCandidate(candidate == null ? null : candidate.gameObject, authoredBack))
                    {
                        button = candidate;
                        break;
                    }
                }
                if (button != null)
                    break;
            }
        }

        if (panelButton == null && manager == null && button == null)
        {
            if (warnIfMissing)
                log.LogWarning("Cerberus could not find the shipped Operation Preparation BACK control after the panel opened; the native board and Modded Operations tab remain available.");
            return false;
        }

        GameObject backControl = panelButton != null
            ? panelButton.gameObject
            : manager != null
                ? manager.gameObject
                : button.gameObject;
        if (!ReplaceNativeButtonAction(backControl, () =>
        {
            if (privateBoard == null || !privateBoard.activeSelf)
                return;
            ReturnNativeOperationToModdedHome(laptop, privateBoard, authoredBack);
        }))
        {
            if (warnIfMissing)
                log.LogWarning("Cerberus could not replace every click surface beneath the private Operation Preparation BACK control.");
            return false;
        }
        nativeBackBoundBoards.Add(boardId);
        log.LogInfo("Modded Operations board bound to shipped Operation Preparation BACK control via " +
            (panelButton != null ? "DreamOS PanelButton" : manager != null ? "ButtonManager" : "Unity Button") +
            "; all clone-local click surfaces replaced.");
        return true;
    }

    private static bool IsNativeBackCandidate(GameObject control, Button authoredBack)
    {
        if (control == null ||
            (authoredBack != null && control == authoredBack.gameObject))
            return false;
        if ((control.name ?? string.Empty).StartsWith("MODDED_", StringComparison.OrdinalIgnoreCase))
            return false;
        return ControlHasText(control, "BACK");
    }

    private static bool ControlHasText(GameObject control, string text)
    {
        if (control == null)
            return false;
        if (control.name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        foreach (var label in control.GetComponentsInChildren<TMP_Text>(true))
            if (label != null && TitleEquals(label.text, text))
                return true;
        return false;
    }

    private void OpenNativeOperationPreparation(MissionLaptop laptop, GameObject privateBoard,
        Button authoredBack)
    {
        if (laptop == null || privateBoard == null)
            return;
        Transform operationSelection = laptop.ActiveOperationsTab == null
            ? null
            : laptop.ActiveOperationsTab.transform.parent;
        if (operationSelection == null)
            return;
        var board = privateBoard.GetComponentInChildren<OperationBoardUI>(true);
        CloseNativeMapConfirmation(board, false);
        SetNativeMapFullscreen(board, false);
        ShowIsolatedNativePreparationPanel(laptop, privateBoard);
        operationSelection.gameObject.SetActive(false);
        BindNativePreparationBack(laptop, privateBoard, authoredBack, true);
        log.LogInfo("Modded Operations board opened through its isolated complete Operation Preparation clone; vanillaContentUntouched=true.");
    }

    private void ShowIsolatedNativePreparationPanel(MissionLaptop laptop, GameObject panel)
    {
        if (laptop == null || panel == null)
            return;
        panel.SetActive(true);
        var animator = panel.GetComponent<Animator>();
        var manager = laptop.cerberusWindowPanelManager;
        string fadeInState = manager == null ? null : manager.panelFadeIn;
        string speedKey = manager == null ? null : manager.animSpeedKey;
        float speed = manager == null ? 1f : manager.panelAnimationSpeed;
        bool playedFadeIn = false;
        if (animator != null)
        {
            animator.enabled = true;
            try
            {
                if (!string.IsNullOrEmpty(speedKey))
                    animator.SetFloat(speedKey, speed);
                if (!string.IsNullOrEmpty(fadeInState))
                {
                    int stateHash = Animator.StringToHash(fadeInState);
                    if (animator.HasState(0, stateHash))
                    {
                        animator.Play(stateHash, 0, 0f);
                        animator.Update(0f);
                        playedFadeIn = true;
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogWarning("Cerberus private preparation fade-in failed: " +
                    ex.GetType().Name + ": " + ex.Message);
            }
        }

        // WindowPanelManager.ShowCurrentPanel applies these root interaction
        // values in the retail route. The private clone is not registered with
        // that manager, so reproduce only its own root presentation state.
        var rootCanvasGroup = panel.GetComponent<CanvasGroup>();
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 1f;
            rootCanvasGroup.interactable = true;
            rootCanvasGroup.blocksRaycasts = true;
        }
        log.LogInfo("Cerberus private preparation presentation activated: animator=" +
            (animator != null) + ", animatorEnabled=" + (animator != null && animator.enabled) +
            ", fadeInState='" + (fadeInState ?? "null") + "', fadeInPlayed=" + playedFadeIn +
            ", rootCanvasGroup=" + (rootCanvasGroup != null) + ".");
    }

    private void ReturnNativeOperationToModdedHome(MissionLaptop laptop, GameObject privateBoard,
        Button authoredBack)
    {
        var board = privateBoard.GetComponentInChildren<OperationBoardUI>(true);
        CloseNativeMapConfirmation(board, false);
        SetNativeMapFullscreen(board, false);
        privateBoard.SetActive(false);
        Transform operationSelection = laptop.ActiveOperationsTab == null
            ? null
            : laptop.ActiveOperationsTab.transform.parent;
        if (operationSelection != null)
            operationSelection.gameObject.SetActive(true);
        var page = FindChild(operationSelection, "MODDED_OPERATIONS_PAGE");
        OpenModdedPage(laptop, page);
        var tab = FindDeep(operationSelection, "MODDED_OPS_NATIVE_TAB");
        SetTabSelectedState(FindNativeActiveOperationsButton(operationSelection), false);
        SetTabSelectedState(FindNativeSimulationOperationsButton(operationSelection), false);
        SetTabSelectedState(tab, true);
        MarkNativeModdedPageOpened(laptop.GetInstanceID(), Time.frameCount);
        log.LogInfo("Modded Operations clone-local BACK closed transient UI and returned home without invoking the vanilla panel manager.");
    }

    private void CloseNativeMapConfirmation(OperationBoardUI board, bool logClose)
    {
        if (board == null || board.ConfirmationWindow == null)
            return;
        var confirmation = board.ConfirmationWindow;
        try
        {
            // The modal root can remain active while its Animator is in the
            // authored closed state. Closing that state again would schedule
            // a delayed DisableObject coroutine which could race the next
            // Execute/OpenWindow click, so key this only to the modal's own
            // authoritative open flag.
            if (confirmation.isOn)
                confirmation.CloseWindow();
            if (logClose)
                log.LogInfo("Modded Operations confirmation window closed through its clone-local modal route.");
        }
        catch (Exception ex)
        {
            // Keep this fallback scoped to the private modal. A stripped
            // CloseWindow wrapper must never force us through a vanilla board
            // or panel-manager callback merely to dismiss the overlay.
            confirmation.gameObject.SetActive(false);
            log.LogWarning("Modded Operations clone-local confirmation close fell back to disabling its private modal: " +
                ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void SetNativeConfirmationLoadingState(
        CatalogPresentation presentation,
        bool loading)
    {
        var confirmation = presentation?.Board == null
            ? null
            : presentation.Board.ConfirmationWindow;
        if (confirmation == null)
            return;
        if (loading)
        {
            confirmation.descriptionText =
                "Loading verified map content. The operation will start when it is ready.";
        }
        else if (presentation.SelectedOperation != null)
        {
            confirmation.descriptionText = "Start " +
                presentation.SelectedOperation.DisplayName + " at " +
                presentation.SelectedTimeCode + "?";
        }
        if (confirmation.windowDescription != null)
            confirmation.windowDescription.text = confirmation.descriptionText;

        Object controlObject = confirmation.confirmButton;
        GameObject control = null;
        if (controlObject is GameObject gameObject)
            control = gameObject;
        else if (controlObject is Component component)
            control = component.gameObject;
        if (control == null)
            return;
        foreach (var panelButton in control.GetComponentsInChildren<PanelButton>(true))
        {
            if (panelButton != null)
                panelButton.isInteractable = !loading;
        }
        foreach (var manager in control.GetComponentsInChildren<ButtonManager>(true))
        {
            if (manager == null)
                continue;
            manager.isInteractable = !loading;
            if (manager.targetButton != null)
                manager.targetButton.interactable = !loading;
        }
        foreach (var button in control.GetComponentsInChildren<Button>(true))
        {
            if (button != null)
                button.interactable = !loading;
        }
    }

    private GameObject CreateNativeBoardActionButton(Transform parent, GameObject template,
        string name, string text, Vector2 anchorMin, Vector2 anchorMax, Action action)
    {
        if (parent == null || template == null || action == null)
            return null;
        var clone = Object.Instantiate(template, parent);
        if (clone == null)
            return null;
        clone.name = name;
        var rect = clone.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
        clone.SetActive(true);
        var manager = clone.GetComponent<ButtonManager>() ?? clone.GetComponentInChildren<ButtonManager>(true);
        if (manager == null)
        {
            Object.Destroy(clone);
            return null;
        }
        manager.useLocalization = false;
        manager.buttonText = text;
        if (manager.localizedObject != null)
            manager.localizedObject.enabled = false;
        SetTmpText(manager.normalTextObj, text);
        SetTmpText(manager.highlightTextObj, text);
        SetTmpText(manager.pressedTextObj, text);
        SetTmpText(manager.disabledTextObj, text);
        foreach (var label in clone.GetComponentsInChildren<TMP_Text>(true))
        {
            DisableLocalizationComponent(label.gameObject);
            SetTmpText(label, text);
        }
        manager.onClick = new UnityEvent();
        manager.onDoubleClick = new UnityEvent();
        manager.checkForDoubleClick = false;
        manager.onClick.AddListener((UnityAction)(() => action()));
        manager.isInteractable = true;
        try
        {
            if (manager.targetButton != null)
                manager.targetButton.interactable = true;
        }
        catch { }
        try { manager.UpdateUI(); } catch { }
        try { manager.UpdateState(); } catch { }
        return clone;
    }

    private static void RestoreAuthoredPresentationChildren(Transform parent, string nativeShellName)
    {
        if (parent == null)
            return;
        for (int index = 0; index < parent.childCount; index++)
        {
            var child = parent.GetChild(index);
            if (child != null && child.name != nativeShellName)
                child.gameObject.SetActive(true);
        }
    }

    private static void SetTmpText(TMP_Text label, string text)
    {
        if (label == null)
            return;
        label.text = text;
        label.enableWordWrapping = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(8f, label.fontSize * 0.55f);
        label.fontSizeMax = Mathf.Max(label.fontSizeMin, label.fontSize);
    }

    private static GameObject FindNativeOperationRowTemplate(Transform list)
    {
        if (list == null)
            return null;
        GameObject fallback = null;
        for (int index = 0; index < list.childCount; index++)
        {
            var child = list.GetChild(index);
            if (child == null)
                continue;
            if (fallback == null && child.GetComponentInChildren<TMP_Text>(true) != null)
                fallback = child.gameObject;
            if (child.GetComponentInChildren<OperationSelectionUI>(true) != null)
                return child.gameObject;
        }
        return fallback;
    }

    private static int[] BuildRelativeChildIndexPath(Transform root, Transform descendant)
    {
        if (root == null || descendant == null)
            return null;
        var indexes = new List<int>();
        var current = descendant;
        while (current != null && current != root)
        {
            indexes.Add(current.GetSiblingIndex());
            current = current.parent;
        }
        if (current != root)
            return null;
        indexes.Reverse();
        return indexes.ToArray();
    }

    private static Transform FollowRelativeChildIndexPath(Transform root, int[] indexes)
    {
        if (root == null || indexes == null)
            return null;
        var current = root;
        foreach (var index in indexes)
        {
            if (index < 0 || index >= current.childCount)
                return null;
            current = current.GetChild(index);
        }
        return current;
    }

    private static void SetChildrenActive(Transform parent, bool active)
    {
        if (parent == null)
            return;
        for (int index = 0; index < parent.childCount; index++)
        {
            var child = parent.GetChild(index);
            if (child != null)
                child.gameObject.SetActive(active);
        }
    }

    private static List<GameObject> CaptureDirectChildren(Transform parent)
    {
        var result = new List<GameObject>();
        if (parent == null)
            return result;
        for (int index = 0; index < parent.childCount; index++)
        {
            var child = parent.GetChild(index);
            if (child != null)
                result.Add(child.gameObject);
        }
        return result;
    }

    private static void SetGameObjectsActive(object collection, bool active)
    {
        foreach (var item in ReadListItems(collection))
        {
            var gameObject = ExtractGameObject(item);
            if (gameObject != null)
                gameObject.SetActive(active);
        }
    }

    private static void SetActiveSafe(GameObject gameObject, bool active)
    {
        if (gameObject != null)
            gameObject.SetActive(active);
    }

    private static void SetComponentActiveSafe(Component component, bool active)
    {
        if (component != null)
            component.gameObject.SetActive(active);
    }

    private static void SetFullStretch(RectTransform rect)
    {
        if (rect == null)
            return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static TMP_Text FindNamedText(Transform root, string name)
    {
        var gameObject = FindDeep(root, name);
        if (gameObject == null)
            return null;
        return gameObject.GetComponent<TMP_Text>() ?? gameObject.GetComponentInChildren<TMP_Text>(true);
    }

    private static TMP_Text FindNativeBriefingText(GameObject shell, params GameObject[] excludedRoots)
    {
        if (shell == null)
            return null;
        // The shipped list page names its actual right-hand briefing body
        // deterministically. Prefer it before scoring other localized labels;
        // the largest localized label can be the generic Active Operations
        // description and was visibly overwriting the modded briefing.
        var exact = FindNamedText(shell.transform, "Operation Selection Briefing");
        if (exact != null && !IsInsideAny(exact.transform, excludedRoots))
            return exact;
        TMP_Text best = null;
        float bestScore = float.MinValue;
        foreach (var localization in shell.GetComponentsInChildren<LocalizationTMPEvent>(true))
        {
            TMP_Text label = null;
            try { label = localization == null ? null : localization.TMP; } catch { }
            if (label == null)
                continue;
            bool excluded = false;
            if (excludedRoots != null)
            {
                foreach (var excludedRoot in excludedRoots)
                {
                    if (excludedRoot != null && label.transform.IsChildOf(excludedRoot.transform))
                    {
                        excluded = true;
                        break;
                    }
                }
            }
            if (excluded)
                continue;
            var rect = label.rectTransform;
            float score = rect == null ? 0f : Mathf.Abs(rect.rect.width * rect.rect.height);
            string labelName = label.gameObject.name ?? string.Empty;
            string currentText = label.text ?? string.Empty;
            if (labelName.IndexOf("brief", StringComparison.OrdinalIgnoreCase) >= 0)
                score += 1000000f;
            if (currentText.Length >= 80)
                score += 500000f;
            if (score > bestScore)
            {
                best = label;
                bestScore = score;
            }
        }
        return best;
    }

    private static bool IsInsideAny(Transform candidate, GameObject[] roots)
    {
        if (candidate == null || roots == null)
            return false;
        foreach (var root in roots)
            if (root != null && candidate.IsChildOf(root.transform))
                return true;
        return false;
    }

    private static void RewriteClonedPageHeading(GameObject shell)
    {
        if (shell == null)
            return;
        foreach (var label in shell.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label == null || (!TitleEquals(label.text, "ACTIVE OPERATIONS") &&
                !TitleEquals(label.text, "ACTIVE OPS")))
                continue;
            DisableLocalizationComponent(label.gameObject);
            if (label.transform.parent != null)
                DisableLocalizationComponent(label.transform.parent.gameObject);
            label.text = "MODDED OPERATIONS";
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Max(8f, label.fontSize * 0.55f);
            label.fontSizeMax = Mathf.Max(label.fontSizeMin, label.fontSize);
        }
    }

    private static void RewriteNativeOperationRow(GameObject row, string operationName,
        string duration, string threat, string mode, string areaOfOperation = null)
    {
        if (row == null)
            return;
        var assigned = new HashSet<int>();
        var selectionUi = row.GetComponentInChildren<OperationSelectionUI>(true);
        var columnReferences = new List<KeyValuePair<TMP_Text, string>>();
        if (selectionUi != null)
        {
            AddColumnReference(columnReferences, FindMemberText(selectionUi, "NameTextBox"), operationName);
            AddColumnReference(columnReferences, FindMemberText(selectionUi, "SecondTextBox"), duration);
            AddColumnReference(columnReferences, FindMemberText(selectionUi, "ThirdTextBox"), threat);
            AddColumnReference(columnReferences, FindMemberText(selectionUi, "SimulationTypeText"), mode);
            SetMemberText(selectionUi, "NameTextBox", operationName, assigned);
            SetMemberText(selectionUi, "RegionNameTextBox",
                string.IsNullOrWhiteSpace(areaOfOperation)
                    ? operationName
                    : areaOfOperation,
                assigned);
            SetMemberText(selectionUi, "SecondTextBox", duration, assigned);
            SetMemberText(selectionUi, "ThirdTextBox", threat, assigned);
            SetMemberText(selectionUi, "SimulationTypeText", mode, assigned);
            var classifiedCover = ExtractGameObject(ReadMember(selectionUi, "ClassfiedCover"));
            var regionCover = ExtractGameObject(ReadMember(selectionUi, "RegionCover"));
            if (classifiedCover != null)
                classifiedCover.SetActive(false);
            if (regionCover != null)
                regionCover.SetActive(false);
        }

        var rowRect = row.GetComponent<RectTransform>();
        foreach (var label in row.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label == null || assigned.Contains(label.GetInstanceID()))
                continue;
            string replacement = null;
            float bestDistance = float.MaxValue;
            foreach (var reference in columnReferences)
            {
                if (reference.Key == null)
                    continue;
                float distance = Mathf.Abs(label.rectTransform.position.x -
                    reference.Key.rectTransform.position.x);
                if (distance < bestDistance)
                {
                    replacement = reference.Value;
                    bestDistance = distance;
                }
            }
            if (replacement == null && rowRect != null && Mathf.Abs(rowRect.rect.width) > 0.01f)
            {
                float localX = rowRect.InverseTransformPoint(
                    label.rectTransform.TransformPoint(label.rectTransform.rect.center)).x;
                float normalizedX = (localX - rowRect.rect.xMin) / rowRect.rect.width;
                replacement = normalizedX < 0.44f ? operationName :
                    normalizedX < 0.72f ? duration : threat;
            }
            if (replacement == null)
                continue;
            DisableLocalizationComponent(label.gameObject);
            label.text = replacement;
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Max(8f, label.fontSize * 0.55f);
            label.fontSizeMax = Mathf.Max(label.fontSizeMin, label.fontSize);
        }
    }

    private static TMP_Text FindMemberText(object owner, string memberName)
    {
        var gameObject = ExtractGameObject(ReadMember(owner, memberName));
        if (gameObject == null)
            return null;
        return gameObject.GetComponent<TMP_Text>() ?? gameObject.GetComponentInChildren<TMP_Text>(true);
    }

    private static void AddColumnReference(List<KeyValuePair<TMP_Text, string>> references,
        TMP_Text label, string value)
    {
        if (references != null && label != null && !string.IsNullOrEmpty(value))
            references.Add(new KeyValuePair<TMP_Text, string>(label, value));
    }

    private static bool SetMemberText(object owner, string memberName, string value,
        HashSet<int> assigned)
    {
        var gameObject = ExtractGameObject(ReadMember(owner, memberName));
        if (gameObject == null)
            return false;
        var label = gameObject.GetComponent<TMP_Text>() ?? gameObject.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            return false;
        DisableLocalizationComponent(gameObject);
        if (label.gameObject != gameObject)
            DisableLocalizationComponent(label.gameObject);
        label.text = value;
        label.enableWordWrapping = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(8f, label.fontSize * 0.55f);
        label.fontSizeMax = Mathf.Max(label.fontSizeMin, label.fontSize);
        assigned?.Add(label.GetInstanceID());
        return true;
    }

    private static void DisableLocalizationComponent(GameObject gameObject)
    {
        if (gameObject == null)
            return;
        // IL2CPP can surface a component's managed wrapper as the generic
        // UnityEngine.Component type. Disable the two known shipped
        // localization behaviours through their strong types first.
        try
        {
            var tmpLocalization = gameObject.GetComponent<LocalizationTMPEvent>();
            if (tmpLocalization != null)
                tmpLocalization.enabled = false;
        }
        catch { }
        try
        {
            var localizedObject = gameObject.GetComponent<LocalizedObject>();
            if (localizedObject != null)
                localizedObject.enabled = false;
        }
        catch { }
        foreach (var component in gameObject.GetComponents<Component>())
        {
            if (component == null)
                continue;
            var typeName = component.GetType().Name;
            if (typeName.IndexOf("LocalizationTMPEvent", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("LocalizedObject", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Destroy is deferred until the end of the frame. Disable the
                // Behaviour immediately so a late localization update cannot
                // restore source-tab/source-mission text first.
                if (component is Behaviour behaviour)
                    behaviour.enabled = false;
                Object.Destroy(component);
            }
        }
    }

    private static void StackNativeRows(GameObject first, GameObject second)
    {
        var firstRect = first == null ? null : first.GetComponent<RectTransform>();
        var secondRect = second == null ? null : second.GetComponent<RectTransform>();
        if (firstRect == null || secondRect == null)
            return;
        var position = firstRect.anchoredPosition;
        float height = Mathf.Max(1f, firstRect.rect.height);
        secondRect.anchoredPosition = new Vector2(position.x, position.y - height - 6f);
    }

    private static void StackNativeRows(IReadOnlyList<GameObject> rows)
    {
        if (rows == null || rows.Count < 2)
            return;
        var firstRect = rows[0] == null ? null : rows[0].GetComponent<RectTransform>();
        if (firstRect == null)
            return;
        Vector2 origin = firstRect.anchoredPosition;
        float height = Mathf.Max(1f, firstRect.rect.height);
        for (int index = 1; index < rows.Count; index++)
        {
            var rect = rows[index] == null ? null : rows[index].GetComponent<RectTransform>();
            if (rect != null)
                rect.anchoredPosition = new Vector2(
                    origin.x,
                    origin.y - index * (height + 6f));
        }
    }

    private void RebindNativeRow(GameObject row, Action singleClick, Action doubleClick = null)
    {
        if (row == null || singleClick == null)
            return;
        var staleHit = FindDeep(row.transform, "MODDED_NATIVE_ROW_HIT_TARGET");
        if (staleHit != null)
            Object.Destroy(staleHit);

        ButtonManager buttonManager = null;
        foreach (var candidate in row.GetComponentsInChildren<ButtonManager>(true))
        {
            if (candidate == null)
                continue;
            if (buttonManager == null || candidate.targetButton != null)
                buttonManager = candidate;
            if (candidate.targetButton != null)
                break;
        }
        if (buttonManager == null)
        {
            // Retail operation rows use OperationSelectionUI + a native Unity
            // Button rather than DreamOS ButtonManager. Keep that shipped
            // Button as the only Selectable/event surface so its exact hover,
            // pressed, color, sound, and transition configuration remain intact.
            var selectionUi = row.GetComponentInChildren<OperationSelectionUI>(true);
            var nativeButton = selectionUi == null ? null : selectionUi.GetComponent<Button>();
            if (nativeButton == null)
                nativeButton = row.GetComponent<Button>();
            if (nativeButton == null)
                nativeButton = row.GetComponentInChildren<Button>(true);
            if (nativeButton == null)
            {
                log?.LogWarning("Cerberus native row binding skipped because the shipped row had neither ButtonManager nor Button: " + row.name + ".");
                return;
            }

            float doubleClickPeriod = 0.3f;
            try
            {
                var nativeClickOwner = selectionUi == null ? null : selectionUi.CerebusUiBase;
                if (nativeClickOwner != null && nativeClickOwner.clickcountTime >= 0.05f &&
                    nativeClickOwner.clickcountTime <= 1f)
                    doubleClickPeriod = nativeClickOwner.clickcountTime;
            }
            catch { }
            float previousClickTime = -100f;
            nativeButton.onClick = new Button.ButtonClickedEvent();
            nativeButton.onClick.AddListener((UnityAction)(() =>
            {
                float now = Time.unscaledTime;
                if (doubleClick != null && now - previousClickTime <= doubleClickPeriod)
                {
                    previousClickTime = -100f;
                    doubleClick();
                    return;
                }
                previousClickTime = now;
                singleClick();
            }));
            nativeButton.interactable = true;
            log?.LogInfo("Cerberus native row bound through shipped Unity Button: row=" + row.name +
                ", button=" + nativeButton.gameObject.name + ", doubleClickPeriod=" +
                doubleClickPeriod.ToString("0.###") + "s.");
            return;
        }

        // Keep the shipped control itself as the event surface. ButtonManager
        // owns the exact normal/highlight/pressed CanvasGroups, fading, sound,
        // ripple, pointer enter/leave behavior, and double-click period used by
        // vanilla operation rows. Only replace the cloned mission callbacks.
        buttonManager.onClick = new UnityEvent();
        buttonManager.onDoubleClick = new UnityEvent();
        buttonManager.checkForDoubleClick = doubleClick != null;
        buttonManager.onClick.AddListener((UnityAction)(() => singleClick()));
        if (doubleClick != null)
            buttonManager.onDoubleClick.AddListener((UnityAction)(() => doubleClick()));
        buttonManager.isInteractable = true;
        try
        {
            if (buttonManager.targetButton != null)
                buttonManager.targetButton.interactable = true;
        }
        catch { }
        try { buttonManager.UpdateState(); } catch { }
    }

    private static void SetNativeRowSelected(GameObject row, bool selected)
    {
        if (row == null)
            return;
        bool foundNativeState = false;
        foreach (var transform in row.GetComponentsInChildren<Transform>(true))
        {
            if (transform == null || transform == row.transform)
                continue;
            if (transform.name.IndexOf("Selected", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            transform.gameObject.SetActive(selected);
            foundNativeState = true;
        }
        if (!foundNativeState)
        {
            var image = row.GetComponent<Image>() ?? row.GetComponentInChildren<Image>(true);
            if (image != null)
                image.color = selected
                    ? new Color(image.color.r, image.color.g, image.color.b, 1f)
                    : new Color(image.color.r, image.color.g, image.color.b, 0.72f);
        }
    }

    private static GameObject CreateNativeActionButton(Transform parent, GameObject template,
        string name, string text, Vector2 anchorMin, Vector2 anchorMax, Action action)
    {
        if (parent == null || template == null || action == null)
            return null;
        var clone = Object.Instantiate(template, parent);
        if (clone == null)
            return null;
        clone.name = name;
        var rect = clone.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
        SetButtonText(clone, text);
        FitTabTitleText(clone, text);

        int lastLogicalFrame = -1;
        Action invokeOnce = () =>
        {
            int frame = Time.frameCount;
            if (frame == lastLogicalFrame)
                return;
            lastLogicalFrame = frame;
            action();
        };
        var panelButton = clone.GetComponent<PanelButton>();
        if (panelButton != null)
        {
            panelButton.onClick = new UnityEvent();
            panelButton.onClick.AddListener((UnityAction)(() => invokeOnce()));
            panelButton.isInteractable = true;
            panelButton.isSelected = false;
        }
        var unityButton = clone.GetComponent<Button>();
        if (unityButton == null)
            unityButton = clone.AddComponent<Button>();
        if (unityButton.targetGraphic == null)
        {
            var hitObject = new GameObject("MODDED_NATIVE_ACTION_HIT_TARGET");
            hitObject.transform.SetParent(clone.transform, false);
            SetFullStretch(hitObject.AddComponent<RectTransform>());
            var hitImage = hitObject.AddComponent<Image>();
            hitImage.color = new Color(0f, 0f, 0f, 0f);
            hitImage.raycastTarget = true;
            unityButton.targetGraphic = hitImage;
        }
        unityButton.transition = Selectable.Transition.None;
        unityButton.onClick.RemoveAllListeners();
        unityButton.onClick.AddListener((UnityAction)(() => invokeOnce()));
        unityButton.interactable = true;
        clone.SetActive(true);
        return clone;
    }

    private void QueueTransitionSnapshot(MissionLaptop laptop, GameObject page, string source, int requestedFrame)
    {
        pendingTransitionSnapshots.Add(new PendingTransitionSnapshot
        {
            RequestedFrame = requestedFrame,
            Laptop = laptop,
            Page = page,
            Source = source
        });
    }

    private void ProcessPendingTransitionSnapshots()
    {
        if (pendingTransitionSnapshots.Count == 0)
            return;
        int frame = Time.frameCount;
        for (int index = pendingTransitionSnapshots.Count - 1; index >= 0; index--)
        {
            var pending = pendingTransitionSnapshots[index];
            if (pending == null || frame <= pending.RequestedFrame)
                continue;
            pendingTransitionSnapshots.RemoveAt(index);
            if (pending.Laptop == null)
                continue;
            log.LogInfo(CaptureLaptopTransitionState(pending.Laptop, pending.Page,
                "next-frame state", pending.Source, frame));
        }
    }

    private static string CaptureLaptopTransitionState(MissionLaptop laptop, GameObject page,
        string phase, string eventSource, int frame)
    {
        if (laptop == null)
            return "Cerberus transition snapshot: laptop=null, phase=" + phase + ".";
        var manager = laptop.cerberusWindowPanelManager;
        var managerState = manager == null
            ? "null"
            : "currentPanelIndex=" + DescribeValue(ReadMember(manager, "currentPanelIndex")) +
              ", currentButtonIndex=" + DescribeValue(ReadMember(manager, "currentButtonIndex")) +
              ", newPanelIndex=" + DescribeValue(ReadMember(manager, "newPanelIndex")) +
              ", currentPanel=" + DescribeValue(ReadMember(manager, "currentPanel")) +
              ", currentButton=" + DescribeValue(ReadMember(manager, "currentButton")) +
              ", panels=" + DescribePanelItems(ReadMember(manager, "panels"));
        var networkType = ResolveLoadedTypeByExactName(
            "MissionLaptopNetworkState", "Il2Cpp.MissionLaptopNetworkState");
        var networkState = networkType == null ? null : ReadMember(networkType, null, "singleton");
        var home = page == null ? null : FindDeep(page.transform, "MODDED_HOME");
        var briefing = page == null ? null : FindDeep(page.transform, "MODDED_BRIEFING");
        return "Cerberus transition snapshot: phase=" + phase + ", source=" + eventSource +
            ", frame=" + frame + ", laptopId=" + laptop.GetInstanceID() +
            ", laptopPath=" + HierarchyPath(laptop.transform) +
            ", manager={" + managerState + "}" +
            ", ActiveOperationsTab={" + DescribeActivity(laptop.ActiveOperationsTab) + "}" +
            ", SimulationOperationsTab={" + DescribeActivity(laptop.SimulationOperationsTab) + "}" +
            ", ActiveOperationsList={" + DescribeActivity(ExtractGameObject(ReadMember(laptop, "ActiveOperationsList"))) + "}" +
            ", SimulationOperationList={" + DescribeActivity(ExtractGameObject(ReadMember(laptop, "SimulationOperationList"))) + "}" +
            ", TargetPackageParent={" + DescribeActivity(ExtractGameObject(ReadMember(laptop, "TargetPackageParent"))) + "}" +
            ", opBoardParent={" + DescribeActivity(ExtractGameObject(ReadMember(laptop, "opBoardParent"))) + "}" +
            ", network={CurrentLaptopPage=" + DescribeValue(ReadMember(networkState, "CurrentLaptopPage")) +
            ", OnSimulationPage=" + DescribeValue(ReadMember(networkState, "OnSimulationPage")) + "}" +
            ", displayers=" + DescribeMissionLaptopDisplayers() +
            ", modPage={" + DescribeActivity(page) + "}" +
            ", modHome={" + DescribeActivity(home) + "}" +
            ", modBriefing={" + DescribeActivity(briefing) + "}.";
    }

    private static string DescribePanelItems(object panelList)
    {
        var panels = ReadListItems(panelList);
        var descriptions = new List<string>(panels.Count);
        for (int index = 0; index < panels.Count; index++)
        {
            var panel = panels[index];
            var panelObject = ExtractGameObject(ReadMember(panel, "panelObject"));
            var panelButton = ExtractGameObject(ReadMember(panel, "panelButton"));
            descriptions.Add(index + ":name=" + DescribeValue(ReadMember(panel, "panelName")) +
                ", panel=" + DescribeActivity(panelObject) +
                ", button=" + DescribeActivity(panelButton));
        }
        return "[" + string.Join(" | ", descriptions) + "]";
    }

    private static string DescribeMissionLaptopDisplayers()
    {
        var type = ResolveLoadedTypeByExactName(
            "MissionLaptopDisplayer", "Il2Cpp.MissionLaptopDisplayer");
        if (type == null)
            return "type-unresolved";
        var components = FindMissionLaptopComponents(type);
        var descriptions = new List<string>(components.Count);
        foreach (var component in components)
        {
            if (component == null || component.gameObject == null)
                continue;
            descriptions.Add("id=" + component.GetInstanceID() +
                ", path=" + HierarchyPath(component.transform) +
                ", selectedOperation=" + DescribeValue(ReadFirstMember(component,
                    "selectedOperation", "SelectedOperation", "currentOperation")) +
                ", selectedTargetPackage=" + DescribeValue(ReadFirstMember(component,
                    "selectedTargetPackage", "SelectedTargetPackage", "currentTargetPackage")) +
                ", opboard=" + DescribeValue(ReadFirstMember(component,
                    "opBoard", "opboard", "operationBoard", "currentOpBoard")));
        }
        return "[" + string.Join(" | ", descriptions) + "]";
    }

    private static object ReadFirstMember(object owner, params string[] names)
    {
        if (owner == null || names == null)
            return null;
        foreach (var name in names)
        {
            var value = ReadMember(owner, name);
            if (value != null)
                return value;
        }
        return null;
    }

    private static object ReadMember(object owner, string name)
    {
        return owner == null ? null : ReadMember(owner.GetType(), owner, name);
    }

    private static object ReadMember(Type type, object owner, string name)
    {
        if (type == null || string.IsNullOrEmpty(name))
            return null;
        try
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static;
            var property = type.GetProperty(name, flags);
            if (property != null)
                return property.GetValue(owner);
            return type.GetField(name, flags)?.GetValue(owner);
        }
        catch { return null; }
    }

    private static List<object> ReadListItems(object list)
    {
        var result = new List<object>();
        if (list == null)
            return result;
        try
        {
            if (list is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                    if (item != null)
                        result.Add(item);
                if (result.Count > 0)
                    return result;
            }
        }
        catch { }
        try
        {
            var countValue = ReadMember(list, "Count") ?? ReadMember(list, "Length");
            if (!(countValue is int count) || count <= 0)
                return result;
            var type = list.GetType();
            var indexer = type.GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var getter = indexer?.GetGetMethod(true) ?? type.GetMethod("get_Item",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(int) }, null);
            if (getter == null)
                return result;
            for (int index = 0; index < count; index++)
            {
                var item = getter.Invoke(list, new object[] { index });
                if (item != null)
                    result.Add(item);
            }
        }
        catch { }
        return result;
    }

    private static Type ResolveLoadedTypeByExactName(params string[] names)
    {
        if (names == null)
            return null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var name in names)
            {
                try
                {
                    var type = assembly.GetType(name, false, false);
                    if (type != null)
                        return type;
                }
                catch { }
            }
        }
        return null;
    }

    private static string DescribeValue(object value)
    {
        if (value == null)
            return "null";
        var gameObject = ExtractGameObject(value);
        if (gameObject != null)
            return gameObject.name + "@" + HierarchyPath(gameObject.transform);
        try { return Convert.ToString(value); }
        catch { return value.GetType().Name; }
    }

    private static GameObject FindNativeActiveOperationsButton(Transform root)
    {
        if (root == null)
            return null;
        PanelButton[] buttons;
        try { buttons = root.GetComponentsInChildren<PanelButton>(true); }
        catch { return null; }
        GameObject fallback = null;
        foreach (var button in buttons)
        {
            if (button == null)
                continue;
            string name = button.gameObject.name ?? string.Empty;
            if (name.IndexOf("ACTIVE OPERATIONS BUTTON", StringComparison.OrdinalIgnoreCase) >= 0)
                return button.gameObject;
            if (fallback == null && name.IndexOf("ACTIVE", StringComparison.OrdinalIgnoreCase) >= 0)
                fallback = button.gameObject;
        }
        return fallback;
    }

    private static GameObject FindNativeSimulationOperationsButton(Transform root)
    {
        if (root == null)
            return null;
        PanelButton[] buttons;
        try { buttons = root.GetComponentsInChildren<PanelButton>(true); }
        catch { return null; }
        foreach (var button in buttons)
        {
            if (button == null)
                continue;
            string name = button.gameObject.name ?? string.Empty;
            if (name.IndexOf("OPERATION SIMULATION BUTTON", StringComparison.OrdinalIgnoreCase) >= 0)
                return button.gameObject;
        }
        return null;
    }

    private static void PositionAsThirdTab(Transform parent, GameObject activeButton, GameObject simulationButton, GameObject moddedButton)
    {
        if (parent == null || moddedButton == null)
            return;

        var parentRect = parent.GetComponent<RectTransform>();
        var moddedRect = moddedButton.GetComponent<RectTransform>();
        var simulationRect = simulationButton == null ? null : simulationButton.GetComponent<RectTransform>();
        var activeRect = activeButton == null ? null : activeButton.GetComponent<RectTransform>();
        var layout = parent.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            int simulationIndex = simulationButton == null ? activeButton.transform.GetSiblingIndex() : simulationButton.transform.GetSiblingIndex();
            moddedButton.transform.SetSiblingIndex(Mathf.Min(simulationIndex + 1, parent.childCount - 1));
            Canvas.ForceUpdateCanvases();
            logStatic("native tab row uses HorizontalLayoutGroup; inserted after simulation");
            return;
        }

        if (moddedRect == null || activeRect == null || parentRect == null)
            return;

        // The old implementation converted world-space corners back through
        // a scaled world-space canvas and then assigned rect.position. On the
        // shipped world-space DreamOS canvas that produced values tens of
        // thousands of pixels away from the row, even though the clone was
        // correctly parented. Work entirely in the parent's local coordinate
        // system instead. This is the same coordinate space used by the
        // official RectTransforms and remains stable at any UI scale.
        var activeBounds = BoundsInParent(parentRect, activeRect);
        var simulationBounds = simulationRect == null
            ? new Bounds(new Vector3(activeBounds.max.x + activeBounds.size.x, activeBounds.center.y, 0f), activeBounds.size)
            : BoundsInParent(parentRect, simulationRect);
        float rowLeft = activeBounds.min.x;
        float rowRight = simulationRect == null ? activeBounds.max.x + activeBounds.size.x : simulationBounds.max.x;
        float centerY = activeBounds.center.y;

        logStatic("native source rect=" + RectSummary(activeButton) + ", simulation rect=" + RectSummary(simulationButton));

        // Keep the third tab inside the left operation-list column. The
        // briefing label is a sibling on the same parent in the retail UI.
        float briefingLeft = float.PositiveInfinity;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null || child == activeButton.transform ||
                (simulationButton != null && child == simulationButton.transform) ||
                child == moddedButton.transform ||
                child.name.IndexOf("BRIEFING", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            var briefingRect = child.GetComponent<RectTransform>();
            if (briefingRect == null)
                continue;
            var briefingBounds = BoundsInParent(parentRect, briefingRect);
            if (briefingBounds.min.x > rowLeft && briefingBounds.min.x < briefingLeft)
                briefingLeft = briefingBounds.min.x;
        }
        if (!float.IsPositiveInfinity(briefingLeft))
            rowRight = Mathf.Min(rowRight, briefingLeft - 12f);
        rowRight = Mathf.Min(rowRight, parentRect.rect.xMax - 8f);
        rowLeft = Mathf.Max(rowLeft, parentRect.rect.xMin + 8f);

        float gap = 8f;
        float available = rowRight - rowLeft;
        float tabWidth = (available - gap * 2f) / 3f;
        if (tabWidth < 96f)
            tabWidth = Mathf.Max(96f, Mathf.Min(activeBounds.size.x, simulationBounds.size.x));
        if (tabWidth * 3f + gap * 2f > parentRect.rect.width - 16f)
            tabWidth = Mathf.Max(96f, (parentRect.rect.width - 16f - gap * 2f) / 3f);

        SetTabRect(activeRect, parentRect, rowLeft + tabWidth * 0.5f, centerY, tabWidth);
        if (simulationRect != null)
            SetTabRect(simulationRect, parentRect, rowLeft + tabWidth + gap + tabWidth * 0.5f, centerY, tabWidth);
        SetTabRect(moddedRect, parentRect, rowLeft + (tabWidth + gap) * 2f + tabWidth * 0.5f, centerY, tabWidth);

        int simulationSibling = simulationButton == null ? activeButton.transform.GetSiblingIndex() : simulationButton.transform.GetSiblingIndex();
        moddedButton.transform.SetSiblingIndex(Mathf.Min(simulationSibling + 1, parent.childCount - 1));
        Canvas.ForceUpdateCanvases();
        logStatic("native tab row normalized in parent-local coordinates: rowLeft=" + rowLeft.ToString("F1") +
            ", rowRight=" + rowRight.ToString("F1") + ", tabWidth=" + tabWidth.ToString("F1") +
            ", parent=" + parentRect.rect.size + ");");
    }

    private static void GetRectBoundsInParent(RectTransform rect, RectTransform parent, out float left, out float right, out float bottom, out float top)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        left = float.PositiveInfinity;
        right = float.NegativeInfinity;
        bottom = float.PositiveInfinity;
        top = float.NegativeInfinity;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 local = parent.InverseTransformPoint(corners[i]);
            left = Mathf.Min(left, local.x);
            right = Mathf.Max(right, local.x);
            bottom = Mathf.Min(bottom, local.y);
            top = Mathf.Max(top, local.y);
        }
    }

    private static Bounds BoundsInParent(RectTransform parent, RectTransform child)
    {
        if (parent == null || child == null)
            return new Bounds(Vector3.zero, Vector3.zero);
        // The official tab children are direct children of Operation Selection
        // and their localPosition/rect values already share the parent's local
        // coordinate system.  Using GetWorldCorners here is tempting, but the
        // retail IL2CPP wrapper can return invalid world-space data for this
        // world-space canvas.  Compute the edges directly instead.
        float width = child.rect.width;
        float height = child.rect.height;
        float left = child.localPosition.x - width * child.pivot.x;
        float right = child.localPosition.x + width * (1f - child.pivot.x);
        float bottom = child.localPosition.y - height * child.pivot.y;
        float top = child.localPosition.y + height * (1f - child.pivot.y);
        var center = new Vector3((left + right) * 0.5f, (bottom + top) * 0.5f, 0f);
        var size = new Vector3(Mathf.Max(0f, right - left), Mathf.Max(0f, top - bottom), 0f);
        return new Bounds(center, size);
    }

    private static void SetTabRect(RectTransform rect, RectTransform parent, float centerX, float centerY, float width)
    {
        if (rect == null || parent == null)
            return;
        float height = Mathf.Max(1f, rect.rect.height);
        // Center anchors eliminate the anchor-offset conversion that caused
        // the previous world-space assignment to explode on the laptop's
        // scaled canvas. localPosition is directly in parent coordinates.
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        Vector3 local = rect.localPosition;
        local.x = centerX;
        local.y = centerY;
        rect.localPosition = local;
    }

    private static void CopyContentRect(GameObject source, RectTransform destination)
    {
        if (destination == null)
            return;
        var sourceRect = source == null ? null : source.GetComponent<RectTransform>();
        if (sourceRect == null)
        {
            destination.anchorMin = Vector2.zero;
            destination.anchorMax = Vector2.one;
            destination.offsetMin = Vector2.zero;
            destination.offsetMax = Vector2.zero;
            return;
        }
        destination.anchorMin = sourceRect.anchorMin;
        destination.anchorMax = sourceRect.anchorMax;
        destination.pivot = sourceRect.pivot;
        destination.anchoredPosition = sourceRect.anchoredPosition;
        destination.sizeDelta = sourceRect.sizeDelta;
        destination.localScale = sourceRect.localScale;
        destination.localRotation = sourceRect.localRotation;
    }

    private static void SetTabSelectedState(GameObject button, bool selected)
    {
        if (button == null)
            return;
        var panelButton = button.GetComponent<PanelButton>();
        if (panelButton != null)
        {
            panelButton.isSelected = selected;
            panelButton.SetSelected(selected);
            panelButton.UpdateUI();
            return;
        }
        var buttonManager = button.GetComponent<ButtonManager>();
        if (buttonManager != null)
            buttonManager.UpdateState();
    }

    private static string RectSummary(GameObject go)
    {
        if (go == null)
            return "null";
        var rect = go.GetComponent<RectTransform>();
        return rect == null ? "no-rect" : "anchored=" + rect.anchoredPosition + ", local=" + rect.localPosition + ", size=" + rect.rect.size + ", pivot=" + rect.pivot + ", parent=" + (go.transform.parent == null ? "null" : go.transform.parent.name);
    }

    private static void logStatic(string message)
    {
        if (instance != null && instance.log != null)
            instance.log.LogInfo("Cerberus " + message + ".");
    }

    private static void FitTabTitleText(GameObject button, string renderedTitle)
    {
        if (button == null || string.IsNullOrWhiteSpace(renderedTitle))
            return;
        int fitted = 0;
        try
        {
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label == null || !TitleEquals(label.text, renderedTitle))
                    continue;
                float authoredMaximum = Mathf.Max(1f, label.fontSize);
                var rect = label.rectTransform;
                if (rect != null)
                {
                    // The shipped title is authored for the original two-tab
                    // width. When the root is narrowed to one third, its
                    // fixed-width title rect otherwise continues into the next
                    // tab even though TMP auto-sizing is enabled. Stretch the
                    // title itself to the resized button with a small inset;
                    // preserve every vertical anchor/offset and all state art.
                    var anchorMin = rect.anchorMin;
                    var anchorMax = rect.anchorMax;
                    var offsetMin = rect.offsetMin;
                    var offsetMax = rect.offsetMax;
                    rect.anchorMin = new Vector2(0f, anchorMin.y);
                    rect.anchorMax = new Vector2(1f, anchorMax.y);
                    rect.offsetMin = new Vector2(12f, offsetMin.y);
                    rect.offsetMax = new Vector2(-12f, offsetMax.y);
                }
                label.enableWordWrapping = false;
                label.enableAutoSizing = true;
                label.fontSizeMax = authoredMaximum;
                label.fontSizeMin = Mathf.Max(10f, authoredMaximum * 0.55f);
                label.ForceMeshUpdate(true, true);
                fitted++;
            }
        }
        catch { }
        Canvas.ForceUpdateCanvases();
        logStatic("title fit applied: button=" + button.name + ", renderedTitle='" + renderedTitle +
            "', copies=" + fitted);
    }

    private static bool TitleEquals(string value, string expected)
    {
        return string.Equals(NormalizeTitle(value), NormalizeTitle(expected),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        return string.Join(" ", value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string HierarchyPath(Transform transform)
    {
        if (transform == null)
            return "<null>";
        var names = new List<string>();
        for (var current = transform; current != null; current = current.parent)
            names.Add(current.name ?? "<unnamed>");
        names.Reverse();
        return string.Join("/", names);
    }

    private static string DescribeActivity(GameObject gameObject)
    {
        return gameObject == null
            ? "null"
            : "activeSelf=" + gameObject.activeSelf + ", activeInHierarchy=" +
              gameObject.activeInHierarchy + ", path=" + HierarchyPath(gameObject.transform);
    }

    private static GameObject FindDeep(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root.gameObject;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDeep(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static void OpenModdedPage(MissionLaptop laptop, GameObject page)
    {
        if (laptop != null)
        {
            if (laptop.ActiveOperationsTab != null)
                laptop.ActiveOperationsTab.SetActive(false);
            if (laptop.SimulationOperationsTab != null)
                laptop.SimulationOperationsTab.SetActive(false);
        }
        if (page != null)
        {
            page.SetActive(true);
            var home = FindDeep(page.transform, "MODDED_HOME");
            var briefing = FindDeep(page.transform, "MODDED_BRIEFING");
            if (home != null) home.SetActive(true);
            if (briefing != null) briefing.SetActive(false);
        }
    }

    private void DumpOperationSelectionHierarchy(MissionLaptop laptop)
    {
        try
        {
            var tab = laptop.ActiveOperationsTab;
            if (tab == null)
            {
                log.LogWarning("Cerberus hierarchy probe: ActiveOperationsTab is null.");
                return;
            }
            log.LogInfo("Cerberus hierarchy probe: ActiveOperationsTab=" + Describe(tab) + ".");
            DumpHierarchy(tab.transform.parent == null ? tab.transform : tab.transform.parent, 0, 3);
        }
        catch (Exception ex)
        {
            log.LogWarning("Cerberus hierarchy probe failed: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private void DumpHierarchy(Transform node, int depth, int maxDepth)
    {
        if (node == null || depth > maxDepth)
            return;
        var componentNames = new List<string>();
        try
        {
            foreach (var component in node.gameObject.GetComponents<Component>())
            {
                if (component != null)
                    componentNames.Add(component.GetType().FullName);
            }
        }
        catch { }
        bool hasPanelButton = false, hasButtonManager = false, hasUnityButton = false, hasTmp = false;
        try { hasPanelButton = node.GetComponent<PanelButton>() != null; } catch { }
        try { hasButtonManager = node.GetComponent<ButtonManager>() != null; } catch { }
        try { hasUnityButton = node.GetComponent<Button>() != null; } catch { }
        try { hasTmp = node.GetComponent<TMP_Text>() != null; } catch { }
        log.LogInfo("Cerberus hierarchy " + new string(' ', depth * 2) + node.name + " components=[" + string.Join(",", componentNames) + "] typed panelButton=" + hasPanelButton + " buttonManager=" + hasButtonManager + " unityButton=" + hasUnityButton + " tmp=" + hasTmp);
        for (int i = 0; i < node.childCount; i++)
            DumpHierarchy(node.GetChild(i), depth + 1, maxDepth);
    }

    private static string Describe(GameObject gameObject)
    {
        return gameObject == null ? "null" : gameObject.name + " parent=" + (gameObject.transform.parent == null ? "null" : gameObject.transform.parent.name);
    }

    private static GameObject FindChild(Transform parent, string name)
    {
        if (parent == null)
            return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child != null && child.name == name)
                return child.gameObject;
        }
        return null;
    }

    private static void SetButtonText(GameObject button, string text)
    {
        if (button == null)
            return;
        DisableLocalizationComponent(button);
        var panelButton = button.GetComponent<PanelButton>();
        if (panelButton != null)
        {
            panelButton.buttonText = text;
            panelButton.useLocalization = false;
            panelButton.useCustomText = true;
        }
        var buttonManager = button.GetComponent<ButtonManager>();
        if (buttonManager != null)
        {
            buttonManager.buttonText = text;
            buttonManager.useLocalization = false;
        }
        foreach (var tmp in button.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp == null)
                continue;
            DisableLocalizationComponent(tmp.gameObject);
            tmp.text = text;
        }
    }

}
