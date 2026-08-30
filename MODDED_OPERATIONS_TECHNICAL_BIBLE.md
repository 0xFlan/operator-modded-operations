# OPERATOR: Modded Operations technical BIBLE

## 1. Purpose

This document is the technical authority for the public framework source in
this repository. It is written for a human maintainer and for an automated
coding agent. Use the exact names in this document. Do not infer a game
contract from a similar name.

The current authored source checkpoint is the `0.3.31` runtime hot-path and
one-shot placement build. The plugin identity is
`operator.modded-operations`, the selected assembly is
`OperatorModdedOperations.dll` or
`OperatorModdedOperations.MelonLoader.dll`, and the required bundled-only Core
version is `0.2.0-alpha.7`. Shared source builds isolated BepInEx and
MelonLoader products; install exactly one loader variant. Protocol v6 covers
PVP and online PVE with exact selected-suite receipt/sidecar verification,
loader-neutral runtime-pair identity, and a host-authoritative PVE enemy-count
identity. The exact selected-suite binaries are BepInEx 642,560 bytes /
`54890536492E645050C7C2125F7D1FF4FFC23C3BE23EBF95A2294E648439DEB7`
and MelonLoader 643,584 bytes /
`EBCAD6563366D614A12C2797622B7639A16377EAE829A4279A3914CFF498C635`.
The current BepInEx artifact has bounded single-machine runtime evidence;
MelonLoader and real peer networking remain `PROVEN-STATIC` until their live
gates pass.

The historical frozen `0.3.29` candidate binaries are
`OperatorModdedOperations.dll` at 279,552 bytes / SHA-256
`95CEF59F62B2DF40ED69C066692953210CDC17A9C3D08DB95753DA7A9B4142CD`,
`OperatorModAPI.dll` at 179,200 bytes / SHA-256
`0C27854DFDD3C9F0946F5BCBC61CE37DAE3037215BB5FC11C3400BD50190EB77`, and
`OperatorModAPI.BepInEx.dll` at 25,600 bytes / SHA-256
`A58E1FA50CE345931104B9980AFBAF356B8EEAC0E7A735BEF7BD21FC93727AD9`.
These historical identities are `PROVEN-STATIC`, not host-plus-remote runtime
evidence and not the current `0.3.31` candidate identity. The latest historical
hash-pinned public publication
remains the exact reviewed
`0.3.28` Release build at 223,232
bytes with SHA-256
`75BB479E863A94807ACCB78E6FA37271BE340B226E4EBDFB1BE48C0B8248852B`.
Its publication closure does not by itself promote runtime evidence. The last
complete runtime candidate remains `0.3.24`: 195,584 bytes with SHA-256
`0B61F0C3CCEC667B5FD38BAD7884C8F7349479F61AE3682F4DD4BB08C8243992`.

The latest decompiled source-checkpoint verification snapshot is
`decompiled/release-0.3.29`; it is historical `PROVEN-STATIC` evidence, not a
supported current hotfix binary release. The last public runtime-release snapshot is
`decompiled/release-0.3.28`. Every section that explicitly names a `0.3.27`,
`0.3.26`, or `0.3.22`
DLL, ZIP, decompiler output, Forest evidence, or render value remains an
immutable historical record; the current audit no longer treats those bytes
as the active publication identity.

## 2. Path tokens

| Token | Meaning |
| --- | --- |
| `<REPOSITORY_ROOT>` | This Git repository. |
| `<OPERATOR_INSTALL>` | The directory that contains `OPERATOR.exe`. |
| `<OPERATOR_MOD_API_REPOSITORY>` | A local Operator Mod API checkout. |
| `<PACKAGE_ROOT>` | One directory that contains `operator-map-package.json`. |
| `<MAP_COMPANION_ROOT>` | One map-owned selected-loader companion source/staging directory. |

Never put a user name or a private drive path in reusable documentation.

## 3. Product boundary

The complete system has four owners.

| Owner | Owns | Does not own |
| --- | --- | --- |
| Operator Mod API Core | package discovery, path checks, manifest checks, file hashes, immutable catalog | Unity scene behavior |
| Modded Operations | Cerberus UI, preview binding, selected-map I/O, exact scene load, mode owner, player flow, PVE population, native PVP adapter, restart, and named map-neutral HDRP/NVG transactions | a map material shader, terrain size, marker coordinate, or A* graph |
| Data-only map package | manifest, bundles, preview, scene, spawn-set names, operation data, render-profile selection, and verified LUT | executable hooks |
| Optional map companion | exact-package and exact-scene material, terrain, navigation, marker, and scene-light reconstruction | other maps, generic UI, or a competing global Volume |

The source enforces this boundary. Search
[`CerberusNativeTabFix.cs`](src/OperatorModdedOperations/CerberusNativeTabFix.cs)
for a map name. A release source must contain no map identity.

The transactional suite installer owns only the exact executable payloads,
manifest sidecar, and selected-loader receipt that it publishes. It does not
install, update, remove, or rewrite data-only packages under
`OPERATOR/OperatorMods`. Runtime agreement verifies the receipt-owned closure
before advertising an identity.

For each dual-loader product, the canonical `runtimeContentId` is the lowercase
SHA-256 of the UTF-8 lines
`operator-loader-neutral-runtime-pair-v1`, plugin GUID, plugin version,
BepInEx DLL SHA-256, and MelonLoader DLL SHA-256, in that order with `\n`
separators and no trailing newline. Generate it with
`tools/operator_runtime_content_id.py`; do not invent a token by hand.

## 4. Startup and catalog flow

The plugin attribute is the first closed gate:

```csharp
[BepInPlugin("operator.modded-operations", "OPERATOR: Modded Operations", "0.3.31")]
[BepInProcess("OPERATOR.exe")]
[BepInDependency("operator.modapi", CerberusNativeTabFix.RequiredApiVersion)]
```

This is the BepInEx host metadata; the isolated MelonLoader build publishes
equivalent product/process/dependency metadata without a BepInEx assembly
reference. At load, the framework registers the managed game-mode types through
Il2CppInterop. It attaches scene callbacks. It also creates one main-thread
runner. The runner discovers live `MissionLaptop` owners and builds private UI
state for each owner.

Core supplies `ModdedOperationsCatalog`. Core accepts packages before the UI
uses them. The framework does not parse an unverified package path as a second
catalog.

## 5. Cerberus mission-laptop flow

