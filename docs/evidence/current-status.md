# Current evidence status

## Active candidate: 0.3.32 steady-state performance and native weapon-effects build

Modded Operations `0.3.32` and bundled-only Operator Mod API
`0.2.0-alpha.7` build isolated BepInEx and MelonLoader products from shared
source. Install exactly one loader. Protocol v6 binds the selected suite
receipt and sidecar, game build, loader-neutral framework/API/companion
identity, complete package content, operation, variant, scene generation, and
host-confirmed PVE enemy count before gameplay commit.

The placement path is generation-scoped and one-shot. It never writes a
remote non-owned player transform. Each owner receives one placement request,
holds a bounded stability window, and acknowledges its exact assignment. The
host freezes connection objects, player netIds, assignment digest, and PVE AI
population receipts. Once the player and population barriers commit,
`GameplayBeginCommitted` disables further placement work. PVP hands lifecycle,
movement, combat, damage, death, score, and respawn to the shipped
`PvpGameode`; the framework does not replace Mirror transforms, bullets,
damage, health, or weapon commands.

Current frozen runtime identities:

```text
OperatorModdedOperations.dll              644096  57CE5F1657CABDC5B1785013CF95D22913027EAEE0D8A08000CC31AD1DFB7D91
OperatorModdedOperations.MelonLoader.dll  645632  FA679A7AC2F9BE3543022B88BDBF7AF8756F8D7C544A656E61F15F3E3EC73CF3
OperatorModAPI.dll                        204800  5B74AC25B4047D9AB8E9929D136C53B7543DA52B621FAF3D4AF42719D276E21E
OperatorModAPI.BepInEx.dll                 15872  6223553C5406AD3586F23EA2B5F05C6F4626FA62A03E0598667E09A5486E1E3B
OperatorModAPI.MelonLoader.dll             26112  79A8491D2497C2A72859C6B05DD6BA8E475327FA54DB47C0C5F3881F59E6CF6A
```

The full source suite passes `110/110`; scene variants pass `16/16`; native
game-mode, policy, runtime-barrier, loader-boundary, binary-contract, installer
round-trip, repository, and package-closure gates pass. Both loader builds
compile with zero warnings and errors. A clean rebuild at later Git HEAD
differs from the frozen DLL only in the PE reproducibility stamp, MVID, and
embedded source revision. Release and installed artifacts remain pinned to
the runtime-tested bytes above.

Current BepInEx single-machine evidence includes sustained LOT 12 PVE,
restart, PVP selector, Forest control, and a fresh 2026-08-30 LOT 12 PVE run.
The fresh run passed `18/18`: the private `10..60` selector chose and retained
10 across Restart, all 72 authored/navigation markers were present, safe
capacity was 71, exactly 10 grounded server-owned AI were observed in both
generations, owned AI were removed, the framework scene cleaned to zero
runtime assets, and the package closure remained unchanged. A separate
current-byte sustained run sampled 4,579 frames, kept all 10 AI grounded and
network-ready, and observed eight move at least one metre without repeat
player placement. Its average was about 38.2 FPS with 28.79 ms p95 frame time
on the local test machine. The lifecycle result is
`<AUTHOR_WORKSPACE>/reports/runtime_qa/20260831_062356-BepInEx-pve-1dc7d9e2-94e1-4ab9-990f-dc950c1e6cde/suite-run-result-v1.json` and the sustained result is
`<AUTHOR_WORKSPACE>/reports/runtime_qa/20260831_062900-BepInEx-pve-sustained-7d2c4196-e16f-4410-ade1-ffcf09dacd0a/suite-run-result-v1.json`.

This is not an online-support claim. The current MelonLoader products pass
build and static/archive checks, but fresh live gameplay remains open because
the test installation still booted through BepInEx's native shim when the
managed suite was switched. A Melon run requires MelonLoader's native shim to
be active and BepInEx's native shim to be disabled. Real
PVE and PVP acceptance each requires a host and remote client on separate PCs.
PVE must prove identical authoritative AI identities, poses, movement, health,
combat, completion, extraction, Restart, and teardown. PVP must prove teams,
movement, firearm hits, damage/death, score, round respawn, Restart, and clean
return. Late join is intentionally unsupported by the fixed-roster protocol.

