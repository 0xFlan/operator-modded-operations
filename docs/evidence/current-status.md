# Current evidence status

## Source state

Framework source version: `0.3.18`.

The source and package validators prove these bounded facts:

- the framework has no Ukrainian Forest identity or map coordinate;
- selected-map bundles load in declared order;
- the framework retains dependency ownership;
- companions can borrow verified dependency assets through
  `LoadVerifiedMapDependencyAsset<T>`;
- PVE and PVP use distinct shipped-compatible owners;
- player and process-global state has identity-checked teardown code.

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

The `0.3.18` verified-asset API is `PROVEN-STATIC`. Its Forest `0.4.12`
consumer is rejected. The live consumer received null for all three requested
raw pine assets, and its fallback made all pine branches white. Forest
`0.4.13` does not use this API in its active pine material path. Do not label
the cross-plugin generic asset path `SUPPORTED` until a separate bounded asset
probe proves it. Do not make the Forest release depend on that open probe.

## Open acceptance gates

- Host and remote-client PVP full round lifecycle.
- PVE reciprocal firearm damage after the current package build.
- KIA end-screen Restart Operation after the current package build.
- Generic verified dependency-asset API live probe with a non-release test asset.
