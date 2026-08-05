# Current evidence status

## Release state

Framework version: `0.3.20`.

The exact release artifacts are:

```text
OperatorModdedOperations.dll
bytes=165888
sha256=193EEFB44511AA8E0A102D9D145C7BD1DEBDBFF4021A7C81E8C991A53A3BE1EB

OperatorModdedOperationsFramework_v0.3.20.zip
bytes=961620
sha256=8B09D64FCFCEBB3A4B086913F0734657C2E71CAB023C552E11D8F0715DF324A3
```

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

Ukrainian Forest declares two bundles with a combined size of `647869804`
bytes. In the exact-package run, the dependency bundle took `24.449 s`, the
scene bundle took `0.833 s`, and verified registration took `25.347 s` total.
Confirm waited `23.442 s` for the remaining selected-map work. Vanilla
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

One fresh process completed a first launch and a native same-process restart.
Both 120-second windows passed the closed verifier:

| Run | Native bots | Native initial delay | Moved at least 1 m | Moved at least 5 m toward insertion | Maximum displacement | Vegetation evidence |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| First launch | 12 | `6.99..40.58 s` | 12 | 4 | `40.16 m` | present |
| Native restart | 10 | `9.04..42.12 s` | 9 | 4 | `46.87 m` | present |

The runtime used the exact 183 authored layer-18 vegetation triggers. The
framework diagnostic is read-only. It does not set a destination, target,
vision field, weapon field, or AI state.

The exact-package two-window log has SHA-256
`DD8F2B6DB569F12ADB14358C764ECDAFB0E8E07709D2CB2C81E6CF968B05713B`.
The smoke trace has SHA-256
`B33D745321F4A11F8272AEEA162531683D39185BF0574129B7524AA6452214E`.
The first-launch player-camera capture has SHA-256
`6823B4933ACA51BBB4C319D77EFDC8ACD4BDC910F5A3BF092A51993DF955692B`.
The restart capture has SHA-256
`D2CEAF125C2105D52F191FB596AEA78955618CAF1C47259FA1A6DDA82C7F1C9E`.

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

## Open acceptance gates

- Reciprocal PVE firearm damage after the current package build.
- Host and remote-client PVP first spawn, freeze, death, respawn, score,
  round, restart, and return lifecycle.
- A separate type-specific live probe for the generic verified dependency-
  asset borrower API. Ukrainian Forest does not depend on that open path.