The physical laptop surface is `MissionLaptop.osCanvas` with
`MissionLaptop.uiRaycaster`. The framework clones a shipped
`Michsky.DreamOS.PanelButton`. It places the clone in the shipped
`Operation Selection` local `RectTransform` space. It keeps the shipped font,
sprites, animator, and state children.

The framework stores one `NativePresentationBinding` for each live laptop.
The binding contains the exact laptop, custom page, preparation panel, and
three tab buttons. The framework does not use one global object-name lookup to
serve all laptops.

The selected row creates `CatalogPresentation`. This object owns the selected
operation, selected time code, native `CerebusOpboard`, target package,
infiltration-map prefab, and briefing text.

Read [Cerberus UI and mission presentation](docs/architecture/cerberus-ui-and-presentation.md)
for the exact member map.

## 6. Manifest-to-UI mapping

| Manifest field | Native presentation consumer |
| --- | --- |
| `displayOrder` | Modded row order. |
| `displayName` | Row, briefing, target, and confirmation title. |
| `areaOfOperation` | Briefing area field. |
| `sitrep` | Situation report. |
| `timeCodes` | Time selector choices. |
| `defaultTimeCode` | Initial time choice. |
| `previewImage` | Preparation, fullscreen, and infiltration map image. |
| `infiltrations[]` | Native infiltration marker clones. |
| `spawnSet` | Three-dimensional scene spawn-set selection. |
| `minEnemies` and `maxEnemies` | Inclusive PVE host population range. |

The preview is a raw JPEG or PNG outside Unity bundles. Core verifies its
length and SHA-256. `GetOrLoadPreviewSprite` decodes the verified bytes with
`ImageConversion.LoadImage`. `ReplaceNativeMapPreview` binds one cached sprite
to all three native views.

Schema version 1 has one preview per map. An end user cannot select an
arbitrary local image. A package author changes the image, updates the
`files[]` byte count and SHA-256, changes the package version, and distributes
the new package identity.

## 7. Selected-map prefetch and Confirm

`BeginSelectedMapPrefetch` starts one cold-load request after row selection.
It loads declared dependency bundles in manifest order. It then loads the
scene bundle. It does not prefetch every map.

`BeginCatalogOperationLaunch` captures the exact `MissionLaptop` and
`PlayerNetworking` before asynchronous file work. `PendingMapLaunch` keeps
that owner identity. Confirm joins an active same-map prefetch. The framework
keeps the confirmation UI disabled and visible while bytes load.

After bundle and scene-path validation, the framework performs one final-frame
handoff:

```text
restore the captured laptop owner if the field was released
-> close the private loading confirmation
-> prime InfilSelectorDisplayer.SpawnMap
-> call CerebusOpboard.Start_Operation
```

Do not patch or call `OperationsManager.StartOperation`,
`CMD_StartOperation`, or `DebugStartOperation`. That combined prefix set is a
known current-build crash boundary.

### Completed bundle-cache lifetime in 0.3.25

`loadedMapBundles` is a verified ownership cache, not a process-lifetime
archive. `TrimCompletedMapBundleCacheAtSafeBoundary` keeps the selected/current
map, the exact `activeOperation` map required by alive or KIA Restart, and the
exact `pendingLaunch` map whose asynchronous prefetch is in flight. A fresh
different-map launch first transfers `activeOperation`; only then can the
previous completed map become an eviction candidate. Same-map Restart never
enters the trim path and reuses the resident bundle.

Do not treat `SceneManager.GetActiveScene().name == "Operation Room"` as
sufficient proof. The Operation Room remains Unity's active scene while a
package scene is loaded additively. The eviction boundary additionally
requires `activeOperation.SceneHandle == 0` and scans every cached and
in-flight scene bundle's `GetAllScenePaths()` inventory against all loaded
Unity scenes. Any match or unreadable inventory vetoes eviction. An accepted
candidate unloads its scene and dependency bundles with `Unload(false)` and
is then removed from `loadedMapBundles`.

The older incomplete/stale same-map discard path uses the same ownership
discipline. It refuses disposal when the entry belongs to `activeOperation`,
belongs to `pendingLaunch`, or owns a loaded scene. It cannot be used as a
shortcut around Restart protection.

The steady-state completed cache is LRU=1 after fresh launch ownership
transfer. A short cross-map transition may retain the active/restart owner
alongside the selected prefetch owner; no third speculative completed cache
is allowed to accumulate at a safe selection boundary. This logic is keyed
only by frozen map identity. It never branches on a scene-variant count or a
specific package/map ID.

### Manifest-driven scene variants in 0.3.24

The sole variant opt-in is `HasDeclaredSceneVariants(map)`, which is true only
when `SceneVariants.Count > 1`. Do not infer opt-in from a package ID, map ID,
bundle path, or companion. A single-scene map keeps its primary `ScenePath`;
the framework does not instantiate, read, or write
`SceneVariantSelectionStore`, consume variant RNG, or emit variant-selection
logs for that map.

For an opted-in map, `SceneVariantSelectionStore` owns a persistent shuffle
bag scoped to package ID plus map ID and preserves exact variant ID-to-path
identity. `TrySelectFreshLaunchScene` advances it only for a fresh Operation
Room Confirm. The exact choice is copied into pending and active operation
state. Alive Restart and KIA Restart reload that active scene without
advancing the bag. Persisted history prevents an immediate repeat across
OPERATOR process restarts.

On an asynchronous cold launch, first validate the dependency and scene
bundles, then select. An already verified cached launch can select immediately
from the same closed manifest inventory. When maps share one content identity
and scene-bundle path and any participating map declares variants, require the
bundle scene addresses to equal the exact union of the declared inventories.
Reject every missing, extra, subset, superset, or unrelated address.

This ownership is generic framework behavior. A map companion must not choose
once at plug-in startup, mutate the map definition's `ScenePath`, or
Harmony-patch `ValidateLoadedSceneBundle`.

The exact single-process observer under
`<AUTHOR_WORKSPACE>/reports/observer_qa/forest_20260810_020127` passed this
sequence:

```text
fresh Confirm -> KH10_WideLabyrinth
alive Restart -> KH10_WideLabyrinth with a new scene handle
native completion -> Mission Successful -> Operation Room
fresh Confirm -> KH03_SerpentineApartment
native KIA -> Mission Failed -> Restart Operation -> KH03_SerpentineApartment
```

Both fresh generations and both restarts became playable with grounded native
PVE AI. The second fresh selection differed from the first; neither restart
rerolled. This is `PROVEN-RUNTIME` for the pinned single-player lifecycle.
Multiplayer selection agreement remains a separate gate.

