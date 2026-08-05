# Troubleshooting

## Confirm does nothing on the first attempt

Check for one `PendingMapLaunch`. Confirm must join the same selected-map
prefetch. Verify that `LaunchLaptop` and `LaunchPlayer` were captured before
the async request. Verify that the loading confirmation remains visible and
disabled. The final handoff must call `CerebusOpboard.Start_Operation()` once.

Do not add a second click handler. Do not call an `OperationsManager` launch
prefix as a substitute.

## Map takes much longer than a shipped map

Compare declared bundle bytes. A large custom dependency bundle can require a
large cold read and deserialization. Shipped scenes can already have shared
content in memory. Start prefetch on row selection. Keep the scene bundle
small. Move reusable assets to dependency bundles. Do not load all packages at
startup.

## Companion cannot read an asset

Call `LoadVerifiedMapDependencyAsset<T>(mapId, assetPath)` after bundle
registration. Check the exact lowercase bundle asset path. Check that the
asset is in a declared dependency bundle, not only in the Unity project.

Do not use `AssetBundle.GetAllLoadedAssetBundles()` as the ownership API. A
managed enumeration can miss or mis-handle IL2CPP wrappers, and it cannot prove
which package owns a bundle. Do not call `LoadFromFile` again for a large
bundle. That creates a second ownership and memory path.

## Player appears below the map

Check that runtime terrain is ready before player handoff. Check that the
marker is inside terrain bounds and that its sampled surface has a collider.
Check generation reset on repeat launch. Do not use a donor Office spawn or an
old scene transform.

## AI cannot shoot the player

Check that enemy markers and the player are inside the same playable walls and
navigation graph. Check reciprocal line of sight. Check `WeaponsAI` and its
weapon list. Use `RaidManager.ServerSpawnAI(false)`. Grenade damage alone does
not prove valid firearm AI.

## Restart loops on a map-ready error

Check that teardown cleared the previous active generation before the new
scene callback. Check exact scene identity and companion readiness. Do not
accept a stale ready flag. Log the generation ID, scene handle, package ID,
map ID, and failing readiness gate.