## Historical source candidate: 0.3.29

The authored plugin identity is Modded Operations `0.3.29`; it requires the
bundled-only Operator Mod API `0.2.0-alpha.6` preview. Its standalone PVP path
now freezes the authenticated host/remote membership and exchanges an exact
framework/API Core/API host binary SHA-256 identity, game build/capability,
package ID/version/content hash, declared runtime-companion identity and hash,
map/operation/mode/spawn-set, variant/scene, time, and min/max-player identity.
The content identity covers the manifest and every declared package file; a
same-version local edit therefore cannot satisfy exact agreement.
Every remote must preload and verify that exact local package, commit the
remote operation, and acknowledge content readiness before native launch.
Every peer must then construct and register the exact native PVP template and,
when declared, observe the companion's unique exact-scene ready marker before
acknowledging the host-issued nonzero scene-generation epoch. Restart advances
the epoch once and binds the acknowledgement to the corresponding monotonic
local package-scene generation, covering host-first, remote-first, and
load-before-unload callback ordering without reusing stale readiness. A
failure marker wins even after readiness.
Remote owner adoption/readiness and host all-players-loaded are independently
bounded; any native lifecycle exception aborts instead of using a position-only
fallback.

Player-marker discovery is isolated by mode. PVP requires at least
`ceil(maximumPlayers / 2)` markers on each team; a 12-player declaration
therefore requires six per side without a map-specific or fixed-capacity
branch. PVE does not consume PVP markers and does not enter the peer-agreement
path. The PVP membership snapshot also binds the connection object, not only
its numeric ID. Late join is unsupported: a join, disconnect, or replacement
during the agreement aborts instead of changing the frozen roster. Because PVE
co-op bypasses this protocol, no current result proves remote PVE package/scene
equivalence, AI placement, movement, bullets, or damage.

The complete 78-test Python suite, including 39 focused PVP
agreement/restart tests, the separate 9-test C# scene-variant selector suite,
and a Release build with zero warnings and
zero errors pass. These results prove source shape and compiler compatibility,
including the direct IL2CPP Mirror handler/writer/connection seam. They do not
prove transport behavior between two OPERATOR processes. The exact
host-plus-remote content barrier, scene barrier, first spawn, movement
replication, firearm-specific hit registration, opposite teams, score, round
respawn, restart, and return lifecycle remains open. The candidate is therefore
`PROVEN-STATIC`, not `SUPPORTED`.

A separately labeled archive exists to transfer the exact framework/API set
for a controlled multiplayer matrix:

```text
OperatorModdedOperations_v0.3.29_API-alpha.6_MULTIPLAYER_TEST_ONLY.zip
bytes=486369
sha256=4507C858888339B19F318F7D23B55F771B53E102CDA2B335F95B52BDF91FC1B8
```

That archive is test packaging only. It is not a Nexus binary release, does
not establish installation or runtime behavior on another machine, and does
not close any host-plus-remote gate. The exact `0.3.29` decompiled tree is now
published as a Git source checkpoint. The runtime-release publication record
immediately below remains immutable `0.3.28` with bundled Operator Mod API
`0.2.0-alpha.5`.

The final non-packaging static build identities for independent review are:

```text
OperatorModdedOperations.dll
bytes=279552
sha256=95CEF59F62B2DF40ED69C066692953210CDC17A9C3D08DB95753DA7A9B4142CD

OperatorModAPI.dll
bytes=179200
sha256=0C27854DFDD3C9F0946F5BCBC61CE37DAE3037215BB5FC11C3400BD50190EB77

OperatorModAPI.BepInEx.dll
bytes=25600
sha256=A58E1FA50CE345931104B9980AFBAF356B8EEAC0E7A735BEF7BD21FC93727AD9
```

## Prior hash-pinned publication source: 0.3.28