## 8. Exact scene and readiness

The framework loads the exact effective scene path from the accepted scene
bundle: the primary `ScenePath` for a single-scene map or the framework-owned
active selection for an opted-in map. It does not load Office, Georgia, or a
donor mission. The scene contract checks the expected package, map, effective
scene path, map marker, current-scene spawn sets, terrain declaration, and
map-companion readiness when required.

The companion must finish its strict world contract before the framework
creates PVE actors. Do not replace this barrier with an arbitrary frame delay.

At the exact additive-scene boundary,
`ShowNativeLoadingScreenForPackageScene` calls the shipped
`GameManagerNetwork.ShowLoadingScreen()` before terrain or material
preparation. The supported native method is at RVA `0x00916210`. Vanilla
`GameManagerNetwork.OnAllPlayersLoaded(false)` uses the same route. The method
activates the shipped canvas, freezes the current player body, clears
velocity, and closes infiltration UI. The native hide method is at RVA
`0x0090E950`; the persistent manager keeps ownership of that transition.

Use `LoadingScreen.activeSelf` and `activeInHierarchy` as the diagnostic.
`LoadingScreenVisible` is not the canvas state on this build. Its getter at
RVA `0x0091A840` returns `_hideLoadingScreenSoon` at offset `0x2A4`.

The framework remains the only owner of loaded package bundles. A companion
can borrow an asset from a verified dependency bundle with this public API:

```csharp
TextAsset state = CerberusNativeTabFix
    .LoadVerifiedMapDependencyAsset<TextAsset>(
        "community.example.example-map",
        "assets/example/material-state.raw.yaml");
```

The API searches only the dependency bundles that Core verified and Modded
Operations retained for the exact `mapId`. The caller must not unload the
bundle or destroy the borrowed asset. This method prevents a companion from
loading a second copy of a large dependency bundle.

## 9. Player spawn ownership

`ConfigureStandalonePlayerSpawnContract` converts only the selected scene's
verified marker transforms into current-scene `SpawnPoint` objects. The
framework captures these process-global values before mutation:

- `GameManager.SpawnPointsInScene`;
- `GameManager.instance.Pspawns`;
- `GameManager.instance.PnextSpawnIndex`.

The operation assigns the list and array to the same marker set. The initial
index is zero because the current native selector reads before it increments.

An owned `PlayerMaster` uses `PlayerMaster.SpawnPlayer()` on request 1. This
route keeps shipped `ClientSpawnBS` camera, input, and locomotion setup. A
repeat-generation owned host can use the exact generated server body once,
after a 300-frame grace period, only if request 1 produced no new player
object. Per-player attempt and completion sets stop duplicate bodies.

## 10. PVE owner and armed AI

The PVE runtime owner is `StandalonePveGameMode : InfiltrationManager`.
`InfiltrationManager.instance` and `GameMode.singleton` point to the same
operation-owned component. The framework advances its synchronized
`RaidTimer` after the shipped all-players-loaded barrier.

The framework filters `GameManager.AllAITypes`. An accepted AI prefab has:

- root `BrainAI`;
- root `NetworkIdentity`;
- `WeaponsAI.SpawnWeapon == true`;
- at least one weapon in `weaponList`.

Before spawning PVE, the framework groups firearm-capable prefabs by their
complete native `TeamIdentifier`/`StartingTeamStats` contract, excludes the
live player's team, and requires one unique strict-largest hostile cohort.
Only that cohort is supplied to `RaidManager.standardAI` for the synchronous
native spawn call; the prior array is restored afterward. New operation brains
are owned by the exact `NetworkServer.spawned` netId/identity/root-`BrainAI`
delta. Native `BrainAI.Start` joins `GameManager.allAI` after the synchronous
spawn call returns, so the framework keeps extraction locked and validates
the full selected-team/reference/pool/target graph from the next Unity frame
through a bounded 60-frame deadline. A missing, extra, vanished, or mismatched
owned entry fails closed and destroys only that exact population. The
framework does not mutate global friendly fire or hand-edit native AI,
enemy, or target lists.

`ChooseStandalonePveEnemyCount` validates
`1 <= minEnemies <= maxEnemies <= 100`. The native briefing owns an
integer enemy-count slider inside that range. Confirm captures the displayed
value into the pending launch and the active operation, so native restart uses
the same count. The loaded scene must expose at least that many enemy markers
on the live navigation graph before the one-shot native population call is
allowed. Utility markers may remain inactive because the native raid consumes
their transforms directly; activation is telemetry, not eligibility.

Current-build native inspection also proves that
`RaidManager.GetValidSpawnPoint` removes a candidate before testing its A*
node and `CanSpawn(position, 1f)`. The framework therefore supplies a stable,
name-ordered subset whose accepted markers are at least 2m apart in the X/Z
plane. `safeCapacity` is the size of that navigation-valid, pairwise-spaced
set, not the raw authored or active count. Earlier releases selected one
inclusive, deterministic host count. `TrySpawnStandalonePveEnemies` passes
the valid prefabs and the safe package markers to a scene-owned `RaidManager`.
It calls `RaidManager.ServerSpawnAI(false)`. Do not use a one-argument manual
`NetworkServer.Spawn` as an AI replacement.

### Native completion, extraction, and ATAK

Every standalone StandardPVE scene must contain exactly one inactive
`PVE_ExfilZone_` marker with a positive trigger `BoxCollider`. The map owns
that transform and collider. `ConfigureStandalonePveController` copies them to
the Mirror-owned PVE bootstrap and adds one shipped `ExfilZone` plus one
shipped `RaidManager`.

The operation starts with zone and global extraction locked. Normal AI
`Health` deaths drain the native `GameManager.allAI` population. The shipped
raid unlocks its one current zone. A living player must physically occupy the
zone for the shipped 15-second `GameManagerNetwork` countdown. Completion uses
the shipped Mission Successful After Action Report and Continue route.

`CreateNativeAtakExfilMarker` reconstructs the current-build `level16` marker:
layer `17`, four-vertex `Marker` mesh, `HDRP/Unlit` material named
`ExfilZone`, resident `512 x 512` `ExfilZone` texture, render queue `2501`,
and the audited transform/UV data. `ExfilZone.ExfilMarker` activates it only
after native unlock.

The full source, serialized-resource identities, code, state sequence,
teardown rules, and evidence are in
[Native PVE completion, extraction, and ATAK](docs/architecture/native-pve-completion-exfil-and-atak.md).

## 11. PVP owner

