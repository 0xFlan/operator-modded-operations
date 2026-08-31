# Complete operation lifecycle

## 1. Purpose

This document explains one complete modded operation. It starts when Core
finds a package. It ends when the player returns to the armory or restarts the
operation. The document uses exact source members from
`src/OperatorModdedOperations/CerberusNativeTabFix.cs`.

Read this document before you change launch, player, AI, PVP, failure,
restart, or teardown code. Do not infer a contract from a similar retail
method.

## 2. Required products and owners

The runtime has four separate owners.

| Owner | Exact responsibility |
| --- | --- |
| Operator Mod API Core | Find packages. Canonicalize paths. Validate schema, byte length, and SHA-256. Freeze an immutable catalog. |
| Modded Operations | Build the native laptop presentation. Load the selected package. Start the exact scene. Reconstruct declared terrain. Own player, PVE, PVP, failure-state, restart, and teardown adapters. |
| Map package | Supply the manifest, dependency bundles, scene bundle, preview image, optional external lighting data, and authored scene transforms. |
| Map companion | Repair only exact-map content that cannot be expressed as generic package data. It must not own Cerberus, package discovery, generic player flow, or generic AI flow. |

One object must have one lifetime owner. A map companion can use an object
that Modded Operations owns. It must not unload or destroy that object.

## 3. Package discovery and the frozen catalog

Core searches each direct child of:

```text
<OPERATOR_INSTALL>/OperatorMods/
```

A package root contains `operator-map-package.json`. Core validates every
declared file before it publishes the map. Validation includes:

1. The manifest matches the closed schema.
2. A relative path stays under the package root after canonicalization.
3. A file exists.
4. The file length equals `files[].bytes`.
5. The SHA-256 equals `files[].sha256`.
6. A dependency and a scene bundle have distinct roles.
7. Every operation refers to a map and spawn set that the same package owns.

The framework reads `OperatorApi.ModdedOperations`. It does not parse the
manifest again. This rule prevents the UI and loader from using different
package identities.

## 4. Laptop discovery and UI ownership

The framework creates one `NativePresentationBinding` for each live
`MissionLaptop`. It uses `MissionLaptop.osCanvas` and
`MissionLaptop.uiRaycaster`. It clones the shipped
`Michsky.DreamOS.PanelButton` and shipped operation-panel objects.

The framework does not use one global name lookup for all laptops. A host,
client, or later scene generation can have a different live laptop object.
The binding stores the exact owner.

The user flow is:

```text
Mission laptop
-> MODDED OPS tab
-> package operation row
-> native preparation page
-> Execute
-> native time and infiltration selector
-> Confirm
```

`CatalogPresentation` holds the selected operation, time code,
`CerebusOpboard`, target-package data, preview objects, and confirmation UI.

## 5. Manifest-to-native presentation mapping

`UpdateCatalogOperationBoard` maps package data to native data. The critical
code is:

```csharp
var target = new TARGETPACKAGE_DETAILS
{
    OPERATION_SCENE = map.ScenePath,
    DISPLAY_NAME = operation.DisplayName,
    INFILTRATION_TIME = timeCode
};

data.AffectGamemode = true;
data.GameModeOverride = operation.Mode ==
    ModdedOperationMode.PlayerVersusEnvironment
        ? OperationsManager.GameMode.PVE_HVTKILL
        : OperationsManager.GameMode.StandardPVP;
```

`OPERATION_SCENE` contains the exact streamed scene address. It does not
contain a donor map name. `GameModeOverride` tells the shipped operation
pipeline which persistent mode family to prepare.

The preview path is separate from the bundle paths. The data flow is:

```text
previewImage
-> Core length and SHA-256 validation
-> File.ReadAllBytes
-> ImageConversion.LoadImage
-> one cached Sprite
-> preparation view
-> fullscreen view
-> infiltration-map view
```

Schema version 1 gives one preview to one map. An end user does not select an
arbitrary image in the game. A package author replaces the image, recomputes
its byte length and SHA-256, increments the package version, and distributes
the new closed package.

`infiltrations[].mapPositionX` and `mapPositionY` are two-dimensional UI
coordinates. They do not move a player in the streamed scene.

