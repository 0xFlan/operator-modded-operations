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

For Ukrainian Forest, the two declared bundles total `647869804` bytes. The
exact combined-package run measured `24.449 s` for the dependency bundle,
`0.833 s` for the scene bundle, and `25.347 s` through verified registration.
Confirm waited `23.442 s` for the remaining selected-map work. This is
expected verified content I/O. It is not evidence that a different scene
loaded.

## Brown proxy appears before the detailed map

Confirm that `ShowNativeLoadingScreenForPackageScene` called the shipped
`GameManagerNetwork.ShowLoadingScreen()` before runtime terrain/material
preparation. On the supported build, the show method is at RVA `0x00916210`
and the hide method is at RVA `0x0090E950`.

Log `LoadingScreen.activeSelf` and `activeInHierarchy`; both must be `true`
at this boundary. Do not use `LoadingScreenVisible` as the canvas probe. Its
getter at RVA `0x0091A840` returns `_hideLoadingScreenSoon` at offset `0x2A4`.

If the brown world remains after readiness, inspect live `TerrainData`, active
portable/error shaders, and the map companion world contract. That is a
different failure from a one-frame presentation gap.

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

## AI spawns but does not move

Confirm that the exact-scene companion created or reused one map-scoped
`AstarPath` and one enabled `Pathfinding.RVO.RVOSimulator` on the same host.
The shipped `BOT V2` uses `FollowerEntity`; a scanned graph without active RVO
can leave every bot stationary.

Measure motion from `BrainAI.agent.position`. Do not use
`BrainAI.transform.position`; that transform belongs to the stationary network
root while the `AgentController` and `FollowerEntity` move on the model child.

## Restart loops on a map-ready error

Check that teardown cleared the previous active generation before the new
scene callback. Check exact scene identity and companion readiness. Do not
accept a stale ready flag. Log the generation ID, scene handle, package ID,
map ID, and failing readiness gate.

## PVP offer is rejected before the scene loads

Compare the exact framework DLL, API Core DLL, API host DLL, game build,
package manifest and declared-file content identity, operation fields, and any
declared companion GUID/version/DLL SHA-256 on host and remote. Matching
displayed versions are insufficient when bytes differ. A malformed envelope,
message-ID collision, conflicting package load, or changed authenticated roster
also fails closed.

Do not bypass the agreement or fall back to position-only play. Install the
same explicitly labeled test build and map archive on both peers, close the
game while replacing files, then confirm every recorded hash again.

## PVP waits at scene readiness or aborts on Restart

Check the current nonzero host scene epoch and the remote's monotonic local
package-scene generation. `SceneReady(epoch)` is valid only for the exact
generation after its scene, spawn, deterministic PVP template, and companion
checks pass. A duplicate request can resend the current acknowledgement; a
zero, stale, future, out-of-phase, or overflowing epoch fails closed.

When `runtimeCompanion` is declared, require exactly one ready marker with the
declared name in the active generation scene. The declared failure marker wins
even after readiness. Also confirm at least `ceil(maximumPlayers / 2)` accepted
spawn markers per side; a 12-player operation needs six per team.

## PVP aborts when another player joins

This is the intended fail-closed behavior. The current PVP protocol freezes
the authenticated connection objects before agreement; late join is
unsupported. A join, disconnect, or replacement connection aborts even when a
numeric connection ID is reused. Start a new session with the intended roster.

## PVE works locally but differs for a remote player

PVE co-op does not use the PVP peer agreement. A passing PVP identity test does
not prove online PVE package/scene selection, enemy placement, movement,
projectiles, or damage. Treat online PVE as unsupported until its separate
host/remote matrix passes.
