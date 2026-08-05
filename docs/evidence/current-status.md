# Current evidence status

## Source state

Framework source version: `0.3.19` candidate.

The source and package validators prove these bounded facts:

- the framework has no Ukrainian Forest identity or map coordinate;
- selected-map bundles load in declared order;
- the framework retains dependency ownership;
- companions can borrow verified dependency assets through
  `LoadVerifiedMapDependencyAsset<T>`;
- PVE and PVP use distinct shipped-compatible owners;
- player and process-global state has identity-checked teardown code.
- schema-v2 `pveAiProfile` is immutable, PVE-only, and operation-local;
- Modded Operations writes its six consumed values before the shipped
  `RaidManager.ServerSpawnAI(false)` call;
- schema-v1 and schema-v2 operations without a profile keep the 72 m, 105
  degree, 90 m, 18 m, communications-on, counter-suppression-on legacy values.
- profiled PVE operations start one read-only six-snapshot diagnostic; its
  source compiles and its gate is `PveAiProfile != null`, but no current live
  log has completed the 120-second window yet.
- `eng/verify_profiled_pve_runtime.py` parses the last requested runs and fails
  closed on incomplete schedules, wrong live profile values, zero native
  delay, immediate movement or acquisition, absent search movement, absent
  toward-insertion movement, or absent vegetation-blocked sight evidence.

The new profile path and the Forest 274-blocker activation are
`PROVEN-STATIC`. The first-launch and repeat-launch physical Forest behavior
matrix remains open. See
[Fixed PVE AI profile and vegetation sight](../architecture/pve-ai-profile-and-forest-sight.md).

The exact candidate artifacts are:

```text
OperatorModdedOperations.dll
bytes=159232
sha256=257F5449463BF2D2E2BD71CBC3AEA513A1788E882578CA98B631FB70E2EB1F25

OperatorModdedOperationsFramework_v0.3.19.zip
bytes=940720
sha256=05FB3FE1841266B17672E5ABDFDBF07F1C91E309A9E069ABA8893E26F1A71B0E
```

The ZIP passed a complete 7-Zip integrity test. Closed-game deployment copied
the exact staged Core and framework DLL hashes into the pinned local install.

## Runtime state

The physical Cerberus tab, package preview, one-click Confirm flow, exact scene
load, Forest grounding, PVE count range, unload, Lone Wolf re-entry, repeat
launch, 11:00 daylight, and 02:00 white-phosphor GPNVG flow have player-camera
and live-log evidence in the paired private test project.

The accepted 11:00 run logged:

```text
sunLux=30000
sunTemperature=5500
sunBounce=5
profileSource=PVP Woods Warehouse day
bloom=0.03
lensFlare=0.5
nightAmbient=False
whitePhosphor=False
externalLut=True
```

The accepted repeat 02:00 run logged:

```text
sunLux=40
sunTemperature=9754
sunBounce=1
profileSource=PVP-map night
bloom=0.3
lensFlare=1
nightAmbient=True
whitePhosphor=True
externalLut=False
```

The repeat run used one Cerberus Confirm, loaded from the verified bundle
cache in approximately eight seconds, spawned the player above live terrain,
and did not enter the `MAP LOADED !BUG!` restart loop. The GPNVG image used
white phosphor in all four tubes. Terrain and foliage remained visible outside
the brighter ECOTI channel.

The 2026-08-04 same-process KIA restart test also passed. The bounded runtime
driver launched PVE through the normal Cerberus path. It applied lethal head
damage through the current native `Health` command handler. It observed the
native player-death state and invoked the current `GameManagerNetwork`
fail-operation RPC handler. It then found and invoked the real
`MissionFailedPopup/RestartOperation` control. The restarted scene used
`Assets/Maps/UkrainianForest/Scenes/UkrainianForest.unity` with a new scene
handle. The result had one owned playable player and 14 active `BrainAI`
instances. All 14 AI instances were grounded. The largest absolute
AI-to-Terrain height difference was 0.03 m.

The accepted terminal evidence was:

```text
SMOKE_NATIVE_PLAYER_DEATH_OBSERVED
SMOKE_NATIVE_FAIL_OPERATION_INVOKED via GameManagerNetwork RPC handler
SMOKE_NATIVE_DEATH_SCREEN_OBSERVED popup=MissionFailedPopup, restartControl=RestartOperation.
SMOKE_DEATH_RESTART_BUTTON_INVOKED
SMOKE_RESTART_BOOTSTRAP_FOUND scene=Assets/Maps/UkrainianForest/Scenes/UkrainianForest.unity
SMOKE_PVE_RESULT bootstrap=true, restart=true, playable=true, players=1, pveAI=14, groundedPveAI=14, maximumAiGroundDelta=0.03.
```

The `0.3.18` verified-asset API is `PROVEN-STATIC`. Its Forest `0.4.12`
consumer is rejected. The live consumer received null for all three requested
raw pine assets, and its fallback made all pine branches white. Forest
`0.4.13` does not use this API in its active pine material path. Do not label
the cross-plugin generic asset path `SUPPORTED` until a separate bounded asset
probe proves it. Do not make the Forest release depend on that open probe.

## Open acceptance gates

- Forest schema-v2 AI search, foliage occlusion, acquisition, and reciprocal
  firearm behavior on first and repeat launch.
- Host and remote-client PVP full round lifecycle.
- PVE reciprocal firearm damage after the current package build.
- Generic verified dependency-asset API live probe with a non-release test asset.