## 6. Selected-map prefetch

`SelectCatalogOperation` calls `BeginSelectedMapPrefetch`. This method starts
I/O for only the selected map.

`BeginSelectedMapPrefetch` performs these checks:

1. The selected operation still resolves to a frozen catalog map.
2. A resident cache entry has the same `PackageContentId`.
3. An incomplete or stale entry is unloaded and removed.
4. An active same-map request is reused.
5. An active different-map request is allowed to finish. Unity does not give
   a safe cancellation operation for `AssetBundleCreateRequest`.

The request list is deterministic:

```csharp
foreach (string path in map.DependencyBundlePaths)
    pending.BundlePaths.Add(path);
pending.BundlePaths.Add(map.SceneBundlePath);
```

Dependency order is the order in the manifest. The scene bundle is last. Do
not put a streamed scene in a dependency bundle.

The reason that a large map can load slower than a retail map is physical
I/O. A cold Forest dependency bundle is approximately 630 MB. Retail bundles
can already be resident through the base game content system. Prefetch moves
that cold I/O to row-selection time. It does not make 630 MB free.

## 7. The Confirm transaction

`BeginCatalogOperationLaunch` is the only package Confirm entry point. It
first revalidates the operation and time against the frozen map. It then
captures the player-owned graph:

```csharp
var launchLaptop = ResolveLaunchLaptop(presentation.Laptop);
var launchPlayer = launchLaptop == null
    ? null
    : launchLaptop.playerNetworking;
```

This capture occurs before asynchronous I/O. The native modal can release its
`playerNetworking` reference while a bundle is loading. The first
implementation did not preserve this owner. The first Confirm then did
nothing. A second interaction created a new live graph and appeared to fix
the problem. The current implementation preserves `LaunchLaptop` and
`LaunchPlayer` in `PendingMapLaunch`.

If the selected-map prefetch is active, Confirm attaches to that request:

```csharp
pendingLaunch.Presentation = presentation;
pendingLaunch.Operation = operation;
pendingLaunch.TimeCode = presentation.SelectedTimeCode;
pendingLaunch.LaunchLaptop = launchLaptop;
pendingLaunch.LaunchPlayer = launchPlayer;
pendingLaunch.LaunchRequested = true;
```

The confirmation remains visible in a loading state. The user must not leave
the laptop and press Confirm again.

## 8. Asynchronous bundle loading

The main-thread runner calls `ProcessPendingLaunch`. It has at most one active
`AssetBundle.LoadFromFileAsync` request.

For each completed dependency bundle, the method requires zero streamed scene
paths. It stores the bundle in both an ordered list and a verified-path map.
The scene bundle is stored separately.

After the last request, `ValidateLoadedSceneBundle` requires:

- the declared `scenePath` is present;
- every scene in that bundle belongs to a frozen map with the same
  `PackageContentId` and scene-bundle path;
- no undeclared scene address is present.

On success, the loader stores `LoadedMapBundles` under the exact map ID. If
Confirm was waiting, it immediately calls `InvokeNativeCatalogLaunch`.

On any error, `FailPendingLaunch`:

1. clears the active request owner;
2. unloads the partial scene bundle with `Unload(false)`;
3. unloads each partial dependency bundle with `Unload(false)`;
4. writes a `failed closed` diagnostic;
5. restores the confirmation UI.

The loader does not start an operation with partial content.

## 9. Native board handoff

`InvokeNativeCatalogLaunch` creates `ActiveMapOperation`. It then calls
`InvokeNativeBoardStart`.

`InvokeNativeBoardStart` restores the captured laptop if the live board field
became null. It completes this owner graph:

```csharp
board.missionLaptop = launchLaptop;
manager.activeMissionLaptop = launchLaptop;
```

It closes and starts in one frame:

```csharp
CloseNativeMapConfirmation(presentation.Board, false);
PrimeNativeInfiltrationSelector(board, operation);
board.Start_Operation();
```

`PrimeNativeInfiltrationSelector` calls the shipped
`InfilSelectorDisplayer.SpawnMap`. It verifies the active package-map clone,
the marker count, `MarkerIndex`, `InfilName`, `MaxPlayers`, and ground-infil
flags.

