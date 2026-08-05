using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;
using Michsky.DreamOS;
using Mirror;
using OperatorModAPI;
using TMPro;
using TMPro.Examples;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[BepInPlugin("operator.modded-operations", "OPERATOR: Modded Operations", "0.3.19")]
[BepInProcess("OPERATOR.exe")]
[BepInDependency("operator.modapi", "0.2.0-alpha.3")]
public sealed class CerberusNativeTabFix : BasePlugin
{
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

	private sealed class PendingTransitionSnapshot
	{
		public int RequestedFrame;

		public MissionLaptop Laptop;

		public GameObject Page;

		public string Source;
	}

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

		public readonly System.Collections.Generic.List<AssetBundle> Dependencies = new System.Collections.Generic.List<AssetBundle>();

		public readonly System.Collections.Generic.Dictionary<string, AssetBundle> DependenciesByPath = new System.Collections.Generic.Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);

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

		public readonly System.Collections.Generic.List<string> BundlePaths = new System.Collections.Generic.List<string>();

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

		public GameMode GameModeComponent;

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

		public readonly System.Collections.Generic.List<TerrainLayer> RuntimeTerrainLayers = new System.Collections.Generic.List<TerrainLayer>();

		public readonly System.Collections.Generic.Dictionary<int, int> PositionedPlayerObjects = new System.Collections.Generic.Dictionary<int, int>();

		public readonly System.Collections.Generic.Dictionary<int, string> PlayerMarkerNames = new System.Collections.Generic.Dictionary<int, string>();

		public readonly System.Collections.Generic.Dictionary<int, int> PlayerSpawnRequestFrames = new System.Collections.Generic.Dictionary<int, int>();

		public readonly System.Collections.Generic.Dictionary<int, int> PlayerSpawnRequestCounts = new System.Collections.Generic.Dictionary<int, int>();

		public readonly HashSet<int> CompletedPlayerSpawnIds = new HashSet<int>();

		public readonly System.Collections.Generic.Dictionary<int, int> PlayerMoveRequestFrames = new System.Collections.Generic.Dictionary<int, int>();

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

		public readonly System.Collections.Generic.List<VolumeProfile> RuntimeRenderProfiles = new System.Collections.Generic.List<VolumeProfile>();

		public bool PveSpawnAttempted;

		public int PveEnemyCount;

		public GameObject RaidUtilityRoot;

		public readonly System.Collections.Generic.List<UnityEngine.Object> RuntimePvpAssets = new System.Collections.Generic.List<UnityEngine.Object>();
	}

	private sealed class FixRunner : MonoBehaviour
	{
		public FixRunner(IntPtr pointer)
			: base(pointer)
		{
		}

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
		public StandalonePvpGameMode(IntPtr pointer)
			: base(pointer)
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
				base.Server_AllPlayersLoaded();
				CerberusNativeTabFix.instance?.OnStandaloneAllPlayersLoaded(nativePvpLifecycle: true);
			}
			catch (Exception exception)
			{
				CerberusNativeTabFix.instance?.OnStandalonePvpAllPlayersLoadedFailed(exception);
			}
		}

		public void EnsureStandaloneReadiness(string source)
		{
			EnterNativeReadiness(source);
		}

		private void EnterNativeReadiness(string source)
		{
			if (CerberusNativeTabFix.instance == null || !CerberusNativeTabFix.instance.TryClaimStandaloneReadinessInitialization(this))
			{
				return;
			}
			try
			{
				base.OnStartClient();
				CerberusNativeTabFix.instance.MarkStandaloneReadinessInitialized(this, source);
			}
			catch (Exception exception)
			{
				CerberusNativeTabFix.instance.MarkStandaloneReadinessInitializationFailed(this, source, exception);
			}
		}
	}

	private sealed class StandalonePveGameMode : InfiltrationManager
	{
		public StandalonePveGameMode(IntPtr pointer)
			: base(pointer)
		{
		}

		public override void OnStartServer()
		{
		}

		public override void OnStartClient()
		{
			EnsureStandaloneReadiness("OnStartClient");
		}

		public override void Server_AllPlayersLoaded()
		{
			CerberusNativeTabFix.instance?.OnStandaloneAllPlayersLoaded(nativePvpLifecycle: false);
		}

		public void EnsureStandaloneReadiness(string source)
		{
			if (CerberusNativeTabFix.instance == null || !CerberusNativeTabFix.instance.TryClaimStandaloneReadinessInitialization(this))
			{
				return;
			}
			try
			{
				Initialize();
				CerberusNativeTabFix.instance.MarkStandaloneReadinessInitialized(this, source);
			}
			catch (Exception exception)
			{
				CerberusNativeTabFix.instance.MarkStandaloneReadinessInitializationFailed(this, source, exception);
			}
		}
	}

	internal const string RequiredApiVersion = "0.2.0-alpha.3";

	private const uint StandalonePveGameModeAssetId = 1297043457u;

	private const uint StandalonePvpGameModeAssetId = 1297043458u;

	private static CerberusNativeTabFix instance;

	private ManualLogSource log;

	private FixRunner runner;

	private readonly System.Collections.Generic.Dictionary<string, LoadedMapBundles> loadedMapBundles = new System.Collections.Generic.Dictionary<string, LoadedMapBundles>(StringComparer.Ordinal);

	private readonly System.Collections.Generic.Dictionary<string, Sprite> previewSprites = new System.Collections.Generic.Dictionary<string, Sprite>(StringComparer.Ordinal);

	private readonly System.Collections.Generic.Dictionary<string, Texture2D> previewTextures = new System.Collections.Generic.Dictionary<string, Texture2D>(StringComparer.Ordinal);

	private readonly System.Collections.Generic.Dictionary<string, Texture3D> packageTonemapLuts = new System.Collections.Generic.Dictionary<string, Texture3D>(StringComparer.Ordinal);

	private GameObject operationBoardVisualTemplate;

	private readonly System.Collections.Generic.Dictionary<int, CatalogPresentation> catalogPresentations = new System.Collections.Generic.Dictionary<int, CatalogPresentation>();

	private PendingMapLaunch pendingLaunch;

	private ActiveMapOperation activeOperation;

	private UnityAction<Scene, LoadSceneMode> sceneLoadedCallback;

	private UnityAction<Scene> sceneUnloadedCallback;

	private readonly HashSet<int> attachedLaptops = new HashSet<int>();

	private readonly HashSet<int> deferredSetupLoggedLaptops = new HashSet<int>();

	private readonly HashSet<string> lutDiagnostics = new HashSet<string>(StringComparer.Ordinal);

	private readonly HashSet<int> nativeBackBoundBoards = new HashSet<int>();

	private MethodInfo directServerPlayerSpawnMethod;

	private readonly System.Collections.Generic.List<NativePresentationBinding> nativePresentationBindings = new System.Collections.Generic.List<NativePresentationBinding>();

	private readonly System.Collections.Generic.List<PendingTransitionSnapshot> pendingTransitionSnapshots = new System.Collections.Generic.List<PendingTransitionSnapshot>();

	private string lastDiagnostic;

	public override void Load()
	{
		if (!string.Equals(OperatorApi.ApiVersion, "0.2.0-alpha.3", StringComparison.Ordinal))
		{
			base.Log.LogError("Modded Operations requires Operator Mod API 0.2.0-alpha.3, but Core exposes " + OperatorApi.ApiVersion + ". Adapter startup was refused.");
			return;
		}
		directServerPlayerSpawnMethod = typeof(PlayerMaster).GetMethod("UserCode_CMDSpawnPlayer__NetworkIdentity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(NetworkIdentity) }, null);
		if (directServerPlayerSpawnMethod == null)
		{
			base.Log.LogError("Modded Operations could not resolve the exact current-build PlayerMaster generated server spawn body. Adapter startup was refused.");
			return;
		}
		instance = this;
		log = base.Log;
		ClassInjector.RegisterTypeInIl2Cpp<FixRunner>();
		ClassInjector.RegisterTypeInIl2Cpp<StandalonePvpGameMode>();
		ClassInjector.RegisterTypeInIl2Cpp<StandalonePveGameMode>();
		GameObject gameObject = new GameObject("Operator_Cerberus_Native_Tab_Fix_Runner");
		UnityEngine.Object.DontDestroyOnLoad(gameObject);
		runner = gameObject.AddComponent<FixRunner>();
		runner.InvokeRepeating("Tick", 1f, 1f);
		sceneLoadedCallback = DelegateSupport.ConvertDelegate<UnityAction<Scene, LoadSceneMode>>(new Action<Scene, LoadSceneMode>(OnSceneLoaded));
		sceneUnloadedCallback = DelegateSupport.ConvertDelegate<UnityAction<Scene>>(new Action<Scene>(OnSceneUnloaded));
		SceneManager.sceneLoaded += sceneLoadedCallback;
		SceneManager.sceneUnloaded += sceneUnloadedCallback;
		log.LogInfo("Modded Operations infrastructure loaded; catalog=" + OperatorApi.ModdedOperations.CatalogId + ", maps=" + OperatorApi.ModdedOperations.Maps.Count + ", operations=" + OperatorApi.ModdedOperations.Operations.Count + ".");
	}

	public static T LoadVerifiedMapDependencyAsset<T>(string mapId, string assetPath) where T : UnityEngine.Object
	{
		if (instance == null || string.IsNullOrWhiteSpace(mapId) || string.IsNullOrWhiteSpace(assetPath))
		{
			return null;
		}
		return instance.LoadVerifiedMapDependencyAssetInternal<T>(mapId, assetPath);
	}

	private T LoadVerifiedMapDependencyAssetInternal<T>(string mapId, string assetPath) where T : UnityEngine.Object
	{
		if (!loadedMapBundles.TryGetValue(mapId, out var value) || value == null)
		{
			return null;
		}
		foreach (AssetBundle dependency in value.Dependencies)
		{
			if (dependency == null)
			{
				continue;
			}
			try
			{
				T val = dependency.LoadAsset<T>(assetPath);
				if (val != null)
				{
					return val;
				}
			}
			catch
			{
			}
		}
		return null;
	}

	public override bool Unload()
	{
		try
		{
			if (sceneLoadedCallback != null)
			{
				SceneManager.sceneLoaded -= sceneLoadedCallback;
			}
			if (sceneUnloadedCallback != null)
			{
				SceneManager.sceneUnloaded -= sceneUnloadedCallback;
			}
			sceneLoadedCallback = null;
			sceneUnloadedCallback = null;
			foreach (CatalogPresentation value in catalogPresentations.Values)
			{
				if (value?.PreparationPanel != null)
				{
					UnityEngine.Object.Destroy(value.PreparationPanel);
				}
				if (value?.Page != null)
				{
					UnityEngine.Object.Destroy(value.Page);
				}
				if (value?.NativeBoardData != null)
				{
					UnityEngine.Object.Destroy(value.NativeBoardData);
				}
				if (value?.NativeTargetData != null)
				{
					UnityEngine.Object.Destroy(value.NativeTargetData);
				}
				if (value?.NativeInfiltrationMapPrefab != null)
				{
					UnityEngine.Object.Destroy(value.NativeInfiltrationMapPrefab);
				}
			}
			catalogPresentations.Clear();
			nativePresentationBindings.Clear();
			attachedLaptops.Clear();
			nativeBackBoundBoards.Clear();
			foreach (LoadedMapBundles value2 in loadedMapBundles.Values)
			{
				try
				{
					value2?.SceneBundle?.Unload(unloadAllLoadedObjects: false);
				}
				catch
				{
				}
				if (value2 == null)
				{
					continue;
				}
				foreach (AssetBundle dependency in value2.Dependencies)
				{
					try
					{
						dependency?.Unload(unloadAllLoadedObjects: false);
					}
					catch
					{
					}
				}
			}
			loadedMapBundles.Clear();
			foreach (Sprite value3 in previewSprites.Values)
			{
				if (value3 != null)
				{
					UnityEngine.Object.Destroy(value3);
				}
			}
			foreach (Texture2D value4 in previewTextures.Values)
			{
				if (value4 != null)
				{
					UnityEngine.Object.Destroy(value4);
				}
			}
			previewSprites.Clear();
			previewTextures.Clear();
			foreach (Texture3D value5 in packageTonemapLuts.Values)
			{
				if (value5 != null)
				{
					UnityEngine.Object.Destroy(value5);
				}
			}
			packageTonemapLuts.Clear();
			ReleaseStandaloneSceneContracts(activeOperation);
			ReleaseRuntimeTerrain(activeOperation);
			if (activeOperation?.BootstrapRoot != null)
			{
				UnityEngine.Object.Destroy(activeOperation.BootstrapRoot);
			}
			activeOperation = null;
			pendingLaunch = null;
			if (runner != null)
			{
				UnityEngine.Object.Destroy(runner.gameObject);
			}
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

	private void TryAttachAll()
	{
		Type type = ResolveMissionLaptopType();
		if (type == null)
		{
			LogOnce("MissionLaptop type was not found in the loaded interop assemblies.", warning: true);
			return;
		}
		foreach (Component item in FindMissionLaptopComponents(type))
		{
			MissionLaptop missionLaptop = item as MissionLaptop;
			if (missionLaptop == null || missionLaptop.gameObject == null || missionLaptop.cerberusWindowPanelManager == null)
			{
				continue;
			}
			Scene scene = missionLaptop.gameObject.scene;
			if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
			{
				continue;
			}
			int instanceID = missionLaptop.GetInstanceID();
			if (attachedLaptops.Contains(instanceID))
			{
				Transform transform = ((missionLaptop.ActiveOperationsTab == null) ? null : missionLaptop.ActiveOperationsTab.transform.parent);
				GameObject gameObject = FindDeep(transform, "MODDED_OPS_NATIVE_TAB") ?? FindDeep(transform, "MODDED_OPS_TAB");
				if (gameObject != null)
				{
					GameObject gameObject2 = FindChild(transform, "MODDED_OPERATIONS_PAGE");
					GameObject obj = ((gameObject2 == null) ? null : FindDeep(gameObject2.transform, "MODDED_NATIVE_HOME"));
					GameObject gameObject3 = FindNativeModdedPreparationPanel(missionLaptop);
					bool flag = OperatorApi.ModdedOperations.Operations.Count == 0;
					if (!(obj == null) && (flag || !(gameObject3 == null)))
					{
						SetButtonText(gameObject, "MODDED OPERATIONS");
						continue;
					}
					attachedLaptops.Remove(instanceID);
				}
				attachedLaptops.Remove(instanceID);
			}
			if (TryAttachNativeTab(missionLaptop))
			{
				attachedLaptops.Add(instanceID);
			}
		}
	}

	private void LogOnce(string message, bool warning)
	{
		if (!string.Equals(lastDiagnostic, message, StringComparison.Ordinal))
		{
			lastDiagnostic = message;
			if (warning)
			{
				log?.LogWarning("Cerberus native tab fix: " + message);
			}
			else
			{
				log?.LogInfo("Cerberus native tab fix: " + message);
			}
		}
	}

	private static System.Collections.Generic.List<Component> FindMissionLaptopComponents(Type laptopType)
	{
		System.Collections.Generic.List<Component> list = new System.Collections.Generic.List<Component>(4);
		if (laptopType == null)
		{
			return list;
		}
		try
		{
			MethodInfo methodInfo = null;
			MethodInfo[] methods = typeof(Resources).GetMethods(BindingFlags.Static | BindingFlags.Public);
			foreach (MethodInfo methodInfo2 in methods)
			{
				if (methodInfo2.Name == "FindObjectsOfTypeAll" && methodInfo2.IsGenericMethodDefinition && methodInfo2.GetGenericArguments().Length == 1 && methodInfo2.GetParameters().Length == 0)
				{
					methodInfo = methodInfo2;
					break;
				}
			}
			if (methodInfo == null)
			{
				return list;
			}
			if (!(methodInfo.MakeGenericMethod(laptopType).Invoke(null, null) is IEnumerable enumerable))
			{
				return list;
			}
			foreach (object item2 in enumerable)
			{
				if (item2 is Component item && !list.Contains(item))
				{
					list.Add(item);
					continue;
				}
				Component existingComponentByManagedType = GetExistingComponentByManagedType(ExtractGameObject(item2), laptopType);
				if (existingComponentByManagedType != null && !list.Contains(existingComponentByManagedType))
				{
					list.Add(existingComponentByManagedType);
				}
			}
		}
		catch (Exception ex)
		{
			if (instance != null)
			{
				instance.LogOnce("MissionLaptop discovery failed: " + ex.GetType().Name + ": " + ex.Message, warning: true);
			}
		}
		return list;
	}

	private static Type ResolveMissionLaptopType()
	{
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (Assembly assembly in assemblies)
		{
			Type type = assembly.GetType("MissionLaptop", throwOnError: false, ignoreCase: false) ?? assembly.GetType("Il2Cpp.MissionLaptop", throwOnError: false, ignoreCase: false);
			if (type != null)
			{
				return type;
			}
		}
		return null;
	}

	private static Component GetExistingComponentByManagedType(GameObject gameObject, Type componentType)
	{
		if (gameObject == null || componentType == null)
		{
			return null;
		}
		MethodInfo[] methods = typeof(GameObject).GetMethods(BindingFlags.Instance | BindingFlags.Public);
		foreach (MethodInfo methodInfo in methods)
		{
			if (methodInfo.Name == "GetComponent" && methodInfo.IsGenericMethodDefinition && methodInfo.GetGenericArguments().Length == 1 && methodInfo.GetParameters().Length == 0)
			{
				return methodInfo.MakeGenericMethod(componentType).Invoke(gameObject, null) as Component;
			}
		}
		return null;
	}

	private static GameObject ExtractGameObject(object value)
	{
		if (value is GameObject result)
		{
			return result;
		}
		if (value is Component component)
		{
			return component.gameObject;
		}
		if (value == null)
		{
			return null;
		}
		try
		{
			Type type = value.GetType();
			return (type.GetProperty("gameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? type.GetProperty("GameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))?.GetValue(value) as GameObject;
		}
		catch
		{
			return null;
		}
	}

	private bool TryAttachNativeTab(MissionLaptop laptop)
	{
		try
		{
			int laptopId = ((!(laptop == null)) ? laptop.GetInstanceID() : 0);
			Transform transform = ((laptop.ActiveOperationsTab == null) ? null : laptop.ActiveOperationsTab.transform.parent);
			GameObject sourceButton = FindNativeActiveOperationsButton(transform);
			if (sourceButton == null || sourceButton.transform.parent == null)
			{
				log.LogWarning("Cerberus native tab fix could not find the in-panel ACTIVE OPERATIONS BUTTON.");
				return false;
			}
			GameObject page = GetOrCreateCatalogPage(laptop, transform);
			if (page == null)
			{
				LogOnce("waiting for same-owner Modded Operations page under laptopId=" + laptopId + ", parent=" + HierarchyPath(transform) + ".", warning: false);
				return false;
			}
			Transform parent = sourceButton.transform.parent;
			GameObject gameObject = FindDeep(parent, "MODDED_OPS_TAB_BUTTON");
			if (gameObject != null)
			{
				UnityEngine.Object.Destroy(gameObject);
			}
			GameObject gameObject2 = FindDeep(parent, "MODDED_OPS_TAB");
			if (gameObject2 != null)
			{
				UnityEngine.Object.Destroy(gameObject2);
			}
			GameObject simulationButton = FindNativeSimulationOperationsButton(transform);
			GameObject duplicate = FindDeep(parent, "MODDED_OPS_NATIVE_TAB");
			if (duplicate != null)
			{
				if (!BuildNativeModdedPresentation(laptop, page, sourceButton))
				{
					return false;
				}
				SetButtonText(duplicate, "MODDED OPERATIONS");
				RegisterNativePresentationIsolation(laptop, page, duplicate, sourceButton, simulationButton);
				log.LogInfo("Cerberus native tab already present; rebuilt or retained its native content.");
				return true;
			}
			RectTransform component = page.GetComponent<RectTransform>();
			if (component != null)
			{
				CopyContentRect(laptop.ActiveOperationsTab, component);
				int value = ((laptop.ActiveOperationsTab == null) ? (parent.childCount - 1) : laptop.ActiveOperationsTab.transform.GetSiblingIndex());
				page.transform.SetSiblingIndex(Mathf.Clamp(value, 0, page.transform.parent.childCount - 1));
			}
			if (!BuildNativeModdedPresentation(laptop, page, sourceButton))
			{
				return false;
			}
			duplicate = UnityEngine.Object.Instantiate(sourceButton, parent);
			duplicate.name = "MODDED_OPS_NATIVE_TAB";
			duplicate.SetActive(value: true);
			SetButtonText(duplicate, "MODDED OPERATIONS");
			PositionAsThirdTab(parent, sourceButton, simulationButton, duplicate);
			FitTabTitleText(sourceButton, "ACTIVE OPERATIONS");
			FitTabTitleText(simulationButton, "OPERATION SIMULATION");
			FitTabTitleText(duplicate, "MODDED OPERATIONS");
			page.SetActive(value: false);
			if (page.GetComponent<Animator>() == null)
			{
				page.AddComponent<Animator>();
			}
			PanelButton component2 = duplicate.GetComponent<PanelButton>();
			ButtonManager component3 = duplicate.GetComponent<ButtonManager>();
			UnityEvent unityEvent = null;
			if (component2 != null)
			{
				component2.onClick = new UnityEvent();
				unityEvent = component2.onClick;
			}
			else if (component3 != null)
			{
				component3.onClick = new UnityEvent();
				unityEvent = component3.onClick;
			}
			Button button = null;
			try
			{
				button = duplicate.GetComponent<Button>();
			}
			catch
			{
				button = null;
			}
			if (button == null)
			{
				try
				{
					button = duplicate.GetComponentInChildren<Button>(includeInactive: true);
				}
				catch
				{
					button = null;
				}
			}
			if (unityEvent == null && button == null)
			{
				log.LogWarning("Cerberus native tab clone had no DreamOS click event.");
				return false;
			}
			int lastLogicalOpenFrame = -1;
			Action<string> openModdedPage = delegate(string eventSource)
			{
				try
				{
					int frameCount = Time.frameCount;
					if (frameCount == lastLogicalOpenFrame)
					{
						log.LogInfo("Cerberus native MODDED OPS duplicate click surface suppressed: laptopId=" + laptopId + ", source=" + eventSource + ", frame=" + frameCount + ".");
					}
					else
					{
						lastLogicalOpenFrame = frameCount;
						log.LogInfo(CaptureLaptopTransitionState(laptop, page, "before MODDED OPS", eventSource, frameCount));
						SetTabSelectedState(sourceButton, selected: false);
						SetTabSelectedState(simulationButton, selected: false);
						SetButtonText(duplicate, "MODDED OPERATIONS");
						SetTabSelectedState(duplicate, selected: true);
						OpenModdedPage(laptop, page);
						MarkNativeModdedPageOpened(laptopId, frameCount);
						log.LogInfo("Cerberus native MODDED OPS logical transition: laptopId=" + laptopId + ", source=" + eventSource + ", frame=" + frameCount + ", pageActiveSelf=" + page.activeSelf + ", pageActiveInHierarchy=" + page.activeInHierarchy + ", ownerCanvas=" + DescribeActivity(laptop.osCanvas) + ", pageParent=" + HierarchyPath(page.transform.parent) + ".");
						QueueTransitionSnapshot(laptop, page, "after MODDED OPS via " + eventSource, frameCount);
					}
				}
				catch (Exception ex2)
				{
					log.LogWarning("Cerberus native MODDED OPS click failed: " + ex2.GetType().Name + ": " + ex2.Message);
				}
			};
			if (unityEvent != null)
			{
				unityEvent.RemoveAllListeners();
				unityEvent.AddListener((Action)delegate
				{
					openModdedPage("DreamOS.PanelButton");
				});
			}
			if (button != null)
			{
				button.onClick.RemoveAllListeners();
				button.onClick.AddListener((Action)delegate
				{
					openModdedPage("UnityEngine.UI.Button");
				});
				button.interactable = true;
			}
			if (component2 != null)
			{
				component2.isInteractable = true;
			}
			RegisterNativePresentationIsolation(laptop, page, duplicate, sourceButton, simulationButton);
			int num = 0;
			try
			{
				num = duplicate.GetComponentsInChildren<Graphic>(includeInactive: true).Length;
			}
			catch
			{
			}
			log.LogInfo("Cerberus native MODDED OPS tab inserted as third native Operation Selection tab; source=" + sourceButton.name + ", simulation=" + ((simulationButton == null) ? "null" : simulationButton.name) + ", visible=" + duplicate.activeInHierarchy + ", unityButton=" + (button != null) + ", targetGraphic=" + ((button == null || button.targetGraphic == null) ? "null" : button.targetGraphic.gameObject.name) + ", graphics=" + num + ", rect=" + RectSummary(duplicate) + ".");
			return true;
		}
		catch (Exception ex)
		{
			log.LogWarning("Cerberus native tab fix failed: " + ex.ToString());
			return false;
		}
	}

	private void RegisterNativePresentationIsolation(MissionLaptop laptop, GameObject page, GameObject moddedButton, GameObject activeButton, GameObject simulationButton)
	{
		if (laptop == null || page == null || moddedButton == null)
		{
			return;
		}
		int instanceID = laptop.GetInstanceID();
		foreach (NativePresentationBinding nativePresentationBinding in nativePresentationBindings)
		{
			if (nativePresentationBinding.LaptopId == instanceID)
			{
				nativePresentationBinding.Laptop = laptop;
				nativePresentationBinding.Page = page;
				nativePresentationBinding.PreparationPanel = FindNativeModdedPreparationPanel(laptop);
				nativePresentationBinding.ModdedButton = moddedButton;
				nativePresentationBinding.ActiveButton = activeButton;
				nativePresentationBinding.SimulationButton = simulationButton;
				return;
			}
		}
		nativePresentationBindings.Add(new NativePresentationBinding
		{
			LaptopId = instanceID,
			Laptop = laptop,
			Page = page,
			PreparationPanel = FindNativeModdedPreparationPanel(laptop),
			ModdedButton = moddedButton,
			ActiveButton = activeButton,
			SimulationButton = simulationButton
		});
	}

	private void MarkNativeModdedPageOpened(int laptopId, int frame)
	{
		foreach (NativePresentationBinding nativePresentationBinding in nativePresentationBindings)
		{
			if (nativePresentationBinding.LaptopId == laptopId)
			{
				nativePresentationBinding.LastModdedOpenFrame = frame;
				break;
			}
		}
	}

	private void MaintainNativePresentationIsolation()
	{
		for (int num = nativePresentationBindings.Count - 1; num >= 0; num--)
		{
			NativePresentationBinding nativePresentationBinding = nativePresentationBindings[num];
			if (nativePresentationBinding == null || nativePresentationBinding.Laptop == null || nativePresentationBinding.Page == null || nativePresentationBinding.ModdedButton == null)
			{
				nativePresentationBindings.RemoveAt(num);
			}
			else
			{
				bool flag = (nativePresentationBinding.Laptop.ActiveOperationsTab != null && nativePresentationBinding.Laptop.ActiveOperationsTab.activeSelf) || (nativePresentationBinding.Laptop.SimulationOperationsTab != null && nativePresentationBinding.Laptop.SimulationOperationsTab.activeSelf);
				bool flag2 = flag || IsNativePanelButtonSelected(nativePresentationBinding.ActiveButton) || IsNativePanelButtonSelected(nativePresentationBinding.SimulationButton);
				if (nativePresentationBinding.Page.activeSelf && flag2 && Time.frameCount > nativePresentationBinding.LastModdedOpenFrame + 1)
				{
					nativePresentationBinding.Page.SetActive(value: false);
					if (nativePresentationBinding.PreparationPanel != null)
					{
						nativePresentationBinding.PreparationPanel.SetActive(value: false);
					}
					SetTabSelectedState(nativePresentationBinding.ModdedButton, selected: false);
					log.LogInfo("Cerberus isolated modded overlay closed after an official tab became selected; laptopId=" + nativePresentationBinding.LaptopId + ", officialPageActive=" + flag + ".");
				}
			}
		}
	}

	private static bool IsNativePanelButtonSelected(GameObject button)
	{
		if (button == null)
		{
			return false;
		}
		try
		{
			PanelButton component = button.GetComponent<PanelButton>();
			return component != null && component.isSelected;
		}
		catch
		{
			return false;
		}
	}

	private static GameObject FindNativeModdedPreparationPanel(MissionLaptop laptop)
	{
		if (laptop == null || laptop.opBoardParent == null)
		{
			return null;
		}
		Transform parent = laptop.opBoardParent.parent;
		return FindDeep(((parent == null) ? null : parent.parent) ?? parent ?? laptop.opBoardParent, "MODDED_NATIVE_OPERATION_PREPARATION");
	}

	private static GameObject GetOrCreateCatalogPage(MissionLaptop laptop, Transform operationSelection)
	{
		if (laptop == null || operationSelection == null || laptop.ActiveOperationsTab == null)
		{
			return null;
		}
		GameObject gameObject = FindChild(operationSelection, "MODDED_OPERATIONS_PAGE");
		if (gameObject != null)
		{
			return gameObject;
		}
		GameObject gameObject2 = new GameObject("MODDED_OPERATIONS_PAGE");
		RectTransform destination = gameObject2.AddComponent<RectTransform>();
		gameObject2.transform.SetParent(operationSelection, worldPositionStays: false);
		CopyContentRect(laptop.ActiveOperationsTab, destination);
		gameObject2.transform.SetSiblingIndex(laptop.ActiveOperationsTab.transform.GetSiblingIndex());
		gameObject2.SetActive(value: false);
		return gameObject2;
	}

	private bool BuildNativeModdedPresentation(MissionLaptop laptop, GameObject page, GameObject nativeButtonTemplate)
	{
		if (laptop == null || page == null || laptop.ActiveOperationsTab == null)
		{
			return false;
		}
		int instanceID = laptop.GetInstanceID();
		bool flag = OperatorApi.ModdedOperations.Operations.Count == 0;
		if (catalogPresentations.TryGetValue(instanceID, out var value) && value != null && value.Page == page && value.HomeShell != null && (flag || value.PreparationPanel != null))
		{
			return true;
		}
		if (value != null)
		{
			if (value.HomeShell != null)
			{
				UnityEngine.Object.Destroy(value.HomeShell);
			}
			if (value.PreparationPanel != null)
			{
				UnityEngine.Object.Destroy(value.PreparationPanel);
			}
			if (value.NativeBoardData != null)
			{
				UnityEngine.Object.Destroy(value.NativeBoardData);
			}
			if (value.NativeTargetData != null)
			{
				UnityEngine.Object.Destroy(value.NativeTargetData);
			}
			if (value.NativeInfiltrationMapPrefab != null)
			{
				UnityEngine.Object.Destroy(value.NativeInfiltrationMapPrefab);
			}
		}
		catalogPresentations.Remove(instanceID);
		GameObject gameObject = FindDeep(page.transform, "MODDED_NATIVE_HOME");
		GameObject gameObject2 = FindNativeModdedPreparationPanel(laptop);
		if (gameObject != null)
		{
			UnityEngine.Object.Destroy(gameObject);
		}
		if (gameObject2 != null)
		{
			UnityEngine.Object.Destroy(gameObject2);
		}
		GameObject gameObject3 = ExtractGameObject(ReadMember(laptop, "ActiveOperationsList"));
		GameObject gameObject4 = ExtractGameObject(ReadMember(laptop, "SimulationOperationList"));
		GameObject gameObject5 = FindNativeOperationRowTemplate((gameObject3 == null) ? null : gameObject3.transform) ?? FindNativeOperationRowTemplate((gameObject4 == null) ? null : gameObject4.transform);
		if (gameObject3 == null || gameObject5 == null)
		{
			log.LogWarning("Cerberus native Modded Ops presentation skipped because no shipped operation-row visual was available.");
			return false;
		}
		int[] array = BuildRelativeChildIndexPath(laptop.ActiveOperationsTab.transform, gameObject3.transform);
		if (array == null)
		{
			log.LogWarning("Cerberus native Modded Ops presentation skipped because the active-list path was outside its page.");
			return false;
		}
		CatalogPresentation catalogPresentation = new CatalogPresentation
		{
			Laptop = laptop,
			Page = page
		};
		if (flag)
		{
			catalogPresentation.HomeShell = CreateEmptyCatalogShell(page.transform, laptop.ActiveOperationsTab, array, out var briefing);
			catalogPresentation.HomeBriefing = briefing;
			if (catalogPresentation.HomeShell == null)
			{
				return false;
			}
			catalogPresentations[instanceID] = catalogPresentation;
			deferredSetupLoggedLaptops.Remove(instanceID);
			Canvas.ForceUpdateCanvases();
			log.LogInfo("Modded Operations framework attached with an empty catalog; install one or more valid data-only map packages under BepInEx\\OperatorMods.");
			return true;
		}
		catalogPresentation.SelectedOperation = OperatorApi.ModdedOperations.Operations[0];
		catalogPresentation.SelectedTimeCode = catalogPresentation.SelectedOperation.DefaultTimeCode;
		GameObject gameObject6 = (catalogPresentation.PreparationPanel = CreateCatalogOperationBoardShell(laptop, catalogPresentation));
		GameObject gameObject7 = null;
		TMP_Text briefing2 = null;
		if (gameObject6 != null)
		{
			gameObject7 = (catalogPresentation.HomeShell = CreateCatalogOperationShell(page.transform, laptop.ActiveOperationsTab, array, gameObject5, catalogPresentation, out briefing2));
			catalogPresentation.HomeBriefing = briefing2;
		}
		if (gameObject7 == null || gameObject6 == null)
		{
			if (gameObject7 != null)
			{
				UnityEngine.Object.Destroy(gameObject7);
			}
			if (gameObject6 != null)
			{
				UnityEngine.Object.Destroy(gameObject6);
			}
			if (deferredSetupLoggedLaptops.Add(instanceID))
			{
				log.LogInfo("Modded Operations deferred one hidden laptop replica until its shipped list and operation-information surfaces are ready; laptopId=" + instanceID + ".");
			}
			return false;
		}
		Image component = page.GetComponent<Image>();
		if (component != null)
		{
			component.color = new Color(component.color.r, component.color.g, component.color.b, 0f);
			component.raycastTarget = false;
		}
		gameObject6.SetActive(value: false);
		catalogPresentations[instanceID] = catalogPresentation;
		Canvas.ForceUpdateCanvases();
		log.LogInfo("Modded Operations presentation built from shipped operation-row and OperationBoardUI visuals; laptopId=" + laptop.GetInstanceID() + ", rows=" + OperatorApi.ModdedOperations.Operations.Count + ", catalog=" + OperatorApi.ModdedOperations.CatalogId + ".");
		return true;
	}

	private static GameObject CreateEmptyCatalogShell(Transform parent, GameObject nativePageTemplate, int[] relativeListPath, out TMP_Text briefing)
	{
		briefing = null;
		if (parent == null || nativePageTemplate == null || relativeListPath == null)
		{
			return null;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(nativePageTemplate, parent);
		if (gameObject == null)
		{
			return null;
		}
		gameObject.name = "MODDED_NATIVE_HOME";
		SetFullStretch(gameObject.GetComponent<RectTransform>());
		gameObject.SetActive(value: true);
		RewriteClonedPageHeading(gameObject);
		Transform transform = FollowRelativeChildIndexPath(gameObject.transform, relativeListPath);
		if (transform == null)
		{
			UnityEngine.Object.Destroy(gameObject);
			return null;
		}
		SetChildrenActive(transform, active: false);
		briefing = FindNativeBriefingText(gameObject);
		if (briefing != null)
		{
			DisableLocalizationComponent(briefing.gameObject);
			if (briefing.transform.parent != null)
			{
				DisableLocalizationComponent(briefing.transform.parent.gameObject);
			}
			briefing.text = "NO MODDED OPERATIONS INSTALLED\n\nInstall a valid map package in BepInEx\\OperatorMods, then restart OPERATOR.";
			briefing.enableWordWrapping = true;
		}
		foreach (OperationSelectionUI componentsInChild in gameObject.GetComponentsInChildren<OperationSelectionUI>(includeInactive: true))
		{
			if (!(componentsInChild == null))
			{
				componentsInChild.enabled = false;
				UnityEngine.Object.Destroy(componentsInChild);
			}
		}
		return gameObject;
	}

	private GameObject CreateCatalogOperationShell(Transform parent, GameObject nativePageTemplate, int[] relativeListPath, GameObject rowTemplate, CatalogPresentation presentation, out TMP_Text briefing)
	{
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Invalid comparison between Unknown and I4
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Invalid comparison between Unknown and I4
		briefing = null;
		if (parent == null || nativePageTemplate == null || rowTemplate == null || presentation == null)
		{
			return null;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(nativePageTemplate, parent);
		if (gameObject == null)
		{
			return null;
		}
		gameObject.name = "MODDED_NATIVE_HOME";
		SetFullStretch(gameObject.GetComponent<RectTransform>());
		gameObject.SetActive(value: true);
		RewriteClonedPageHeading(gameObject);
		Transform transform = FollowRelativeChildIndexPath(gameObject.transform, relativeListPath);
		if (transform == null)
		{
			UnityEngine.Object.Destroy(gameObject);
			return null;
		}
		SetChildrenActive(transform, active: false);
		System.Collections.Generic.List<GameObject> list = new System.Collections.Generic.List<GameObject>();
		int num = 0;
		foreach (ModdedOperationDefinition operation in OperatorApi.ModdedOperations.Operations)
		{
			GameObject gameObject2 = UnityEngine.Object.Instantiate(rowTemplate, transform);
			if (!(gameObject2 == null))
			{
				gameObject2.name = "MODDED_NATIVE_ROW_" + num.ToString("D3");
				gameObject2.SetActive(value: true);
				string mode = (((int)operation.Mode == 2) ? "PVE" : "PVP");
				string threat = (((int)operation.Mode == 2) ? "HIGH" : "VARIABLE");
				RewriteNativeOperationRow(gameObject2, operation.DisplayName, "30-45 MIN", threat, mode, operation.AreaOfOperation);
				list.Add(gameObject2);
				ModdedOperationDefinition captured = operation;
				RebindNativeRow(gameObject2, delegate
				{
					SelectCatalogOperation(presentation, captured, openPreparation: false);
				}, delegate
				{
					SelectCatalogOperation(presentation, captured, openPreparation: true);
				});
				num++;
			}
		}
		if (list.Count == 0)
		{
			UnityEngine.Object.Destroy(gameObject);
			return null;
		}
		if (transform.GetComponent<VerticalLayoutGroup>() == null)
		{
			StackNativeRows(list);
		}
		briefing = FindNativeBriefingText(gameObject, list.ToArray());
		if (briefing != null)
		{
			DisableLocalizationComponent(briefing.gameObject);
			if (briefing.transform.parent != null)
			{
				DisableLocalizationComponent(briefing.transform.parent.gameObject);
			}
			briefing.text = FormatCatalogBriefing(presentation.SelectedOperation);
			briefing.enableWordWrapping = true;
		}
		foreach (OperationSelectionUI componentsInChild in gameObject.GetComponentsInChildren<OperationSelectionUI>(includeInactive: true))
		{
			if (!(componentsInChild == null))
			{
				componentsInChild.enabled = false;
				UnityEngine.Object.Destroy(componentsInChild);
			}
		}
		return gameObject;
	}

	private void SelectCatalogOperation(CatalogPresentation presentation, ModdedOperationDefinition operation, bool openPreparation)
	{
		if (presentation != null && operation != null)
		{
			presentation.SelectedOperation = operation;
			presentation.SelectedTimeCode = operation.DefaultTimeCode;
			if (presentation.HomeBriefing != null)
			{
				presentation.HomeBriefing.text = FormatCatalogBriefing(operation);
			}
			UpdateCatalogOperationBoard(presentation);
			BeginSelectedMapPrefetch(operation);
			log.LogInfo("Modded Operations row " + (openPreparation ? "double-click" : "single-click") + ": operation=" + operation.Id + ".");
			if (openPreparation)
			{
				OpenNativeOperationPreparation(presentation.Laptop, presentation.PreparationPanel, null);
			}
		}
	}

	private void BeginSelectedMapPrefetch(ModdedOperationDefinition operation)
	{
		ModdedMapDefinition val = default(ModdedMapDefinition);
		if (operation == null || !OperatorApi.ModdedOperations.TryGetMap(operation.MapId, ref val) || val == null || (loadedMapBundles.TryGetValue(val.Id, out var value) && value != null && value.SceneBundle != null && value.Map != null && string.Equals(value.Map.PackageContentId, val.PackageContentId, StringComparison.Ordinal)))
		{
			return;
		}
		if (value != null)
		{
			try
			{
				value.SceneBundle?.Unload(unloadAllLoadedObjects: false);
			}
			catch
			{
			}
			foreach (AssetBundle dependency in value.Dependencies)
			{
				try
				{
					dependency?.Unload(unloadAllLoadedObjects: false);
				}
				catch
				{
				}
			}
			loadedMapBundles.Remove(val.Id);
			log.LogWarning("Modded Operations discarded an incomplete or stale bundle cache entry before selected-map prefetch: map=" + val.Id + ".");
		}
		if (pendingLaunch != null)
		{
			ModdedMapDefinition map = pendingLaunch.Map;
			if (string.Equals((map != null) ? map.Id : null, val.Id, StringComparison.Ordinal))
			{
				ModdedMapDefinition map2 = pendingLaunch.Map;
				if (string.Equals((map2 != null) ? map2.PackageContentId : null, val.PackageContentId, StringComparison.Ordinal))
				{
					return;
				}
			}
			ManualLogSource manualLogSource = log;
			string[] obj3 = new string[5] { "Modded Operations deferred selected-map prefetch for ", val.Id, " while ", null, null };
			ModdedMapDefinition map3 = pendingLaunch.Map;
			obj3[3] = ((map3 != null) ? map3.Id : null);
			obj3[4] = " is loading.";
			manualLogSource.LogInfo(string.Concat(obj3));
			return;
		}
		LoadedMapBundles loadingBundles = new LoadedMapBundles
		{
			Map = val
		};
		PendingMapLaunch pendingMapLaunch = new PendingMapLaunch
		{
			Map = val,
			LoadingBundles = loadingBundles,
			LoadStartedTimestamp = Stopwatch.GetTimestamp()
		};
		foreach (string dependencyBundlePath in val.DependencyBundlePaths)
		{
			pendingMapLaunch.BundlePaths.Add(dependencyBundlePath);
		}
		pendingMapLaunch.BundlePaths.Add(val.SceneBundlePath);
		pendingLaunch = pendingMapLaunch;
		log.LogInfo("Modded Operations began selected-map bundle prefetch: map=" + val.Id + ", bundles=" + pendingMapLaunch.BundlePaths.Count + ", bytes=" + GetBundleByteTotal(pendingMapLaunch.BundlePaths) + ".");
	}

	private static string FormatCatalogBriefing(ModdedOperationDefinition operation)
	{
		if (operation == null)
		{
			return string.Empty;
		}
		return operation.DisplayName + "\n\nAREA OF OPERATION // " + operation.AreaOfOperation + "\n\n" + operation.Sitrep;
	}

	private GameObject CreateCatalogOperationBoardShell(MissionLaptop laptop, CatalogPresentation presentation)
	{
		GameObject gameObject = ResolveNativeOperationBoardVisualTemplate();
		if (laptop == null || laptop.opBoardParent == null || presentation == null || gameObject == null)
		{
			return null;
		}
		Transform opBoardParent = laptop.opBoardParent;
		Transform parent = opBoardParent.parent;
		Transform transform = ((parent == null) ? null : parent.parent);
		if (parent == null || transform == null)
		{
			return null;
		}
		int[] array = BuildRelativeChildIndexPath(parent, opBoardParent);
		if (array == null)
		{
			return null;
		}
		GameObject gameObject2 = UnityEngine.Object.Instantiate(parent.gameObject, transform);
		if (gameObject2 == null)
		{
			return null;
		}
		gameObject2.name = "MODDED_NATIVE_OPERATION_PREPARATION";
		gameObject2.SetActive(value: true);
		Transform transform2 = FollowRelativeChildIndexPath(gameObject2.transform, array);
		if (transform2 == null)
		{
			UnityEngine.Object.Destroy(gameObject2);
			return null;
		}
		SetChildrenActive(transform2, active: false);
		GameObject gameObject3 = UnityEngine.Object.Instantiate(gameObject, transform2);
		if (gameObject3 == null)
		{
			UnityEngine.Object.Destroy(gameObject2);
			return null;
		}
		gameObject3.name = "MODDED_NATIVE_OPERATION_INFORMATION";
		SetFullStretch(gameObject3.GetComponent<RectTransform>());
		gameObject3.SetActive(value: false);
		OperationBoardUI board = gameObject3.GetComponent<OperationBoardUI>() ?? gameObject3.GetComponentInChildren<OperationBoardUI>(includeInactive: true);
		if (board == null || board.SituationReportText == null || board.SituationReportText.TMP == null || board.ExecuteOperationButton == null || board.ConfirmationWindow == null || board.InfilTimeSlider == null || board.PrearationTimeSlider == null)
		{
			UnityEngine.Object.Destroy(gameObject2);
			return null;
		}
		CerebusOpboard cerebusOpboard = null;
		try
		{
			cerebusOpboard = ScriptableObject.CreateInstance<CerebusOpboard>();
			cerebusOpboard.name = "MODDED_OPERATIONS_PRIVATE_OPBOARD_DATA";
			CerebusTargetPackage cerebusTargetPackage = ScriptableObject.CreateInstance<CerebusTargetPackage>();
			if (cerebusOpboard == null || cerebusTargetPackage == null)
			{
				throw new InvalidOperationException("package-owned operation data could not be allocated");
			}
			cerebusTargetPackage.name = "MODDED_OPERATIONS_PRIVATE_TARGET_PACKAGE";
			cerebusTargetPackage.TargetPackage = cerebusOpboard;
			cerebusTargetPackage.TargetPackageIndex = 0;
			cerebusTargetPackage.isLocked = false;
			cerebusTargetPackage.isSimulation = false;
			cerebusTargetPackage.isRegion = false;
			cerebusTargetPackage.MissionRequiredForUnlock = null;
			cerebusTargetPackage.unlockCode = string.Empty;
			cerebusTargetPackage.progressionUnlockCode = string.Empty;
			cerebusTargetPackage.OperationType = new LocalizedString();
			cerebusTargetPackage.EnemyCount = new LocalizedString();
			cerebusTargetPackage.OperationDescription = new LocalizedString();
			cerebusOpboard.ThisMissionTargetPackage = cerebusTargetPackage;
			presentation.NativeTargetData = cerebusTargetPackage;
			cerebusOpboard.MapPrefab = null;
			cerebusOpboard.SituationReport = new LocalizedString();
			cerebusOpboard.requireEnoughExfils = false;
			cerebusOpboard.requireEnoughInfils = false;
			cerebusOpboard.isLocked = false;
			cerebusOpboard.unlockCode = string.Empty;
			cerebusOpboard.exfilUnlockingKey = string.Empty;
			cerebusOpboard.TargetRaidTime = 0f;
			cerebusOpboard.AchievementName = string.Empty;
			cerebusOpboard.achievement_min_ai_amount = 0;
			cerebusOpboard.INFILTRATION_TARGET = string.Empty;
			cerebusOpboard.INFILTRATION_TIME = string.Empty;
			cerebusOpboard.SelectedInfiltrationTime = 0;
			cerebusOpboard.PrepTimeInSeconds = 5;
			cerebusOpboard.isCompleted = false;
			cerebusOpboard.UnlockOnExfil = string.Empty;
			cerebusOpboard.HVTSpawnIsRandom = false;
			cerebusOpboard.isSimulation = false;
			cerebusOpboard.missionLaptop = laptop;
			cerebusOpboard.OperationBoardUI = board;
			board.CerebusOpboard = cerebusOpboard;
			board.Laptop = laptop;
			deferredSetupLoggedLaptops.Remove(laptop.GetInstanceID());
		}
		catch (Exception ex)
		{
			int instanceID = laptop.GetInstanceID();
			if (deferredSetupLoggedLaptops.Add(instanceID))
			{
				log.LogInfo("Modded Operations deferred one hidden laptop replica until its shipped OperationBoardUI ownership graph is ready; laptopId=" + instanceID + ", reason=" + ex.GetType().Name + ".");
			}
			if (cerebusOpboard != null)
			{
				UnityEngine.Object.Destroy(cerebusOpboard);
			}
			if (presentation.NativeTargetData != null)
			{
				UnityEngine.Object.Destroy(presentation.NativeTargetData);
			}
			presentation.NativeTargetData = null;
			if (presentation.NativeInfiltrationMapPrefab != null)
			{
				UnityEngine.Object.Destroy(presentation.NativeInfiltrationMapPrefab);
			}
			presentation.NativeInfiltrationMapPrefab = null;
			UnityEngine.Object.Destroy(gameObject2);
			return null;
		}
		presentation.Board = board;
		presentation.NativeBoardData = cerebusOpboard;
		presentation.SituationReport = board.SituationReportText.TMP;
		presentation.SituationReport.gameObject.name = "MODDED_NATIVE_SITREP";
		DisableLocalizationComponent(board.SituationReportText.gameObject);
		DisableLocalizationComponent(presentation.SituationReport.gameObject);
		if (presentation.SituationReport.transform.parent != null)
		{
			DisableLocalizationComponent(presentation.SituationReport.transform.parent.gameObject);
		}
		presentation.SituationReport.enableWordWrapping = true;
		SetActiveSafe(board.SimulationButton, active: false);
		SetActiveSafe(board.ActiveOperationButton, active: false);
		SetActiveSafe(board.NotEnoughInfilsButton, active: false);
		SetActiveSafe(board.NotEnoughExfilsButton, active: false);
		SetNativeMapFullscreen(board, fullscreen: false);
		SetActiveSafe(board.AbortOperationButton, active: false);
		SetActiveSafe(board.ExecuteOperationButton, active: true);
		SetActiveSafe(board.InfiltrationTargetParent, active: false);
		SetComponentActiveSafe(board.InfilTargetSlider, active: false);
		SetComponentActiveSafe(board.InfilTimeSlider, active: true);
		SetComponentActiveSafe(board.PrearationTimeSlider, active: true);
		SetComponentActiveSafe(board.OpforCountSlider, active: false);
		SetComponentActiveSafe(board.EnemyCountSlider, active: false);
		SetComponentActiveSafe(board.OpforDifficultySlider, active: false);
		SetComponentActiveSafe(board.HVT_OpforCountSlider, active: false);
		SetComponentActiveSafe(board.HVTEnemyCountSlider, active: false);
		SetComponentActiveSafe(board.HVTOpforDifficultySlider, active: false);
		SetComponentActiveSafe(board.HVTCountSlider, active: false);
		SetComponentActiveSafe(board.HVTDifficultySlider, active: false);
		SetGameObjectsActive(board.SimulationParameters, active: false);
		SetGameObjectsActive(board.HVTSimulationParameters, active: false);
		SetGameObjectsActive(board.PVPParameters, active: false);
		SetGameObjectsActive(board.FFAParameters, active: false);
		ConfigureNativeSelector(board.PrearationTimeSlider, new string[1] { "5 SECONDS" }, null);
		ModalWindowManager confirmation = board.ConfirmationWindow;
		confirmation.useLocalization = false;
		confirmation.useCustomContent = true;
		confirmation.titleText = "Start Operation";
		confirmation.descriptionText = "Are you sure you want to start this operation?";
		confirmation.showConfirmButton = true;
		confirmation.showCancelButton = true;
		confirmation.closeOnConfirm = true;
		confirmation.closeOnCancel = true;
		confirmation.onOpen = new UnityEvent();
		confirmation.onClose = new UnityEvent();
		confirmation.onConfirm = new UnityEvent();
		confirmation.onConfirm.AddListener((Action)delegate
		{
			BeginCatalogOperationLaunch(presentation);
		});
		confirmation.onCancel = new UnityEvent();
		confirmation.onCancel.AddListener((Action)delegate
		{
			CloseNativeMapConfirmation(board, logClose: true);
		});
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
		try
		{
			confirmation.UpdateUI();
		}
		catch
		{
		}
		if (!ReplaceNativeButtonAction(confirmation.confirmButton, delegate
		{
			BeginCatalogOperationLaunch(presentation);
		}) || !ReplaceNativeButtonAction(confirmation.cancelButton, delegate
		{
			confirmation.onCancel.Invoke();
		}) || !ReplaceNativeButtonAction(board.ExecuteOperationButton, delegate
		{
			confirmation.OpenWindow();
		}))
		{
			UnityEngine.Object.Destroy(cerebusOpboard);
			UnityEngine.Object.Destroy(presentation.NativeTargetData);
			UnityEngine.Object.Destroy(presentation.NativeInfiltrationMapPrefab);
			presentation.NativeBoardData = null;
			presentation.NativeTargetData = null;
			presentation.NativeInfiltrationMapPrefab = null;
			UnityEngine.Object.Destroy(gameObject2);
			return null;
		}
		RebindNativeFullscreenControls(board, gameObject2);
		BindNativePreparationBack(laptop, gameObject2, null, warnIfMissing: false);
		UpdateCatalogOperationBoard(presentation);
		gameObject3.SetActive(value: true);
		gameObject2.SetActive(value: false);
		return gameObject2;
	}

	private GameObject ResolveNativeOperationBoardVisualTemplate()
	{
		if (operationBoardVisualTemplate != null)
		{
			return operationBoardVisualTemplate;
		}
		if (OperationsManager.singleton != null && OperationsManager.singleton.OperationBoardUIPrefab != null)
		{
			operationBoardVisualTemplate = OperationsManager.singleton.OperationBoardUIPrefab;
			return operationBoardVisualTemplate;
		}
		foreach (OperationsManager item in Resources.FindObjectsOfTypeAll<OperationsManager>())
		{
			if (!(item == null) && !(item.OperationBoardUIPrefab == null))
			{
				operationBoardVisualTemplate = item.OperationBoardUIPrefab;
				return operationBoardVisualTemplate;
			}
		}
		foreach (OperationBoardUI item2 in Resources.FindObjectsOfTypeAll<OperationBoardUI>())
		{
			if (!(item2 == null) && !(item2.gameObject == null) && !item2.gameObject.name.StartsWith("MODDED_NATIVE_", StringComparison.Ordinal) && !(item2.SituationReportText == null) && !(item2.SituationReportText.TMP == null) && !(item2.ExecuteOperationButton == null) && !(item2.ConfirmationWindow == null) && !(item2.InfilTimeSlider == null) && !(item2.PrearationTimeSlider == null))
			{
				operationBoardVisualTemplate = item2.gameObject;
				return operationBoardVisualTemplate;
			}
		}
		return null;
	}

	private void UpdateCatalogOperationBoard(CatalogPresentation presentation)
	{
		//IL_030a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0310: Invalid comparison between Unknown and I4
		//IL_041c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0422: Invalid comparison between Unknown and I4
		//IL_0439: Unknown result type (might be due to invalid IL or missing references)
		//IL_043f: Invalid comparison between Unknown and I4
		if (presentation == null || presentation.SelectedOperation == null || presentation.Board == null || presentation.NativeBoardData == null)
		{
			return;
		}
		ModdedOperationDefinition operation = presentation.SelectedOperation;
		ModdedMapDefinition val = default(ModdedMapDefinition);
		if (!OperatorApi.ModdedOperations.TryGetMap(operation.MapId, ref val) || val == null)
		{
			return;
		}
		if (!operation.SupportedTimeCodes.Contains<string>(presentation.SelectedTimeCode, StringComparer.Ordinal))
		{
			presentation.SelectedTimeCode = operation.DefaultTimeCode;
		}
		if (presentation.SituationReport != null)
		{
			presentation.SituationReport.text = FormatCatalogBriefing(operation);
		}
		Sprite orLoadPreviewSprite = GetOrLoadPreviewSprite(val);
		if (orLoadPreviewSprite != null)
		{
			ReplaceNativeMapPreview(presentation.Board.MapParent, orLoadPreviewSprite, "MODDED_NATIVE_MAP_PREVIEW");
			ReplaceNativeMapPreview(presentation.Board.FullscreenMapParent, orLoadPreviewSprite, "MODDED_NATIVE_MAP_FULLSCREEN");
		}
		else
		{
			SetGameObjectsActive(CaptureDirectChildren(presentation.Board.MapParent), active: false);
			SetGameObjectsActive(CaptureDirectChildren(presentation.Board.FullscreenMapParent), active: false);
		}
		Il2CppStringArray il2CppStringArray = new Il2CppStringArray(operation.Infiltrations.Count);
		Il2CppReferenceArray<TARGETPACKAGE_DETAILS> il2CppReferenceArray = new Il2CppReferenceArray<TARGETPACKAGE_DETAILS>(operation.SupportedTimeCodes.Count);
		int num = 0;
		for (int i = 0; i < operation.SupportedTimeCodes.Count; i++)
		{
			string text = operation.SupportedTimeCodes[i];
			TARGETPACKAGE_DETAILS value = new TARGETPACKAGE_DETAILS
			{
				OPERATION_SCENE = val.ScenePath,
				DISPLAY_NAME = operation.DisplayName,
				INFILTRATION_TIME = text
			};
			il2CppReferenceArray[i] = value;
			if (string.Equals(text, presentation.SelectedTimeCode, StringComparison.Ordinal))
			{
				num = i;
			}
		}
		for (int j = 0; j < operation.Infiltrations.Count; j++)
		{
			il2CppStringArray[j] = operation.Infiltrations[j].DisplayName;
		}
		if (presentation.NativeInfiltrationMapPrefab != null)
		{
			UnityEngine.Object.Destroy(presentation.NativeInfiltrationMapPrefab);
		}
		presentation.NativeInfiltrationMapPrefab = BuildPackageInfiltrationMapPrefab(presentation, operation, orLoadPreviewSprite);
		if (presentation.NativeInfiltrationMapPrefab == null)
		{
			log.LogError("Modded Operations refused to arm operation '" + operation.Id + "' because no sanitized native infiltration selector map could be built.");
			return;
		}
		CerebusOpboard data = presentation.NativeBoardData;
		data.requireEnoughExfils = false;
		data.requireEnoughInfils = false;
		data.AffectGamemode = true;
		data.GameModeOverride = (((int)operation.Mode != 2) ? OperationsManager.GameMode.StandardPVP : OperationsManager.GameMode.PVE_HVTKILL);
		if (presentation.NativeTargetData != null)
		{
			presentation.NativeTargetData.TargetPackage = data;
			presentation.NativeTargetData.OPERATION_AREA_OF_OPERATION = operation.AreaOfOperation;
			presentation.NativeTargetData.isRegion = false;
			presentation.NativeTargetData.isLocked = false;
			presentation.NativeTargetData.MissionRequiredForUnlock = null;
			presentation.NativeTargetData.unlockCode = string.Empty;
			presentation.NativeTargetData.progressionUnlockCode = string.Empty;
			presentation.NativeTargetData.TargetPackageIndex = 0;
			presentation.NativeTargetData.isSimulation = false;
			data.ThisMissionTargetPackage = presentation.NativeTargetData;
		}
		data.TARGETPACKAGE = il2CppReferenceArray;
		data.AvailableInfils = il2CppStringArray;
		data.MinAI = (((int)operation.Mode == 2) ? 8 : 0);
		data.MaxAI = (((int)operation.Mode == 2) ? 16 : 0);
		data.MapPrefab = presentation.NativeInfiltrationMapPrefab;
		data.isLocked = false;
		data.unlockCode = string.Empty;
		data.exfilUnlockingKey = string.Empty;
		data.TargetRaidTime = 0f;
		data.AchievementName = string.Empty;
		data.achievement_min_ai_amount = 0;
		data.INFILTRATION_TARGET = operation.AreaOfOperation;
		data.INFILTRATION_TIME = presentation.SelectedTimeCode;
		data.SelectedInfiltrationTime = num;
		data.PrepTimeInSeconds = 5;
		data.isCompleted = false;
		data.UnlockOnExfil = string.Empty;
		data.HVTSpawnIsRandom = false;
		data.isSimulation = false;
		data.missionLaptop = presentation.Laptop;
		data.OperationBoardUI = presentation.Board;
		ModalWindowManager confirmation = presentation.Board.ConfirmationWindow;
		if (confirmation != null)
		{
			confirmation.useLocalization = false;
			confirmation.useCustomContent = true;
			confirmation.titleText = "Start Operation";
			confirmation.descriptionText = "Start " + operation.DisplayName + " at " + presentation.SelectedTimeCode + "?";
			if (confirmation.windowTitle != null)
			{
				confirmation.windowTitle.text = confirmation.titleText;
			}
			if (confirmation.windowDescription != null)
			{
				confirmation.windowDescription.text = confirmation.descriptionText;
			}
			try
			{
				confirmation.UpdateUI();
			}
			catch
			{
			}
		}
		ConfigureNativeSelector(presentation.Board.InfilTimeSlider, operation.SupportedTimeCodes.ToArray(), delegate(int index)
		{
			if (index >= 0 && index < operation.SupportedTimeCodes.Count)
			{
				presentation.SelectedTimeCode = operation.SupportedTimeCodes[index];
				data.SelectedInfiltrationTime = index;
				data.INFILTRATION_TIME = presentation.SelectedTimeCode;
				if (confirmation != null)
				{
					confirmation.descriptionText = "Start " + operation.DisplayName + " at " + presentation.SelectedTimeCode + "?";
					if (confirmation.windowDescription != null)
					{
						confirmation.windowDescription.text = confirmation.descriptionText;
					}
					try
					{
						confirmation.UpdateUI();
					}
					catch
					{
					}
				}
			}
		}, num);
	}

	private Sprite GetOrLoadPreviewSprite(ModdedMapDefinition map)
	{
		if (map == null)
		{
			return null;
		}
		if (previewSprites.TryGetValue(map.Id, out var value) && value != null)
		{
			return value;
		}
		try
		{
			byte[] array = File.ReadAllBytes(map.PreviewImagePath);
			Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: true)
			{
				name = "MODDED_OPERATIONS_PREVIEW_" + map.Id
			};
			if (!texture2D.LoadImage(array, markNonReadable: false))
			{
				UnityEngine.Object.Destroy(texture2D);
				return null;
			}
			texture2D.wrapMode = TextureWrapMode.Clamp;
			Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f);
			sprite.name = "MODDED_OPERATIONS_PREVIEW_" + map.Id;
			previewTextures[map.Id] = texture2D;
			previewSprites[map.Id] = sprite;
			return sprite;
		}
		catch (Exception ex)
		{
			log.LogWarning("Modded Operations preview load failed for " + map.Id + ": " + ex.GetType().Name + ": " + ex.Message);
			return null;
		}
	}

	private GameObject BuildPackageInfiltrationMapPrefab(CatalogPresentation presentation, ModdedOperationDefinition operation, Sprite preview)
	{
		if (presentation == null || presentation.Board == null || operation == null || preview == null || operation.Infiltrations == null || operation.Infiltrations.Count == 0)
		{
			return null;
		}
		MapInfilMarker mapInfilMarker = ResolveNativeInfiltrationMarkerTemplate();
		if (mapInfilMarker == null || mapInfilMarker.gameObject == null)
		{
			return null;
		}
		GameObject gameObject = null;
		try
		{
			gameObject = new GameObject("MODDED_OPERATIONS_PACKAGE_INFILTRATION_MAP");
			SetFullStretch(gameObject.AddComponent<RectTransform>());
			GameObject gameObject2 = new GameObject("PACKAGE_MAP_PREVIEW");
			gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
			SetFullStretch(gameObject2.AddComponent<RectTransform>());
			Image image = gameObject2.AddComponent<Image>();
			image.sprite = preview;
			image.color = Color.white;
			image.preserveAspect = true;
			image.raycastTarget = false;
			for (int i = 0; i < operation.Infiltrations.Count; i++)
			{
				ModdedInfiltrationDefinition val = operation.Infiltrations[i];
				GameObject gameObject3 = UnityEngine.Object.Instantiate(mapInfilMarker.gameObject, gameObject.transform);
				if (gameObject3 == null)
				{
					throw new InvalidOperationException("the shipped infiltration marker visual could not be duplicated");
				}
				gameObject3.name = "PACKAGE_INFIL_" + val.Id;
				MapInfilMarker mapInfilMarker2 = gameObject3.GetComponent<MapInfilMarker>() ?? gameObject3.GetComponentInChildren<MapInfilMarker>(includeInactive: true);
				if (mapInfilMarker2 == null)
				{
					throw new InvalidOperationException("the duplicated infiltration marker lost its native component");
				}
				mapInfilMarker2.MaxPlayers = val.MaximumPlayers;
				mapInfilMarker2.InfilName = val.DisplayName;
				mapInfilMarker2.IsGroundInfil = true;
				mapInfilMarker2.IsHeliInfil = false;
				mapInfilMarker2.IsExfil = false;
				mapInfilMarker2.OpboardUI = presentation.Board;
				mapInfilMarker2.MarkerIndex = i;
				mapInfilMarker2.individualSelectMode = false;
				mapInfilMarker2.CurrentNumPlayers = 0;
				mapInfilMarker2.isSelectedLocal = false;
				RectTransform obj = gameObject3.GetComponent<RectTransform>() ?? mapInfilMarker2.GetComponent<RectTransform>();
				if (obj == null)
				{
					throw new InvalidOperationException("the shipped infiltration marker visual has no RectTransform");
				}
				Vector2 anchorMax = (obj.anchorMin = new Vector2(val.MapPositionX, val.MapPositionY));
				obj.anchorMax = anchorMax;
				obj.anchoredPosition = Vector2.zero;
				obj.localScale = Vector3.one;
				gameObject3.SetActive(value: true);
			}
			gameObject.SetActive(value: false);
			log.LogInfo("Modded Operations built a package-owned native infiltration selector map: operation=" + operation.Id + ", markers=" + operation.Infiltrations.Count + ", visualSource=" + mapInfilMarker.gameObject.name + ".");
			return gameObject;
		}
		catch (Exception ex)
		{
			if (gameObject != null)
			{
				UnityEngine.Object.Destroy(gameObject);
			}
			log.LogError("Modded Operations could not build the package-owned infiltration selector map: " + ex.GetType().Name + ": " + ex.Message);
			return null;
		}
	}

	private static MapInfilMarker ResolveNativeInfiltrationMarkerTemplate()
	{
		try
		{
			foreach (MapInfilMarker item in Resources.FindObjectsOfTypeAll<MapInfilMarker>())
			{
				if (IsUsableNativeInfiltrationMarkerTemplate(item))
				{
					return item;
				}
			}
			foreach (CerebusOpboard item2 in Resources.FindObjectsOfTypeAll<CerebusOpboard>())
			{
				if (item2 == null || item2.MapPrefab == null || item2.MapPrefab.name.StartsWith("MODDED_OPERATIONS_", StringComparison.Ordinal))
				{
					continue;
				}
				foreach (MapInfilMarker componentsInChild in item2.MapPrefab.GetComponentsInChildren<MapInfilMarker>(includeInactive: true))
				{
					if (IsUsableNativeInfiltrationMarkerTemplate(componentsInChild))
					{
						return componentsInChild;
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private static bool IsUsableNativeInfiltrationMarkerTemplate(MapInfilMarker marker)
	{
		if (marker != null && marker.gameObject != null && !marker.gameObject.name.StartsWith("PACKAGE_INFIL_", StringComparison.Ordinal) && marker.SelectedParent != null && marker.DeselectedParent != null)
		{
			return marker.canvasGroup != null;
		}
		return false;
	}

	private void BeginCatalogOperationLaunch(CatalogPresentation presentation)
	{
		if (presentation == null || presentation.SelectedOperation == null)
		{
			return;
		}
		ModdedOperationDefinition operation = presentation.SelectedOperation;
		ModdedMapDefinition val = default(ModdedMapDefinition);
		if (!OperatorApi.ModdedOperations.TryGetMap(operation.MapId, ref val) || val == null)
		{
			log.LogError("Modded Operations launch rejected because map " + operation.MapId + " is no longer in the frozen catalog.");
			return;
		}
		if (!val.Operations.Any((ModdedOperationDefinition candidate) => string.Equals(candidate.Id, operation.Id, StringComparison.Ordinal)) || !operation.SupportedTimeCodes.Contains<string>(presentation.SelectedTimeCode, StringComparer.Ordinal))
		{
			log.LogError("Modded Operations launch rejected because the selected operation/time pair is not owned by its frozen package map.");
			return;
		}
		log.LogInfo("Modded Operations launch request captured: operation=" + operation.Id + ", laptopId=" + presentation.Laptop.GetInstanceID() + ", time=" + presentation.SelectedTimeCode + ".");
		MissionLaptop missionLaptop = ResolveLaunchLaptop(presentation.Laptop);
		PlayerNetworking playerNetworking = ((missionLaptop == null) ? null : missionLaptop.playerNetworking);
		if (!IsPlayerOwnedLaunchLaptop(missionLaptop) || playerNetworking == null)
		{
			log.LogError("Modded Operations launch rejected before package loading because no player-owned mission laptop was available.");
			return;
		}
		SetNativeConfirmationLoadingState(presentation, loading: true);
		if (loadedMapBundles.TryGetValue(val.Id, out var value) && value != null && value.SceneBundle != null && value.Map != null && string.Equals(value.Map.PackageContentId, val.PackageContentId, StringComparison.Ordinal))
		{
			InvokeNativeCatalogLaunch(presentation, val, operation, presentation.SelectedTimeCode, missionLaptop, playerNetworking);
			return;
		}
		if (pendingLaunch != null)
		{
			ModdedMapDefinition map = pendingLaunch.Map;
			if (string.Equals((map != null) ? map.Id : null, val.Id, StringComparison.Ordinal))
			{
				ModdedMapDefinition map2 = pendingLaunch.Map;
				if (string.Equals((map2 != null) ? map2.PackageContentId : null, val.PackageContentId, StringComparison.Ordinal))
				{
					pendingLaunch.Presentation = presentation;
					pendingLaunch.Operation = operation;
					pendingLaunch.TimeCode = presentation.SelectedTimeCode;
					pendingLaunch.LaunchLaptop = missionLaptop;
					pendingLaunch.LaunchPlayer = playerNetworking;
					pendingLaunch.LaunchRequested = true;
					pendingLaunch.LaunchRequestedTimestamp = Stopwatch.GetTimestamp();
					log.LogInfo("Modded Operations attached Confirm to the selected-map prefetch: map=" + val.Id + ", completedBundles=" + pendingLaunch.RequestIndex + "/" + pendingLaunch.BundlePaths.Count + ".");
					return;
				}
			}
			SetNativeConfirmationLoadingState(presentation, loading: false);
			ManualLogSource manualLogSource = log;
			string[] obj = new string[5] { "Modded Operations ignored a launch while another selected map is still loading: requested=", val.Id, ", loading=", null, null };
			ModdedMapDefinition map3 = pendingLaunch.Map;
			obj[3] = ((map3 != null) ? map3.Id : null);
			obj[4] = ".";
			manualLogSource.LogWarning(string.Concat(obj));
			return;
		}
		if (value != null)
		{
			try
			{
				value.SceneBundle?.Unload(unloadAllLoadedObjects: false);
			}
			catch
			{
			}
			foreach (AssetBundle dependency in value.Dependencies)
			{
				try
				{
					dependency?.Unload(unloadAllLoadedObjects: false);
				}
				catch
				{
				}
			}
			loadedMapBundles.Remove(val.Id);
			log.LogWarning("Modded Operations discarded an incomplete or stale bundle cache entry before loading map=" + val.Id + ".");
		}
		LoadedMapBundles loadingBundles = new LoadedMapBundles
		{
			Map = val
		};
		PendingMapLaunch pendingMapLaunch = new PendingMapLaunch
		{
			Presentation = presentation,
			Map = val,
			Operation = operation,
			TimeCode = presentation.SelectedTimeCode,
			LaunchLaptop = missionLaptop,
			LaunchPlayer = playerNetworking,
			LoadingBundles = loadingBundles,
			LaunchRequested = true,
			LoadStartedTimestamp = Stopwatch.GetTimestamp(),
			LaunchRequestedTimestamp = Stopwatch.GetTimestamp()
		};
		foreach (string dependencyBundlePath in val.DependencyBundlePaths)
		{
			pendingMapLaunch.BundlePaths.Add(dependencyBundlePath);
		}
		pendingMapLaunch.BundlePaths.Add(val.SceneBundlePath);
		pendingLaunch = pendingMapLaunch;
		log.LogInfo("Modded Operations began asynchronous package load: map=" + val.Id + ", bundles=" + pendingMapLaunch.BundlePaths.Count + ".");
	}

	private void ProcessPendingLaunch()
	{
		PendingMapLaunch pendingMapLaunch = pendingLaunch;
		if (pendingMapLaunch == null)
		{
			return;
		}
		try
		{
			if (pendingMapLaunch.CurrentRequest == null)
			{
				if (pendingMapLaunch.RequestIndex >= pendingMapLaunch.BundlePaths.Count)
				{
					if (!ValidateLoadedSceneBundle(pendingMapLaunch.Map, pendingMapLaunch.LoadingBundles))
					{
						FailPendingLaunch("scene bundle is missing its declared scene address or contains an undeclared scene address");
						return;
					}
					loadedMapBundles[pendingMapLaunch.Map.Id] = pendingMapLaunch.LoadingBundles;
					pendingLaunch = null;
					log.LogInfo("Modded Operations completed verified bundle registration: map=" + pendingMapLaunch.Map.Id + ", seconds=" + FormatElapsedSeconds(pendingMapLaunch.LoadStartedTimestamp) + ", launchRequested=" + pendingMapLaunch.LaunchRequested + ".");
					if (pendingMapLaunch.LaunchRequested)
					{
						log.LogInfo("Modded Operations Confirm waited " + FormatElapsedSeconds(pendingMapLaunch.LaunchRequestedTimestamp) + " seconds for remaining selected-map bundle work.");
						InvokeNativeCatalogLaunch(pendingMapLaunch.Presentation, pendingMapLaunch.Map, pendingMapLaunch.Operation, pendingMapLaunch.TimeCode, pendingMapLaunch.LaunchLaptop, pendingMapLaunch.LaunchPlayer);
					}
				}
				else
				{
					string text = pendingMapLaunch.BundlePaths[pendingMapLaunch.RequestIndex];
					pendingMapLaunch.CurrentRequestStartedTimestamp = Stopwatch.GetTimestamp();
					pendingMapLaunch.CurrentRequest = AssetBundle.LoadFromFileAsync(text);
					if (pendingMapLaunch.CurrentRequest == null)
					{
						FailPendingLaunch("Unity rejected bundle request for " + text);
					}
				}
			}
			else
			{
				if (!pendingMapLaunch.CurrentRequest.isDone)
				{
					return;
				}
				AssetBundle assetBundle = pendingMapLaunch.CurrentRequest.assetBundle;
				pendingMapLaunch.CurrentRequest = null;
				if (assetBundle == null)
				{
					FailPendingLaunch("Unity could not load bundle " + pendingMapLaunch.BundlePaths[pendingMapLaunch.RequestIndex]);
					return;
				}
				long num = 0L;
				try
				{
					num = new FileInfo(pendingMapLaunch.BundlePaths[pendingMapLaunch.RequestIndex]).Length;
				}
				catch
				{
				}
				log.LogInfo("Modded Operations loaded verified bundle " + (pendingMapLaunch.RequestIndex + 1) + "/" + pendingMapLaunch.BundlePaths.Count + ": file=" + Path.GetFileName(pendingMapLaunch.BundlePaths[pendingMapLaunch.RequestIndex]) + ", bytes=" + num + ", seconds=" + FormatElapsedSeconds(pendingMapLaunch.CurrentRequestStartedTimestamp) + ".");
				if (pendingMapLaunch.RequestIndex == pendingMapLaunch.BundlePaths.Count - 1)
				{
					pendingMapLaunch.LoadingBundles.SceneBundle = assetBundle;
				}
				else
				{
					string[] array = null;
					try
					{
						array = assetBundle.GetAllScenePaths();
					}
					catch
					{
					}
					if (array != null && array.Length != 0)
					{
						try
						{
							assetBundle.Unload(unloadAllLoadedObjects: false);
						}
						catch
						{
						}
						FailPendingLaunch("dependency bundle contains a streamed scene: " + pendingMapLaunch.BundlePaths[pendingMapLaunch.RequestIndex]);
						return;
					}
					pendingMapLaunch.LoadingBundles.Dependencies.Add(assetBundle);
					pendingMapLaunch.LoadingBundles.DependenciesByPath[pendingMapLaunch.BundlePaths[pendingMapLaunch.RequestIndex]] = assetBundle;
				}
				pendingMapLaunch.RequestIndex++;
			}
		}
		catch (Exception ex)
		{
			FailPendingLaunch(ex.GetType().Name + ": " + ex.Message);
		}
	}

	private static long GetBundleByteTotal(System.Collections.Generic.IEnumerable<string> paths)
	{
		long num = 0L;
		foreach (string item in paths ?? Enumerable.Empty<string>())
		{
			try
			{
				num = checked(num + new FileInfo(item).Length);
			}
			catch
			{
			}
		}
		return num;
	}

	private static string FormatElapsedSeconds(long startedTimestamp)
	{
		if (startedTimestamp <= 0)
		{
			return "0.000";
		}
		return ((double)(Stopwatch.GetTimestamp() - startedTimestamp) / (double)Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture);
	}

	private static bool ValidateLoadedSceneBundle(ModdedMapDefinition map, LoadedMapBundles bundles)
	{
		if (map == null || bundles == null || bundles.SceneBundle == null)
		{
			return false;
		}
		try
		{
			HashSet<string> hashSet = new HashSet<string>(from candidate in OperatorApi.ModdedOperations.Maps
				where candidate != null && string.Equals(candidate.PackageContentId, map.PackageContentId, StringComparison.Ordinal) && string.Equals(candidate.SceneBundlePath, map.SceneBundlePath, StringComparison.OrdinalIgnoreCase)
				select candidate.ScenePath, StringComparer.OrdinalIgnoreCase);
			if (!hashSet.Contains(map.ScenePath))
			{
				return false;
			}
			bool result = false;
			foreach (string allScenePath in bundles.SceneBundle.GetAllScenePaths())
			{
				if (!hashSet.Contains(allScenePath))
				{
					return false;
				}
				if (string.Equals(allScenePath, map.ScenePath, StringComparison.OrdinalIgnoreCase))
				{
					result = true;
				}
			}
			return result;
		}
		catch
		{
		}
		return false;
	}

	private void FailPendingLaunch(string reason)
	{
		PendingMapLaunch pendingMapLaunch = pendingLaunch;
		pendingLaunch = null;
		if (pendingMapLaunch?.LoadingBundles != null)
		{
			try
			{
				pendingMapLaunch.LoadingBundles.SceneBundle?.Unload(unloadAllLoadedObjects: false);
			}
			catch
			{
			}
			foreach (AssetBundle dependency in pendingMapLaunch.LoadingBundles.Dependencies)
			{
				try
				{
					dependency?.Unload(unloadAllLoadedObjects: false);
				}
				catch
				{
				}
			}
		}
		log.LogError("Modded Operations package " + ((pendingMapLaunch != null && pendingMapLaunch.LaunchRequested) ? "launch" : "prefetch") + " failed closed: " + reason + ".");
		if (pendingMapLaunch?.Presentation != null)
		{
			SetNativeConfirmationLoadingState(pendingMapLaunch.Presentation, loading: false);
		}
	}

	private void InvokeNativeCatalogLaunch(CatalogPresentation presentation, ModdedMapDefinition map, ModdedOperationDefinition operation, string timeCode, MissionLaptop launchLaptop, PlayerNetworking launchPlayer)
	{
		if (presentation == null || presentation.NativeBoardData == null || map == null || operation == null)
		{
			return;
		}
		try
		{
			presentation.SelectedOperation = operation;
			presentation.SelectedTimeCode = timeCode;
			UpdateCatalogOperationBoard(presentation);
			if (!string.Equals(operation.MapId, map.Id, StringComparison.Ordinal) || !operation.SupportedTimeCodes.Contains<string>(timeCode, StringComparer.Ordinal))
			{
				throw new InvalidOperationException("the requested operation/time pair does not belong to the selected map");
			}
			activeOperation = new ActiveMapOperation
			{
				Map = map,
				Operation = operation,
				TimeCode = timeCode,
				SceneHandle = 0
			};
			log.LogInfo("Modded Operations accepted an isolated package launch: operation=" + operation.Id + ", map=" + map.Id + ", content=" + map.PackageContentId + ", scene=" + map.ScenePath + ", time=" + timeCode + ".");
			LogNativeLaunchContract(presentation.NativeBoardData);
			InvokeNativeBoardStart(presentation, map, operation, timeCode, launchLaptop, launchPlayer);
		}
		catch (Exception ex)
		{
			activeOperation = null;
			SetNativeConfirmationLoadingState(presentation, loading: false);
			log.LogError("Modded Operations native start failed closed: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	private void InvokeNativeBoardStart(CatalogPresentation presentation, ModdedMapDefinition map, ModdedOperationDefinition operation, string timeCode, MissionLaptop capturedLaptop, PlayerNetworking capturedPlayer)
	{
		OperationsManager singleton = OperationsManager.singleton;
		CerebusOpboard cerebusOpboard = presentation?.NativeBoardData;
		if (singleton == null || cerebusOpboard == null || map == null || operation == null)
		{
			throw new InvalidOperationException("native operation manager was unavailable");
		}
		if (!string.Equals(operation.MapId, map.Id, StringComparison.Ordinal) || !operation.SupportedTimeCodes.Contains<string>(timeCode, StringComparer.Ordinal))
		{
			throw new InvalidOperationException("package operation ownership changed before native start");
		}
		MissionLaptop missionLaptop = RestoreCapturedLaunchLaptop(capturedLaptop, capturedPlayer, presentation.Laptop);
		if (missionLaptop == null)
		{
			throw new InvalidOperationException("no player-owned mission laptop was available for native launch");
		}
		if (singleton.currentOpInfo == null)
		{
			singleton.currentOpInfo = new CurrentOpInfo();
		}
		if (singleton.currentOpInfo == null)
		{
			throw new InvalidOperationException("native current-operation state could not be allocated");
		}
		cerebusOpboard.missionLaptop = missionLaptop;
		singleton.activeMissionLaptop = missionLaptop;
		if (cerebusOpboard.TARGETPACKAGE == null || cerebusOpboard.TARGETPACKAGE.Length == 0 || cerebusOpboard.SelectedInfiltrationTime < 0 || cerebusOpboard.SelectedInfiltrationTime >= cerebusOpboard.TARGETPACKAGE.Length || cerebusOpboard.missionLaptop == null || cerebusOpboard.OperationBoardUI == null)
		{
			throw new InvalidOperationException("private operation board launch contract was incomplete");
		}
		log.LogInfo("Modded Operations completed the native board ownership graph: sourceLaptop=" + presentation.Laptop.GetInstanceID() + ", launchLaptop=" + missionLaptop.GetInstanceID() + ", playerOwned=" + (missionLaptop.playerNetworking != null) + ", currentOperationInfo=" + (singleton.currentOpInfo != null) + ".");
		CloseNativeMapConfirmation(presentation.Board, logClose: false);
		PrimeNativeInfiltrationSelector(cerebusOpboard, operation);
		cerebusOpboard.Start_Operation();
		log.LogInfo("Modded Operations entered the shipped board launch pipeline through CerebusOpboard.Start_Operation: operation=" + operation.Id + ", scene=" + map.ScenePath + ".");
	}

	private void PrimeNativeInfiltrationSelector(CerebusOpboard board, ModdedOperationDefinition operation)
	{
		if (board == null || board.MapPrefab == null || operation == null || operation.Infiltrations == null || operation.Infiltrations.Count == 0)
		{
			throw new InvalidOperationException("package infiltration selector data was unavailable");
		}
		InfilSelectorDisplayer infilSelectorDisplayer = InfilSelectorDisplayer.instance ?? Resources.FindObjectsOfTypeAll<InfilSelectorDisplayer>().FirstOrDefault((InfilSelectorDisplayer item) => item != null && item.MapParent != null && item.SelectInfilUI != null);
		if (infilSelectorDisplayer == null)
		{
			throw new InvalidOperationException("the shipped infiltration selector was unavailable");
		}
		bool activeSelf = board.MapPrefab.activeSelf;
		try
		{
			if (!activeSelf)
			{
				board.MapPrefab.SetActive(value: true);
			}
			infilSelectorDisplayer.SpawnMap(board.MapPrefab);
		}
		finally
		{
			if (board.MapPrefab != null && board.MapPrefab.activeSelf != activeSelf)
			{
				board.MapPrefab.SetActive(activeSelf);
			}
		}
		MapInfilMarker[] array = ((infilSelectorDisplayer.MapInfilMarkers == null) ? Array.Empty<MapInfilMarker>() : infilSelectorDisplayer.MapInfilMarkers.ToArray());
		if (infilSelectorDisplayer.ActiveMap == null || !infilSelectorDisplayer.ActiveMap.name.StartsWith("MODDED_OPERATIONS_PACKAGE_INFILTRATION_MAP", StringComparison.Ordinal) || array.Length != operation.Infiltrations.Count)
		{
			throw new InvalidOperationException("the shipped infiltration selector did not instantiate the package map (selector=" + infilSelectorDisplayer.GetInstanceID() + ", activeMap=" + ((infilSelectorDisplayer.ActiveMap == null) ? "null" : infilSelectorDisplayer.ActiveMap.name) + ", markers=" + array.Length + ", expectedMarkers=" + operation.Infiltrations.Count + ")");
		}
		for (int num = 0; num < array.Length; num++)
		{
			MapInfilMarker mapInfilMarker = array[num];
			ModdedInfiltrationDefinition val = operation.Infiltrations[num];
			if (mapInfilMarker == null || mapInfilMarker.MarkerIndex != num || !string.Equals(mapInfilMarker.InfilName, val.DisplayName, StringComparison.Ordinal) || mapInfilMarker.MaxPlayers != val.MaximumPlayers || !mapInfilMarker.IsGroundInfil || mapInfilMarker.IsHeliInfil || mapInfilMarker.IsExfil)
			{
				throw new InvalidOperationException("the shipped infiltration selector retained non-package marker data");
			}
		}
		log.LogInfo("Modded Operations primed the shipped infiltration selector through InfilSelectorDisplayer.SpawnMap: operation=" + operation.Id + ", activeMap=" + infilSelectorDisplayer.ActiveMap.name + ", markers=" + array.Length + ".");
	}

	private static MissionLaptop ResolveLaunchLaptop(MissionLaptop preferred)
	{
		MissionLaptop missionLaptop = ((OperationsManager.singleton == null) ? null : OperationsManager.singleton.activeMissionLaptop);
		if (IsPlayerOwnedLaunchLaptop(missionLaptop))
		{
			return missionLaptop;
		}
		if (IsPlayerOwnedLaunchLaptop(preferred))
		{
			return preferred;
		}
		MissionLaptop missionLaptop2 = null;
		foreach (Component item in FindMissionLaptopComponents(ResolveMissionLaptopType()))
		{
			MissionLaptop missionLaptop3 = item as MissionLaptop;
			if (IsPlayerOwnedLaunchLaptop(missionLaptop3))
			{
				if (missionLaptop3.gameObject.activeInHierarchy)
				{
					return missionLaptop3;
				}
				if ((object)missionLaptop2 == null)
				{
					missionLaptop2 = missionLaptop3;
				}
			}
		}
		return missionLaptop2;
	}

	private static MissionLaptop RestoreCapturedLaunchLaptop(MissionLaptop capturedLaptop, PlayerNetworking capturedPlayer, MissionLaptop preferred)
	{
		if (IsPlayerOwnedLaunchLaptop(capturedLaptop))
		{
			return capturedLaptop;
		}
		if (capturedLaptop != null && capturedPlayer != null && capturedPlayer.gameObject != null && capturedPlayer.isOwned)
		{
			capturedLaptop.playerNetworking = capturedPlayer;
			if (IsPlayerOwnedLaunchLaptop(capturedLaptop))
			{
				return capturedLaptop;
			}
		}
		return ResolveLaunchLaptop(preferred);
	}

	private static bool IsPlayerOwnedLaunchLaptop(MissionLaptop laptop)
	{
		if (laptop != null && laptop.gameObject != null && laptop.playerNetworking != null && laptop.LaptopPlayerCamera != null && laptop.computerManager != null && laptop.uiRaycaster != null && laptop.PlayerBlocker != null)
		{
			return laptop.ShitToKillWhenNotUsing != null;
		}
		return false;
	}

	private void LogNativeLaunchContract(CerebusOpboard board)
	{
		if (board == null)
		{
			return;
		}
		try
		{
			CerebusTargetPackage thisMissionTargetPackage = board.ThisMissionTargetPackage;
			MissionLaptop missionLaptop = board.missionLaptop;
			log.LogInfo("Modded Operations private UI launch contract: mapPrefab=" + ((board.MapPrefab == null) ? "null" : board.MapPrefab.name) + ", targetPackage=" + ((thisMissionTargetPackage == null) ? "null" : thisMissionTargetPackage.name) + ", targetBackref=" + ((thisMissionTargetPackage == null || thisMissionTargetPackage.TargetPackage == null) ? "null" : thisMissionTargetPackage.TargetPackage.name) + ", targets=" + ((board.TARGETPACKAGE == null) ? (-1) : board.TARGETPACKAGE.Length) + ", selectedInfil=" + board.SelectedInfiltrationTime + ", operationBoard=" + (board.OperationBoardUI != null) + ", laptop=" + (missionLaptop != null) + ", laptopPlayer=" + (missionLaptop != null && missionLaptop.playerNetworking != null) + ", operationsManager=" + (OperationsManager.singleton != null) + ", gameManagerNetwork=" + (GameManagerNetwork.instance != null) + ".");
		}
		catch (Exception ex)
		{
			log.LogWarning("Modded Operations could not describe the native launch contract: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		ActiveMapOperation activeMapOperation = activeOperation;
		if (activeMapOperation != null && activeMapOperation.Map != null && scene.IsValid() && scene.isLoaded && SceneMatchesMap(scene, activeMapOperation.Map))
		{
			if (!ValidateStandaloneSceneContract(scene, activeMapOperation, out var error))
			{
				log.LogError("Standalone package scene contract rejected map=" + activeMapOperation.Map.Id + ": " + error + ".");
				return;
			}
			ReleaseStandaloneSceneContracts(activeMapOperation);
			activeMapOperation.SceneHandle = scene.handle;
			activeMapOperation.BootstrapRoot = null;
			activeMapOperation.BootstrapIdentity = null;
			activeMapOperation.BootstrapPrefabRoot = null;
			activeMapOperation.BootstrapPrefabIdentity = null;
			activeMapOperation.BootstrapAssetId = 0u;
			activeMapOperation.BootstrapPrefabRegistered = false;
			activeMapOperation.GameModeComponent = null;
			activeMapOperation.BootstrapCreated = false;
			activeMapOperation.NetworkSpawnRequested = false;
			activeMapOperation.ReadinessInitializationClaimed = false;
			activeMapOperation.ReadinessInitialized = false;
			activeMapOperation.AllPlayersLoaded = false;
			activeMapOperation.NativePvpLifecycleActive = false;
			activeMapOperation.AllPlayersLoadedFrame = -1;
			activeMapOperation.BootstrapFrame = Time.frameCount;
			activeMapOperation.LastMaintenanceFrame = -1;
			activeMapOperation.SpawnCursor = 0;
			activeMapOperation.ScenePreparationComplete = false;
			activeMapOperation.ScenePreparationStarted = false;
			activeMapOperation.ScenePreparationEarliestFrame = Time.frameCount + 1;
			activeMapOperation.TerrainReady = false;
			ReleaseRuntimeTerrain(activeMapOperation);
			activeMapOperation.PositionedPlayerObjects.Clear();
			activeMapOperation.PlayerMarkerNames.Clear();
			activeMapOperation.PlayerSpawnRequestFrames.Clear();
			activeMapOperation.PlayerSpawnRequestCounts.Clear();
			activeMapOperation.CompletedPlayerSpawnIds.Clear();
			activeMapOperation.PlayerMoveRequestFrames.Clear();
			activeMapOperation.PveSpawnAttempted = false;
			activeMapOperation.PveEnemyCount = 0;
			activeMapOperation.RaidUtilityRoot = null;
		}
	}

	private void OnSceneUnloaded(Scene scene)
	{
		ActiveMapOperation activeMapOperation = activeOperation;
		if (activeMapOperation != null && !(scene.handle != activeMapOperation.SceneHandle))
		{
			ReleaseStandaloneSceneContracts(activeMapOperation);
			activeMapOperation.SceneHandle = 0;
			activeMapOperation.BootstrapRoot = null;
			activeMapOperation.BootstrapIdentity = null;
			activeMapOperation.BootstrapPrefabRoot = null;
			activeMapOperation.BootstrapPrefabIdentity = null;
			activeMapOperation.BootstrapAssetId = 0u;
			activeMapOperation.BootstrapPrefabRegistered = false;
			activeMapOperation.GameModeComponent = null;
			activeMapOperation.BootstrapCreated = false;
			activeMapOperation.NetworkSpawnRequested = false;
			activeMapOperation.ReadinessInitializationClaimed = false;
			activeMapOperation.ReadinessInitialized = false;
			activeMapOperation.AllPlayersLoaded = false;
			activeMapOperation.NativePvpLifecycleActive = false;
			activeMapOperation.AllPlayersLoadedFrame = -1;
			activeMapOperation.ScenePreparationComplete = false;
			activeMapOperation.ScenePreparationStarted = false;
			activeMapOperation.ScenePreparationEarliestFrame = -1;
			activeMapOperation.TerrainReady = false;
			ReleaseRuntimeTerrain(activeMapOperation);
			activeMapOperation.PositionedPlayerObjects.Clear();
			activeMapOperation.PlayerMarkerNames.Clear();
			activeMapOperation.PlayerSpawnRequestFrames.Clear();
			activeMapOperation.PlayerSpawnRequestCounts.Clear();
			activeMapOperation.CompletedPlayerSpawnIds.Clear();
			activeMapOperation.PlayerMoveRequestFrames.Clear();
			activeMapOperation.PveSpawnAttempted = false;
			activeMapOperation.PveEnemyCount = 0;
			activeMapOperation.RaidUtilityRoot = null;
			log.LogInfo("Modded Operations map scene unloaded; package bundles remain resident so the shipped Restart Operation route can reload the same scene.");
		}
	}

	private void PrepareStandaloneScene(Scene scene, ActiveMapOperation operation)
	{
		if (activeOperation != operation || operation.SceneHandle != scene.handle || !scene.IsValid() || !scene.isLoaded)
		{
			return;
		}
		if (!TryPrepareRuntimeTerrain(scene, operation, out var error))
		{
			log.LogError("Standalone package terrain preparation failed closed: map=" + operation.Map.Id + ", reason=" + error + ".");
			return;
		}
		Physics.SyncTransforms();
		if (!ValidateWalkableGroundContract(scene, operation, out var error2))
		{
			log.LogError("Standalone package walkable-ground contract failed closed: map=" + operation.Map.Id + ", reason=" + error2 + ".");
			ReleaseRuntimeTerrain(operation);
		}
		else
		{
			operation.TerrainReady = true;
			ConfigureStandalonePlayerSpawnContract(scene, operation);
			CreateStandaloneGameplayBootstrap(scene, operation);
			ApplyStandaloneRenderContract(scene, operation);
			operation.ScenePreparationComplete = operation.BootstrapCreated;
			log.LogInfo("Standalone package scene services are ready before native player spawn: map=" + operation.Map.Id + ", terrain=" + (operation.Map.RuntimeTerrain != null) + ", walkableGround=true, bootstrap=" + operation.BootstrapCreated + ".");
		}
	}

	private static bool SceneMatchesMap(Scene scene, ModdedMapDefinition map)
	{
		if (map == null)
		{
			return false;
		}
		if (!string.IsNullOrEmpty(scene.path) && string.Equals(scene.path, map.ScenePath, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return string.Equals(scene.name, Path.GetFileNameWithoutExtension(map.ScenePath), StringComparison.OrdinalIgnoreCase);
	}

	private bool TryPrepareRuntimeTerrain(Scene scene, ActiveMapOperation operation, out string error)
	{
		error = string.Empty;
		object obj;
		if (operation == null)
		{
			obj = null;
		}
		else
		{
			ModdedMapDefinition map = operation.Map;
			obj = ((map != null) ? map.RuntimeTerrain : null);
		}
		ModdedRuntimeTerrainDefinition val = (ModdedRuntimeTerrainDefinition)obj;
		if (val == null)
		{
			return true;
		}
		if (!loadedMapBundles.TryGetValue(operation.Map.Id, out var value) || value == null || value.Map == null || !string.Equals(value.Map.PackageContentId, operation.Map.PackageContentId, StringComparison.Ordinal) || !value.DependenciesByPath.TryGetValue(val.VerifiedDependencyBundlePath, out var value2) || value2 == null)
		{
			error = "the verified terrain dependency bundle is not resident";
			return false;
		}
		int matches;
		Transform transform = FindExactSceneTransform(scene, val.RootObjectName, out matches);
		if (transform == null || matches != 1)
		{
			error = "runtimeTerrain rootObject must resolve to exactly one scene object";
			return false;
		}
		TerrainData terrainData = null;
		System.Collections.Generic.List<TerrainLayer> list = new System.Collections.Generic.List<TerrainLayer>();
		try
		{
			Texture2D texture2D = LoadRequiredTerrainTexture(value2, val.HeightPayloadAssetPath, requireReadable: true);
			Texture2D texture2D2 = LoadRequiredTerrainTexture(value2, val.SurfaceWeightsPayloadAssetPath, requireReadable: true);
			if (texture2D == null || texture2D2 == null)
			{
				error = "one or more numerical terrain payloads could not be loaded";
				return false;
			}
			if (texture2D.width != val.HeightmapResolution || texture2D.height != val.HeightmapResolution || texture2D2.width != val.AlphamapResolution || texture2D2.height != val.AlphamapResolution)
			{
				error = "terrain payload dimensions do not match the frozen manifest";
				return false;
			}
			Texture2D[] array = new Texture2D[val.Layers.Count];
			Texture2D[] array2 = new Texture2D[val.Layers.Count];
			Texture2D[] array3 = new Texture2D[val.Layers.Count];
			for (int i = 0; i < val.Layers.Count; i++)
			{
				ModdedRuntimeTerrainLayerDefinition val2 = val.Layers[i];
				array[i] = LoadRequiredTerrainTexture(value2, val2.DiffuseAssetPath, requireReadable: false);
				array2[i] = LoadRequiredTerrainTexture(value2, val2.NormalAssetPath, requireReadable: false);
				array3[i] = LoadRequiredTerrainTexture(value2, val2.MaskAssetPath, requireReadable: false);
				if (array[i] == null || array2[i] == null || array3[i] == null)
				{
					error = "one or more terrain-layer textures could not be loaded";
					return false;
				}
			}
			terrainData = new TerrainData
			{
				name = "MODDED_OPERATIONS_RUNTIME_TERRAIN_" + operation.Map.Id,
				heightmapResolution = val.HeightmapResolution,
				alphamapResolution = val.AlphamapResolution,
				baseMapResolution = val.BaseMapResolution,
				size = new Vector3(val.Width, val.Height, val.Length)
			};
			terrainData.SetDetailResolution(val.DetailResolution, val.DetailResolutionPerPatch);
			Il2CppStructArray<Color32> pixels = texture2D.GetPixels32();
			Il2CppStructArray<float> il2CppStructArray = AllocateIl2CppFloatArray(val.HeightmapResolution, val.HeightmapResolution);
			Span<float> span = il2CppStructArray.AsSpan();
			for (int j = 0; j < val.HeightmapResolution; j++)
			{
				for (int k = 0; k < val.HeightmapResolution; k++)
				{
					Color32 color = pixels[j * val.HeightmapResolution + k];
					span[j * val.HeightmapResolution + k] = (float)((color.r << 8) | color.g) / 65535f;
				}
			}
			terrainData.SetHeights(0, 0, il2CppStructArray);
			Il2CppReferenceArray<TerrainLayer> il2CppReferenceArray = new Il2CppReferenceArray<TerrainLayer>(val.Layers.Count);
			for (int l = 0; l < val.Layers.Count; l++)
			{
				ModdedRuntimeTerrainLayerDefinition val3 = val.Layers[l];
				TerrainLayer terrainLayer = new TerrainLayer
				{
					name = val3.Name,
					diffuseTexture = array[l],
					normalMapTexture = array2[l],
					maskMapTexture = array3[l],
					tileSize = new Vector2(val3.TileSizeX, val3.TileSizeZ),
					tileOffset = Vector2.zero,
					normalScale = val3.NormalScale,
					metallic = val3.Metallic,
					smoothness = val3.Smoothness
				};
				list.Add(terrainLayer);
				il2CppReferenceArray[l] = terrainLayer;
			}
			terrainData.terrainLayers = il2CppReferenceArray;
			Il2CppStructArray<Color32> pixels2 = texture2D2.GetPixels32();
			Il2CppStructArray<float> il2CppStructArray2 = AllocateIl2CppFloatArray(val.AlphamapResolution, val.AlphamapResolution, val.Layers.Count);
			Span<float> span2 = il2CppStructArray2.AsSpan();
			for (int m = 0; m < val.AlphamapResolution; m++)
			{
				for (int n = 0; n < val.AlphamapResolution; n++)
				{
					Color32 color2 = pixels2[m * val.AlphamapResolution + n];
					float num = (float)(int)color2.r / 255f;
					float num2 = (float)(int)color2.g / 255f;
					float num3 = (float)(int)color2.b / 255f;
					float num4 = num + num2 + num3;
					if (num4 <= 1E-05f)
					{
						num = 1f;
						num2 = 0f;
						num3 = 0f;
					}
					else
					{
						num /= num4;
						num2 /= num4;
						num3 /= num4;
					}
					int num5 = (m * val.AlphamapResolution + n) * 3;
					span2[num5] = num;
					span2[num5 + 1] = num2;
					span2[num5 + 2] = num3;
				}
			}
			terrainData.SetAlphamaps(0, 0, il2CppStructArray2);
			Terrain terrain = transform.GetComponent<Terrain>();
			if (terrain == null)
			{
				terrain = transform.gameObject.AddComponent<Terrain>();
			}
			TerrainCollider terrainCollider = transform.GetComponent<TerrainCollider>();
			if (terrainCollider == null)
			{
				terrainCollider = transform.gameObject.AddComponent<TerrainCollider>();
			}
			transform.position = new Vector3(val.OriginX, val.OriginY, val.OriginZ);
			terrain.terrainData = terrainData;
			terrainCollider.terrainData = terrainData;
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
			Transform transform2 = transform.Find("NATIVE_Ground_HillyTerrain_RenderFallback");
			if (transform2 != null && transform2.gameObject.activeSelf)
			{
				transform2.gameObject.SetActive(value: false);
				log.LogInfo("Disabled package terrain render fallback after TerrainData bind: " + transform2.name + ".");
			}
			Physics.SyncTransforms();
			if (terrain.terrainData != terrainData || terrainCollider.terrainData != terrainData)
			{
				error = "Unity did not bind the reconstructed TerrainData to rendering and collision";
				return false;
			}
			operation.RuntimeTerrainData = terrainData;
			operation.RuntimeTerrainLayers.AddRange(list);
			log.LogInfo("Standalone reconstructed package-owned runtime terrain: map=" + operation.Map.Id + ", root=" + val.RootObjectName + ", size=" + terrainData.size.ToString() + ", heightmap=" + terrainData.heightmapResolution + ", alphamap=" + terrainData.alphamapResolution + ", layers=" + terrainData.terrainLayers.Length + ", colliderBound=true.");
			return true;
		}
		catch (Exception ex)
		{
			error = ex.GetType().Name + ": " + ex.Message;
			return false;
		}
		finally
		{
			if (operation.RuntimeTerrainData != terrainData)
			{
				if (terrainData != null)
				{
					UnityEngine.Object.Destroy(terrainData);
				}
				foreach (TerrainLayer item in list)
				{
					if (item != null)
					{
						UnityEngine.Object.Destroy(item);
					}
				}
			}
		}
	}

	private Texture2D LoadRequiredTerrainTexture(AssetBundle bundle, string assetPath, bool requireReadable)
	{
		string diagnostic;
		Texture2D texture2D = NativeBundleAssetLoader.LoadTexture2D(bundle, assetPath, out diagnostic);
		if (texture2D == null)
		{
			log.LogError("Required package terrain Texture2D could not be loaded: asset=" + assetPath + ", diagnostic=" + diagnostic);
			return null;
		}
		if (requireReadable && !texture2D.isReadable)
		{
			log.LogError("Required numerical package terrain texture is not readable: " + assetPath + ".");
			return null;
		}
		return texture2D;
	}

	private static Transform FindExactSceneTransform(Scene scene, string exactName, out int matches)
	{
		matches = 0;
		Transform transform = null;
		foreach (GameObject rootGameObject in scene.GetRootGameObjects())
		{
			foreach (Transform componentsInChild in rootGameObject.GetComponentsInChildren<Transform>(includeInactive: true))
			{
				if (componentsInChild != null && string.Equals(componentsInChild.name, exactName, StringComparison.Ordinal))
				{
					matches++;
					if ((object)transform == null)
					{
						transform = componentsInChild;
					}
				}
			}
		}
		return transform;
	}

	private static bool ValidateWalkableGroundContract(Scene scene, ActiveMapOperation operation, out string error)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		error = string.Empty;
		System.Collections.Generic.List<Transform> list = FindStandalonePlayerMarkers(scene, operation.Operation.Mode);
		if (list.Count == 0)
		{
			error = "no compatible player markers were available for ground checks";
			return false;
		}
		System.Collections.Generic.List<Collider> list2 = new System.Collections.Generic.List<Collider>();
		foreach (GameObject rootGameObject in scene.GetRootGameObjects())
		{
			list2.AddRange(from collider in rootGameObject.GetComponentsInChildren<Collider>(includeInactive: true)
				where collider != null && collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger
				select collider);
		}
		if (list2.Count == 0)
		{
			error = "the package scene contains no active non-trigger collider";
			return false;
		}
		if (operation.Map.RuntimeTerrain != null)
		{
			int matches;
			Transform transform = FindExactSceneTransform(scene, operation.Map.RuntimeTerrain.RootObjectName, out matches);
			Terrain terrain = ((transform == null) ? null : transform.GetComponent<Terrain>());
			TerrainCollider terrainCollider = ((transform == null) ? null : transform.GetComponent<TerrainCollider>());
			if (matches != 1 || terrain == null || terrainCollider == null || terrain.terrainData == null || terrain.terrainData != terrainCollider.terrainData || terrain.terrainData != operation.RuntimeTerrainData)
			{
				error = "the declared terrain root does not own one shared render/collision TerrainData";
				return false;
			}
		}
		int num = 0;
		foreach (Transform item in list)
		{
			Ray ray = new Ray(item.position + Vector3.up * 64f, Vector3.down);
			bool flag = false;
			foreach (Collider item2 in list2)
			{
				if (item2.Raycast(ray, out var _, 256f))
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				num++;
			}
		}
		if (num != list.Count)
		{
			error = "only " + num + " of " + list.Count + " player markers raycast to package-owned walkable collision";
			return false;
		}
		return true;
	}

	private static void ReleaseRuntimeTerrain(ActiveMapOperation operation)
	{
		if (operation == null)
		{
			return;
		}
		if (operation.RuntimeTerrainData != null)
		{
			UnityEngine.Object.Destroy(operation.RuntimeTerrainData);
		}
		operation.RuntimeTerrainData = null;
		foreach (TerrainLayer runtimeTerrainLayer in operation.RuntimeTerrainLayers)
		{
			if (runtimeTerrainLayer != null)
			{
				UnityEngine.Object.Destroy(runtimeTerrainLayer);
			}
		}
		operation.RuntimeTerrainLayers.Clear();
	}

	private unsafe static Il2CppStructArray<float> AllocateIl2CppFloatArray(params int[] dimensions)
	{
		if (dimensions == null || dimensions.Length < 2 || dimensions.Length > 3)
		{
			throw new ArgumentOutOfRangeException("dimensions", "Terrain payload arrays must have rank 2 or 3.");
		}
		ulong[] array = new ulong[dimensions.Length];
		ulong[] array2 = new ulong[dimensions.Length];
		for (int i = 0; i < dimensions.Length; i++)
		{
			if (dimensions[i] <= 0)
			{
				throw new ArgumentOutOfRangeException("dimensions");
			}
			array[i] = (ulong)dimensions[i];
		}
		IntPtr intPtr = IL2CPP.il2cpp_array_class_get(IL2CPP.il2cpp_class_get_element_class(new Il2CppStructArray<float>(1L).ObjectClass), (uint)dimensions.Length);
		if (intPtr == IntPtr.Zero)
		{
			throw new InvalidOperationException("Could not resolve the native float array class.");
		}
		IntPtr intPtr2;
		fixed (ulong* ptr = array)
		{
			fixed (ulong* ptr2 = array2)
			{
				intPtr2 = IL2CPP.il2cpp_array_new_full(intPtr, ref *ptr, ref *ptr2);
			}
		}
		if (intPtr2 == IntPtr.Zero)
		{
			throw new InvalidOperationException("IL2CPP could not allocate a terrain array.");
		}
		return new Il2CppStructArray<float>(intPtr2);
	}

	private void ConfigureStandalonePlayerSpawnContract(Scene scene, ActiveMapOperation operation)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		if (operation == null || !scene.IsValid() || !scene.isLoaded)
		{
			return;
		}
		try
		{
			System.Collections.Generic.List<Transform> list = FindStandalonePlayerMarkers(scene, operation.Operation.Mode);
			if (list.Count == 0)
			{
				throw new InvalidOperationException("package scene has no compatible player spawn markers");
			}
			Il2CppSystem.Collections.Generic.List<SpawnPoint> list2 = new Il2CppSystem.Collections.Generic.List<SpawnPoint>();
			Il2CppReferenceArray<GameObject> il2CppReferenceArray = new Il2CppReferenceArray<GameObject>(list.Count);
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < list.Count; i++)
			{
				Transform transform = list[i];
				SpawnPoint spawnPoint = transform.GetComponent<SpawnPoint>() ?? transform.gameObject.AddComponent<SpawnPoint>();
				bool flag = IsTeamTwoPlayerMarker(transform.name);
				spawnPoint.CanSpawnPlayer = true;
				spawnPoint.Team = ((!flag) ? 1 : 2);
				if (flag)
				{
					num2++;
				}
				else
				{
					num++;
				}
				list2.Add(spawnPoint);
				il2CppReferenceArray[i] = transform.gameObject;
			}
			RestoreStandalonePlayerSpawnContract(operation);
			operation.PreviousSpawnPoints = GameManager.SpawnPointsInScene;
			operation.OwnedSpawnPoints = list2;
			operation.SpawnContractInstalled = true;
			GameManager.SpawnPointsInScene = list2;
			if (GameManager.instance != null)
			{
				operation.PreviousFallbackSpawns = GameManager.instance.Pspawns;
				operation.PreviousNextSpawnIndex = GameManager.instance.PnextSpawnIndex;
				operation.PreviousRandomSpawns = GameManager.instance.RandomSpawns;
				operation.OwnedRandomSpawns = false;
				operation.RandomSpawnsCaptured = true;
				operation.OwnedFallbackSpawns = il2CppReferenceArray;
				GameManager.instance.Pspawns = il2CppReferenceArray;
				GameManager.instance.PnextSpawnIndex = 0;
				GameManager.instance.RandomSpawns = operation.OwnedRandomSpawns;
			}
			log.LogInfo("Standalone registered the package-owned shipped player spawn contract: total=" + list.Count + ", team1=" + num + ", team2=" + num2 + ", gameManager=" + (GameManager.instance != null) + ", randomSpawns=" + ((GameManager.instance == null) ? "unavailable" : GameManager.instance.RandomSpawns.ToString()) + ".");
		}
		catch (Exception ex)
		{
			RestoreStandalonePlayerSpawnContract(operation);
			log.LogError("Standalone package player spawn contract failed: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	private static bool IsTeamTwoPlayerMarker(string markerName)
	{
		string text = markerName ?? string.Empty;
		if (!text.StartsWith("Team2_Spawn_", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("Team2_Backup_Spawn_", StringComparison.OrdinalIgnoreCase))
		{
			return text.StartsWith("PVP_Team2Spawn_", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static void RestoreStandalonePlayerSpawnContract(ActiveMapOperation operation)
	{
		if (operation == null || !operation.SpawnContractInstalled)
		{
			return;
		}
		try
		{
			if (SameNativeSpawnList(GameManager.SpawnPointsInScene, operation.OwnedSpawnPoints))
			{
				GameManager.SpawnPointsInScene = (IsUsableSpawnList(operation.PreviousSpawnPoints) ? operation.PreviousSpawnPoints : new Il2CppSystem.Collections.Generic.List<SpawnPoint>());
			}
			GameManager gameManager = GameManager.instance;
			if (gameManager != null && SameNativeGameObjectArray(gameManager.Pspawns, operation.OwnedFallbackSpawns))
			{
				gameManager.Pspawns = (IsUsableSpawnArray(operation.PreviousFallbackSpawns) ? operation.PreviousFallbackSpawns : new Il2CppReferenceArray<GameObject>(0L));
				gameManager.PnextSpawnIndex = Math.Max(0, operation.PreviousNextSpawnIndex);
			}
			if (gameManager != null && operation.RandomSpawnsCaptured && gameManager.RandomSpawns == operation.OwnedRandomSpawns)
			{
				gameManager.RandomSpawns = operation.PreviousRandomSpawns;
			}
		}
		catch
		{
			try
			{
				GameManager.SpawnPointsInScene = new Il2CppSystem.Collections.Generic.List<SpawnPoint>();
				if (GameManager.instance != null)
				{
					GameManager.instance.Pspawns = new Il2CppReferenceArray<GameObject>(0L);
					GameManager.instance.PnextSpawnIndex = 0;
					GameManager.instance.RandomSpawns = false;
				}
			}
			catch
			{
			}
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

	private static bool SameNativeSpawnList(Il2CppSystem.Collections.Generic.List<SpawnPoint> left, Il2CppSystem.Collections.Generic.List<SpawnPoint> right)
	{
		if (left == right)
		{
			return true;
		}
		try
		{
			return left != null && right != null && left.Pointer == right.Pointer;
		}
		catch
		{
			return false;
		}
	}

	private static bool SameNativeGameObjectArray(Il2CppReferenceArray<GameObject> left, Il2CppReferenceArray<GameObject> right)
	{
		if (left == right)
		{
			return true;
		}
		try
		{
			return left != null && right != null && left.Pointer == right.Pointer;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsUsableSpawnList(Il2CppSystem.Collections.Generic.List<SpawnPoint> spawns)
	{
		if (spawns == null)
		{
			return false;
		}
		try
		{
			for (int i = 0; i < spawns.Count; i++)
			{
				SpawnPoint spawnPoint = spawns[i];
				if (spawnPoint == null || spawnPoint.gameObject == null || !spawnPoint.gameObject.scene.IsValid() || !spawnPoint.gameObject.scene.isLoaded)
				{
					return false;
				}
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsUsableSpawnArray(Il2CppReferenceArray<GameObject> spawns)
	{
		if (spawns == null)
		{
			return false;
		}
		try
		{
			for (int i = 0; i < spawns.Length; i++)
			{
				GameObject gameObject = spawns[i];
				if (gameObject == null || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
				{
					return false;
				}
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool ValidateStandaloneSceneContract(Scene scene, ActiveMapOperation operation, out string error)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Invalid comparison between Unknown and I4
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Invalid comparison between Unknown and I4
		error = string.Empty;
		if (!HasExactSceneMarker(scene, "MAP_ID_" + operation.Map.Id))
		{
			error = "missing exact MAP_ID_ metadata marker";
			return false;
		}
		if (!HasExactSceneMarker(scene, "SPAWN_SET_" + operation.Operation.SpawnSetId))
		{
			error = "missing declared SPAWN_SET_ metadata marker";
			return false;
		}
		System.Collections.Generic.List<Transform> list = FindStandalonePlayerMarkers(scene, operation.Operation.Mode);
		if (list.Count == 0)
		{
			error = "no compatible player spawn markers";
			return false;
		}
		if ((int)operation.Operation.Mode == 2 && FindSceneMarkers(scene, "PVE_EnemySpawn_").Count == 0)
		{
			error = "PVE mode has no PVE_EnemySpawn_ markers";
			return false;
		}
		if ((int)operation.Operation.Mode == 1)
		{
			bool flag = list.Any((Transform marker) => marker.name.StartsWith("Team1", StringComparison.OrdinalIgnoreCase) || marker.name.StartsWith("PVP_Team1", StringComparison.OrdinalIgnoreCase));
			bool flag2 = list.Any((Transform marker) => marker.name.StartsWith("Team2", StringComparison.OrdinalIgnoreCase) || marker.name.StartsWith("PVP_Team2", StringComparison.OrdinalIgnoreCase));
			if (!flag || !flag2)
			{
				error = "PVP mode requires separated Team1 and Team2 spawn markers";
				return false;
			}
		}
		bool flag3 = false;
		foreach (GameObject rootGameObject in scene.GetRootGameObjects())
		{
			if (rootGameObject.GetComponentsInChildren<Light>(includeInactive: true).Any((Light light) => light != null && light.type == LightType.Directional))
			{
				flag3 = true;
				break;
			}
		}
		if (!flag3)
		{
			error = "no package-owned fallback directional light";
			return false;
		}
		return true;
	}

	private static bool HasExactSceneMarker(Scene scene, string name)
	{
		foreach (GameObject rootGameObject in scene.GetRootGameObjects())
		{
			foreach (Transform componentsInChild in rootGameObject.GetComponentsInChildren<Transform>(includeInactive: true))
			{
				if (componentsInChild != null && string.Equals(componentsInChild.name, name, StringComparison.Ordinal))
				{
					return true;
				}
			}
		}
		return false;
	}

	private void CreateStandaloneGameplayBootstrap(Scene scene, ActiveMapOperation operation)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Invalid comparison between Unknown and I4
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Invalid comparison between Unknown and I4
		if (operation == null || operation.BootstrapCreated)
		{
			return;
		}
		try
		{
			GameObject gameObject = new GameObject("MODDED_OPERATIONS_GAME_MODE_TEMPLATE");
			gameObject.SetActive(value: false);
			SceneManager.MoveGameObjectToScene(gameObject, scene);
			NetworkIdentity networkIdentity = gameObject.AddComponent<NetworkIdentity>();
			GameMode gameMode;
			uint num;
			if ((int)operation.Operation.Mode == 2)
			{
				StandalonePveGameMode standalonePveGameMode = gameObject.AddComponent<StandalonePveGameMode>();
				standalonePveGameMode.enabled = false;
				standalonePveGameMode.AllowRespawns = false;
				standalonePveGameMode.NetworkRaidTimer = 0f;
				InfiltrationManager.instance = standalonePveGameMode;
				gameMode = standalonePveGameMode;
				num = 1297043457u;
			}
			else
			{
				StandalonePvpGameMode standalonePvpGameMode = gameObject.AddComponent<StandalonePvpGameMode>();
				ConfigureStandalonePvpController(scene, operation, gameObject, standalonePvpGameMode);
				gameMode = standalonePvpGameMode;
				num = 1297043458u;
			}
			gameMode.isNight = ParseTimeHour(operation.TimeCode) < 6;
			GameMode.singleton = gameMode;
			operation.BootstrapRoot = gameObject;
			operation.BootstrapIdentity = networkIdentity;
			operation.BootstrapPrefabRoot = gameObject;
			operation.BootstrapPrefabIdentity = networkIdentity;
			operation.BootstrapAssetId = num;
			operation.GameModeComponent = gameMode;
			operation.BootstrapCreated = true;
			operation.BootstrapFrame = Time.frameCount;
			networkIdentity.assetId = num;
			EnsureStandaloneBootstrapPrefabRegistered(operation);
			if (OperationsManager.singleton != null)
			{
				if ((int)operation.Operation.Mode == 2)
				{
					OperationsManager.singleton.AssignTeamsPVE();
				}
				else
				{
					OperationsManager.singleton.AssignTeamsTDM();
				}
			}
			log.LogInfo("Standalone gameplay bootstrap created in package scene: map=" + operation.Map.Id + ", mode=" + ((object)operation.Operation.Mode/*cast due to constrained. prefix*/).ToString() + ", owner=" + gameMode.GetType().Name + ", mirrorAssetId=0x" + num.ToString("X8") + ", prefabRegistered=" + operation.BootstrapPrefabRegistered + ", donorScene=false, sceneHandle=" + scene.handle.ToString() + ".");
		}
		catch (Exception ex)
		{
			log.LogError("Standalone gameplay bootstrap failed: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	private void EnsureStandaloneBootstrapPrefabRegistered(ActiveMapOperation operation)
	{
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		if (operation == null || operation.BootstrapPrefabRegistered || !NetworkClient.active || operation.BootstrapPrefabRoot == null || operation.BootstrapPrefabIdentity == null || operation.BootstrapAssetId == 0)
		{
			return;
		}
		Il2CppSystem.Collections.Generic.Dictionary<uint, GameObject> prefabs = NetworkClient.prefabs;
		if (prefabs != null && prefabs.TryGetValue(operation.BootstrapAssetId, out var value))
		{
			if (!(value == null))
			{
				if (value == operation.BootstrapPrefabRoot)
				{
					operation.BootstrapPrefabRegistered = true;
					operation.BootstrapPrefabIdentity.assetId = operation.BootstrapAssetId;
					return;
				}
				throw new InvalidOperationException("Mirror prefab asset ID collision for standalone game mode 0x" + operation.BootstrapAssetId.ToString("X8") + ": existing=" + value.name + ".");
			}
			prefabs.Remove(operation.BootstrapAssetId);
			NetworkClient.UnregisterSpawnHandler(operation.BootstrapAssetId);
			instance?.log?.LogInfo("Removed destroyed standalone game-mode Mirror prefab before repeat registration: assetId=0x" + operation.BootstrapAssetId.ToString("X8") + ".");
		}
		NetworkClient.RegisterPrefab(operation.BootstrapPrefabRoot, operation.BootstrapAssetId);
		operation.BootstrapPrefabIdentity.assetId = operation.BootstrapAssetId;
		operation.BootstrapPrefabRegistered = true;
		log.LogInfo("Standalone game-mode Mirror prefab registered on this peer: assetId=0x" + operation.BootstrapAssetId.ToString("X8") + ", mode=" + ((object)operation.Operation.Mode/*cast due to constrained. prefix*/).ToString() + ".");
	}

	private void ConfigureStandalonePvpController(Scene scene, ActiveMapOperation operation, GameObject bootstrapRoot, StandalonePvpGameMode pvp)
	{
		if (operation == null || bootstrapRoot == null || pvp == null)
		{
			throw new ArgumentNullException("pvp");
		}
		Il2CppSystem.Collections.Generic.List<SpawnPoint> list = new Il2CppSystem.Collections.Generic.List<SpawnPoint>();
		Il2CppSystem.Collections.Generic.List<SpawnPoint> list2 = new Il2CppSystem.Collections.Generic.List<SpawnPoint>();
		foreach (Transform item in FindStandalonePlayerMarkers(scene, (ModdedOperationMode)1))
		{
			if (!(item == null))
			{
				SpawnPoint spawnPoint = item.GetComponent<SpawnPoint>() ?? item.gameObject.AddComponent<SpawnPoint>();
				bool flag = IsTeamTwoPlayerMarker(item.name);
				spawnPoint.CanSpawnPlayer = true;
				spawnPoint.Team = ((!flag) ? 1 : 2);
				if (flag)
				{
					list2.Add(spawnPoint);
				}
				else
				{
					list.Add(spawnPoint);
				}
			}
		}
		if (list.Count == 0 || list2.Count == 0)
		{
			throw new InvalidOperationException("PvpGameode requires non-empty Team1SpawnPoints and Team2SpawnPoints lists");
		}
		pvp.Team1SpawnPoints = list;
		pvp.Team2SpawnPoints = list2;
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
		log.LogInfo("Standalone StandardPVP owner wired to shipped PvpGameode: team1Spawns=" + list.Count + ", team2Spawns=" + list2.Count + ", MaxRounds=13, RoundsToWin=7, RoundTime=120.");
	}

	private static void ConfigureStandalonePvpPresentation(ActiveMapOperation operation, GameObject bootstrapRoot, StandalonePvpGameMode pvp)
	{
		AudioSource audioSource = bootstrapRoot.AddComponent<AudioSource>();
		audioSource.playOnAwake = false;
		audioSource.loop = false;
		AudioSource audioSource2 = bootstrapRoot.AddComponent<AudioSource>();
		audioSource2.playOnAwake = false;
		audioSource2.loop = false;
		pvp.MusicSource = audioSource;
		pvp.AnnouncerSource = audioSource2;
		AudioClip audioClip = AudioClip.Create("MODDED_PVP_SILENT_ANNOUNCER", 1, 1, 48000, stream: false);
		operation.RuntimePvpAssets.Add(audioClip);
		pvp.bluforSpawn = CreatePvpClipArray(audioClip, 3);
		pvp.bluforSpawnShort = CreatePvpClipArray(audioClip, 3);
		pvp.bluforRoundWin = CreatePvpClipArray(audioClip, 3);
		pvp.bluforGameWin = CreatePvpClipArray(audioClip, 3);
		pvp.bluforGameLose = CreatePvpClipArray(audioClip, 3);
		pvp.bluforRoundLose = CreatePvpClipArray(audioClip, 3);
		pvp.bluforGameDraw = CreatePvpClipArray(audioClip, 1);
		pvp.bluforRoundDraw = CreatePvpClipArray(audioClip, 1);
		pvp.opforSpawn = CreatePvpClipArray(audioClip, 3);
		pvp.opforSpawnShort = CreatePvpClipArray(audioClip, 3);
		pvp.opforRoundWin = CreatePvpClipArray(audioClip, 3);
		pvp.opforGameWin = CreatePvpClipArray(audioClip, 3);
		pvp.opforGameLose = CreatePvpClipArray(audioClip, 3);
		pvp.opforRoundLose = CreatePvpClipArray(audioClip, 3);
		pvp.opforGameDraw = CreatePvpClipArray(audioClip, 1);
		pvp.opforRoundDraw = CreatePvpClipArray(audioClip, 1);
		GameObject gameObject = new GameObject("MODDED_PVP_NATIVE_UI");
		gameObject.layer = 5;
		gameObject.transform.SetParent(bootstrapRoot.transform, worldPositionStays: false);
		Canvas canvas = gameObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 160;
		CanvasScaler canvasScaler = gameObject.AddComponent<CanvasScaler>();
		canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
		canvasScaler.matchWidthOrHeight = 0.5f;
		TextMeshProUGUI textMeshProUGUI = CreatePvpLabel(gameObject.transform, "BLUFOR_SCORE", "0", new Vector2(0.42f, 0.94f), new Vector2(0.48f, 0.99f), 28f);
		TextMeshProUGUI clock = CreatePvpLabel(gameObject.transform, "ROUND_CLOCK", "02:00", new Vector2(0.48f, 0.94f), new Vector2(0.52f, 0.99f), 28f);
		TextMeshProUGUI textMeshProUGUI2 = CreatePvpLabel(gameObject.transform, "OPFOR_SCORE", "0", new Vector2(0.52f, 0.94f), new Vector2(0.58f, 0.99f), 28f);
		pvp.bluforScore = textMeshProUGUI;
		pvp.opforScore = textMeshProUGUI2;
		pvp.clock = clock;
		pvp.GameUI_bluforScore = textMeshProUGUI;
		pvp.GameUI_opforScore = textMeshProUGUI2;
		TextMeshProUGUI textMeshProUGUI3 = CreatePvpLabel(gameObject.transform, "ROUND_CAUSE", string.Empty, new Vector2(0.25f, 0.38f), new Vector2(0.75f, 0.48f), 30f);
		TeleType teleType = textMeshProUGUI3.gameObject.AddComponent<TeleType>();
		teleType.m_textMeshPro = textMeshProUGUI3;
		teleType.autoReveal = false;
		teleType.label01 = string.Empty;
		teleType.label02 = string.Empty;
		pvp.Cause = teleType;
		GameObject gameObject2 = CreatePvpUiGroup(gameObject.transform, "ROUND_END_UI");
		pvp.RoundEndAnimator = gameObject2.AddComponent<Animator>();
		pvp.RoundWinImage = CreatePvpOutcome(gameObject2.transform, "ROUND_WIN", "ROUND WON");
		pvp.RoundLoseImage = CreatePvpOutcome(gameObject2.transform, "ROUND_LOSS", "ROUND LOST");
		pvp.RoundDrawImage = CreatePvpOutcome(gameObject2.transform, "ROUND_DRAW", "ROUND DRAW");
		GameObject gameObject3 = CreatePvpUiGroup(gameObject.transform, "GAME_END_UI");
		pvp.GameUiAnimator = gameObject3.AddComponent<Animator>();
		pvp.GameWinImage = CreatePvpOutcome(gameObject3.transform, "GAME_VICTORY", "VICTORY");
		pvp.GameLoseImage = CreatePvpOutcome(gameObject3.transform, "GAME_DEFEAT", "DEFEAT");
		pvp.GameDrawImage = CreatePvpOutcome(gameObject3.transform, "GAME_DRAW", "GAME DRAW");
		pvp.GameUI_Winning = CreatePvpLabel(gameObject3.transform, "GAME_UI_WINNING", "WINNING", new Vector2(0.35f, 0.8f), new Vector2(0.65f, 0.87f), 26f);
		pvp.GameUI_Losing = CreatePvpLabel(gameObject3.transform, "GAME_UI_LOSING", "LOSING", new Vector2(0.35f, 0.8f), new Vector2(0.65f, 0.87f), 26f);
		pvp.GameUI_Tie = CreatePvpLabel(gameObject3.transform, "GAME_UI_TIE", "TIED", new Vector2(0.35f, 0.8f), new Vector2(0.65f, 0.87f), 26f);
		pvp.GameUI_Winning.gameObject.SetActive(value: false);
		pvp.GameUI_Losing.gameObject.SetActive(value: false);
		pvp.GameUI_Tie.gameObject.SetActive(value: false);
		pvp.FadeOut = "FadeOut";
		pvp.FadeIn = "FadeIn";
	}

	private static Il2CppReferenceArray<AudioClip> CreatePvpClipArray(AudioClip clip, int count)
	{
		Il2CppReferenceArray<AudioClip> il2CppReferenceArray = new Il2CppReferenceArray<AudioClip>(count);
		for (int i = 0; i < count; i++)
		{
			il2CppReferenceArray[i] = clip;
		}
		return il2CppReferenceArray;
	}

	private static GameObject CreatePvpUiGroup(Transform parent, string name)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.layer = 5;
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		return gameObject;
	}

	private static GameObject CreatePvpOutcome(Transform parent, string name, string text)
	{
		GameObject gameObject = CreatePvpUiGroup(parent, name);
		CreatePvpLabel(gameObject.transform, name + "_TEXT", text, new Vector2(0.3f, 0.5f), new Vector2(0.7f, 0.62f), 48f);
		gameObject.SetActive(value: false);
		return gameObject;
	}

	private static TextMeshProUGUI CreatePvpLabel(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, float fontSize)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.layer = 5;
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		TextMeshProUGUI textMeshProUGUI = gameObject.AddComponent<TextMeshProUGUI>();
		textMeshProUGUI.text = text ?? string.Empty;
		textMeshProUGUI.fontSize = fontSize;
		textMeshProUGUI.alignment = TextAlignmentOptions.Center;
		textMeshProUGUI.color = Color.white;
		textMeshProUGUI.raycastTarget = false;
		textMeshProUGUI.enableWordWrapping = false;
		RectTransform rectTransform = textMeshProUGUI.rectTransform;
		rectTransform.anchorMin = anchorMin;
		rectTransform.anchorMax = anchorMax;
		rectTransform.offsetMin = Vector2.zero;
		rectTransform.offsetMax = Vector2.zero;
		rectTransform.localScale = Vector3.one;
		return textMeshProUGUI;
	}

	private static int ParseTimeHour(string timeCode)
	{
		if (string.IsNullOrEmpty(timeCode) || timeCode.Length != 4 || !int.TryParse(timeCode.Substring(0, 2), out var result))
		{
			return 11;
		}
		return result;
	}

	private void ApplyStandaloneRenderContract(Scene scene, ActiveMapOperation operation)
	{
		if (operation == null || !scene.IsValid() || !scene.isLoaded)
		{
			return;
		}
		if (!HasExactSceneMarker(scene, "RENDER_PROFILE_NATIVE_OUTDOOR_V1"))
		{
			ApplySceneAuthoredRenderContract(scene, operation);
			return;
		}
		try
		{
			Light light = null;
			foreach (GameObject rootGameObject in scene.GetRootGameObjects())
			{
				foreach (Light componentsInChild in rootGameObject.GetComponentsInChildren<Light>(includeInactive: true))
				{
					if (componentsInChild != null && componentsInChild.type == LightType.Directional)
					{
						light = componentsInChild;
						break;
					}
				}
				if (light != null)
				{
					break;
				}
			}
			if (light == null)
			{
				GameObject gameObject = new GameObject("MODDED_OPERATIONS_DIRECTIONAL_LIGHT");
				SceneManager.MoveGameObjectToScene(gameObject, scene);
				light = gameObject.AddComponent<Light>();
				light.type = LightType.Directional;
			}
			bool flag = ParseTimeHour(operation.TimeCode) < 6;
			if (flag && GameManager.instance != null)
			{
				if (!operation.NvgColorCaptured)
				{
					operation.PreviousNvgColor = GameManager.instance.NVGColor;
					operation.NvgColorCaptured = true;
				}
				GameManager.instance.SetNVGColor(0);
				operation.WhitePhosphorApplied = true;
			}
			light.transform.rotation = (flag ? new Quaternion(0.1168685f, 0.6386099f, -0.6774567f, 0.3457913f) : new Quaternion(0.115319036f, 0.019461127f, 0.11803713f, 0.9860984f));
			light.color = Color.white;
			light.useColorTemperature = true;
			light.colorTemperature = (flag ? 9754f : 5500f);
			light.intensity = (flag ? 40f : 30000f);
			light.bounceIntensity = (flag ? 1f : 5f);
			light.shadows = LightShadows.Soft;
			light.shadowResolution = LightShadowResolution.VeryHigh;
			light.shadowStrength = 1f;
			light.shadowBias = 0.05f;
			light.shadowNormalBias = 0.4f;
			light.shadowNearPlane = 0.2f;
			HDAdditionalLightData obj = light.GetComponent<HDAdditionalLightData>() ?? light.gameObject.AddComponent<HDAdditionalLightData>();
			obj.lightUnit = LightUnit.Lux;
			obj.intensity = light.intensity;
			obj.volumetricDimmer = 1f;
			obj.angularDiameter = 0.5f;
			if (flag)
			{
				GameObject gameObject2 = new GameObject("MODDED_OPERATIONS_NIGHT_AMBIENT");
				SceneManager.MoveGameObjectToScene(gameObject2, scene);
				gameObject2.transform.rotation = new Quaternion(0.96095735f, 0.14374454f, 0.22723518f, -0.06528974f);
				Light light2 = gameObject2.AddComponent<Light>();
				light2.type = LightType.Directional;
				light2.color = Color.white;
				light2.useColorTemperature = true;
				light2.colorTemperature = 6570f;
				light2.intensity = 3500f;
				light2.bounceIntensity = 1f;
				light2.shadows = LightShadows.None;
				HDAdditionalLightData hDAdditionalLightData = gameObject2.AddComponent<HDAdditionalLightData>();
				hDAdditionalLightData.lightUnit = LightUnit.Lux;
				hDAdditionalLightData.intensity = 3500f;
				hDAdditionalLightData.volumetricDimmer = 1f;
			}
			GameObject gameObject3 = new GameObject("MODDED_OPERATIONS_OUTDOOR_ENVIRONMENT");
			SceneManager.MoveGameObjectToScene(gameObject3, scene);
			Volume volume = gameObject3.AddComponent<Volume>();
			volume.isGlobal = true;
			volume.priority = 500000f;
			volume.weight = 1f;
			VolumeProfile volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
			volumeProfile.name = "MODDED_OPERATIONS_OUTDOOR_PROFILE";
			volume.sharedProfile = volumeProfile;
			operation.RuntimeRenderProfiles.Add(volumeProfile);
			VisualEnvironment visualEnvironment = volumeProfile.Add<VisualEnvironment>(overrides: true);
			visualEnvironment.skyType.Override(4);
			visualEnvironment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
			visualEnvironment.renderingSpace.Override(RenderingSpace.Camera);
			visualEnvironment.windOrientation.Override(18f);
			visualEnvironment.windSpeed.Override(14f);
			PhysicallyBasedSky physicallyBasedSky = volumeProfile.Add<PhysicallyBasedSky>(overrides: true);
			physicallyBasedSky.type.Override(PhysicallyBasedSkyModel.EarthSimple);
			physicallyBasedSky.atmosphericScattering.Override(x: true);
			physicallyBasedSky.aerosolDensity.Override(0.18f);
			physicallyBasedSky.groundTint.Override(new Color(0.13f, 0.105f, 0.075f, 1f));
			physicallyBasedSky.horizonTint.Override(new Color(0.96f, 0.98f, 1f, 1f));
			physicallyBasedSky.zenithTint.Override(new Color(0.84f, 0.91f, 1f, 1f));
			Exposure exposure = volumeProfile.Add<Exposure>();
			exposure.mode.Override(ExposureMode.AutomaticHistogram);
			exposure.meteringMode.Override(flag ? MeteringMode.ProceduralMask : MeteringMode.CenterWeighted);
			exposure.fixedExposure.Override(flag ? 8.32f : 10f);
			exposure.compensation.Override(flag ? 1.16f : 0f);
			exposure.limitMin.Override(flag ? 5.065282f : 8.5f);
			exposure.limitMax.Override(flag ? 9.348571f : 11f);
			exposure.adaptationSpeedDarkToLight.Override(flag ? 3f : 0.5f);
			exposure.adaptationSpeedLightToDark.Override(flag ? 3f : 0.5f);
			Tonemapping tonemapping = volumeProfile.Add<Tonemapping>();
			Texture3D texture3D = (flag ? null : LoadPackageTonemapLut(operation.Map));
			if (flag)
			{
				tonemapping.mode.Override(TonemappingMode.ACES);
				tonemapping.useFullACES.Override(x: true);
			}
			else if (texture3D != null)
			{
				tonemapping.mode.Override(TonemappingMode.External);
				tonemapping.useFullACES.Override(x: false);
				tonemapping.lutTexture.Override(texture3D);
			}
			else
			{
				tonemapping.mode.Override(TonemappingMode.ACES);
				tonemapping.useFullACES.Override(x: true);
			}
			Bloom bloom = volumeProfile.Add<Bloom>();
			bloom.quality.Override(flag ? 1 : 3);
			bloom.intensity.Override(flag ? 0.3f : 0.03f);
			bloom.threshold.Override(0.9f);
			bloom.scatter.Override(flag ? 0.2f : 0.893f);
			bloom.anamorphic.Override(x: false);
			if (flag)
			{
				bloom.m_Resolution.Override(BloomResolution.Half);
			}
			ScreenSpaceLensFlare screenSpaceLensFlare = volumeProfile.Add<ScreenSpaceLensFlare>();
			screenSpaceLensFlare.intensity.Override(flag ? 1f : 0.5f);
			screenSpaceLensFlare.streaksIntensity.Override(flag ? 1f : 1.55f);
			screenSpaceLensFlare.streaksLength.Override(flag ? 0.091f : 0.022f);
			if (!flag)
			{
				screenSpaceLensFlare.streaksOrientation.Override(0f);
				screenSpaceLensFlare.chromaticAbberationIntensity.Override(0.6f);
			}
			ColorAdjustments colorAdjustments = volumeProfile.Add<ColorAdjustments>();
			colorAdjustments.postExposure.Override(flag ? 0f : (-0.3f));
			colorAdjustments.contrast.Override(flag ? 17.3f : 30f);
			colorAdjustments.saturation.Override(flag ? 22f : (-15f));
			if (!flag)
			{
				WhiteBalance whiteBalance = volumeProfile.Add<WhiteBalance>();
				whiteBalance.temperature.Override(-3.6f);
				whiteBalance.tint.Override(-8.6f);
				LiftGammaGain liftGammaGain = volumeProfile.Add<LiftGammaGain>();
				liftGammaGain.lift.Override(new Vector4(1f, 1f, 1f, 0.00827304f));
				liftGammaGain.gamma.Override(new Vector4(1f, 1f, 1f, -0.09100296f));
				liftGammaGain.gain.Override(new Vector4(1f, 1f, 1f, 0.09100296f));
			}
			if (flag)
			{
				IndirectLightingController indirectLightingController = volumeProfile.Add<IndirectLightingController>();
				indirectLightingController.indirectDiffuseLightingMultiplier.Override(1f);
				indirectLightingController.reflectionLightingMultiplier.Override(1f);
				indirectLightingController.reflectionProbeIntensityMultiplier.Override(1f);
			}
			HDShadowSettings hDShadowSettings = volumeProfile.Add<HDShadowSettings>();
			hDShadowSettings.maxShadowDistance.Override(flag ? 200f : 125f);
			if (flag)
			{
				hDShadowSettings.cascadeShadowSplit0.Override(0.05f);
			}
			log.LogInfo("Standalone render contract applied from package-owned scene plus its explicit native-outdoor-v1 profile marker: time=" + operation.TimeCode + ", sunLux=" + light.intensity + ", sunTemperature=" + light.colorTemperature + ", sunBounce=" + light.bounceIntensity + ", profileSource=" + (flag ? "PVP-map night" : "PVP Woods Warehouse day") + ", bloom=" + (flag ? 0.3f : 0.03f) + ", lensFlare=" + (flag ? 1f : 0.5f) + ", nightAmbient=" + flag + ", whitePhosphor=" + operation.WhitePhosphorApplied + ", externalLut=" + (texture3D != null) + ".");
		}
		catch (Exception ex)
		{
			log.LogWarning("Standalone HDRP render contract fell back to the scene-authored light: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	private void ApplySceneAuthoredRenderContract(Scene scene, ActiveMapOperation operation)
	{
		try
		{
			Texture3D texture3D = LoadPackageTonemapLut(operation.Map);
			if (texture3D != null)
			{
				GameObject gameObject = new GameObject("MODDED_OPERATIONS_PACKAGE_EXTERNAL_TONEMAP");
				SceneManager.MoveGameObjectToScene(gameObject, scene);
				Volume volume = gameObject.AddComponent<Volume>();
				volume.isGlobal = true;
				volume.priority = 500000f;
				volume.weight = 1f;
				VolumeProfile volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
				volumeProfile.name = "MODDED_OPERATIONS_PACKAGE_TONEMAP_PROFILE";
				volume.sharedProfile = volumeProfile;
				operation.RuntimeRenderProfiles.Add(volumeProfile);
				Tonemapping tonemapping = volumeProfile.Add<Tonemapping>();
				tonemapping.mode.Override(TonemappingMode.External);
				tonemapping.useFullACES.Override(x: true);
				tonemapping.lutTexture.Override(texture3D);
			}
			log.LogInfo("Standalone render contract retained package scene lighting: map=" + operation.Map.Id + ", time=" + operation.TimeCode + ", externalLut=" + (texture3D != null) + ", adapterPreset=false.");
		}
		catch (Exception ex)
		{
			log.LogWarning("Package scene lighting was retained, but its optional external tonemap LUT could not be applied: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	private Texture3D LoadPackageTonemapLut(ModdedMapDefinition map)
	{
		if (map == null || map.ExternalTonemapLut == null)
		{
			return null;
		}
		if (packageTonemapLuts.TryGetValue(map.Id, out var value) && value != null)
		{
			return value;
		}
		try
		{
			ModdedExternalTonemapLutDefinition externalTonemapLut = map.ExternalTonemapLut;
			byte[] array = File.ReadAllBytes(externalTonemapLut.VerifiedPath);
			long num = (long)externalTonemapLut.Dimension * (long)externalTonemapLut.Dimension * externalTonemapLut.Dimension * 8;
			if (array.LongLength != num)
			{
				throw new InvalidDataException("verified LUT length changed after catalog freeze");
			}
			Texture3D texture3D = new Texture3D(externalTonemapLut.Dimension, externalTonemapLut.Dimension, externalTonemapLut.Dimension, TextureFormat.RGBAHalf, mipChain: false)
			{
				name = "PACKAGE_EXTERNAL_TONEMAP_LUT_" + map.Id,
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear,
				anisoLevel = 0
			};
			Il2CppStructArray<byte> data = new Il2CppStructArray<byte>(array);
			texture3D.SetPixelData(data, 0);
			texture3D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			packageTonemapLuts[map.Id] = texture3D;
			return texture3D;
		}
		catch (Exception ex)
		{
			string item = map.Id + "|" + ex.GetType().FullName + "|" + ex.Message;
			if (lutDiagnostics.Add(item))
			{
				log.LogWarning("Package tonemap LUT reconstruction failed for map=" + map.Id + ": " + ex.GetType().Name + ": " + ex.Message);
			}
		}
		return null;
	}

	private bool TryClaimStandaloneReadinessInitialization(GameMode gameMode)
	{
		ActiveMapOperation activeMapOperation = activeOperation;
		if (activeMapOperation == null || gameMode == null)
		{
			return false;
		}
		if (activeMapOperation.GameModeComponent != gameMode && !TryAdoptNetworkSpawnedGameMode(activeMapOperation, gameMode))
		{
			return false;
		}
		if (activeMapOperation.ReadinessInitializationClaimed)
		{
			return false;
		}
		activeMapOperation.ReadinessInitializationClaimed = true;
		return true;
	}

	private bool TryAdoptNetworkSpawnedGameMode(ActiveMapOperation operation, GameMode gameMode)
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Invalid comparison between Unknown and I4
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		if (operation == null || gameMode == null || operation.BootstrapAssetId == 0 || gameMode.gameObject == null)
		{
			return false;
		}
		NetworkIdentity component = gameMode.GetComponent<NetworkIdentity>();
		if (component == null || component.assetId != operation.BootstrapAssetId)
		{
			return false;
		}
		if (!(((int)operation.Operation.Mode == 2) ? (gameMode is StandalonePveGameMode) : (gameMode is StandalonePvpGameMode)))
		{
			return false;
		}
		operation.BootstrapRoot = gameMode.gameObject;
		operation.BootstrapIdentity = component;
		operation.GameModeComponent = gameMode;
		GameMode.singleton = gameMode;
		if (gameMode is StandalonePveGameMode standalonePveGameMode)
		{
			InfiltrationManager.instance = standalonePveGameMode;
		}
		if (gameMode is StandalonePvpGameMode standalonePvpGameMode)
		{
			PvpGameode.instance = standalonePvpGameMode;
		}
		log.LogInfo("Standalone game-mode Mirror spawn adopted on this peer: assetId=0x" + operation.BootstrapAssetId.ToString("X8") + ", netId=" + component.netId + ", mode=" + ((object)operation.Operation.Mode/*cast due to constrained. prefix*/).ToString() + ".");
		return true;
	}

	private void MarkStandaloneReadinessInitialized(GameMode gameMode, string source)
	{
		ActiveMapOperation activeMapOperation = activeOperation;
		if (activeMapOperation != null && !(activeMapOperation.GameModeComponent != gameMode))
		{
			activeMapOperation.ReadinessInitialized = true;
			log.LogInfo("Standalone game mode entered the shipped readiness coroutines through GameMode.Initialize(): source=" + source + ".");
		}
	}

	private void MarkStandaloneReadinessInitializationFailed(GameMode gameMode, string source, Exception exception)
	{
		ActiveMapOperation activeMapOperation = activeOperation;
		if (activeMapOperation != null && !(activeMapOperation.GameModeComponent != gameMode))
		{
			activeMapOperation.ReadinessInitializationClaimed = false;
			activeMapOperation.ReadinessInitialized = false;
			log.LogError("Standalone readiness initialization failed closed: source=" + source + ", " + exception.GetType().Name + ": " + exception.Message);
		}
	}

	private void OnStandaloneAllPlayersLoaded(bool nativePvpLifecycle)
	{
		ActiveMapOperation activeMapOperation = activeOperation;
		if (activeMapOperation != null)
		{
			activeMapOperation.AllPlayersLoaded = true;
			activeMapOperation.NativePvpLifecycleActive = nativePvpLifecycle;
			activeMapOperation.AllPlayersLoadedFrame = Time.frameCount;
			log.LogInfo("Standalone game mode received the shipped all-players-loaded barrier for operation=" + activeMapOperation.Operation.Id + ", nativePvpLifecycle=" + nativePvpLifecycle + ".");
			if (!nativePvpLifecycle)
			{
				SpawnAndPositionStandalonePlayers(activeMapOperation, allowSpawnRequest: true);
			}
		}
	}

	private void OnStandalonePvpAllPlayersLoadedFailed(Exception exception)
	{
		ActiveMapOperation activeMapOperation = activeOperation;
		if (activeMapOperation != null)
		{
			activeMapOperation.AllPlayersLoaded = true;
			activeMapOperation.NativePvpLifecycleActive = false;
			activeMapOperation.AllPlayersLoadedFrame = Time.frameCount;
			log.LogError("Shipped PvpGameode.Server_AllPlayersLoaded failed; using the bounded position-only fallback for this session: " + exception.GetType().Name + ": " + exception.Message);
			SpawnAndPositionStandalonePlayers(activeMapOperation, allowSpawnRequest: true);
		}
	}

	private void MaintainStandaloneGameplay()
	{
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Invalid comparison between Unknown and I4
		ActiveMapOperation activeMapOperation = activeOperation;
		if (activeMapOperation == null || activeMapOperation.SceneHandle == 0)
		{
			return;
		}
		StandalonePveGameMode standalonePveGameMode = activeMapOperation.GameModeComponent as StandalonePveGameMode;
		if (NetworkServer.active && activeMapOperation.AllPlayersLoaded && standalonePveGameMode != null)
		{
			standalonePveGameMode.NetworkRaidTimer += Time.deltaTime;
		}
		if (!activeMapOperation.ScenePreparationComplete && !activeMapOperation.ScenePreparationStarted && Time.frameCount >= activeMapOperation.ScenePreparationEarliestFrame)
		{
			activeMapOperation.ScenePreparationStarted = true;
			Scene scene = FindLoadedSceneByHandle(activeMapOperation.SceneHandle);
			PrepareStandaloneScene(scene, activeMapOperation);
		}
		if (!activeMapOperation.ScenePreparationComplete || !activeMapOperation.TerrainReady || activeMapOperation.BootstrapRoot == null || Time.frameCount < activeMapOperation.LastMaintenanceFrame + 15)
		{
			return;
		}
		activeMapOperation.LastMaintenanceFrame = Time.frameCount;
		try
		{
			EnsureStandaloneBootstrapPrefabRegistered(activeMapOperation);
		}
		catch (Exception ex)
		{
			log.LogError("Standalone game-mode Mirror prefab registration failed closed: " + ex.GetType().Name + ": " + ex.Message);
			return;
		}
		if (NetworkServer.active && !activeMapOperation.NetworkSpawnRequested && activeMapOperation.BootstrapIdentity != null && activeMapOperation.BootstrapAssetId != 0)
		{
			try
			{
				activeMapOperation.BootstrapRoot.SetActive(value: true);
				NetworkServer.Spawn(activeMapOperation.BootstrapRoot, activeMapOperation.BootstrapAssetId);
				activeMapOperation.NetworkSpawnRequested = true;
				log.LogInfo("Standalone game mode network identity spawned by the host: assetId=0x" + activeMapOperation.BootstrapAssetId.ToString("X8") + ".");
			}
			catch (Exception ex2)
			{
				activeMapOperation.BootstrapRoot.SetActive(value: false);
				log.LogWarning("Standalone game mode network spawn is waiting: " + ex2.GetType().Name + ": " + ex2.Message);
			}
		}
		if (!NetworkServer.active)
		{
			return;
		}
		if (!activeMapOperation.ReadinessInitialized && activeMapOperation.NetworkSpawnRequested && NetworkClient.active && activeMapOperation.GameModeComponent != null && Time.frameCount >= activeMapOperation.BootstrapFrame + 30)
		{
			EnsureStandaloneReadiness(activeMapOperation.GameModeComponent, "bounded host fallback after network spawn");
		}
		if (activeMapOperation.AllPlayersLoaded)
		{
			if (!activeMapOperation.NativePvpLifecycleActive)
			{
				SpawnAndPositionStandalonePlayers(activeMapOperation, allowSpawnRequest: true);
			}
			if ((int)activeMapOperation.Operation.Mode == 2 && !activeMapOperation.PveSpawnAttempted && activeMapOperation.AllPlayersLoadedFrame >= 0 && Time.frameCount >= activeMapOperation.AllPlayersLoadedFrame + 180)
			{
				TrySpawnStandalonePveEnemies(activeMapOperation);
			}
		}
	}

	private static void EnsureStandaloneReadiness(GameMode gameMode, string source)
	{
		StandalonePveGameMode standalonePveGameMode = gameMode as StandalonePveGameMode;
		if (standalonePveGameMode != null)
		{
			standalonePveGameMode.EnsureStandaloneReadiness(source);
		}
		else
		{
			(gameMode as StandalonePvpGameMode)?.EnsureStandaloneReadiness(source);
		}
	}

	private static void ReleaseStandaloneSceneContracts(ActiveMapOperation operation)
	{
		if (operation != null)
		{
			RestoreStandalonePlayerSpawnContract(operation);
			ReleaseStandaloneRenderContract(operation);
			ReleaseStandaloneGameMode(operation);
		}
	}

	private static void ReleaseStandaloneRenderContract(ActiveMapOperation operation)
	{
		if (operation == null)
		{
			return;
		}
		if (operation.NvgColorCaptured)
		{
			try
			{
				if (GameManager.instance != null)
				{
					GameManager.instance.SetNVGColor(operation.PreviousNvgColor);
				}
			}
			catch
			{
			}
		}
		operation.NvgColorCaptured = false;
		operation.PreviousNvgColor = 0;
		operation.WhitePhosphorApplied = false;
		foreach (VolumeProfile runtimeRenderProfile in operation.RuntimeRenderProfiles)
		{
			if (runtimeRenderProfile != null)
			{
				UnityEngine.Object.Destroy(runtimeRenderProfile);
			}
		}
		operation.RuntimeRenderProfiles.Clear();
	}

	private static void ReleaseStandaloneGameMode(ActiveMapOperation operation)
	{
		if (operation == null)
		{
			return;
		}
		GameMode gameModeComponent = operation.GameModeComponent;
		if (gameModeComponent != null && InfiltrationManager.instance == gameModeComponent)
		{
			InfiltrationManager.instance = null;
		}
		if (gameModeComponent != null && PvpGameode.instance == gameModeComponent)
		{
			PvpGameode.instance = null;
		}
		if (gameModeComponent != null && GameMode.singleton == gameModeComponent)
		{
			GameMode.singleton = null;
		}
		uint bootstrapAssetId = operation.BootstrapAssetId;
		GameObject bootstrapPrefabRoot = operation.BootstrapPrefabRoot;
		if (bootstrapPrefabRoot != null)
		{
			try
			{
				NetworkClient.UnregisterPrefab(bootstrapPrefabRoot);
			}
			catch
			{
			}
		}
		if (bootstrapAssetId != 0)
		{
			try
			{
				NetworkClient.prefabs?.Remove(bootstrapAssetId);
				NetworkClient.UnregisterSpawnHandler(bootstrapAssetId);
			}
			catch
			{
			}
		}
		operation.BootstrapPrefabRegistered = false;
		operation.BootstrapAssetId = 0u;
		operation.BootstrapPrefabIdentity = null;
		operation.BootstrapPrefabRoot = null;
		foreach (UnityEngine.Object runtimePvpAsset in operation.RuntimePvpAssets)
		{
			if (runtimePvpAsset != null)
			{
				UnityEngine.Object.Destroy(runtimePvpAsset);
			}
		}
		operation.RuntimePvpAssets.Clear();
		if (operation.RaidUtilityRoot != null)
		{
			RaidManager component = operation.RaidUtilityRoot.GetComponent<RaidManager>();
			if (component != null && RaidManager.singleton == component)
			{
				RaidManager.singleton = null;
			}
			UnityEngine.Object.Destroy(operation.RaidUtilityRoot);
			operation.RaidUtilityRoot = null;
		}
	}

	private void TrySpawnStandalonePveEnemies(ActiveMapOperation operation)
	{
		if (operation == null || operation.PveSpawnAttempted || !NetworkServer.active)
		{
			return;
		}
		operation.PveSpawnAttempted = true;
		Scene scene = FindLoadedSceneByHandle(operation.SceneHandle);
		if (!scene.IsValid() || !scene.isLoaded)
		{
			return;
		}
		System.Collections.Generic.List<Transform> list = FindSceneMarkers(scene, "PVE_EnemySpawn_");
		if (list.Count == 0)
		{
			log.LogError("Standalone PVE package has no PVE_EnemySpawn_ markers.");
			return;
		}
		int minimumEnemies = operation.Operation.MinimumEnemies;
		int maximumEnemies = operation.Operation.MaximumEnemies;
		if (minimumEnemies < 1 || maximumEnemies < minimumEnemies)
		{
			log.LogError("Standalone PVE package has invalid enemy bounds: operation=" + operation.Operation.Id + ", requestedRange=" + minimumEnemies + "-" + maximumEnemies + ".");
			return;
		}
		if (list.Count < minimumEnemies)
		{
			log.LogError("Standalone PVE package does not author enough enemy markers for its declared minimum: operation=" + operation.Operation.Id + ", requestedRange=" + minimumEnemies + "-" + maximumEnemies + ", markers=" + list.Count + ".");
			return;
		}
		GameManager gameManager = GameManager.instance;
		Il2CppSystem.Collections.Generic.List<GameObject> list2 = ((gameManager == null) ? null : gameManager.AllAITypes);
		System.Collections.Generic.List<GameObject> list3 = new System.Collections.Generic.List<GameObject>();
		if (list2 != null)
		{
			for (int i = 0; i < list2.Count; i++)
			{
				GameObject gameObject = list2[i];
				BrainAI brainAI = ((gameObject == null) ? null : gameObject.GetComponent<BrainAI>());
				NetworkIdentity networkIdentity = ((gameObject == null) ? null : gameObject.GetComponent<NetworkIdentity>());
				WeaponsAI weaponsAI = ((brainAI == null) ? null : (brainAI.weapons ?? gameObject.GetComponentInChildren<WeaponsAI>(includeInactive: true)));
				if (!(gameObject == null) && !(brainAI == null) && !(networkIdentity == null) && !(weaponsAI == null) && weaponsAI.SpawnWeapon && weaponsAI.weaponList != null && weaponsAI.weaponList.Count != 0)
				{
					list3.Add(gameObject);
				}
			}
		}
		if (list3.Count == 0)
		{
			log.LogError("Standalone PVE could not find a server-registered AI prefab in persistent GameManager.AllAITypes.");
			return;
		}
		try
		{
			GameObject gameObject2 = new GameObject("MODDED_OPERATIONS_PVE_DIRECTOR");
			SceneManager.MoveGameObjectToScene(gameObject2, scene);
			RaidManager raidManager = gameObject2.AddComponent<RaidManager>();
			raidManager.enabled = false;
			RaidManager.singleton = raidManager;
			operation.RaidUtilityRoot = gameObject2;
			int val = ChooseStandalonePveEnemyCount(operation);
			int num = Math.Min(val, list.Count);
			raidManager.infiltrationManager = operation.GameModeComponent as InfiltrationManager;
			raidManager.spawnVehicleAI = false;
			raidManager.hasIEDs = false;
			raidManager.hasReinforcements = false;
			raidManager.timedBackup = false;
			raidManager.standardAI = new Il2CppReferenceArray<GameObject>(list3.Count);
			for (int j = 0; j < list3.Count; j++)
			{
				raidManager.standardAI[j] = list3[j];
			}
			raidManager.prohibitedWeapons = new Il2CppReferenceArray<PuppetWeapon>(0L);
			raidManager.mapSpecificWeapons = new Il2CppReferenceArray<PuppetWeapon>(0L);
			raidManager.botSpawnPoints = new Il2CppSystem.Collections.Generic.List<GameObject>();
			ModdedPveAiProfileDefinition pveAiProfile = operation.Operation.PveAiProfile;
			foreach (Transform item in list)
			{
				ConfigureStandaloneBotDetails(item.GetComponent<BotSpawnDetails>() ?? item.gameObject.AddComponent<BotSpawnDetails>(), pveAiProfile);
				raidManager.botSpawnPoints.Add(item.gameObject);
			}
			gameManager.botAmount = num;
			gameManager.botHVTAmount = 0;
			raidManager.ServerSpawnAI(_custom: false);
			operation.PveEnemyCount = num;
			log.LogInfo("Standalone PVE released a server-owned AI population through shipped RaidManager.ServerSpawnAI: count=" + num + ", requestedRange=" + minimumEnemies + "-" + maximumEnemies + ", chosen=" + val + ", markers=" + list.Count + ", firearmCapablePrefabs=" + list3.Count + ", aiProfile=" + FormatPveAiProfile(pveAiProfile) + ".");
		}
		catch (Exception ex)
		{
			log.LogError("Standalone PVE spawn failed closed after " + operation.PveEnemyCount + " confirmed AI: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	private static int ChooseStandalonePveEnemyCount(ActiveMapOperation operation)
	{
		int minimumEnemies = operation.Operation.MinimumEnemies;
		int maximumEnemies = operation.Operation.MaximumEnemies;
		if (maximumEnemies <= minimumEnemies)
		{
			return minimumEnemies;
		}
		string text = operation.Operation.Id + "|" + operation.TimeCode + "|" + operation.SceneHandle;
		uint num = 2166136261u;
		for (int i = 0; i < text.Length; i++)
		{
			num ^= text[i];
			num *= 16777619;
		}
		return minimumEnemies + (int)(num % (uint)(maximumEnemies - minimumEnemies + 1));
	}

	private static void ConfigureStandaloneBotDetails(BotSpawnDetails details, ModdedPveAiProfileDefinition profile)
	{
		if (!(details == null))
		{
			details.DetectionTimeMultiplier = ((profile == null) ? 1.15f : 1f);
			details.HearingRange = ((profile == null) ? 52f : 20f);
			details.DetectionRange = ((profile != null) ? profile.DetectionRangeMeters : 72f);
			details.FOV = ((profile != null) ? profile.FieldOfViewDegrees : 105f);
			details.maxEffectiveRange = ((profile != null) ? profile.MaximumEffectiveRangeMeters : 90f);
			details.useComms = profile == null || profile.UseComms;
			details.DoesCounterSuppression = profile == null || profile.CounterSuppression;
			details.WanderDistance = ((profile != null) ? profile.WanderDistanceMeters : 18);
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
	}

	private static string FormatPveAiProfile(ModdedPveAiProfileDefinition profile)
	{
		if (profile == null)
		{
			return "framework-legacy(range=72m,fov=105,maxEffective=90m,wander=18m,comms=true,counterSuppression=true)";
		}
		return profile.Id + "(range=" + profile.DetectionRangeMeters.ToString("F1", CultureInfo.InvariantCulture) + "m,fov=" + profile.FieldOfViewDegrees.ToString("F1", CultureInfo.InvariantCulture) + ",maxEffective=" + profile.MaximumEffectiveRangeMeters.ToString("F1", CultureInfo.InvariantCulture) + "m,wander=" + profile.WanderDistanceMeters + "m,comms=" + profile.UseComms + ",counterSuppression=" + profile.CounterSuppression + ")";
	}

	private static System.Collections.Generic.List<Transform> FindSceneMarkers(Scene scene, string prefix)
	{
		System.Collections.Generic.List<Transform> list = new System.Collections.Generic.List<Transform>();
		foreach (GameObject rootGameObject in scene.GetRootGameObjects())
		{
			foreach (Transform componentsInChild in rootGameObject.GetComponentsInChildren<Transform>(includeInactive: true))
			{
				if ((componentsInChild.name ?? string.Empty).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					list.Add(componentsInChild);
				}
			}
		}
		list.Sort((Transform left, Transform right) => string.CompareOrdinal(left.name, right.name));
		return list;
	}

	private void SpawnAndPositionStandalonePlayers(ActiveMapOperation operation, bool allowSpawnRequest)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		if (operation == null || operation.SceneHandle == 0)
		{
			return;
		}
		Scene scene = FindLoadedSceneByHandle(operation.SceneHandle);
		if (!scene.IsValid() || !scene.isLoaded)
		{
			return;
		}
		System.Collections.Generic.List<Transform> list = FindStandalonePlayerMarkers(scene, operation.Operation.Mode);
		if (list.Count == 0)
		{
			log.LogError("Standalone package scene has no compatible player spawn markers for spawnSet=" + operation.Operation.SpawnSetId + ".");
			return;
		}
		PlayerMaster[] array;
		try
		{
			array = Resources.FindObjectsOfTypeAll<PlayerMaster>();
		}
		catch
		{
			return;
		}
		PlayerMaster[] array2 = array;
		foreach (PlayerMaster playerMaster in array2)
		{
			if (playerMaster == null || !playerMaster.gameObject.scene.IsValid())
			{
				continue;
			}
			int instanceID = playerMaster.GetInstanceID();
			Transform transform = SelectPlayerMarker(operation, playerMaster, list);
			if (transform == null)
			{
				continue;
			}
			try
			{
				playerMaster.LastSpawnPoint = transform;
				playerMaster.spawnRotation = transform.eulerAngles;
			}
			catch
			{
			}
			PlayerNetworking playerNetworking = null;
			try
			{
				playerNetworking = playerMaster.PlayerSpawnedObject;
			}
			catch
			{
			}
			if (playerNetworking != null)
			{
				operation.CompletedPlayerSpawnIds.Add(instanceID);
			}
			bool flag = false;
			try
			{
				flag = playerMaster.currentlySpawnedAndAlive;
			}
			catch
			{
			}
			if (playerNetworking == null && flag)
			{
				operation.CompletedPlayerSpawnIds.Add(instanceID);
			}
			else
			{
				if (playerNetworking == null && operation.CompletedPlayerSpawnIds.Contains(instanceID))
				{
					continue;
				}
				if (playerNetworking == null && allowSpawnRequest)
				{
					bool flag2 = false;
					try
					{
						flag2 = playerMaster.isOwned;
					}
					catch
					{
					}
					int value;
					int num = (operation.PlayerSpawnRequestCounts.TryGetValue(instanceID, out value) ? value : 0);
					int num2 = ((flag2 && NetworkServer.active) ? 2 : 3);
					if (num >= num2 || (operation.PlayerSpawnRequestFrames.TryGetValue(instanceID, out var value2) && Time.frameCount < value2 + 300))
					{
						continue;
					}
					try
					{
						if (!flag)
						{
							operation.PlayerSpawnRequestFrames[instanceID] = Time.frameCount;
							operation.PlayerSpawnRequestCounts[instanceID] = num + 1;
							string text = RequestStandalonePlayerSpawn(playerMaster, flag2, num);
							bool flag3 = false;
							try
							{
								flag3 = playerMaster.PlayerSpawnedObject != null;
							}
							catch
							{
							}
							if (flag3)
							{
								operation.CompletedPlayerSpawnIds.Add(instanceID);
							}
							log.LogInfo("Standalone requested the shipped player spawn pipeline: playerMaster=" + instanceID + ", owned=" + flag2 + ", serverActive=" + NetworkServer.active + ", route=" + text + ", attempt=" + (num + 1) + "/" + num2 + ", producedPlayerObject=" + flag3 + ".");
						}
					}
					catch (Exception ex)
					{
						Exception ex2 = ((ex is TargetInvocationException && ex.InnerException != null) ? ex.InnerException : ex);
						log.LogWarning("Standalone player spawn request is waiting for " + instanceID + ": " + ex2.GetType().Name + ": " + ex2.Message);
					}
					continue;
				}
				int num3 = ((!(playerNetworking == null)) ? playerNetworking.GetInstanceID() : 0);
				if (playerNetworking == null || (operation.PositionedPlayerObjects.TryGetValue(instanceID, out var value3) && value3 == num3))
				{
					continue;
				}
				bool flag4 = false;
				try
				{
					flag4 = playerNetworking.isOwned || playerNetworking.isLocalPlayer || playerMaster.isOwned;
				}
				catch
				{
				}
				Vector3 vector = transform.position + Vector3.up * 0.25f;
				int value4;
				if (IsPlayerAtPackageSpawn(playerNetworking, vector, flag4, out var state))
				{
					operation.PositionedPlayerObjects[instanceID] = num3;
					log.LogInfo("Standalone player reached package marker through the shipped movement contract: marker=" + transform.name + ", playerMaster=" + instanceID + ", owned=" + flag4 + ", state=" + state + ".");
				}
				else if (!operation.PlayerMoveRequestFrames.TryGetValue(instanceID, out value4) || Time.frameCount >= value4 + 300)
				{
					if (flag4 && GameManager.instance != null)
					{
						GameManager.instance.StartCoroutine(GameManager.instance.MovePlayerToSpawn(vector, transform.rotation));
						operation.PlayerMoveRequestFrames[instanceID] = Time.frameCount;
						log.LogInfo("Standalone invoked shipped GameManager.MovePlayerToSpawn for owned player: marker=" + transform.name + ", playerMaster=" + instanceID + ", priorState=" + state + ".");
					}
					else
					{
						MoveRemotePlayerRoot(playerNetworking.gameObject, vector, transform.rotation);
						operation.PlayerMoveRequestFrames[instanceID] = Time.frameCount;
						log.LogInfo("Standalone moved server-owned remote player root to package marker=" + transform.name + ", playerMaster=" + instanceID + ".");
					}
				}
			}
		}
	}

	private string RequestStandalonePlayerSpawn(PlayerMaster player, bool owned, int priorRequestCount)
	{
		if (player == null)
		{
			throw new ArgumentNullException("player");
		}
		if (owned)
		{
			if (NetworkServer.active && priorRequestCount > 0)
			{
				InvokeGeneratedServerPlayerSpawnBody(player);
				return "owned-host-generated-server-recovery";
			}
			player.SpawnPlayer();
			if (!NetworkServer.active)
			{
				return "owned-native-kickoff-client";
			}
			return "owned-native-kickoff-host";
		}
		if (NetworkServer.active)
		{
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
			throw new MissingMethodException(typeof(PlayerMaster).FullName, "UserCode_CMDSpawnPlayer__NetworkIdentity");
		}
		NetworkIdentity component = player.GetComponent<NetworkIdentity>();
		if (component == null)
		{
			throw new InvalidOperationException("PlayerMaster has no NetworkIdentity for the shipped server spawn body");
		}
		directServerPlayerSpawnMethod.Invoke(player, new object[1] { component });
	}

	private static Scene FindLoadedSceneByHandle(int handle)
	{
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			Scene sceneAt = SceneManager.GetSceneAt(i);
			if (sceneAt.handle == handle)
			{
				return sceneAt;
			}
		}
		return default(Scene);
	}

	private static System.Collections.Generic.List<Transform> FindStandalonePlayerMarkers(Scene scene, ModdedOperationMode mode)
	{
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Invalid comparison between Unknown and I4
		System.Collections.Generic.List<Transform> list = new System.Collections.Generic.List<Transform>();
		System.Collections.Generic.List<Transform> list2 = new System.Collections.Generic.List<Transform>();
		foreach (GameObject rootGameObject in scene.GetRootGameObjects())
		{
			foreach (Transform componentsInChild in rootGameObject.GetComponentsInChildren<Transform>(includeInactive: true))
			{
				string text = componentsInChild.name ?? string.Empty;
				if (text.StartsWith("Team1_Spawn_", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Team1_Backup_Spawn_", StringComparison.OrdinalIgnoreCase) || text.StartsWith("PVP_Team1Spawn_", StringComparison.OrdinalIgnoreCase) || text.StartsWith("PVE_PlayerSpawn_", StringComparison.OrdinalIgnoreCase))
				{
					list.Add(componentsInChild);
				}
				else if (text.StartsWith("Team2_Spawn_", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Team2_Backup_Spawn_", StringComparison.OrdinalIgnoreCase) || text.StartsWith("PVP_Team2Spawn_", StringComparison.OrdinalIgnoreCase))
				{
					list2.Add(componentsInChild);
				}
			}
		}
		list.Sort((Transform left, Transform right) => string.CompareOrdinal(left.name, right.name));
		list2.Sort((Transform left, Transform right) => string.CompareOrdinal(left.name, right.name));
		if ((int)mode == 2)
		{
			return list;
		}
		list.AddRange(list2);
		return list;
	}

	private static Transform SelectPlayerMarker(ActiveMapOperation operation, PlayerMaster player, System.Collections.Generic.List<Transform> markers)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Invalid comparison between Unknown and I4
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Invalid comparison between Unknown and I4
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Invalid comparison between Unknown and I4
		if (operation == null || markers == null || markers.Count == 0)
		{
			return null;
		}
		int instanceID = player.GetInstanceID();
		int pvpTeamId = 0;
		if ((int)operation.Operation.Mode == 1)
		{
			try
			{
				pvpTeamId = player.MyTeamIdentifier?.TeamID ?? 0;
			}
			catch
			{
			}
			if (pvpTeamId != 1 && pvpTeamId != 2)
			{
				return null;
			}
		}
		if (operation.PlayerMarkerNames.TryGetValue(instanceID, out var assignedName))
		{
			Transform transform = markers.FirstOrDefault((Transform marker) => string.Equals(marker.name, assignedName, StringComparison.Ordinal));
			if (transform != null && ((int)operation.Operation.Mode != 1 || PvpMarkerMatchesTeam(transform, pvpTeamId)))
			{
				return transform;
			}
			operation.PlayerMarkerNames.Remove(instanceID);
		}
		Transform transform2;
		if ((int)operation.Operation.Mode == 1)
		{
			System.Collections.Generic.List<Transform> list = markers.Where((Transform marker) => PvpMarkerMatchesTeam(marker, pvpTeamId)).ToList();
			if (list.Count > 0)
			{
				transform2 = list[operation.SpawnCursor++ % list.Count];
				operation.PlayerMarkerNames[instanceID] = transform2.name;
				return transform2;
			}
		}
		transform2 = markers[operation.SpawnCursor++ % markers.Count];
		operation.PlayerMarkerNames[instanceID] = transform2.name;
		return transform2;
	}

	private static bool PvpMarkerMatchesTeam(Transform marker, int teamId)
	{
		string text = marker?.name ?? string.Empty;
		switch (teamId)
		{
		case 1:
			if (!text.StartsWith("Team1", StringComparison.OrdinalIgnoreCase))
			{
				return text.StartsWith("PVP_Team1", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		case 2:
			if (!text.StartsWith("Team2", StringComparison.OrdinalIgnoreCase))
			{
				return text.StartsWith("PVP_Team2", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		default:
			return false;
		}
	}

	private static bool IsPlayerAtPackageSpawn(PlayerNetworking player, Vector3 target, bool owned, out string state)
	{
		if (player == null)
		{
			state = "player=null";
			return false;
		}
		float num = Vector3.Distance(player.transform.position, target);
		if (!owned)
		{
			state = "network=" + num.ToString("F2");
			return num <= 3f;
		}
		PlayerNetworking playerNetworking = null;
		GameObject gameObject = null;
		FirstPersonController firstPersonController = null;
		try
		{
			playerNetworking = GameManager.myPlayerNetworking;
			gameObject = GameManager.myPlayer;
			firstPersonController = GameManager.myPlayerController;
		}
		catch
		{
		}
		float num2 = ((gameObject == null) ? float.MaxValue : Vector3.Distance(gameObject.transform.position, target));
		float num3 = ((firstPersonController == null) ? float.MaxValue : Vector3.Distance(firstPersonController.transform.position, target));
		GameObject gameObject2 = null;
		try
		{
			gameObject2 = player.Camera;
		}
		catch
		{
		}
		float num4 = ((gameObject2 == null) ? float.MaxValue : Vector3.Distance(gameObject2.transform.position, target));
		bool flag = playerNetworking == null || playerNetworking == player;
		bool flag2 = num2 <= 8f || num3 <= 8f;
		bool flag3 = num4 <= 12f;
		state = "network=" + num.ToString("F2") + ", managerPlayer=" + DescribeDistance(num2) + ", controller=" + DescribeDistance(num3) + ", camera=" + DescribeDistance(num4) + ", sameOwner=" + flag;
		return num <= 3f && flag && flag2 && flag3;
	}

	private static string DescribeDistance(float distance)
	{
		if (distance != float.MaxValue)
		{
			return distance.ToString("F2");
		}
		return "null";
	}

	private static void MoveRemotePlayerRoot(GameObject player, Vector3 target, Quaternion rotation)
	{
		if (player == null)
		{
			return;
		}
		CharacterController characterController = player.GetComponent<CharacterController>() ?? player.GetComponentInChildren<CharacterController>(includeInactive: true);
		bool flag = characterController != null && characterController.enabled;
		if (flag)
		{
			characterController.enabled = false;
		}
		player.transform.SetPositionAndRotation(target, rotation);
		foreach (Rigidbody componentsInChild in player.GetComponentsInChildren<Rigidbody>(includeInactive: true))
		{
			componentsInChild.linearVelocity = Vector3.zero;
			componentsInChild.angularVelocity = Vector3.zero;
		}
		if (flag)
		{
			characterController.enabled = true;
		}
		Physics.SyncTransforms();
	}

	private static void ReplaceNativeMapPreview(Transform parent, Sprite previewSprite, string name)
	{
		if (!(parent == null) && !(previewSprite == null))
		{
			SetGameObjectsActive(CaptureDirectChildren(parent), active: false);
			GameObject gameObject = new GameObject(name);
			gameObject.transform.SetParent(parent, worldPositionStays: false);
			SetFullStretch(gameObject.AddComponent<RectTransform>());
			Image image = gameObject.AddComponent<Image>();
			image.sprite = previewSprite;
			image.color = Color.white;
			image.preserveAspect = true;
			image.raycastTarget = false;
		}
	}

	private void RebindNativeFullscreenControls(OperationBoardUI board, GameObject preparationPanel)
	{
		if (board == null || preparationPanel == null)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		foreach (PanelButton componentsInChild in preparationPanel.GetComponentsInChildren<PanelButton>(includeInactive: true))
		{
			int fullscreenEventDirection = GetFullscreenEventDirection((componentsInChild == null) ? null : componentsInChild.onClick, (componentsInChild == null) ? null : componentsInChild.gameObject, board);
			if (fullscreenEventDirection != 0)
			{
				componentsInChild.onClick = new UnityEvent();
				bool fullscreen = fullscreenEventDirection > 0;
				componentsInChild.onClick.AddListener((Action)delegate
				{
					SetNativeMapFullscreen(board, fullscreen);
				});
				componentsInChild.isInteractable = true;
				if (fullscreen)
				{
					num++;
				}
				else
				{
					num2++;
				}
			}
		}
		foreach (ButtonManager componentsInChild2 in preparationPanel.GetComponentsInChildren<ButtonManager>(includeInactive: true))
		{
			int fullscreenEventDirection2 = GetFullscreenEventDirection((componentsInChild2 == null) ? null : componentsInChild2.onClick, (componentsInChild2 == null) ? null : componentsInChild2.gameObject, board);
			if (fullscreenEventDirection2 == 0)
			{
				continue;
			}
			componentsInChild2.onClick = new UnityEvent();
			componentsInChild2.onDoubleClick = new UnityEvent();
			componentsInChild2.checkForDoubleClick = false;
			bool fullscreen2 = fullscreenEventDirection2 > 0;
			componentsInChild2.onClick.AddListener((Action)delegate
			{
				SetNativeMapFullscreen(board, fullscreen2);
			});
			componentsInChild2.isInteractable = true;
			try
			{
				if (componentsInChild2.targetButton != null)
				{
					componentsInChild2.targetButton.interactable = true;
				}
			}
			catch
			{
			}
			if (fullscreen2)
			{
				num++;
			}
			else
			{
				num2++;
			}
		}
		foreach (Button componentsInChild3 in preparationPanel.GetComponentsInChildren<Button>(includeInactive: true))
		{
			int fullscreenEventDirection3 = GetFullscreenEventDirection((componentsInChild3 == null) ? null : componentsInChild3.onClick, (componentsInChild3 == null) ? null : componentsInChild3.gameObject, board);
			if (fullscreenEventDirection3 != 0)
			{
				componentsInChild3.onClick = new Button.ButtonClickedEvent();
				bool fullscreen3 = fullscreenEventDirection3 > 0;
				componentsInChild3.onClick.AddListener((Action)delegate
				{
					SetNativeMapFullscreen(board, fullscreen3);
				});
				componentsInChild3.interactable = true;
				if (fullscreen3)
				{
					num++;
				}
				else
				{
					num2++;
				}
			}
		}
		log.LogInfo("Modded Operations clone-local fullscreen controls rebound: enter=" + num + ", exit=" + num2 + ".");
	}

	private static int GetFullscreenEventDirection(UnityEventBase clickEvent, GameObject control, OperationBoardUI board)
	{
		if (EventInvokesMethod(clickEvent, "EnterFullscreenButton"))
		{
			return 1;
		}
		if (EventInvokesMethod(clickEvent, "ExitFullscreenButton"))
		{
			return -1;
		}
		if (control == null || board == null)
		{
			return 0;
		}
		string text = control.name ?? string.Empty;
		if (text.IndexOf("FULLSCREEN", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("EXPAND", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("MAXIMIZE", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("REDUCE", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("MINIMIZE", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return 0;
		}
		if (board.FullscreenMapObject != null && control.transform.IsChildOf(board.FullscreenMapObject.transform))
		{
			return -1;
		}
		return 1;
	}

	private static bool EventInvokesMethod(UnityEventBase clickEvent, string methodName)
	{
		if (clickEvent == null || string.IsNullOrEmpty(methodName))
		{
			return false;
		}
		try
		{
			int persistentEventCount = clickEvent.GetPersistentEventCount();
			for (int i = 0; i < persistentEventCount; i++)
			{
				if (string.Equals(clickEvent.GetPersistentMethodName(i), methodName, StringComparison.Ordinal))
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static void SetNativeMapFullscreen(OperationBoardUI board, bool fullscreen)
	{
		if (!(board == null))
		{
			try
			{
				board.SetFullscreenImage(fullscreen);
			}
			catch
			{
			}
			SetActiveSafe(board.FullscreenMapObject, fullscreen);
		}
	}

	private static void ConfigureNativeSelector(HorizontalSelector selector, string[] values, Action<int> onChanged, int initialIndex = 0)
	{
		if (selector == null || values == null || values.Length == 0)
		{
			return;
		}
		selector.useLocalization = false;
		selector.saveSelected = false;
		if (selector.localizedObject != null)
		{
			selector.localizedObject.enabled = false;
		}
		selector.items.Clear();
		foreach (string title in values)
		{
			selector.CreateNewItem(title);
		}
		initialIndex = Mathf.Clamp(initialIndex, 0, values.Length - 1);
		selector.defaultIndex = initialIndex;
		selector.index = initialIndex;
		selector.onValueChanged = new HorizontalSelector.HorizontalSelectorEvent();
		if (onChanged != null)
		{
			selector.onValueChanged.AddListener((Action<int>)delegate(int index)
			{
				onChanged(index);
			});
		}
		try
		{
			selector.InitializeSelector();
		}
		catch
		{
			try
			{
				selector.UpdateUI();
			}
			catch
			{
			}
		}
	}

	private static bool ReplaceNativeButtonAction(UnityEngine.Object controlObject, Action action)
	{
		if (controlObject == null || action == null)
		{
			return false;
		}
		GameObject gameObject = controlObject as GameObject;
		if (gameObject == null)
		{
			Component component = controlObject as Component;
			if (component != null)
			{
				gameObject = component.gameObject;
			}
		}
		if (gameObject == null)
		{
			return false;
		}
		int lastLogicalFrame = -1;
		Action invokeOnce = delegate
		{
			int frameCount = Time.frameCount;
			if (frameCount != lastLogicalFrame)
			{
				lastLogicalFrame = frameCount;
				action();
			}
		};
		bool result = false;
		foreach (PanelButton componentsInChild in gameObject.GetComponentsInChildren<PanelButton>(includeInactive: true))
		{
			if (!(componentsInChild == null))
			{
				componentsInChild.onClick = new UnityEvent();
				componentsInChild.onClick.AddListener((Action)delegate
				{
					invokeOnce();
				});
				componentsInChild.isInteractable = true;
				result = true;
			}
		}
		foreach (ButtonManager componentsInChild2 in gameObject.GetComponentsInChildren<ButtonManager>(includeInactive: true))
		{
			if (componentsInChild2 == null)
			{
				continue;
			}
			componentsInChild2.onClick = new UnityEvent();
			componentsInChild2.onDoubleClick = new UnityEvent();
			componentsInChild2.checkForDoubleClick = false;
			componentsInChild2.onClick.AddListener((Action)delegate
			{
				invokeOnce();
			});
			componentsInChild2.isInteractable = true;
			try
			{
				if (componentsInChild2.targetButton != null)
				{
					componentsInChild2.targetButton.interactable = true;
				}
			}
			catch
			{
			}
			result = true;
		}
		foreach (Button componentsInChild3 in gameObject.GetComponentsInChildren<Button>(includeInactive: true))
		{
			if (!(componentsInChild3 == null))
			{
				componentsInChild3.onClick = new Button.ButtonClickedEvent();
				componentsInChild3.onClick.AddListener((Action)delegate
				{
					invokeOnce();
				});
				componentsInChild3.interactable = true;
				result = true;
			}
		}
		return result;
	}

	private bool BindNativePreparationBack(MissionLaptop laptop, GameObject privateBoard, Button authoredBack, bool warnIfMissing)
	{
		if (laptop == null || privateBoard == null)
		{
			return false;
		}
		int instanceID = privateBoard.GetInstanceID();
		if (nativeBackBoundBoards.Contains(instanceID))
		{
			return true;
		}
		System.Collections.Generic.List<Transform> list = new System.Collections.Generic.List<Transform>(1) { privateBoard.transform };
		PanelButton panelButton = null;
		foreach (Transform item in list)
		{
			foreach (PanelButton componentsInChild in item.GetComponentsInChildren<PanelButton>(includeInactive: true))
			{
				if (IsNativeBackCandidate((componentsInChild == null) ? null : componentsInChild.gameObject, authoredBack))
				{
					panelButton = componentsInChild;
					break;
				}
			}
			if (panelButton != null)
			{
				break;
			}
		}
		ButtonManager buttonManager = null;
		if (panelButton == null)
		{
			foreach (Transform item2 in list)
			{
				foreach (ButtonManager componentsInChild2 in item2.GetComponentsInChildren<ButtonManager>(includeInactive: true))
				{
					if (IsNativeBackCandidate((componentsInChild2 == null) ? null : componentsInChild2.gameObject, authoredBack))
					{
						buttonManager = componentsInChild2;
						break;
					}
				}
				if (buttonManager != null)
				{
					break;
				}
			}
		}
		Button button = null;
		if (panelButton == null && buttonManager == null)
		{
			foreach (Transform item3 in list)
			{
				foreach (Button componentsInChild3 in item3.GetComponentsInChildren<Button>(includeInactive: true))
				{
					if (IsNativeBackCandidate((componentsInChild3 == null) ? null : componentsInChild3.gameObject, authoredBack))
					{
						button = componentsInChild3;
						break;
					}
				}
				if (button != null)
				{
					break;
				}
			}
		}
		if (panelButton == null && buttonManager == null && button == null)
		{
			if (warnIfMissing)
			{
				log.LogWarning("Cerberus could not find the shipped Operation Preparation BACK control after the panel opened; the native board and Modded Operations tab remain available.");
			}
			return false;
		}
		if (!ReplaceNativeButtonAction((panelButton != null) ? panelButton.gameObject : ((buttonManager != null) ? buttonManager.gameObject : button.gameObject), delegate
		{
			if (!(privateBoard == null) && privateBoard.activeSelf)
			{
				ReturnNativeOperationToModdedHome(laptop, privateBoard, authoredBack);
			}
		}))
		{
			if (warnIfMissing)
			{
				log.LogWarning("Cerberus could not replace every click surface beneath the private Operation Preparation BACK control.");
			}
			return false;
		}
		nativeBackBoundBoards.Add(instanceID);
		log.LogInfo("Modded Operations board bound to shipped Operation Preparation BACK control via " + ((panelButton != null) ? "DreamOS PanelButton" : ((buttonManager != null) ? "ButtonManager" : "Unity Button")) + "; all clone-local click surfaces replaced.");
		return true;
	}

	private static bool IsNativeBackCandidate(GameObject control, Button authoredBack)
	{
		if (control == null || (authoredBack != null && control == authoredBack.gameObject))
		{
			return false;
		}
		if ((control.name ?? string.Empty).StartsWith("MODDED_", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		return ControlHasText(control, "BACK");
	}

	private static bool ControlHasText(GameObject control, string text)
	{
		if (control == null)
		{
			return false;
		}
		if (control.name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}
		foreach (TMP_Text componentsInChild in control.GetComponentsInChildren<TMP_Text>(includeInactive: true))
		{
			if (componentsInChild != null && TitleEquals(componentsInChild.text, text))
			{
				return true;
			}
		}
		return false;
	}

	private void OpenNativeOperationPreparation(MissionLaptop laptop, GameObject privateBoard, Button authoredBack)
	{
		if (!(laptop == null) && !(privateBoard == null))
		{
			Transform transform = ((laptop.ActiveOperationsTab == null) ? null : laptop.ActiveOperationsTab.transform.parent);
			if (!(transform == null))
			{
				OperationBoardUI componentInChildren = privateBoard.GetComponentInChildren<OperationBoardUI>(includeInactive: true);
				CloseNativeMapConfirmation(componentInChildren, logClose: false);
				SetNativeMapFullscreen(componentInChildren, fullscreen: false);
				ShowIsolatedNativePreparationPanel(laptop, privateBoard);
				transform.gameObject.SetActive(value: false);
				BindNativePreparationBack(laptop, privateBoard, authoredBack, warnIfMissing: true);
				log.LogInfo("Modded Operations board opened through its isolated complete Operation Preparation clone; vanillaContentUntouched=true.");
			}
		}
	}

	private void ShowIsolatedNativePreparationPanel(MissionLaptop laptop, GameObject panel)
	{
		if (laptop == null || panel == null)
		{
			return;
		}
		panel.SetActive(value: true);
		Animator component = panel.GetComponent<Animator>();
		WindowPanelManager cerberusWindowPanelManager = laptop.cerberusWindowPanelManager;
		string text = ((cerberusWindowPanelManager == null) ? null : cerberusWindowPanelManager.panelFadeIn);
		string text2 = ((cerberusWindowPanelManager == null) ? null : cerberusWindowPanelManager.animSpeedKey);
		float value = ((cerberusWindowPanelManager == null) ? 1f : cerberusWindowPanelManager.panelAnimationSpeed);
		bool flag = false;
		if (component != null)
		{
			component.enabled = true;
			try
			{
				if (!string.IsNullOrEmpty(text2))
				{
					component.SetFloat(text2, value);
				}
				if (!string.IsNullOrEmpty(text))
				{
					int num = Animator.StringToHash(text);
					if (component.HasState(0, num))
					{
						component.Play(num, 0, 0f);
						component.Update(0f);
						flag = true;
					}
				}
			}
			catch (Exception ex)
			{
				log.LogWarning("Cerberus private preparation fade-in failed: " + ex.GetType().Name + ": " + ex.Message);
			}
		}
		CanvasGroup component2 = panel.GetComponent<CanvasGroup>();
		if (component2 != null)
		{
			component2.alpha = 1f;
			component2.interactable = true;
			component2.blocksRaycasts = true;
		}
		log.LogInfo("Cerberus private preparation presentation activated: animator=" + (component != null) + ", animatorEnabled=" + (component != null && component.enabled) + ", fadeInState='" + (text ?? "null") + "', fadeInPlayed=" + flag + ", rootCanvasGroup=" + (component2 != null) + ".");
	}

	private void ReturnNativeOperationToModdedHome(MissionLaptop laptop, GameObject privateBoard, Button authoredBack)
	{
		OperationBoardUI componentInChildren = privateBoard.GetComponentInChildren<OperationBoardUI>(includeInactive: true);
		CloseNativeMapConfirmation(componentInChildren, logClose: false);
		SetNativeMapFullscreen(componentInChildren, fullscreen: false);
		privateBoard.SetActive(value: false);
		Transform transform = ((laptop.ActiveOperationsTab == null) ? null : laptop.ActiveOperationsTab.transform.parent);
		if (transform != null)
		{
			transform.gameObject.SetActive(value: true);
		}
		GameObject page = FindChild(transform, "MODDED_OPERATIONS_PAGE");
		OpenModdedPage(laptop, page);
		GameObject button = FindDeep(transform, "MODDED_OPS_NATIVE_TAB");
		SetTabSelectedState(FindNativeActiveOperationsButton(transform), selected: false);
		SetTabSelectedState(FindNativeSimulationOperationsButton(transform), selected: false);
		SetTabSelectedState(button, selected: true);
		MarkNativeModdedPageOpened(laptop.GetInstanceID(), Time.frameCount);
		log.LogInfo("Modded Operations clone-local BACK closed transient UI and returned home without invoking the vanilla panel manager.");
	}

	private void CloseNativeMapConfirmation(OperationBoardUI board, bool logClose)
	{
		if (board == null || board.ConfirmationWindow == null)
		{
			return;
		}
		ModalWindowManager confirmationWindow = board.ConfirmationWindow;
		try
		{
			if (confirmationWindow.isOn)
			{
				confirmationWindow.CloseWindow();
			}
			if (logClose)
			{
				log.LogInfo("Modded Operations confirmation window closed through its clone-local modal route.");
			}
		}
		catch (Exception ex)
		{
			confirmationWindow.gameObject.SetActive(value: false);
			log.LogWarning("Modded Operations clone-local confirmation close fell back to disabling its private modal: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	private static void SetNativeConfirmationLoadingState(CatalogPresentation presentation, bool loading)
	{
		ModalWindowManager modalWindowManager = ((presentation?.Board == null) ? null : presentation.Board.ConfirmationWindow);
		if (modalWindowManager == null)
		{
			return;
		}
		if (loading)
		{
			modalWindowManager.descriptionText = "Loading verified map content. The operation will start when it is ready.";
		}
		else if (presentation.SelectedOperation != null)
		{
			modalWindowManager.descriptionText = "Start " + presentation.SelectedOperation.DisplayName + " at " + presentation.SelectedTimeCode + "?";
		}
		if (modalWindowManager.windowDescription != null)
		{
			modalWindowManager.windowDescription.text = modalWindowManager.descriptionText;
		}
		UnityEngine.Object confirmButton = modalWindowManager.confirmButton;
		GameObject gameObject = null;
		if (confirmButton is GameObject gameObject2)
		{
			gameObject = gameObject2;
		}
		else if (confirmButton is Component component)
		{
			gameObject = component.gameObject;
		}
		if (gameObject == null)
		{
			return;
		}
		foreach (PanelButton componentsInChild in gameObject.GetComponentsInChildren<PanelButton>(includeInactive: true))
		{
			if (componentsInChild != null)
			{
				componentsInChild.isInteractable = !loading;
			}
		}
		foreach (ButtonManager componentsInChild2 in gameObject.GetComponentsInChildren<ButtonManager>(includeInactive: true))
		{
			if (!(componentsInChild2 == null))
			{
				componentsInChild2.isInteractable = !loading;
				if (componentsInChild2.targetButton != null)
				{
					componentsInChild2.targetButton.interactable = !loading;
				}
			}
		}
		foreach (Button componentsInChild3 in gameObject.GetComponentsInChildren<Button>(includeInactive: true))
		{
			if (componentsInChild3 != null)
			{
				componentsInChild3.interactable = !loading;
			}
		}
	}

	private GameObject CreateNativeBoardActionButton(Transform parent, GameObject template, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Action action)
	{
		if (parent == null || template == null || action == null)
		{
			return null;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(template, parent);
		if (gameObject == null)
		{
			return null;
		}
		gameObject.name = name;
		RectTransform component = gameObject.GetComponent<RectTransform>();
		if (component != null)
		{
			component.anchorMin = anchorMin;
			component.anchorMax = anchorMax;
			component.offsetMin = Vector2.zero;
			component.offsetMax = Vector2.zero;
			component.localScale = Vector3.one;
		}
		gameObject.SetActive(value: true);
		ButtonManager buttonManager = gameObject.GetComponent<ButtonManager>() ?? gameObject.GetComponentInChildren<ButtonManager>(includeInactive: true);
		if (buttonManager == null)
		{
			UnityEngine.Object.Destroy(gameObject);
			return null;
		}
		buttonManager.useLocalization = false;
		buttonManager.buttonText = text;
		if (buttonManager.localizedObject != null)
		{
			buttonManager.localizedObject.enabled = false;
		}
		SetTmpText(buttonManager.normalTextObj, text);
		SetTmpText(buttonManager.highlightTextObj, text);
		SetTmpText(buttonManager.pressedTextObj, text);
		SetTmpText(buttonManager.disabledTextObj, text);
		foreach (TMP_Text componentsInChild in gameObject.GetComponentsInChildren<TMP_Text>(includeInactive: true))
		{
			DisableLocalizationComponent(componentsInChild.gameObject);
			SetTmpText(componentsInChild, text);
		}
		buttonManager.onClick = new UnityEvent();
		buttonManager.onDoubleClick = new UnityEvent();
		buttonManager.checkForDoubleClick = false;
		buttonManager.onClick.AddListener((Action)delegate
		{
			action();
		});
		buttonManager.isInteractable = true;
		try
		{
			if (buttonManager.targetButton != null)
			{
				buttonManager.targetButton.interactable = true;
			}
		}
		catch
		{
		}
		try
		{
			buttonManager.UpdateUI();
		}
		catch
		{
		}
		try
		{
			buttonManager.UpdateState();
		}
		catch
		{
		}
		return gameObject;
	}

	private static void RestoreAuthoredPresentationChildren(Transform parent, string nativeShellName)
	{
		if (parent == null)
		{
			return;
		}
		for (int i = 0; i < parent.childCount; i++)
		{
			Transform child = parent.GetChild(i);
			if (child != null && child.name != nativeShellName)
			{
				child.gameObject.SetActive(value: true);
			}
		}
	}

	private static void SetTmpText(TMP_Text label, string text)
	{
		if (!(label == null))
		{
			label.text = text;
			label.enableWordWrapping = false;
			label.enableAutoSizing = true;
			label.fontSizeMin = Mathf.Max(8f, label.fontSize * 0.55f);
			label.fontSizeMax = Mathf.Max(label.fontSizeMin, label.fontSize);
		}
	}

	private static GameObject FindNativeOperationRowTemplate(Transform list)
	{
		if (list == null)
		{
			return null;
		}
		GameObject gameObject = null;
		for (int i = 0; i < list.childCount; i++)
		{
			Transform child = list.GetChild(i);
			if (!(child == null))
			{
				if (gameObject == null && child.GetComponentInChildren<TMP_Text>(includeInactive: true) != null)
				{
					gameObject = child.gameObject;
				}
				if (child.GetComponentInChildren<OperationSelectionUI>(includeInactive: true) != null)
				{
					return child.gameObject;
				}
			}
		}
		return gameObject;
	}

	private static int[] BuildRelativeChildIndexPath(Transform root, Transform descendant)
	{
		if (root == null || descendant == null)
		{
			return null;
		}
		System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>();
		Transform transform = descendant;
		while (transform != null && transform != root)
		{
			list.Add(transform.GetSiblingIndex());
			transform = transform.parent;
		}
		if (transform != root)
		{
			return null;
		}
		list.Reverse();
		return list.ToArray();
	}

	private static Transform FollowRelativeChildIndexPath(Transform root, int[] indexes)
	{
		if (root == null || indexes == null)
		{
			return null;
		}
		Transform transform = root;
		foreach (int num in indexes)
		{
			if (num < 0 || num >= transform.childCount)
			{
				return null;
			}
			transform = transform.GetChild(num);
		}
		return transform;
	}

	private static void SetChildrenActive(Transform parent, bool active)
	{
		if (parent == null)
		{
			return;
		}
		for (int i = 0; i < parent.childCount; i++)
		{
			Transform child = parent.GetChild(i);
			if (child != null)
			{
				child.gameObject.SetActive(active);
			}
		}
	}

	private static System.Collections.Generic.List<GameObject> CaptureDirectChildren(Transform parent)
	{
		System.Collections.Generic.List<GameObject> list = new System.Collections.Generic.List<GameObject>();
		if (parent == null)
		{
			return list;
		}
		for (int i = 0; i < parent.childCount; i++)
		{
			Transform child = parent.GetChild(i);
			if (child != null)
			{
				list.Add(child.gameObject);
			}
		}
		return list;
	}

	private static void SetGameObjectsActive(object collection, bool active)
	{
		foreach (object item in ReadListItems(collection))
		{
			GameObject gameObject = ExtractGameObject(item);
			if (gameObject != null)
			{
				gameObject.SetActive(active);
			}
		}
	}

	private static void SetActiveSafe(GameObject gameObject, bool active)
	{
		if (gameObject != null)
		{
			gameObject.SetActive(active);
		}
	}

	private static void SetComponentActiveSafe(Component component, bool active)
	{
		if (component != null)
		{
			component.gameObject.SetActive(active);
		}
	}

	private static void SetFullStretch(RectTransform rect)
	{
		if (!(rect == null))
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			rect.localScale = Vector3.one;
		}
	}

	private static TMP_Text FindNamedText(Transform root, string name)
	{
		GameObject gameObject = FindDeep(root, name);
		if (gameObject == null)
		{
			return null;
		}
		return gameObject.GetComponent<TMP_Text>() ?? gameObject.GetComponentInChildren<TMP_Text>(includeInactive: true);
	}

	private static TMP_Text FindNativeBriefingText(GameObject shell, params GameObject[] excludedRoots)
	{
		if (shell == null)
		{
			return null;
		}
		TMP_Text tMP_Text = FindNamedText(shell.transform, "Operation Selection Briefing");
		if (tMP_Text != null && !IsInsideAny(tMP_Text.transform, excludedRoots))
		{
			return tMP_Text;
		}
		TMP_Text result = null;
		float num = float.MinValue;
		foreach (LocalizationTMPEvent componentsInChild in shell.GetComponentsInChildren<LocalizationTMPEvent>(includeInactive: true))
		{
			TMP_Text tMP_Text2 = null;
			try
			{
				tMP_Text2 = ((componentsInChild == null) ? null : componentsInChild.TMP);
			}
			catch
			{
			}
			if (tMP_Text2 == null)
			{
				continue;
			}
			bool flag = false;
			if (excludedRoots != null)
			{
				foreach (GameObject gameObject in excludedRoots)
				{
					if (gameObject != null && tMP_Text2.transform.IsChildOf(gameObject.transform))
					{
						flag = true;
						break;
					}
				}
			}
			if (!flag)
			{
				RectTransform rectTransform = tMP_Text2.rectTransform;
				float num2 = ((rectTransform == null) ? 0f : Mathf.Abs(rectTransform.rect.width * rectTransform.rect.height));
				string text = tMP_Text2.gameObject.name ?? string.Empty;
				string obj2 = tMP_Text2.text ?? string.Empty;
				if (text.IndexOf("brief", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					num2 += 1000000f;
				}
				if (obj2.Length >= 80)
				{
					num2 += 500000f;
				}
				if (num2 > num)
				{
					result = tMP_Text2;
					num = num2;
				}
			}
		}
		return result;
	}

	private static bool IsInsideAny(Transform candidate, GameObject[] roots)
	{
		if (candidate == null || roots == null)
		{
			return false;
		}
		foreach (GameObject gameObject in roots)
		{
			if (gameObject != null && candidate.IsChildOf(gameObject.transform))
			{
				return true;
			}
		}
		return false;
	}

	private static void RewriteClonedPageHeading(GameObject shell)
	{
		if (shell == null)
		{
			return;
		}
		foreach (TMP_Text componentsInChild in shell.GetComponentsInChildren<TMP_Text>(includeInactive: true))
		{
			if (!(componentsInChild == null) && (TitleEquals(componentsInChild.text, "ACTIVE OPERATIONS") || TitleEquals(componentsInChild.text, "ACTIVE OPS")))
			{
				DisableLocalizationComponent(componentsInChild.gameObject);
				if (componentsInChild.transform.parent != null)
				{
					DisableLocalizationComponent(componentsInChild.transform.parent.gameObject);
				}
				componentsInChild.text = "MODDED OPERATIONS";
				componentsInChild.enableWordWrapping = false;
				componentsInChild.enableAutoSizing = true;
				componentsInChild.fontSizeMin = Mathf.Max(8f, componentsInChild.fontSize * 0.55f);
				componentsInChild.fontSizeMax = Mathf.Max(componentsInChild.fontSizeMin, componentsInChild.fontSize);
			}
		}
	}

	private static void RewriteNativeOperationRow(GameObject row, string operationName, string duration, string threat, string mode, string areaOfOperation = null)
	{
		if (row == null)
		{
			return;
		}
		HashSet<int> hashSet = new HashSet<int>();
		OperationSelectionUI componentInChildren = row.GetComponentInChildren<OperationSelectionUI>(includeInactive: true);
		System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<TMP_Text, string>> list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<TMP_Text, string>>();
		if (componentInChildren != null)
		{
			AddColumnReference(list, FindMemberText(componentInChildren, "NameTextBox"), operationName);
			AddColumnReference(list, FindMemberText(componentInChildren, "SecondTextBox"), duration);
			AddColumnReference(list, FindMemberText(componentInChildren, "ThirdTextBox"), threat);
			AddColumnReference(list, FindMemberText(componentInChildren, "SimulationTypeText"), mode);
			SetMemberText(componentInChildren, "NameTextBox", operationName, hashSet);
			SetMemberText(componentInChildren, "RegionNameTextBox", string.IsNullOrWhiteSpace(areaOfOperation) ? operationName : areaOfOperation, hashSet);
			SetMemberText(componentInChildren, "SecondTextBox", duration, hashSet);
			SetMemberText(componentInChildren, "ThirdTextBox", threat, hashSet);
			SetMemberText(componentInChildren, "SimulationTypeText", mode, hashSet);
			GameObject gameObject = ExtractGameObject(ReadMember(componentInChildren, "ClassfiedCover"));
			GameObject gameObject2 = ExtractGameObject(ReadMember(componentInChildren, "RegionCover"));
			if (gameObject != null)
			{
				gameObject.SetActive(value: false);
			}
			if (gameObject2 != null)
			{
				gameObject2.SetActive(value: false);
			}
		}
		RectTransform component = row.GetComponent<RectTransform>();
		foreach (TMP_Text componentsInChild in row.GetComponentsInChildren<TMP_Text>(includeInactive: true))
		{
			if (componentsInChild == null || hashSet.Contains(componentsInChild.GetInstanceID()))
			{
				continue;
			}
			string text = null;
			float num = float.MaxValue;
			foreach (System.Collections.Generic.KeyValuePair<TMP_Text, string> item in list)
			{
				if (!(item.Key == null))
				{
					float num2 = Mathf.Abs(componentsInChild.rectTransform.position.x - item.Key.rectTransform.position.x);
					if (num2 < num)
					{
						text = item.Value;
						num = num2;
					}
				}
			}
			if (text == null && component != null && Mathf.Abs(component.rect.width) > 0.01f)
			{
				float num3 = (component.InverseTransformPoint(componentsInChild.rectTransform.TransformPoint(componentsInChild.rectTransform.rect.center)).x - component.rect.xMin) / component.rect.width;
				text = ((num3 < 0.44f) ? operationName : ((num3 < 0.72f) ? duration : threat));
			}
			if (text != null)
			{
				DisableLocalizationComponent(componentsInChild.gameObject);
				componentsInChild.text = text;
				componentsInChild.enableWordWrapping = false;
				componentsInChild.enableAutoSizing = true;
				componentsInChild.fontSizeMin = Mathf.Max(8f, componentsInChild.fontSize * 0.55f);
				componentsInChild.fontSizeMax = Mathf.Max(componentsInChild.fontSizeMin, componentsInChild.fontSize);
			}
		}
	}

	private static TMP_Text FindMemberText(object owner, string memberName)
	{
		GameObject gameObject = ExtractGameObject(ReadMember(owner, memberName));
		if (gameObject == null)
		{
			return null;
		}
		return gameObject.GetComponent<TMP_Text>() ?? gameObject.GetComponentInChildren<TMP_Text>(includeInactive: true);
	}

	private static void AddColumnReference(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<TMP_Text, string>> references, TMP_Text label, string value)
	{
		if (references != null && label != null && !string.IsNullOrEmpty(value))
		{
			references.Add(new System.Collections.Generic.KeyValuePair<TMP_Text, string>(label, value));
		}
	}

	private static bool SetMemberText(object owner, string memberName, string value, HashSet<int> assigned)
	{
		GameObject gameObject = ExtractGameObject(ReadMember(owner, memberName));
		if (gameObject == null)
		{
			return false;
		}
		TMP_Text tMP_Text = gameObject.GetComponent<TMP_Text>() ?? gameObject.GetComponentInChildren<TMP_Text>(includeInactive: true);
		if (tMP_Text == null)
		{
			return false;
		}
		DisableLocalizationComponent(gameObject);
		if (tMP_Text.gameObject != gameObject)
		{
			DisableLocalizationComponent(tMP_Text.gameObject);
		}
		tMP_Text.text = value;
		tMP_Text.enableWordWrapping = false;
		tMP_Text.enableAutoSizing = true;
		tMP_Text.fontSizeMin = Mathf.Max(8f, tMP_Text.fontSize * 0.55f);
		tMP_Text.fontSizeMax = Mathf.Max(tMP_Text.fontSizeMin, tMP_Text.fontSize);
		assigned?.Add(tMP_Text.GetInstanceID());
		return true;
	}

	private static void DisableLocalizationComponent(GameObject gameObject)
	{
		if (gameObject == null)
		{
			return;
		}
		try
		{
			LocalizationTMPEvent component = gameObject.GetComponent<LocalizationTMPEvent>();
			if (component != null)
			{
				component.enabled = false;
			}
		}
		catch
		{
		}
		try
		{
			Michsky.DreamOS.LocalizedObject component2 = gameObject.GetComponent<Michsky.DreamOS.LocalizedObject>();
			if (component2 != null)
			{
				component2.enabled = false;
			}
		}
		catch
		{
		}
		foreach (Component component3 in gameObject.GetComponents<Component>())
		{
			if (component3 == null)
			{
				continue;
			}
			string name = component3.GetType().Name;
			if (name.IndexOf("LocalizationTMPEvent", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("LocalizedObject", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				if (component3 is Behaviour behaviour)
				{
					behaviour.enabled = false;
				}
				UnityEngine.Object.Destroy(component3);
			}
		}
	}

	private static void StackNativeRows(GameObject first, GameObject second)
	{
		RectTransform rectTransform = ((first == null) ? null : first.GetComponent<RectTransform>());
		RectTransform rectTransform2 = ((second == null) ? null : second.GetComponent<RectTransform>());
		if (!(rectTransform == null) && !(rectTransform2 == null))
		{
			Vector2 anchoredPosition = rectTransform.anchoredPosition;
			float num = Mathf.Max(1f, rectTransform.rect.height);
			rectTransform2.anchoredPosition = new Vector2(anchoredPosition.x, anchoredPosition.y - num - 6f);
		}
	}

	private static void StackNativeRows(System.Collections.Generic.IReadOnlyList<GameObject> rows)
	{
		if (rows == null || rows.Count < 2)
		{
			return;
		}
		RectTransform rectTransform = ((rows[0] == null) ? null : rows[0].GetComponent<RectTransform>());
		if (rectTransform == null)
		{
			return;
		}
		Vector2 anchoredPosition = rectTransform.anchoredPosition;
		float num = Mathf.Max(1f, rectTransform.rect.height);
		for (int i = 1; i < rows.Count; i++)
		{
			RectTransform rectTransform2 = ((rows[i] == null) ? null : rows[i].GetComponent<RectTransform>());
			if (rectTransform2 != null)
			{
				rectTransform2.anchoredPosition = new Vector2(anchoredPosition.x, anchoredPosition.y - (float)i * (num + 6f));
			}
		}
	}

	private void RebindNativeRow(GameObject row, Action singleClick, Action doubleClick = null)
	{
		if (row == null || singleClick == null)
		{
			return;
		}
		GameObject gameObject = FindDeep(row.transform, "MODDED_NATIVE_ROW_HIT_TARGET");
		if (gameObject != null)
		{
			UnityEngine.Object.Destroy(gameObject);
		}
		ButtonManager buttonManager = null;
		foreach (ButtonManager componentsInChild in row.GetComponentsInChildren<ButtonManager>(includeInactive: true))
		{
			if (!(componentsInChild == null))
			{
				if (buttonManager == null || componentsInChild.targetButton != null)
				{
					buttonManager = componentsInChild;
				}
				if (componentsInChild.targetButton != null)
				{
					break;
				}
			}
		}
		if (buttonManager == null)
		{
			OperationSelectionUI componentInChildren = row.GetComponentInChildren<OperationSelectionUI>(includeInactive: true);
			Button button = ((componentInChildren == null) ? null : componentInChildren.GetComponent<Button>());
			if (button == null)
			{
				button = row.GetComponent<Button>();
			}
			if (button == null)
			{
				button = row.GetComponentInChildren<Button>(includeInactive: true);
			}
			if (button == null)
			{
				log?.LogWarning("Cerberus native row binding skipped because the shipped row had neither ButtonManager nor Button: " + row.name + ".");
				return;
			}
			float doubleClickPeriod = 0.3f;
			try
			{
				CerebusUiBase cerebusUiBase = ((componentInChildren == null) ? null : componentInChildren.CerebusUiBase);
				if (cerebusUiBase != null && cerebusUiBase.clickcountTime >= 0.05f && cerebusUiBase.clickcountTime <= 1f)
				{
					doubleClickPeriod = cerebusUiBase.clickcountTime;
				}
			}
			catch
			{
			}
			float previousClickTime = -100f;
			button.onClick = new Button.ButtonClickedEvent();
			button.onClick.AddListener((Action)delegate
			{
				float unscaledTime = Time.unscaledTime;
				if (doubleClick != null && unscaledTime - previousClickTime <= doubleClickPeriod)
				{
					previousClickTime = -100f;
					doubleClick();
				}
				else
				{
					previousClickTime = unscaledTime;
					singleClick();
				}
			});
			button.interactable = true;
			log?.LogInfo("Cerberus native row bound through shipped Unity Button: row=" + row.name + ", button=" + button.gameObject.name + ", doubleClickPeriod=" + doubleClickPeriod.ToString("0.###") + "s.");
			return;
		}
		buttonManager.onClick = new UnityEvent();
		buttonManager.onDoubleClick = new UnityEvent();
		buttonManager.checkForDoubleClick = doubleClick != null;
		buttonManager.onClick.AddListener((Action)delegate
		{
			singleClick();
		});
		if (doubleClick != null)
		{
			buttonManager.onDoubleClick.AddListener((Action)delegate
			{
				doubleClick();
			});
		}
		buttonManager.isInteractable = true;
		try
		{
			if (buttonManager.targetButton != null)
			{
				buttonManager.targetButton.interactable = true;
			}
		}
		catch
		{
		}
		try
		{
			buttonManager.UpdateState();
		}
		catch
		{
		}
	}

	private static void SetNativeRowSelected(GameObject row, bool selected)
	{
		if (row == null)
		{
			return;
		}
		bool flag = false;
		foreach (Transform componentsInChild in row.GetComponentsInChildren<Transform>(includeInactive: true))
		{
			if (!(componentsInChild == null) && !(componentsInChild == row.transform) && componentsInChild.name.IndexOf("Selected", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				componentsInChild.gameObject.SetActive(selected);
				flag = true;
			}
		}
		if (!flag)
		{
			Image image = row.GetComponent<Image>() ?? row.GetComponentInChildren<Image>(includeInactive: true);
			if (image != null)
			{
				image.color = (selected ? new Color(image.color.r, image.color.g, image.color.b, 1f) : new Color(image.color.r, image.color.g, image.color.b, 0.72f));
			}
		}
	}

	private static GameObject CreateNativeActionButton(Transform parent, GameObject template, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Action action)
	{
		if (parent == null || template == null || action == null)
		{
			return null;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(template, parent);
		if (gameObject == null)
		{
			return null;
		}
		gameObject.name = name;
		RectTransform component = gameObject.GetComponent<RectTransform>();
		if (component != null)
		{
			component.anchorMin = anchorMin;
			component.anchorMax = anchorMax;
			component.offsetMin = Vector2.zero;
			component.offsetMax = Vector2.zero;
			component.localScale = Vector3.one;
		}
		SetButtonText(gameObject, text);
		FitTabTitleText(gameObject, text);
		int lastLogicalFrame = -1;
		Action invokeOnce = delegate
		{
			int frameCount = Time.frameCount;
			if (frameCount != lastLogicalFrame)
			{
				lastLogicalFrame = frameCount;
				action();
			}
		};
		PanelButton component2 = gameObject.GetComponent<PanelButton>();
		if (component2 != null)
		{
			component2.onClick = new UnityEvent();
			component2.onClick.AddListener((Action)delegate
			{
				invokeOnce();
			});
			component2.isInteractable = true;
			component2.isSelected = false;
		}
		Button button = gameObject.GetComponent<Button>();
		if (button == null)
		{
			button = gameObject.AddComponent<Button>();
		}
		if (button.targetGraphic == null)
		{
			GameObject gameObject2 = new GameObject("MODDED_NATIVE_ACTION_HIT_TARGET");
			gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
			SetFullStretch(gameObject2.AddComponent<RectTransform>());
			Image image = gameObject2.AddComponent<Image>();
			image.color = new Color(0f, 0f, 0f, 0f);
			image.raycastTarget = true;
			button.targetGraphic = image;
		}
		button.transition = Selectable.Transition.None;
		button.onClick.RemoveAllListeners();
		button.onClick.AddListener((Action)delegate
		{
			invokeOnce();
		});
		button.interactable = true;
		gameObject.SetActive(value: true);
		return gameObject;
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
		{
			return;
		}
		int frameCount = Time.frameCount;
		for (int num = pendingTransitionSnapshots.Count - 1; num >= 0; num--)
		{
			PendingTransitionSnapshot pendingTransitionSnapshot = pendingTransitionSnapshots[num];
			if (pendingTransitionSnapshot != null && frameCount > pendingTransitionSnapshot.RequestedFrame)
			{
				pendingTransitionSnapshots.RemoveAt(num);
				if (!(pendingTransitionSnapshot.Laptop == null))
				{
					log.LogInfo(CaptureLaptopTransitionState(pendingTransitionSnapshot.Laptop, pendingTransitionSnapshot.Page, "next-frame state", pendingTransitionSnapshot.Source, frameCount));
				}
			}
		}
	}

	private static string CaptureLaptopTransitionState(MissionLaptop laptop, GameObject page, string phase, string eventSource, int frame)
	{
		if (laptop == null)
		{
			return "Cerberus transition snapshot: laptop=null, phase=" + phase + ".";
		}
		WindowPanelManager cerberusWindowPanelManager = laptop.cerberusWindowPanelManager;
		string text = ((cerberusWindowPanelManager == null) ? "null" : ("currentPanelIndex=" + DescribeValue(ReadMember(cerberusWindowPanelManager, "currentPanelIndex")) + ", currentButtonIndex=" + DescribeValue(ReadMember(cerberusWindowPanelManager, "currentButtonIndex")) + ", newPanelIndex=" + DescribeValue(ReadMember(cerberusWindowPanelManager, "newPanelIndex")) + ", currentPanel=" + DescribeValue(ReadMember(cerberusWindowPanelManager, "currentPanel")) + ", currentButton=" + DescribeValue(ReadMember(cerberusWindowPanelManager, "currentButton")) + ", panels=" + DescribePanelItems(ReadMember(cerberusWindowPanelManager, "panels"))));
		Type type = ResolveLoadedTypeByExactName("MissionLaptopNetworkState", "Il2Cpp.MissionLaptopNetworkState");
		object owner = ((type == null) ? null : ReadMember(type, null, "singleton"));
		GameObject gameObject = ((page == null) ? null : FindDeep(page.transform, "MODDED_HOME"));
		GameObject gameObject2 = ((page == null) ? null : FindDeep(page.transform, "MODDED_BRIEFING"));
		return "Cerberus transition snapshot: phase=" + phase + ", source=" + eventSource + ", frame=" + frame + ", laptopId=" + laptop.GetInstanceID() + ", laptopPath=" + HierarchyPath(laptop.transform) + ", manager={" + text + "}, ActiveOperationsTab={" + DescribeActivity(laptop.ActiveOperationsTab) + "}, SimulationOperationsTab={" + DescribeActivity(laptop.SimulationOperationsTab) + "}, ActiveOperationsList={" + DescribeActivity(ExtractGameObject(ReadMember(laptop, "ActiveOperationsList"))) + "}, SimulationOperationList={" + DescribeActivity(ExtractGameObject(ReadMember(laptop, "SimulationOperationList"))) + "}, TargetPackageParent={" + DescribeActivity(ExtractGameObject(ReadMember(laptop, "TargetPackageParent"))) + "}, opBoardParent={" + DescribeActivity(ExtractGameObject(ReadMember(laptop, "opBoardParent"))) + "}, network={CurrentLaptopPage=" + DescribeValue(ReadMember(owner, "CurrentLaptopPage")) + ", OnSimulationPage=" + DescribeValue(ReadMember(owner, "OnSimulationPage")) + "}, displayers=" + DescribeMissionLaptopDisplayers() + ", modPage={" + DescribeActivity(page) + "}, modHome={" + DescribeActivity(gameObject) + "}, modBriefing={" + DescribeActivity(gameObject2) + "}.";
	}

	private static string DescribePanelItems(object panelList)
	{
		System.Collections.Generic.List<object> list = ReadListItems(panelList);
		System.Collections.Generic.List<string> list2 = new System.Collections.Generic.List<string>(list.Count);
		for (int i = 0; i < list.Count; i++)
		{
			object owner = list[i];
			GameObject gameObject = ExtractGameObject(ReadMember(owner, "panelObject"));
			GameObject gameObject2 = ExtractGameObject(ReadMember(owner, "panelButton"));
			list2.Add(i + ":name=" + DescribeValue(ReadMember(owner, "panelName")) + ", panel=" + DescribeActivity(gameObject) + ", button=" + DescribeActivity(gameObject2));
		}
		return "[" + string.Join(" | ", list2) + "]";
	}

	private static string DescribeMissionLaptopDisplayers()
	{
		Type type = ResolveLoadedTypeByExactName("MissionLaptopDisplayer", "Il2Cpp.MissionLaptopDisplayer");
		if (type == null)
		{
			return "type-unresolved";
		}
		System.Collections.Generic.List<Component> list = FindMissionLaptopComponents(type);
		System.Collections.Generic.List<string> list2 = new System.Collections.Generic.List<string>(list.Count);
		foreach (Component item in list)
		{
			if (!(item == null) && !(item.gameObject == null))
			{
				list2.Add("id=" + item.GetInstanceID() + ", path=" + HierarchyPath(item.transform) + ", selectedOperation=" + DescribeValue(ReadFirstMember(item, "selectedOperation", "SelectedOperation", "currentOperation")) + ", selectedTargetPackage=" + DescribeValue(ReadFirstMember(item, "selectedTargetPackage", "SelectedTargetPackage", "currentTargetPackage")) + ", opboard=" + DescribeValue(ReadFirstMember(item, "opBoard", "opboard", "operationBoard", "currentOpBoard")));
			}
		}
		return "[" + string.Join(" | ", list2) + "]";
	}

	private static object ReadFirstMember(object owner, params string[] names)
	{
		if (owner == null || names == null)
		{
			return null;
		}
		foreach (string name in names)
		{
			object obj = ReadMember(owner, name);
			if (obj != null)
			{
				return obj;
			}
		}
		return null;
	}

	private static object ReadMember(object owner, string name)
	{
		if (owner != null)
		{
			return ReadMember(owner.GetType(), owner, name);
		}
		return null;
	}

	private static object ReadMember(Type type, object owner, string name)
	{
		if (type == null || string.IsNullOrEmpty(name))
		{
			return null;
		}
		try
		{
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			PropertyInfo property = type.GetProperty(name, bindingAttr);
			if (property != null)
			{
				return property.GetValue(owner);
			}
			return type.GetField(name, bindingAttr)?.GetValue(owner);
		}
		catch
		{
			return null;
		}
	}

	private static System.Collections.Generic.List<object> ReadListItems(object list)
	{
		System.Collections.Generic.List<object> list2 = new System.Collections.Generic.List<object>();
		if (list == null)
		{
			return list2;
		}
		try
		{
			if (list is IEnumerable enumerable)
			{
				foreach (object item in enumerable)
				{
					if (item != null)
					{
						list2.Add(item);
					}
				}
				if (list2.Count > 0)
				{
					return list2;
				}
			}
		}
		catch
		{
		}
		try
		{
			if (!((ReadMember(list, "Count") ?? ReadMember(list, "Length")) is int num) || num <= 0)
			{
				return list2;
			}
			Type type = list.GetType();
			MethodInfo methodInfo = type.GetProperty("Item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetGetMethod(nonPublic: true) ?? type.GetMethod("get_Item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(int) }, null);
			if (methodInfo == null)
			{
				return list2;
			}
			for (int i = 0; i < num; i++)
			{
				object obj2 = methodInfo.Invoke(list, new object[1] { i });
				if (obj2 != null)
				{
					list2.Add(obj2);
				}
			}
		}
		catch
		{
		}
		return list2;
	}

	private static Type ResolveLoadedTypeByExactName(params string[] names)
	{
		if (names == null)
		{
			return null;
		}
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (Assembly assembly in assemblies)
		{
			foreach (string name in names)
			{
				try
				{
					Type type = assembly.GetType(name, throwOnError: false, ignoreCase: false);
					if (type != null)
					{
						return type;
					}
				}
				catch
				{
				}
			}
		}
		return null;
	}

	private static string DescribeValue(object value)
	{
		if (value == null)
		{
			return "null";
		}
		GameObject gameObject = ExtractGameObject(value);
		if (gameObject != null)
		{
			return gameObject.name + "@" + HierarchyPath(gameObject.transform);
		}
		try
		{
			return Convert.ToString(value);
		}
		catch
		{
			return value.GetType().Name;
		}
	}

	private static GameObject FindNativeActiveOperationsButton(Transform root)
	{
		if (root == null)
		{
			return null;
		}
		PanelButton[] array;
		try
		{
			array = root.GetComponentsInChildren<PanelButton>(includeInactive: true);
		}
		catch
		{
			return null;
		}
		GameObject gameObject = null;
		PanelButton[] array2 = array;
		foreach (PanelButton panelButton in array2)
		{
			if (!(panelButton == null))
			{
				string text = panelButton.gameObject.name ?? string.Empty;
				if (text.IndexOf("ACTIVE OPERATIONS BUTTON", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return panelButton.gameObject;
				}
				if (gameObject == null && text.IndexOf("ACTIVE", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					gameObject = panelButton.gameObject;
				}
			}
		}
		return gameObject;
	}

	private static GameObject FindNativeSimulationOperationsButton(Transform root)
	{
		if (root == null)
		{
			return null;
		}
		PanelButton[] array;
		try
		{
			array = root.GetComponentsInChildren<PanelButton>(includeInactive: true);
		}
		catch
		{
			return null;
		}
		PanelButton[] array2 = array;
		foreach (PanelButton panelButton in array2)
		{
			if (!(panelButton == null) && (panelButton.gameObject.name ?? string.Empty).IndexOf("OPERATION SIMULATION BUTTON", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return panelButton.gameObject;
			}
		}
		return null;
	}

	private static void PositionAsThirdTab(Transform parent, GameObject activeButton, GameObject simulationButton, GameObject moddedButton)
	{
		if (parent == null || moddedButton == null)
		{
			return;
		}
		RectTransform component = parent.GetComponent<RectTransform>();
		RectTransform component2 = moddedButton.GetComponent<RectTransform>();
		RectTransform rectTransform = ((simulationButton == null) ? null : simulationButton.GetComponent<RectTransform>());
		RectTransform rectTransform2 = ((activeButton == null) ? null : activeButton.GetComponent<RectTransform>());
		if (parent.GetComponent<HorizontalLayoutGroup>() != null)
		{
			int num = ((simulationButton == null) ? activeButton.transform.GetSiblingIndex() : simulationButton.transform.GetSiblingIndex());
			moddedButton.transform.SetSiblingIndex(Mathf.Min(num + 1, parent.childCount - 1));
			Canvas.ForceUpdateCanvases();
			logStatic("native tab row uses HorizontalLayoutGroup; inserted after simulation");
		}
		else
		{
			if (component2 == null || rectTransform2 == null || component == null)
			{
				return;
			}
			Bounds bounds = BoundsInParent(component, rectTransform2);
			Bounds bounds2 = ((rectTransform == null) ? new Bounds(new Vector3(bounds.max.x + bounds.size.x, bounds.center.y, 0f), bounds.size) : BoundsInParent(component, rectTransform));
			float x = bounds.min.x;
			float a = ((rectTransform == null) ? (bounds.max.x + bounds.size.x) : bounds2.max.x);
			float y = bounds.center.y;
			logStatic("native source rect=" + RectSummary(activeButton) + ", simulation rect=" + RectSummary(simulationButton));
			float num2 = float.PositiveInfinity;
			for (int i = 0; i < parent.childCount; i++)
			{
				Transform child = parent.GetChild(i);
				if (child == null || child == activeButton.transform || (simulationButton != null && child == simulationButton.transform) || child == moddedButton.transform || child.name.IndexOf("BRIEFING", StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}
				RectTransform component3 = child.GetComponent<RectTransform>();
				if (!(component3 == null))
				{
					Bounds bounds3 = BoundsInParent(component, component3);
					if (bounds3.min.x > x && bounds3.min.x < num2)
					{
						num2 = bounds3.min.x;
					}
				}
			}
			if (!float.IsPositiveInfinity(num2))
			{
				a = Mathf.Min(a, num2 - 12f);
			}
			a = Mathf.Min(a, component.rect.xMax - 8f);
			x = Mathf.Max(x, component.rect.xMin + 8f);
			float num3 = 8f;
			float num4 = (a - x - num3 * 2f) / 3f;
			if (num4 < 96f)
			{
				num4 = Mathf.Max(96f, Mathf.Min(bounds.size.x, bounds2.size.x));
			}
			if (num4 * 3f + num3 * 2f > component.rect.width - 16f)
			{
				num4 = Mathf.Max(96f, (component.rect.width - 16f - num3 * 2f) / 3f);
			}
			SetTabRect(rectTransform2, component, x + num4 * 0.5f, y, num4);
			if (rectTransform != null)
			{
				SetTabRect(rectTransform, component, x + num4 + num3 + num4 * 0.5f, y, num4);
			}
			SetTabRect(component2, component, x + (num4 + num3) * 2f + num4 * 0.5f, y, num4);
			int num5 = ((simulationButton == null) ? activeButton.transform.GetSiblingIndex() : simulationButton.transform.GetSiblingIndex());
			moddedButton.transform.SetSiblingIndex(Mathf.Min(num5 + 1, parent.childCount - 1));
			Canvas.ForceUpdateCanvases();
			logStatic("native tab row normalized in parent-local coordinates: rowLeft=" + x.ToString("F1") + ", rowRight=" + a.ToString("F1") + ", tabWidth=" + num4.ToString("F1") + ", parent=" + component.rect.size.ToString() + ");");
		}
	}

	private static void GetRectBoundsInParent(RectTransform rect, RectTransform parent, out float left, out float right, out float bottom, out float top)
	{
		Vector3[] array = new Vector3[4];
		rect.GetWorldCorners(array);
		left = float.PositiveInfinity;
		right = float.NegativeInfinity;
		bottom = float.PositiveInfinity;
		top = float.NegativeInfinity;
		for (int i = 0; i < array.Length; i++)
		{
			Vector3 vector = parent.InverseTransformPoint(array[i]);
			left = Mathf.Min(left, vector.x);
			right = Mathf.Max(right, vector.x);
			bottom = Mathf.Min(bottom, vector.y);
			top = Mathf.Max(top, vector.y);
		}
	}

	private static Bounds BoundsInParent(RectTransform parent, RectTransform child)
	{
		if (parent == null || child == null)
		{
			return new Bounds(Vector3.zero, Vector3.zero);
		}
		float width = child.rect.width;
		float height = child.rect.height;
		float num = child.localPosition.x - width * child.pivot.x;
		float num2 = child.localPosition.x + width * (1f - child.pivot.x);
		float num3 = child.localPosition.y - height * child.pivot.y;
		float num4 = child.localPosition.y + height * (1f - child.pivot.y);
		Vector3 center = new Vector3((num + num2) * 0.5f, (num3 + num4) * 0.5f, 0f);
		Vector3 size = new Vector3(Mathf.Max(0f, num2 - num), Mathf.Max(0f, num4 - num3), 0f);
		return new Bounds(center, size);
	}

	private static void SetTabRect(RectTransform rect, RectTransform parent, float centerX, float centerY, float width)
	{
		if (!(rect == null) && !(parent == null))
		{
			float y = Mathf.Max(1f, rect.rect.height);
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(width, y);
			Vector3 localPosition = rect.localPosition;
			localPosition.x = centerX;
			localPosition.y = centerY;
			rect.localPosition = localPosition;
		}
	}

	private static void CopyContentRect(GameObject source, RectTransform destination)
	{
		if (!(destination == null))
		{
			RectTransform rectTransform = ((source == null) ? null : source.GetComponent<RectTransform>());
			if (rectTransform == null)
			{
				destination.anchorMin = Vector2.zero;
				destination.anchorMax = Vector2.one;
				destination.offsetMin = Vector2.zero;
				destination.offsetMax = Vector2.zero;
			}
			else
			{
				destination.anchorMin = rectTransform.anchorMin;
				destination.anchorMax = rectTransform.anchorMax;
				destination.pivot = rectTransform.pivot;
				destination.anchoredPosition = rectTransform.anchoredPosition;
				destination.sizeDelta = rectTransform.sizeDelta;
				destination.localScale = rectTransform.localScale;
				destination.localRotation = rectTransform.localRotation;
			}
		}
	}

	private static void SetTabSelectedState(GameObject button, bool selected)
	{
		if (button == null)
		{
			return;
		}
		PanelButton component = button.GetComponent<PanelButton>();
		if (component != null)
		{
			component.isSelected = selected;
			component.SetSelected(selected);
			component.UpdateUI();
			return;
		}
		ButtonManager component2 = button.GetComponent<ButtonManager>();
		if (component2 != null)
		{
			component2.UpdateState();
		}
	}

	private static string RectSummary(GameObject go)
	{
		if (go == null)
		{
			return "null";
		}
		RectTransform component = go.GetComponent<RectTransform>();
		if (!(component == null))
		{
			return "anchored=" + component.anchoredPosition.ToString() + ", local=" + component.localPosition.ToString() + ", size=" + component.rect.size.ToString() + ", pivot=" + component.pivot.ToString() + ", parent=" + ((go.transform.parent == null) ? "null" : go.transform.parent.name);
		}
		return "no-rect";
	}

	private static void logStatic(string message)
	{
		if (instance != null && instance.log != null)
		{
			instance.log.LogInfo("Cerberus " + message + ".");
		}
	}

	private static void FitTabTitleText(GameObject button, string renderedTitle)
	{
		if (button == null || string.IsNullOrWhiteSpace(renderedTitle))
		{
			return;
		}
		int num = 0;
		try
		{
			foreach (TMP_Text componentsInChild in button.GetComponentsInChildren<TMP_Text>(includeInactive: true))
			{
				if (!(componentsInChild == null) && TitleEquals(componentsInChild.text, renderedTitle))
				{
					float num2 = Mathf.Max(1f, componentsInChild.fontSize);
					RectTransform rectTransform = componentsInChild.rectTransform;
					if (rectTransform != null)
					{
						Vector2 anchorMin = rectTransform.anchorMin;
						Vector2 anchorMax = rectTransform.anchorMax;
						Vector2 offsetMin = rectTransform.offsetMin;
						Vector2 offsetMax = rectTransform.offsetMax;
						rectTransform.anchorMin = new Vector2(0f, anchorMin.y);
						rectTransform.anchorMax = new Vector2(1f, anchorMax.y);
						rectTransform.offsetMin = new Vector2(12f, offsetMin.y);
						rectTransform.offsetMax = new Vector2(-12f, offsetMax.y);
					}
					componentsInChild.enableWordWrapping = false;
					componentsInChild.enableAutoSizing = true;
					componentsInChild.fontSizeMax = num2;
					componentsInChild.fontSizeMin = Mathf.Max(10f, num2 * 0.55f);
					componentsInChild.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
					num++;
				}
			}
		}
		catch
		{
		}
		Canvas.ForceUpdateCanvases();
		logStatic("title fit applied: button=" + button.name + ", renderedTitle='" + renderedTitle + "', copies=" + num);
	}

	private static bool TitleEquals(string value, string expected)
	{
		return string.Equals(NormalizeTitle(value), NormalizeTitle(expected), StringComparison.OrdinalIgnoreCase);
	}

	private static string NormalizeTitle(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		return string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
	}

	private static string HierarchyPath(Transform transform)
	{
		if (transform == null)
		{
			return "<null>";
		}
		System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
		Transform transform2 = transform;
		while (transform2 != null)
		{
			list.Add(transform2.name ?? "<unnamed>");
			transform2 = transform2.parent;
		}
		list.Reverse();
		return string.Join("/", list);
	}

	private static string DescribeActivity(GameObject gameObject)
	{
		if (!(gameObject == null))
		{
			return "activeSelf=" + gameObject.activeSelf + ", activeInHierarchy=" + gameObject.activeInHierarchy + ", path=" + HierarchyPath(gameObject.transform);
		}
		return "null";
	}

	private static GameObject FindDeep(Transform root, string name)
	{
		if (root == null)
		{
			return null;
		}
		if (root.name == name)
		{
			return root.gameObject;
		}
		for (int i = 0; i < root.childCount; i++)
		{
			GameObject gameObject = FindDeep(root.GetChild(i), name);
			if (gameObject != null)
			{
				return gameObject;
			}
		}
		return null;
	}

	private static void OpenModdedPage(MissionLaptop laptop, GameObject page)
	{
		if (laptop != null)
		{
			if (laptop.ActiveOperationsTab != null)
			{
				laptop.ActiveOperationsTab.SetActive(value: false);
			}
			if (laptop.SimulationOperationsTab != null)
			{
				laptop.SimulationOperationsTab.SetActive(value: false);
			}
		}
		if (page != null)
		{
			page.SetActive(value: true);
			GameObject gameObject = FindDeep(page.transform, "MODDED_HOME");
			GameObject gameObject2 = FindDeep(page.transform, "MODDED_BRIEFING");
			if (gameObject != null)
			{
				gameObject.SetActive(value: true);
			}
			if (gameObject2 != null)
			{
				gameObject2.SetActive(value: false);
			}
		}
	}

	private void DumpOperationSelectionHierarchy(MissionLaptop laptop)
	{
		try
		{
			GameObject activeOperationsTab = laptop.ActiveOperationsTab;
			if (activeOperationsTab == null)
			{
				log.LogWarning("Cerberus hierarchy probe: ActiveOperationsTab is null.");
				return;
			}
			log.LogInfo("Cerberus hierarchy probe: ActiveOperationsTab=" + Describe(activeOperationsTab) + ".");
			DumpHierarchy((activeOperationsTab.transform.parent == null) ? activeOperationsTab.transform : activeOperationsTab.transform.parent, 0, 3);
		}
		catch (Exception ex)
		{
			log.LogWarning("Cerberus hierarchy probe failed: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	private void DumpHierarchy(Transform node, int depth, int maxDepth)
	{
		if (node == null || depth > maxDepth)
		{
			return;
		}
		System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
		try
		{
			foreach (Component component in node.gameObject.GetComponents<Component>())
			{
				if (component != null)
				{
					list.Add(component.GetType().FullName);
				}
			}
		}
		catch
		{
		}
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		bool flag4 = false;
		try
		{
			flag = node.GetComponent<PanelButton>() != null;
		}
		catch
		{
		}
		try
		{
			flag2 = node.GetComponent<ButtonManager>() != null;
		}
		catch
		{
		}
		try
		{
			flag3 = node.GetComponent<Button>() != null;
		}
		catch
		{
		}
		try
		{
			flag4 = node.GetComponent<TMP_Text>() != null;
		}
		catch
		{
		}
		log.LogInfo("Cerberus hierarchy " + new string(' ', depth * 2) + node.name + " components=[" + string.Join(",", list) + "] typed panelButton=" + flag + " buttonManager=" + flag2 + " unityButton=" + flag3 + " tmp=" + flag4);
		for (int i = 0; i < node.childCount; i++)
		{
			DumpHierarchy(node.GetChild(i), depth + 1, maxDepth);
		}
	}

	private static string Describe(GameObject gameObject)
	{
		if (!(gameObject == null))
		{
			return gameObject.name + " parent=" + ((gameObject.transform.parent == null) ? "null" : gameObject.transform.parent.name);
		}
		return "null";
	}

	private static GameObject FindChild(Transform parent, string name)
	{
		if (parent == null)
		{
			return null;
		}
		for (int i = 0; i < parent.childCount; i++)
		{
			Transform child = parent.GetChild(i);
			if (child != null && child.name == name)
			{
				return child.gameObject;
			}
		}
		return null;
	}

	private static void SetButtonText(GameObject button, string text)
	{
		if (button == null)
		{
			return;
		}
		DisableLocalizationComponent(button);
		PanelButton component = button.GetComponent<PanelButton>();
		if (component != null)
		{
			component.buttonText = text;
			component.useLocalization = false;
			component.useCustomText = true;
		}
		ButtonManager component2 = button.GetComponent<ButtonManager>();
		if (component2 != null)
		{
			component2.buttonText = text;
			component2.useLocalization = false;
		}
		foreach (TMP_Text componentsInChild in button.GetComponentsInChildren<TMP_Text>(includeInactive: true))
		{
			if (!(componentsInChild == null))
			{
				DisableLocalizationComponent(componentsInChild.gameObject);
				componentsInChild.text = text;
			}
		}
	}
}