The `0.3.28` publication targets Modded Operations `0.3.28` and keeps bundled
Operator Mod API at `0.2.0-alpha.5`. It retains strict native hostile-team
cohort selection, then captures the exact Mirror netId/identity/BrainAI delta
created by `RaidManager.ServerSpawnAI(false)`. Because native `BrainAI.Start`
joins `GameManager.allAI` after the synchronous spawn call returns, full
team/reference/pool/list validation now begins on the next Unity frame and is
bounded by a 60-frame deadline. Pending or failed validation keeps both
zone-level and global extraction state locked. Failure, restart, and unload
destroy only the exact owned identity set before game-mode release.

The Release build completed with zero warnings and zero errors, and the
complete Python suite passes 39 of 39 tests. Its exact identity is:

```text
OperatorModdedOperations.dll
bytes=223232
sha256=75BB479E863A94807ACCB78E6FA37271BE340B226E4EBDFB1BE48C0B8248852B
```

The exact authored `CerberusNativeTabFix.cs` is SHA-256
`3A33AC1C1B97CA31E77F5198EE77BBCD24EFE3CD2DE9284758540AC94E9F4302`.
The final independent source/lifecycle review found no unresolved publication
blocker. ILSpy `10.1.1.8388` produced the seven-file
`decompiled/release-0.3.28` tree: 427,039 bytes with tree SHA-256
`59D672462C7AA4DA00268D3A2185EA79D2C43F078853F71E40EAE40FDEE361C8`.
The deterministic publication manifest and adjacent SHA-256 sidecar bind the
complete public source state to those exact compiler/decompiler artifacts.

This is publication and static-test evidence, not packaging, deployment, or
runtime-acceptance evidence. No archive was created and OPERATOR was not
launched for this promotion.

## Prior hash-pinned publication source: 0.3.27

The `0.3.27` publication and bundled Operator Mod API `0.2.0-alpha.5` added
strict hostile-team cohort selection and post-spawn team/target-graph
validation, plus opt-in native reaction disposition and maximum-reaction-time
controls. Its exact privacy-clean Release DLL is 216,064 bytes with SHA-256
`CD2B326E76FC95439352E999BB138EB4499B08895C5BE552807D2C8A7FF62579`.

The optional reaction fields are fail-closed and map-scoped. Omission preserves
the selected native prefab behavior; declared values use native response
disposition and cap only `_baseReactionTime` and `ReactionTime`. Final live
team, target-graph, reaction, and combat acceptance for these exact bytes
remains a separate QA gate.

That historical DLL has a hash-pinned ILSpy `10.1.1.8388` tree at
`decompiled/release-0.3.27`. The deterministic
publication record for `0.3.28` supersedes it as current identity. The
`0.3.26` binary and snapshot remain historical evidence and are not relabeled.

## Prior static candidate: 0.3.25

The exact `0.3.25` Mirror/bootstrap and bundle-cache candidate remains
200,704 bytes with SHA-256
`E5354D32336C10FAB4A86EFA5CAC073604B075B58B0FE9761FC291DD6148E016`.
Its corrections are retained by `0.3.28`; its hash is historical evidence,
not the identity of the current source.

## Last complete runtime candidate: 0.3.24

The exact runtime-tested `0.3.24` DLL is 195,584 bytes with SHA-256
`0B61F0C3CCEC667B5FD38BAD7884C8F7349479F61AE3682F4DD4BB08C8243992`.

Scene variants are manifest-driven and strictly opt-in. Only a map with
`SceneVariants.Count > 1` enters the selector. A single-scene map keeps its
primary `ScenePath` and does not construct, read, or write variant state,
consume variant RNG, or emit variant-selection logs. An opted-in map uses one
persistent shuffle bag scoped to package ID plus map ID. A fresh Operation
Room Confirm advances that bag; alive Restart and KIA Restart retain the exact
active selection. The persisted state also prevents an immediate repeat after
an OPERATOR process restart.

The exact single-process lifecycle under
`<AUTHOR_WORKSPACE>/reports/observer_qa/forest_20260810_020127` reports
`Outcome=passed` and proves:

1. A fresh Confirm selected `KH10_WideLabyrinth`; one player and 12 of 12
   grounded PVE AI became playable.