Do not call these methods from the adapter:

```text
OperationsManager.StartOperation
OperationsManager.CMD_StartOperation
OperationsManager.DebugStartOperation
```

They are internal parts of a larger retail transaction. Direct calls bypass
required board, laptop, selector, and current-operation state. Current-build
tests also identified the combined direct-manager route as a crash boundary.

## 10. Exact scene acceptance

The shipped board starts the operation and loads the exact `scenePath`.
`OnSceneLoaded` ignores every other scene. For the expected scene,
`ValidateStandaloneSceneContract` requires these authored objects:

```text
MAP_ID_<mapId>
SPAWN_SET_<spawnSet>
one compatible player marker set
PVE_EnemySpawn_... markers for PVE support
PVP_Team1Spawn_... and PVP_Team2Spawn_... markers for PVP support
one package-owned directional-light route
```

The method also validates the scene name/path and operation ownership. A
marker with the correct spelling in the wrong loaded scene does not pass.
Marker discovery is mode-isolated: PVE excludes both PVP prefixes and every
Team 2 marker; PVP excludes `PVE_PlayerSpawn_`. A PVP scene must supply at
least `ceil(maximumPlayers / 2)` accepted markers for each side.

`OnSceneLoaded` first releases any previous generation contracts. It then
calls `ShowNativeLoadingScreenForPackageScene`. This method enters the shipped
`GameManagerNetwork.ShowLoadingScreen()` path at supported-build RVA
`0x00916210` before any terrain or material preparation. It activates the
shipped canvas, freezes the current player body, clears velocity, and closes
infiltration UI. The persistent manager owns `HideLoadingScreen` at RVA
`0x0090E950` after readiness.

The call closes the one-frame additive-scene gap before the replacement
`GameMode` can assert `OnAllPlayersLoaded(false)`. Without it, the camera can
show the portable brown proxy. The release diagnostic reads
`LoadingScreen.activeSelf` and `activeInHierarchy`. It does not use the
misnamed `LoadingScreenVisible` property; that getter returns the private
`_hideLoadingScreenSoon` byte on this build.

`OnSceneLoaded` then clears player attempts, mode objects, readiness flags, terrain references,
and PVE state. It schedules preparation for the next frame. This generation
reset prevents a second operation from inheriting the first operation.

## 11. Generic runtime terrain reconstruction

If the manifest contains `runtimeTerrain`, `TryPrepareRuntimeTerrain` performs
the reconstruction. The map companion does not own this generic step.

The method retrieves the exact dependency bundle by the Core-verified
canonical path. It finds exactly one `rootObject`. It loads:

- one readable height payload;
- one readable surface-weight payload;
- diffuse, normal, and mask textures for every declared layer.

The 16-bit height decode is:

```csharp
height = ((sample.r << 8) | sample.g) / 65535f;
```

The weight decode reads RGB, normalizes the three values, and uses layer zero
when the total is zero.

The method creates one `TerrainData`, sets all resolutions and size values,
creates each `TerrainLayer`, and binds the same object to both components:

```csharp
terrain.terrainData = data;
collider.terrainData = data;
```

It then disables
`NATIVE_Ground_HillyTerrain_RenderFallback`. Leaving that mesh active caused
the reported flat brown plane below the hills. It also gave raycasts and a
player camera two competing ground surfaces.

`ValidateWalkableGroundContract` requires every compatible player marker to
raycast to active, non-trigger collision. For a declared terrain, it also
requires identity equality between:

```text
Terrain.terrainData
TerrainCollider.terrainData
ActiveMapOperation.RuntimeTerrainData
```

Failure stops preparation. It does not spawn a player on a fallback plane.

## 12. Scene preparation order

`PrepareStandaloneScene` uses this order:

```text
TryPrepareRuntimeTerrain
-> Physics.SyncTransforms
-> ValidateWalkableGroundContract
-> ConfigureStandalonePlayerSpawnContract
-> CreateStandaloneGameplayBootstrap
-> ApplyStandaloneRenderContract
-> ScenePreparationComplete = true
-> NotifyPvpScenePrepared for PVP
```

