# Ownership and load order

## Rule

Keep generic framework code separate from map-specific code. This rule makes
multiple packages possible and makes teardown deterministic.

## Load sequence

```text
Operator Mod API discovers package roots
-> Core validates manifest, paths, sizes, hashes, IDs, and dependencies
-> Core freezes one immutable catalog
-> Modded Operations binds private Cerberus UI to each live laptop
-> row selection starts one selected-map prefetch
-> Confirm captures the exact laptop and player owner
-> dependencies load in declared order
-> scene bundle loads and exact scene path is validated
-> shipped infiltration selector receives package data
-> CerebusOpboard.Start_Operation starts native operation flow
-> framework validates the exact streamed scene contract
-> framework reconstructs manifest-declared runtime terrain when present
-> framework validates walkable collision and installs current-scene spawns
-> framework creates the native-compatible game-mode owner
-> map companion applies exact-map materials, grounding, lighting, and A*
-> framework starts player and PVE/PVP flow
```

## Why the package directory is data-only

Core treats `OPERATOR/OperatorMods/<package-id>` as untrusted structured input.
It can bound JSON, canonicalize paths, reject reparse escapes, verify every
declared file, and freeze identities before Unity loads data. A DLL in that
directory would bypass the data contract.

Put executable map code in exactly one selected-loader location instead:

```text
<OPERATOR_INSTALL>/BepInEx/plugins/<map-plugin>/<map-plugin>.dll
<OPERATOR_INSTALL>/Mods/<map-plugin>.MelonLoader.dll
```

The companion must refuse activation unless package ID, map ID, scene path,
game build, and framework contract agree.

## What belongs to the framework

- Native-looking `MODDED OPS` tab and rows.
- Briefing and infiltration presentation.
- Raw preview decode and cache.
- Verified bundle load and exact scene load.
- Generic manifest-declared runtime TerrainData reconstruction.
- Shared `Terrain` and `TerrainCollider` binding and walkable-ground gate.
- `InfiltrationManager`-compatible PVE owner.
- `PvpGameode`-derived PVP owner.
- Player-spawn process-global capture and restore.
- Package-declared PVE count selection.
- Shipped `RaidManager.ServerSpawnAI(false)` actor creation.
- Shipped failure UI handoff and restart lifecycle.

## What belongs to a companion

- An installed private shader profile for one map.
- Exact-map terrain material repair after the generic bind.
- A map-owned A* service and graph dimensions.
- Marker containment, nearest-node snap, and tight ground checks.
- Map lighting payloads that a bundle cannot transport.
- Exact-scene tree or prop correction.
- Release of all state that the companion created.

## Refusal behavior

Fail closed. A package load failure must restore the confirmation control and
write an actionable `failed closed` diagnostic. It must not load a donor
scene or fall back to a vanilla map. A map diagnostic failure blocks release
acceptance. The framework terrain, collision, marker, and game-mode gates must
not start an operation generation from incomplete generic scene services.

Read [Complete operation lifecycle](full-operation-lifecycle.md) for exact
members and reverse teardown.