The PVP runtime owner is `StandalonePvpGameMode : PvpGameode`. The current
native team IDs are one-based:

```text
Team 1 -> SpawnPoint.Team = 1
Team 2 -> SpawnPoint.Team = 2
```

The framework reads `PlayerMaster.MyTeamIdentifier.TeamID`. It does not parse
`ToString()`. It supplies separate non-empty `Team1SpawnPoints` and
`Team2SpawnPoints`. It seeds `MaxRounds=13`, `RoundsToWin=7`, and
`RoundTime=120`. The retail server can replace lobby-owned values.

The framework supplies the two audio sources, 16 non-empty clip arrays,
timer/score text, result roots, animators, fade state names, and outcome text
that shipped PVP methods read. It calls the shipped `OnStartClient` and
`Server_AllPlayersLoaded` bodies. It keeps shipped round, freeze, score,
death, respawn, and operation-end logic.

The `0.3.31` source owns a multi-stage protocol-v6 peer agreement for PVP and
online PVE. Before the native board starts, the host freezes the exact
authenticated remote connection objects and IDs. Every peer validates its
selected-loader suite receipt, exact receipt-owned manifest sidecar and file
records, and the loaded framework, API Core, API host, and declared companion
paths. The digest then binds the loader-neutral suite/runtime-pair identity,
API version, game build/capabilities, complete package content,
map/operation/mode/spawn set, scene variant/path, time, and min/max players.
PVE also binds its declared range and host-confirmed enemy count. A remote
preloads its exact verified package and commits the matching operation before
sending `ContentReady`; matching version text is insufficient when any byte
differs. After transition, every peer creates and registers the native mode
template and passes the declared companion's unique exact-scene ready marker
before sending `SceneReady`; a failure marker wins even after readiness. PVE
scene readiness also validates the agreed count against the navigation-valid,
pairwise-spaced ordinary marker capacity.
The host issues a nonzero scene-generation epoch before initial transition and
increments it exactly once per retained-content Restart. `SceneReady(epoch)`
is accepted only for the current session, exact connection object, current
host phase, and current epoch. The remote maps each request to its monotonic
local package-scene generation, so remote-first and host-first replacement
loads cannot reuse the preceding generation's readiness. Requests retry within
the existing bounded deadline; duplicates resend the current acknowledgement;
zero, stale, out-of-phase, and overflowing epochs fail closed.
Unchanged membership and the full scene-ready set gate the host's one
`NetworkServer.Spawn` call. The remote adopts only the exact deterministic mode
asset and subtype while the agreement is live.

Marker discovery is mode-isolated. PVP never consumes `PVE_PlayerSpawn_`, and
each side must expose at least `ceil(maximumPlayers / 2)` accepted markers.
The pinned maximum declaration of 12 therefore requires at least six accepted
markers for Team 1 and six for Team 2; fewer markers fail before launch.
PVE never consumes PVP-prefixed or Team 2 markers. Same-operation Restart
retains content agreement and
repeats scene and native-lifecycle readiness. Remote owner adoption/readiness
and host owner publication/all-players-loaded are bounded. Mismatch,
malformed/trailing envelope, private message-ID collision, membership change,
rejection, timeout, scene failure, or native lifecycle exception fails closed
and triggers the shipped host return or remote disconnect before teardown.
Membership is immutable for the session. Late join is unsupported, and a
disconnect, new connection, or replacement connection aborts even if it reuses
the same numeric connection ID. The protocol-v6 PVE path remains
`PROVEN-STATIC`: remote AI equivalence, movement, combat, completion,
extraction, Restart on both peers, and teardown remain unproven online.

Implementation-test note: the first focused run after replacing the
unversioned readiness latch compiled cleanly but reported four test failures.
All four asserted retired field names or the old method that directly sent
`SceneReady`; none exposed a compiler or lifecycle defect. The assertions were
updated to the epoch helpers, ordering/overflow coverage expanded from 28 to
39 focused cases, and the final focused and complete suites passed. Keep this
distinction so assertion drift is not misreported as a native runtime result.

## 12. Mirror template identity

The framework uses deterministic nonzero IDs:

```csharp
private const uint StandalonePveGameModeAssetId = 0x4D4F5001;
private const uint StandalonePvpGameModeAssetId = 0x4D4F5002;
```

`EnsureStandaloneBootstrapPrefabRegistered` checks
`NetworkClient.prefabs`. It rejects a different live object at the same ID.
It removes only a fake-null entry that belongs to the same ID. It also removes
that ID's spawn handler before registration.

On release, the framework removes only the operation-owned prefab key and
spawn handler. It does this by asset ID even when Unity destroyed the template
wrapper. Never use `NetworkClient.ClearSpawners()` for this repair.

### Injected NetworkBehaviour constructor boundary in 0.3.25

The exact `0.3.24` lifecycle log recorded four caught first-attempt spawn
exceptions, one on each fresh/alive-restart/fresh/KIA-restart generation. In
every case the top native frame was
`Mirror.NetworkBehaviour.ClearAllDirtyBits`; the root
`StandalonePveGameMode` reported `syncObjects=null`, while the native
`ExfilZone` and `RaidManager` components reported non-null empty lists. The
failed first entry delayed readiness and exposed the shipped transient
`Map Loaded... !BUG! Click Restart Operation` fallback before a later bounded
recovery.

The earlier accepted `0.3.21` generation at
`<AUTHOR_WORKSPACE>/reports/observer_qa/forest_20260805_190946` is the direct
working comparison: `StandalonePveGameMode`, `ExfilZone`, and the then-current
raid subtype all reported `syncObjects=0`; the first native spawn emitted no
`ClearAllDirtyBits` exception and readiness entered through `OnStartClient`.
This comparison isolates the list baseline from map content and restart
selection.

Current-build Cecil inspection proves that `NetworkBehaviour.syncObjects` is
an assignable
`Il2CppSystem.Collections.Generic.List<Mirror.SyncObject>`. Its managed
parameterless wrapper allocates a native object and invokes Mirror's native
constructor; its `IntPtr` wrapper only attaches to an existing native object.
ClassInjector creates `StandalonePveGameMode` and `StandalonePvpGameMode`
through that `IntPtr` route, so their base native constructor never supplies
the normally non-null empty list. This is the confirmed cause; the scene,
variant selector, and package identity are not involved.