Do not move player creation before terrain and collision are coherent. That
change can put the player under the map or leave the player root attached to
a temporary support surface.

## 13. Player spawn and respawn

`ConfigureStandalonePlayerSpawnContract` converts only current-scene marker
transforms into `SpawnPoint` components. It captures these process globals:

```text
GameManager.SpawnPointsInScene
GameManager.instance.Pspawns
GameManager.instance.PnextSpawnIndex
GameManager.instance.RandomSpawns
```

It assigns `PnextSpawnIndex = 0` and `RandomSpawns = false`. The current
native selector reads the initial array element before it advances the index.

For PVE, the compatible player markers are the selected north infiltration
set. For PVP:

```text
Team1_Spawn_... / Team1_Backup_Spawn_... / PVP_Team1Spawn_...
    -> SpawnPoint.Team = 1
Team2_Spawn_... / Team2_Backup_Spawn_... / PVP_Team2Spawn_...
    -> SpawnPoint.Team = 2
```

Team values are one-based. The framework reads
`PlayerMaster.MyTeamIdentifier.TeamID`.

The first owned player request calls:

```csharp
PlayerMaster.SpawnPlayer();
```

This route preserves the shipped `ClientSpawnBS`, camera, input, locomotion,
and networking setup. A repeat-host recovery can call the exact generated
`UserCode_CMDSpawnPlayer__NetworkIdentity` body after 300 frames, but only if
request 1 made no new player object. Per-player request and completion sets
prevent duplicate bodies.

The owned local player moves through `GameManager.MovePlayerToSpawn`. The
server moves a remote player root. A PVP round respawn remains a shipped
`PvpGameode` action. It uses the same team arrays and current-generation
markers.

## 14. Mirror game-mode owner and readiness

`CreateStandaloneGameplayBootstrap` creates one inactive template in the map
scene. The deterministic IDs are:

```csharp
private const uint StandalonePveGameModeAssetId = 0x4D4F5001;
private const uint StandalonePvpGameModeAssetId = 0x4D4F5002;
```

Every agreed peer can register the same nonzero ID. A zero-ID runtime
`NetworkIdentity` can work on a host client but gives a remote client no
prefab or scene ID to instantiate.

For PVP, the `0.3.32` performance/runtime-fix build uses the protocol-v6 fail-closed agreement. Before native board
start, the host snapshots the exact authenticated remote connection objects and
numeric IDs. Its private, collision-checked Mirror envelope carries a
per-launch nonce and digest over
loader-neutral framework/API Core/API host identities, the exact selected-loader
companion DLL and its loader-neutral runtime-pair identity,
protocol/API version/game build/capabilities, package ID/version/content hash,
companion GUID/version/marker contract, map/operation/mode/spawn set, scene variant/path,
time, and min/max players. A remote resolves only that exact identity through
its frozen local catalog, preloads the locally verified bundles when needed,
commits `ActiveMapOperation`, and then acknowledges content readiness. Every
frozen peer must acknowledge before the host enters the native transition.

After scene preparation, the host and every remote must have constructed and
registered `StandalonePvpGameMode` at `0x4D4F5002` and passed the declared
companion's unique exact-scene ready marker. A failure marker always wins and
remains monitored after readiness. Remote scene-ready
acknowledgements and unchanged membership gate the host's one
`NetworkServer.Spawn` call. The remote accepts the spawned owner only when its
asset ID, subtype, and exact agreement still match. Same-operation Restart
retains content agreement and repeats the scene/native barriers. Remote owner
adoption/readiness and host owner publication/all-players-loaded are bounded.
Mismatch, timeout, rejection, disconnect/join, handler collision, or a native
lifecycle exception cancels the session and tears down the exact generation.

The initial scene barrier uses host-issued epoch `1`. Each retained-content
Restart advances it exactly once. The remote maps
`SceneReadyRequest(epoch)` to its monotonic local package-scene generation and
acknowledges only after that exact generation passes scene, spawn, template,
and companion checks. Duplicate requests can resend the current
acknowledgement; zero, stale, future, out-of-phase, or overflowing epochs fail
closed. This covers host-first, remote-first, and load-before-unload ordering
without allowing readiness from an earlier scene generation to cross Restart.
Late join is unsupported. A join, disconnect, or replacement connection aborts
instead of changing the frozen roster, even when a numeric ID is reused.