2. Alive Restart reloaded `KH10_WideLabyrinth` with a new scene handle; one
   player and 11 of 11 grounded PVE AI became playable.
3. The shipped Standard-PVE path reached zero native AI, unlocked the native
   extraction zone and ATAK marker, completed the physical 15-second timer,
   showed Mission Successful, and returned through Continue to the Operation
   Room.
4. A second fresh Confirm selected the different
   `KH03_SerpentineApartment`; one player and 9 of 9 grounded PVE AI became
   playable.
5. Native lethal damage showed the shipped Mission Failed popup. Its Restart
   Operation control reloaded `KH03_SerpentineApartment`, kept the selection,
   and restored a playable operation.

The final trace reports `freshBdiffersFromA=True`,
`sameSceneOnKiaRestart=true`, and `playableAfterKiaRestart=true`. The trace
SHA-256 is
`348ADF149CF569D1C9F38805FFD3E229F7D474C856D01F61375A32381781A51A`.
The machine-readable result SHA-256 is
`95BC5FE668D72A252E22DC00942B1D885C69CB0E18EC8ADBFBBF7155C280792A`.
This is `PROVEN-RUNTIME` for the pinned single-player variant lifecycle.
Multiplayer replication remains a separate gate.

## Mirror bootstrap clean-spawn correction

The exact later `0.3.24` lifecycle log at
`<AUTHOR_WORKSPACE>/reports/observer_qa/forest_20260810_071941` retained the
correct selected scene across alive and KIA Restart and completed successfully,
but it also exposed one caught first-attempt Mirror exception on each of its
four operation generations. Every exception entered
`Mirror.NetworkBehaviour.ClearAllDirtyBits`. The runtime audit immediately
before spawn reported:

```text
StandalonePveGameMode:syncObjects=null
ExfilZone:syncObjects=0
RaidManager:syncObjects=0
```

Current-build Cecil inspection confirms that the injected subtype's `IntPtr`
wrapper constructor does not invoke `Mirror.NetworkBehaviour`'s native
parameterless constructor. `syncObjects` is an assignable
`Il2CppSystem.Collections.Generic.List<Mirror.SyncObject>`, and the exact
native baseline is a non-null empty list. This constructor boundary, not the
map or scene-variant selector, caused the `ClearAllDirtyBits` null dereference
and delayed readiness at the shipped transient `Map Loaded... !BUG!` text.
The accepted `0.3.21` comparison run at
`<AUTHOR_WORKSPACE>/reports/observer_qa/forest_20260805_190946` reported all
three root lists at count zero, spawned on the first native attempt without
that exception, and entered readiness through `OnStartClient`.

`0.3.25` supplies only that missing baseline on operation-owned runtime root
behaviours, hard-gates registration and spawn, records a single attempt before
native re-entry, and performs ordered unspawn/unregister/root destruction.
Five source-level regression tests prevent null restoration, retry, global
spawner clearing, and teardown reordering. The correction is
`PROVEN-STATIC`; a new controlled lifecycle must still prove zero warnings,
zero `!BUG!` fallback, and unchanged restart pinning before runtime promotion.

## Cross-map bundle-cache correction

Earlier source retained every distinct verified map in `loadedMapBundles`
until plug-in unload. `0.3.25` now trims prior completed maps only at a proven
safe Operation Room boundary. The check requires a zero active package-scene
handle and compares every cached and in-flight scene bundle inventory against
all loaded Unity scenes; an unreadable inventory or any match vetoes unload.

The retained owner set is map-neutral: selected/current map, exact
`activeOperation` map for same-scene alive or KIA Restart, and exact
`pendingLaunch` map during asynchronous prefetch. Fresh different-map native
launch transfers `activeOperation` before the trim, returning the completed
cache to LRU=1 with `Unload(false)`. Six static integration gates cover the
boundary, loaded-scene veto, same-map reuse, different-map ownership
transfer, pending-owner preservation, and single-scene/variant isolation.
This correction is `PROVEN-STATIC`; it has not been deployed or observed in
the game.

## Archived 0.3.22 publication state

