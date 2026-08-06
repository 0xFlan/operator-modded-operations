# Current evidence status

## Release state

Framework version: `0.3.22`.

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
PVE verifier unit suite passed all three tests. The public repository audit
checks source closure, decompiler identity, private-path absence, links, JSON,
and forbidden release binaries.

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

- Host and remote-client PVP first spawn, freeze, death, respawn, score,
  round, restart, and return lifecycle.
- A separate type-specific live probe for the generic verified dependency-
  asset borrower API. Ukrainian Forest does not depend on that open path.