`0.3.25` initializes only a null list on operation-owned root behaviours,
tracks that ownership, and validates every root behaviour before both
`NetworkClient.RegisterPrefab` and `NetworkServer.Spawn`. The framework
records one spawn attempt before entering native code and never retries a
partially entered spawn. Teardown unspawns first, unregisters the exact
deterministic ID second, and destroys the root/list ownership last. It never
restores null and never clears another owner's spawners. Five regression gates,
the complete 14-test Python suite, the nine-test selector suite, and a
zero-warning Release build pass. This correction is `PROVEN-STATIC` until a
fresh exact lifecycle run proves zero `ClearAllDirtyBits` warnings, no
`Map Loaded... !BUG!` fallback, and unchanged restart pinning.

## 13. Time, NVG, and process-global state

The manifest selects one named framework render profile. Ukrainian Forest uses
`native-outdoor-v1`. `ApplyStandaloneRenderContract` implements this profile.
The map companion must not create a second global Volume or overwrite the
framework Volume after this call.

For the exact `0200` choice, the framework calls
`GameManager.instance.SetNVGColor(0)`. The pinned build maps zero to the
shipped white-phosphor mode. It applies this exact night state:

```csharp
sun.colorTemperature = 9754f;
sun.intensity = 40f;
sun.bounceIntensity = 1f;
bloom.intensity.Override(0.3f);
lensFlare.intensity.Override(1f);
tonemapping.mode.Override(TonemappingMode.ACES);
```

The operation-owned night ambient object supplies low-level world radiance so
the four GPNVG tubes can resolve terrain and foliage outside the brighter
ECOTI circle. The 2026-08-04 repeat run accepted white phosphor across all
four tubes and visible world detail outside that circle.

For day choices, `0.3.22` copies the audited
`level11/PVP Woods Warehouse` donor instead of the rejected generic
`level6/PVP map` donor:

```csharp
sun.colorTemperature = 5500f;
sun.intensity = 30000f;
sun.bounceIntensity = 5f;
bloom.intensity.Override(0.03f);
lensFlare.intensity.Override(0.5f);
color.saturation.Override(-15f);
whiteBalance.temperature.Override(-3.6f);
whiteBalance.tint.Override(-8.6f);
liftGammaGain.lift.Override(new Vector4(1f, 1f, 1f, 0.00827304f));
liftGammaGain.gamma.Override(new Vector4(1f, 1f, 1f, -0.09100296f));
liftGammaGain.gain.Override(new Vector4(1f, 1f, 1f, 0.09100296f));
```

The day branch also uses external tonemapping with the package-verified
`AgX_Powerful_RGBAHalf_32.bytes` LUT. The framework logs the source profile,
sun lux, temperature, bounce, bloom, lens flare, night-ambient state,
white-phosphor state, and LUT state after it applies the live profile. A log
with 52,241.375 lux, 6,727 K, or bloom 0.359 identifies the archived rejected
day profile.

The framework creates one operation-owned HDRP Volume and one owned profile.
It captures the previous NVG value and Volume ownership.

The framework restores only values that it still owns. This rule prevents one
operation from overwriting state installed later by another owner.

## 14. Restart and teardown

`ReleaseStandaloneSceneContracts` and `ReleaseStandaloneGameMode` release the
generation in reverse order. The framework:

1. stops stale asynchronous completions;
2. destroys the exact operation-owned PVE identity set before game-mode
   release;
3. removes the operation-owned Mirror keys;
4. clears mode singletons only when they still point at the owned component;
5. restores player spawn globals only when identity still matches;
6. restores NVG state and destroys the owned Volume/profile;
7. releases scene-lifetime state while retaining the exact verified bundle
   required by same-map Restart;
8. clears cached player attempts, assignments, and generation handles.

The persistent shipped `GameManagerNetwork` owns Mission Failed UI and its
Restart Operation control. The framework must not clone that UI. The PVE
`InfiltrationManager` and timer supply the state that native failure code
expects.

The 2026-08-04 bounded KIA test proves this path at runtime. The driver used
the normal Cerberus launch, applied lethal head damage through the current
native `Health` command handler, observed the dead state, and invoked the
current `GameManagerNetwork` fail-operation RPC handler. It found and invoked
the real `MissionFailedPopup/RestartOperation` control. The replacement scene
had a new Forest scene handle, one owned playable player, and 14 active
`BrainAI` instances. All 14 AI instances were grounded. The largest absolute
AI-to-Terrain height difference was 0.03 m. This evidence is
`PROVEN-RUNTIME` for same-process KIA restart. It is not evidence for
reciprocal firearm damage or a remote PVP peer.

## 15. Release layout

The current Git source checkpoint is the `0.3.31` runtime hot-path and
one-shot placement build. Its selected-suite binaries and receipt are frozen
to the identities in section 1. The separately labeled transfer archive is
test-only and does not establish online support. The historical `0.3.29`
BepInEx DLL is 279,552 bytes with SHA-256
`95CEF59F62B2DF40ED69C066692953210CDC17A9C3D08DB95753DA7A9B4142CD`.
The last runtime-release publication source is `0.3.28`; its exact reviewed DLL
is 223,232 bytes with SHA-256
`75BB479E863A94807ACCB78E6FA37271BE340B226E4EBDFB1BE48C0B8248852B`.
The last complete runtime candidate remains `0.3.24`: 195,584 bytes with
SHA-256
`0B61F0C3CCEC667B5FD38BAD7884C8F7349479F61AE3682F4DD4BB08C8243992`.
The Git repository preserves the `0.3.29` compiler surface as the hash-pinned
`decompiled/release-0.3.29` source-checkpoint snapshot and preserves the final
`0.3.28` runtime-release compiler surface at
`decompiled/release-0.3.28`. The prior
`decompiled/release-0.3.27`, `decompiled/release-0.3.26`, and
`decompiled/release-0.3.22` trees plus the `0.3.20`, `0.3.19`, `0.3.18`, and
rejected `0.3.17` snapshots remain historical comparison evidence. The
release ZIP contains the compiled DLL, not the bracketed repository
placeholders. Use
[the package placeholder](packaging/README-PACKAGE-PLACEHOLDER.md) as the exact
install-root-relative staging contract.

The current public release-layout record is pinned to a Modded Operations
`0.3.28` archive contract with Operator Mod API `0.2.0-alpha.5` Core (174,592
bytes,
`43445DC37FE85196EFFF0744233847223D87B8BA68EC5BD79B12585F2A764DC3`)
and BepInEx host (25,600 bytes,
`BC9D4ABCDB62D37E045FDEEAB80098788891B690965F2367CDC17EAC308978EF`)
plus the framework, each in its own
plugin folder, and contains no map data. Runtime ownership remains separate;
download ownership does not. The preview API is not published as a standalone
archive. A map archive contains only package data and its map companion and
must not duplicate Core or framework files. A separate public Operator Mod API
release is deferred until the API reaches a full stable version.