Protocol v6 applies the content and scene-generation barriers to networked PVE.
After the exact injected owner is spawned, each remote must report owner
adoption and native readiness. The host then assigns each client-owned player
to an exact local marker; that owner invokes the shipped local
`GameManager.MovePlayerToSpawn` path and reports a stable grounded root,
controller, and camera. The host never writes a remote-owned player Transform.
Only after every placement receipt may the host call the one native PVE AI
spawn. It publishes the resulting server-owned AI netId/team/initial-pose
manifest, and every remote must validate that exact population before the host
commits gameplay. PVP uses the same session architecture but remains
deliberately fail-closed in the active BepInEx checkpoint pending physical
validation.

## 15. PVE creation and firearm ownership

The PVE owner is `StandalonePveGameMode : InfiltrationManager`.
`InfiltrationManager.instance` and `GameMode.singleton` point to this same
operation-owned component.

`TrySpawnStandalonePveEnemies` validates:

```text
1 <= minEnemies <= maxEnemies <= 100
minEnemies <= confirmed count <= maxEnemies
navigation-valid authored marker count >= confirmed count
NetworkServer.active == true
```

Marker active state is reported separately but is not an eligibility gate.
Existing companions intentionally keep utility spawn-marker objects inactive;
the native population path consumes their transforms directly. Every accepted
marker must still pass `AstarPath.IsPointOnNavmesh(marker.position)`.

It filters `GameManager.AllAITypes`. An accepted prefab has:

```text
BrainAI on the root
NetworkIdentity on the root
WeaponsAI.SpawnWeapon == true
WeaponsAI.weaponList.Count > 0
```

The framework creates one scene-owned `RaidManager`, adds or repairs
`BotSpawnDetails` on package markers, sets the host count, and calls:

```csharp
raid.ServerSpawnAI(false);
```

Current-build native inspection proved that this shipped body instantiates an
AI, calls
`NetworkServer.Spawn(bot, GameManager.instance.gameObject)`, and then applies
`BotSpawnDetails`. The rejected manual adapter reversed part of this order and
did not supply the owner. AI grenades worked because they were server-owned,
but firearm damage did not complete. The shipped raid method is therefore a
required contract.

The native briefing selector is host-owned and integer-valued. Confirm copies
the displayed count into the pending launch and then the active operation.
Restart retains it; a fresh operation selection starts from the bounded
midpoint default. A client does not choose another count.

## 16. PVP teams, rounds, death, and respawn

The PVP owner is `StandalonePvpGameMode : PvpGameode`. The framework supplies
separate, non-empty Team 1 and Team 2 arrays. It also supplies every field
that the shipped controller reads:

- two audio sources;
- 16 non-empty clip arrays;
- timer and score text;
- result roots and animators;
- fade-state names;
- outcome text;
- `MaxRounds = 13`;
- `RoundsToWin = 7`;
- `RoundTime = 120`.

It calls the shipped `OnStartClient` and `Server_AllPlayersLoaded` behavior.
The shipped controller keeps freeze, round start, score, death, team respawn,
and final operation-end ownership.

A single host cannot prove PVP. Use one host and one remote client. Verify
opposite authored sides, direct bullet damage, score, next-round respawn, and
final result.

## 17. Failure decision

The persistent `GameManagerNetwork` owns the retail Mission Failed UI.
`GameManagerNetwork.FailOperation` reads the active
`InfiltrationManager.instance` and its synchronized raid timer.

The generic retail `InfiltrationManager.Update` cannot run safely in a
standalone package scene. The framework advances only the required field after
readiness:

```csharp
if (NetworkServer.active && operation.AllPlayersLoaded && pveGameMode != null)
    pveGameMode.NetworkRaidTimer += Time.deltaTime;
```

This preserves the native failure consumer without running donor-scene
behavior. The framework does not clone Mission Failed UI and does not invent a
second failure screen.

## 18. Restart operation

The shipped restart control reloads the operation scene. The framework keeps
the verified dependency and scene bundles resident across this scene unload.
This avoids another 630 MB cold load for a simple restart.

