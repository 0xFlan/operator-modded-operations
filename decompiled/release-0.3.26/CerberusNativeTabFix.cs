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
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using Michsky.DreamOS;
using Mirror;
using OperatorModAPI;
using Pathfinding;
using TMPro;
using TMPro.Examples;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[BepInPlugin("operator.modded-operations", "OPERATOR: Modded Operations", "0.3.26")]
[BepInProcess("OPERATOR.exe")]
[BepInDependency("operator.modapi", "0.2.0-alpha.4")]
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

		public readonly List<AssetBundle> Dependencies = new List<AssetBundle>();

		public readonly Dictionary<string, AssetBundle> DependenciesByPath = new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);

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

		public SceneVariantSelection SceneSelection;

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

		public SceneVariantSelection SceneSelection;

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

		public bool NetworkSpawnFailed;

		public readonly List<OwnedBootstrapSyncObjects> BootstrapSyncObjects = new List<OwnedBootstrapSyncObjects>();

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

		public readonly List<TerrainLayer> RuntimeTerrainLayers = new List<TerrainLayer>();

		public readonly Dictionary<int, int> PositionedPlayerObjects = new Dictionary<int, int>();

		public readonly Dictionary<int, string> PlayerMarkerNames = new Dictionary<int, string>();

		public readonly Dictionary<int, int> PlayerSpawnRequestFrames = new Dictionary<int, int>();

		public readonly Dictionary<int, int> PlayerSpawnRequestCounts = new Dictionary<int, int>();

		public readonly HashSet<int> CompletedPlayerSpawnIds = new HashSet<int>();

		public readonly Dictionary<int, int> PlayerMoveRequestFrames = new Dictionary<int, int>();

		public readonly Dictionary<int, int> WeaponAuthorityRequestFrames = new Dictionary<int, int>();

		public readonly Dictionary<int, int> WeaponAuthorityRequestCounts = new Dictionary<int, int>();

		public readonly HashSet<int> ConfirmedWeaponAuthorityIds = new HashSet<int>();

		public List<SpawnPoint> PreviousSpawnPoints;

		public List<SpawnPoint> OwnedSpawnPoints;

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

		public readonly List<VolumeProfile> RuntimeRenderProfiles = new List<VolumeProfile>();

		public bool PveSpawnAttempted;

		public int PveEnemyCount;

		public RaidManager PveRaidManager;

		public ExfilZone PveExfilZone;

		public BoxCollider PveExfilCollider;

		public readonly HashSet<int> ProfiledPvePreexistingBrainIds = new HashSet<int>();

		public readonly List<BrainAI> ProfiledPveDiagnosticBrains = new List<BrainAI>();

		public readonly Dictionary<int, Vector3> ProfiledPveInitialBrainPositions = new Dictionary<int, Vector3>();

		public bool ProfiledPveInitialPlayerPositionCaptured;

		public Vector3 ProfiledPveInitialPlayerPosition;

		public float ProfiledPveAiDiagnosticStartedAt = -1f;

		public float ProfiledPveAiDiagnosticNextProbeAt = -1f;

		public int ProfiledPveAiDiagnosticSnapshotIndex;

		public bool ProfiledPveAiDiagnosticComplete;

		public bool ProfiledPveAiDiagnosticAwaitingBrains;

		public bool ProfiledPveNativeContractLogged;

		public readonly HashSet<int> ProfiledPveInitialWanderDelayHandledBrainIds = new HashSet<int>();

		public int ProfiledPveInitialWanderDelayAdvanced;

		public int ProfiledPveInitialWanderDelayPreserved;

		public int ProfiledPveInitialWanderDelaySkipped;

		public readonly List<Object> RuntimePveAssets = new List<Object>();

		public readonly List<Object> RuntimePvpAssets = new List<Object>();
	}

	private sealed class OwnedBootstrapSyncObjects
	{
		public NetworkBehaviour Behaviour;

		public List<SyncObject> Value;
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
				((PvpGameode)this).Server_AllPlayersLoaded();
				instance?.OnStandaloneAllPlayersLoaded(nativePvpLifecycle: true);
			}
			catch (Exception exception)
			{
				instance?.OnStandalonePvpAllPlayersLoadedFailed(exception);
			}
		}

		public void EnsureStandaloneReadiness(string source)
		{
			EnterNativeReadiness(source);
		}

		private void EnterNativeReadiness(string source)
		{
			if (instance == null || !instance.TryClaimStandaloneReadinessInitialization((GameMode)(object)this))
			{
				return;
			}
			try
			{
				((PvpGameode)this).OnStartClient();
				instance.MarkStandaloneReadinessInitialized((GameMode)(object)this, source);
			}
			catch (Exception exception)
			{
				instance.MarkStandaloneReadinessInitializationFailed((GameMode)(object)this, source, exception);
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
			instance?.OnStandaloneAllPlayersLoaded(nativePvpLifecycle: false);
		}

		public void EnsureStandaloneReadiness(string source)
		{
			if (instance == null || !instance.TryClaimStandaloneReadinessInitialization((GameMode)(object)this))
			{
				return;
			}
			try
			{
				((GameMode)this).Initialize();
				instance.MarkStandaloneReadinessInitialized((GameMode)(object)this, source);
			}
			catch (Exception exception)
			{
				instance.MarkStandaloneReadinessInitializationFailed((GameMode)(object)this, source, exception);
			}
		}
	}

	internal const string RequiredApiVersion = "0.2.0-alpha.4";

	private const uint StandalonePveGameModeAssetId = 1297043457u;

	private const uint StandalonePvpGameModeAssetId = 1297043458u;

	private const string StandalonePveExfilMarkerPrefix = "PVE_ExfilZone_";

	private const float StandalonePveExtractionSeconds = 15f;

	private static readonly float[] ProfiledPveAiDiagnosticSnapshotSeconds = new float[6] { 0f, 10f, 30f, 60f, 90f, 120f };

	private static CerberusNativeTabFix instance;

	private ManualLogSource log;

	private FixRunner runner;

	private readonly Dictionary<string, LoadedMapBundles> loadedMapBundles = new Dictionary<string, LoadedMapBundles>(StringComparer.Ordinal);

	private readonly Dictionary<string, Sprite> previewSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);

	private readonly Dictionary<string, Texture2D> previewTextures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

	private readonly Dictionary<string, Texture3D> packageTonemapLuts = new Dictionary<string, Texture3D>(StringComparer.Ordinal);

	private GameObject operationBoardVisualTemplate;

	private readonly Dictionary<int, CatalogPresentation> catalogPresentations = new Dictionary<int, CatalogPresentation>();

	private PendingMapLaunch pendingLaunch;

	private ActiveMapOperation activeOperation;

	private SceneVariantSelectionStore sceneVariantSelectionStore;

	private UnityAction<Scene, LoadSceneMode> sceneLoadedCallback;

	private UnityAction<Scene> sceneUnloadedCallback;

	private readonly HashSet<int> attachedLaptops = new HashSet<int>();

	private readonly HashSet<int> deferredSetupLoggedLaptops = new HashSet<int>();

	private readonly HashSet<string> lutDiagnostics = new HashSet<string>(StringComparer.Ordinal);

	private readonly HashSet<int> nativeBackBoundBoards = new HashSet<int>();

	private MethodInfo directServerPlayerSpawnMethod;

	private readonly List<NativePresentationBinding> nativePresentationBindings = new List<NativePresentationBinding>();

	private readonly List<PendingTransitionSnapshot> pendingTransitionSnapshots = new List<PendingTransitionSnapshot>();

	private string lastDiagnostic;

	public override void Load()
	{
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Expected O, but got Unknown
		if (!string.Equals(OperatorApi.ApiVersion, "0.2.0-alpha.4", StringComparison.Ordinal))
		{
			((BasePlugin)this).Log.LogError((object)("Modded Operations requires Operator Mod API 0.2.0-alpha.4, but Core exposes " + OperatorApi.ApiVersion + ". Adapter startup was refused."));
			return;
		}
		directServerPlayerSpawnMethod = typeof(PlayerMaster).GetMethod("UserCode_CMDSpawnPlayer__NetworkIdentity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(NetworkIdentity) }, null);
		if (directServerPlayerSpawnMethod == null)
		{
			((BasePlugin)this).Log.LogError((object)"Modded Operations could not resolve the exact current-build PlayerMaster generated server spawn body. Adapter startup was refused.");
			return;
		}
		instance = this;
		log = ((BasePlugin)this).Log;
		ClassInjector.RegisterTypeInIl2Cpp<FixRunner>();
		ClassInjector.RegisterTypeInIl2Cpp<StandalonePvpGameMode>();
		ClassInjector.RegisterTypeInIl2Cpp<StandalonePveGameMode>();
		GameObject val = new GameObject("Operator_Cerberus_Native_Tab_Fix_Runner");
		Object.DontDestroyOnLoad((Object)(object)val);
		runner = val.AddComponent<FixRunner>();
		((MonoBehaviour)runner).InvokeRepeating("Tick", 1f, 1f);
		sceneLoadedCallback = DelegateSupport.ConvertDelegate<UnityAction<Scene, LoadSceneMode>>((Delegate)new Action<Scene, LoadSceneMode>(OnSceneLoaded));
		sceneUnloadedCallback = DelegateSupport.ConvertDelegate<UnityAction<Scene>>((Delegate)new Action<Scene>(OnSceneUnloaded));
		SceneManager.sceneLoaded += sceneLoadedCallback;
		SceneManager.sceneUnloaded += sceneUnloadedCallback;
		log.LogInfo((object)("Modded Operations infrastructure loaded; catalog=" + OperatorApi.ModdedOperations.CatalogId + ", maps=" + OperatorApi.ModdedOperations.Maps.Count + ", operations=" + OperatorApi.ModdedOperations.Operations.Count + "."));
	}

	public static T LoadVerifiedMapDependencyAsset<T>(string mapId, string assetPath) where T : Object
	{
		if (instance == null || string.IsNullOrWhiteSpace(mapId) || string.IsNullOrWhiteSpace(assetPath))
		{
			return default(T);
		}
		return instance.LoadVerifiedMapDependencyAssetInternal<T>(mapId, assetPath);
	}

	private T LoadVerifiedMapDependencyAssetInternal<T>(string mapId, string assetPath) where T : Object
	{
		if (!loadedMapBundles.TryGetValue(mapId, out var value) || value == null)
		{
			return default(T);
		}
		foreach (AssetBundle dependency in value.Dependencies)
		{
			if ((Object)(object)dependency == (Object)null)
			{
				continue;
			}
			try
			{
				T val = dependency.LoadAsset<T>(assetPath);
				if ((Object)(object)val != (Object)null)
				{
					return val;
				}
			}
			catch
			{
			}
		}
		return default(T);
	}

	private void TrimCompletedMapBundleCacheAtSafeBoundary(string retainedMapId, string source)
	{
		if (string.IsNullOrWhiteSpace(retainedMapId) || !IsSafeMapBundleEvictionBoundary())
		{
			return;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal) { retainedMapId };
		ActiveMapOperation activeMapOperation = activeOperation;
		object value;
		if (activeMapOperation == null)
		{
			value = null;
		}
		else
		{
			ModdedMapDefinition map = activeMapOperation.Map;
			value = ((map != null) ? map.Id : null);
		}
		if (!string.IsNullOrWhiteSpace((string?)value))
		{
			hashSet.Add(activeOperation.Map.Id);
		}
		PendingMapLaunch pendingMapLaunch = pendingLaunch;
		object value2;
		if (pendingMapLaunch == null)
		{
			value2 = null;
		}
		else
		{
			ModdedMapDefinition map2 = pendingMapLaunch.Map;
			value2 = ((map2 != null) ? map2.Id : null);
		}
		if (!string.IsNullOrWhiteSpace((string?)value2))
		{
			hashSet.Add(pendingLaunch.Map.Id);
		}
		List<string> list = new List<string>();
		KeyValuePair<string, LoadedMapBundles>[] array = loadedMapBundles.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			KeyValuePair<string, LoadedMapBundles> keyValuePair = array[i];
			if (!hashSet.Contains(keyValuePair.Key))
			{
				if (BundleReferencesLoadedScene(keyValuePair.Value))
				{
					log.LogWarning((object)("Modded Operations deferred bundle cache eviction because a declared package scene is still loaded: map=" + keyValuePair.Key + ", source=" + source + "."));
				}
				else
				{
					UnloadMapBundles(keyValuePair.Value);
					loadedMapBundles.Remove(keyValuePair.Key);
					list.Add(keyValuePair.Key);
				}
			}
		}
		if (list.Count > 0)
		{
			log.LogInfo((object)("Modded Operations evicted prior completed map bundles at the safe Operation Room boundary: retained=" + string.Join(",", hashSet.OrderBy((string id) => id)) + ", evicted=" + string.Join(",", list.OrderBy((string id) => id)) + ", source=" + source + "."));
		}
	}

	private bool IsSafeMapBundleEvictionBoundary()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		Scene activeScene = SceneManager.GetActiveScene();
		if (!((Scene)(ref activeScene)).IsValid() || !((Scene)(ref activeScene)).isLoaded || !string.Equals(((Scene)(ref activeScene)).name, "Operation Room", StringComparison.Ordinal))
		{
			return false;
		}
		if (activeOperation != null && activeOperation.SceneHandle != 0)
		{
			return false;
		}
		foreach (LoadedMapBundles value in loadedMapBundles.Values)
		{
			if (BundleReferencesLoadedScene(value))
			{
				return false;
			}
		}
		return !BundleReferencesLoadedScene(pendingLaunch?.LoadingBundles);
	}

	private static bool BundleReferencesLoadedScene(LoadedMapBundles bundles)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)bundles?.SceneBundle == (Object)null)
		{
			return false;
		}
		string[] array;
		try
		{
			array = Il2CppArrayBase<string>.op_Implicit((Il2CppArrayBase<string>)(object)bundles.SceneBundle.GetAllScenePaths());
		}
		catch
		{
			return true;
		}
		if (array == null || array.Length == 0)
		{
			return false;
		}
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			Scene sceneAt = SceneManager.GetSceneAt(i);
			if (!((Scene)(ref sceneAt)).IsValid() || !((Scene)(ref sceneAt)).isLoaded)
			{
				continue;
			}
			string[] array2 = array;
			foreach (string text in array2)
			{
				if (!string.IsNullOrEmpty(((Scene)(ref sceneAt)).path) && string.Equals(((Scene)(ref sceneAt)).path, text, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
				if (string.IsNullOrEmpty(((Scene)(ref sceneAt)).path) && string.Equals(((Scene)(ref sceneAt)).name, Path.GetFileNameWithoutExtension(text), StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static void UnloadMapBundles(LoadedMapBundles bundles)
	{
		if (bundles == null)
		{
			return;
		}
		try
		{
			AssetBundle sceneBundle = bundles.SceneBundle;
			if (sceneBundle != null)
			{
				sceneBundle.Unload(false);
			}
		}
		catch
		{
		}
		foreach (AssetBundle dependency in bundles.Dependencies)
		{
			try
			{
				if (dependency != null)
				{
					dependency.Unload(false);
				}
			}
			catch
			{
			}
		}
	}

	private bool TryDiscardStaleCompletedMapBundle(string mapId, LoadedMapBundles bundles, string source)
	{
		ActiveMapOperation activeMapOperation = activeOperation;
		object a;
		if (activeMapOperation == null)
		{
			a = null;
		}
		else
		{
			ModdedMapDefinition map = activeMapOperation.Map;
			a = ((map != null) ? map.Id : null);
		}
		bool flag = string.Equals((string?)a, mapId, StringComparison.Ordinal);
		PendingMapLaunch pendingMapLaunch = pendingLaunch;
		object a2;
		if (pendingMapLaunch == null)
		{
			a2 = null;
		}
		else
		{
			ModdedMapDefinition map2 = pendingMapLaunch.Map;
			a2 = ((map2 != null) ? map2.Id : null);
		}
		bool flag2 = string.Equals((string?)a2, mapId, StringComparison.Ordinal);
		if (flag || flag2 || BundleReferencesLoadedScene(bundles))
		{
			log.LogWarning((object)("Modded Operations refused to discard a stale bundle cache entry while it still has a live owner: map=" + mapId + ", activeRestartOwner=" + flag + ", pendingPrefetchOwner=" + flag2 + ", source=" + source + "."));
			return false;
		}
		UnloadMapBundles(bundles);
		loadedMapBundles.Remove(mapId);
		log.LogWarning((object)("Modded Operations discarded an incomplete or stale bundle cache entry: map=" + mapId + ", source=" + source + "."));
		return true;
	}

	public override bool Unload()
	{
		try
		{
			if ((Delegate)(object)sceneLoadedCallback != (Delegate)null)
			{
				SceneManager.sceneLoaded -= sceneLoadedCallback;
			}
			if ((Delegate)(object)sceneUnloadedCallback != (Delegate)null)
			{
				SceneManager.sceneUnloaded -= sceneUnloadedCallback;
			}
			sceneLoadedCallback = null;
			sceneUnloadedCallback = null;
			foreach (CatalogPresentation value in catalogPresentations.Values)
			{
				if ((Object)(object)value?.PreparationPanel != (Object)null)
				{
					Object.Destroy((Object)(object)value.PreparationPanel);
				}
				if ((Object)(object)value?.Page != (Object)null)
				{
					Object.Destroy((Object)(object)value.Page);
				}
				if ((Object)(object)value?.NativeBoardData != (Object)null)
				{
					Object.Destroy((Object)(object)value.NativeBoardData);
				}
				if ((Object)(object)value?.NativeTargetData != (Object)null)
				{
					Object.Destroy((Object)(object)value.NativeTargetData);
				}
				if ((Object)(object)value?.NativeInfiltrationMapPrefab != (Object)null)
				{
					Object.Destroy((Object)(object)value.NativeInfiltrationMapPrefab);
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
					if (value2 != null)
					{
						AssetBundle sceneBundle = value2.SceneBundle;
						if (sceneBundle != null)
						{
							sceneBundle.Unload(false);
						}
					}
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
						if (dependency != null)
						{
							dependency.Unload(false);
						}
					}
					catch
					{
					}
				}
			}
			loadedMapBundles.Clear();
			foreach (Sprite value3 in previewSprites.Values)
			{
				if ((Object)(object)value3 != (Object)null)
				{
					Object.Destroy((Object)(object)value3);
				}
			}
			foreach (Texture2D value4 in previewTextures.Values)
			{
				if ((Object)(object)value4 != (Object)null)
				{
					Object.Destroy((Object)(object)value4);
				}
			}
			previewSprites.Clear();
			previewTextures.Clear();
			foreach (Texture3D value5 in packageTonemapLuts.Values)
			{
				if ((Object)(object)value5 != (Object)null)
				{
					Object.Destroy((Object)(object)value5);
				}
			}
			packageTonemapLuts.Clear();
			ReleaseStandaloneSceneContracts(activeOperation);
			ReleaseRuntimeTerrain(activeOperation);
			if ((Object)(object)activeOperation?.BootstrapRoot != (Object)null)
			{
				Object.Destroy((Object)(object)activeOperation.BootstrapRoot);
			}
			activeOperation = null;
			pendingLaunch = null;
			sceneVariantSelectionStore = null;
			if ((Object)(object)runner != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)runner).gameObject);
			}
			runner = null;
			instance = null;
			return true;
		}
		catch (Exception ex)
		{
			ManualLogSource obj3 = log;
			if (obj3 != null)
			{
				obj3.LogError((object)("Modded Operations unload failed: " + ex));
			}
			return false;
		}
	}

	private void TryAttachAll()
	{
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		Type type = ResolveMissionLaptopType();
		if (type == null)
		{
			LogOnce("MissionLaptop type was not found in the loaded interop assemblies.", warning: true);
			return;
		}
		foreach (Component item in FindMissionLaptopComponents(type))
		{
			MissionLaptop val = (MissionLaptop)(object)((item is MissionLaptop) ? item : null);
			if ((Object)(object)val == (Object)null || (Object)(object)((Component)val).gameObject == (Object)null || (Object)(object)val.cerberusWindowPanelManager == (Object)null)
			{
				continue;
			}
			Scene scene = ((Component)val).gameObject.scene;
			if (!((Scene)(ref scene)).IsValid() || !((Scene)(ref scene)).isLoaded || string.IsNullOrWhiteSpace(((Scene)(ref scene)).path))
			{
				continue;
			}
			int instanceID = ((Object)val).GetInstanceID();
			if (attachedLaptops.Contains(instanceID))
			{
				Transform val2 = (((Object)(object)val.ActiveOperationsTab == (Object)null) ? null : val.ActiveOperationsTab.transform.parent);
				GameObject val3 = FindDeep(val2, "MODDED_OPS_NATIVE_TAB") ?? FindDeep(val2, "MODDED_OPS_TAB");
				if ((Object)(object)val3 != (Object)null)
				{
					GameObject val4 = FindChild(val2, "MODDED_OPERATIONS_PAGE");
					GameObject obj = (((Object)(object)val4 == (Object)null) ? null : FindDeep(val4.transform, "MODDED_NATIVE_HOME"));
					GameObject val5 = FindNativeModdedPreparationPanel(val);
					bool flag = OperatorApi.ModdedOperations.Operations.Count == 0;
					if (!((Object)(object)obj == (Object)null) && (flag || !((Object)(object)val5 == (Object)null)))
					{
						SetButtonText(val3, "MODDED OPERATIONS");
						continue;
					}
					attachedLaptops.Remove(instanceID);
				}
				attachedLaptops.Remove(instanceID);
			}
			if (TryAttachNativeTab(val))
			{
				attachedLaptops.Add(instanceID);
			}
		}
	}

	private void LogOnce(string message, bool warning)
	{
		if (string.Equals(lastDiagnostic, message, StringComparison.Ordinal))
		{
			return;
		}
		lastDiagnostic = message;
		if (warning)
		{
			ManualLogSource obj = log;
			if (obj != null)
			{
				obj.LogWarning((object)("Cerberus native tab fix: " + message));
			}
		}
		else
		{
			ManualLogSource obj2 = log;
			if (obj2 != null)
			{
				obj2.LogInfo((object)("Cerberus native tab fix: " + message));
			}
		}
	}

	private static List<Component> FindMissionLaptopComponents(Type laptopType)
	{
		List<Component> list = new List<Component>(4);
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
			foreach (object item in enumerable)
			{
				Component val = (Component)((item is Component) ? item : null);
				if (val != null && !list.Contains(val))
				{
					list.Add(val);
					continue;
				}
				Component existingComponentByManagedType = GetExistingComponentByManagedType(ExtractGameObject(item), laptopType);
				if ((Object)(object)existingComponentByManagedType != (Object)null && !list.Contains(existingComponentByManagedType))
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
		if ((Object)(object)gameObject == (Object)null || componentType == null)
		{
			return null;
		}
		MethodInfo[] methods = typeof(GameObject).GetMethods(BindingFlags.Instance | BindingFlags.Public);
		foreach (MethodInfo methodInfo in methods)
		{
			if (methodInfo.Name == "GetComponent" && methodInfo.IsGenericMethodDefinition && methodInfo.GetGenericArguments().Length == 1 && methodInfo.GetParameters().Length == 0)
			{
				object? obj = methodInfo.MakeGenericMethod(componentType).Invoke(gameObject, null);
				return (Component)((obj is Component) ? obj : null);
			}
		}
		return null;
	}

	private static GameObject ExtractGameObject(object value)
	{
		GameObject val = (GameObject)((value is GameObject) ? value : null);
		if (val != null)
		{
			return val;
		}
		Component val2 = (Component)((value is Component) ? value : null);
		if (val2 != null)
		{
			return val2.gameObject;
		}
		if (value == null)
		{
			return null;
		}
		try
		{
			Type type = value.GetType();
			object? obj = (type.GetProperty("gameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? type.GetProperty("GameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))?.GetValue(value);
			return (GameObject)((obj is GameObject) ? obj : null);
		}
		catch
		{
			return null;
		}
	}

	private bool TryAttachNativeTab(MissionLaptop laptop)
	{
		//IL_0380: Unknown result type (might be due to invalid IL or missing references)
		//IL_038a: Expected O, but got Unknown
		//IL_03a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ab: Expected O, but got Unknown
		try
		{
			int laptopId = ((!((Object)(object)laptop == (Object)null)) ? ((Object)laptop).GetInstanceID() : 0);
			Transform val = (((Object)(object)laptop.ActiveOperationsTab == (Object)null) ? null : laptop.ActiveOperationsTab.transform.parent);
			GameObject sourceButton = FindNativeActiveOperationsButton(val);
			if ((Object)(object)sourceButton == (Object)null || (Object)(object)sourceButton.transform.parent == (Object)null)
			{
				log.LogWarning((object)"Cerberus native tab fix could not find the in-panel ACTIVE OPERATIONS BUTTON.");
				return false;
			}
			GameObject page = GetOrCreateCatalogPage(laptop, val);
			if ((Object)(object)page == (Object)null)
			{
				LogOnce("waiting for same-owner Modded Operations page under laptopId=" + laptopId + ", parent=" + HierarchyPath(val) + ".", warning: false);
				return false;
			}
			Transform parent = sourceButton.transform.parent;
			GameObject val2 = FindDeep(parent, "MODDED_OPS_TAB_BUTTON");
			if ((Object)(object)val2 != (Object)null)
			{
				Object.Destroy((Object)(object)val2);
			}
			GameObject val3 = FindDeep(parent, "MODDED_OPS_TAB");
			if ((Object)(object)val3 != (Object)null)
			{
				Object.Destroy((Object)(object)val3);
			}
			GameObject simulationButton = FindNativeSimulationOperationsButton(val);
			GameObject duplicate = FindDeep(parent, "MODDED_OPS_NATIVE_TAB");
			if ((Object)(object)duplicate != (Object)null)
			{
				if (!BuildNativeModdedPresentation(laptop, page, sourceButton))
				{
					return false;
				}
				SetButtonText(duplicate, "MODDED OPERATIONS");
				RegisterNativePresentationIsolation(laptop, page, duplicate, sourceButton, simulationButton);
				log.LogInfo((object)"Cerberus native tab already present; rebuilt or retained its native content.");
				return true;
			}
			RectTransform component = page.GetComponent<RectTransform>();
			if ((Object)(object)component != (Object)null)
			{
				CopyContentRect(laptop.ActiveOperationsTab, component);
				int num = (((Object)(object)laptop.ActiveOperationsTab == (Object)null) ? (parent.childCount - 1) : laptop.ActiveOperationsTab.transform.GetSiblingIndex());
				page.transform.SetSiblingIndex(Mathf.Clamp(num, 0, page.transform.parent.childCount - 1));
			}
			if (!BuildNativeModdedPresentation(laptop, page, sourceButton))
			{
				return false;
			}
			duplicate = Object.Instantiate<GameObject>(sourceButton, parent);
			((Object)duplicate).name = "MODDED_OPS_NATIVE_TAB";
			duplicate.SetActive(true);
			SetButtonText(duplicate, "MODDED OPERATIONS");
			PositionAsThirdTab(parent, sourceButton, simulationButton, duplicate);
			FitTabTitleText(sourceButton, "ACTIVE OPERATIONS");
			FitTabTitleText(simulationButton, "OPERATION SIMULATION");
			FitTabTitleText(duplicate, "MODDED OPERATIONS");
			page.SetActive(false);
			if ((Object)(object)page.GetComponent<Animator>() == (Object)null)
			{
				page.AddComponent<Animator>();
			}
			PanelButton component2 = duplicate.GetComponent<PanelButton>();
			ButtonManager component3 = duplicate.GetComponent<ButtonManager>();
			UnityEvent val4 = null;
			if ((Object)(object)component2 != (Object)null)
			{
				component2.onClick = new UnityEvent();
				val4 = component2.onClick;
			}
			else if ((Object)(object)component3 != (Object)null)
			{
				component3.onClick = new UnityEvent();
				val4 = component3.onClick;
			}
			Button val5 = null;
			try
			{
				val5 = duplicate.GetComponent<Button>();
			}
			catch
			{
				val5 = null;
			}
			if ((Object)(object)val5 == (Object)null)
			{
				try
				{
					val5 = duplicate.GetComponentInChildren<Button>(true);
				}
				catch
				{
					val5 = null;
				}
			}
			if (val4 == null && (Object)(object)val5 == (Object)null)
			{
				log.LogWarning((object)"Cerberus native tab clone had no DreamOS click event.");
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
						log.LogInfo((object)("Cerberus native MODDED OPS duplicate click surface suppressed: laptopId=" + laptopId + ", source=" + eventSource + ", frame=" + frameCount + "."));
					}
					else
					{
						lastLogicalOpenFrame = frameCount;
						log.LogInfo((object)CaptureLaptopTransitionState(laptop, page, "before MODDED OPS", eventSource, frameCount));
						SetTabSelectedState(sourceButton, selected: false);
						SetTabSelectedState(simulationButton, selected: false);
						SetButtonText(duplicate, "MODDED OPERATIONS");
						SetTabSelectedState(duplicate, selected: true);
						OpenModdedPage(laptop, page);
						MarkNativeModdedPageOpened(laptopId, frameCount);
						log.LogInfo((object)("Cerberus native MODDED OPS logical transition: laptopId=" + laptopId + ", source=" + eventSource + ", frame=" + frameCount + ", pageActiveSelf=" + page.activeSelf + ", pageActiveInHierarchy=" + page.activeInHierarchy + ", ownerCanvas=" + DescribeActivity(laptop.osCanvas) + ", pageParent=" + HierarchyPath(page.transform.parent) + "."));
						QueueTransitionSnapshot(laptop, page, "after MODDED OPS via " + eventSource, frameCount);
					}
				}
				catch (Exception ex2)
				{
					log.LogWarning((object)("Cerberus native MODDED OPS click failed: " + ex2.GetType().Name + ": " + ex2.Message));
				}
			};
			if (val4 != null)
			{
				((UnityEventBase)val4).RemoveAllListeners();
				val4.AddListener(UnityAction.op_Implicit((Action)delegate
				{
					openModdedPage("DreamOS.PanelButton");
				}));
			}
			if ((Object)(object)val5 != (Object)null)
			{
				((UnityEventBase)val5.onClick).RemoveAllListeners();
				((UnityEvent)val5.onClick).AddListener(UnityAction.op_Implicit((Action)delegate
				{
					openModdedPage("UnityEngine.UI.Button");
				}));
				((Selectable)val5).interactable = true;
			}
			if ((Object)(object)component2 != (Object)null)
			{
				component2.isInteractable = true;
			}
			RegisterNativePresentationIsolation(laptop, page, duplicate, sourceButton, simulationButton);
			int num2 = 0;
			try
			{
				num2 = duplicate.GetComponentsInChildren<Graphic>(true).Length;
			}
			catch
			{
			}
			log.LogInfo((object)("Cerberus native MODDED OPS tab inserted as third native Operation Selection tab; source=" + ((Object)sourceButton).name + ", simulation=" + (((Object)(object)simulationButton == (Object)null) ? "null" : ((Object)simulationButton).name) + ", visible=" + duplicate.activeInHierarchy + ", unityButton=" + ((Object)(object)val5 != (Object)null) + ", targetGraphic=" + (((Object)(object)val5 == (Object)null || (Object)(object)((Selectable)val5).targetGraphic == (Object)null) ? "null" : ((Object)((Component)((Selectable)val5).targetGraphic).gameObject).name) + ", graphics=" + num2 + ", rect=" + RectSummary(duplicate) + "."));
			return true;
		}
		catch (Exception ex)
		{
			log.LogWarning((object)("Cerberus native tab fix failed: " + ex.ToString()));
			return false;
		}
	}

	private void RegisterNativePresentationIsolation(MissionLaptop laptop, GameObject page, GameObject moddedButton, GameObject activeButton, GameObject simulationButton)
	{
		if ((Object)(object)laptop == (Object)null || (Object)(object)page == (Object)null || (Object)(object)moddedButton == (Object)null)
		{
			return;
		}
		int instanceID = ((Object)laptop).GetInstanceID();
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
			if (nativePresentationBinding == null || (Object)(object)nativePresentationBinding.Laptop == (Object)null || (Object)(object)nativePresentationBinding.Page == (Object)null || (Object)(object)nativePresentationBinding.ModdedButton == (Object)null)
			{
				nativePresentationBindings.RemoveAt(num);
			}
			else
			{
				bool flag = ((Object)(object)nativePresentationBinding.Laptop.ActiveOperationsTab != (Object)null && nativePresentationBinding.Laptop.ActiveOperationsTab.activeSelf) || ((Object)(object)nativePresentationBinding.Laptop.SimulationOperationsTab != (Object)null && nativePresentationBinding.Laptop.SimulationOperationsTab.activeSelf);
				bool flag2 = flag || IsNativePanelButtonSelected(nativePresentationBinding.ActiveButton) || IsNativePanelButtonSelected(nativePresentationBinding.SimulationButton);
				if (nativePresentationBinding.Page.activeSelf && flag2 && Time.frameCount > nativePresentationBinding.LastModdedOpenFrame + 1)
				{
					nativePresentationBinding.Page.SetActive(false);
					if ((Object)(object)nativePresentationBinding.PreparationPanel != (Object)null)
					{
						nativePresentationBinding.PreparationPanel.SetActive(false);
					}
					SetTabSelectedState(nativePresentationBinding.ModdedButton, selected: false);
					log.LogInfo((object)("Cerberus isolated modded overlay closed after an official tab became selected; laptopId=" + nativePresentationBinding.LaptopId + ", officialPageActive=" + flag + "."));
				}
			}
		}
	}

	private static bool IsNativePanelButtonSelected(GameObject button)
	{
		if ((Object)(object)button == (Object)null)
		{
			return false;
		}
		try
		{
			PanelButton component = button.GetComponent<PanelButton>();
			return (Object)(object)component != (Object)null && component.isSelected;
		}
		catch
		{
			return false;
		}
	}

	private static GameObject FindNativeModdedPreparationPanel(MissionLaptop laptop)
	{
		if ((Object)(object)laptop == (Object)null || (Object)(object)laptop.opBoardParent == (Object)null)
		{
			return null;
		}
		Transform parent = laptop.opBoardParent.parent;
		return FindDeep((((Object)(object)parent == (Object)null) ? null : parent.parent) ?? parent ?? laptop.opBoardParent, "MODDED_NATIVE_OPERATION_PREPARATION");
	}

	private static GameObject GetOrCreateCatalogPage(MissionLaptop laptop, Transform operationSelection)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Expected O, but got Unknown
		if ((Object)(object)laptop == (Object)null || (Object)(object)operationSelection == (Object)null || (Object)(object)laptop.ActiveOperationsTab == (Object)null)
		{
			return null;
		}
		GameObject val = FindChild(operationSelection, "MODDED_OPERATIONS_PAGE");
		if ((Object)(object)val != (Object)null)
		{
			return val;
		}
		GameObject val2 = new GameObject("MODDED_OPERATIONS_PAGE");
		RectTransform destination = val2.AddComponent<RectTransform>();
		val2.transform.SetParent(operationSelection, false);
		CopyContentRect(laptop.ActiveOperationsTab, destination);
		val2.transform.SetSiblingIndex(laptop.ActiveOperationsTab.transform.GetSiblingIndex());
		val2.SetActive(false);
		return val2;
	}

	private bool BuildNativeModdedPresentation(MissionLaptop laptop, GameObject page, GameObject nativeButtonTemplate)
	{
		//IL_036e: Unknown result type (might be due to invalid IL or missing references)
		//IL_037a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0386: Unknown result type (might be due to invalid IL or missing references)
		//IL_0395: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)laptop == (Object)null || (Object)(object)page == (Object)null || (Object)(object)laptop.ActiveOperationsTab == (Object)null)
		{
			return false;
		}
		int instanceID = ((Object)laptop).GetInstanceID();
		bool flag = OperatorApi.ModdedOperations.Operations.Count == 0;
		if (catalogPresentations.TryGetValue(instanceID, out var value) && value != null && (Object)(object)value.Page == (Object)(object)page && (Object)(object)value.HomeShell != (Object)null && (flag || (Object)(object)value.PreparationPanel != (Object)null))
		{
			return true;
		}
		if (value != null)
		{
			if ((Object)(object)value.HomeShell != (Object)null)
			{
				Object.Destroy((Object)(object)value.HomeShell);
			}
			if ((Object)(object)value.PreparationPanel != (Object)null)
			{
				Object.Destroy((Object)(object)value.PreparationPanel);
			}
			if ((Object)(object)value.NativeBoardData != (Object)null)
			{
				Object.Destroy((Object)(object)value.NativeBoardData);
			}
			if ((Object)(object)value.NativeTargetData != (Object)null)
			{
				Object.Destroy((Object)(object)value.NativeTargetData);
			}
			if ((Object)(object)value.NativeInfiltrationMapPrefab != (Object)null)
			{
				Object.Destroy((Object)(object)value.NativeInfiltrationMapPrefab);
			}
		}
		catalogPresentations.Remove(instanceID);
		GameObject val = FindDeep(page.transform, "MODDED_NATIVE_HOME");
		GameObject val2 = FindNativeModdedPreparationPanel(laptop);
		if ((Object)(object)val != (Object)null)
		{
			Object.Destroy((Object)(object)val);
		}
		if ((Object)(object)val2 != (Object)null)
		{
			Object.Destroy((Object)(object)val2);
		}
		GameObject val3 = ExtractGameObject(ReadMember(laptop, "ActiveOperationsList"));
		GameObject val4 = ExtractGameObject(ReadMember(laptop, "SimulationOperationList"));
		GameObject val5 = FindNativeOperationRowTemplate(((Object)(object)val3 == (Object)null) ? null : val3.transform) ?? FindNativeOperationRowTemplate(((Object)(object)val4 == (Object)null) ? null : val4.transform);
		if ((Object)(object)val3 == (Object)null || (Object)(object)val5 == (Object)null)
		{
			log.LogWarning((object)"Cerberus native Modded Ops presentation skipped because no shipped operation-row visual was available.");
			return false;
		}
		int[] array = BuildRelativeChildIndexPath(laptop.ActiveOperationsTab.transform, val3.transform);
		if (array == null)
		{
			log.LogWarning((object)"Cerberus native Modded Ops presentation skipped because the active-list path was outside its page.");
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
			if ((Object)(object)catalogPresentation.HomeShell == (Object)null)
			{
				return false;
			}
			catalogPresentations[instanceID] = catalogPresentation;
			deferredSetupLoggedLaptops.Remove(instanceID);
			Canvas.ForceUpdateCanvases();
			log.LogInfo((object)"Modded Operations framework attached with an empty catalog; install one or more valid data-only map packages under BepInEx\\OperatorMods.");
			return true;
		}
		catalogPresentation.SelectedOperation = OperatorApi.ModdedOperations.Operations[0];
		catalogPresentation.SelectedTimeCode = catalogPresentation.SelectedOperation.DefaultTimeCode;
		GameObject val6 = (catalogPresentation.PreparationPanel = CreateCatalogOperationBoardShell(laptop, catalogPresentation));
		GameObject val7 = null;
		TMP_Text briefing2 = null;
		if ((Object)(object)val6 != (Object)null)
		{
			val7 = (catalogPresentation.HomeShell = CreateCatalogOperationShell(page.transform, laptop.ActiveOperationsTab, array, val5, catalogPresentation, out briefing2));
			catalogPresentation.HomeBriefing = briefing2;
		}
		if ((Object)(object)val7 == (Object)null || (Object)(object)val6 == (Object)null)
		{
			if ((Object)(object)val7 != (Object)null)
			{
				Object.Destroy((Object)(object)val7);
			}
			if ((Object)(object)val6 != (Object)null)
			{
				Object.Destroy((Object)(object)val6);
			}
			if (deferredSetupLoggedLaptops.Add(instanceID))
			{
				log.LogInfo((object)("Modded Operations deferred one hidden laptop replica until its shipped list and operation-information surfaces are ready; laptopId=" + instanceID + "."));
			}
			return false;
		}
		Image component = page.GetComponent<Image>();
		if ((Object)(object)component != (Object)null)
		{
			((Graphic)component).color = new Color(((Graphic)component).color.r, ((Graphic)component).color.g, ((Graphic)component).color.b, 0f);
			((Graphic)component).raycastTarget = false;
		}
		val6.SetActive(false);
		catalogPresentations[instanceID] = catalogPresentation;
		Canvas.ForceUpdateCanvases();
		log.LogInfo((object)("Modded Operations presentation built from shipped operation-row and OperationBoardUI visuals; laptopId=" + ((Object)laptop).GetInstanceID() + ", rows=" + OperatorApi.ModdedOperations.Operations.Count + ", catalog=" + OperatorApi.ModdedOperations.CatalogId + "."));
		return true;
	}

	private static GameObject CreateEmptyCatalogShell(Transform parent, GameObject nativePageTemplate, int[] relativeListPath, out TMP_Text briefing)
	{
		briefing = null;
		if ((Object)(object)parent == (Object)null || (Object)(object)nativePageTemplate == (Object)null || relativeListPath == null)
		{
			return null;
		}
		GameObject val = Object.Instantiate<GameObject>(nativePageTemplate, parent);
		if ((Object)(object)val == (Object)null)
		{
			return null;
		}
		((Object)val).name = "MODDED_NATIVE_HOME";
		SetFullStretch(val.GetComponent<RectTransform>());
		val.SetActive(true);
		RewriteClonedPageHeading(val);
		Transform val2 = FollowRelativeChildIndexPath(val.transform, relativeListPath);
		if ((Object)(object)val2 == (Object)null)
		{
			Object.Destroy((Object)(object)val);
			return null;
		}
		SetChildrenActive(val2, active: false);
		briefing = FindNativeBriefingText(val);
		if ((Object)(object)briefing != (Object)null)
		{
			DisableLocalizationComponent(((Component)briefing).gameObject);
			if ((Object)(object)briefing.transform.parent != (Object)null)
			{
				DisableLocalizationComponent(((Component)briefing.transform.parent).gameObject);
			}
			briefing.text = "NO MODDED OPERATIONS INSTALLED\n\nInstall a valid map package in BepInEx\\OperatorMods, then restart OPERATOR.";
			briefing.enableWordWrapping = true;
		}
		foreach (OperationSelectionUI componentsInChild in val.GetComponentsInChildren<OperationSelectionUI>(true))
		{
			if (!((Object)(object)componentsInChild == (Object)null))
			{
				((Behaviour)componentsInChild).enabled = false;
				Object.Destroy((Object)(object)componentsInChild);
			}
		}
		return val;
	}

	private GameObject CreateCatalogOperationShell(Transform parent, GameObject nativePageTemplate, int[] relativeListPath, GameObject rowTemplate, CatalogPresentation presentation, out TMP_Text briefing)
	{
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Invalid comparison between Unknown and I4
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Invalid comparison between Unknown and I4
		briefing = null;
		if ((Object)(object)parent == (Object)null || (Object)(object)nativePageTemplate == (Object)null || (Object)(object)rowTemplate == (Object)null || presentation == null)
		{
			return null;
		}
		GameObject val = Object.Instantiate<GameObject>(nativePageTemplate, parent);
		if ((Object)(object)val == (Object)null)
		{
			return null;
		}
		((Object)val).name = "MODDED_NATIVE_HOME";
		SetFullStretch(val.GetComponent<RectTransform>());
		val.SetActive(true);
		RewriteClonedPageHeading(val);
		Transform val2 = FollowRelativeChildIndexPath(val.transform, relativeListPath);
		if ((Object)(object)val2 == (Object)null)
		{
			Object.Destroy((Object)(object)val);
			return null;
		}
		SetChildrenActive(val2, active: false);
		List<GameObject> list = new List<GameObject>();
		int num = 0;
		foreach (ModdedOperationDefinition operation in OperatorApi.ModdedOperations.Operations)
		{
			GameObject val3 = Object.Instantiate<GameObject>(rowTemplate, val2);
			if (!((Object)(object)val3 == (Object)null))
			{
				((Object)val3).name = "MODDED_NATIVE_ROW_" + num.ToString("D3");
				val3.SetActive(true);
				string mode = (((int)operation.Mode == 2) ? "PVE" : "PVP");
				string threat = (((int)operation.Mode == 2) ? "HIGH" : "VARIABLE");
				RewriteNativeOperationRow(val3, operation.DisplayName, "30-45 MIN", threat, mode, operation.AreaOfOperation);
				list.Add(val3);
				ModdedOperationDefinition captured = operation;
				RebindNativeRow(val3, delegate
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
			Object.Destroy((Object)(object)val);
			return null;
		}
		if ((Object)(object)((Component)val2).GetComponent<VerticalLayoutGroup>() == (Object)null)
		{
			StackNativeRows(list);
		}
		briefing = FindNativeBriefingText(val, list.ToArray());
		if ((Object)(object)briefing != (Object)null)
		{
			DisableLocalizationComponent(((Component)briefing).gameObject);
			if ((Object)(object)briefing.transform.parent != (Object)null)
			{
				DisableLocalizationComponent(((Component)briefing.transform.parent).gameObject);
			}
			briefing.text = FormatCatalogBriefing(presentation.SelectedOperation);
			briefing.enableWordWrapping = true;
		}
		foreach (OperationSelectionUI componentsInChild in val.GetComponentsInChildren<OperationSelectionUI>(true))
		{
			if (!((Object)(object)componentsInChild == (Object)null))
			{
				((Behaviour)componentsInChild).enabled = false;
				Object.Destroy((Object)(object)componentsInChild);
			}
		}
		return val;
	}

	private void SelectCatalogOperation(CatalogPresentation presentation, ModdedOperationDefinition operation, bool openPreparation)
	{
		if (presentation != null && operation != null)
		{
			presentation.SelectedOperation = operation;
			presentation.SelectedTimeCode = operation.DefaultTimeCode;
			if ((Object)(object)presentation.HomeBriefing != (Object)null)
			{
				presentation.HomeBriefing.text = FormatCatalogBriefing(operation);
			}
			UpdateCatalogOperationBoard(presentation);
			BeginSelectedMapPrefetch(operation);
			log.LogInfo((object)("Modded Operations row " + (openPreparation ? "double-click" : "single-click") + ": operation=" + operation.Id + "."));
			if (openPreparation)
			{
				OpenNativeOperationPreparation(presentation.Laptop, presentation.PreparationPanel, null);
			}
		}
	}

	private void BeginSelectedMapPrefetch(ModdedOperationDefinition operation)
	{
		ModdedMapDefinition val = default(ModdedMapDefinition);
		if (operation == null || !OperatorApi.ModdedOperations.TryGetMap(operation.MapId, ref val) || val == null)
		{
			return;
		}
		TrimCompletedMapBundleCacheAtSafeBoundary(val.Id, "selected-map prefetch ownership");
		if ((loadedMapBundles.TryGetValue(val.Id, out var value) && value != null && (Object)(object)value.SceneBundle != (Object)null && value.Map != null && string.Equals(value.Map.PackageContentId, val.PackageContentId, StringComparison.Ordinal)) || (value != null && !TryDiscardStaleCompletedMapBundle(val.Id, value, "selected-map prefetch")))
		{
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
					return;
				}
			}
			ManualLogSource obj = log;
			string[] obj2 = new string[5] { "Modded Operations deferred selected-map prefetch for ", val.Id, " while ", null, null };
			ModdedMapDefinition map3 = pendingLaunch.Map;
			obj2[3] = ((map3 != null) ? map3.Id : null);
			obj2[4] = " is loading.";
			obj.LogInfo((object)string.Concat(obj2));
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
		log.LogInfo((object)("Modded Operations began selected-map bundle prefetch: map=" + val.Id + ", bundles=" + pendingMapLaunch.BundlePaths.Count + ", bytes=" + GetBundleByteTotal(pendingMapLaunch.BundlePaths) + "."));
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
		//IL_025c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Expected O, but got Unknown
		//IL_0268: Unknown result type (might be due to invalid IL or missing references)
		//IL_0272: Expected O, but got Unknown
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_027e: Expected O, but got Unknown
		//IL_029e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a8: Expected O, but got Unknown
		//IL_0752: Unknown result type (might be due to invalid IL or missing references)
		//IL_075c: Expected O, but got Unknown
		//IL_0762: Unknown result type (might be due to invalid IL or missing references)
		//IL_076c: Expected O, but got Unknown
		//IL_0772: Unknown result type (might be due to invalid IL or missing references)
		//IL_077c: Expected O, but got Unknown
		//IL_07a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_07ad: Expected O, but got Unknown
		GameObject val = ResolveNativeOperationBoardVisualTemplate();
		if ((Object)(object)laptop == (Object)null || (Object)(object)laptop.opBoardParent == (Object)null || presentation == null || (Object)(object)val == (Object)null)
		{
			return null;
		}
		Transform opBoardParent = laptop.opBoardParent;
		Transform parent = opBoardParent.parent;
		Transform val2 = (((Object)(object)parent == (Object)null) ? null : parent.parent);
		if ((Object)(object)parent == (Object)null || (Object)(object)val2 == (Object)null)
		{
			return null;
		}
		int[] array = BuildRelativeChildIndexPath(parent, opBoardParent);
		if (array == null)
		{
			return null;
		}
		GameObject val3 = Object.Instantiate<GameObject>(((Component)parent).gameObject, val2);
		if ((Object)(object)val3 == (Object)null)
		{
			return null;
		}
		((Object)val3).name = "MODDED_NATIVE_OPERATION_PREPARATION";
		val3.SetActive(true);
		Transform val4 = FollowRelativeChildIndexPath(val3.transform, array);
		if ((Object)(object)val4 == (Object)null)
		{
			Object.Destroy((Object)(object)val3);
			return null;
		}
		SetChildrenActive(val4, active: false);
		GameObject val5 = Object.Instantiate<GameObject>(val, val4);
		if ((Object)(object)val5 == (Object)null)
		{
			Object.Destroy((Object)(object)val3);
			return null;
		}
		((Object)val5).name = "MODDED_NATIVE_OPERATION_INFORMATION";
		SetFullStretch(val5.GetComponent<RectTransform>());
		val5.SetActive(false);
		OperationBoardUI board = val5.GetComponent<OperationBoardUI>() ?? val5.GetComponentInChildren<OperationBoardUI>(true);
		if ((Object)(object)board == (Object)null || (Object)(object)board.SituationReportText == (Object)null || (Object)(object)board.SituationReportText.TMP == (Object)null || (Object)(object)board.ExecuteOperationButton == (Object)null || (Object)(object)board.ConfirmationWindow == (Object)null || (Object)(object)board.InfilTimeSlider == (Object)null || (Object)(object)board.PrearationTimeSlider == (Object)null)
		{
			Object.Destroy((Object)(object)val3);
			return null;
		}
		CerebusOpboard val6 = null;
		try
		{
			val6 = ScriptableObject.CreateInstance<CerebusOpboard>();
			((Object)val6).name = "MODDED_OPERATIONS_PRIVATE_OPBOARD_DATA";
			CerebusTargetPackage val7 = ScriptableObject.CreateInstance<CerebusTargetPackage>();
			if ((Object)(object)val6 == (Object)null || (Object)(object)val7 == (Object)null)
			{
				throw new InvalidOperationException("package-owned operation data could not be allocated");
			}
			((Object)val7).name = "MODDED_OPERATIONS_PRIVATE_TARGET_PACKAGE";
			val7.TargetPackage = val6;
			val7.TargetPackageIndex = 0;
			val7.isLocked = false;
			val7.isSimulation = false;
			val7.isRegion = false;
			val7.MissionRequiredForUnlock = null;
			val7.unlockCode = string.Empty;
			val7.progressionUnlockCode = string.Empty;
			val7.OperationType = new LocalizedString();
			val7.EnemyCount = new LocalizedString();
			val7.OperationDescription = new LocalizedString();
			val6.ThisMissionTargetPackage = val7;
			presentation.NativeTargetData = val7;
			val6.MapPrefab = null;
			val6.SituationReport = new LocalizedString();
			val6.requireEnoughExfils = false;
			val6.requireEnoughInfils = false;
			val6.isLocked = false;
			val6.unlockCode = string.Empty;
			val6.exfilUnlockingKey = string.Empty;
			val6.TargetRaidTime = 0f;
			val6.AchievementName = string.Empty;
			val6.achievement_min_ai_amount = 0;
			val6.INFILTRATION_TARGET = string.Empty;
			val6.INFILTRATION_TIME = string.Empty;
			val6.SelectedInfiltrationTime = 0;
			val6.PrepTimeInSeconds = 5;
			val6.isCompleted = false;
			val6.UnlockOnExfil = string.Empty;
			val6.HVTSpawnIsRandom = false;
			val6.isSimulation = false;
			val6.missionLaptop = laptop;
			val6.OperationBoardUI = board;
			board.CerebusOpboard = val6;
			board.Laptop = laptop;
			deferredSetupLoggedLaptops.Remove(((Object)laptop).GetInstanceID());
		}
		catch (Exception ex)
		{
			int instanceID = ((Object)laptop).GetInstanceID();
			if (deferredSetupLoggedLaptops.Add(instanceID))
			{
				log.LogInfo((object)("Modded Operations deferred one hidden laptop replica until its shipped OperationBoardUI ownership graph is ready; laptopId=" + instanceID + ", reason=" + ex.GetType().Name + "."));
			}
			if ((Object)(object)val6 != (Object)null)
			{
				Object.Destroy((Object)(object)val6);
			}
			if ((Object)(object)presentation.NativeTargetData != (Object)null)
			{
				Object.Destroy((Object)(object)presentation.NativeTargetData);
			}
			presentation.NativeTargetData = null;
			if ((Object)(object)presentation.NativeInfiltrationMapPrefab != (Object)null)
			{
				Object.Destroy((Object)(object)presentation.NativeInfiltrationMapPrefab);
			}
			presentation.NativeInfiltrationMapPrefab = null;
			Object.Destroy((Object)(object)val3);
			return null;
		}
		presentation.Board = board;
		presentation.NativeBoardData = val6;
		presentation.SituationReport = (TMP_Text)(object)board.SituationReportText.TMP;
		((Object)((Component)presentation.SituationReport).gameObject).name = "MODDED_NATIVE_SITREP";
		DisableLocalizationComponent(((Component)board.SituationReportText).gameObject);
		DisableLocalizationComponent(((Component)presentation.SituationReport).gameObject);
		if ((Object)(object)presentation.SituationReport.transform.parent != (Object)null)
		{
			DisableLocalizationComponent(((Component)presentation.SituationReport.transform.parent).gameObject);
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
		SetComponentActiveSafe((Component)(object)board.InfilTargetSlider, active: false);
		SetComponentActiveSafe((Component)(object)board.InfilTimeSlider, active: true);
		SetComponentActiveSafe((Component)(object)board.PrearationTimeSlider, active: true);
		SetComponentActiveSafe((Component)(object)board.OpforCountSlider, active: false);
		SetComponentActiveSafe((Component)(object)board.EnemyCountSlider, active: false);
		SetComponentActiveSafe((Component)(object)board.OpforDifficultySlider, active: false);
		SetComponentActiveSafe((Component)(object)board.HVT_OpforCountSlider, active: false);
		SetComponentActiveSafe((Component)(object)board.HVTEnemyCountSlider, active: false);
		SetComponentActiveSafe((Component)(object)board.HVTOpforDifficultySlider, active: false);
		SetComponentActiveSafe((Component)(object)board.HVTCountSlider, active: false);
		SetComponentActiveSafe((Component)(object)board.HVTDifficultySlider, active: false);
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
		confirmation.onConfirm.AddListener(UnityAction.op_Implicit((Action)delegate
		{
			BeginCatalogOperationLaunch(presentation);
		}));
		confirmation.onCancel = new UnityEvent();
		confirmation.onCancel.AddListener(UnityAction.op_Implicit((Action)delegate
		{
			CloseNativeMapConfirmation(board, logClose: true);
		}));
		if ((Object)(object)confirmation.windowTitle != (Object)null)
		{
			DisableLocalizationComponent(((Component)confirmation.windowTitle).gameObject);
			((TMP_Text)confirmation.windowTitle).text = confirmation.titleText;
		}
		if ((Object)(object)confirmation.windowDescription != (Object)null)
		{
			DisableLocalizationComponent(((Component)confirmation.windowDescription).gameObject);
			((TMP_Text)confirmation.windowDescription).text = confirmation.descriptionText;
		}
		try
		{
			confirmation.UpdateUI();
		}
		catch
		{
		}
		if (!ReplaceNativeButtonAction((Object)(object)confirmation.confirmButton, delegate
		{
			BeginCatalogOperationLaunch(presentation);
		}) || !ReplaceNativeButtonAction((Object)(object)confirmation.cancelButton, delegate
		{
			confirmation.onCancel.Invoke();
		}) || !ReplaceNativeButtonAction((Object)(object)board.ExecuteOperationButton, delegate
		{
			confirmation.OpenWindow();
		}))
		{
			Object.Destroy((Object)(object)val6);
			Object.Destroy((Object)(object)presentation.NativeTargetData);
			Object.Destroy((Object)(object)presentation.NativeInfiltrationMapPrefab);
			presentation.NativeBoardData = null;
			presentation.NativeTargetData = null;
			presentation.NativeInfiltrationMapPrefab = null;
			Object.Destroy((Object)(object)val3);
			return null;
		}
		RebindNativeFullscreenControls(board, val3);
		BindNativePreparationBack(laptop, val3, null, warnIfMissing: false);
		UpdateCatalogOperationBoard(presentation);
		val5.SetActive(true);
		val3.SetActive(false);
		return val3;
	}

	private GameObject ResolveNativeOperationBoardVisualTemplate()
	{
		if ((Object)(object)operationBoardVisualTemplate != (Object)null)
		{
			return operationBoardVisualTemplate;
		}
		if ((Object)(object)OperationsManager.singleton != (Object)null && (Object)(object)OperationsManager.singleton.OperationBoardUIPrefab != (Object)null)
		{
			operationBoardVisualTemplate = OperationsManager.singleton.OperationBoardUIPrefab;
			return operationBoardVisualTemplate;
		}
		foreach (OperationsManager item in Resources.FindObjectsOfTypeAll<OperationsManager>())
		{
			if (!((Object)(object)item == (Object)null) && !((Object)(object)item.OperationBoardUIPrefab == (Object)null))
			{
				operationBoardVisualTemplate = item.OperationBoardUIPrefab;
				return operationBoardVisualTemplate;
			}
		}
		foreach (OperationBoardUI item2 in Resources.FindObjectsOfTypeAll<OperationBoardUI>())
		{
			if (!((Object)(object)item2 == (Object)null) && !((Object)(object)((Component)item2).gameObject == (Object)null) && !((Object)((Component)item2).gameObject).name.StartsWith("MODDED_NATIVE_", StringComparison.Ordinal) && !((Object)(object)item2.SituationReportText == (Object)null) && !((Object)(object)item2.SituationReportText.TMP == (Object)null) && !((Object)(object)item2.ExecuteOperationButton == (Object)null) && !((Object)(object)item2.ConfirmationWindow == (Object)null) && !((Object)(object)item2.InfilTimeSlider == (Object)null) && !((Object)(object)item2.PrearationTimeSlider == (Object)null))
			{
				operationBoardVisualTemplate = ((Component)item2).gameObject;
				return operationBoardVisualTemplate;
			}
		}
		return null;
	}

	private void UpdateCatalogOperationBoard(CatalogPresentation presentation, string scenePathOverride = null)
	{
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Expected O, but got Unknown
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Expected O, but got Unknown
		//IL_032d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0333: Invalid comparison between Unknown and I4
		//IL_0440: Unknown result type (might be due to invalid IL or missing references)
		//IL_0446: Invalid comparison between Unknown and I4
		//IL_045d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0463: Invalid comparison between Unknown and I4
		if (presentation == null || presentation.SelectedOperation == null || (Object)(object)presentation.Board == (Object)null || (Object)(object)presentation.NativeBoardData == (Object)null)
		{
			return;
		}
		ModdedOperationDefinition operation = presentation.SelectedOperation;
		ModdedMapDefinition val = default(ModdedMapDefinition);
		if (!OperatorApi.ModdedOperations.TryGetMap(operation.MapId, ref val) || val == null)
		{
			return;
		}
		string text = (string.IsNullOrEmpty(scenePathOverride) ? val.ScenePath : scenePathOverride);
		if (!ScenePathBelongsToMap(val, text))
		{
			throw new InvalidOperationException("the requested scene path is not declared by the selected map");
		}
		if (!operation.SupportedTimeCodes.Contains<string>(presentation.SelectedTimeCode, StringComparer.Ordinal))
		{
			presentation.SelectedTimeCode = operation.DefaultTimeCode;
		}
		if ((Object)(object)presentation.SituationReport != (Object)null)
		{
			presentation.SituationReport.text = FormatCatalogBriefing(operation);
		}
		Sprite orLoadPreviewSprite = GetOrLoadPreviewSprite(val);
		if ((Object)(object)orLoadPreviewSprite != (Object)null)
		{
			ReplaceNativeMapPreview(presentation.Board.MapParent, orLoadPreviewSprite, "MODDED_NATIVE_MAP_PREVIEW");
			ReplaceNativeMapPreview(presentation.Board.FullscreenMapParent, orLoadPreviewSprite, "MODDED_NATIVE_MAP_FULLSCREEN");
		}
		else
		{
			SetGameObjectsActive(CaptureDirectChildren(presentation.Board.MapParent), active: false);
			SetGameObjectsActive(CaptureDirectChildren(presentation.Board.FullscreenMapParent), active: false);
		}
		Il2CppStringArray val2 = new Il2CppStringArray((long)operation.Infiltrations.Count);
		Il2CppReferenceArray<TARGETPACKAGE_DETAILS> val3 = new Il2CppReferenceArray<TARGETPACKAGE_DETAILS>((long)operation.SupportedTimeCodes.Count);
		int num = 0;
		for (int i = 0; i < operation.SupportedTimeCodes.Count; i++)
		{
			string text2 = operation.SupportedTimeCodes[i];
			TARGETPACKAGE_DETAILS val4 = new TARGETPACKAGE_DETAILS
			{
				OPERATION_SCENE = text,
				DISPLAY_NAME = operation.DisplayName,
				INFILTRATION_TIME = text2
			};
			((Il2CppArrayBase<TARGETPACKAGE_DETAILS>)(object)val3)[i] = val4;
			if (string.Equals(text2, presentation.SelectedTimeCode, StringComparison.Ordinal))
			{
				num = i;
			}
		}
		for (int j = 0; j < operation.Infiltrations.Count; j++)
		{
			((Il2CppArrayBase<string>)(object)val2)[j] = operation.Infiltrations[j].DisplayName;
		}
		if ((Object)(object)presentation.NativeInfiltrationMapPrefab != (Object)null)
		{
			Object.Destroy((Object)(object)presentation.NativeInfiltrationMapPrefab);
		}
		presentation.NativeInfiltrationMapPrefab = BuildPackageInfiltrationMapPrefab(presentation, operation, orLoadPreviewSprite);
		if ((Object)(object)presentation.NativeInfiltrationMapPrefab == (Object)null)
		{
			log.LogError((object)("Modded Operations refused to arm operation '" + operation.Id + "' because no sanitized native infiltration selector map could be built."));
			return;
		}
		CerebusOpboard data = presentation.NativeBoardData;
		data.requireEnoughExfils = false;
		data.requireEnoughInfils = false;
		data.AffectGamemode = true;
		data.GameModeOverride = (GameMode)((int)operation.Mode != 2);
		if ((Object)(object)presentation.NativeTargetData != (Object)null)
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
		data.TARGETPACKAGE = val3;
		data.AvailableInfils = val2;
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
		if ((Object)(object)confirmation != (Object)null)
		{
			confirmation.useLocalization = false;
			confirmation.useCustomContent = true;
			confirmation.titleText = "Start Operation";
			confirmation.descriptionText = "Start " + operation.DisplayName + " at " + presentation.SelectedTimeCode + "?";
			if ((Object)(object)confirmation.windowTitle != (Object)null)
			{
				((TMP_Text)confirmation.windowTitle).text = confirmation.titleText;
			}
			if ((Object)(object)confirmation.windowDescription != (Object)null)
			{
				((TMP_Text)confirmation.windowDescription).text = confirmation.descriptionText;
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
				if ((Object)(object)confirmation != (Object)null)
				{
					confirmation.descriptionText = "Start " + operation.DisplayName + " at " + presentation.SelectedTimeCode + "?";
					if ((Object)(object)confirmation.windowDescription != (Object)null)
					{
						((TMP_Text)confirmation.windowDescription).text = confirmation.descriptionText;
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
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		if (map == null)
		{
			return null;
		}
		if (previewSprites.TryGetValue(map.Id, out var value) && (Object)(object)value != (Object)null)
		{
			return value;
		}
		try
		{
			byte[] array = File.ReadAllBytes(map.PreviewImagePath);
			Texture2D val = new Texture2D(2, 2, (TextureFormat)4, false, true)
			{
				name = "MODDED_OPERATIONS_PREVIEW_" + map.Id
			};
			if (!ImageConversion.LoadImage(val, Il2CppStructArray<byte>.op_Implicit(array), false))
			{
				Object.Destroy((Object)(object)val);
				return null;
			}
			((Texture)val).wrapMode = (TextureWrapMode)1;
			Sprite val2 = Sprite.Create(val, new Rect(0f, 0f, (float)((Texture)val).width, (float)((Texture)val).height), new Vector2(0.5f, 0.5f), 100f);
			((Object)val2).name = "MODDED_OPERATIONS_PREVIEW_" + map.Id;
			previewTextures[map.Id] = val;
			previewSprites[map.Id] = val2;
			return val2;
		}
		catch (Exception ex)
		{
			log.LogWarning((object)("Modded Operations preview load failed for " + map.Id + ": " + ex.GetType().Name + ": " + ex.Message));
			return null;
		}
	}

	private GameObject BuildPackageInfiltrationMapPrefab(CatalogPresentation presentation, ModdedOperationDefinition operation, Sprite preview)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Expected O, but got Unknown
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		if (presentation == null || (Object)(object)presentation.Board == (Object)null || operation == null || (Object)(object)preview == (Object)null || operation.Infiltrations == null || operation.Infiltrations.Count == 0)
		{
			return null;
		}
		MapInfilMarker val = ResolveNativeInfiltrationMarkerTemplate();
		if ((Object)(object)val == (Object)null || (Object)(object)((Component)val).gameObject == (Object)null)
		{
			return null;
		}
		GameObject val2 = null;
		try
		{
			val2 = new GameObject("MODDED_OPERATIONS_PACKAGE_INFILTRATION_MAP");
			SetFullStretch(val2.AddComponent<RectTransform>());
			GameObject val3 = new GameObject("PACKAGE_MAP_PREVIEW");
			val3.transform.SetParent(val2.transform, false);
			SetFullStretch(val3.AddComponent<RectTransform>());
			Image obj = val3.AddComponent<Image>();
			obj.sprite = preview;
			((Graphic)obj).color = Color.white;
			obj.preserveAspect = true;
			((Graphic)obj).raycastTarget = false;
			Vector2 val7 = default(Vector2);
			for (int i = 0; i < operation.Infiltrations.Count; i++)
			{
				ModdedInfiltrationDefinition val4 = operation.Infiltrations[i];
				GameObject val5 = Object.Instantiate<GameObject>(((Component)val).gameObject, val2.transform);
				if ((Object)(object)val5 == (Object)null)
				{
					throw new InvalidOperationException("the shipped infiltration marker visual could not be duplicated");
				}
				((Object)val5).name = "PACKAGE_INFIL_" + val4.Id;
				MapInfilMarker val6 = val5.GetComponent<MapInfilMarker>() ?? val5.GetComponentInChildren<MapInfilMarker>(true);
				if ((Object)(object)val6 == (Object)null)
				{
					throw new InvalidOperationException("the duplicated infiltration marker lost its native component");
				}
				val6.MaxPlayers = val4.MaximumPlayers;
				val6.InfilName = val4.DisplayName;
				val6.IsGroundInfil = true;
				val6.IsHeliInfil = false;
				val6.IsExfil = false;
				val6.OpboardUI = presentation.Board;
				val6.MarkerIndex = i;
				val6.individualSelectMode = false;
				val6.CurrentNumPlayers = 0;
				val6.isSelectedLocal = false;
				RectTransform obj2 = val5.GetComponent<RectTransform>() ?? ((Component)val6).GetComponent<RectTransform>();
				if ((Object)(object)obj2 == (Object)null)
				{
					throw new InvalidOperationException("the shipped infiltration marker visual has no RectTransform");
				}
				((Vector2)(ref val7))._002Ector(val4.MapPositionX, val4.MapPositionY);
				obj2.anchorMin = val7;
				obj2.anchorMax = val7;
				obj2.anchoredPosition = Vector2.zero;
				((Transform)obj2).localScale = Vector3.one;
				val5.SetActive(true);
			}
			val2.SetActive(false);
			log.LogInfo((object)("Modded Operations built a package-owned native infiltration selector map: operation=" + operation.Id + ", markers=" + operation.Infiltrations.Count + ", visualSource=" + ((Object)((Component)val).gameObject).name + "."));
			return val2;
		}
		catch (Exception ex)
		{
			if ((Object)(object)val2 != (Object)null)
			{
				Object.Destroy((Object)(object)val2);
			}
			log.LogError((object)("Modded Operations could not build the package-owned infiltration selector map: " + ex.GetType().Name + ": " + ex.Message));
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
				if ((Object)(object)item2 == (Object)null || (Object)(object)item2.MapPrefab == (Object)null || ((Object)item2.MapPrefab).name.StartsWith("MODDED_OPERATIONS_", StringComparison.Ordinal))
				{
					continue;
				}
				foreach (MapInfilMarker componentsInChild in item2.MapPrefab.GetComponentsInChildren<MapInfilMarker>(true))
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
		if ((Object)(object)marker != (Object)null && (Object)(object)((Component)marker).gameObject != (Object)null && !((Object)((Component)marker).gameObject).name.StartsWith("PACKAGE_INFIL_", StringComparison.Ordinal) && (Object)(object)marker.SelectedParent != (Object)null && (Object)(object)marker.DeselectedParent != (Object)null)
		{
			return (Object)(object)marker.canvasGroup != (Object)null;
		}
		return false;
	}

	private bool TrySelectFreshLaunchScene(ModdedMapDefinition map, out SceneVariantSelection selection)
	{
		selection = null;
		if (map == null)
		{
			return false;
		}
		if (!HasDeclaredSceneVariants(map))
		{
			selection = new SceneVariantSelection((map.SceneVariants != null && map.SceneVariants.Count == 1) ? map.SceneVariants[0].Id : "default", map.ScenePath, 0);
			return true;
		}
		try
		{
			if (sceneVariantSelectionStore == null)
			{
				sceneVariantSelectionStore = new SceneVariantSelectionStore(Path.Combine(Paths.ConfigPath, "operator.modded-operations.scene-variants.v1.state"));
			}
			SceneVariantCandidate[] variants = map.SceneVariants.Select((ModdedSceneVariantDefinition variant) => new SceneVariantCandidate(variant.Id, variant.ScenePath)).ToArray();
			selection = sceneVariantSelectionStore.Select(map.PackageId, map.PackageContentId, map.Id, variants);
			log.LogInfo((object)("Modded Operations selected a fresh declared scene variant: package=" + map.PackageId + ", map=" + map.Id + ", variant=" + selection.Id + ", scene=" + selection.ScenePath + ", remainingInBag=" + selection.RemainingVariantCount + "."));
			return true;
		}
		catch (Exception ex)
		{
			log.LogError((object)("Modded Operations variant-map launch failed closed before native board start: package=" + map.PackageId + ", map=" + map.Id + ", reason=" + ex.GetType().Name + ": " + ex.Message));
			return false;
		}
	}

	private static bool HasDeclaredSceneVariants(ModdedMapDefinition map)
	{
		if (map != null && map.SceneVariants != null)
		{
			return map.SceneVariants.Count > 1;
		}
		return false;
	}

	private static bool ScenePathBelongsToMap(ModdedMapDefinition map, string scenePath)
	{
		if (map == null || string.IsNullOrWhiteSpace(scenePath))
		{
			return false;
		}
		if (!HasDeclaredSceneVariants(map))
		{
			return string.Equals(scenePath, map.ScenePath, StringComparison.OrdinalIgnoreCase);
		}
		return map.SceneVariants.Any((ModdedSceneVariantDefinition variant) => variant != null && string.Equals(variant.ScenePath, scenePath, StringComparison.OrdinalIgnoreCase));
	}

	private static bool SelectionBelongsToMap(ModdedMapDefinition map, SceneVariantSelection selection)
	{
		if (map == null || selection == null || !ScenePathBelongsToMap(map, selection.ScenePath))
		{
			return false;
		}
		if (!HasDeclaredSceneVariants(map))
		{
			return string.Equals(selection.ScenePath, map.ScenePath, StringComparison.OrdinalIgnoreCase);
		}
		return map.SceneVariants.Any((ModdedSceneVariantDefinition variant) => variant != null && string.Equals(variant.Id, selection.Id, StringComparison.Ordinal) && string.Equals(variant.ScenePath, selection.ScenePath, StringComparison.OrdinalIgnoreCase));
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
			log.LogError((object)("Modded Operations launch rejected because map " + operation.MapId + " is no longer in the frozen catalog."));
			return;
		}
		if (!val.Operations.Any((ModdedOperationDefinition candidate) => string.Equals(candidate.Id, operation.Id, StringComparison.Ordinal)) || !operation.SupportedTimeCodes.Contains<string>(presentation.SelectedTimeCode, StringComparer.Ordinal))
		{
			log.LogError((object)"Modded Operations launch rejected because the selected operation/time pair is not owned by its frozen package map.");
			return;
		}
		TrimCompletedMapBundleCacheAtSafeBoundary(val.Id, "catalog Confirm ownership");
		log.LogInfo((object)("Modded Operations launch request captured: operation=" + operation.Id + ", laptopId=" + ((Object)presentation.Laptop).GetInstanceID() + ", time=" + presentation.SelectedTimeCode + "."));
		MissionLaptop val2 = ResolveLaunchLaptop(presentation.Laptop);
		PlayerNetworking val3 = (((Object)(object)val2 == (Object)null) ? null : val2.playerNetworking);
		if (!IsPlayerOwnedLaunchLaptop(val2) || (Object)(object)val3 == (Object)null)
		{
			log.LogError((object)"Modded Operations launch rejected before package loading because no player-owned mission laptop was available.");
			return;
		}
		SetNativeConfirmationLoadingState(presentation, loading: true);
		if (loadedMapBundles.TryGetValue(val.Id, out var value) && value != null && (Object)(object)value.SceneBundle != (Object)null && value.Map != null && string.Equals(value.Map.PackageContentId, val.PackageContentId, StringComparison.Ordinal))
		{
			if (!TrySelectFreshLaunchScene(val, out var selection))
			{
				SetNativeConfirmationLoadingState(presentation, loading: false);
			}
			else
			{
				InvokeNativeCatalogLaunch(presentation, val, operation, presentation.SelectedTimeCode, val2, val3, selection);
			}
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
					pendingLaunch.LaunchLaptop = val2;
					pendingLaunch.LaunchPlayer = val3;
					pendingLaunch.LaunchRequested = true;
					pendingLaunch.LaunchRequestedTimestamp = Stopwatch.GetTimestamp();
					log.LogInfo((object)("Modded Operations attached Confirm to the selected-map prefetch: map=" + val.Id + ", completedBundles=" + pendingLaunch.RequestIndex + "/" + pendingLaunch.BundlePaths.Count + "."));
					return;
				}
			}
			SetNativeConfirmationLoadingState(presentation, loading: false);
			ManualLogSource obj = log;
			string[] obj2 = new string[5] { "Modded Operations ignored a launch while another selected map is still loading: requested=", val.Id, ", loading=", null, null };
			ModdedMapDefinition map3 = pendingLaunch.Map;
			obj2[3] = ((map3 != null) ? map3.Id : null);
			obj2[4] = ".";
			obj.LogWarning((object)string.Concat(obj2));
			return;
		}
		if (value != null && !TryDiscardStaleCompletedMapBundle(val.Id, value, "catalog Confirm"))
		{
			SetNativeConfirmationLoadingState(presentation, loading: false);
			return;
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
			LaunchLaptop = val2,
			LaunchPlayer = val3,
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
		log.LogInfo((object)("Modded Operations began asynchronous package load: map=" + val.Id + ", bundles=" + pendingMapLaunch.BundlePaths.Count + "."));
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
					if (pendingMapLaunch.LaunchRequested && pendingMapLaunch.SceneSelection == null && !TrySelectFreshLaunchScene(pendingMapLaunch.Map, out pendingMapLaunch.SceneSelection))
					{
						FailPendingLaunch("declared scene variant selection could not be committed");
						return;
					}
					loadedMapBundles[pendingMapLaunch.Map.Id] = pendingMapLaunch.LoadingBundles;
					pendingLaunch = null;
					TrimCompletedMapBundleCacheAtSafeBoundary(pendingMapLaunch.Map.Id, "verified selected-map prefetch completion");
					log.LogInfo((object)("Modded Operations completed verified bundle registration: map=" + pendingMapLaunch.Map.Id + ", seconds=" + FormatElapsedSeconds(pendingMapLaunch.LoadStartedTimestamp) + ", launchRequested=" + pendingMapLaunch.LaunchRequested + "."));
					if (pendingMapLaunch.LaunchRequested)
					{
						log.LogInfo((object)("Modded Operations Confirm waited " + FormatElapsedSeconds(pendingMapLaunch.LaunchRequestedTimestamp) + " seconds for remaining selected-map bundle work."));
						InvokeNativeCatalogLaunch(pendingMapLaunch.Presentation, pendingMapLaunch.Map, pendingMapLaunch.Operation, pendingMapLaunch.TimeCode, pendingMapLaunch.LaunchLaptop, pendingMapLaunch.LaunchPlayer, pendingMapLaunch.SceneSelection);
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
				if (!((AsyncOperation)pendingMapLaunch.CurrentRequest).isDone)
				{
					return;
				}
				AssetBundle assetBundle = pendingMapLaunch.CurrentRequest.assetBundle;
				pendingMapLaunch.CurrentRequest = null;
				if ((Object)(object)assetBundle == (Object)null)
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
				log.LogInfo((object)("Modded Operations loaded verified bundle " + (pendingMapLaunch.RequestIndex + 1) + "/" + pendingMapLaunch.BundlePaths.Count + ": file=" + Path.GetFileName(pendingMapLaunch.BundlePaths[pendingMapLaunch.RequestIndex]) + ", bytes=" + num + ", seconds=" + FormatElapsedSeconds(pendingMapLaunch.CurrentRequestStartedTimestamp) + "."));
				if (pendingMapLaunch.RequestIndex == pendingMapLaunch.BundlePaths.Count - 1)
				{
					pendingMapLaunch.LoadingBundles.SceneBundle = assetBundle;
				}
				else
				{
					string[] array = null;
					try
					{
						array = Il2CppArrayBase<string>.op_Implicit((Il2CppArrayBase<string>)(object)assetBundle.GetAllScenePaths());
					}
					catch
					{
					}
					if (array != null && array.Length != 0)
					{
						try
						{
							assetBundle.Unload(false);
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

	private static long GetBundleByteTotal(IEnumerable<string> paths)
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
		if (map == null || bundles == null || (Object)(object)bundles.SceneBundle == (Object)null)
		{
			return false;
		}
		try
		{
			ModdedMapDefinition[] array = OperatorApi.ModdedOperations.Maps.Where((ModdedMapDefinition candidate) => candidate != null && string.Equals(candidate.PackageContentId, map.PackageContentId, StringComparison.Ordinal) && string.Equals(candidate.SceneBundlePath, map.SceneBundlePath, StringComparison.OrdinalIgnoreCase)).ToArray();
			if (array.Any(HasDeclaredSceneVariants))
			{
				HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				ModdedMapDefinition[] array2 = array;
				foreach (ModdedMapDefinition val in array2)
				{
					if (val.SceneVariants != null && val.SceneVariants.Count > 0)
					{
						foreach (ModdedSceneVariantDefinition sceneVariant in val.SceneVariants)
						{
							hashSet.Add(sceneVariant.ScenePath);
						}
					}
					else
					{
						hashSet.Add(val.ScenePath);
					}
				}
				string[] array3 = Il2CppArrayBase<string>.op_Implicit((Il2CppArrayBase<string>)(object)bundles.SceneBundle.GetAllScenePaths());
				if (array3 == null)
				{
					return false;
				}
				HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				string[] array4 = array3;
				foreach (string text in array4)
				{
					if (string.IsNullOrWhiteSpace(text) || !hashSet2.Add(text))
					{
						return false;
					}
				}
				return hashSet2.SetEquals(hashSet);
			}
			HashSet<string> hashSet3 = new HashSet<string>(array.Select((ModdedMapDefinition candidate) => candidate.ScenePath), StringComparer.OrdinalIgnoreCase);
			if (!hashSet3.Contains(map.ScenePath))
			{
				return false;
			}
			bool result = false;
			foreach (string item in (Il2CppArrayBase<string>)(object)bundles.SceneBundle.GetAllScenePaths())
			{
				if (!hashSet3.Contains(item))
				{
					return false;
				}
				if (string.Equals(item, map.ScenePath, StringComparison.OrdinalIgnoreCase))
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
				AssetBundle sceneBundle = pendingMapLaunch.LoadingBundles.SceneBundle;
				if (sceneBundle != null)
				{
					sceneBundle.Unload(false);
				}
			}
			catch
			{
			}
			foreach (AssetBundle dependency in pendingMapLaunch.LoadingBundles.Dependencies)
			{
				try
				{
					if (dependency != null)
					{
						dependency.Unload(false);
					}
				}
				catch
				{
				}
			}
		}
		log.LogError((object)("Modded Operations package " + ((pendingMapLaunch != null && pendingMapLaunch.LaunchRequested) ? "launch" : "prefetch") + " failed closed: " + reason + "."));
		if (pendingMapLaunch?.Presentation != null)
		{
			SetNativeConfirmationLoadingState(pendingMapLaunch.Presentation, loading: false);
		}
	}

	private void InvokeNativeCatalogLaunch(CatalogPresentation presentation, ModdedMapDefinition map, ModdedOperationDefinition operation, string timeCode, MissionLaptop launchLaptop, PlayerNetworking launchPlayer, SceneVariantSelection sceneSelection)
	{
		if (presentation == null || (Object)(object)presentation.NativeBoardData == (Object)null || map == null || operation == null)
		{
			return;
		}
		try
		{
			presentation.SelectedOperation = operation;
			presentation.SelectedTimeCode = timeCode;
			if (!SelectionBelongsToMap(map, sceneSelection))
			{
				throw new InvalidOperationException("the selected scene variant is not owned by the selected map");
			}
			UpdateCatalogOperationBoard(presentation, sceneSelection.ScenePath);
			if (!string.Equals(operation.MapId, map.Id, StringComparison.Ordinal) || !operation.SupportedTimeCodes.Contains<string>(timeCode, StringComparer.Ordinal))
			{
				throw new InvalidOperationException("the requested operation/time pair does not belong to the selected map");
			}
			activeOperation = new ActiveMapOperation
			{
				Map = map,
				Operation = operation,
				TimeCode = timeCode,
				SceneSelection = sceneSelection,
				SceneHandle = 0
			};
			TrimCompletedMapBundleCacheAtSafeBoundary(map.Id, "fresh operation ownership transfer");
			log.LogInfo((object)("Modded Operations accepted an isolated package launch: operation=" + operation.Id + ", map=" + map.Id + ", content=" + map.PackageContentId + ", scene=" + sceneSelection.ScenePath + ", time=" + timeCode + "."));
			LogNativeLaunchContract(presentation.NativeBoardData);
			InvokeNativeBoardStart(presentation, map, operation, timeCode, launchLaptop, launchPlayer, sceneSelection);
		}
		catch (Exception ex)
		{
			activeOperation = null;
			SetNativeConfirmationLoadingState(presentation, loading: false);
			log.LogError((object)("Modded Operations native start failed closed: " + ex.GetType().Name + ": " + ex.Message));
		}
	}

	private void InvokeNativeBoardStart(CatalogPresentation presentation, ModdedMapDefinition map, ModdedOperationDefinition operation, string timeCode, MissionLaptop capturedLaptop, PlayerNetworking capturedPlayer, SceneVariantSelection sceneSelection)
	{
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Expected O, but got Unknown
		OperationsManager singleton = OperationsManager.singleton;
		CerebusOpboard val = presentation?.NativeBoardData;
		if ((Object)(object)singleton == (Object)null || (Object)(object)val == (Object)null || map == null || operation == null)
		{
			throw new InvalidOperationException("native operation manager was unavailable");
		}
		if (!string.Equals(operation.MapId, map.Id, StringComparison.Ordinal) || !operation.SupportedTimeCodes.Contains<string>(timeCode, StringComparer.Ordinal) || !SelectionBelongsToMap(map, sceneSelection))
		{
			throw new InvalidOperationException("package operation ownership changed before native start");
		}
		MissionLaptop val2 = RestoreCapturedLaunchLaptop(capturedLaptop, capturedPlayer, presentation.Laptop);
		if ((Object)(object)val2 == (Object)null)
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
		val.missionLaptop = val2;
		singleton.activeMissionLaptop = val2;
		if (val.TARGETPACKAGE == null || ((Il2CppArrayBase<TARGETPACKAGE_DETAILS>)(object)val.TARGETPACKAGE).Length == 0 || val.SelectedInfiltrationTime < 0 || val.SelectedInfiltrationTime >= ((Il2CppArrayBase<TARGETPACKAGE_DETAILS>)(object)val.TARGETPACKAGE).Length || (Object)(object)val.missionLaptop == (Object)null || (Object)(object)val.OperationBoardUI == (Object)null)
		{
			throw new InvalidOperationException("private operation board launch contract was incomplete");
		}
		TARGETPACKAGE_DETAILS val3 = ((Il2CppArrayBase<TARGETPACKAGE_DETAILS>)(object)val.TARGETPACKAGE)[val.SelectedInfiltrationTime];
		if (val3 == null || !string.Equals(val3.OPERATION_SCENE, sceneSelection.ScenePath, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("private operation board did not retain the selected scene address");
		}
		log.LogInfo((object)("Modded Operations completed the native board ownership graph: sourceLaptop=" + ((Object)presentation.Laptop).GetInstanceID() + ", launchLaptop=" + ((Object)val2).GetInstanceID() + ", playerOwned=" + ((Object)(object)val2.playerNetworking != (Object)null) + ", currentOperationInfo=" + (singleton.currentOpInfo != null) + "."));
		CloseNativeMapConfirmation(presentation.Board, logClose: false);
		PrimeNativeInfiltrationSelector(val, operation);
		val.Start_Operation();
		log.LogInfo((object)("Modded Operations entered the shipped board launch pipeline through CerebusOpboard.Start_Operation: operation=" + operation.Id + ", scene=" + sceneSelection.ScenePath + "."));
	}

	private void PrimeNativeInfiltrationSelector(CerebusOpboard board, ModdedOperationDefinition operation)
	{
		if ((Object)(object)board == (Object)null || (Object)(object)board.MapPrefab == (Object)null || operation == null || operation.Infiltrations == null || operation.Infiltrations.Count == 0)
		{
			throw new InvalidOperationException("package infiltration selector data was unavailable");
		}
		InfilSelectorDisplayer val = InfilSelectorDisplayer.instance ?? ((IEnumerable<InfilSelectorDisplayer>)Resources.FindObjectsOfTypeAll<InfilSelectorDisplayer>()).FirstOrDefault((InfilSelectorDisplayer item) => (Object)(object)item != (Object)null && (Object)(object)item.MapParent != (Object)null && (Object)(object)item.SelectInfilUI != (Object)null);
		if ((Object)(object)val == (Object)null)
		{
			throw new InvalidOperationException("the shipped infiltration selector was unavailable");
		}
		bool activeSelf = board.MapPrefab.activeSelf;
		try
		{
			if (!activeSelf)
			{
				board.MapPrefab.SetActive(true);
			}
			val.SpawnMap(board.MapPrefab);
		}
		finally
		{
			if ((Object)(object)board.MapPrefab != (Object)null && board.MapPrefab.activeSelf != activeSelf)
			{
				board.MapPrefab.SetActive(activeSelf);
			}
		}
		MapInfilMarker[] array = ((val.MapInfilMarkers == null) ? Array.Empty<MapInfilMarker>() : ((IEnumerable<MapInfilMarker>)val.MapInfilMarkers).ToArray());
		if ((Object)(object)val.ActiveMap == (Object)null || !((Object)val.ActiveMap).name.StartsWith("MODDED_OPERATIONS_PACKAGE_INFILTRATION_MAP", StringComparison.Ordinal) || array.Length != operation.Infiltrations.Count)
		{
			throw new InvalidOperationException("the shipped infiltration selector did not instantiate the package map (selector=" + ((Object)val).GetInstanceID() + ", activeMap=" + (((Object)(object)val.ActiveMap == (Object)null) ? "null" : ((Object)val.ActiveMap).name) + ", markers=" + array.Length + ", expectedMarkers=" + operation.Infiltrations.Count + ")");
		}
		for (int num = 0; num < array.Length; num++)
		{
			MapInfilMarker val2 = array[num];
			ModdedInfiltrationDefinition val3 = operation.Infiltrations[num];
			if ((Object)(object)val2 == (Object)null || val2.MarkerIndex != num || !string.Equals(val2.InfilName, val3.DisplayName, StringComparison.Ordinal) || val2.MaxPlayers != val3.MaximumPlayers || !val2.IsGroundInfil || val2.IsHeliInfil || val2.IsExfil)
			{
				throw new InvalidOperationException("the shipped infiltration selector retained non-package marker data");
			}
		}
		log.LogInfo((object)("Modded Operations primed the shipped infiltration selector through InfilSelectorDisplayer.SpawnMap: operation=" + operation.Id + ", activeMap=" + ((Object)val.ActiveMap).name + ", markers=" + array.Length + "."));
	}

	private static MissionLaptop ResolveLaunchLaptop(MissionLaptop preferred)
	{
		MissionLaptop val = (((Object)(object)OperationsManager.singleton == (Object)null) ? null : OperationsManager.singleton.activeMissionLaptop);
		if (IsPlayerOwnedLaunchLaptop(val))
		{
			return val;
		}
		if (IsPlayerOwnedLaunchLaptop(preferred))
		{
			return preferred;
		}
		MissionLaptop val2 = null;
		foreach (Component item in FindMissionLaptopComponents(ResolveMissionLaptopType()))
		{
			MissionLaptop val3 = (MissionLaptop)(object)((item is MissionLaptop) ? item : null);
			if (IsPlayerOwnedLaunchLaptop(val3))
			{
				if (((Component)val3).gameObject.activeInHierarchy)
				{
					return val3;
				}
				if (val2 == null)
				{
					val2 = val3;
				}
			}
		}
		return val2;
	}

	private static MissionLaptop RestoreCapturedLaunchLaptop(MissionLaptop capturedLaptop, PlayerNetworking capturedPlayer, MissionLaptop preferred)
	{
		if (IsPlayerOwnedLaunchLaptop(capturedLaptop))
		{
			return capturedLaptop;
		}
		if ((Object)(object)capturedLaptop != (Object)null && (Object)(object)capturedPlayer != (Object)null && (Object)(object)((Component)capturedPlayer).gameObject != (Object)null && ((NetworkBehaviour)capturedPlayer).isOwned)
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
		if ((Object)(object)laptop != (Object)null && (Object)(object)((Component)laptop).gameObject != (Object)null && (Object)(object)laptop.playerNetworking != (Object)null && (Object)(object)laptop.LaptopPlayerCamera != (Object)null && (Object)(object)laptop.computerManager != (Object)null && (Object)(object)laptop.uiRaycaster != (Object)null && (Object)(object)laptop.PlayerBlocker != (Object)null)
		{
			return laptop.ShitToKillWhenNotUsing != null;
		}
		return false;
	}

	private void LogNativeLaunchContract(CerebusOpboard board)
	{
		if ((Object)(object)board == (Object)null)
		{
			return;
		}
		try
		{
			CerebusTargetPackage thisMissionTargetPackage = board.ThisMissionTargetPackage;
			MissionLaptop missionLaptop = board.missionLaptop;
			log.LogInfo((object)("Modded Operations private UI launch contract: mapPrefab=" + (((Object)(object)board.MapPrefab == (Object)null) ? "null" : ((Object)board.MapPrefab).name) + ", targetPackage=" + (((Object)(object)thisMissionTargetPackage == (Object)null) ? "null" : ((Object)thisMissionTargetPackage).name) + ", targetBackref=" + (((Object)(object)thisMissionTargetPackage == (Object)null || (Object)(object)thisMissionTargetPackage.TargetPackage == (Object)null) ? "null" : ((Object)thisMissionTargetPackage.TargetPackage).name) + ", targets=" + ((board.TARGETPACKAGE == null) ? (-1) : ((Il2CppArrayBase<TARGETPACKAGE_DETAILS>)(object)board.TARGETPACKAGE).Length) + ", selectedInfil=" + board.SelectedInfiltrationTime + ", operationBoard=" + ((Object)(object)board.OperationBoardUI != (Object)null) + ", laptop=" + ((Object)(object)missionLaptop != (Object)null) + ", laptopPlayer=" + ((Object)(object)missionLaptop != (Object)null && (Object)(object)missionLaptop.playerNetworking != (Object)null) + ", operationsManager=" + ((Object)(object)OperationsManager.singleton != (Object)null) + ", gameManagerNetwork=" + ((Object)(object)GameManagerNetwork.instance != (Object)null) + "."));
		}
		catch (Exception ex)
		{
			log.LogWarning((object)("Modded Operations could not describe the native launch contract: " + ex.GetType().Name + ": " + ex.Message));
		}
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		ActiveMapOperation activeMapOperation = activeOperation;
		if (activeMapOperation != null && activeMapOperation.Map != null && ((Scene)(ref scene)).IsValid() && ((Scene)(ref scene)).isLoaded && SceneMatchesOperation(scene, activeMapOperation))
		{
			if (!ValidateStandaloneSceneContract(scene, activeMapOperation, out var error))
			{
				log.LogError((object)("Standalone package scene contract rejected map=" + activeMapOperation.Map.Id + ": " + error + "."));
				return;
			}
			ReleaseStandaloneSceneContracts(activeMapOperation);
			ShowNativeLoadingScreenForPackageScene(activeMapOperation);
			activeMapOperation.SceneHandle = SceneHandle.op_Implicit(((Scene)(ref scene)).handle);
			activeMapOperation.BootstrapRoot = null;
			activeMapOperation.BootstrapIdentity = null;
			activeMapOperation.BootstrapPrefabRoot = null;
			activeMapOperation.BootstrapPrefabIdentity = null;
			activeMapOperation.BootstrapAssetId = 0u;
			activeMapOperation.BootstrapPrefabRegistered = false;
			activeMapOperation.GameModeComponent = null;
			activeMapOperation.BootstrapCreated = false;
			activeMapOperation.NetworkSpawnRequested = false;
			activeMapOperation.NetworkSpawnFailed = false;
			activeMapOperation.BootstrapSyncObjects.Clear();
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
			activeMapOperation.PveRaidManager = null;
			activeMapOperation.PveExfilZone = null;
			activeMapOperation.PveExfilCollider = null;
		}
	}

	private void ShowNativeLoadingScreenForPackageScene(ActiveMapOperation operation)
	{
		try
		{
			GameManagerNetwork val = GameManagerNetwork.instance;
			if ((Object)(object)val == (Object)null || (Object)(object)val.LoadingScreen == (Object)null)
			{
				log.LogWarning((object)"Standalone package scene could not enter the shipped loading presentation because GameManagerNetwork or its LoadingScreen was unavailable.");
				return;
			}
			val.ShowLoadingScreen();
			log.LogInfo((object)("Standalone package scene entered the shipped GameManagerNetwork loading presentation before runtime terrain/material preparation: map=" + operation.Map.Id + ", loadingScreenActiveSelf=" + val.LoadingScreen.activeSelf + ", loadingScreenActiveInHierarchy=" + val.LoadingScreen.activeInHierarchy + ", nativeHideSoonFlag=" + val.LoadingScreenVisible + "."));
		}
		catch (Exception ex)
		{
			log.LogWarning((object)("Standalone package scene could not enter the shipped loading presentation: " + ex.GetType().Name + ": " + ex.Message));
		}
	}

	private void OnSceneUnloaded(Scene scene)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		ActiveMapOperation activeMapOperation = activeOperation;
		if (activeMapOperation != null && !(((Scene)(ref scene)).handle != SceneHandle.op_Implicit(activeMapOperation.SceneHandle)))
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
			activeMapOperation.NetworkSpawnFailed = false;
			activeMapOperation.BootstrapSyncObjects.Clear();
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
			activeMapOperation.PveRaidManager = null;
			activeMapOperation.PveExfilZone = null;
			activeMapOperation.PveExfilCollider = null;
			log.LogInfo((object)"Modded Operations map scene unloaded; package bundles remain resident so the shipped Restart Operation route can reload the same scene.");
		}
	}

	private void PrepareStandaloneScene(Scene scene, ActiveMapOperation operation)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		if (activeOperation != operation || SceneHandle.op_Implicit(operation.SceneHandle) != ((Scene)(ref scene)).handle || !((Scene)(ref scene)).IsValid() || !((Scene)(ref scene)).isLoaded)
		{
			return;
		}
		if (!TryPrepareRuntimeTerrain(scene, operation, out var error))
		{
			log.LogError((object)("Standalone package terrain preparation failed closed: map=" + operation.Map.Id + ", reason=" + error + "."));
			return;
		}
		Physics.SyncTransforms();
		if (!ValidateWalkableGroundContract(scene, operation, out var error2))
		{
			log.LogError((object)("Standalone package walkable-ground contract failed closed: map=" + operation.Map.Id + ", reason=" + error2 + "."));
			ReleaseRuntimeTerrain(operation);
		}
		else
		{
			operation.TerrainReady = true;
			ConfigureStandalonePlayerSpawnContract(scene, operation);
			CreateStandaloneGameplayBootstrap(scene, operation);
			ApplyStandaloneRenderContract(scene, operation);
			operation.ScenePreparationComplete = operation.BootstrapCreated;
			log.LogInfo((object)("Standalone package scene services are ready before native player spawn: map=" + operation.Map.Id + ", terrain=" + (operation.Map.RuntimeTerrain != null) + ", walkableGround=true, bootstrap=" + operation.BootstrapCreated + "."));
		}
	}

	private static bool SceneMatchesOperation(Scene scene, ActiveMapOperation operation)
	{
		if (operation == null || operation.Map == null || operation.SceneSelection == null || !SelectionBelongsToMap(operation.Map, operation.SceneSelection))
		{
			return false;
		}
		string scenePath = operation.SceneSelection.ScenePath;
		if (!string.IsNullOrEmpty(((Scene)(ref scene)).path) && string.Equals(((Scene)(ref scene)).path, scenePath, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return string.Equals(((Scene)(ref scene)).name, Path.GetFileNameWithoutExtension(scenePath), StringComparison.OrdinalIgnoreCase);
	}

	private bool TryPrepareRuntimeTerrain(Scene scene, ActiveMapOperation operation, out string error)
	{
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		//IL_025c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0268: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Expected O, but got Unknown
		//IL_02e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0306: Unknown result type (might be due to invalid IL or missing references)
		//IL_036b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0370: Unknown result type (might be due to invalid IL or missing references)
		//IL_037d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0388: Unknown result type (might be due to invalid IL or missing references)
		//IL_0393: Unknown result type (might be due to invalid IL or missing references)
		//IL_039e: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_03eb: Expected O, but got Unknown
		//IL_047d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0482: Unknown result type (might be due to invalid IL or missing references)
		//IL_0491: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0706: Unknown result type (might be due to invalid IL or missing references)
		//IL_070b: Unknown result type (might be due to invalid IL or missing references)
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
		if (!loadedMapBundles.TryGetValue(operation.Map.Id, out var value) || value == null || value.Map == null || !string.Equals(value.Map.PackageContentId, operation.Map.PackageContentId, StringComparison.Ordinal) || !value.DependenciesByPath.TryGetValue(val.VerifiedDependencyBundlePath, out var value2) || (Object)(object)value2 == (Object)null)
		{
			error = "the verified terrain dependency bundle is not resident";
			return false;
		}
		int matches;
		Transform val2 = FindExactSceneTransform(scene, val.RootObjectName, out matches);
		if ((Object)(object)val2 == (Object)null || matches != 1)
		{
			error = "runtimeTerrain rootObject must resolve to exactly one scene object";
			return false;
		}
		TerrainData val3 = null;
		List<TerrainLayer> list = new List<TerrainLayer>();
		try
		{
			Texture2D val4 = LoadRequiredTerrainTexture(value2, val.HeightPayloadAssetPath, requireReadable: true);
			Texture2D val5 = LoadRequiredTerrainTexture(value2, val.SurfaceWeightsPayloadAssetPath, requireReadable: true);
			if ((Object)(object)val4 == (Object)null || (Object)(object)val5 == (Object)null)
			{
				error = "one or more numerical terrain payloads could not be loaded";
				return false;
			}
			if (((Texture)val4).width != val.HeightmapResolution || ((Texture)val4).height != val.HeightmapResolution || ((Texture)val5).width != val.AlphamapResolution || ((Texture)val5).height != val.AlphamapResolution)
			{
				error = "terrain payload dimensions do not match the frozen manifest";
				return false;
			}
			Texture2D[] array = (Texture2D[])(object)new Texture2D[val.Layers.Count];
			Texture2D[] array2 = (Texture2D[])(object)new Texture2D[val.Layers.Count];
			Texture2D[] array3 = (Texture2D[])(object)new Texture2D[val.Layers.Count];
			for (int i = 0; i < val.Layers.Count; i++)
			{
				ModdedRuntimeTerrainLayerDefinition val6 = val.Layers[i];
				array[i] = LoadRequiredTerrainTexture(value2, val6.DiffuseAssetPath, requireReadable: false);
				array2[i] = LoadRequiredTerrainTexture(value2, val6.NormalAssetPath, requireReadable: false);
				array3[i] = LoadRequiredTerrainTexture(value2, val6.MaskAssetPath, requireReadable: false);
				if ((Object)(object)array[i] == (Object)null || (Object)(object)array2[i] == (Object)null || (Object)(object)array3[i] == (Object)null)
				{
					error = "one or more terrain-layer textures could not be loaded";
					return false;
				}
			}
			val3 = new TerrainData
			{
				name = "MODDED_OPERATIONS_RUNTIME_TERRAIN_" + operation.Map.Id,
				heightmapResolution = val.HeightmapResolution,
				alphamapResolution = val.AlphamapResolution,
				baseMapResolution = val.BaseMapResolution,
				size = new Vector3(val.Width, val.Height, val.Length)
			};
			val3.SetDetailResolution(val.DetailResolution, val.DetailResolutionPerPatch);
			Il2CppStructArray<Color32> pixels = val4.GetPixels32();
			Il2CppStructArray<float> val7 = AllocateIl2CppFloatArray(val.HeightmapResolution, val.HeightmapResolution);
			Span<float> span = val7.AsSpan();
			for (int j = 0; j < val.HeightmapResolution; j++)
			{
				for (int k = 0; k < val.HeightmapResolution; k++)
				{
					Color32 val8 = ((Il2CppArrayBase<Color32>)(object)pixels)[j * val.HeightmapResolution + k];
					span[j * val.HeightmapResolution + k] = (float)((val8.r << 8) | val8.g) / 65535f;
				}
			}
			val3.SetHeights(0, 0, (Il2CppObjectBase)(object)val7);
			Il2CppReferenceArray<TerrainLayer> val9 = new Il2CppReferenceArray<TerrainLayer>((long)val.Layers.Count);
			for (int l = 0; l < val.Layers.Count; l++)
			{
				ModdedRuntimeTerrainLayerDefinition val10 = val.Layers[l];
				TerrainLayer val11 = new TerrainLayer
				{
					name = val10.Name,
					diffuseTexture = array[l],
					normalMapTexture = array2[l],
					maskMapTexture = array3[l],
					tileSize = new Vector2(val10.TileSizeX, val10.TileSizeZ),
					tileOffset = Vector2.zero,
					normalScale = val10.NormalScale,
					metallic = val10.Metallic,
					smoothness = val10.Smoothness
				};
				list.Add(val11);
				((Il2CppArrayBase<TerrainLayer>)(object)val9)[l] = val11;
			}
			val3.terrainLayers = val9;
			Il2CppStructArray<Color32> pixels2 = val5.GetPixels32();
			Il2CppStructArray<float> val12 = AllocateIl2CppFloatArray(val.AlphamapResolution, val.AlphamapResolution, val.Layers.Count);
			Span<float> span2 = val12.AsSpan();
			for (int m = 0; m < val.AlphamapResolution; m++)
			{
				for (int n = 0; n < val.AlphamapResolution; n++)
				{
					Color32 val13 = ((Il2CppArrayBase<Color32>)(object)pixels2)[m * val.AlphamapResolution + n];
					float num = (float)(int)val13.r / 255f;
					float num2 = (float)(int)val13.g / 255f;
					float num3 = (float)(int)val13.b / 255f;
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
			val3.SetAlphamaps(0, 0, (Il2CppObjectBase)(object)val12);
			Terrain val14 = ((Component)val2).GetComponent<Terrain>();
			if ((Object)(object)val14 == (Object)null)
			{
				val14 = ((Component)val2).gameObject.AddComponent<Terrain>();
			}
			TerrainCollider val15 = ((Component)val2).GetComponent<TerrainCollider>();
			if ((Object)(object)val15 == (Object)null)
			{
				val15 = ((Component)val2).gameObject.AddComponent<TerrainCollider>();
			}
			val2.position = new Vector3(val.OriginX, val.OriginY, val.OriginZ);
			val14.terrainData = val3;
			val15.terrainData = val3;
			val14.materialType = (MaterialType)3;
			val14.drawInstanced = true;
			val14.drawTreesAndFoliage = false;
			val14.heightmapPixelError = 5f;
			val14.basemapDistance = 1000f;
			val14.treeDistance = 5000f;
			val14.treeBillboardDistance = 50f;
			val14.detailObjectDistance = 200f;
			val14.detailObjectDensity = 1f;
			val14.Flush();
			Transform val16 = val2.Find("NATIVE_Ground_HillyTerrain_RenderFallback");
			if ((Object)(object)val16 != (Object)null && ((Component)val16).gameObject.activeSelf)
			{
				((Component)val16).gameObject.SetActive(false);
				log.LogInfo((object)("Disabled package terrain render fallback after TerrainData bind: " + ((Object)val16).name + "."));
			}
			Physics.SyncTransforms();
			if ((Object)(object)val14.terrainData != (Object)(object)val3 || (Object)(object)val15.terrainData != (Object)(object)val3)
			{
				error = "Unity did not bind the reconstructed TerrainData to rendering and collision";
				return false;
			}
			operation.RuntimeTerrainData = val3;
			operation.RuntimeTerrainLayers.AddRange(list);
			log.LogInfo((object)("Standalone reconstructed package-owned runtime terrain: map=" + operation.Map.Id + ", root=" + val.RootObjectName + ", size=" + ((object)val3.size/*cast due to constrained. prefix*/).ToString() + ", heightmap=" + val3.heightmapResolution + ", alphamap=" + val3.alphamapResolution + ", layers=" + ((Il2CppArrayBase<TerrainLayer>)(object)val3.terrainLayers).Length + ", colliderBound=true."));
			return true;
		}
		catch (Exception ex)
		{
			error = ex.GetType().Name + ": " + ex.Message;
			return false;
		}
		finally
		{
			if ((Object)(object)operation.RuntimeTerrainData != (Object)(object)val3)
			{
				if ((Object)(object)val3 != (Object)null)
				{
					Object.Destroy((Object)(object)val3);
				}
				foreach (TerrainLayer item in list)
				{
					if ((Object)(object)item != (Object)null)
					{
						Object.Destroy((Object)(object)item);
					}
				}
			}
		}
	}

	private Texture2D LoadRequiredTerrainTexture(AssetBundle bundle, string assetPath, bool requireReadable)
	{
		string diagnostic;
		Texture2D val = NativeBundleAssetLoader.LoadTexture2D(bundle, assetPath, out diagnostic);
		if ((Object)(object)val == (Object)null)
		{
			log.LogError((object)("Required package terrain Texture2D could not be loaded: asset=" + assetPath + ", diagnostic=" + diagnostic));
			return null;
		}
		if (requireReadable && !((Texture)val).isReadable)
		{
			log.LogError((object)("Required numerical package terrain texture is not readable: " + assetPath + "."));
			return null;
		}
		return val;
	}

	private static Transform FindExactSceneTransform(Scene scene, string exactName, out int matches)
	{
		matches = 0;
		Transform val = null;
		foreach (GameObject item in (Il2CppArrayBase<GameObject>)(object)((Scene)(ref scene)).GetRootGameObjects())
		{
			foreach (Transform componentsInChild in item.GetComponentsInChildren<Transform>(true))
			{
				if ((Object)(object)componentsInChild != (Object)null && string.Equals(((Object)componentsInChild).name, exactName, StringComparison.Ordinal))
				{
					matches++;
					if (val == null)
					{
						val = componentsInChild;
					}
				}
			}
		}
		return val;
	}

	private static bool ValidateWalkableGroundContract(Scene scene, ActiveMapOperation operation, out string error)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		error = string.Empty;
		List<Transform> list = FindStandalonePlayerMarkers(scene, operation.Operation.Mode);
		if (list.Count == 0)
		{
			error = "no compatible player markers were available for ground checks";
			return false;
		}
		List<Collider> list2 = new List<Collider>();
		foreach (GameObject item in (Il2CppArrayBase<GameObject>)(object)((Scene)(ref scene)).GetRootGameObjects())
		{
			list2.AddRange(((IEnumerable<Collider>)item.GetComponentsInChildren<Collider>(true)).Where((Collider collider) => (Object)(object)collider != (Object)null && collider.enabled && ((Component)collider).gameObject.activeInHierarchy && !collider.isTrigger));
		}
		if (list2.Count == 0)
		{
			error = "the package scene contains no active non-trigger collider";
			return false;
		}
		if (operation.Map.RuntimeTerrain != null)
		{
			int matches;
			Transform val = FindExactSceneTransform(scene, operation.Map.RuntimeTerrain.RootObjectName, out matches);
			Terrain val2 = (((Object)(object)val == (Object)null) ? null : ((Component)val).GetComponent<Terrain>());
			TerrainCollider val3 = (((Object)(object)val == (Object)null) ? null : ((Component)val).GetComponent<TerrainCollider>());
			if (matches != 1 || (Object)(object)val2 == (Object)null || (Object)(object)val3 == (Object)null || (Object)(object)val2.terrainData == (Object)null || (Object)(object)val2.terrainData != (Object)(object)val3.terrainData || (Object)(object)val2.terrainData != (Object)(object)operation.RuntimeTerrainData)
			{
				error = "the declared terrain root does not own one shared render/collision TerrainData";
				return false;
			}
		}
		int num = 0;
		Ray val4 = default(Ray);
		RaycastHit val5 = default(RaycastHit);
		foreach (Transform item2 in list)
		{
			((Ray)(ref val4))._002Ector(item2.position + Vector3.up * 64f, Vector3.down);
			bool flag = false;
			foreach (Collider item3 in list2)
			{
				if (item3.Raycast(val4, ref val5, 256f))
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
		if ((Object)(object)operation.RuntimeTerrainData != (Object)null)
		{
			Object.Destroy((Object)(object)operation.RuntimeTerrainData);
		}
		operation.RuntimeTerrainData = null;
		foreach (TerrainLayer runtimeTerrainLayer in operation.RuntimeTerrainLayers)
		{
			if ((Object)(object)runtimeTerrainLayer != (Object)null)
			{
				Object.Destroy((Object)(object)runtimeTerrainLayer);
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
		IntPtr intPtr = IL2CPP.il2cpp_array_class_get(IL2CPP.il2cpp_class_get_element_class(((Il2CppObjectBase)new Il2CppStructArray<float>(1L)).ObjectClass), (uint)dimensions.Length);
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
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		if (operation == null || !((Scene)(ref scene)).IsValid() || !((Scene)(ref scene)).isLoaded)
		{
			return;
		}
		try
		{
			List<Transform> list = FindStandalonePlayerMarkers(scene, operation.Operation.Mode);
			if (list.Count == 0)
			{
				throw new InvalidOperationException("package scene has no compatible player spawn markers");
			}
			List<SpawnPoint> val = new List<SpawnPoint>();
			Il2CppReferenceArray<GameObject> val2 = new Il2CppReferenceArray<GameObject>((long)list.Count);
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < list.Count; i++)
			{
				Transform val3 = list[i];
				SpawnPoint val4 = ((Component)val3).GetComponent<SpawnPoint>() ?? ((Component)val3).gameObject.AddComponent<SpawnPoint>();
				bool flag = IsTeamTwoPlayerMarker(((Object)val3).name);
				val4.CanSpawnPlayer = true;
				val4.Team = ((!flag) ? 1 : 2);
				if (flag)
				{
					num2++;
				}
				else
				{
					num++;
				}
				val.Add(val4);
				((Il2CppArrayBase<GameObject>)(object)val2)[i] = ((Component)val3).gameObject;
			}
			RestoreStandalonePlayerSpawnContract(operation);
			operation.PreviousSpawnPoints = GameManager.SpawnPointsInScene;
			operation.OwnedSpawnPoints = val;
			operation.SpawnContractInstalled = true;
			GameManager.SpawnPointsInScene = val;
			if ((Object)(object)GameManager.instance != (Object)null)
			{
				operation.PreviousFallbackSpawns = GameManager.instance.Pspawns;
				operation.PreviousNextSpawnIndex = GameManager.instance.PnextSpawnIndex;
				operation.PreviousRandomSpawns = GameManager.instance.RandomSpawns;
				operation.OwnedRandomSpawns = false;
				operation.RandomSpawnsCaptured = true;
				operation.OwnedFallbackSpawns = val2;
				GameManager.instance.Pspawns = val2;
				GameManager.instance.PnextSpawnIndex = 0;
				GameManager.instance.RandomSpawns = operation.OwnedRandomSpawns;
			}
			log.LogInfo((object)("Standalone registered the package-owned shipped player spawn contract: total=" + list.Count + ", team1=" + num + ", team2=" + num2 + ", gameManager=" + ((Object)(object)GameManager.instance != (Object)null) + ", randomSpawns=" + (((Object)(object)GameManager.instance == (Object)null) ? "unavailable" : GameManager.instance.RandomSpawns.ToString()) + "."));
		}
		catch (Exception ex)
		{
			RestoreStandalonePlayerSpawnContract(operation);
			log.LogError((object)("Standalone package player spawn contract failed: " + ex.GetType().Name + ": " + ex.Message));
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
				GameManager.SpawnPointsInScene = (IsUsableSpawnList(operation.PreviousSpawnPoints) ? operation.PreviousSpawnPoints : new List<SpawnPoint>());
			}
			GameManager val = GameManager.instance;
			if ((Object)(object)val != (Object)null && SameNativeGameObjectArray(val.Pspawns, operation.OwnedFallbackSpawns))
			{
				val.Pspawns = (IsUsableSpawnArray(operation.PreviousFallbackSpawns) ? operation.PreviousFallbackSpawns : new Il2CppReferenceArray<GameObject>(0L));
				val.PnextSpawnIndex = Math.Max(0, operation.PreviousNextSpawnIndex);
			}
			if ((Object)(object)val != (Object)null && operation.RandomSpawnsCaptured && val.RandomSpawns == operation.OwnedRandomSpawns)
			{
				val.RandomSpawns = operation.PreviousRandomSpawns;
			}
		}
		catch
		{
			try
			{
				GameManager.SpawnPointsInScene = new List<SpawnPoint>();
				if ((Object)(object)GameManager.instance != (Object)null)
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

	private static bool SameNativeSpawnList(List<SpawnPoint> left, List<SpawnPoint> right)
	{
		if (left == right)
		{
			return true;
		}
		try
		{
			return left != null && right != null && ((Il2CppObjectBase)left).Pointer == ((Il2CppObjectBase)right).Pointer;
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
			return left != null && right != null && ((Il2CppObjectBase)left).Pointer == ((Il2CppObjectBase)right).Pointer;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsUsableSpawnList(List<SpawnPoint> spawns)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if (spawns == null)
		{
			return false;
		}
		try
		{
			int num = 0;
			while (num < spawns.Count)
			{
				SpawnPoint val = spawns[num];
				if (!((Object)(object)val == (Object)null) && !((Object)(object)((Component)val).gameObject == (Object)null))
				{
					Scene scene = ((Component)val).gameObject.scene;
					if (((Scene)(ref scene)).IsValid())
					{
						scene = ((Component)val).gameObject.scene;
						if (((Scene)(ref scene)).isLoaded)
						{
							num++;
							continue;
						}
					}
				}
				return false;
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
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		if (spawns == null)
		{
			return false;
		}
		try
		{
			int num = 0;
			while (num < ((Il2CppArrayBase<GameObject>)(object)spawns).Length)
			{
				GameObject val = ((Il2CppArrayBase<GameObject>)(object)spawns)[num];
				if (!((Object)(object)val == (Object)null))
				{
					Scene scene = val.scene;
					if (((Scene)(ref scene)).IsValid())
					{
						scene = val.scene;
						if (((Scene)(ref scene)).isLoaded)
						{
							num++;
							continue;
						}
					}
				}
				return false;
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
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Invalid comparison between Unknown and I4
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Invalid comparison between Unknown and I4
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Invalid comparison between Unknown and I4
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
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
		List<Transform> list = FindStandalonePlayerMarkers(scene, operation.Operation.Mode);
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
		if ((int)operation.Operation.Mode == 2)
		{
			List<Transform> list2 = FindSceneMarkers(scene, "PVE_ExfilZone_");
			if (list2.Count != 1)
			{
				error = "PVE mode requires exactly one PVE_ExfilZone_ marker; found=" + list2.Count;
				return false;
			}
			BoxCollider component = ((Component)list2[0]).GetComponent<BoxCollider>();
			if ((Object)(object)component == (Object)null || !((Collider)component).isTrigger || component.size.x <= 0f || component.size.y <= 0f || component.size.z <= 0f)
			{
				error = "PVE_ExfilZone_ marker requires a positive BoxCollider trigger";
				return false;
			}
		}
		if ((int)operation.Operation.Mode == 1)
		{
			bool flag = list.Any((Transform marker) => ((Object)marker).name.StartsWith("Team1", StringComparison.OrdinalIgnoreCase) || ((Object)marker).name.StartsWith("PVP_Team1", StringComparison.OrdinalIgnoreCase));
			bool flag2 = list.Any((Transform marker) => ((Object)marker).name.StartsWith("Team2", StringComparison.OrdinalIgnoreCase) || ((Object)marker).name.StartsWith("PVP_Team2", StringComparison.OrdinalIgnoreCase));
			if (!flag || !flag2)
			{
				error = "PVP mode requires separated Team1 and Team2 spawn markers";
				return false;
			}
		}
		bool flag3 = false;
		foreach (GameObject item in (Il2CppArrayBase<GameObject>)(object)((Scene)(ref scene)).GetRootGameObjects())
		{
			if (((IEnumerable<Light>)item.GetComponentsInChildren<Light>(true)).Any((Light light) => (Object)(object)light != (Object)null && (int)light.type == 1))
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
		foreach (GameObject item in (Il2CppArrayBase<GameObject>)(object)((Scene)(ref scene)).GetRootGameObjects())
		{
			foreach (Transform componentsInChild in item.GetComponentsInChildren<Transform>(true))
			{
				if ((Object)(object)componentsInChild != (Object)null && string.Equals(((Object)componentsInChild).name, name, StringComparison.Ordinal))
				{
					return true;
				}
			}
		}
		return false;
	}

	private void CreateStandaloneGameplayBootstrap(Scene scene, ActiveMapOperation operation)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Invalid comparison between Unknown and I4
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		//IL_0282: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Invalid comparison between Unknown and I4
		if (operation == null || operation.BootstrapCreated)
		{
			return;
		}
		try
		{
			GameObject val = new GameObject("MODDED_OPERATIONS_GAME_MODE_TEMPLATE");
			val.SetActive(false);
			SceneManager.MoveGameObjectToScene(val, scene);
			NetworkIdentity val2 = val.AddComponent<NetworkIdentity>();
			GameMode val3;
			uint num;
			if ((int)operation.Operation.Mode == 2)
			{
				StandalonePveGameMode standalonePveGameMode = val.AddComponent<StandalonePveGameMode>();
				((Behaviour)standalonePveGameMode).enabled = false;
				((InfiltrationManager)standalonePveGameMode).AllowRespawns = false;
				((InfiltrationManager)standalonePveGameMode).NetworkRaidTimer = 0f;
				InfiltrationManager.instance = (InfiltrationManager)(object)standalonePveGameMode;
				ConfigureStandalonePveController(scene, operation, val, standalonePveGameMode);
				val3 = (GameMode)(object)standalonePveGameMode;
				num = 1297043457u;
			}
			else
			{
				StandalonePvpGameMode standalonePvpGameMode = val.AddComponent<StandalonePvpGameMode>();
				ConfigureStandalonePvpController(scene, operation, val, standalonePvpGameMode);
				val3 = (GameMode)(object)standalonePvpGameMode;
				num = 1297043458u;
			}
			val3.isNight = ParseTimeHour(operation.TimeCode) < 6;
			GameMode.singleton = val3;
			operation.BootstrapRoot = val;
			operation.BootstrapIdentity = val2;
			operation.BootstrapPrefabRoot = val;
			operation.BootstrapPrefabIdentity = val2;
			operation.BootstrapAssetId = num;
			operation.GameModeComponent = val3;
			operation.BootstrapCreated = true;
			operation.BootstrapFrame = Time.frameCount;
			val2.assetId = num;
			val.SetActive(true);
			val.SetActive(false);
			if (!TryInitializeStandaloneBootstrapSyncObjects(operation, out var error))
			{
				throw new InvalidOperationException(error);
			}
			string[] value = ((IEnumerable<NetworkBehaviour>)val.GetComponents<NetworkBehaviour>()).Select((NetworkBehaviour item) => (!((Object)(object)item == (Object)null)) ? (((object)item).GetType().Name + ":syncObjects=" + ((item.syncObjects == null) ? "null" : item.syncObjects.Count.ToString())) : "<null-component>").ToArray();
			log.LogInfo((object)("Standalone runtime Mirror prefab prewarm state: " + string.Join(", ", value) + ", initializedOwnedSyncObjectLists=" + operation.BootstrapSyncObjects.Count + "."));
			EnsureStandaloneBootstrapPrefabRegistered(operation);
			if ((Object)(object)OperationsManager.singleton != (Object)null)
			{
				OperationsManager.singleton.IsSimulation = false;
				if ((int)operation.Operation.Mode == 2)
				{
					OperationsManager.singleton.NetworkCurrentGameMode = (GameMode)0;
					OperationsManager.singleton.AssignTeamsPVE();
				}
				else
				{
					OperationsManager.singleton.AssignTeamsTDM();
				}
			}
			log.LogInfo((object)("Standalone gameplay bootstrap created in package scene: map=" + operation.Map.Id + ", mode=" + ((object)operation.Operation.Mode/*cast due to constrained. prefix*/).ToString() + ", owner=" + ((object)val3).GetType().Name + ", mirrorAssetId=0x" + num.ToString("X8") + ", prefabRegistered=" + operation.BootstrapPrefabRegistered + ", donorScene=false, sceneHandle=" + ((object)((Scene)(ref scene)).handle/*cast due to constrained. prefix*/).ToString() + "."));
		}
		catch (Exception ex)
		{
			log.LogError((object)("Standalone gameplay bootstrap failed: " + ex.GetType().Name + ": " + ex.Message));
		}
	}

	private void ConfigureStandalonePveController(Scene scene, ActiveMapOperation operation, GameObject bootstrapRoot, StandalonePveGameMode pve)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Expected O, but got Unknown
		//IL_03a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ec: Unknown result type (might be due to invalid IL or missing references)
		if (operation == null || (Object)(object)bootstrapRoot == (Object)null || (Object)(object)pve == (Object)null)
		{
			throw new ArgumentNullException("pve");
		}
		List<Transform> list = FindSceneMarkers(scene, "PVE_ExfilZone_");
		if (list.Count != 1)
		{
			throw new InvalidOperationException("StandardPVE requires exactly one package-authored PVE_ExfilZone_ marker; found=" + list.Count);
		}
		Transform val = list[0];
		BoxCollider component = ((Component)val).GetComponent<BoxCollider>();
		if ((Object)(object)component == (Object)null || !((Collider)component).isTrigger)
		{
			throw new InvalidOperationException(((Object)val).name + " requires a BoxCollider trigger");
		}
		bootstrapRoot.layer = ((Component)val).gameObject.layer;
		bootstrapRoot.transform.SetPositionAndRotation(val.position, val.rotation);
		BoxCollider val2 = bootstrapRoot.AddComponent<BoxCollider>();
		((Collider)val2).isTrigger = true;
		val2.center = component.center;
		val2.size = component.size;
		GameObject val3 = new GameObject("MODDED_PVE_EXFIL_LOCKED_MARKER");
		val3.transform.SetParent(bootstrapRoot.transform, false);
		val3.SetActive(false);
		GameObject val4 = CreateNativeAtakExfilMarker(operation, bootstrapRoot.transform);
		val4.SetActive(false);
		ExfilZone val5 = bootstrapRoot.AddComponent<ExfilZone>();
		val5.unlockingKey = string.Empty;
		val5.notificationCooldown = 20f;
		val5._notifT = 0f;
		val5.isHelicopter = false;
		val5.InfiltrationAnimationPrefab = null;
		val5.exfilName = operation.Operation.DisplayName + " Extraction";
		val5.ExfilAnimationName = string.Empty;
		val5.exfilSpawned = false;
		val5.InfilMarker = val3;
		val5.ExfilMarker = val4;
		val5._occupants = new HashSet<int>();
		val5.NetworkPlayersInExfil = 0;
		val5.NetworkcanExtract = false;
		val5.linkedInfils = new List<string>();
		if (operation.Operation.Infiltrations != null)
		{
			for (int i = 0; i < operation.Operation.Infiltrations.Count; i++)
			{
				string displayName = operation.Operation.Infiltrations[i].DisplayName;
				if (!string.IsNullOrWhiteSpace(displayName))
				{
					val5.linkedInfils.Add(displayName);
				}
			}
		}
		RaidManager val6 = bootstrapRoot.AddComponent<RaidManager>();
		((Behaviour)val6).enabled = false;
		val6.infiltrationManager = (InfiltrationManager)(object)pve;
		val6.EXTRACT_TIMER = 15f;
		val6.objectives = new Il2CppReferenceArray<ObjectiveSetter>(0L);
		val6.missionAssets = new List<VehicleHealth>();
		val6.standardAI = new Il2CppReferenceArray<GameObject>(0L);
		val6.customAI = new List<GameObject>();
		val6.hvtSpawnPoints = new Il2CppReferenceArray<GameObject>(0L);
		val6.hvtAI = new Il2CppReferenceArray<GameObject>(0L);
		val6.staticVehicleSpawnPoints = new Il2CppReferenceArray<GameObject>(0L);
		val6.staticVehicleAI = new Il2CppReferenceArray<GameObject>(0L);
		val6.Reinforcements = new Il2CppReferenceArray<aiReinforcement>(0L);
		val6.prohibitedWeapons = new Il2CppReferenceArray<PuppetWeapon>(0L);
		val6.mapSpecificWeapons = new Il2CppReferenceArray<PuppetWeapon>(0L);
		val6.IED_locations = new Il2CppReferenceArray<Transform>(0L);
		val6.botSpawnPoints = new List<GameObject>();
		val6.allHelicopters = new List<HelicopterV2>();
		val6.objectiveObjects = new List<ObjectiveObject>();
		val6.ImportantObjectiveObjects = new List<ObjectiveObject>();
		val6.spawnVehicleAI = false;
		val6.hasReinforcements = false;
		val6.timedBackup = false;
		val6.hasIEDs = false;
		val6.hasNotified = false;
		val6.hasNotifiedEnemiesDead = false;
		val6.exfilZones = new List<ExfilZone>();
		val6.exfilZones.Add(val5);
		RaidManager.singleton = val6;
		operation.PveRaidManager = val6;
		operation.PveExfilZone = val5;
		operation.PveExfilCollider = val2;
		ResetStandalonePveExtractionState();
		log.LogInfo((object)("Standalone StandardPVE owner wired to shipped RaidManager and ExfilZone: marker=" + ((Object)val).name + ", position=" + ((object)val.position/*cast due to constrained. prefix*/).ToString() + ", triggerCenter=" + ((object)val2.center/*cast due to constrained. prefix*/).ToString() + ", triggerSize=" + ((object)val2.size/*cast due to constrained. prefix*/).ToString() + ", extractionSeconds=" + 15f + "."));
	}

	private static GameObject CreateNativeAtakExfilMarker(ActiveMapOperation operation, Transform parent)
	{
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Expected O, but got Unknown
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Expected O, but got Unknown
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_0249: Unknown result type (might be due to invalid IL or missing references)
		//IL_0254: Unknown result type (might be due to invalid IL or missing references)
		//IL_025b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0268: Expected O, but got Unknown
		//IL_0293: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f2: Unknown result type (might be due to invalid IL or missing references)
		if (operation == null || (Object)(object)parent == (Object)null)
		{
			throw new ArgumentNullException("parent");
		}
		Texture2D val = ((IEnumerable<Texture2D>)Resources.FindObjectsOfTypeAll<Texture2D>()).FirstOrDefault((Texture2D texture) => (Object)(object)texture != (Object)null && string.Equals(((Object)texture).name, "ExfilZone", StringComparison.Ordinal) && ((Texture)texture).width == 512 && ((Texture)texture).height == 512);
		if ((Object)(object)val == (Object)null)
		{
			throw new InvalidOperationException("The resident vanilla 512x512 ExfilZone texture is unavailable");
		}
		Shader val2 = Shader.Find("HDRP/Unlit");
		if ((Object)(object)val2 == (Object)null)
		{
			throw new InvalidOperationException("The resident HDRP/Unlit shader is unavailable");
		}
		GameObject val3 = new GameObject("ATAK Exfil Marker");
		val3.layer = 17;
		val3.transform.SetParent(parent, false);
		val3.transform.localPosition = Vector3.zero;
		val3.transform.localRotation = new Quaternion(-3.015905E-07f, -0.7071068f, -0.70710677f, 3.2782552E-07f);
		val3.transform.localScale = Vector3.one * 0.65f;
		Mesh val4 = new Mesh
		{
			name = "Marker"
		};
		val4.vertices = Il2CppStructArray<Vector3>.op_Implicit((Vector3[])(object)new Vector3[4]
		{
			new Vector3(-9.599999f, -5.4f, 0f),
			new Vector3(-9.599999f, 5.4f, 0f),
			new Vector3(9.599999f, 5.4f, 0f),
			new Vector3(9.599999f, -5.4f, 0f)
		});
		val4.uv = Il2CppStructArray<Vector2>.op_Implicit((Vector2[])(object)new Vector2[4]
		{
			new Vector2(0.00022763014f, 0.43788773f),
			new Vector2(0.00022763014f, 1.0001317f),
			new Vector2(0.99977237f, 1.0001317f),
			new Vector2(0.99977237f, 0.43788773f)
		});
		val4.normals = Il2CppStructArray<Vector3>.op_Implicit((Vector3[])(object)new Vector3[4]
		{
			Vector3.forward,
			Vector3.forward,
			Vector3.forward,
			Vector3.forward
		});
		val4.triangles = Il2CppStructArray<int>.op_Implicit(new int[6] { 2, 1, 0, 3, 2, 0 });
		val4.RecalculateBounds();
		val4.UploadMeshData(true);
		Material val5 = new Material(val2)
		{
			name = "ExfilZone",
			enableInstancing = true,
			renderQueue = 2501
		};
		val5.SetTexture("_MainTex", (Texture)(object)val);
		val5.SetTexture("_UnlitColorMap", (Texture)(object)val);
		val5.SetTextureOffset("_MainTex", new Vector2(0f, -0.22f));
		val5.SetTextureOffset("_UnlitColorMap", new Vector2(0f, -0.22f));
		val5.SetColor("_Color", Color.white);
		val5.SetColor("_UnlitColor", Color.white);
		val5.SetColor("_BaseColor", Color.white);
		val5.SetColor("_EmissionColor", Color.white);
		val5.SetFloat("_SurfaceType", 0f);
		val5.SetFloat("_BlendMode", 0f);
		val5.SetFloat("_ZWrite", 1f);
		val5.SetFloat("_CullMode", 2f);
		val5.SetFloat("_OpaqueCullMode", 2f);
		val5.SetFloat("_AlphaCutoffEnable", 0f);
		val5.SetFloat("_Smoothness", 1f);
		val5.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
		val5.DisableKeyword("_ALPHATEST_ON");
		val5.SetShaderPassEnabled("DistortionVectors", false);
		val5.SetShaderPassEnabled("MOTIONVECTORS", false);
		val3.AddComponent<MeshFilter>().sharedMesh = val4;
		MeshRenderer obj = val3.AddComponent<MeshRenderer>();
		((Renderer)obj).sharedMaterial = val5;
		((Renderer)obj).shadowCastingMode = (ShadowCastingMode)0;
		((Renderer)obj).receiveShadows = true;
		operation.RuntimePveAssets.Add((Object)(object)val4);
		operation.RuntimePveAssets.Add((Object)(object)val5);
		return val3;
	}

	private static void ResetStandalonePveExtractionState()
	{
		if (!NetworkServer.active)
		{
			return;
		}
		GameManagerNetwork val = GameManagerNetwork.instance;
		if (!((Object)(object)val == (Object)null))
		{
			if (val._globalExfilOccupants == null)
			{
				val._globalExfilOccupants = new HashSet<int>();
			}
			else
			{
				val._globalExfilOccupants.Clear();
			}
			val.NetworkPlayersInAnyExfil = 0;
			val.NetworkcanExtract = false;
			val.NetworkisExtracting = false;
			val.NetworkextractionStartTime = 0.0;
			val.ExfilTime = 15f;
			val.SuccessfulOperation = false;
		}
	}

	private static bool TryInitializeStandaloneBootstrapSyncObjects(ActiveMapOperation operation, out string error)
	{
		error = string.Empty;
		if (operation == null || (Object)(object)operation.BootstrapPrefabRoot == (Object)null)
		{
			error = "the operation-owned Mirror bootstrap root is unavailable";
			return false;
		}
		NetworkBehaviour[] array = Il2CppArrayBase<NetworkBehaviour>.op_Implicit(operation.BootstrapPrefabRoot.GetComponents<NetworkBehaviour>());
		if (array == null || array.Length == 0)
		{
			error = "the operation-owned Mirror bootstrap root has no NetworkBehaviour";
			return false;
		}
		for (int i = 0; i < array.Length; i++)
		{
			NetworkBehaviour val = array[i];
			if ((Object)(object)val == (Object)null)
			{
				error = "the operation-owned Mirror bootstrap root contains a null NetworkBehaviour at index " + i;
				return false;
			}
			if (val.syncObjects == null)
			{
				List<SyncObject> value = (val.syncObjects = new List<SyncObject>());
				operation.BootstrapSyncObjects.Add(new OwnedBootstrapSyncObjects
				{
					Behaviour = val,
					Value = value
				});
			}
			if (val.syncObjects == null)
			{
				error = "the operation-owned Mirror bootstrap NetworkBehaviour " + ((object)val).GetType().Name + " has no syncObjects list after baseline initialization";
				return false;
			}
		}
		return true;
	}

	private static bool ValidateStandaloneBootstrapSyncObjects(GameObject root, out string error)
	{
		error = string.Empty;
		if ((Object)(object)root == (Object)null)
		{
			error = "the operation-owned Mirror bootstrap root is unavailable";
			return false;
		}
		NetworkBehaviour[] array = Il2CppArrayBase<NetworkBehaviour>.op_Implicit(root.GetComponents<NetworkBehaviour>());
		if (array == null || array.Length == 0)
		{
			error = "the operation-owned Mirror bootstrap root has no NetworkBehaviour";
			return false;
		}
		for (int i = 0; i < array.Length; i++)
		{
			NetworkBehaviour val = array[i];
			if ((Object)(object)val == (Object)null)
			{
				error = "the operation-owned Mirror bootstrap root contains a null NetworkBehaviour at index " + i;
				return false;
			}
			if (val.syncObjects == null)
			{
				error = "the operation-owned Mirror bootstrap NetworkBehaviour " + ((object)val).GetType().Name + " has no syncObjects list";
				return false;
			}
		}
		return true;
	}

	private void EnsureStandaloneBootstrapPrefabRegistered(ActiveMapOperation operation)
	{
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		if (operation == null || operation.BootstrapPrefabRegistered || !NetworkClient.active || (Object)(object)operation.BootstrapPrefabRoot == (Object)null || (Object)(object)operation.BootstrapPrefabIdentity == (Object)null || operation.BootstrapAssetId == 0)
		{
			return;
		}
		if (!ValidateStandaloneBootstrapSyncObjects(operation.BootstrapPrefabRoot, out var error))
		{
			throw new InvalidOperationException("Mirror prefab registration refused: " + error);
		}
		Dictionary<uint, GameObject> prefabs = NetworkClient.prefabs;
		GameObject val = default(GameObject);
		if (prefabs != null && prefabs.TryGetValue(operation.BootstrapAssetId, ref val))
		{
			if (!((Object)(object)val == (Object)null))
			{
				if ((Object)(object)val == (Object)(object)operation.BootstrapPrefabRoot)
				{
					operation.BootstrapPrefabRegistered = true;
					operation.BootstrapPrefabIdentity.assetId = operation.BootstrapAssetId;
					return;
				}
				throw new InvalidOperationException("Mirror prefab asset ID collision for standalone game mode 0x" + operation.BootstrapAssetId.ToString("X8") + ": existing=" + ((Object)val).name + ".");
			}
			prefabs.Remove(operation.BootstrapAssetId);
			NetworkClient.UnregisterSpawnHandler(operation.BootstrapAssetId);
			CerberusNativeTabFix cerberusNativeTabFix = instance;
			if (cerberusNativeTabFix != null)
			{
				ManualLogSource obj = cerberusNativeTabFix.log;
				if (obj != null)
				{
					obj.LogInfo((object)("Removed destroyed standalone game-mode Mirror prefab before repeat registration: assetId=0x" + operation.BootstrapAssetId.ToString("X8") + "."));
				}
			}
		}
		NetworkClient.RegisterPrefab(operation.BootstrapPrefabRoot, operation.BootstrapAssetId);
		operation.BootstrapPrefabIdentity.assetId = operation.BootstrapAssetId;
		operation.BootstrapPrefabRegistered = true;
		log.LogInfo((object)("Standalone game-mode Mirror prefab registered on this peer: assetId=0x" + operation.BootstrapAssetId.ToString("X8") + ", mode=" + ((object)operation.Operation.Mode/*cast due to constrained. prefix*/).ToString() + "."));
	}

	private void ConfigureStandalonePvpController(Scene scene, ActiveMapOperation operation, GameObject bootstrapRoot, StandalonePvpGameMode pvp)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (operation == null || (Object)(object)bootstrapRoot == (Object)null || (Object)(object)pvp == (Object)null)
		{
			throw new ArgumentNullException("pvp");
		}
		List<SpawnPoint> val = new List<SpawnPoint>();
		List<SpawnPoint> val2 = new List<SpawnPoint>();
		foreach (Transform item in FindStandalonePlayerMarkers(scene, (ModdedOperationMode)1))
		{
			if (!((Object)(object)item == (Object)null))
			{
				SpawnPoint val3 = ((Component)item).GetComponent<SpawnPoint>() ?? ((Component)item).gameObject.AddComponent<SpawnPoint>();
				bool flag = IsTeamTwoPlayerMarker(((Object)item).name);
				val3.CanSpawnPlayer = true;
				val3.Team = ((!flag) ? 1 : 2);
				if (flag)
				{
					val2.Add(val3);
				}
				else
				{
					val.Add(val3);
				}
			}
		}
		if (val.Count == 0 || val2.Count == 0)
		{
			throw new InvalidOperationException("PvpGameode requires non-empty Team1SpawnPoints and Team2SpawnPoints lists");
		}
		((PvpGameode)pvp).Team1SpawnPoints = val;
		((PvpGameode)pvp).Team2SpawnPoints = val2;
		((PvpGameode)pvp).MaxRounds = 13;
		((PvpGameode)pvp).RoundsToWin = 7;
		((PvpGameode)pvp).currentRound = 0;
		((PvpGameode)pvp).RoundTime = 120;
		((PvpGameode)pvp).RoundTimer = 0;
		((PvpGameode)pvp).Team1Score = 0;
		((PvpGameode)pvp).Team2Score = 0;
		((PvpGameode)pvp).CurrentScoreUI = 0;
		((PvpGameode)pvp).Team1Players = new List<TeamIdentifier>();
		((PvpGameode)pvp).Team2Players = new List<TeamIdentifier>();
		((PvpGameode)pvp)._roundEnded = false;
		((PvpGameode)pvp)._roundEndTimer = 0f;
		((PvpGameode)pvp)._waitingForPlayerSpawn = false;
		((PvpGameode)pvp)._waitingForPlayerSpawnTimer = 0f;
		((PvpGameode)pvp)._respawningPlayers = false;
		((PvpGameode)pvp)._freezeTimer = 0f;
		((PvpGameode)pvp)._isFreezeTime = false;
		((PvpGameode)pvp)._roundActive = false;
		ConfigureStandalonePvpPresentation(operation, bootstrapRoot, pvp);
		PvpGameode.instance = (PvpGameode)(object)pvp;
		log.LogInfo((object)("Standalone StandardPVP owner wired to shipped PvpGameode: team1Spawns=" + val.Count + ", team2Spawns=" + val2.Count + ", MaxRounds=13, RoundsToWin=7, RoundTime=120."));
	}

	private static void ConfigureStandalonePvpPresentation(ActiveMapOperation operation, GameObject bootstrapRoot, StandalonePvpGameMode pvp)
	{
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_027a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0289: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03da: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0419: Unknown result type (might be due to invalid IL or missing references)
		//IL_0428: Unknown result type (might be due to invalid IL or missing references)
		//IL_0458: Unknown result type (might be due to invalid IL or missing references)
		//IL_0467: Unknown result type (might be due to invalid IL or missing references)
		AudioSource val = bootstrapRoot.AddComponent<AudioSource>();
		val.playOnAwake = false;
		val.loop = false;
		AudioSource val2 = bootstrapRoot.AddComponent<AudioSource>();
		val2.playOnAwake = false;
		val2.loop = false;
		((PvpGameode)pvp).MusicSource = val;
		((PvpGameode)pvp).AnnouncerSource = val2;
		AudioClip val3 = AudioClip.Create("MODDED_PVP_SILENT_ANNOUNCER", 1, 1, 48000, false);
		operation.RuntimePvpAssets.Add((Object)(object)val3);
		((PvpGameode)pvp).bluforSpawn = CreatePvpClipArray(val3, 3);
		((PvpGameode)pvp).bluforSpawnShort = CreatePvpClipArray(val3, 3);
		((PvpGameode)pvp).bluforRoundWin = CreatePvpClipArray(val3, 3);
		((PvpGameode)pvp).bluforGameWin = CreatePvpClipArray(val3, 3);
		((PvpGameode)pvp).bluforGameLose = CreatePvpClipArray(val3, 3);
		((PvpGameode)pvp).bluforRoundLose = CreatePvpClipArray(val3, 3);
		((PvpGameode)pvp).bluforGameDraw = CreatePvpClipArray(val3, 1);
		((PvpGameode)pvp).bluforRoundDraw = CreatePvpClipArray(val3, 1);
		((PvpGameode)pvp).opforSpawn = CreatePvpClipArray(val3, 3);
		((PvpGameode)pvp).opforSpawnShort = CreatePvpClipArray(val3, 3);
		((PvpGameode)pvp).opforRoundWin = CreatePvpClipArray(val3, 3);
		((PvpGameode)pvp).opforGameWin = CreatePvpClipArray(val3, 3);
		((PvpGameode)pvp).opforGameLose = CreatePvpClipArray(val3, 3);
		((PvpGameode)pvp).opforRoundLose = CreatePvpClipArray(val3, 3);
		((PvpGameode)pvp).opforGameDraw = CreatePvpClipArray(val3, 1);
		((PvpGameode)pvp).opforRoundDraw = CreatePvpClipArray(val3, 1);
		GameObject val4 = new GameObject("MODDED_PVP_NATIVE_UI")
		{
			layer = 5
		};
		val4.transform.SetParent(bootstrapRoot.transform, false);
		Canvas obj = val4.AddComponent<Canvas>();
		obj.renderMode = (RenderMode)0;
		obj.sortingOrder = 160;
		CanvasScaler obj2 = val4.AddComponent<CanvasScaler>();
		obj2.uiScaleMode = (ScaleMode)1;
		obj2.referenceResolution = new Vector2(1920f, 1080f);
		obj2.matchWidthOrHeight = 0.5f;
		TextMeshProUGUI val5 = CreatePvpLabel(val4.transform, "BLUFOR_SCORE", "0", new Vector2(0.42f, 0.94f), new Vector2(0.48f, 0.99f), 28f);
		TextMeshProUGUI clock = CreatePvpLabel(val4.transform, "ROUND_CLOCK", "02:00", new Vector2(0.48f, 0.94f), new Vector2(0.52f, 0.99f), 28f);
		TextMeshProUGUI val6 = CreatePvpLabel(val4.transform, "OPFOR_SCORE", "0", new Vector2(0.52f, 0.94f), new Vector2(0.58f, 0.99f), 28f);
		((PvpGameode)pvp).bluforScore = val5;
		((PvpGameode)pvp).opforScore = val6;
		((PvpGameode)pvp).clock = clock;
		((PvpGameode)pvp).GameUI_bluforScore = val5;
		((PvpGameode)pvp).GameUI_opforScore = val6;
		TextMeshProUGUI val7 = CreatePvpLabel(val4.transform, "ROUND_CAUSE", string.Empty, new Vector2(0.25f, 0.38f), new Vector2(0.75f, 0.48f), 30f);
		TeleType val8 = ((Component)val7).gameObject.AddComponent<TeleType>();
		val8.m_textMeshPro = (TMP_Text)(object)val7;
		val8.autoReveal = false;
		val8.label01 = string.Empty;
		val8.label02 = string.Empty;
		((PvpGameode)pvp).Cause = val8;
		GameObject val9 = CreatePvpUiGroup(val4.transform, "ROUND_END_UI");
		((PvpGameode)pvp).RoundEndAnimator = val9.AddComponent<Animator>();
		((PvpGameode)pvp).RoundWinImage = CreatePvpOutcome(val9.transform, "ROUND_WIN", "ROUND WON");
		((PvpGameode)pvp).RoundLoseImage = CreatePvpOutcome(val9.transform, "ROUND_LOSS", "ROUND LOST");
		((PvpGameode)pvp).RoundDrawImage = CreatePvpOutcome(val9.transform, "ROUND_DRAW", "ROUND DRAW");
		GameObject val10 = CreatePvpUiGroup(val4.transform, "GAME_END_UI");
		((PvpGameode)pvp).GameUiAnimator = val10.AddComponent<Animator>();
		((PvpGameode)pvp).GameWinImage = CreatePvpOutcome(val10.transform, "GAME_VICTORY", "VICTORY");
		((PvpGameode)pvp).GameLoseImage = CreatePvpOutcome(val10.transform, "GAME_DEFEAT", "DEFEAT");
		((PvpGameode)pvp).GameDrawImage = CreatePvpOutcome(val10.transform, "GAME_DRAW", "GAME DRAW");
		((PvpGameode)pvp).GameUI_Winning = CreatePvpLabel(val10.transform, "GAME_UI_WINNING", "WINNING", new Vector2(0.35f, 0.8f), new Vector2(0.65f, 0.87f), 26f);
		((PvpGameode)pvp).GameUI_Losing = CreatePvpLabel(val10.transform, "GAME_UI_LOSING", "LOSING", new Vector2(0.35f, 0.8f), new Vector2(0.65f, 0.87f), 26f);
		((PvpGameode)pvp).GameUI_Tie = CreatePvpLabel(val10.transform, "GAME_UI_TIE", "TIED", new Vector2(0.35f, 0.8f), new Vector2(0.65f, 0.87f), 26f);
		((Component)((PvpGameode)pvp).GameUI_Winning).gameObject.SetActive(false);
		((Component)((PvpGameode)pvp).GameUI_Losing).gameObject.SetActive(false);
		((Component)((PvpGameode)pvp).GameUI_Tie).gameObject.SetActive(false);
		((PvpGameode)pvp).FadeOut = "FadeOut";
		((PvpGameode)pvp).FadeIn = "FadeIn";
	}

	private static Il2CppReferenceArray<AudioClip> CreatePvpClipArray(AudioClip clip, int count)
	{
		Il2CppReferenceArray<AudioClip> val = new Il2CppReferenceArray<AudioClip>((long)count);
		for (int i = 0; i < count; i++)
		{
			((Il2CppArrayBase<AudioClip>)(object)val)[i] = clip;
		}
		return val;
	}

	private static GameObject CreatePvpUiGroup(Transform parent, string name)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected O, but got Unknown
		GameObject val = new GameObject(name)
		{
			layer = 5
		};
		val.transform.SetParent(parent, false);
		return val;
	}

	private static GameObject CreatePvpOutcome(Transform parent, string name, string text)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		GameObject obj = CreatePvpUiGroup(parent, name);
		CreatePvpLabel(obj.transform, name + "_TEXT", text, new Vector2(0.3f, 0.5f), new Vector2(0.7f, 0.62f), 48f);
		obj.SetActive(false);
		return obj;
	}

	private static TextMeshProUGUI CreatePvpLabel(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, float fontSize)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject(name)
		{
			layer = 5
		};
		val.transform.SetParent(parent, false);
		TextMeshProUGUI obj = val.AddComponent<TextMeshProUGUI>();
		((TMP_Text)obj).text = text ?? string.Empty;
		((TMP_Text)obj).fontSize = fontSize;
		((TMP_Text)obj).alignment = (TextAlignmentOptions)514;
		((Graphic)obj).color = Color.white;
		((Graphic)obj).raycastTarget = false;
		((TMP_Text)obj).enableWordWrapping = false;
		RectTransform rectTransform = ((TMP_Text)obj).rectTransform;
		rectTransform.anchorMin = anchorMin;
		rectTransform.anchorMax = anchorMax;
		rectTransform.offsetMin = Vector2.zero;
		rectTransform.offsetMax = Vector2.zero;
		((Transform)rectTransform).localScale = Vector3.one;
		return obj;
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
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Expected O, but got Unknown
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Invalid comparison between Unknown and I4
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_0303: Unknown result type (might be due to invalid IL or missing references)
		//IL_0308: Unknown result type (might be due to invalid IL or missing references)
		//IL_0309: Unknown result type (might be due to invalid IL or missing references)
		//IL_030f: Expected O, but got Unknown
		//IL_0267: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_026d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0273: Expected O, but got Unknown
		//IL_0273: Unknown result type (might be due to invalid IL or missing references)
		//IL_028d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0297: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0419: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_045b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0440: Unknown result type (might be due to invalid IL or missing references)
		//IL_049c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0481: Unknown result type (might be due to invalid IL or missing references)
		//IL_0525: Unknown result type (might be due to invalid IL or missing references)
		//IL_0549: Unknown result type (might be due to invalid IL or missing references)
		//IL_08c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_08e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0907: Unknown result type (might be due to invalid IL or missing references)
		if (operation == null || !((Scene)(ref scene)).IsValid() || !((Scene)(ref scene)).isLoaded)
		{
			return;
		}
		bool flag = HasExactSceneMarker(scene, "RENDER_PROFILE_ARCTIC_OVERCAST_V1");
		if (!HasExactSceneMarker(scene, "RENDER_PROFILE_NATIVE_OUTDOOR_V1") && !flag)
		{
			ApplySceneAuthoredRenderContract(scene, operation);
			return;
		}
		try
		{
			Light val = null;
			foreach (GameObject item in (Il2CppArrayBase<GameObject>)(object)((Scene)(ref scene)).GetRootGameObjects())
			{
				foreach (Light componentsInChild in item.GetComponentsInChildren<Light>(true))
				{
					if ((Object)(object)componentsInChild != (Object)null && (int)componentsInChild.type == 1)
					{
						val = componentsInChild;
						break;
					}
				}
				if ((Object)(object)val != (Object)null)
				{
					break;
				}
			}
			if ((Object)(object)val == (Object)null)
			{
				GameObject val2 = new GameObject("MODDED_OPERATIONS_DIRECTIONAL_LIGHT");
				SceneManager.MoveGameObjectToScene(val2, scene);
				val = val2.AddComponent<Light>();
				val.type = (LightType)1;
			}
			bool flag2 = ParseTimeHour(operation.TimeCode) < 6;
			if (flag2 && (Object)(object)GameManager.instance != (Object)null)
			{
				if (!operation.NvgColorCaptured)
				{
					operation.PreviousNvgColor = GameManager.instance.NVGColor;
					operation.NvgColorCaptured = true;
				}
				GameManager.instance.SetNVGColor(0);
				operation.WhitePhosphorApplied = true;
			}
			((Component)val).transform.rotation = (flag2 ? new Quaternion(0.1168685f, 0.6386099f, -0.6774567f, 0.3457913f) : new Quaternion(0.115319036f, 0.019461127f, 0.11803713f, 0.9860984f));
			val.color = Color.white;
			val.useColorTemperature = true;
			val.colorTemperature = (flag2 ? 9754f : (flag ? 6500f : 5500f));
			val.intensity = (flag2 ? 40f : (flag ? 65000f : 30000f));
			val.bounceIntensity = (flag2 ? 1f : (flag ? 2f : 5f));
			val.shadows = (LightShadows)2;
			val.shadowResolution = (LightShadowResolution)3;
			val.shadowStrength = 1f;
			val.shadowBias = 0.05f;
			val.shadowNormalBias = 0.4f;
			val.shadowNearPlane = 0.2f;
			HDAdditionalLightData obj = ((Component)val).GetComponent<HDAdditionalLightData>() ?? ((Component)val).gameObject.AddComponent<HDAdditionalLightData>();
			obj.lightUnit = (LightUnit)2;
			obj.intensity = val.intensity;
			obj.volumetricDimmer = 1f;
			obj.angularDiameter = 0.5f;
			if (flag2)
			{
				GameObject val3 = new GameObject("MODDED_OPERATIONS_NIGHT_AMBIENT");
				SceneManager.MoveGameObjectToScene(val3, scene);
				val3.transform.rotation = new Quaternion(0.96095735f, 0.14374454f, 0.22723518f, -0.06528974f);
				Light obj2 = val3.AddComponent<Light>();
				obj2.type = (LightType)1;
				obj2.color = Color.white;
				obj2.useColorTemperature = true;
				obj2.colorTemperature = 6570f;
				obj2.intensity = 3500f;
				obj2.bounceIntensity = 1f;
				obj2.shadows = (LightShadows)0;
				HDAdditionalLightData obj3 = val3.AddComponent<HDAdditionalLightData>();
				obj3.lightUnit = (LightUnit)2;
				obj3.intensity = 3500f;
				obj3.volumetricDimmer = 1f;
			}
			GameObject val4 = new GameObject("MODDED_OPERATIONS_OUTDOOR_ENVIRONMENT");
			SceneManager.MoveGameObjectToScene(val4, scene);
			Volume obj4 = val4.AddComponent<Volume>();
			obj4.isGlobal = true;
			obj4.priority = 500000f;
			obj4.weight = 1f;
			VolumeProfile val5 = ScriptableObject.CreateInstance<VolumeProfile>();
			((Object)val5).name = (flag ? "MODDED_OPERATIONS_ARCTIC_OVERCAST_PROFILE" : "MODDED_OPERATIONS_OUTDOOR_PROFILE");
			obj4.sharedProfile = val5;
			operation.RuntimeRenderProfiles.Add(val5);
			VisualEnvironment obj5 = val5.Add<VisualEnvironment>(true);
			((VolumeParameter<int>)(object)obj5.skyType).Override(4);
			((VolumeParameter<SkyAmbientMode>)(object)obj5.skyAmbientMode).Override((SkyAmbientMode)1);
			((VolumeParameter<RenderingSpace>)(object)obj5.renderingSpace).Override((RenderingSpace)0);
			((VolumeParameter<float>)(object)obj5.windOrientation).Override(18f);
			((VolumeParameter<float>)(object)obj5.windSpeed).Override(14f);
			PhysicallyBasedSky obj6 = val5.Add<PhysicallyBasedSky>(true);
			((VolumeParameter<PhysicallyBasedSkyModel>)(object)obj6.type).Override((PhysicallyBasedSkyModel)0);
			((VolumeParameter<bool>)(object)obj6.atmosphericScattering).Override(true);
			((VolumeParameter<float>)(object)obj6.aerosolDensity).Override(flag ? 0.32f : 0.18f);
			((VolumeParameter<Color>)(object)obj6.groundTint).Override(flag ? new Color(0.68f, 0.74f, 0.81f, 1f) : new Color(0.13f, 0.105f, 0.075f, 1f));
			((VolumeParameter<Color>)(object)obj6.horizonTint).Override(flag ? new Color(0.76f, 0.84f, 0.92f, 1f) : new Color(0.96f, 0.98f, 1f, 1f));
			((VolumeParameter<Color>)(object)obj6.zenithTint).Override(flag ? new Color(0.48f, 0.6f, 0.74f, 1f) : new Color(0.84f, 0.91f, 1f, 1f));
			if (flag)
			{
				Fog obj7 = val5.Add<Fog>(false);
				((VolumeParameter<bool>)(object)obj7.enabled).Override(true);
				((VolumeParameter<bool>)(object)obj7.enableVolumetricFog).Override(true);
				((VolumeParameter<float>)(object)obj7.meanFreePath).Override(50f);
				((VolumeParameter<float>)(object)obj7.baseHeight).Override(-12f);
				((VolumeParameter<float>)(object)obj7.maximumHeight).Override(170f);
				((VolumeParameter<float>)(object)obj7.maxFogDistance).Override(900f);
				((VolumeParameter<Color>)(object)obj7.albedo).Override(new Color(0.82f, 0.89f, 0.96f, 1f));
				((VolumeParameter<Color>)(object)obj7.tint).Override(new Color(0.75f, 0.84f, 0.94f, 1f));
				((VolumeParameter<float>)(object)obj7.depthExtent).Override(650f);
				((VolumeParameter<float>)(object)obj7.anisotropy).Override(0.2f);
				((VolumeParameter<float>)(object)obj7.globalLightProbeDimmer).Override(1f);
				((VolumeParameter<float>)(object)obj7.sliceDistributionUniformity).Override(0.7f);
				((VolumeParameter<float>)(object)obj7.multipleScatteringIntensity).Override(1f);
			}
			Exposure obj8 = val5.Add<Exposure>(false);
			((VolumeParameter<ExposureMode>)(object)obj8.mode).Override((ExposureMode)4);
			((VolumeParameter<MeteringMode>)(object)obj8.meteringMode).Override((MeteringMode)(flag2 ? 4 : 2));
			((VolumeParameter<float>)(object)obj8.fixedExposure).Override(flag2 ? 8.32f : 10f);
			((VolumeParameter<float>)(object)obj8.compensation).Override(flag2 ? 1.16f : 0f);
			((VolumeParameter<float>)(object)obj8.limitMin).Override(flag2 ? 5.065282f : 8.5f);
			((VolumeParameter<float>)(object)obj8.limitMax).Override(flag2 ? 9.348571f : 11f);
			((VolumeParameter<float>)(object)obj8.adaptationSpeedDarkToLight).Override(flag2 ? 3f : 0.5f);
			((VolumeParameter<float>)(object)obj8.adaptationSpeedLightToDark).Override(flag2 ? 3f : 0.5f);
			Tonemapping val6 = val5.Add<Tonemapping>(false);
			Texture3D val7 = ((flag2 || flag) ? null : LoadPackageTonemapLut(operation.Map));
			if (flag2)
			{
				((VolumeParameter<TonemappingMode>)(object)val6.mode).Override((TonemappingMode)2);
				((VolumeParameter<bool>)(object)val6.useFullACES).Override(true);
			}
			else if ((Object)(object)val7 != (Object)null)
			{
				((VolumeParameter<TonemappingMode>)(object)val6.mode).Override((TonemappingMode)4);
				((VolumeParameter<bool>)(object)val6.useFullACES).Override(false);
				((VolumeParameter<Texture>)(object)val6.lutTexture).Override((Texture)(object)val7);
			}
			else
			{
				((VolumeParameter<TonemappingMode>)(object)val6.mode).Override((TonemappingMode)2);
				((VolumeParameter<bool>)(object)val6.useFullACES).Override(true);
			}
			Bloom val8 = val5.Add<Bloom>(false);
			((VolumeParameter<int>)(object)((VolumeComponentWithQuality)val8).quality).Override(flag2 ? 1 : 3);
			((VolumeParameter<float>)(object)val8.intensity).Override(flag2 ? 0.3f : 0.03f);
			((VolumeParameter<float>)(object)val8.threshold).Override(0.9f);
			((VolumeParameter<float>)(object)val8.scatter).Override(flag2 ? 0.2f : 0.893f);
			((VolumeParameter<bool>)(object)val8.anamorphic).Override(false);
			if (flag2)
			{
				((VolumeParameter<BloomResolution>)(object)val8.m_Resolution).Override((BloomResolution)2);
			}
			ScreenSpaceLensFlare val9 = val5.Add<ScreenSpaceLensFlare>(false);
			((VolumeParameter<float>)(object)val9.intensity).Override(flag2 ? 1f : 0.5f);
			((VolumeParameter<float>)(object)val9.streaksIntensity).Override(flag2 ? 1f : 1.55f);
			((VolumeParameter<float>)(object)val9.streaksLength).Override(flag2 ? 0.091f : 0.022f);
			if (!flag2)
			{
				((VolumeParameter<float>)(object)val9.streaksOrientation).Override(0f);
				((VolumeParameter<float>)(object)val9.chromaticAbberationIntensity).Override(0.6f);
			}
			ColorAdjustments obj9 = val5.Add<ColorAdjustments>(false);
			((VolumeParameter<float>)(object)obj9.postExposure).Override(flag2 ? 0f : (flag ? 1.2f : (-0.3f)));
			((VolumeParameter<float>)(object)obj9.contrast).Override(flag2 ? 17.3f : (flag ? 12f : 30f));
			((VolumeParameter<float>)(object)obj9.saturation).Override(flag2 ? 22f : (flag ? (-10f) : (-15f)));
			if (!flag2)
			{
				WhiteBalance obj10 = val5.Add<WhiteBalance>(false);
				((VolumeParameter<float>)(object)obj10.temperature).Override(flag ? (-18f) : (-3.6f));
				((VolumeParameter<float>)(object)obj10.tint).Override(flag ? (-4f) : (-8.6f));
				LiftGammaGain obj11 = val5.Add<LiftGammaGain>(false);
				((VolumeParameter<Vector4>)(object)obj11.lift).Override(new Vector4(1f, 1f, 1f, 0.00827304f));
				((VolumeParameter<Vector4>)(object)obj11.gamma).Override(new Vector4(1f, 1f, 1f, -0.09100296f));
				((VolumeParameter<Vector4>)(object)obj11.gain).Override(new Vector4(1f, 1f, 1f, 0.09100296f));
			}
			if (flag2)
			{
				IndirectLightingController obj12 = val5.Add<IndirectLightingController>(false);
				((VolumeParameter<float>)(object)obj12.indirectDiffuseLightingMultiplier).Override(1f);
				((VolumeParameter<float>)(object)obj12.reflectionLightingMultiplier).Override(1f);
				((VolumeParameter<float>)(object)obj12.reflectionProbeIntensityMultiplier).Override(1f);
			}
			HDShadowSettings val10 = val5.Add<HDShadowSettings>(false);
			((VolumeParameter<float>)(object)val10.maxShadowDistance).Override(flag2 ? 200f : 125f);
			if (flag2)
			{
				((VolumeParameter<float>)(object)val10.cascadeShadowSplit0).Override(0.05f);
			}
			string text = (flag ? "arctic-overcast-v1" : "native-outdoor-v1");
			log.LogInfo((object)("Standalone render contract applied from package-owned scene plus its explicit " + text + " profile marker: time=" + operation.TimeCode + ", sunLux=" + val.intensity + ", sunTemperature=" + val.colorTemperature + ", sunBounce=" + val.bounceIntensity + ", profileSource=" + (flag2 ? "PVP-map night" : "PVP Woods Warehouse day") + ", bloom=" + (flag2 ? 0.3f : 0.03f) + ", lensFlare=" + (flag2 ? 1f : 0.5f) + ", nightAmbient=" + flag2 + ", whitePhosphor=" + operation.WhitePhosphorApplied + ", volumetricFog=" + flag + ", externalLut=" + ((Object)(object)val7 != (Object)null) + "."));
		}
		catch (Exception ex)
		{
			log.LogWarning((object)("Standalone HDRP render contract fell back to the scene-authored light: " + ex.GetType().Name + ": " + ex.Message));
		}
	}

	private void ApplySceneAuthoredRenderContract(Scene scene, ActiveMapOperation operation)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected O, but got Unknown
		try
		{
			Texture3D val = LoadPackageTonemapLut(operation.Map);
			if ((Object)(object)val != (Object)null)
			{
				GameObject val2 = new GameObject("MODDED_OPERATIONS_PACKAGE_EXTERNAL_TONEMAP");
				SceneManager.MoveGameObjectToScene(val2, scene);
				Volume obj = val2.AddComponent<Volume>();
				obj.isGlobal = true;
				obj.priority = 500000f;
				obj.weight = 1f;
				VolumeProfile val3 = ScriptableObject.CreateInstance<VolumeProfile>();
				((Object)val3).name = "MODDED_OPERATIONS_PACKAGE_TONEMAP_PROFILE";
				obj.sharedProfile = val3;
				operation.RuntimeRenderProfiles.Add(val3);
				Tonemapping obj2 = val3.Add<Tonemapping>(false);
				((VolumeParameter<TonemappingMode>)(object)obj2.mode).Override((TonemappingMode)4);
				((VolumeParameter<bool>)(object)obj2.useFullACES).Override(true);
				((VolumeParameter<Texture>)(object)obj2.lutTexture).Override((Texture)(object)val);
			}
			log.LogInfo((object)("Standalone render contract retained package scene lighting: map=" + operation.Map.Id + ", time=" + operation.TimeCode + ", externalLut=" + ((Object)(object)val != (Object)null) + ", adapterPreset=false."));
		}
		catch (Exception ex)
		{
			log.LogWarning((object)("Package scene lighting was retained, but its optional external tonemap LUT could not be applied: " + ex.GetType().Name + ": " + ex.Message));
		}
	}

	private Texture3D LoadPackageTonemapLut(ModdedMapDefinition map)
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Expected O, but got Unknown
		if (map == null || map.ExternalTonemapLut == null)
		{
			return null;
		}
		if (packageTonemapLuts.TryGetValue(map.Id, out var value) && (Object)(object)value != (Object)null)
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
			Texture3D val = new Texture3D(externalTonemapLut.Dimension, externalTonemapLut.Dimension, externalTonemapLut.Dimension, (TextureFormat)17, false)
			{
				name = "PACKAGE_EXTERNAL_TONEMAP_LUT_" + map.Id,
				wrapMode = (TextureWrapMode)1,
				filterMode = (FilterMode)1,
				anisoLevel = 0
			};
			Il2CppStructArray<byte> val2 = new Il2CppStructArray<byte>(array);
			val.SetPixelData<byte>((Il2CppArrayBase<byte>)(object)val2, 0, 0);
			val.Apply(false, false);
			packageTonemapLuts[map.Id] = val;
			return val;
		}
		catch (Exception ex)
		{
			string item = map.Id + "|" + ex.GetType().FullName + "|" + ex.Message;
			if (lutDiagnostics.Add(item))
			{
				log.LogWarning((object)("Package tonemap LUT reconstruction failed for map=" + map.Id + ": " + ex.GetType().Name + ": " + ex.Message));
			}
		}
		return null;
	}

	private bool TryClaimStandaloneReadinessInitialization(GameMode gameMode)
	{
		ActiveMapOperation activeMapOperation = activeOperation;
		if (activeMapOperation == null || (Object)(object)gameMode == (Object)null)
		{
			return false;
		}
		if ((Object)(object)activeMapOperation.GameModeComponent != (Object)(object)gameMode && !TryAdoptNetworkSpawnedGameMode(activeMapOperation, gameMode))
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
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		if (operation == null || (Object)(object)gameMode == (Object)null || operation.BootstrapAssetId == 0 || (Object)(object)((Component)gameMode).gameObject == (Object)null)
		{
			return false;
		}
		NetworkIdentity component = ((Component)gameMode).GetComponent<NetworkIdentity>();
		if ((Object)(object)component == (Object)null || component.assetId != operation.BootstrapAssetId)
		{
			return false;
		}
		if (!(((int)operation.Operation.Mode == 2) ? (gameMode is StandalonePveGameMode) : (gameMode is StandalonePvpGameMode)))
		{
			return false;
		}
		operation.BootstrapRoot = ((Component)gameMode).gameObject;
		operation.BootstrapIdentity = component;
		operation.GameModeComponent = gameMode;
		GameMode.singleton = gameMode;
		if (gameMode is StandalonePveGameMode infiltrationManager)
		{
			RaidManager component2 = ((Component)gameMode).GetComponent<RaidManager>();
			ExfilZone component3 = ((Component)gameMode).GetComponent<ExfilZone>();
			BoxCollider component4 = ((Component)gameMode).GetComponent<BoxCollider>();
			if ((Object)(object)component2 == (Object)null || (Object)(object)component3 == (Object)null || (Object)(object)component4 == (Object)null || !((Collider)component4).isTrigger)
			{
				return false;
			}
			InfiltrationManager.instance = (InfiltrationManager)(object)infiltrationManager;
			RaidManager.singleton = component2;
			component2.infiltrationManager = (InfiltrationManager)(object)infiltrationManager;
			component2.EXTRACT_TIMER = 15f;
			component2.exfilZones = new List<ExfilZone>();
			component2.exfilZones.Add(component3);
			operation.PveRaidManager = component2;
			operation.PveExfilZone = component3;
			operation.PveExfilCollider = component4;
		}
		if (gameMode is StandalonePvpGameMode standalonePvpGameMode)
		{
			PvpGameode.instance = (PvpGameode)(object)standalonePvpGameMode;
		}
		log.LogInfo((object)("Standalone game-mode Mirror spawn adopted on this peer: assetId=0x" + operation.BootstrapAssetId.ToString("X8") + ", netId=" + component.netId + ", mode=" + ((object)operation.Operation.Mode/*cast due to constrained. prefix*/).ToString() + "."));
		return true;
	}

	private void MarkStandaloneReadinessInitialized(GameMode gameMode, string source)
	{
		ActiveMapOperation activeMapOperation = activeOperation;
		if (activeMapOperation != null && !((Object)(object)activeMapOperation.GameModeComponent != (Object)(object)gameMode))
		{
			activeMapOperation.ReadinessInitialized = true;
			log.LogInfo((object)("Standalone game mode entered the shipped readiness coroutines through GameMode.Initialize(): source=" + source + "."));
		}
	}

	private void MarkStandaloneReadinessInitializationFailed(GameMode gameMode, string source, Exception exception)
	{
		ActiveMapOperation activeMapOperation = activeOperation;
		if (activeMapOperation != null && !((Object)(object)activeMapOperation.GameModeComponent != (Object)(object)gameMode))
		{
			activeMapOperation.ReadinessInitializationClaimed = false;
			activeMapOperation.ReadinessInitialized = false;
			log.LogError((object)("Standalone readiness initialization failed closed: source=" + source + ", " + exception.GetType().Name + ": " + exception.Message));
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
			log.LogInfo((object)("Standalone game mode received the shipped all-players-loaded barrier for operation=" + activeMapOperation.Operation.Id + ", nativePvpLifecycle=" + nativePvpLifecycle + "."));
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
			log.LogError((object)("Shipped PvpGameode.Server_AllPlayersLoaded failed; using the bounded position-only fallback for this session: " + exception.GetType().Name + ": " + exception.Message));
			SpawnAndPositionStandalonePlayers(activeMapOperation, allowSpawnRequest: true);
		}
	}

	private void MaintainStandaloneGameplay()
	{
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Invalid comparison between Unknown and I4
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		ActiveMapOperation activeMapOperation = activeOperation;
		if (activeMapOperation == null || activeMapOperation.SceneHandle == 0)
		{
			return;
		}
		StandalonePveGameMode standalonePveGameMode = activeMapOperation.GameModeComponent as StandalonePveGameMode;
		if (NetworkServer.active && activeMapOperation.AllPlayersLoaded && (Object)(object)standalonePveGameMode != (Object)null)
		{
			((InfiltrationManager)standalonePveGameMode).NetworkRaidTimer = ((InfiltrationManager)standalonePveGameMode).NetworkRaidTimer + Time.deltaTime;
		}
		if (!activeMapOperation.ScenePreparationComplete && !activeMapOperation.ScenePreparationStarted && Time.frameCount >= activeMapOperation.ScenePreparationEarliestFrame)
		{
			activeMapOperation.ScenePreparationStarted = true;
			Scene scene = FindLoadedSceneByHandle(activeMapOperation.SceneHandle);
			PrepareStandaloneScene(scene, activeMapOperation);
		}
		if (!activeMapOperation.ScenePreparationComplete || !activeMapOperation.TerrainReady || (Object)(object)activeMapOperation.BootstrapRoot == (Object)null || Time.frameCount < activeMapOperation.LastMaintenanceFrame + 15)
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
			log.LogError((object)("Standalone game-mode Mirror prefab registration failed closed: " + ex.GetType().Name + ": " + ex.Message));
			return;
		}
		if (NetworkServer.active && !activeMapOperation.NetworkSpawnRequested && !activeMapOperation.NetworkSpawnFailed && (Object)(object)activeMapOperation.BootstrapIdentity != (Object)null && activeMapOperation.BootstrapAssetId != 0)
		{
			if (!ValidateStandaloneBootstrapSyncObjects(activeMapOperation.BootstrapRoot, out var error))
			{
				activeMapOperation.NetworkSpawnFailed = true;
				log.LogError((object)("Standalone game mode network spawn failed closed before NetworkServer.Spawn: " + error + "."));
				return;
			}
			try
			{
				activeMapOperation.BootstrapRoot.SetActive(true);
				activeMapOperation.NetworkSpawnRequested = true;
				NetworkServer.Spawn(activeMapOperation.BootstrapRoot, activeMapOperation.BootstrapAssetId, (NetworkConnection)null);
				log.LogInfo((object)("Standalone game mode network identity spawned by the host: assetId=0x" + activeMapOperation.BootstrapAssetId.ToString("X8") + "."));
			}
			catch (Exception ex2)
			{
				activeMapOperation.NetworkSpawnFailed = true;
				activeMapOperation.BootstrapRoot.SetActive(false);
				log.LogError((object)("Standalone game mode network spawn failed closed on its single native attempt: " + ex2.GetType().Name + ": " + ex2.Message));
			}
		}
		if (!NetworkServer.active)
		{
			return;
		}
		if (!activeMapOperation.ReadinessInitialized && activeMapOperation.NetworkSpawnRequested && !activeMapOperation.NetworkSpawnFailed && NetworkClient.active && (Object)(object)activeMapOperation.GameModeComponent != (Object)null && Time.frameCount >= activeMapOperation.BootstrapFrame + 30)
		{
			EnsureStandaloneReadiness(activeMapOperation.GameModeComponent, "bounded host fallback after network spawn");
		}
		if (activeMapOperation.AllPlayersLoaded)
		{
			if (!activeMapOperation.NativePvpLifecycleActive)
			{
				SpawnAndPositionStandalonePlayers(activeMapOperation, allowSpawnRequest: true);
			}
			MaintainOwnedStandaloneWeaponAuthority(activeMapOperation);
			if ((int)activeMapOperation.Operation.Mode == 2 && !activeMapOperation.PveSpawnAttempted && activeMapOperation.AllPlayersLoadedFrame >= 0 && Time.frameCount >= activeMapOperation.AllPlayersLoadedFrame + 180)
			{
				TrySpawnStandalonePveEnemies(activeMapOperation);
			}
			ProcessProfiledPveAiDiagnostics(activeMapOperation);
		}
	}

	private static void EnsureStandaloneReadiness(GameMode gameMode, string source)
	{
		StandalonePveGameMode standalonePveGameMode = gameMode as StandalonePveGameMode;
		if ((Object)(object)standalonePveGameMode != (Object)null)
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
				if ((Object)(object)GameManager.instance != (Object)null)
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
			if ((Object)(object)runtimeRenderProfile != (Object)null)
			{
				Object.Destroy((Object)(object)runtimeRenderProfile);
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
		if ((Object)(object)gameModeComponent != (Object)null && (Object)(object)InfiltrationManager.instance == (Object)(object)gameModeComponent)
		{
			InfiltrationManager.instance = null;
		}
		if ((Object)(object)gameModeComponent != (Object)null && (Object)(object)PvpGameode.instance == (Object)(object)gameModeComponent)
		{
			PvpGameode.instance = null;
		}
		if ((Object)(object)gameModeComponent != (Object)null && (Object)(object)GameMode.singleton == (Object)(object)gameModeComponent)
		{
			GameMode.singleton = null;
		}
		uint bootstrapAssetId = operation.BootstrapAssetId;
		GameObject bootstrapRoot = operation.BootstrapRoot;
		GameObject bootstrapPrefabRoot = operation.BootstrapPrefabRoot;
		if ((Object)(object)bootstrapRoot != (Object)null && NetworkServer.active && operation.NetworkSpawnRequested)
		{
			try
			{
				NetworkServer.UnSpawn(bootstrapRoot);
			}
			catch (Exception ex)
			{
				CerberusNativeTabFix cerberusNativeTabFix = instance;
				if (cerberusNativeTabFix != null)
				{
					ManualLogSource obj = cerberusNativeTabFix.log;
					if (obj != null)
					{
						obj.LogWarning((object)("Standalone Mirror bootstrap unspawn cleanup was incomplete: " + ex.GetType().Name + ": " + ex.Message));
					}
				}
			}
		}
		if ((Object)(object)bootstrapPrefabRoot != (Object)null)
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
		if ((Object)(object)bootstrapRoot != (Object)null)
		{
			Object.Destroy((Object)(object)bootstrapRoot);
		}
		operation.BootstrapSyncObjects.Clear();
		operation.BootstrapIdentity = null;
		operation.BootstrapRoot = null;
		operation.GameModeComponent = null;
		operation.NetworkSpawnRequested = false;
		operation.NetworkSpawnFailed = false;
		foreach (Object runtimePveAsset in operation.RuntimePveAssets)
		{
			if (runtimePveAsset != (Object)null)
			{
				Object.Destroy(runtimePveAsset);
			}
		}
		operation.RuntimePveAssets.Clear();
		foreach (Object runtimePvpAsset in operation.RuntimePvpAssets)
		{
			if (runtimePvpAsset != (Object)null)
			{
				Object.Destroy(runtimePvpAsset);
			}
		}
		operation.RuntimePvpAssets.Clear();
		if ((Object)(object)operation.PveRaidManager != (Object)null && (Object)(object)RaidManager.singleton == (Object)(object)operation.PveRaidManager)
		{
			RaidManager.singleton = null;
		}
		GameManagerNetwork val = GameManagerNetwork.instance;
		if (NetworkServer.active && (Object)(object)val != (Object)null && !val.SuccessfulOperation)
		{
			if (val._globalExfilOccupants != null)
			{
				val._globalExfilOccupants.Clear();
			}
			val.NetworkPlayersInAnyExfil = 0;
			val.NetworkcanExtract = false;
			val.NetworkisExtracting = false;
			val.NetworkextractionStartTime = 0.0;
		}
		operation.PveRaidManager = null;
		operation.PveExfilZone = null;
		operation.PveExfilCollider = null;
	}

	private void TrySpawnStandalonePveEnemies(ActiveMapOperation operation)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		if (operation == null || operation.PveSpawnAttempted || !NetworkServer.active)
		{
			return;
		}
		operation.PveSpawnAttempted = true;
		Scene scene = FindLoadedSceneByHandle(operation.SceneHandle);
		if (!((Scene)(ref scene)).IsValid() || !((Scene)(ref scene)).isLoaded)
		{
			return;
		}
		List<Transform> list = FindSceneMarkers(scene, "PVE_EnemySpawn_");
		if (list.Count == 0)
		{
			log.LogError((object)"Standalone PVE package has no PVE_EnemySpawn_ markers.");
			return;
		}
		int minimumEnemies = operation.Operation.MinimumEnemies;
		int maximumEnemies = operation.Operation.MaximumEnemies;
		if (minimumEnemies < 1 || maximumEnemies < minimumEnemies)
		{
			log.LogError((object)("Standalone PVE package has invalid enemy bounds: operation=" + operation.Operation.Id + ", requestedRange=" + minimumEnemies + "-" + maximumEnemies + "."));
			return;
		}
		if (list.Count < minimumEnemies)
		{
			log.LogError((object)("Standalone PVE package does not author enough enemy markers for its declared minimum: operation=" + operation.Operation.Id + ", requestedRange=" + minimumEnemies + "-" + maximumEnemies + ", markers=" + list.Count + "."));
			return;
		}
		GameManager val = GameManager.instance;
		List<GameObject> val2 = (((Object)(object)val == (Object)null) ? null : val.AllAITypes);
		List<GameObject> list2 = new List<GameObject>();
		if (val2 != null)
		{
			for (int i = 0; i < val2.Count; i++)
			{
				GameObject val3 = val2[i];
				BrainAI val4 = (((Object)(object)val3 == (Object)null) ? null : val3.GetComponent<BrainAI>());
				NetworkIdentity val5 = (((Object)(object)val3 == (Object)null) ? null : val3.GetComponent<NetworkIdentity>());
				WeaponsAI val6 = (((Object)(object)val4 == (Object)null) ? null : (val4.weapons ?? val3.GetComponentInChildren<WeaponsAI>(true)));
				if (!((Object)(object)val3 == (Object)null) && !((Object)(object)val4 == (Object)null) && !((Object)(object)val5 == (Object)null) && !((Object)(object)val6 == (Object)null) && val6.SpawnWeapon && val6.weaponList != null && val6.weaponList.Count != 0)
				{
					list2.Add(val3);
				}
			}
		}
		if (list2.Count == 0)
		{
			log.LogError((object)"Standalone PVE could not find a server-registered AI prefab in persistent GameManager.AllAITypes.");
			return;
		}
		try
		{
			RaidManager pveRaidManager = operation.PveRaidManager;
			if ((Object)(object)pveRaidManager == (Object)null || (Object)(object)operation.PveExfilZone == (Object)null)
			{
				throw new InvalidOperationException("StandardPVE network owner is missing RaidManager or ExfilZone");
			}
			RaidManager.singleton = pveRaidManager;
			int val7 = ChooseStandalonePveEnemyCount(operation);
			int num = Math.Min(val7, list.Count);
			GameMode gameModeComponent = operation.GameModeComponent;
			pveRaidManager.infiltrationManager = (InfiltrationManager)(object)((gameModeComponent is InfiltrationManager) ? gameModeComponent : null);
			pveRaidManager.spawnVehicleAI = false;
			pveRaidManager.hasIEDs = false;
			pveRaidManager.hasReinforcements = false;
			pveRaidManager.timedBackup = false;
			pveRaidManager.standardAI = new Il2CppReferenceArray<GameObject>((long)list2.Count);
			for (int j = 0; j < list2.Count; j++)
			{
				((Il2CppArrayBase<GameObject>)(object)pveRaidManager.standardAI)[j] = list2[j];
			}
			pveRaidManager.prohibitedWeapons = new Il2CppReferenceArray<PuppetWeapon>(0L);
			pveRaidManager.mapSpecificWeapons = new Il2CppReferenceArray<PuppetWeapon>(0L);
			pveRaidManager.EXTRACT_TIMER = 15f;
			pveRaidManager.exfilZones = new List<ExfilZone>();
			pveRaidManager.exfilZones.Add(operation.PveExfilZone);
			pveRaidManager.botSpawnPoints = new List<GameObject>();
			ModdedPveAiProfileDefinition pveAiProfile = operation.Operation.PveAiProfile;
			foreach (Transform item in list)
			{
				ConfigureStandaloneBotDetails(((Component)item).GetComponent<BotSpawnDetails>() ?? ((Component)item).gameObject.AddComponent<BotSpawnDetails>(), pveAiProfile);
				pveRaidManager.botSpawnPoints.Add(((Component)item).gameObject);
			}
			val.botAmount = num;
			val.botHVTAmount = 0;
			CaptureProfiledPvePreexistingBrains(operation, val);
			pveRaidManager.ServerSpawnAI(false);
			pveRaidManager.exfilZones = new List<ExfilZone>();
			pveRaidManager.exfilZones.Add(operation.PveExfilZone);
			operation.PveEnemyCount = num;
			StartProfiledPveAiDiagnostics(operation, val);
			log.LogInfo((object)("Standalone PVE released a server-owned AI population through shipped RaidManager.ServerSpawnAI: count=" + num + ", requestedRange=" + minimumEnemies + "-" + maximumEnemies + ", chosen=" + val7 + ", markers=" + list.Count + ", firearmCapablePrefabs=" + list2.Count + ", raidExfilsAfterNativeSpawn=" + pveRaidManager.exfilZones.Count + ", aiProfile=" + FormatPveAiProfile(pveAiProfile) + "."));
		}
		catch (Exception ex)
		{
			log.LogError((object)("Standalone PVE spawn failed closed after " + operation.PveEnemyCount + " confirmed AI: " + ex.GetType().Name + ": " + ex.Message));
		}
	}

	private static bool IsProfiledPveDiagnosticOperation(ActiveMapOperation operation)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Invalid comparison between Unknown and I4
		if (operation?.Operation != null && (int)operation.Operation.Mode == 2)
		{
			return operation.Operation.PveAiProfile != null;
		}
		return false;
	}

	private static void CaptureProfiledPvePreexistingBrains(ActiveMapOperation operation, GameManager gameManager)
	{
		if (!IsProfiledPveDiagnosticOperation(operation) || ((gameManager != null) ? gameManager.allAI : null) == null)
		{
			return;
		}
		operation.ProfiledPvePreexistingBrainIds.Clear();
		for (int i = 0; i < gameManager.allAI.Count; i++)
		{
			BrainAI val = gameManager.allAI[i];
			if ((Object)(object)val != (Object)null)
			{
				operation.ProfiledPvePreexistingBrainIds.Add(((Object)val).GetInstanceID());
			}
		}
	}

	private void StartProfiledPveAiDiagnostics(ActiveMapOperation operation, GameManager gameManager)
	{
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		if (IsProfiledPveDiagnosticOperation(operation))
		{
			operation.ProfiledPveDiagnosticBrains.Clear();
			operation.ProfiledPveInitialBrainPositions.Clear();
			operation.ProfiledPveAiDiagnosticStartedAt = -1f;
			operation.ProfiledPveAiDiagnosticNextProbeAt = Time.realtimeSinceStartup;
			operation.ProfiledPveAiDiagnosticSnapshotIndex = 0;
			operation.ProfiledPveAiDiagnosticComplete = false;
			operation.ProfiledPveAiDiagnosticAwaitingBrains = true;
			operation.ProfiledPveNativeContractLogged = false;
			operation.ProfiledPveInitialWanderDelayHandledBrainIds.Clear();
			operation.ProfiledPveInitialWanderDelayAdvanced = 0;
			operation.ProfiledPveInitialWanderDelayPreserved = 0;
			operation.ProfiledPveInitialWanderDelaySkipped = 0;
			GameObject val = (((Object)(object)gameManager == (Object)null) ? null : GameManager.myPlayer);
			operation.ProfiledPveInitialPlayerPositionCaptured = (Object)(object)val != (Object)null;
			if ((Object)(object)val != (Object)null)
			{
				operation.ProfiledPveInitialPlayerPosition = val.transform.position;
			}
			if (!TryBeginProfiledPveAiDiagnostics(operation, gameManager, "spawn"))
			{
				log.LogInfo((object)("Profiled PVE AI diagnostic is waiting for the native NetworkServer spawn callback for operation=" + operation.Operation.Id + "."));
			}
		}
	}

	private bool TryBeginProfiledPveAiDiagnostics(ActiveMapOperation operation, GameManager gameManager, string source)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		RefreshProfiledPveDiagnosticBrains(operation, gameManager);
		if (operation.ProfiledPveDiagnosticBrains.Count == 0)
		{
			return false;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		operation.ProfiledPveAiDiagnosticAwaitingBrains = false;
		operation.ProfiledPveAiDiagnosticStartedAt = realtimeSinceStartup;
		operation.ProfiledPveAiDiagnosticNextProbeAt = realtimeSinceStartup;
		operation.ProfiledPveAiDiagnosticSnapshotIndex = 0;
		operation.ProfiledPveInitialBrainPositions.Clear();
		foreach (BrainAI profiledPveDiagnosticBrain in operation.ProfiledPveDiagnosticBrains)
		{
			if ((Object)(object)profiledPveDiagnosticBrain != (Object)null)
			{
				operation.ProfiledPveInitialBrainPositions[((Object)profiledPveDiagnosticBrain).GetInstanceID()] = GetProfiledPveNavigationPosition(profiledPveDiagnosticBrain);
			}
		}
		GameObject val = (((Object)(object)gameManager == (Object)null) ? null : GameManager.myPlayer);
		if ((Object)(object)val != (Object)null)
		{
			operation.ProfiledPveInitialPlayerPositionCaptured = true;
			operation.ProfiledPveInitialPlayerPosition = val.transform.position;
		}
		LogProfiledPveNativeAiContract(operation, source);
		operation.ProfiledPveNativeContractLogged = true;
		return true;
	}

	private void RefreshProfiledPveDiagnosticBrains(ActiveMapOperation operation, GameManager gameManager)
	{
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		if (operation == null || ((gameManager != null) ? gameManager.allAI : null) == null)
		{
			return;
		}
		HashSet<int> hashSet = new HashSet<int>();
		foreach (BrainAI profiledPveDiagnosticBrain in operation.ProfiledPveDiagnosticBrains)
		{
			if ((Object)(object)profiledPveDiagnosticBrain != (Object)null)
			{
				hashSet.Add(((Object)profiledPveDiagnosticBrain).GetInstanceID());
			}
		}
		for (int i = 0; i < gameManager.allAI.Count; i++)
		{
			BrainAI val = gameManager.allAI[i];
			if (!((Object)(object)val == (Object)null))
			{
				int instanceID = ((Object)val).GetInstanceID();
				if (!operation.ProfiledPvePreexistingBrainIds.Contains(instanceID) && hashSet.Add(instanceID))
				{
					operation.ProfiledPveDiagnosticBrains.Add(val);
					int spawnOrdinal = operation.ProfiledPveDiagnosticBrains.Count - 1;
					TryApplyProfiledPveInitialWanderDelayCap(operation, val, spawnOrdinal);
					operation.ProfiledPveInitialBrainPositions[instanceID] = GetProfiledPveNavigationPosition(val);
				}
			}
		}
	}

	private void TryApplyProfiledPveInitialWanderDelayCap(ActiveMapOperation operation, BrainAI brain, int spawnOrdinal)
	{
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Invalid comparison between Unknown and I4
		float? obj;
		if (operation == null)
		{
			obj = null;
		}
		else
		{
			ModdedOperationDefinition operation2 = operation.Operation;
			if (operation2 == null)
			{
				obj = null;
			}
			else
			{
				ModdedPveAiProfileDefinition pveAiProfile = operation2.PveAiProfile;
				obj = ((pveAiProfile != null) ? pveAiProfile.InitialWanderDelayMaxSeconds : ((float?)null));
			}
		}
		float? num = obj;
		if (!num.HasValue || (Object)(object)brain == (Object)null || !NetworkServer.active)
		{
			return;
		}
		int instanceID = ((Object)brain).GetInstanceID();
		if (!operation.ProfiledPveInitialWanderDelayHandledBrainIds.Add(instanceID))
		{
			return;
		}
		try
		{
			float value = num.Value;
			float num2 = (float)brain.WanderTimer * brain.Patience;
			if (!float.IsFinite(value) || value <= 0f || !float.IsFinite(num2) || num2 <= 0f || !float.IsFinite(brain.wanderTime) || (int)brain.idleStates != 1 || brain.responding)
			{
				operation.ProfiledPveInitialWanderDelaySkipped++;
				return;
			}
			float num3 = Mathf.Max(0f, brain.wanderTime);
			float num4 = Mathf.Max(0f, num2 - num3);
			float num5 = ChooseProfiledPveInitialWanderRemainingDelay(operation, spawnOrdinal, value);
			if (num4 <= num5)
			{
				operation.ProfiledPveInitialWanderDelayPreserved++;
				return;
			}
			brain.wanderTime = Mathf.Max(num3, num2 - num5);
			operation.ProfiledPveInitialWanderDelayAdvanced++;
		}
		catch (Exception ex)
		{
			operation.ProfiledPveInitialWanderDelaySkipped++;
			log.LogWarning((object)("Profiled PVE initial wander-delay cap skipped one new native BrainAI: operation=" + operation.Operation.Id + ", brain=" + instanceID + ", " + ex.GetType().Name + ": " + ex.Message));
		}
	}

	private static float ChooseProfiledPveInitialWanderRemainingDelay(ActiveMapOperation operation, int spawnOrdinal, float capSeconds)
	{
		float num = (float)(ComputeStableFnv1a(operation.Operation.Id + "|" + operation.TimeCode + "|" + operation.SceneHandle + "|initial-wander|" + spawnOrdinal) % 10001) / 10000f;
		return capSeconds * (0.5f + 0.5f * num);
	}

	private static Vector3 GetProfiledPveNavigationPosition(BrainAI brain)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)brain == (Object)null)
		{
			return Vector3.zero;
		}
		try
		{
			AgentController agent = brain.agent;
			if ((Object)(object)agent != (Object)null && agent.entityExists)
			{
				return agent.position;
			}
		}
		catch
		{
		}
		return ((Component)brain).transform.position;
	}

	private void LogProfiledPveNativeAiContract(ActiveMapOperation operation, string source)
	{
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Invalid comparison between Unknown and I4
		if (operation == null)
		{
			return;
		}
		int num = 0;
		float num2 = float.MaxValue;
		float num3 = float.MinValue;
		float num4 = float.MaxValue;
		float num5 = float.MinValue;
		float num6 = float.MaxValue;
		float num7 = float.MinValue;
		int val = int.MaxValue;
		int val2 = int.MinValue;
		float num8 = float.MaxValue;
		float num9 = float.MinValue;
		int num10 = 0;
		int num11 = 0;
		foreach (BrainAI profiledPveDiagnosticBrain in operation.ProfiledPveDiagnosticBrains)
		{
			if ((Object)(object)profiledPveDiagnosticBrain == (Object)null)
			{
				continue;
			}
			try
			{
				float num12 = (float)profiledPveDiagnosticBrain.WanderTimer * profiledPveDiagnosticBrain.Patience;
				num2 = Mathf.Min(num2, num12);
				num3 = Mathf.Max(num3, num12);
				float num13 = Mathf.Max(0f, num12 - profiledPveDiagnosticBrain.wanderTime);
				num4 = Mathf.Min(num4, num13);
				num5 = Mathf.Max(num5, num13);
				num6 = Mathf.Min(num6, profiledPveDiagnosticBrain.DetectionRange);
				num7 = Mathf.Max(num7, profiledPveDiagnosticBrain.DetectionRange);
				val = Math.Min(val, profiledPveDiagnosticBrain.WanderDistance);
				val2 = Math.Max(val2, profiledPveDiagnosticBrain.WanderDistance);
				num8 = Mathf.Min(num8, profiledPveDiagnosticBrain.EyesFOVAngle);
				num9 = Mathf.Max(num9, profiledPveDiagnosticBrain.EyesFOVAngle);
				if ((int)profiledPveDiagnosticBrain.idleStates == 1)
				{
					num10++;
				}
				if (profiledPveDiagnosticBrain.useComms)
				{
					num11++;
				}
				num++;
			}
			catch
			{
			}
		}
		if (num == 0)
		{
			log.LogWarning((object)("Profiled PVE AI diagnostic found no new BrainAI instances for operation=" + operation.Operation.Id + " at " + source + "; the bounded snapshots will retry."));
			return;
		}
		string text = "Profiled PVE native AI contract: operation=" + operation.Operation.Id + ", profile=" + operation.Operation.PveAiProfile.Id + ", source=" + source + ", brains=" + num + ", nativeInitialWanderDelay=" + num2.ToString("F2", CultureInfo.InvariantCulture) + ".." + num3.ToString("F2", CultureInfo.InvariantCulture) + "s, detection=" + num6.ToString("F1", CultureInfo.InvariantCulture) + ".." + num7.ToString("F1", CultureInfo.InvariantCulture) + "m, fov=" + num8.ToString("F1", CultureInfo.InvariantCulture) + ".." + num9.ToString("F1", CultureInfo.InvariantCulture) + ", wander=" + val + ".." + val2 + "m, idleWander=" + num10 + "/" + num + ", comms=" + num11 + "/" + num + ".";
		if (num2 <= 0f)
		{
			log.LogWarning((object)(text + " At least one native prefab has no initial wander delay."));
		}
		else
		{
			log.LogInfo((object)text);
		}
		float? initialWanderDelayMaxSeconds = operation.Operation.PveAiProfile.InitialWanderDelayMaxSeconds;
		if (initialWanderDelayMaxSeconds.HasValue)
		{
			log.LogInfo((object)("Profiled PVE initial wander-delay cap: operation=" + operation.Operation.Id + ", profile=" + operation.Operation.PveAiProfile.Id + ", cap=" + initialWanderDelayMaxSeconds.Value.ToString("F2", CultureInfo.InvariantCulture) + "s, handled=" + operation.ProfiledPveInitialWanderDelayHandledBrainIds.Count + "/" + num + ", advanced=" + operation.ProfiledPveInitialWanderDelayAdvanced + ", preserved=" + operation.ProfiledPveInitialWanderDelayPreserved + ", skipped=" + operation.ProfiledPveInitialWanderDelaySkipped + ", observedRemaining=" + num4.ToString("F2", CultureInfo.InvariantCulture) + ".." + num5.ToString("F2", CultureInfo.InvariantCulture) + "s, nativeTimerFieldsUnchanged=True, subsequentCyclesNative=True."));
		}
	}

	private void ProcessProfiledPveAiDiagnostics(ActiveMapOperation operation)
	{
		if (!IsProfiledPveDiagnosticOperation(operation) || operation.ProfiledPveAiDiagnosticComplete || operation.ProfiledPveAiDiagnosticSnapshotIndex >= ProfiledPveAiDiagnosticSnapshotSeconds.Length)
		{
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (realtimeSinceStartup < operation.ProfiledPveAiDiagnosticNextProbeAt)
		{
			return;
		}
		if (operation.ProfiledPveAiDiagnosticAwaitingBrains)
		{
			if (!TryBeginProfiledPveAiDiagnostics(operation, GameManager.instance, "native-network-spawn"))
			{
				operation.ProfiledPveAiDiagnosticNextProbeAt = realtimeSinceStartup + 0.25f;
				return;
			}
			realtimeSinceStartup = Time.realtimeSinceStartup;
		}
		if (operation.ProfiledPveAiDiagnosticStartedAt < 0f)
		{
			return;
		}
		float num = realtimeSinceStartup - operation.ProfiledPveAiDiagnosticStartedAt;
		float num2 = ProfiledPveAiDiagnosticSnapshotSeconds[operation.ProfiledPveAiDiagnosticSnapshotIndex];
		if (num + 0.05f < num2)
		{
			operation.ProfiledPveAiDiagnosticNextProbeAt = operation.ProfiledPveAiDiagnosticStartedAt + num2;
			return;
		}
		GameManager gameManager = GameManager.instance;
		RefreshProfiledPveDiagnosticBrains(operation, gameManager);
		LogProfiledPveAiSnapshot(operation, gameManager, num2, num);
		operation.ProfiledPveAiDiagnosticSnapshotIndex++;
		if (operation.ProfiledPveAiDiagnosticSnapshotIndex >= ProfiledPveAiDiagnosticSnapshotSeconds.Length)
		{
			operation.ProfiledPveAiDiagnosticComplete = true;
			log.LogInfo((object)("Profiled PVE AI diagnostic completed its bounded 120-second read-only acceptance window for operation=" + operation.Operation.Id + "."));
		}
		else
		{
			operation.ProfiledPveAiDiagnosticNextProbeAt = operation.ProfiledPveAiDiagnosticStartedAt + ProfiledPveAiDiagnosticSnapshotSeconds[operation.ProfiledPveAiDiagnosticSnapshotIndex];
		}
	}

	private void LogProfiledPveAiSnapshot(ActiveMapOperation operation, GameManager gameManager, float scheduled, float elapsed)
	{
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_04bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0360: Unknown result type (might be due to invalid IL or missing references)
		//IL_0365: Unknown result type (might be due to invalid IL or missing references)
		//IL_038c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0391: Unknown result type (might be due to invalid IL or missing references)
		//IL_0395: Unknown result type (might be due to invalid IL or missing references)
		//IL_0397: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		int num9 = 0;
		int num10 = 0;
		int num11 = 0;
		int num12 = 0;
		int num13 = 0;
		int num14 = 0;
		int num15 = 0;
		int num16 = 0;
		int num17 = 0;
		int num18 = 0;
		int num19 = 0;
		int num20 = 0;
		int num21 = 0;
		int num22 = 0;
		int num23 = 0;
		int num24 = 0;
		int num25 = 0;
		int num26 = 0;
		int num27 = 0;
		float num28 = float.MaxValue;
		float num29 = float.MinValue;
		float num30 = float.MaxValue;
		float num31 = float.MinValue;
		float num32 = 0f;
		float num33 = 0f;
		float num34 = 0f;
		float num35 = 0f;
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.Ordinal);
		Dictionary<int, int> dictionary2 = new Dictionary<int, int>();
		GameObject val = (((Object)(object)gameManager == (Object)null) ? null : GameManager.myPlayer);
		Vector3 val2 = (((Object)(object)val == (Object)null) ? Vector3.zero : (val.transform.position + Vector3.up * 1.35f));
		Vector2 val3 = default(Vector2);
		Vector2 val4 = default(Vector2);
		Vector2 val5 = default(Vector2);
		Vector2 val6 = default(Vector2);
		RaycastHit val7 = default(RaycastHit);
		foreach (BrainAI profiledPveDiagnosticBrain in operation.ProfiledPveDiagnosticBrains)
		{
			if ((Object)(object)profiledPveDiagnosticBrain == (Object)null)
			{
				continue;
			}
			try
			{
				int instanceID = ((Object)profiledPveDiagnosticBrain).GetInstanceID();
				if (!operation.ProfiledPveInitialBrainPositions.TryGetValue(instanceID, out var value))
				{
					value = GetProfiledPveNavigationPosition(profiledPveDiagnosticBrain);
					operation.ProfiledPveInitialBrainPositions[instanceID] = value;
				}
				Vector3 profiledPveNavigationPosition = GetProfiledPveNavigationPosition(profiledPveDiagnosticBrain);
				((Vector2)(ref val3))._002Ector(profiledPveNavigationPosition.x - value.x, profiledPveNavigationPosition.z - value.z);
				float magnitude = ((Vector2)(ref val3)).magnitude;
				num34 += magnitude;
				num35 = Mathf.Max(num35, magnitude);
				if (magnitude >= 1f)
				{
					num2++;
				}
				if (operation.ProfiledPveInitialPlayerPositionCaptured)
				{
					((Vector2)(ref val4))._002Ector(value.x - operation.ProfiledPveInitialPlayerPosition.x, value.z - operation.ProfiledPveInitialPlayerPosition.z);
					((Vector2)(ref val5))._002Ector(profiledPveNavigationPosition.x - operation.ProfiledPveInitialPlayerPosition.x, profiledPveNavigationPosition.z - operation.ProfiledPveInitialPlayerPosition.z);
					if (((Vector2)(ref val4)).magnitude - ((Vector2)(ref val5)).magnitude >= 5f)
					{
						num3++;
					}
				}
				if ((Object)(object)profiledPveDiagnosticBrain.CurrentSeenTarget != (Object)null)
				{
					num4++;
				}
				string key = ((object)profiledPveDiagnosticBrain.CurrentState/*cast due to constrained. prefix*/).ToString();
				dictionary[key] = ((!dictionary.TryGetValue(key, out var value2)) ? 1 : (value2 + 1));
				if (((Behaviour)profiledPveDiagnosticBrain).enabled)
				{
					num8++;
				}
				if (profiledPveDiagnosticBrain.responding)
				{
					num26++;
				}
				if (profiledPveDiagnosticBrain._currentCover != null)
				{
					num27++;
				}
				num28 = Mathf.Min(num28, profiledPveDiagnosticBrain.wanderTime);
				num29 = Mathf.Max(num29, profiledPveDiagnosticBrain.wanderTime);
				int movementType = profiledPveDiagnosticBrain.movementType;
				dictionary2[movementType] = ((!dictionary2.TryGetValue(movementType, out var value3)) ? 1 : (value3 + 1));
				AgentController agent = profiledPveDiagnosticBrain.agent;
				if ((Object)(object)agent != (Object)null)
				{
					num9++;
					if (((Behaviour)agent).enabled)
					{
						num10++;
					}
					if (((Behaviour)agent).isActiveAndEnabled)
					{
						num11++;
					}
					if (agent.entityExists)
					{
						num12++;
						if (agent.updatePosition)
						{
							num13++;
						}
						if (agent.pathPending)
						{
							num14++;
						}
						if (agent.hasPath)
						{
							num15++;
						}
						float maxSpeed = agent.maxSpeed;
						num30 = Mathf.Min(num30, maxSpeed);
						num31 = Mathf.Max(num31, maxSpeed);
						Vector3 velocity = agent.velocity;
						float magnitude2 = ((Vector3)(ref velocity)).magnitude;
						num32 = Mathf.Max(num32, magnitude2);
						if (magnitude2 >= 0.01f)
						{
							num25++;
						}
						Vector3 position = agent.position;
						num33 = Mathf.Max(num33, Vector3.Distance(position, profiledPveNavigationPosition));
						Vector3 destination = agent.destination;
						((Vector2)(ref val6))._002Ector(destination.x - profiledPveNavigationPosition.x, destination.z - profiledPveNavigationPosition.z);
						if (((Vector2)(ref val6)).magnitude >= 1f)
						{
							num16++;
						}
					}
					FollowerEntity agent2 = agent.Agent;
					if ((Object)(object)agent2 != (Object)null)
					{
						num17++;
						if (((Behaviour)agent2).enabled)
						{
							num18++;
						}
						if (((Behaviour)agent2).isActiveAndEnabled)
						{
							num19++;
						}
						if (agent2.entityExists)
						{
							num20++;
						}
						if (agent2.canMove)
						{
							num21++;
						}
						if (agent2.canSearch)
						{
							num22++;
						}
						if (agent2.simulateMovement)
						{
							num23++;
						}
						if (agent2.isStopped)
						{
							num24++;
						}
					}
				}
				if ((Object)(object)val != (Object)null && (Object)(object)profiledPveDiagnosticBrain.eyesAI != (Object)null)
				{
					GameObject eyesTransform = profiledPveDiagnosticBrain.eyesAI.EyesTransform;
					if (Physics.Linecast(((Object)(object)eyesTransform == (Object)null) ? (profiledPveNavigationPosition + Vector3.up * 1.6f) : eyesTransform.transform.position, val2, ref val7, LayerMask.op_Implicit(profiledPveDiagnosticBrain.eyesAI.DetectionLayerMask), (QueryTriggerInteraction)2))
					{
						Transform val8 = (((Object)(object)((RaycastHit)(ref val7)).collider == (Object)null) ? null : ((Component)((RaycastHit)(ref val7)).collider).transform);
						if ((Object)(object)val8 != (Object)null && (Object)(object)val8.root == (Object)(object)val.transform.root)
						{
							num7++;
						}
						else if ((Object)(object)((RaycastHit)(ref val7)).collider != (Object)null && ((Component)((RaycastHit)(ref val7)).collider).gameObject.layer == 18)
						{
							num5++;
						}
						else
						{
							num6++;
						}
					}
					else
					{
						num7++;
					}
				}
				num++;
			}
			catch
			{
			}
		}
		string text = ((dictionary.Count == 0) ? "none" : string.Join(",", from pair in dictionary
			orderby pair.Key
			select pair.Key + "=" + pair.Value));
		float num36 = ((num == 0) ? 0f : (num34 / (float)num));
		log.LogInfo((object)("Profiled PVE AI snapshot: operation=" + operation.Operation.Id + ", profile=" + operation.Operation.PveAiProfile.Id + ", scheduled=" + scheduled.ToString("F0", CultureInfo.InvariantCulture) + "s, elapsed=" + elapsed.ToString("F2", CultureInfo.InvariantCulture) + "s, live=" + num + ", moved>=1m=" + num2 + ", movedTowardInsertion>=5m=" + num3 + ", movementMean=" + num36.ToString("F2", CultureInfo.InvariantCulture) + "m, movementMax=" + num35.ToString("F2", CultureInfo.InvariantCulture) + "m, actualSeenTarget=" + num4 + ", sameMaskSightProbe(vegetation=" + num5 + ",other=" + num6 + ",clearOrPlayer=" + num7 + "), states=" + text + "."));
		string text2 = ((dictionary2.Count == 0) ? "none" : string.Join(",", from pair in dictionary2
			orderby pair.Key
			select pair.Key + "=" + pair.Value));
		string text3 = ((num == 0) ? "none" : (num28.ToString("F2", CultureInfo.InvariantCulture) + ".." + num29.ToString("F2", CultureInfo.InvariantCulture) + "s"));
		string text4 = ((num12 == 0) ? "none" : (num30.ToString("F2", CultureInfo.InvariantCulture) + ".." + num31.ToString("F2", CultureInfo.InvariantCulture) + "mps"));
		log.LogInfo((object)("Profiled PVE native agent diagnostic: operation=" + operation.Operation.Id + ", scheduled=" + scheduled.ToString("F0", CultureInfo.InvariantCulture) + "s, brainEnabled=" + num8 + "/" + num + ", controllerAssigned=" + num9 + "/" + num + ", controllerEnabled=" + num10 + "/" + num + ", controllerActive=" + num11 + "/" + num + ", entityExists=" + num12 + "/" + num + ", updatePosition=" + num13 + "/" + num + ", pathPending=" + num14 + ", hasPath=" + num15 + ", destination>=1m=" + num16 + ", followerAssigned=" + num17 + "/" + num + ", followerEnabled=" + num18 + "/" + num + ", followerActive=" + num19 + "/" + num + ", followerEntityExists=" + num20 + "/" + num + ", canMove=" + num21 + "/" + num + ", canSearch=" + num22 + "/" + num + ", simulateMovement=" + num23 + "/" + num + ", isStopped=" + num24 + "/" + num + ", maxSpeed=" + text4 + ", velocity>=0.01mps=" + num25 + ", velocityMax=" + num32.ToString("F2", CultureInfo.InvariantCulture) + "mps, controllerPositionOffsetMax=" + num33.ToString("F2", CultureInfo.InvariantCulture) + "m, responding=" + num26 + ", currentCover=" + num27 + ", wanderClock=" + text3 + ", movementTypes=" + text2 + "."));
	}

	private static int ChooseStandalonePveEnemyCount(ActiveMapOperation operation)
	{
		int minimumEnemies = operation.Operation.MinimumEnemies;
		int maximumEnemies = operation.Operation.MaximumEnemies;
		if (maximumEnemies <= minimumEnemies)
		{
			return minimumEnemies;
		}
		uint num = ComputeStableFnv1a(operation.Operation.Id + "|" + operation.TimeCode + "|" + operation.SceneHandle);
		return minimumEnemies + (int)(num % (uint)(maximumEnemies - minimumEnemies + 1));
	}

	private static uint ComputeStableFnv1a(string value)
	{
		uint num = 2166136261u;
		for (int i = 0; i < value.Length; i++)
		{
			num ^= value[i];
			num *= 16777619;
		}
		return num;
	}

	private static void ConfigureStandaloneBotDetails(BotSpawnDetails details, ModdedPveAiProfileDefinition profile)
	{
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)details == (Object)null))
		{
			details.DetectionTimeMultiplier = ((profile == null) ? 1.15f : 1f);
			details.HearingRange = ((profile == null) ? 52f : 20f);
			details.DetectionRange = ((profile != null) ? profile.DetectionRangeMeters : 72f);
			details.FOV = ((profile != null) ? profile.FieldOfViewDegrees : 105f);
			details.maxEffectiveRange = ((profile != null) ? profile.MaximumEffectiveRangeMeters : 90f);
			details.useComms = profile == null || profile.UseComms;
			details.DoesCounterSuppression = profile == null || profile.CounterSuppression;
			if (profile != null)
			{
				details.idleState = (IdleStates)1;
			}
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
		return profile.Id + "(range=" + profile.DetectionRangeMeters.ToString("F1", CultureInfo.InvariantCulture) + "m,fov=" + profile.FieldOfViewDegrees.ToString("F1", CultureInfo.InvariantCulture) + ",maxEffective=" + profile.MaximumEffectiveRangeMeters.ToString("F1", CultureInfo.InvariantCulture) + "m,wander=" + profile.WanderDistanceMeters + "m,initialWanderDelayMax=" + (profile.InitialWanderDelayMaxSeconds.HasValue ? (profile.InitialWanderDelayMaxSeconds.Value.ToString("F1", CultureInfo.InvariantCulture) + "s") : "native") + ",comms=" + profile.UseComms + ",counterSuppression=" + profile.CounterSuppression + ")";
	}

	private static List<Transform> FindSceneMarkers(Scene scene, string prefix)
	{
		List<Transform> list = new List<Transform>();
		foreach (GameObject item in (Il2CppArrayBase<GameObject>)(object)((Scene)(ref scene)).GetRootGameObjects())
		{
			foreach (Transform componentsInChild in item.GetComponentsInChildren<Transform>(true))
			{
				if ((((Object)componentsInChild).name ?? string.Empty).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					list.Add(componentsInChild);
				}
			}
		}
		list.Sort((Transform left, Transform right) => string.CompareOrdinal(((Object)left).name, ((Object)right).name));
		return list;
	}

	private void SpawnAndPositionStandalonePlayers(ActiveMapOperation operation, bool allowSpawnRequest)
	{
		//IL_03d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03da: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_053f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0543: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		if (operation == null || operation.SceneHandle == 0)
		{
			return;
		}
		Scene scene = FindLoadedSceneByHandle(operation.SceneHandle);
		if (!((Scene)(ref scene)).IsValid() || !((Scene)(ref scene)).isLoaded)
		{
			return;
		}
		List<Transform> list = FindStandalonePlayerMarkers(scene, operation.Operation.Mode);
		if (list.Count == 0)
		{
			log.LogError((object)("Standalone package scene has no compatible player spawn markers for spawnSet=" + operation.Operation.SpawnSetId + "."));
			return;
		}
		PlayerMaster[] array;
		try
		{
			array = Il2CppArrayBase<PlayerMaster>.op_Implicit(Resources.FindObjectsOfTypeAll<PlayerMaster>());
		}
		catch
		{
			return;
		}
		PlayerMaster[] array2 = array;
		foreach (PlayerMaster val in array2)
		{
			if ((Object)(object)val == (Object)null)
			{
				continue;
			}
			Scene scene2 = ((Component)val).gameObject.scene;
			if (!((Scene)(ref scene2)).IsValid())
			{
				continue;
			}
			int instanceID = ((Object)val).GetInstanceID();
			Transform val2 = SelectPlayerMarker(operation, val, list);
			if ((Object)(object)val2 == (Object)null)
			{
				continue;
			}
			try
			{
				val.LastSpawnPoint = val2;
				val.spawnRotation = val2.eulerAngles;
			}
			catch
			{
			}
			PlayerNetworking val3 = null;
			try
			{
				val3 = val.PlayerSpawnedObject;
			}
			catch
			{
			}
			if ((Object)(object)val3 != (Object)null)
			{
				operation.CompletedPlayerSpawnIds.Add(instanceID);
			}
			bool flag = false;
			try
			{
				flag = val.currentlySpawnedAndAlive;
			}
			catch
			{
			}
			if ((Object)(object)val3 == (Object)null && flag)
			{
				operation.CompletedPlayerSpawnIds.Add(instanceID);
			}
			else
			{
				if ((Object)(object)val3 == (Object)null && operation.CompletedPlayerSpawnIds.Contains(instanceID))
				{
					continue;
				}
				if ((Object)(object)val3 == (Object)null && allowSpawnRequest)
				{
					bool flag2 = false;
					try
					{
						flag2 = ((NetworkBehaviour)val).isOwned;
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
							string text = RequestStandalonePlayerSpawn(val, flag2, num);
							bool flag3 = false;
							try
							{
								flag3 = (Object)(object)val.PlayerSpawnedObject != (Object)null;
							}
							catch
							{
							}
							if (flag3)
							{
								operation.CompletedPlayerSpawnIds.Add(instanceID);
							}
							log.LogInfo((object)("Standalone requested the shipped player spawn pipeline: playerMaster=" + instanceID + ", owned=" + flag2 + ", serverActive=" + NetworkServer.active + ", route=" + text + ", attempt=" + (num + 1) + "/" + num2 + ", producedPlayerObject=" + flag3 + "."));
						}
					}
					catch (Exception ex)
					{
						Exception ex2 = ((ex is TargetInvocationException && ex.InnerException != null) ? ex.InnerException : ex);
						log.LogWarning((object)("Standalone player spawn request is waiting for " + instanceID + ": " + ex2.GetType().Name + ": " + ex2.Message));
					}
					continue;
				}
				int num3 = ((!((Object)(object)val3 == (Object)null)) ? ((Object)val3).GetInstanceID() : 0);
				if ((Object)(object)val3 == (Object)null || (operation.PositionedPlayerObjects.TryGetValue(instanceID, out var value3) && value3 == num3))
				{
					continue;
				}
				bool flag4 = false;
				try
				{
					flag4 = ((NetworkBehaviour)val3).isOwned || ((NetworkBehaviour)val3).isLocalPlayer || ((NetworkBehaviour)val).isOwned;
				}
				catch
				{
				}
				Vector3 val4 = val2.position + Vector3.up * 0.25f;
				int value4;
				if (IsPlayerAtPackageSpawn(val3, val4, flag4, out var state))
				{
					operation.PositionedPlayerObjects[instanceID] = num3;
					log.LogInfo((object)("Standalone player reached package marker through the shipped movement contract: marker=" + ((Object)val2).name + ", playerMaster=" + instanceID + ", owned=" + flag4 + ", state=" + state + "."));
				}
				else if (!operation.PlayerMoveRequestFrames.TryGetValue(instanceID, out value4) || Time.frameCount >= value4 + 300)
				{
					if (flag4 && (Object)(object)GameManager.instance != (Object)null)
					{
						((MonoBehaviour)GameManager.instance).StartCoroutine(GameManager.instance.MovePlayerToSpawn(val4, val2.rotation));
						operation.PlayerMoveRequestFrames[instanceID] = Time.frameCount;
						log.LogInfo((object)("Standalone invoked shipped GameManager.MovePlayerToSpawn for owned player: marker=" + ((Object)val2).name + ", playerMaster=" + instanceID + ", priorState=" + state + "."));
					}
					else
					{
						MoveRemotePlayerRoot(((Component)val3).gameObject, val4, val2.rotation);
						operation.PlayerMoveRequestFrames[instanceID] = Time.frameCount;
						log.LogInfo((object)("Standalone moved server-owned remote player root to package marker=" + ((Object)val2).name + ", playerMaster=" + instanceID + "."));
					}
				}
			}
		}
	}

	private void MaintainOwnedStandaloneWeaponAuthority(ActiveMapOperation operation)
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		if (operation == null || !NetworkClient.active)
		{
			return;
		}
		PlayerNetworking[] array;
		try
		{
			array = Il2CppArrayBase<PlayerNetworking>.op_Implicit(Resources.FindObjectsOfTypeAll<PlayerNetworking>());
		}
		catch
		{
			return;
		}
		PlayerNetworking[] array2 = array;
		foreach (PlayerNetworking val in array2)
		{
			if ((Object)(object)val == (Object)null || (Object)(object)((Component)val).gameObject == (Object)null)
			{
				continue;
			}
			Scene scene = ((Component)val).gameObject.scene;
			if (((Scene)(ref scene)).IsValid())
			{
				bool isOwned;
				try
				{
					isOwned = ((NetworkBehaviour)val).isOwned;
				}
				catch
				{
					continue;
				}
				if (isOwned)
				{
					MaintainOwnedStandaloneWeaponSlot(operation, val, 0, "primary", val.PrimaryWeaponID, (Component)(object)val.primaryWeapon);
					MaintainOwnedStandaloneWeaponSlot(operation, val, 1, "second-primary", val.SecondPrimaryWeaponID, (Component)(object)val.secondPrimaryWeapon);
					MaintainOwnedStandaloneWeaponSlot(operation, val, 2, "secondary", val.SecondaryWeaponID, (Component)(object)val.secondaryWeapon);
					MaintainOwnedStandaloneWeaponSlot(operation, val, 3, "special-purpose", val.SpecialPurposeWeaponID, (Component)(object)val.specialPurposeWeapon);
					MaintainOwnedStandaloneWeaponSlot(operation, val, 4, "grenade", val.CurrentGrenadeWeaponID, (Component)(object)(((Object)(object)val.GrenadeV2Weapon == (Object)null) ? null : val.GrenadeV2Weapon.GetComponent<GrenadeV2>()));
				}
			}
		}
	}

	private void MaintainOwnedStandaloneWeaponSlot(ActiveMapOperation operation, PlayerNetworking player, int correctiveSlot, string slotName, int syncedWeaponNetId, Component weapon)
	{
		if ((Object)(object)weapon == (Object)null || syncedWeaponNetId == 0)
		{
			return;
		}
		int instanceID = ((Object)weapon).GetInstanceID();
		NetworkIdentity val = null;
		try
		{
			val = weapon.GetComponent<NetworkIdentity>();
		}
		catch
		{
		}
		if ((Object)(object)val == (Object)null || val.netId == 0)
		{
			return;
		}
		bool isOwned;
		try
		{
			isOwned = val.isOwned;
		}
		catch
		{
			return;
		}
		if (isOwned)
		{
			if (operation.ConfirmedWeaponAuthorityIds.Add(instanceID))
			{
				log.LogInfo((object)("Standalone confirmed vanilla weapon authority: player=" + ((Object)player).GetInstanceID() + ", slot=" + slotName + ", netId=" + val.netId + ", weapon=" + ((Object)weapon).name + "."));
			}
			return;
		}
		int value;
		int num = (operation.WeaponAuthorityRequestCounts.TryGetValue(instanceID, out value) ? value : 0);
		if (num >= 3 || (operation.WeaponAuthorityRequestFrames.TryGetValue(instanceID, out var value2) && Time.frameCount < value2 + 120))
		{
			return;
		}
		operation.WeaponAuthorityRequestFrames[instanceID] = Time.frameCount;
		operation.WeaponAuthorityRequestCounts[instanceID] = num + 1;
		try
		{
			player.CMD_SetCorrectiveOwnershipOfWeapon(correctiveSlot);
			log.LogInfo((object)("Standalone requested OPERATOR's vanilla corrective weapon authority: player=" + ((Object)player).GetInstanceID() + ", slot=" + slotName + ", syncedNetId=" + syncedWeaponNetId + ", identityNetId=" + val.netId + ", weapon=" + ((Object)weapon).name + ", attempt=" + (num + 1) + "/3."));
		}
		catch (Exception ex)
		{
			log.LogWarning((object)("Standalone vanilla corrective weapon authority is waiting: player=" + ((Object)player).GetInstanceID() + ", slot=" + slotName + ", weapon=" + ((Object)weapon).name + ", " + ex.GetType().Name + ": " + ex.Message));
		}
	}

	private string RequestStandalonePlayerSpawn(PlayerMaster player, bool owned, int priorRequestCount)
	{
		if ((Object)(object)player == (Object)null)
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
		NetworkIdentity component = ((Component)player).GetComponent<NetworkIdentity>();
		if ((Object)(object)component == (Object)null)
		{
			throw new InvalidOperationException("PlayerMaster has no NetworkIdentity for the shipped server spawn body");
		}
		directServerPlayerSpawnMethod.Invoke(player, new object[1] { component });
	}

	private static Scene FindLoadedSceneByHandle(int handle)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			Scene sceneAt = SceneManager.GetSceneAt(i);
			if (((Scene)(ref sceneAt)).handle == SceneHandle.op_Implicit(handle))
			{
				return sceneAt;
			}
		}
		return default(Scene);
	}

	private static List<Transform> FindStandalonePlayerMarkers(Scene scene, ModdedOperationMode mode)
	{
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Invalid comparison between Unknown and I4
		List<Transform> list = new List<Transform>();
		List<Transform> list2 = new List<Transform>();
		foreach (GameObject item in (Il2CppArrayBase<GameObject>)(object)((Scene)(ref scene)).GetRootGameObjects())
		{
			foreach (Transform componentsInChild in item.GetComponentsInChildren<Transform>(true))
			{
				string text = ((Object)componentsInChild).name ?? string.Empty;
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
		list.Sort((Transform left, Transform right) => string.CompareOrdinal(((Object)left).name, ((Object)right).name));
		list2.Sort((Transform left, Transform right) => string.CompareOrdinal(((Object)left).name, ((Object)right).name));
		if ((int)mode == 2)
		{
			return list;
		}
		list.AddRange(list2);
		return list;
	}

	private static Transform SelectPlayerMarker(ActiveMapOperation operation, PlayerMaster player, List<Transform> markers)
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
		int instanceID = ((Object)player).GetInstanceID();
		int pvpTeamId = 0;
		if ((int)operation.Operation.Mode == 1)
		{
			try
			{
				TeamIdentifier myTeamIdentifier = player.MyTeamIdentifier;
				pvpTeamId = ((myTeamIdentifier != null) ? myTeamIdentifier.TeamID : 0);
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
			Transform val = markers.FirstOrDefault((Transform marker) => string.Equals(((Object)marker).name, assignedName, StringComparison.Ordinal));
			if ((Object)(object)val != (Object)null && ((int)operation.Operation.Mode != 1 || PvpMarkerMatchesTeam(val, pvpTeamId)))
			{
				return val;
			}
			operation.PlayerMarkerNames.Remove(instanceID);
		}
		Transform val2;
		if ((int)operation.Operation.Mode == 1)
		{
			List<Transform> list = markers.Where((Transform marker) => PvpMarkerMatchesTeam(marker, pvpTeamId)).ToList();
			if (list.Count > 0)
			{
				val2 = list[operation.SpawnCursor++ % list.Count];
				operation.PlayerMarkerNames[instanceID] = ((Object)val2).name;
				return val2;
			}
		}
		val2 = markers[operation.SpawnCursor++ % markers.Count];
		operation.PlayerMarkerNames[instanceID] = ((Object)val2).name;
		return val2;
	}

	private static bool PvpMarkerMatchesTeam(Transform marker, int teamId)
	{
		string text = ((marker != null) ? ((Object)marker).name : null) ?? string.Empty;
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
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)player == (Object)null)
		{
			state = "player=null";
			return false;
		}
		float num = Vector3.Distance(((Component)player).transform.position, target);
		if (!owned)
		{
			state = "network=" + num.ToString("F2");
			return num <= 3f;
		}
		PlayerNetworking val = null;
		GameObject val2 = null;
		FirstPersonController val3 = null;
		try
		{
			val = GameManager.myPlayerNetworking;
			val2 = GameManager.myPlayer;
			val3 = GameManager.myPlayerController;
		}
		catch
		{
		}
		float num2 = (((Object)(object)val2 == (Object)null) ? float.MaxValue : Vector3.Distance(val2.transform.position, target));
		float num3 = (((Object)(object)val3 == (Object)null) ? float.MaxValue : Vector3.Distance(((Component)val3).transform.position, target));
		GameObject val4 = null;
		try
		{
			val4 = player.Camera;
		}
		catch
		{
		}
		float num4 = (((Object)(object)val4 == (Object)null) ? float.MaxValue : Vector3.Distance(val4.transform.position, target));
		bool flag = (Object)(object)val == (Object)null || (Object)(object)val == (Object)(object)player;
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
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)player == (Object)null)
		{
			return;
		}
		CharacterController val = player.GetComponent<CharacterController>() ?? player.GetComponentInChildren<CharacterController>(true);
		bool flag = (Object)(object)val != (Object)null && ((Collider)val).enabled;
		if (flag)
		{
			((Collider)val).enabled = false;
		}
		player.transform.SetPositionAndRotation(target, rotation);
		foreach (Rigidbody componentsInChild in player.GetComponentsInChildren<Rigidbody>(true))
		{
			componentsInChild.linearVelocity = Vector3.zero;
			componentsInChild.angularVelocity = Vector3.zero;
		}
		if (flag)
		{
			((Collider)val).enabled = true;
		}
		Physics.SyncTransforms();
	}

	private static void ReplaceNativeMapPreview(Transform parent, Sprite previewSprite, string name)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)parent == (Object)null) && !((Object)(object)previewSprite == (Object)null))
		{
			SetGameObjectsActive(CaptureDirectChildren(parent), active: false);
			GameObject val = new GameObject(name);
			val.transform.SetParent(parent, false);
			SetFullStretch(val.AddComponent<RectTransform>());
			Image obj = val.AddComponent<Image>();
			obj.sprite = previewSprite;
			((Graphic)obj).color = Color.white;
			obj.preserveAspect = true;
			((Graphic)obj).raycastTarget = false;
		}
	}

	private void RebindNativeFullscreenControls(OperationBoardUI board, GameObject preparationPanel)
	{
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Expected O, but got Unknown
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Expected O, but got Unknown
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Expected O, but got Unknown
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Expected O, but got Unknown
		if ((Object)(object)board == (Object)null || (Object)(object)preparationPanel == (Object)null)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		foreach (PanelButton componentsInChild in preparationPanel.GetComponentsInChildren<PanelButton>(true))
		{
			int fullscreenEventDirection = GetFullscreenEventDirection((UnityEventBase)(object)(((Object)(object)componentsInChild == (Object)null) ? null : componentsInChild.onClick), ((Object)(object)componentsInChild == (Object)null) ? null : ((Component)componentsInChild).gameObject, board);
			if (fullscreenEventDirection != 0)
			{
				componentsInChild.onClick = new UnityEvent();
				bool fullscreen = fullscreenEventDirection > 0;
				componentsInChild.onClick.AddListener(UnityAction.op_Implicit((Action)delegate
				{
					SetNativeMapFullscreen(board, fullscreen);
				}));
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
		foreach (ButtonManager componentsInChild2 in preparationPanel.GetComponentsInChildren<ButtonManager>(true))
		{
			int fullscreenEventDirection2 = GetFullscreenEventDirection((UnityEventBase)(object)(((Object)(object)componentsInChild2 == (Object)null) ? null : componentsInChild2.onClick), ((Object)(object)componentsInChild2 == (Object)null) ? null : ((Component)componentsInChild2).gameObject, board);
			if (fullscreenEventDirection2 == 0)
			{
				continue;
			}
			componentsInChild2.onClick = new UnityEvent();
			componentsInChild2.onDoubleClick = new UnityEvent();
			componentsInChild2.checkForDoubleClick = false;
			bool fullscreen2 = fullscreenEventDirection2 > 0;
			componentsInChild2.onClick.AddListener(UnityAction.op_Implicit((Action)delegate
			{
				SetNativeMapFullscreen(board, fullscreen2);
			}));
			componentsInChild2.isInteractable = true;
			try
			{
				if ((Object)(object)componentsInChild2.targetButton != (Object)null)
				{
					((Selectable)componentsInChild2.targetButton).interactable = true;
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
		foreach (Button componentsInChild3 in preparationPanel.GetComponentsInChildren<Button>(true))
		{
			int fullscreenEventDirection3 = GetFullscreenEventDirection((UnityEventBase)(object)(((Object)(object)componentsInChild3 == (Object)null) ? null : componentsInChild3.onClick), ((Object)(object)componentsInChild3 == (Object)null) ? null : ((Component)componentsInChild3).gameObject, board);
			if (fullscreenEventDirection3 != 0)
			{
				componentsInChild3.onClick = new ButtonClickedEvent();
				bool fullscreen3 = fullscreenEventDirection3 > 0;
				((UnityEvent)componentsInChild3.onClick).AddListener(UnityAction.op_Implicit((Action)delegate
				{
					SetNativeMapFullscreen(board, fullscreen3);
				}));
				((Selectable)componentsInChild3).interactable = true;
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
		log.LogInfo((object)("Modded Operations clone-local fullscreen controls rebound: enter=" + num + ", exit=" + num2 + "."));
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
		if ((Object)(object)control == (Object)null || (Object)(object)board == (Object)null)
		{
			return 0;
		}
		string text = ((Object)control).name ?? string.Empty;
		if (text.IndexOf("FULLSCREEN", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("EXPAND", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("MAXIMIZE", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("REDUCE", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("MINIMIZE", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return 0;
		}
		if ((Object)(object)board.FullscreenMapObject != (Object)null && control.transform.IsChildOf(board.FullscreenMapObject.transform))
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
		if (!((Object)(object)board == (Object)null))
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
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Expected O, but got Unknown
		if ((Object)(object)selector == (Object)null || values == null || values.Length == 0)
		{
			return;
		}
		selector.useLocalization = false;
		selector.saveSelected = false;
		if ((Object)(object)selector.localizedObject != (Object)null)
		{
			((Behaviour)selector.localizedObject).enabled = false;
		}
		selector.items.Clear();
		foreach (string text in values)
		{
			selector.CreateNewItem(text);
		}
		initialIndex = Mathf.Clamp(initialIndex, 0, values.Length - 1);
		selector.defaultIndex = initialIndex;
		selector.index = initialIndex;
		selector.onValueChanged = new HorizontalSelectorEvent();
		if (onChanged != null)
		{
			((UnityEvent<int>)(object)selector.onValueChanged).AddListener(UnityAction<int>.op_Implicit((Action<int>)delegate(int index)
			{
				onChanged(index);
			}));
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

	private static bool ReplaceNativeButtonAction(Object controlObject, Action action)
	{
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Expected O, but got Unknown
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Expected O, but got Unknown
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected O, but got Unknown
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Expected O, but got Unknown
		if (controlObject == (Object)null || action == null)
		{
			return false;
		}
		GameObject val = (GameObject)(object)((controlObject is GameObject) ? controlObject : null);
		if ((Object)(object)val == (Object)null)
		{
			Component val2 = (Component)(object)((controlObject is Component) ? controlObject : null);
			if ((Object)(object)val2 != (Object)null)
			{
				val = val2.gameObject;
			}
		}
		if ((Object)(object)val == (Object)null)
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
		foreach (PanelButton componentsInChild in val.GetComponentsInChildren<PanelButton>(true))
		{
			if (!((Object)(object)componentsInChild == (Object)null))
			{
				componentsInChild.onClick = new UnityEvent();
				componentsInChild.onClick.AddListener(UnityAction.op_Implicit((Action)delegate
				{
					invokeOnce();
				}));
				componentsInChild.isInteractable = true;
				result = true;
			}
		}
		foreach (ButtonManager componentsInChild2 in val.GetComponentsInChildren<ButtonManager>(true))
		{
			if ((Object)(object)componentsInChild2 == (Object)null)
			{
				continue;
			}
			componentsInChild2.onClick = new UnityEvent();
			componentsInChild2.onDoubleClick = new UnityEvent();
			componentsInChild2.checkForDoubleClick = false;
			componentsInChild2.onClick.AddListener(UnityAction.op_Implicit((Action)delegate
			{
				invokeOnce();
			}));
			componentsInChild2.isInteractable = true;
			try
			{
				if ((Object)(object)componentsInChild2.targetButton != (Object)null)
				{
					((Selectable)componentsInChild2.targetButton).interactable = true;
				}
			}
			catch
			{
			}
			result = true;
		}
		foreach (Button componentsInChild3 in val.GetComponentsInChildren<Button>(true))
		{
			if (!((Object)(object)componentsInChild3 == (Object)null))
			{
				componentsInChild3.onClick = new ButtonClickedEvent();
				((UnityEvent)componentsInChild3.onClick).AddListener(UnityAction.op_Implicit((Action)delegate
				{
					invokeOnce();
				}));
				((Selectable)componentsInChild3).interactable = true;
				result = true;
			}
		}
		return result;
	}

	private bool BindNativePreparationBack(MissionLaptop laptop, GameObject privateBoard, Button authoredBack, bool warnIfMissing)
	{
		if ((Object)(object)laptop == (Object)null || (Object)(object)privateBoard == (Object)null)
		{
			return false;
		}
		int instanceID = ((Object)privateBoard).GetInstanceID();
		if (nativeBackBoundBoards.Contains(instanceID))
		{
			return true;
		}
		List<Transform> list = new List<Transform>(1) { privateBoard.transform };
		PanelButton val = null;
		foreach (Transform item in list)
		{
			foreach (PanelButton componentsInChild in ((Component)item).GetComponentsInChildren<PanelButton>(true))
			{
				if (IsNativeBackCandidate(((Object)(object)componentsInChild == (Object)null) ? null : ((Component)componentsInChild).gameObject, authoredBack))
				{
					val = componentsInChild;
					break;
				}
			}
			if ((Object)(object)val != (Object)null)
			{
				break;
			}
		}
		ButtonManager val2 = null;
		if ((Object)(object)val == (Object)null)
		{
			foreach (Transform item2 in list)
			{
				foreach (ButtonManager componentsInChild2 in ((Component)item2).GetComponentsInChildren<ButtonManager>(true))
				{
					if (IsNativeBackCandidate(((Object)(object)componentsInChild2 == (Object)null) ? null : ((Component)componentsInChild2).gameObject, authoredBack))
					{
						val2 = componentsInChild2;
						break;
					}
				}
				if ((Object)(object)val2 != (Object)null)
				{
					break;
				}
			}
		}
		Button val3 = null;
		if ((Object)(object)val == (Object)null && (Object)(object)val2 == (Object)null)
		{
			foreach (Transform item3 in list)
			{
				foreach (Button componentsInChild3 in ((Component)item3).GetComponentsInChildren<Button>(true))
				{
					if (IsNativeBackCandidate(((Object)(object)componentsInChild3 == (Object)null) ? null : ((Component)componentsInChild3).gameObject, authoredBack))
					{
						val3 = componentsInChild3;
						break;
					}
				}
				if ((Object)(object)val3 != (Object)null)
				{
					break;
				}
			}
		}
		if ((Object)(object)val == (Object)null && (Object)(object)val2 == (Object)null && (Object)(object)val3 == (Object)null)
		{
			if (warnIfMissing)
			{
				log.LogWarning((object)"Cerberus could not find the shipped Operation Preparation BACK control after the panel opened; the native board and Modded Operations tab remain available.");
			}
			return false;
		}
		if (!ReplaceNativeButtonAction((Object)(object)(((Object)(object)val != (Object)null) ? ((Component)val).gameObject : (((Object)(object)val2 != (Object)null) ? ((Component)val2).gameObject : ((Component)val3).gameObject)), delegate
		{
			if (!((Object)(object)privateBoard == (Object)null) && privateBoard.activeSelf)
			{
				ReturnNativeOperationToModdedHome(laptop, privateBoard, authoredBack);
			}
		}))
		{
			if (warnIfMissing)
			{
				log.LogWarning((object)"Cerberus could not replace every click surface beneath the private Operation Preparation BACK control.");
			}
			return false;
		}
		nativeBackBoundBoards.Add(instanceID);
		log.LogInfo((object)("Modded Operations board bound to shipped Operation Preparation BACK control via " + (((Object)(object)val != (Object)null) ? "DreamOS PanelButton" : (((Object)(object)val2 != (Object)null) ? "ButtonManager" : "Unity Button")) + "; all clone-local click surfaces replaced."));
		return true;
	}

	private static bool IsNativeBackCandidate(GameObject control, Button authoredBack)
	{
		if ((Object)(object)control == (Object)null || ((Object)(object)authoredBack != (Object)null && (Object)(object)control == (Object)(object)((Component)authoredBack).gameObject))
		{
			return false;
		}
		if ((((Object)control).name ?? string.Empty).StartsWith("MODDED_", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		return ControlHasText(control, "BACK");
	}

	private static bool ControlHasText(GameObject control, string text)
	{
		if ((Object)(object)control == (Object)null)
		{
			return false;
		}
		if (((Object)control).name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}
		foreach (TMP_Text componentsInChild in control.GetComponentsInChildren<TMP_Text>(true))
		{
			if ((Object)(object)componentsInChild != (Object)null && TitleEquals(componentsInChild.text, text))
			{
				return true;
			}
		}
		return false;
	}

	private void OpenNativeOperationPreparation(MissionLaptop laptop, GameObject privateBoard, Button authoredBack)
	{
		if (!((Object)(object)laptop == (Object)null) && !((Object)(object)privateBoard == (Object)null))
		{
			Transform val = (((Object)(object)laptop.ActiveOperationsTab == (Object)null) ? null : laptop.ActiveOperationsTab.transform.parent);
			if (!((Object)(object)val == (Object)null))
			{
				OperationBoardUI componentInChildren = privateBoard.GetComponentInChildren<OperationBoardUI>(true);
				CloseNativeMapConfirmation(componentInChildren, logClose: false);
				SetNativeMapFullscreen(componentInChildren, fullscreen: false);
				ShowIsolatedNativePreparationPanel(laptop, privateBoard);
				((Component)val).gameObject.SetActive(false);
				BindNativePreparationBack(laptop, privateBoard, authoredBack, warnIfMissing: true);
				log.LogInfo((object)"Modded Operations board opened through its isolated complete Operation Preparation clone; vanillaContentUntouched=true.");
			}
		}
	}

	private void ShowIsolatedNativePreparationPanel(MissionLaptop laptop, GameObject panel)
	{
		if ((Object)(object)laptop == (Object)null || (Object)(object)panel == (Object)null)
		{
			return;
		}
		panel.SetActive(true);
		Animator component = panel.GetComponent<Animator>();
		WindowPanelManager cerberusWindowPanelManager = laptop.cerberusWindowPanelManager;
		string text = (((Object)(object)cerberusWindowPanelManager == (Object)null) ? null : cerberusWindowPanelManager.panelFadeIn);
		string text2 = (((Object)(object)cerberusWindowPanelManager == (Object)null) ? null : cerberusWindowPanelManager.animSpeedKey);
		float num = (((Object)(object)cerberusWindowPanelManager == (Object)null) ? 1f : cerberusWindowPanelManager.panelAnimationSpeed);
		bool flag = false;
		if ((Object)(object)component != (Object)null)
		{
			((Behaviour)component).enabled = true;
			try
			{
				if (!string.IsNullOrEmpty(text2))
				{
					component.SetFloat(text2, num);
				}
				if (!string.IsNullOrEmpty(text))
				{
					int num2 = Animator.StringToHash(text);
					if (component.HasState(0, num2))
					{
						component.Play(num2, 0, 0f);
						component.Update(0f);
						flag = true;
					}
				}
			}
			catch (Exception ex)
			{
				log.LogWarning((object)("Cerberus private preparation fade-in failed: " + ex.GetType().Name + ": " + ex.Message));
			}
		}
		CanvasGroup component2 = panel.GetComponent<CanvasGroup>();
		if ((Object)(object)component2 != (Object)null)
		{
			component2.alpha = 1f;
			component2.interactable = true;
			component2.blocksRaycasts = true;
		}
		log.LogInfo((object)("Cerberus private preparation presentation activated: animator=" + ((Object)(object)component != (Object)null) + ", animatorEnabled=" + ((Object)(object)component != (Object)null && ((Behaviour)component).enabled) + ", fadeInState='" + (text ?? "null") + "', fadeInPlayed=" + flag + ", rootCanvasGroup=" + ((Object)(object)component2 != (Object)null) + "."));
	}

	private void ReturnNativeOperationToModdedHome(MissionLaptop laptop, GameObject privateBoard, Button authoredBack)
	{
		OperationBoardUI componentInChildren = privateBoard.GetComponentInChildren<OperationBoardUI>(true);
		CloseNativeMapConfirmation(componentInChildren, logClose: false);
		SetNativeMapFullscreen(componentInChildren, fullscreen: false);
		privateBoard.SetActive(false);
		Transform val = (((Object)(object)laptop.ActiveOperationsTab == (Object)null) ? null : laptop.ActiveOperationsTab.transform.parent);
		if ((Object)(object)val != (Object)null)
		{
			((Component)val).gameObject.SetActive(true);
		}
		GameObject page = FindChild(val, "MODDED_OPERATIONS_PAGE");
		OpenModdedPage(laptop, page);
		GameObject button = FindDeep(val, "MODDED_OPS_NATIVE_TAB");
		SetTabSelectedState(FindNativeActiveOperationsButton(val), selected: false);
		SetTabSelectedState(FindNativeSimulationOperationsButton(val), selected: false);
		SetTabSelectedState(button, selected: true);
		MarkNativeModdedPageOpened(((Object)laptop).GetInstanceID(), Time.frameCount);
		log.LogInfo((object)"Modded Operations clone-local BACK closed transient UI and returned home without invoking the vanilla panel manager.");
	}

	private void CloseNativeMapConfirmation(OperationBoardUI board, bool logClose)
	{
		if ((Object)(object)board == (Object)null || (Object)(object)board.ConfirmationWindow == (Object)null)
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
				log.LogInfo((object)"Modded Operations confirmation window closed through its clone-local modal route.");
			}
		}
		catch (Exception ex)
		{
			((Component)confirmationWindow).gameObject.SetActive(false);
			log.LogWarning((object)("Modded Operations clone-local confirmation close fell back to disabling its private modal: " + ex.GetType().Name + ": " + ex.Message));
		}
	}

	private static void SetNativeConfirmationLoadingState(CatalogPresentation presentation, bool loading)
	{
		ModalWindowManager val = (((Object)(object)presentation?.Board == (Object)null) ? null : presentation.Board.ConfirmationWindow);
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		if (loading)
		{
			val.descriptionText = "Loading verified map content. The operation will start when it is ready.";
		}
		else if (presentation.SelectedOperation != null)
		{
			val.descriptionText = "Start " + presentation.SelectedOperation.DisplayName + " at " + presentation.SelectedTimeCode + "?";
		}
		if ((Object)(object)val.windowDescription != (Object)null)
		{
			((TMP_Text)val.windowDescription).text = val.descriptionText;
		}
		Object confirmButton = (Object)(object)val.confirmButton;
		GameObject val2 = null;
		GameObject val3 = (GameObject)(object)((confirmButton is GameObject) ? confirmButton : null);
		if (val3 != null)
		{
			val2 = val3;
		}
		else
		{
			Component val4 = (Component)(object)((confirmButton is Component) ? confirmButton : null);
			if (val4 != null)
			{
				val2 = val4.gameObject;
			}
		}
		if ((Object)(object)val2 == (Object)null)
		{
			return;
		}
		foreach (PanelButton componentsInChild in val2.GetComponentsInChildren<PanelButton>(true))
		{
			if ((Object)(object)componentsInChild != (Object)null)
			{
				componentsInChild.isInteractable = !loading;
			}
		}
		foreach (ButtonManager componentsInChild2 in val2.GetComponentsInChildren<ButtonManager>(true))
		{
			if (!((Object)(object)componentsInChild2 == (Object)null))
			{
				componentsInChild2.isInteractable = !loading;
				if ((Object)(object)componentsInChild2.targetButton != (Object)null)
				{
					((Selectable)componentsInChild2.targetButton).interactable = !loading;
				}
			}
		}
		foreach (Button componentsInChild3 in val2.GetComponentsInChildren<Button>(true))
		{
			if ((Object)(object)componentsInChild3 != (Object)null)
			{
				((Selectable)componentsInChild3).interactable = !loading;
			}
		}
	}

	private GameObject CreateNativeBoardActionButton(Transform parent, GameObject template, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Action action)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Expected O, but got Unknown
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Expected O, but got Unknown
		if ((Object)(object)parent == (Object)null || (Object)(object)template == (Object)null || action == null)
		{
			return null;
		}
		GameObject val = Object.Instantiate<GameObject>(template, parent);
		if ((Object)(object)val == (Object)null)
		{
			return null;
		}
		((Object)val).name = name;
		RectTransform component = val.GetComponent<RectTransform>();
		if ((Object)(object)component != (Object)null)
		{
			component.anchorMin = anchorMin;
			component.anchorMax = anchorMax;
			component.offsetMin = Vector2.zero;
			component.offsetMax = Vector2.zero;
			((Transform)component).localScale = Vector3.one;
		}
		val.SetActive(true);
		ButtonManager val2 = val.GetComponent<ButtonManager>() ?? val.GetComponentInChildren<ButtonManager>(true);
		if ((Object)(object)val2 == (Object)null)
		{
			Object.Destroy((Object)(object)val);
			return null;
		}
		val2.useLocalization = false;
		val2.buttonText = text;
		if ((Object)(object)val2.localizedObject != (Object)null)
		{
			((Behaviour)val2.localizedObject).enabled = false;
		}
		SetTmpText((TMP_Text)(object)val2.normalTextObj, text);
		SetTmpText((TMP_Text)(object)val2.highlightTextObj, text);
		SetTmpText((TMP_Text)(object)val2.pressedTextObj, text);
		SetTmpText((TMP_Text)(object)val2.disabledTextObj, text);
		foreach (TMP_Text componentsInChild in val.GetComponentsInChildren<TMP_Text>(true))
		{
			DisableLocalizationComponent(((Component)componentsInChild).gameObject);
			SetTmpText(componentsInChild, text);
		}
		val2.onClick = new UnityEvent();
		val2.onDoubleClick = new UnityEvent();
		val2.checkForDoubleClick = false;
		val2.onClick.AddListener(UnityAction.op_Implicit((Action)delegate
		{
			action();
		}));
		val2.isInteractable = true;
		try
		{
			if ((Object)(object)val2.targetButton != (Object)null)
			{
				((Selectable)val2.targetButton).interactable = true;
			}
		}
		catch
		{
		}
		try
		{
			val2.UpdateUI();
		}
		catch
		{
		}
		try
		{
			val2.UpdateState();
		}
		catch
		{
		}
		return val;
	}

	private static void RestoreAuthoredPresentationChildren(Transform parent, string nativeShellName)
	{
		if ((Object)(object)parent == (Object)null)
		{
			return;
		}
		for (int i = 0; i < parent.childCount; i++)
		{
			Transform child = parent.GetChild(i);
			if ((Object)(object)child != (Object)null && ((Object)child).name != nativeShellName)
			{
				((Component)child).gameObject.SetActive(true);
			}
		}
	}

	private static void SetTmpText(TMP_Text label, string text)
	{
		if (!((Object)(object)label == (Object)null))
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
		if ((Object)(object)list == (Object)null)
		{
			return null;
		}
		GameObject val = null;
		for (int i = 0; i < list.childCount; i++)
		{
			Transform child = list.GetChild(i);
			if (!((Object)(object)child == (Object)null))
			{
				if ((Object)(object)val == (Object)null && (Object)(object)((Component)child).GetComponentInChildren<TMP_Text>(true) != (Object)null)
				{
					val = ((Component)child).gameObject;
				}
				if ((Object)(object)((Component)child).GetComponentInChildren<OperationSelectionUI>(true) != (Object)null)
				{
					return ((Component)child).gameObject;
				}
			}
		}
		return val;
	}

	private static int[] BuildRelativeChildIndexPath(Transform root, Transform descendant)
	{
		if ((Object)(object)root == (Object)null || (Object)(object)descendant == (Object)null)
		{
			return null;
		}
		List<int> list = new List<int>();
		Transform val = descendant;
		while ((Object)(object)val != (Object)null && (Object)(object)val != (Object)(object)root)
		{
			list.Add(val.GetSiblingIndex());
			val = val.parent;
		}
		if ((Object)(object)val != (Object)(object)root)
		{
			return null;
		}
		list.Reverse();
		return list.ToArray();
	}

	private static Transform FollowRelativeChildIndexPath(Transform root, int[] indexes)
	{
		if ((Object)(object)root == (Object)null || indexes == null)
		{
			return null;
		}
		Transform val = root;
		foreach (int num in indexes)
		{
			if (num < 0 || num >= val.childCount)
			{
				return null;
			}
			val = val.GetChild(num);
		}
		return val;
	}

	private static void SetChildrenActive(Transform parent, bool active)
	{
		if ((Object)(object)parent == (Object)null)
		{
			return;
		}
		for (int i = 0; i < parent.childCount; i++)
		{
			Transform child = parent.GetChild(i);
			if ((Object)(object)child != (Object)null)
			{
				((Component)child).gameObject.SetActive(active);
			}
		}
	}

	private static List<GameObject> CaptureDirectChildren(Transform parent)
	{
		List<GameObject> list = new List<GameObject>();
		if ((Object)(object)parent == (Object)null)
		{
			return list;
		}
		for (int i = 0; i < parent.childCount; i++)
		{
			Transform child = parent.GetChild(i);
			if ((Object)(object)child != (Object)null)
			{
				list.Add(((Component)child).gameObject);
			}
		}
		return list;
	}

	private static void SetGameObjectsActive(object collection, bool active)
	{
		foreach (object item in ReadListItems(collection))
		{
			GameObject val = ExtractGameObject(item);
			if ((Object)(object)val != (Object)null)
			{
				val.SetActive(active);
			}
		}
	}

	private static void SetActiveSafe(GameObject gameObject, bool active)
	{
		if ((Object)(object)gameObject != (Object)null)
		{
			gameObject.SetActive(active);
		}
	}

	private static void SetComponentActiveSafe(Component component, bool active)
	{
		if ((Object)(object)component != (Object)null)
		{
			component.gameObject.SetActive(active);
		}
	}

	private static void SetFullStretch(RectTransform rect)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)rect == (Object)null))
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			((Transform)rect).localScale = Vector3.one;
		}
	}

	private static TMP_Text FindNamedText(Transform root, string name)
	{
		GameObject val = FindDeep(root, name);
		if ((Object)(object)val == (Object)null)
		{
			return null;
		}
		return val.GetComponent<TMP_Text>() ?? val.GetComponentInChildren<TMP_Text>(true);
	}

	private static TMP_Text FindNativeBriefingText(GameObject shell, params GameObject[] excludedRoots)
	{
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)shell == (Object)null)
		{
			return null;
		}
		TMP_Text val = FindNamedText(shell.transform, "Operation Selection Briefing");
		if ((Object)(object)val != (Object)null && !IsInsideAny(val.transform, excludedRoots))
		{
			return val;
		}
		TMP_Text result = null;
		float num = float.MinValue;
		foreach (LocalizationTMPEvent componentsInChild in shell.GetComponentsInChildren<LocalizationTMPEvent>(true))
		{
			TMP_Text val2 = null;
			try
			{
				val2 = (TMP_Text)(object)(((Object)(object)componentsInChild == (Object)null) ? null : componentsInChild.TMP);
			}
			catch
			{
			}
			if ((Object)(object)val2 == (Object)null)
			{
				continue;
			}
			bool flag = false;
			if (excludedRoots != null)
			{
				foreach (GameObject val3 in excludedRoots)
				{
					if ((Object)(object)val3 != (Object)null && val2.transform.IsChildOf(val3.transform))
					{
						flag = true;
						break;
					}
				}
			}
			if (!flag)
			{
				RectTransform rectTransform = val2.rectTransform;
				float num2;
				if (!((Object)(object)rectTransform == (Object)null))
				{
					Rect rect = rectTransform.rect;
					float width = ((Rect)(ref rect)).width;
					rect = rectTransform.rect;
					num2 = Mathf.Abs(width * ((Rect)(ref rect)).height);
				}
				else
				{
					num2 = 0f;
				}
				float num3 = num2;
				string text = ((Object)((Component)val2).gameObject).name ?? string.Empty;
				string obj2 = val2.text ?? string.Empty;
				if (text.IndexOf("brief", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					num3 += 1000000f;
				}
				if (obj2.Length >= 80)
				{
					num3 += 500000f;
				}
				if (num3 > num)
				{
					result = val2;
					num = num3;
				}
			}
		}
		return result;
	}

	private static bool IsInsideAny(Transform candidate, GameObject[] roots)
	{
		if ((Object)(object)candidate == (Object)null || roots == null)
		{
			return false;
		}
		foreach (GameObject val in roots)
		{
			if ((Object)(object)val != (Object)null && candidate.IsChildOf(val.transform))
			{
				return true;
			}
		}
		return false;
	}

	private static void RewriteClonedPageHeading(GameObject shell)
	{
		if ((Object)(object)shell == (Object)null)
		{
			return;
		}
		foreach (TMP_Text componentsInChild in shell.GetComponentsInChildren<TMP_Text>(true))
		{
			if (!((Object)(object)componentsInChild == (Object)null) && (TitleEquals(componentsInChild.text, "ACTIVE OPERATIONS") || TitleEquals(componentsInChild.text, "ACTIVE OPS")))
			{
				DisableLocalizationComponent(((Component)componentsInChild).gameObject);
				if ((Object)(object)componentsInChild.transform.parent != (Object)null)
				{
					DisableLocalizationComponent(((Component)componentsInChild.transform.parent).gameObject);
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
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_0228: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		//IL_023d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0248: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)row == (Object)null)
		{
			return;
		}
		HashSet<int> hashSet = new HashSet<int>();
		OperationSelectionUI componentInChildren = row.GetComponentInChildren<OperationSelectionUI>(true);
		List<KeyValuePair<TMP_Text, string>> list = new List<KeyValuePair<TMP_Text, string>>();
		if ((Object)(object)componentInChildren != (Object)null)
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
			GameObject val = ExtractGameObject(ReadMember(componentInChildren, "ClassfiedCover"));
			GameObject val2 = ExtractGameObject(ReadMember(componentInChildren, "RegionCover"));
			if ((Object)(object)val != (Object)null)
			{
				val.SetActive(false);
			}
			if ((Object)(object)val2 != (Object)null)
			{
				val2.SetActive(false);
			}
		}
		RectTransform component = row.GetComponent<RectTransform>();
		foreach (TMP_Text componentsInChild in row.GetComponentsInChildren<TMP_Text>(true))
		{
			if ((Object)(object)componentsInChild == (Object)null || hashSet.Contains(((Object)componentsInChild).GetInstanceID()))
			{
				continue;
			}
			string text = null;
			float num = float.MaxValue;
			foreach (KeyValuePair<TMP_Text, string> item in list)
			{
				if (!((Object)(object)item.Key == (Object)null))
				{
					float num2 = Mathf.Abs(((Transform)componentsInChild.rectTransform).position.x - ((Transform)item.Key.rectTransform).position.x);
					if (num2 < num)
					{
						text = item.Value;
						num = num2;
					}
				}
			}
			if (text == null && (Object)(object)component != (Object)null)
			{
				Rect rect = component.rect;
				if (Mathf.Abs(((Rect)(ref rect)).width) > 0.01f)
				{
					RectTransform rectTransform = componentsInChild.rectTransform;
					rect = componentsInChild.rectTransform.rect;
					float x = ((Transform)component).InverseTransformPoint(((Transform)rectTransform).TransformPoint(Vector2.op_Implicit(((Rect)(ref rect)).center))).x;
					rect = component.rect;
					float num3 = x - ((Rect)(ref rect)).xMin;
					rect = component.rect;
					float num4 = num3 / ((Rect)(ref rect)).width;
					text = ((num4 < 0.44f) ? operationName : ((num4 < 0.72f) ? duration : threat));
				}
			}
			if (text != null)
			{
				DisableLocalizationComponent(((Component)componentsInChild).gameObject);
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
		GameObject val = ExtractGameObject(ReadMember(owner, memberName));
		if ((Object)(object)val == (Object)null)
		{
			return null;
		}
		return val.GetComponent<TMP_Text>() ?? val.GetComponentInChildren<TMP_Text>(true);
	}

	private static void AddColumnReference(List<KeyValuePair<TMP_Text, string>> references, TMP_Text label, string value)
	{
		if (references != null && (Object)(object)label != (Object)null && !string.IsNullOrEmpty(value))
		{
			references.Add(new KeyValuePair<TMP_Text, string>(label, value));
		}
	}

	private static bool SetMemberText(object owner, string memberName, string value, HashSet<int> assigned)
	{
		GameObject val = ExtractGameObject(ReadMember(owner, memberName));
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}
		TMP_Text val2 = val.GetComponent<TMP_Text>() ?? val.GetComponentInChildren<TMP_Text>(true);
		if ((Object)(object)val2 == (Object)null)
		{
			return false;
		}
		DisableLocalizationComponent(val);
		if ((Object)(object)((Component)val2).gameObject != (Object)(object)val)
		{
			DisableLocalizationComponent(((Component)val2).gameObject);
		}
		val2.text = value;
		val2.enableWordWrapping = false;
		val2.enableAutoSizing = true;
		val2.fontSizeMin = Mathf.Max(8f, val2.fontSize * 0.55f);
		val2.fontSizeMax = Mathf.Max(val2.fontSizeMin, val2.fontSize);
		assigned?.Add(((Object)val2).GetInstanceID());
		return true;
	}

	private static void DisableLocalizationComponent(GameObject gameObject)
	{
		if ((Object)(object)gameObject == (Object)null)
		{
			return;
		}
		try
		{
			LocalizationTMPEvent component = gameObject.GetComponent<LocalizationTMPEvent>();
			if ((Object)(object)component != (Object)null)
			{
				((Behaviour)component).enabled = false;
			}
		}
		catch
		{
		}
		try
		{
			LocalizedObject component2 = gameObject.GetComponent<LocalizedObject>();
			if ((Object)(object)component2 != (Object)null)
			{
				((Behaviour)component2).enabled = false;
			}
		}
		catch
		{
		}
		foreach (Component component3 in gameObject.GetComponents<Component>())
		{
			if ((Object)(object)component3 == (Object)null)
			{
				continue;
			}
			string name = ((object)component3).GetType().Name;
			if (name.IndexOf("LocalizationTMPEvent", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("LocalizedObject", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				Behaviour val = (Behaviour)(object)((component3 is Behaviour) ? component3 : null);
				if (val != null)
				{
					val.enabled = false;
				}
				Object.Destroy((Object)(object)component3);
			}
		}
	}

	private static void StackNativeRows(GameObject first, GameObject second)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		RectTransform val = (((Object)(object)first == (Object)null) ? null : first.GetComponent<RectTransform>());
		RectTransform val2 = (((Object)(object)second == (Object)null) ? null : second.GetComponent<RectTransform>());
		if (!((Object)(object)val == (Object)null) && !((Object)(object)val2 == (Object)null))
		{
			Vector2 anchoredPosition = val.anchoredPosition;
			Rect rect = val.rect;
			float num = Mathf.Max(1f, ((Rect)(ref rect)).height);
			val2.anchoredPosition = new Vector2(anchoredPosition.x, anchoredPosition.y - num - 6f);
		}
	}

	private static void StackNativeRows(IReadOnlyList<GameObject> rows)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		if (rows == null || rows.Count < 2)
		{
			return;
		}
		RectTransform val = (((Object)(object)rows[0] == (Object)null) ? null : rows[0].GetComponent<RectTransform>());
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		Vector2 anchoredPosition = val.anchoredPosition;
		Rect rect = val.rect;
		float num = Mathf.Max(1f, ((Rect)(ref rect)).height);
		for (int i = 1; i < rows.Count; i++)
		{
			RectTransform val2 = (((Object)(object)rows[i] == (Object)null) ? null : rows[i].GetComponent<RectTransform>());
			if ((Object)(object)val2 != (Object)null)
			{
				val2.anchoredPosition = new Vector2(anchoredPosition.x, anchoredPosition.y - (float)i * (num + 6f));
			}
		}
	}

	private void RebindNativeRow(GameObject row, Action singleClick, Action doubleClick = null)
	{
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Expected O, but got Unknown
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Expected O, but got Unknown
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Expected O, but got Unknown
		if ((Object)(object)row == (Object)null || singleClick == null)
		{
			return;
		}
		GameObject val = FindDeep(row.transform, "MODDED_NATIVE_ROW_HIT_TARGET");
		if ((Object)(object)val != (Object)null)
		{
			Object.Destroy((Object)(object)val);
		}
		ButtonManager val2 = null;
		foreach (ButtonManager componentsInChild in row.GetComponentsInChildren<ButtonManager>(true))
		{
			if (!((Object)(object)componentsInChild == (Object)null))
			{
				if ((Object)(object)val2 == (Object)null || (Object)(object)componentsInChild.targetButton != (Object)null)
				{
					val2 = componentsInChild;
				}
				if ((Object)(object)componentsInChild.targetButton != (Object)null)
				{
					break;
				}
			}
		}
		if ((Object)(object)val2 == (Object)null)
		{
			OperationSelectionUI componentInChildren = row.GetComponentInChildren<OperationSelectionUI>(true);
			Button val3 = (((Object)(object)componentInChildren == (Object)null) ? null : ((Component)componentInChildren).GetComponent<Button>());
			if ((Object)(object)val3 == (Object)null)
			{
				val3 = row.GetComponent<Button>();
			}
			if ((Object)(object)val3 == (Object)null)
			{
				val3 = row.GetComponentInChildren<Button>(true);
			}
			if ((Object)(object)val3 == (Object)null)
			{
				ManualLogSource obj = log;
				if (obj != null)
				{
					obj.LogWarning((object)("Cerberus native row binding skipped because the shipped row had neither ButtonManager nor Button: " + ((Object)row).name + "."));
				}
				return;
			}
			float doubleClickPeriod = 0.3f;
			try
			{
				CerebusUiBase val4 = (((Object)(object)componentInChildren == (Object)null) ? null : componentInChildren.CerebusUiBase);
				if ((Object)(object)val4 != (Object)null && val4.clickcountTime >= 0.05f && val4.clickcountTime <= 1f)
				{
					doubleClickPeriod = val4.clickcountTime;
				}
			}
			catch
			{
			}
			float previousClickTime = -100f;
			val3.onClick = new ButtonClickedEvent();
			((UnityEvent)val3.onClick).AddListener(UnityAction.op_Implicit((Action)delegate
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
			}));
			((Selectable)val3).interactable = true;
			ManualLogSource obj3 = log;
			if (obj3 != null)
			{
				obj3.LogInfo((object)("Cerberus native row bound through shipped Unity Button: row=" + ((Object)row).name + ", button=" + ((Object)((Component)val3).gameObject).name + ", doubleClickPeriod=" + doubleClickPeriod.ToString("0.###") + "s."));
			}
			return;
		}
		val2.onClick = new UnityEvent();
		val2.onDoubleClick = new UnityEvent();
		val2.checkForDoubleClick = doubleClick != null;
		val2.onClick.AddListener(UnityAction.op_Implicit((Action)delegate
		{
			singleClick();
		}));
		if (doubleClick != null)
		{
			val2.onDoubleClick.AddListener(UnityAction.op_Implicit((Action)delegate
			{
				doubleClick();
			}));
		}
		val2.isInteractable = true;
		try
		{
			if ((Object)(object)val2.targetButton != (Object)null)
			{
				((Selectable)val2.targetButton).interactable = true;
			}
		}
		catch
		{
		}
		try
		{
			val2.UpdateState();
		}
		catch
		{
		}
	}

	private static void SetNativeRowSelected(GameObject row, bool selected)
	{
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)row == (Object)null)
		{
			return;
		}
		bool flag = false;
		foreach (Transform componentsInChild in row.GetComponentsInChildren<Transform>(true))
		{
			if (!((Object)(object)componentsInChild == (Object)null) && !((Object)(object)componentsInChild == (Object)(object)row.transform) && ((Object)componentsInChild).name.IndexOf("Selected", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				((Component)componentsInChild).gameObject.SetActive(selected);
				flag = true;
			}
		}
		if (!flag)
		{
			Image val = row.GetComponent<Image>() ?? row.GetComponentInChildren<Image>(true);
			if ((Object)(object)val != (Object)null)
			{
				((Graphic)val).color = (selected ? new Color(((Graphic)val).color.r, ((Graphic)val).color.g, ((Graphic)val).color.b, 1f) : new Color(((Graphic)val).color.r, ((Graphic)val).color.g, ((Graphic)val).color.b, 0.72f));
			}
		}
	}

	private static GameObject CreateNativeActionButton(Transform parent, GameObject template, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Action action)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Expected O, but got Unknown
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)parent == (Object)null || (Object)(object)template == (Object)null || action == null)
		{
			return null;
		}
		GameObject val = Object.Instantiate<GameObject>(template, parent);
		if ((Object)(object)val == (Object)null)
		{
			return null;
		}
		((Object)val).name = name;
		RectTransform component = val.GetComponent<RectTransform>();
		if ((Object)(object)component != (Object)null)
		{
			component.anchorMin = anchorMin;
			component.anchorMax = anchorMax;
			component.offsetMin = Vector2.zero;
			component.offsetMax = Vector2.zero;
			((Transform)component).localScale = Vector3.one;
		}
		SetButtonText(val, text);
		FitTabTitleText(val, text);
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
		PanelButton component2 = val.GetComponent<PanelButton>();
		if ((Object)(object)component2 != (Object)null)
		{
			component2.onClick = new UnityEvent();
			component2.onClick.AddListener(UnityAction.op_Implicit((Action)delegate
			{
				invokeOnce();
			}));
			component2.isInteractable = true;
			component2.isSelected = false;
		}
		Button val2 = val.GetComponent<Button>();
		if ((Object)(object)val2 == (Object)null)
		{
			val2 = val.AddComponent<Button>();
		}
		if ((Object)(object)((Selectable)val2).targetGraphic == (Object)null)
		{
			GameObject val3 = new GameObject("MODDED_NATIVE_ACTION_HIT_TARGET");
			val3.transform.SetParent(val.transform, false);
			SetFullStretch(val3.AddComponent<RectTransform>());
			Image val4 = val3.AddComponent<Image>();
			((Graphic)val4).color = new Color(0f, 0f, 0f, 0f);
			((Graphic)val4).raycastTarget = true;
			((Selectable)val2).targetGraphic = (Graphic)(object)val4;
		}
		((Selectable)val2).transition = (Transition)0;
		((UnityEventBase)val2.onClick).RemoveAllListeners();
		((UnityEvent)val2.onClick).AddListener(UnityAction.op_Implicit((Action)delegate
		{
			invokeOnce();
		}));
		((Selectable)val2).interactable = true;
		val.SetActive(true);
		return val;
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
				if (!((Object)(object)pendingTransitionSnapshot.Laptop == (Object)null))
				{
					log.LogInfo((object)CaptureLaptopTransitionState(pendingTransitionSnapshot.Laptop, pendingTransitionSnapshot.Page, "next-frame state", pendingTransitionSnapshot.Source, frameCount));
				}
			}
		}
	}

	private static string CaptureLaptopTransitionState(MissionLaptop laptop, GameObject page, string phase, string eventSource, int frame)
	{
		if ((Object)(object)laptop == (Object)null)
		{
			return "Cerberus transition snapshot: laptop=null, phase=" + phase + ".";
		}
		WindowPanelManager cerberusWindowPanelManager = laptop.cerberusWindowPanelManager;
		string text = (((Object)(object)cerberusWindowPanelManager == (Object)null) ? "null" : ("currentPanelIndex=" + DescribeValue(ReadMember(cerberusWindowPanelManager, "currentPanelIndex")) + ", currentButtonIndex=" + DescribeValue(ReadMember(cerberusWindowPanelManager, "currentButtonIndex")) + ", newPanelIndex=" + DescribeValue(ReadMember(cerberusWindowPanelManager, "newPanelIndex")) + ", currentPanel=" + DescribeValue(ReadMember(cerberusWindowPanelManager, "currentPanel")) + ", currentButton=" + DescribeValue(ReadMember(cerberusWindowPanelManager, "currentButton")) + ", panels=" + DescribePanelItems(ReadMember(cerberusWindowPanelManager, "panels"))));
		Type type = ResolveLoadedTypeByExactName("MissionLaptopNetworkState", "Il2Cpp.MissionLaptopNetworkState");
		object owner = ((type == null) ? null : ReadMember(type, null, "singleton"));
		GameObject gameObject = (((Object)(object)page == (Object)null) ? null : FindDeep(page.transform, "MODDED_HOME"));
		GameObject gameObject2 = (((Object)(object)page == (Object)null) ? null : FindDeep(page.transform, "MODDED_BRIEFING"));
		return "Cerberus transition snapshot: phase=" + phase + ", source=" + eventSource + ", frame=" + frame + ", laptopId=" + ((Object)laptop).GetInstanceID() + ", laptopPath=" + HierarchyPath(((Component)laptop).transform) + ", manager={" + text + "}, ActiveOperationsTab={" + DescribeActivity(laptop.ActiveOperationsTab) + "}, SimulationOperationsTab={" + DescribeActivity(laptop.SimulationOperationsTab) + "}, ActiveOperationsList={" + DescribeActivity(ExtractGameObject(ReadMember(laptop, "ActiveOperationsList"))) + "}, SimulationOperationList={" + DescribeActivity(ExtractGameObject(ReadMember(laptop, "SimulationOperationList"))) + "}, TargetPackageParent={" + DescribeActivity(ExtractGameObject(ReadMember(laptop, "TargetPackageParent"))) + "}, opBoardParent={" + DescribeActivity(ExtractGameObject(ReadMember(laptop, "opBoardParent"))) + "}, network={CurrentLaptopPage=" + DescribeValue(ReadMember(owner, "CurrentLaptopPage")) + ", OnSimulationPage=" + DescribeValue(ReadMember(owner, "OnSimulationPage")) + "}, displayers=" + DescribeMissionLaptopDisplayers() + ", modPage={" + DescribeActivity(page) + "}, modHome={" + DescribeActivity(gameObject) + "}, modBriefing={" + DescribeActivity(gameObject2) + "}.";
	}

	private static string DescribePanelItems(object panelList)
	{
		List<object> list = ReadListItems(panelList);
		List<string> list2 = new List<string>(list.Count);
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
		List<Component> list = FindMissionLaptopComponents(type);
		List<string> list2 = new List<string>(list.Count);
		foreach (Component item in list)
		{
			if (!((Object)(object)item == (Object)null) && !((Object)(object)item.gameObject == (Object)null))
			{
				list2.Add("id=" + ((Object)item).GetInstanceID() + ", path=" + HierarchyPath(item.transform) + ", selectedOperation=" + DescribeValue(ReadFirstMember(item, "selectedOperation", "SelectedOperation", "currentOperation")) + ", selectedTargetPackage=" + DescribeValue(ReadFirstMember(item, "selectedTargetPackage", "SelectedTargetPackage", "currentTargetPackage")) + ", opboard=" + DescribeValue(ReadFirstMember(item, "opBoard", "opboard", "operationBoard", "currentOpBoard")));
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

	private static List<object> ReadListItems(object list)
	{
		List<object> list2 = new List<object>();
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
		GameObject val = ExtractGameObject(value);
		if ((Object)(object)val != (Object)null)
		{
			return ((Object)val).name + "@" + HierarchyPath(val.transform);
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
		if ((Object)(object)root == (Object)null)
		{
			return null;
		}
		PanelButton[] array;
		try
		{
			array = Il2CppArrayBase<PanelButton>.op_Implicit(((Component)root).GetComponentsInChildren<PanelButton>(true));
		}
		catch
		{
			return null;
		}
		GameObject val = null;
		PanelButton[] array2 = array;
		foreach (PanelButton val2 in array2)
		{
			if (!((Object)(object)val2 == (Object)null))
			{
				string text = ((Object)((Component)val2).gameObject).name ?? string.Empty;
				if (text.IndexOf("ACTIVE OPERATIONS BUTTON", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return ((Component)val2).gameObject;
				}
				if ((Object)(object)val == (Object)null && text.IndexOf("ACTIVE", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					val = ((Component)val2).gameObject;
				}
			}
		}
		return val;
	}

	private static GameObject FindNativeSimulationOperationsButton(Transform root)
	{
		if ((Object)(object)root == (Object)null)
		{
			return null;
		}
		PanelButton[] array;
		try
		{
			array = Il2CppArrayBase<PanelButton>.op_Implicit(((Component)root).GetComponentsInChildren<PanelButton>(true));
		}
		catch
		{
			return null;
		}
		PanelButton[] array2 = array;
		foreach (PanelButton val in array2)
		{
			if (!((Object)(object)val == (Object)null) && (((Object)((Component)val).gameObject).name ?? string.Empty).IndexOf("OPERATION SIMULATION BUTTON", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return ((Component)val).gameObject;
			}
		}
		return null;
	}

	private static void PositionAsThirdTab(Transform parent, GameObject activeButton, GameObject simulationButton, GameObject moddedButton)
	{
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0275: Unknown result type (might be due to invalid IL or missing references)
		//IL_027a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0293: Unknown result type (might be due to invalid IL or missing references)
		//IL_0298: Unknown result type (might be due to invalid IL or missing references)
		//IL_030f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0314: Unknown result type (might be due to invalid IL or missing references)
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_032b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0330: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		//IL_044d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0452: Unknown result type (might be due to invalid IL or missing references)
		//IL_0456: Unknown result type (might be due to invalid IL or missing references)
		//IL_045b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_0239: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)parent == (Object)null || (Object)(object)moddedButton == (Object)null)
		{
			return;
		}
		RectTransform component = ((Component)parent).GetComponent<RectTransform>();
		RectTransform component2 = moddedButton.GetComponent<RectTransform>();
		RectTransform val = (((Object)(object)simulationButton == (Object)null) ? null : simulationButton.GetComponent<RectTransform>());
		RectTransform val2 = (((Object)(object)activeButton == (Object)null) ? null : activeButton.GetComponent<RectTransform>());
		if ((Object)(object)((Component)parent).GetComponent<HorizontalLayoutGroup>() != (Object)null)
		{
			int num = (((Object)(object)simulationButton == (Object)null) ? activeButton.transform.GetSiblingIndex() : simulationButton.transform.GetSiblingIndex());
			moddedButton.transform.SetSiblingIndex(Mathf.Min(num + 1, parent.childCount - 1));
			Canvas.ForceUpdateCanvases();
			logStatic("native tab row uses HorizontalLayoutGroup; inserted after simulation");
		}
		else
		{
			if ((Object)(object)component2 == (Object)null || (Object)(object)val2 == (Object)null || (Object)(object)component == (Object)null)
			{
				return;
			}
			Bounds val3 = BoundsInParent(component, val2);
			Bounds val4 = (Bounds)(((Object)(object)val == (Object)null) ? new Bounds(new Vector3(((Bounds)(ref val3)).max.x + ((Bounds)(ref val3)).size.x, ((Bounds)(ref val3)).center.y, 0f), ((Bounds)(ref val3)).size) : BoundsInParent(component, val));
			float x = ((Bounds)(ref val3)).min.x;
			float num2 = (((Object)(object)val == (Object)null) ? (((Bounds)(ref val3)).max.x + ((Bounds)(ref val3)).size.x) : ((Bounds)(ref val4)).max.x);
			float y = ((Bounds)(ref val3)).center.y;
			logStatic("native source rect=" + RectSummary(activeButton) + ", simulation rect=" + RectSummary(simulationButton));
			float num3 = float.PositiveInfinity;
			for (int i = 0; i < parent.childCount; i++)
			{
				Transform child = parent.GetChild(i);
				if ((Object)(object)child == (Object)null || (Object)(object)child == (Object)(object)activeButton.transform || ((Object)(object)simulationButton != (Object)null && (Object)(object)child == (Object)(object)simulationButton.transform) || (Object)(object)child == (Object)(object)moddedButton.transform || ((Object)child).name.IndexOf("BRIEFING", StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}
				RectTransform component3 = ((Component)child).GetComponent<RectTransform>();
				if (!((Object)(object)component3 == (Object)null))
				{
					Bounds val5 = BoundsInParent(component, component3);
					if (((Bounds)(ref val5)).min.x > x && ((Bounds)(ref val5)).min.x < num3)
					{
						num3 = ((Bounds)(ref val5)).min.x;
					}
				}
			}
			if (!float.IsPositiveInfinity(num3))
			{
				num2 = Mathf.Min(num2, num3 - 12f);
			}
			float num4 = num2;
			Rect rect = component.rect;
			num2 = Mathf.Min(num4, ((Rect)(ref rect)).xMax - 8f);
			float num5 = x;
			rect = component.rect;
			x = Mathf.Max(num5, ((Rect)(ref rect)).xMin + 8f);
			float num6 = 8f;
			float num7 = (num2 - x - num6 * 2f) / 3f;
			if (num7 < 96f)
			{
				num7 = Mathf.Max(96f, Mathf.Min(((Bounds)(ref val3)).size.x, ((Bounds)(ref val4)).size.x));
			}
			float num8 = num7 * 3f + num6 * 2f;
			rect = component.rect;
			if (num8 > ((Rect)(ref rect)).width - 16f)
			{
				rect = component.rect;
				num7 = Mathf.Max(96f, (((Rect)(ref rect)).width - 16f - num6 * 2f) / 3f);
			}
			SetTabRect(val2, component, x + num7 * 0.5f, y, num7);
			if ((Object)(object)val != (Object)null)
			{
				SetTabRect(val, component, x + num7 + num6 + num7 * 0.5f, y, num7);
			}
			SetTabRect(component2, component, x + (num7 + num6) * 2f + num7 * 0.5f, y, num7);
			int num9 = (((Object)(object)simulationButton == (Object)null) ? activeButton.transform.GetSiblingIndex() : simulationButton.transform.GetSiblingIndex());
			moddedButton.transform.SetSiblingIndex(Mathf.Min(num9 + 1, parent.childCount - 1));
			Canvas.ForceUpdateCanvases();
			string[] obj = new string[9]
			{
				"native tab row normalized in parent-local coordinates: rowLeft=",
				x.ToString("F1"),
				", rowRight=",
				num2.ToString("F1"),
				", tabWidth=",
				num7.ToString("F1"),
				", parent=",
				null,
				null
			};
			rect = component.rect;
			obj[7] = ((object)((Rect)(ref rect)).size/*cast due to constrained. prefix*/).ToString();
			obj[8] = ");";
			logStatic(string.Concat(obj));
		}
	}

	private static void GetRectBoundsInParent(RectTransform rect, RectTransform parent, out float left, out float right, out float bottom, out float top)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		Vector3[] array = (Vector3[])(object)new Vector3[4];
		rect.GetWorldCorners(Il2CppStructArray<Vector3>.op_Implicit(array));
		left = float.PositiveInfinity;
		right = float.NegativeInfinity;
		bottom = float.PositiveInfinity;
		top = float.NegativeInfinity;
		for (int i = 0; i < array.Length; i++)
		{
			Vector3 val = ((Transform)parent).InverseTransformPoint(array[i]);
			left = Mathf.Min(left, val.x);
			right = Mathf.Max(right, val.x);
			bottom = Mathf.Min(bottom, val.y);
			top = Mathf.Max(top, val.y);
		}
	}

	private static Bounds BoundsInParent(RectTransform parent, RectTransform child)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)parent == (Object)null || (Object)(object)child == (Object)null)
		{
			return new Bounds(Vector3.zero, Vector3.zero);
		}
		Rect rect = child.rect;
		float width = ((Rect)(ref rect)).width;
		rect = child.rect;
		float height = ((Rect)(ref rect)).height;
		float num = ((Transform)child).localPosition.x - width * child.pivot.x;
		float num2 = ((Transform)child).localPosition.x + width * (1f - child.pivot.x);
		float num3 = ((Transform)child).localPosition.y - height * child.pivot.y;
		float num4 = ((Transform)child).localPosition.y + height * (1f - child.pivot.y);
		Vector3 val = new Vector3((num + num2) * 0.5f, (num3 + num4) * 0.5f, 0f);
		Vector3 val2 = default(Vector3);
		((Vector3)(ref val2))._002Ector(Mathf.Max(0f, num2 - num), Mathf.Max(0f, num4 - num3), 0f);
		return new Bounds(val, val2);
	}

	private static void SetTabRect(RectTransform rect, RectTransform parent, float centerX, float centerY, float width)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)rect == (Object)null) && !((Object)(object)parent == (Object)null))
		{
			Rect rect2 = rect.rect;
			float num = Mathf.Max(1f, ((Rect)(ref rect2)).height);
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(width, num);
			Vector3 localPosition = ((Transform)rect).localPosition;
			localPosition.x = centerX;
			localPosition.y = centerY;
			((Transform)rect).localPosition = localPosition;
		}
	}

	private static void CopyContentRect(GameObject source, RectTransform destination)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)destination == (Object)null))
		{
			RectTransform val = (((Object)(object)source == (Object)null) ? null : source.GetComponent<RectTransform>());
			if ((Object)(object)val == (Object)null)
			{
				destination.anchorMin = Vector2.zero;
				destination.anchorMax = Vector2.one;
				destination.offsetMin = Vector2.zero;
				destination.offsetMax = Vector2.zero;
			}
			else
			{
				destination.anchorMin = val.anchorMin;
				destination.anchorMax = val.anchorMax;
				destination.pivot = val.pivot;
				destination.anchoredPosition = val.anchoredPosition;
				destination.sizeDelta = val.sizeDelta;
				((Transform)destination).localScale = ((Transform)val).localScale;
				((Transform)destination).localRotation = ((Transform)val).localRotation;
			}
		}
	}

	private static void SetTabSelectedState(GameObject button, bool selected)
	{
		if ((Object)(object)button == (Object)null)
		{
			return;
		}
		PanelButton component = button.GetComponent<PanelButton>();
		if ((Object)(object)component != (Object)null)
		{
			component.isSelected = selected;
			component.SetSelected(selected);
			component.UpdateUI();
			return;
		}
		ButtonManager component2 = button.GetComponent<ButtonManager>();
		if ((Object)(object)component2 != (Object)null)
		{
			component2.UpdateState();
		}
	}

	private static string RectSummary(GameObject go)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)go == (Object)null)
		{
			return "null";
		}
		RectTransform component = go.GetComponent<RectTransform>();
		if (!((Object)(object)component == (Object)null))
		{
			string[] obj = new string[10]
			{
				"anchored=",
				((object)component.anchoredPosition/*cast due to constrained. prefix*/).ToString(),
				", local=",
				((object)((Transform)component).localPosition/*cast due to constrained. prefix*/).ToString(),
				", size=",
				null,
				null,
				null,
				null,
				null
			};
			Rect rect = component.rect;
			obj[5] = ((object)((Rect)(ref rect)).size/*cast due to constrained. prefix*/).ToString();
			obj[6] = ", pivot=";
			obj[7] = ((object)component.pivot/*cast due to constrained. prefix*/).ToString();
			obj[8] = ", parent=";
			obj[9] = (((Object)(object)go.transform.parent == (Object)null) ? "null" : ((Object)go.transform.parent).name);
			return string.Concat(obj);
		}
		return "no-rect";
	}

	private static void logStatic(string message)
	{
		if (instance != null && instance.log != null)
		{
			instance.log.LogInfo((object)("Cerberus " + message + "."));
		}
	}

	private static void FitTabTitleText(GameObject button, string renderedTitle)
	{
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)button == (Object)null || string.IsNullOrWhiteSpace(renderedTitle))
		{
			return;
		}
		int num = 0;
		try
		{
			foreach (TMP_Text componentsInChild in button.GetComponentsInChildren<TMP_Text>(true))
			{
				if (!((Object)(object)componentsInChild == (Object)null) && TitleEquals(componentsInChild.text, renderedTitle))
				{
					float num2 = Mathf.Max(1f, componentsInChild.fontSize);
					RectTransform rectTransform = componentsInChild.rectTransform;
					if ((Object)(object)rectTransform != (Object)null)
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
					componentsInChild.ForceMeshUpdate(true, true);
					num++;
				}
			}
		}
		catch
		{
		}
		Canvas.ForceUpdateCanvases();
		logStatic("title fit applied: button=" + ((Object)button).name + ", renderedTitle='" + renderedTitle + "', copies=" + num);
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
		if ((Object)(object)transform == (Object)null)
		{
			return "<null>";
		}
		List<string> list = new List<string>();
		Transform val = transform;
		while ((Object)(object)val != (Object)null)
		{
			list.Add(((Object)val).name ?? "<unnamed>");
			val = val.parent;
		}
		list.Reverse();
		return string.Join("/", list);
	}

	private static string DescribeActivity(GameObject gameObject)
	{
		if (!((Object)(object)gameObject == (Object)null))
		{
			return "activeSelf=" + gameObject.activeSelf + ", activeInHierarchy=" + gameObject.activeInHierarchy + ", path=" + HierarchyPath(gameObject.transform);
		}
		return "null";
	}

	private static GameObject FindDeep(Transform root, string name)
	{
		if ((Object)(object)root == (Object)null)
		{
			return null;
		}
		if (((Object)root).name == name)
		{
			return ((Component)root).gameObject;
		}
		for (int i = 0; i < root.childCount; i++)
		{
			GameObject val = FindDeep(root.GetChild(i), name);
			if ((Object)(object)val != (Object)null)
			{
				return val;
			}
		}
		return null;
	}

	private static void OpenModdedPage(MissionLaptop laptop, GameObject page)
	{
		if ((Object)(object)laptop != (Object)null)
		{
			if ((Object)(object)laptop.ActiveOperationsTab != (Object)null)
			{
				laptop.ActiveOperationsTab.SetActive(false);
			}
			if ((Object)(object)laptop.SimulationOperationsTab != (Object)null)
			{
				laptop.SimulationOperationsTab.SetActive(false);
			}
		}
		if ((Object)(object)page != (Object)null)
		{
			page.SetActive(true);
			GameObject val = FindDeep(page.transform, "MODDED_HOME");
			GameObject val2 = FindDeep(page.transform, "MODDED_BRIEFING");
			if ((Object)(object)val != (Object)null)
			{
				val.SetActive(true);
			}
			if ((Object)(object)val2 != (Object)null)
			{
				val2.SetActive(false);
			}
		}
	}

	private void DumpOperationSelectionHierarchy(MissionLaptop laptop)
	{
		try
		{
			GameObject activeOperationsTab = laptop.ActiveOperationsTab;
			if ((Object)(object)activeOperationsTab == (Object)null)
			{
				log.LogWarning((object)"Cerberus hierarchy probe: ActiveOperationsTab is null.");
				return;
			}
			log.LogInfo((object)("Cerberus hierarchy probe: ActiveOperationsTab=" + Describe(activeOperationsTab) + "."));
			DumpHierarchy(((Object)(object)activeOperationsTab.transform.parent == (Object)null) ? activeOperationsTab.transform : activeOperationsTab.transform.parent, 0, 3);
		}
		catch (Exception ex)
		{
			log.LogWarning((object)("Cerberus hierarchy probe failed: " + ex.GetType().Name + ": " + ex.Message));
		}
	}

	private void DumpHierarchy(Transform node, int depth, int maxDepth)
	{
		if ((Object)(object)node == (Object)null || depth > maxDepth)
		{
			return;
		}
		List<string> list = new List<string>();
		try
		{
			foreach (Component component in ((Component)node).gameObject.GetComponents<Component>())
			{
				if ((Object)(object)component != (Object)null)
				{
					list.Add(((object)component).GetType().FullName);
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
			flag = (Object)(object)((Component)node).GetComponent<PanelButton>() != (Object)null;
		}
		catch
		{
		}
		try
		{
			flag2 = (Object)(object)((Component)node).GetComponent<ButtonManager>() != (Object)null;
		}
		catch
		{
		}
		try
		{
			flag3 = (Object)(object)((Component)node).GetComponent<Button>() != (Object)null;
		}
		catch
		{
		}
		try
		{
			flag4 = (Object)(object)((Component)node).GetComponent<TMP_Text>() != (Object)null;
		}
		catch
		{
		}
		log.LogInfo((object)("Cerberus hierarchy " + new string(' ', depth * 2) + ((Object)node).name + " components=[" + string.Join(",", list) + "] typed panelButton=" + flag + " buttonManager=" + flag2 + " unityButton=" + flag3 + " tmp=" + flag4));
		for (int i = 0; i < node.childCount; i++)
		{
			DumpHierarchy(node.GetChild(i), depth + 1, maxDepth);
		}
	}

	private static string Describe(GameObject gameObject)
	{
		if (!((Object)(object)gameObject == (Object)null))
		{
			return ((Object)gameObject).name + " parent=" + (((Object)(object)gameObject.transform.parent == (Object)null) ? "null" : ((Object)gameObject.transform.parent).name);
		}
		return "null";
	}

	private static GameObject FindChild(Transform parent, string name)
	{
		if ((Object)(object)parent == (Object)null)
		{
			return null;
		}
		for (int i = 0; i < parent.childCount; i++)
		{
			Transform child = parent.GetChild(i);
			if ((Object)(object)child != (Object)null && ((Object)child).name == name)
			{
				return ((Component)child).gameObject;
			}
		}
		return null;
	}

	private static void SetButtonText(GameObject button, string text)
	{
		if ((Object)(object)button == (Object)null)
		{
			return;
		}
		DisableLocalizationComponent(button);
		PanelButton component = button.GetComponent<PanelButton>();
		if ((Object)(object)component != (Object)null)
		{
			component.buttonText = text;
			component.useLocalization = false;
			component.useCustomText = true;
		}
		ButtonManager component2 = button.GetComponent<ButtonManager>();
		if ((Object)(object)component2 != (Object)null)
		{
			component2.buttonText = text;
			component2.useLocalization = false;
		}
		foreach (TMP_Text componentsInChild in button.GetComponentsInChildren<TMP_Text>(true))
		{
			if (!((Object)(object)componentsInChild == (Object)null))
			{
				DisableLocalizationComponent(((Component)componentsInChild).gameObject);
				componentsInChild.text = text;
			}
		}
	}
}