The frozen `0.3.29` / alpha.6 set has one separately labeled transfer archive
for controlled multiplayer testing:

```text
OperatorModdedOperations_v0.3.29_API-alpha.6_MULTIPLAYER_TEST_ONLY.zip
bytes=486369
sha256=4507C858888339B19F318F7D23B55F771B53E102CDA2B335F95B52BDF91FC1B8
```

It contains the candidate framework plus the bundled preview API; it is not a
standalone API release or Nexus binary publication. Its existence is packaging
evidence only. The Git source-checkpoint decompiler is public, while the
runtime-release publication source-state remains `0.3.28`. The future promoted framework
archive will continue to bundle the preview API, while each map stays a
separate download and must not duplicate the framework or API.

The historical `0.3.22` `OperatorModdedOperations.dll` is 173,568 bytes with SHA-256
`0B8BE9B55C36AFCA81BAB677C5D0720D89A3E2B0E5F25A60BD2FF81C4192349A`.
Its archived drag-and-drop framework ZIP is
`OperatorModdedOperationsFramework_v0.3.22.zip`: 1,005,267 bytes, SHA-256
`4A173D50EABCFEEE3F87D63000D92D31D96CFB3FD11D28713ED01043D13A74A8`.
It passed a full 7-Zip integrity test. All 54 entries in
`CHECKSUMS_MODDED_OPERATIONS.sha256` match their staged files.

The historical `eng/audit_repository.py` record verifies the `0.3.29` authored
and source-checkpoint decompiler seams plus the immutable `0.3.28` runtime-release
publication record. It binds exact DLL and
decompiler identities, explicit placeholders, privacy rules, Markdown/JSON
integrity, and the deterministic
`publication/source-state-manifest.json`. The adjacent sidecar pins the exact
manifest bytes. The historical `0.3.22` archive record above is not relabeled
or treated as current evidence.

Never ship QA flags, force-scene code, test controls, private logs, copied game
DLLs, or extracted game assets.

## 16. Verification matrix

| Gate | Required proof |
| --- | --- |
| UI | Physical click on `MODDED OPS`, row, Back, Execute, Cancel, and Confirm. |
| First Confirm | One physical Confirm starts the scene. No second laptop interaction. |
| Preview | Same verified image in preparation, fullscreen, and infiltration views. |
| PVE | Package-certified count range through the global hard cap of 100, navigation-valid ordinary markers at least 2 m apart after snapping, armed AI, reciprocal bullet damage, all-AI-dead unlock, ATAK marker, physical 15-second extraction, native success screen, and spawn/frame-time/Restart/teardown performance at the claimed maximum. |
| Peer agreement | Exact selected-loader receipt/sidecar/files and complete package identity, declared companion runtime-pair identity/readiness/failure precedence, remote verified preload and operation commit, every content-ready ACK before native launch, host-issued scene epoch plus exact-generation scene-ready ACK before spawn, unchanged connection-object membership, explicit late-join rejection, restart-ordering races, and closed mismatch/timeout/overflow tests. PVE also binds the host-confirmed count. |
| PVP gameplay | Host and remote client on different authored sides; synchronized first spawn and movement; firearm-specific hit registration and death; score, round respawn, Restart, and return. |
| PVE online | Two distinct processes start together, both leave loading and remain grounded, AI identity/placement/movement/health replicate, real projectiles and damage work, completion/extraction pass, Restart replaces both scenes, and failure/return/close tear down cleanly. Protocol-v4 static agreement does not satisfy this gate. |
| Player | Player object, camera, input, movement, correct terrain spawn, repeat launch. |
| Restart | Alive restart and KIA end-screen restart as separate gates. |
| Scene variants | `SceneVariants.Count > 1` opt-in, single-scene bypass, different fresh selections, and the exact active scene retained across alive and KIA Restart. |
| Teardown | Armory return and a second operation generation without stale state. |
| Deployment | Source, stage, archive, and installed hashes match while game was closed. |

## 17. Evidence labels

- `OBSERVED`: one raw event or artifact exists.
- `PROVEN-STATIC`: source, binary, bundle, or validator proves a bounded fact.
- `PROVEN-RUNTIME`: a controlled runtime test proves a bounded fact.
- `SUPPORTED`: all stated user-facing gates for that capability pass.
- `REJECTED-LIVE`: the player camera or physical flow rejected the candidate.

Do not promote a compile, bundle parse, forced scene, or offscreen render to
`SUPPORTED`.

## 18. Detailed references

- [Ownership and load order](docs/architecture/ownership-and-load-order.md)
- [Cerberus UI and mission presentation](docs/architecture/cerberus-ui-and-presentation.md)
- [Launch, game modes, restart](docs/architecture/launch-game-modes-and-restart.md)
- [Build and install](docs/guides/build-install-and-test.md)
- [Create a map package](docs/guides/create-a-map-package.md)
- [Exact source and native contract](docs/reference/source-and-native-contract.md)
- [Manifest and preview reference](docs/reference/manifest-and-preview.md)
- [Troubleshooting](docs/reference/troubleshooting.md)
- [Complete operation lifecycle](docs/architecture/full-operation-lifecycle.md)
- [Source publication and native inspection](docs/reference/source-publication-and-native-inspection.md)
- [Decompiled release snapshot](decompiled/README.md)
- [Drag-and-drop package placeholders](packaging/README-PACKAGE-PLACEHOLDER.md)
- [Current evidence status](docs/evidence/current-status.md)

## 19. Vanilla-equivalence research rule

Before the framework or a companion changes a native-facing contract, inspect
how the exact installed game build uses the same type or asset. Record:

1. source file or serialized asset file;
2. path ID or addressable name when applicable;
3. assembly and exact IL2CPP type;
4. method signature and generated wrapper/body distinction;
5. field values before the first call;
6. owner object and lifetime;
7. call order and scene state;
8. network owner and peer requirements;
9. teardown behavior;
10. player-camera or physical-input result.

For managed native wrappers, use the installed files under
`<OPERATOR_INSTALL>/BepInEx/interop` as the current-build type and signature
evidence. For serialized content, inspect the exact `sharedassets*.assets`,
scene shared assets, bundle, or `globalgamemanagers.assets` record that owns
the object. A similar type name is not proof.