The following exact artifact record is the preserved `0.3.22` public
publication snapshot. It is historical release evidence, not the identity of
the current hash-pinned `0.3.28` publication source.

The exact release artifacts are:

```text
OperatorModdedOperations.dll
bytes=173568
sha256=0B8BE9B55C36AFCA81BAB677C5D0720D89A3E2B0E5F25A60BD2FF81C4192349A
```

`OperatorModdedOperationsFramework_v0.3.22.zip` is `1005267` bytes. Its
SHA-256 is
`4A173D50EABCFEEE3F87D63000D92D31D96CFB3FD11D28713ED01043D13A74A8`.
The production DLL is `173568` bytes.

The ZIP passed a complete 7-Zip integrity test. Every entry in
`CHECKSUMS_MODDED_OPERATIONS.sha256` matches its staged file. The stage has no
QA driver, forced-scene control, log, PDB, or map package.

The Release build completed with zero warnings and zero errors. The profiled
PVE verifier unit suite passed all three tests.

`eng/audit_repository.py` binds the current `0.3.28` authored source,
`decompiled/release-0.3.28`, exact DLL/decompiler identities, bundled alpha.5
API pins, and deterministic source-state manifest. The `0.3.22` DLL, ZIP, and
decompiler tree above remain historical evidence and are not relabeled.

The loading, Forest profile, completion, and ATAK sections below preserve
their original version-bounded runtime evidence. They are not an implicit
claim that every historical gate was rerun with `0.3.28`.

## Native loading presentation

The supported vanilla route is:

```text
GameManagerNetwork.OnAllPlayersLoaded(false)
-> GameManagerNetwork.ShowLoadingScreen  RVA 0x00916210
-> shipped canvas active
-> current player body frozen
-> player velocity cleared
-> infiltration UI closed
```

`GameManagerNetwork.HideLoadingScreen` is at RVA `0x0090E950`. Modded
Operations now calls the exact shipped show method when an additive package
scene enters and before runtime terrain/material preparation. The native
manager keeps ownership of the hide transition.

The exact combined release package logged on both scene generations:

```text
loadingScreenActiveSelf=True
loadingScreenActiveInHierarchy=True
nativeHideSoonFlag=False
```

The last value is expected. `get_LoadingScreenVisible` at RVA `0x0091A840`
returns the private `_hideLoadingScreenSoon` byte at offset `0x2A4`; it is not
the canvas active state.

The exact combined release package reached the Forest scene twice: first
launch and the shipped same-process restart. Both scene entries logged
`loadingScreenActiveSelf=True` and `loadingScreenActiveInHierarchy=True`
before terrain and material preparation. Both entries passed terrain and
walkable-ground readiness and logged
`portableOrErrorShaderRenderers=0`. The captured player-camera frame shows the
complete detailed forest. This result closes the brown-proxy presentation
regression for the tested release archive.

Ukrainian Forest declares two bundles with a combined size of `647940302`
bytes. In the accepted package `0.3.21` run, the dependency bundle took
`23.270 s`, the scene bundle took `0.807 s`, and verified registration took
`24.148 s` total. Confirm waited `22.119 s` for the remaining selected-map
work. Vanilla
missions can appear faster because their installed content can already be
resident. Selected-map prefetch moves this I/O earlier; it does not remove the
bytes.

## Forest-only PVE profile

The fixed schema-v2 profile is operation-local and PVE-only. It writes six
values to `BotSpawnDetails` before the shipped
`RaidManager.ServerSpawnAI(false)` call:

```text
profile=dense-forest-balanced-v1
detectionRangeMeters=45
fieldOfViewDegrees=90
maximumEffectiveRangeMeters=-1
wanderDistanceMeters=38
useComms=true
counterSuppression=false
```

Schema v1 and operations without a profile keep the legacy framework values.
PVP, vanilla operations, and other packages do not receive the Forest profile.
The framework adds no difficulty UI.

The shipped `BOT V2` prefab keeps `BrainAI` on its network root. The moving
`SK_Insurgent_P8` child has `AgentController` and enabled `FollowerEntity`.
The release diagnostic therefore measures `brain.agent.position`, not the
stationary `brain.transform.position`.

