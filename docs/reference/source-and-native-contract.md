# Source and native contract

## Exact public source

The complete framework implementation is
[`CerberusNativeTabFix.cs`](../../src/OperatorModdedOperations/CerberusNativeTabFix.cs).
The native asynchronous asset helper is
[`NativeBundleAssetLoader.cs`](../../src/OperatorModdedOperations/NativeBundleAssetLoader.cs).

## Important source members

| Member | Purpose |
| --- | --- |
| `Load()` | Version gate, IL2CPP registration, runner, scene callbacks. |
| `EnsureNativePresentation` | One Cerberus binding per live laptop. |
| `BuildPackageInfiltrationMapPrefab` | Native marker clones and package preview. |
| `BeginSelectedMapPrefetch` | Selected-map cold prefetch. |
| `ProcessPendingLaunch` | Ordered asynchronous bundle registration and the waiting-Confirm continuation. |
| `InvokeNativeCatalogLaunch` | Final native board handoff. |
| `LoadVerifiedMapDependencyAsset<T>` | Borrow one asset from a retained verified dependency. |
| `ConfigureStandalonePlayerSpawnContract` | Current-scene player markers and global capture. |
| `TrySpawnStandalonePveEnemies` | Shipped PVE actor path. |
| `ConfigureStandalonePvpGameMode` | Native PVP fields and team arrays. |
| `ReleaseStandaloneSceneContracts` | Identity-safe reverse teardown. |

## Native types used by the framework

| Assembly/type | Use |
| --- | --- |
| `Assembly-CSharp/MissionLaptop` | Physical laptop owner. |
| `Assembly-CSharp/OperationsManager` | Shipped operation state, not direct package launch. |
| `Assembly-CSharp/CerebusOpboard` | Native board handoff. |
| `Assembly-CSharp/InfilSelectorDisplayer` | Infiltration map owner. |
| `Assembly-CSharp/InfiltrationManager` | PVE game-mode base. |
| `Assembly-CSharp/PvpGameode` | PVP game-mode base. |
| `Assembly-CSharp/PlayerMaster` | Shipped player creation. |
| `Assembly-CSharp/RaidManager` | Shipped AI creation. |
| `Mirror/NetworkIdentity` | Network identity and deterministic template IDs. |
| `Michsky.DreamOS/PanelButton` | Native Cerberus tab visual. |

## Verified dependency API

The implementation is intentionally small:

```csharp
public static T LoadVerifiedMapDependencyAsset<T>(string mapId, string assetPath)
    where T : UnityEngine.Object
```

It requires a current `loadedMapBundles[mapId]` entry. It searches only the
`Dependencies` collection in that entry. It does not search all Unity bundles.
It does not expose an unload method. A null result means that the map is not
registered, the path is invalid, or no declared dependency owns the asset.

This API is the correct cross-mod boundary for a map companion. The framework
performs package verification and lifetime control. The companion performs
map-specific interpretation.

## Compatibility rule

Do not use a method name alone as compatibility proof. Confirm the type,
assembly, signature, field layout, and call sequence in the exact installed
build. Generated IL2CPP wrappers can change without a source-level API change.