This rule produced three important corrections in the current implementation:

- `PlayerMaster.SpawnPlayer()` remains request 1 because it preserves the
  shipped camera, input, and `ClientSpawnBS` path. The generated server body is
  a bounded owned-host recovery, not the default path.
- `RaidManager.ServerSpawnAI(false)` remains the PVE creator because its native
  body performs owner-aware `NetworkServer.Spawn(bot,
  GameManager.instance.gameObject)` before it applies `BotSpawnDetails`.
- `PvpGameode` remains the PVP owner because its shipped methods consume team
  arrays, presentation objects, timers, scores, death, respawn, and round
  state. A generic `GameMode` replacement is incomplete.

## 20. Exact author-to-runtime data flow

The author supplies one closed data chain:

```text
operator-map-package.json
  -> Core canonical path, size, and SHA-256 validation
  -> dependencyBundles[] load in declared order
  -> sceneBundle load
  -> exact scene-address validation, including the declared union for variants
  -> fresh variant selection only when SceneVariants.Count > 1
  -> effective scene-path equality check
  -> MAP_ID_<mapId> and SPAWN_SET_<spawnSet> scene checks
  -> optional map-companion reconstruction and ready result
  -> package-owned player marker registration
  -> native PVE or PVP owner creation
  -> physical player handoff
```

The UI chain is separate from the world chain:

```text
previewImage verified bytes
  -> ImageConversion.LoadImage
  -> cached Sprite
  -> preparation Image
  -> fullscreen Image
  -> InfilSelectorDisplayer map Image

infiltrations[].mapPositionX/Y
  -> two-dimensional marker clone

named scene Transform
  -> three-dimensional player or AI position
```

Do not use a two-dimensional infiltration coordinate as a world spawn. Do not
use a world marker name as proof that the correct image marker exists.

## 21. Exact package-to-code members

The following source members are the implementation entry points for package
authors and maintainers:

| Data or event | Exact source member |
| --- | --- |
| Catalog row selected | `SelectCatalogOperation` and `BuildCatalogPresentation` |
| Verified preview decode/cache | `GetOrLoadPreviewSprite` |
| Three native preview surfaces | `ReplaceNativeMapPreview` |
| Package infiltration prefab | `BuildPackageInfiltrationMapPrefab` |
| Selected-map prefetch | `BeginSelectedMapPrefetch` |
| Physical Confirm request | `BeginCatalogOperationLaunch` |
| Variant opt-in | `HasDeclaredSceneVariants` |
| Fresh variant selection | `TrySelectFreshLaunchScene` and `SceneVariantSelectionStore` |
| Asynchronous launch state | `PendingMapLaunch` and `ProcessPendingLaunch` |
| Final board handoff | `InvokeNativeCatalogLaunch` |
| Exact map/scene/spawn-set gate | `ValidateStandaloneSceneContract` |
| Runtime terrain declaration | `TryReconstructDeclaredRuntimeTerrain` |
| Player marker registration | `ConfigureStandalonePlayerSpawnContract` |
| Native PVE population | `TrySpawnStandalonePveEnemies` |
| Native PVP owner | `ConfigureStandalonePvpController` |
| Verified dependency asset loan | `LoadVerifiedMapDependencyAsset<T>` |
| Reverse release | `ReleaseStandaloneSceneContracts` and `ReleaseStandaloneGameMode` |

Use these exact member names when you cite or review the implementation. Line
numbers can change. A member name plus repository commit identifies the code
more reliably than a stale line number.

## 22. Map-author completion checklist

A map repository is not complete until it contains:

- a schema-valid manifest example;
- exact dependency and scene bundle names;
- the exact streamed scene path;
- all scene marker naming rules;
- all model, submesh, material, texture, and shader closure rules;
- terrain serialization or reconstruction instructions;
- collision and bullet-barrier ownership;
- navigation construction and marker-on-node checks;
- PVE count and armed-AI requirements;
- PVP one-based team requirements;
- preview replacement and hash instructions;
- time/NVG/lighting ownership;
- first launch, repeat launch, restart, armory return, and multiplayer gates;
- build, validation, package, install, and uninstall commands;
- PII-safe path tokens;
- source and release hashes for the accepted candidate;
- explicit `PROVEN-STATIC`, `PROVEN-RUNTIME`, `SUPPORTED`, and
  `REJECTED-LIVE` labels.

The exhaustive construction procedure is
[Create a map package](docs/guides/create-a-map-package.md). The public map
guide and each map-specific repository must add their own exact asset and
companion details; this framework BIBLE does not invent map data.

## 23. Schema-v2 fixed PVE AI profile

Modded Operations `0.3.28` and Operator Mod API `0.2.0-alpha.5` introduced the
optional `pveAiProfile` object; `0.3.29` / `0.2.0-alpha.6` retain it unchanged.
Only a schema-v2 PVE operation can own it. PVP rejects it. Schema v1 rejects
it. There is no difficulty UI and no process-global AI write.

The closed fields are `id`, `detectionRangeMeters`, `fieldOfViewDegrees`,
`maximumEffectiveRangeMeters`, `wanderDistanceMeters`, optional
`initialWanderDelayMaxSeconds`, optional `reactionDisposition`, optional
`maximumReactionTimeSeconds`, `useComms`, and `counterSuppression`. Operator
Mod API validates and freezes them. Framework
member `ConfigureStandaloneBotDetails` writes the selected values to each
native `BotSpawnDetails` before `RaidManager.ServerSpawnAI(false)`.

The current native `RaidManager.ApplyBotSpawnSettings` transfers detection
range to `BrainAI` and `EyesAI`, FOV to `BrainAI`, communications,
counter-suppression, effective range unless the marker value is `-1`, and
wander distance unless the marker value is `-1`. It does not consume marker
`DetectionTimeMultiplier` or `HearingRange` in the pinned build.

`BrainAI.Wander(float)` normally preserves the native prefab delay. It waits for
`WanderTimer * Patience`, then chooses around the current position with the
equivalent of `RandomNavSphere(position, 5, WanderDistance)`. Package authors
must therefore tune wander from playable geometry and spawn gaps. Repeated
native choices can expand a search; a larger radius does not remove the first
delay. When `initialWanderDelayMaxSeconds` is omitted, the framework returns
before writing any AI state and this complete native delay remains unchanged.
When present, `TryApplyProfiledPveInitialWanderDelayCap` advances only each
new operation-owned, non-responding Wander bot's first `wanderTime` on the
server. A stable FNV-1a stagger leaves 50–100% of the declared cap. It never
writes `WanderTimer`, `Patience`, or `ReactionTime`; after the first native
destination resets `wanderTime`, all later cycles use the full shipped delay.