The Forest `0.4.17` and Modded Operations `0.3.20` movement baseline used one
fresh process for a first launch and native same-process restart. Both
120-second windows passed the repository verifier:

| Run | Native bots | Native initial delay | Moved at least 1 m | Moved at least 5 m toward insertion | Maximum displacement | Vegetation evidence |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| First launch | 15 | `9.31..36.78 s` | 15 | 6 | `51.19 m` | present |
| Native restart | 14 | `10.97..33.02 s` | 14 | 4 | `49.34 m` | present |

The runtime used the exact 183 authored layer-18 vegetation triggers. The
framework diagnostic is read-only. It does not set a destination, target,
vision field, weapon field, or AI state.

The exact-package two-window log has SHA-256
`EB246BBBE8334C72702FDF2C8B58DD1D7DB15E566405B194D89914A8F94B29C8`.
The smoke trace has SHA-256
`10AECCF44BED40E747097FF5C637D838B3A068E585498A80675DF53339353B09`.
The first-launch player-camera capture has SHA-256
`C3FAAA9459E559AE8B15B7AF90BAA10068EE3AF46CBE99CB77190AB3E1589423`.
The restart capture has SHA-256
`3CE33FDD42B3EC9273888E1A3248F20705F8BE3E6196A8F167F2794FC67C999D`.
The machine-readable result has SHA-256
`5FEE75A6B50ED680E805214E230DD4FA1FF9FC7805A205F5445838188F62A8A3`.
It reports `passed`, exact operation `community.ukrainian-forest.pve`, hold
time `122`, and no private driver after cleanup.

## Other accepted runtime gates

The pinned local build accepts:

- physical `MODDED OPS` tab and package preview;
- one-click first Confirm;
- exact standalone scene load;
- above-terrain player spawn;
- 10 through 15 grounded native PVE actors;
- unload and Lone Wolf re-entry;
- same-process alive restart;
- same-process KIA restart through the shipped Mission Failed UI;
- 11:00 `PVP Woods Warehouse` daylight contract; and
- 02:00 four-tube white-phosphor GPNVG with terrain visible outside ECOTI.

The KIA test restored one owned playable player and grounded 14 of 14 active
AI within `0.03 m` of live Terrain.

## Native PVE completion and ATAK

The release-version completion runs under
`<AUTHOR_WORKSPACE>/reports/observer_qa/forest_20260805_215437` and
`<AUTHOR_WORKSPACE>/reports/observer_qa/forest_20260805_220738` accept:

- `StandardPVE` with `IsSimulation=false`;
- one shipped `RaidManager` and one shipped `ExfilZone`;
- `raid.exfilZones.Count=1`;
- extraction locked while native AI remain;
- `GameManager.allAI` reaching zero through the native Health death route;
- zone and global extraction unlock;
- an active layer-17 ATAK marker with four-vertex `Marker` mesh,
  `HDRP/Unlit` `ExfilZone` material, and resident `ExfilZone` texture;
- positive zone/global physical occupant counts;
- the shipped 15-second timer; and
- the shipped Mission Successful After Action Report.

The Forest extraction root in those runs is `(0.00,0.11,7.00)`, in the same
northern area as the PVE player insertion. A direct player test accepted the
placement and completion behavior.

## Open acceptance gates

- Host and remote-client PVP exact-content transfer/preload, scene agreement,
  first spawn, freeze, synchronized movement, firearm-specific hit
  registration, death, respawn, score, round, retained-content Restart, and
  return lifecycle.
- Deliberate PVP mismatch and membership-change live tests must fail closed.
  Late join is not a supported target for this protocol.
- Online PVE package/scene identity, grounded owner-local placement, identical
  server-authored AI population, movement, projectile, damage, extraction, and
  restart equivalence. Protocol v6 now gates these phases statically, but only a
  paired two-PC BepInEx run can establish their live behavior.
- A separate type-specific live probe for the generic verified dependency-
  asset borrower API. Ukrainian Forest does not depend on that open path.