`OnSceneUnloaded` releases the generation but does not discard the package
cache. When the scene loads again, `OnSceneLoaded` creates fresh terrain,
spawn, game-mode, player-attempt, render, and PVE state.

Restart has two separate acceptance gates:

1. Restart while the player is alive.
2. Restart from the Mission Failed screen.

The 2026-08-04 bounded runtime test accepts gate 2 for the current pinned
release. It used this exact sequence:

```text
normal Cerberus PVE launch
-> native Health lethal-damage command handler
-> native player dead state
-> GameManagerNetwork fail-operation RPC handler
-> MissionFailedPopup/RestartOperation
-> new UkrainianForest scene handle
-> one owned playable player
-> 14 active and grounded BrainAI instances
```

The largest absolute AI-to-Terrain height difference after restart was
0.03 m. This test used the real shipped failure popup. It did not clone or
synthesize a replacement control.

The old `MAP LOADED !BUG!` loop came from a stale Mirror registration. Unity
had destroyed the template object, so its IL2CPP wrapper compared as null.
`UnregisterPrefab(GameObject)` could not recover the ID. The current release
also removes the exact dictionary key and spawn handler:

```csharp
NetworkClient.prefabs.Remove(bootstrapAssetId);
NetworkClient.UnregisterSpawnHandler(bootstrapAssetId);
```

Do not call `NetworkClient.ClearSpawners()`. That operation would remove
registrations owned by the game and other mods.

## 19. Leave operation and armory return

Leaving the operation unloads the map scene and returns control to persistent
game objects. `ReleaseStandaloneSceneContracts` releases in this order:

```text
RestoreStandalonePlayerSpawnContract
-> ReleaseStandaloneRenderContract
-> ReleaseStandaloneGameMode
```

The detailed release rules are:

1. Restore spawn globals only if they still point to the operation-owned
   arrays or lists.
2. Restore NVG color only if this operation captured it.
3. Destroy only operation-created Volume profiles.
4. Clear `InfiltrationManager.instance`, `PvpGameode.instance`, and
   `GameMode.singleton` only if each still points to the owned component.
5. Remove only the deterministic Mirror ID for this operation.
6. Destroy only operation-created PVP assets and the scene-owned raid utility.
7. Release runtime `TerrainData` and created `TerrainLayer` objects.
8. Clear generation-specific player assignments and request sets.

Identity-conditional restoration prevents the armory or another mod from
losing newer state. Failure to restore the spawn globals caused a player to
return floating in the armory hallway in an earlier candidate.

The active map's verified bundle remains resident here because alive Restart
and KIA Restart reload the pinned scene from that exact owner. A later
different-map selection may overlap it only as the bounded
`pendingLaunch`/prefetch owner. Cross-map eviction requires all of the
following before `Unload(false)`:

1. Unity's active scene is exactly `Operation Room`;
2. the active package-scene handle is zero;
3. no loaded Unity scene matches any cached or in-flight scene bundle path;
4. the candidate is neither the active/restart map nor the pending map.

The first gate alone is unsafe because Operation Room stays active underneath
every additively loaded package scene. Fresh launch transfers
`activeOperation` to the selected map before trimming, so a prior map cannot
be unloaded out from under Restart.

## 20. Repeat-launch invariant

A second launch is not a continuation of generation one. It must satisfy all
of these conditions:

- no generation-one scene handle;
- no generation-one `TerrainData`;
- no generation-one `SpawnPoint` list;
- no generation-one player request or completion entry;
- no stale PVE or PVP singleton;
- no stale Mirror prefab key or spawn handler;
- no generation-one map-companion root or A* graph;
- a resident same-map bundle cache only when `PackageContentId` still matches;
- after different-map ownership transfer, no prior distinct completed bundle
  cache.

Use this log order as evidence:

```text
map scene unloaded; package bundles remain resident
-> exact package scene loaded again
-> runtime terrain reconstructed again
-> scene services ready again
-> game-mode identity spawned again
-> all players loaded again
```

## 21. Failure matrix