When declared, `reactionDisposition` maps the exact lowercase values
`defensive`, `offensive`, and `random` to the native marker `reactType` before
spawn. `maximumReactionTimeSeconds` accepts 0.10 through 1.50 seconds. After
native `BrainAI.Awake`, the authoritative server caps `_baseReactionTime` and
`ReactionTime` independently with `min(native, declared)`, preserving zero and
every faster prefab value. Omission performs no corresponding write; no
reaction timer, difficulty, target, combat state, or existing bot is changed.

Foliage sight remains map content. The map companion must inspect how the
same installed vanilla prefab participates in the shipped `EyesAI` linecast.
It can activate an authored `AI_VisionBlock` collider when that is the native
prefab contract. The generic framework must not invent map-specific bush
names, counts, colliders, or coordinates.

The full schema bounds, source members, native offsets/RVAs, exact application
order, foliage collision rules, logging, and gates are in
[Fixed PVE AI profile and vegetation sight](docs/architecture/pve-ai-profile-and-forest-sight.md).

`CaptureOwnedStandalonePveServerPopulation` records the exact new
`NetworkServer.spawned` netIds, identity references, and root `BrainAI`
instance IDs created by the native population call. After deferred startup
validation binds those same roots in `GameManager.allAI`,
`StartProfiledPveAiDiagnostics` tracks only that exact owned set. The
diagnostic gate is `operation.Operation.PveAiProfile != null`. There is no map
ID in the framework gate.

The diagnostic first reads the live spawned bots' `WanderTimer * Patience`,
`DetectionRange`, `EyesFOVAngle`, `WanderDistance`, and `useComms`. It then
calls `LogProfiledPveAiSnapshot` at 0, 10, 30, 60, 90, and 120 seconds. Each
snapshot reports horizontal movement from spawn, movement toward the captured
insertion position, `CurrentSeenTarget`, `CurrentState`, and a read-only
bot-eye-to-player linecast with that bot's `EyesAI.DetectionLayerMask`.

The linecast groups layer-18 first hits as vegetation. It groups other first
hits separately. A clear or player hit is a third group. This diagnostic does
not set a destination, target, vision field, weapon field, or AI state. Its
linecast is geometry evidence, not acquisition proof. Physical camera behavior
and reciprocal firearm damage remain required.

The first launch and same-process native restart movement baseline is
`PROVEN-RUNTIME` for the tested Forest scope. The Forest `0.4.17` and Modded
Operations `0.3.20` observer created 15 and 14 native
bots. At 120 seconds, all 15 and all 14 bots had moved at least 1 m. Six and
four bots had moved at least 5 m toward insertion. Maximum displacement was
`51.19 m` and `49.34 m`. The native delay ranges were `9.31..36.78 s` and
`10.97..33.02 s`. Both generations recorded vegetation first-hit evidence.
The repository verifier accepted both six-snapshot windows. The current
Forest `0.4.19` and Modded Operations `0.3.22` single-player scope separately
passed reciprocal firearm play and the native completion/extraction flow.
Two-peer combat remains a separate gate.

## 24. Private stationary observer QA

An unattended observer can validate Modded Operations without steering the
playable character. It must remain a separate private BepInEx plug-in. It is
not framework behavior and it must never enter the Nexus archive.

The observer uses the actual product path:

```text
Lone Wolf
-> player-owned MissionLaptop.AccessLaptop
-> MissionLaptop.CerebusWindow.OpenWindow
-> MODDED_OPS_NATIVE_TAB
-> exact MODDED_NATIVE_ROW_<index>
-> private OperationBoardUI Execute
-> confirmation Confirm
-> shipped InfilSelectorDisplayer selection and Confirm
-> exact additive package scene
-> native readiness and owned player camera
-> GameManagerNetwork.RestartOperation
```

The private driver must select an exact immutable operation ID from the frozen
catalog. It must not select only the first operation with a compatible mode.
It must refuse an ID/mode mismatch. A configurable observation time must have
a strict upper bound. Use at least 122 seconds when the test must include the
0, 10, 30, 60, 90, and 120 second AI snapshots.

The player remains stationary. The observer requires the real owned
`PlayerMaster`, `PlayerSpawnedObject`, package spawn, declared Cinemachine
camera, and retail output camera. A free camera or forced scene is not
equivalent evidence.

The launcher must refuse to attach to an existing OPERATOR process. It records
the executable path, process ID, and start time for every process created by
the controlled launch. On a timeout, it uses a graceful close first and acts
only on an exact recorded process. It collects and hashes the BepInEx logs,
driver trace, initial capture, and restart capture. It then removes its driver,
control files, and capture directory.

Do not use this method to bypass the physical first-Confirm gate. The private
driver invokes the live UI events, but a human physical-pointer pass remains a
separate release gate when the UI interaction surface itself changed.

## 25. Loader-neutral framework evidence

Framework lifecycle evidence uses one stable, single-line schema under both
supported hosts:

```text
MODDED_OPS_EVIDENCE|schema=1|event=<event>|loader=<loader>|operation=<id>|map=<id>|sceneHandle=<handle>|sceneGeneration=<generation>|<event payload>
```

Identity fields are URI-escaped and absent values are `none`. Payload keys are
event-specific, concise, ordered, and restricted to one line. Schema and scene
numbers use invariant formatting. Emission is a best-effort, exception-isolated
observer: formatter or log-sink failure cannot change launch, population,
transport, teardown, or loader-unload behavior. The marker stream covers PVE briefing
count pending/active/restart retention; safe navigation capacity; synchronous
`ServerSpawnAI(false)` return and deferred validation; native all-enemies-dead,
extraction, timer, result, and Operation Room return observations; scene
teardown; successful framework unload; and PVP transport registration/release,
scene epochs, and final session close.

Scene lifecycle and completion observations are edge-latched per exact scene
generation. They read native state but do not authorize spawns, extraction,
success, return, or PVP readiness. Restart markers retain the prior exact scene
handle in either callback order. PVP epoch-issued markers identify the target
scene generation and use a target handle when the replacement load is already
observable. Every started evidence generation produces one post-cleanup
teardown summary with a completed/exception outcome and remaining-ownership
counts. Static tests and loader-specific compilation prove the instrumentation
contract. Only captured game logs can promote any individual event to runtime
evidence.