| Symptom | Failed contract | Required inspection |
| --- | --- | --- |
| First Confirm does nothing | Captured laptop/player owner was lost during I/O. | `BeginCatalogOperationLaunch`, `PendingMapLaunch`, loading-state log. |
| Confirm loops or needs a second interaction | Confirm did not join same-map prefetch or final handoff did not run. | `ProcessPendingLaunch`, `InvokeNativeCatalogLaunch`. |
| Brown proxy flashes before detail | Shipped loading canvas did not cover the additive-scene preparation gap. | `ShowNativeLoadingScreenForPackageScene`, `LoadingScreen.activeSelf`, `activeInHierarchy`. |
| Flat brown plane remains after readiness | Runtime terrain was not reconstructed or render fallback stayed active. | `TryPrepareRuntimeTerrain`, payload paths, shared TerrainData identity. |
| Player under terrain | Player creation ran before terrain/collider and marker raycast gates. | `PrepareStandaloneScene`, `ValidateWalkableGroundContract`. |
| AI outside wall | Scene marker set or A* coverage is wrong. | Exact PVE markers, wall bounds, graph-node test. |
| Grenades work, bullets do not | AI was not created through the shipped owner-aware raid route. | `TrySpawnStandalonePveEnemies`, `RaidManager.ServerSpawnAI(false)`. |
| All AI die but no exfil appears | The native raid list does not contain exactly the current zone, or AI did not use native Health death. | `ConfigureStandalonePveController`, post-`ServerSpawnAI` `raid.exfilZones`, `GameManager.allAI`. |
| ATAK has no exfil icon | `ExfilZone.ExfilMarker` does not match the current-build resource contract. | `CreateNativeAtakExfilMarker`, layer 17, `Marker`, `ExfilZone`, `HDRP/Unlit`, resident 512-by-512 texture. |
| Extraction timer does not start | Zone/global unlock or physical occupant state is incomplete. | `NetworkcanExtract`, zone/global occupant counts, `NetworkisExtracting`. |
| Teams use the same side | Team values or arrays are wrong. | one-based `TeamID`, Team 1 and Team 2 arrays. |
| PVP offer is rejected before load | One peer has different framework/API/package/companion bytes or operation identity. | Exact offer digest inputs and all DLL/package SHA-256 values on both peers. |
| PVP waits at scene readiness | The exact epoch generation, native template, spawn contract, or companion ready marker is missing; a failure marker may also be present. | Current host epoch, local scene generation, marker multiplicity, template asset ID, and companion failure state. |
| PVP aborts when a player connects | Frozen membership changed. Late join is unsupported. | Exact connection objects captured at offer time and join/disconnect log order. |
| `MAP LOADED !BUG!` repeats | Mirror key or handler survived scene unload. | `ReleaseStandaloneGameMode` ID removal. |
| Armory player floats | Process-global spawn state was not identity-conditionally restored. | `RestoreStandalonePlayerSpawnContract`. |

## 22. Minimum proof before release

Run these tests with physical input:

1. Cold first launch at 1100.
2. Warm first Confirm after selected-map prefetch.
3. PVE count between the manifest limits.
4. Reciprocal direct bullet damage.
5. Leave to armory and verify normal position and movement.
6. Launch the same operation again.
7. Restart while alive.
8. Restart from Mission Failed.
9. Start 0200 and verify white-phosphor NVG and readable ambient light.
10. Kill all native PVE AI. Verify native extraction unlock and the exact
    current-build ATAK exfil marker.
11. Enter the physical trigger. Verify the 15-second native timer, Mission
    Successful After Action Report, and Continue return.
12. Start PVP with a host and remote client whose exact candidate DLL and
    package hashes match. Verify content transfer/preload, synchronized first
    spawn and movement, opposite teams, firearm-specific hit registration and
    death, score, round respawn, retained-content Restart, and operation end.
13. Repeat PVP with a deliberate binary/package mismatch and a membership
    change. Verify both fail closed without spawning a partial native owner.

A compile proves syntax and type compatibility. A static bundle validator
proves a bounded artifact fact. Neither result proves player-camera behavior or
network transport. The frozen `0.3.29` candidate remains `PROVEN-STATIC` until
the host-plus-remote matrix passes.
