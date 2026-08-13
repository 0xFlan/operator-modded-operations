# Create a map package

This procedure creates the data that OPERATOR: Modded Operations consumes. It
also identifies the work that belongs in an optional map companion. Follow the
order. Do not make the framework contain one map's shader, terrain, or marker
repair.

If you are new to the runtime, read
[Complete operation lifecycle](../architecture/full-operation-lifecycle.md)
first. It explains how each field reaches Cerberus, the streamed scene,
terrain, player, PVE/PVP, failure, restart, and teardown consumers.

## 1. Select stable identities

Choose lowercase, dot-separated identities before you build content:

```text
packageId   = author.example-map
mapId       = author.example-map.main
operationId = author.example-map.main-pve
spawnSet    = main-pve
```

Do not change an ID when you change a display name. Increase the package
`version` when any declared byte changes.

## 2. Create the package directory

The install target is one direct child of
`<OPERATOR_INSTALL>/BepInEx/OperatorMods`:

```text
<PACKAGE_ROOT>/
  operator-map-package.json
  content/
    example_map_assets
    example_map_scene
  media/
    example_map_preview.jpg
  lighting/
    optional_lut.bytes
```

The package root is data-only. Do not place a DLL under `<PACKAGE_ROOT>`. If
the map needs executable reconstruction, install its companion separately at
`<OPERATOR_INSTALL>/BepInEx/plugins/<MAP_COMPANION>/`.

## 3. Use the supported Unity target

Ukrainian Forest uses Unity `6000.3.8f1`, HDRP, and
`BuildTarget.StandaloneWindows64`. Match the target game build's Unity and
render-pipeline expectations. A Unity version mismatch can change serialized
types, shader variants, terrain payloads, or scene dependencies.

Use explicit `AssetBundleBuild` records and `BuildAssetBundleOptions.StrictMode`:

```csharp
var builds = new[]
{
    new AssetBundleBuild
    {
        assetBundleName = "example_map_assets",
        assetNames = dependencyAssetNames.ToArray()
    },
    new AssetBundleBuild
    {
        assetBundleName = "example_map_scene",
        assetNames = new[]
        {
            "Assets/Maps/ExampleMap/Scenes/ExampleMap.unity"
        }
    }
};

BuildPipeline.BuildAssetBundles(
    outputDirectory,
    builds,
    BuildAssetBundleOptions.StrictMode,
    BuildTarget.StandaloneWindows64);
```

## 4. Split the bundles by ownership

The dependency bundle contains reusable objects and payloads:

- map root prefab;
- meshes and complete renderer submesh closure;
- portable materials;
- albedo/base, normal, mask, height, thickness, and opacity textures;
- terrain height and surface-weight payloads;
- lighting profiles or serialized `TextAsset` records;
- map-owned LUT assets when Unity can serialize them correctly;
- any prefab that the scene references and that is not embedded in the scene.

The scene bundle contains the streamed scene and its direct scene closure.
The dependency bundle must report zero scene paths. The scene bundle must
report the exact `scenePath` declared in the manifest.

List dependency bundles in reference order. Modded Operations loads
`dependencyBundles[]` from first to last, then loads `sceneBundle`.

## 5. Author the exact scene markers

The streamed scene must contain an inactive or active Transform with this
exact case-sensitive name:

```text
MAP_ID_<mapId>
```

For `mapId=author.example-map.main`, create:

```text
MAP_ID_author.example-map.main
```

Create one exact spawn-set marker for every operation that uses the scene:

```text
SPAWN_SET_main-pve
SPAWN_SET_main-pvp
```

These are metadata markers. Their positions do not place actors.
`ValidateStandaloneSceneContract` searches every root and child Transform,
including inactive objects, and requires exact ordinal names.

## 6. Author world spawn transforms

Modded Operations discovers spawn transforms by name. It adds the shipped
`SpawnPoint` component at runtime when the player marker does not already have
one.

| Use | Accepted prefix | Runtime result |
| --- | --- | --- |
| PVE player | `PVE_PlayerSpawn_` | Team 1 player spawn. |
| PVP Team 1 | `PVP_Team1Spawn_`, `Team1_Spawn_`, `Team1_Backup_Spawn_` | `SpawnPoint.Team=1`. |
| PVP Team 2 | `PVP_Team2Spawn_`, `Team2_Spawn_`, `Team2_Backup_Spawn_` | `SpawnPoint.Team=2`. |
| PVE enemy | `PVE_EnemySpawn_` | `RaidManager.botSpawnPoints`. |
| PVE HVT, map-specific | `PVE_HVTSpawn_` | Companion/operation HVT contract. |
| FFA, when used | `FFA_Spawn_` | Native FFA spawn collection. |

Use zero-padded suffixes, for example `PVE_EnemySpawn_00`. Keep every actor
marker inside the playable collision boundary, above the live terrain, and on
a valid navigation node. Author at least `minEnemies` enemy markers.

The current PVP contract is one-based. Team ID `1` must use the Team 1 set.
Team ID `2` must use the Team 2 set. Do not use zero and one. Each side must
contain at least `ceil(operation.maximumPlayers / 2)` accepted markers. The
vanilla-compatible maximum declaration of 12 therefore requires six markers
per side.

## 7. Separate UI infiltration markers from world spawns

An `infiltrations[]` record places a marker on the briefing image. It does not
place a player in the Unity scene.

```json
{
  "id": "north-entry",
  "displayName": "NORTH ENTRY",
  "mapPositionX": 0.50,
  "mapPositionY": 0.20,
  "maxPlayers": 8
}
```

`mapPositionX` and `mapPositionY` are normalized image coordinates in the
closed range `0..1`. World placement comes from the named scene transforms in
the previous section.

## 8. Supply a directional-light fallback

The standalone scene contract requires at least one Directional `Light` in
the selected scene. A map companion can later adopt or reconfigure a verified
vanilla light profile. The scene must still have a package-owned fallback so
the fail-closed contract does not accept a black scene.

## 9. Decide who owns terrain reconstruction

Use a normal serialized `TerrainData` only when a clean external-bundle test
proves that render and collision payloads survive. Ukrainian Forest did not
assume that result. It ships lossless height and surface-weight payloads and a
`runtimeTerrain` declaration.

A complete `runtimeTerrain` record identifies:

- the scene root to receive live data;
- the dependency bundle that owns all terrain inputs;
- height and surface-weight asset paths and encodings;
- heightmap, alphamap, basemap, and detail resolutions;
- world origin and width/height/length;
- every layer's diffuse, normal, mask, tile size, normal scale, metallic, and
  smoothness.

Modded Operations interprets this generic closed record in
`TryPrepareRuntimeTerrain`. It loads inputs from the verified dependency
bundle, decodes the declared height and weight encodings, creates the layer
objects, and binds one `TerrainData` to `Terrain` and `TerrainCollider`. The
framework does not contain one map's terrain equation. The author supplies
the numerical payload. A companion can perform exact-map work only after the
generic bind.

For an exact working record, inspect
`OPERATOR: Ukrainian Forest/source/operator_map_packages/community.ukrainian-forest/operator-map-package.json`.

## 10. Preserve model and material closure

For every renderer, record:

1. mesh and LOD identity;
2. submesh count;
3. material slot count and slot order;
4. base/albedo texture;
5. normal texture and normal-map import type;
6. mask/ORM texture and channel meaning;
7. opacity/alpha or thickness texture;
8. texture colour space, dimensions, mip count, streaming state, and wrap;
9. shader family and actual property names in the installed game;
10. render queue, sidedness, keywords, tags, and disabled passes.

A white object is usually not evidence that the texture file is missing. It
can mean that the live shader reads a different property name. Ukrainian
Forest proved this with the vanilla `SeedMesh_Tree_Bark` properties
`_MainTex`, `Normal_vegetation`, and `mask_vegetation`.

Research the same asset in an installed vanilla map before you write a
runtime repair. Compare the complete prefab graph, renderer slots, material
pointers, compiled shader name, and serialized values. Do not infer from a
similar material.

## 11. Preserve complete prefab graphs

Do not replace a native interactive prefab with a visual mesh. Keep its root,
children, colliders, rigidbody/network synchronization, interaction objects,
navigation links/cuts, and serialized reference topology.

For example, `_DoorV2_BASE.prefab` is authored content. OPERATOR does not
normally spawn its door pieces as a map-start repair. A correct map contains
the complete door prefab graph in the prefab/scene. See the map guide's DoorV2
page for the exact component and null-field contract.

## 12. Add a map companion only for map-specific work

A companion should gate itself on both exact scene path and exact
`MAP_ID_...` marker. Appropriate responsibilities include:

- map-specific resident-shader reconstruction;
- exact terrain-material repair after the generic runtime-terrain bind;
- tree grounding after live TerrainData exists;
- exact marker correction/quarantine;
- map-owned A* graph construction and validation;
- native lighting-profile adoption;
- an exact-map diagnostic or readiness result that supplements the framework
  scene, collision, and game-mode gates;
- reverse-order cleanup of only map-owned runtime objects.

It must not load or unload a second copy of the dependency bundle. Borrow one
verified dependency asset when necessary:

```csharp
TextAsset state = CerberusNativeTabFix
    .LoadVerifiedMapDependencyAsset<TextAsset>(
        "author.example-map.main",
        "assets/maps/example/material-state.raw.yaml");
```

Treat the returned object as borrowed. Modded Operations owns the bundle
lifetime.

When a map with PVP depends on companion code, schema v2 must declare the
loaded module and its scene-lifecycle gate exactly:

```json
"runtimeCompanion": {
  "pluginGuid": "author.example-map.runtime",
  "pluginVersion": "1.0.0",
  "sha256": "<64 lowercase hex characters>",
  "readyMarkerName": "RUNTIME_EXAMPLE_MAP_READY",
  "failureMarkerName": "RUNTIME_EXAMPLE_MAP_FAILED"
}
```

The companion creates exactly one ready marker only after every required
runtime repair and validation passes. It creates the failure marker on any
later fatal audit as well as initial failure. The framework resolves the exact
loaded plugin GUID/version/DLL hash on every peer, searches only the active
generation scene for the two exact case-sensitive names, gives failure
precedence, and keeps monitoring failure after `SceneReady`. Omit the whole
property (or use `null`) when no companion exists; never invent a sentinel
plugin or marker.

## 13. Create the preview image

Use a normal JPEG or PNG outside the AssetBundles. Set `previewImage` to its
package-relative path. Modded Operations decodes the verified bytes once and
uses the same cached sprite for:

- the operation-preparation map;
- the fullscreen map;
- the infiltration-selector map.

The consumer does not browse for an arbitrary image. A package author can
replace the file, update the `files[]` identity, increase the package version,
and distribute the new package. A private local replacement without matching
manifest bytes is rejected and creates a multiplayer identity mismatch.

Calculate the exact identity:

```powershell
$file = Get-Item -LiteralPath '<PACKAGE_ROOT>\media\example_map_preview.jpg'
$bytes = $file.Length
$sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
"bytes=$bytes"
"sha256=$sha256"
```

## 14. Write the manifest

Start with
[`examples/operator-map-package.example.json`](../../examples/operator-map-package.example.json).
Use schema version 2 when the operation needs a fixed package-owned PVE AI
profile, scene variants, runtime terrain, or `runtimeCompanion`:

```json
{
  "$schema": "https://operator-mod-api.dev/schemas/operator-map-package-v2.json",
  "schemaVersion": 2
}
```

Schema v1 remains valid and cannot contain `pveAiProfile` or
`runtimeCompanion`. Schema v2 accepts the closed profile only on PVE and the
optional companion contract at map level. The framework applies the PVE
profile through native `BotSpawnDetails`; it does not add a difficulty UI or
change another operation.

The following PVE operation is complete enough to explain every presentation
and population field:

```json
{
  "operationId": "author.example-map.main-pve",
  "displayName": "EXAMPLE MAP PVE",
  "displayOrder": 0,
  "mode": "pve",
  "areaOfOperation": "EXAMPLE REGION",
  "sitrep": "Operators enter from the north. Armed contacts hold the south route.",
  "minPlayers": 1,
  "maxPlayers": 8,
  "minEnemies": 10,
  "maxEnemies": 15,
  "pveAiProfile": {
    "id": "woodland-balanced-v1",
    "detectionRangeMeters": 45.0,
    "fieldOfViewDegrees": 90.0,
    "maximumEffectiveRangeMeters": -1.0,
    "wanderDistanceMeters": 38,
    "initialWanderDelayMaxSeconds": 12.0,
    "useComms": true,
    "counterSuppression": false
  },
  "spawnSet": "main-pve",
  "infiltrations": [
    {
      "id": "north-entry",
      "displayName": "NORTH ENTRY",
      "mapPositionX": 0.5,
      "mapPositionY": 0.2,
      "maxPlayers": 8
    }
  ],
  "timeCodes": ["1100", "0200"],
  "defaultTimeCode": "1100"
}
```

Measure the playable combat volume and every accepted player-to-enemy spawn
distance before choosing values. Do not use the larger visual terrain apron.
Keep `wanderDistanceMeters` below the distance that would let one native
wander choice cross the intended encounter midpoint. The native bot waits for
its prefab-owned `WanderTimer * Patience` before it chooses a destination
around its current position, so repeated choices create a progressive search.
Omit `initialWanderDelayMaxSeconds` to preserve that complete prefab-owned
first delay. Declare 2 through 60 seconds only when the map needs a bounded
first search response. The server then advances only each new, non-responding
Wander bot's first clock with a deterministic stagger; it does not change
reaction time, patience, combat, or later wander cycles.

Use `maximumEffectiveRangeMeters=-1` to preserve each native AI prefab's
effective range. This does not mean unlimited range. It means the current
native settings copier skips that field. Read
[Fixed PVE AI profile and vegetation sight](../architecture/pve-ai-profile-and-forest-sight.md)
for bounds, current-build native offsets/RVAs, line-of-sight layers, code, and
diagnostics.

For PVP, omit PVE enemy bounds and author separated Team 1 and Team 2 world
marker sets at the capacity required by `ceil(maximumPlayers / 2)`.

## 15. Close the file identity list

Every file that the manifest names must have one `files[]` item with its
canonical package-relative path, exact byte count, and lowercase SHA-256.
Core rejects:

- an absent file;
- a size or hash mismatch;
- an absolute path;
- `..` path escape;
- a reparse-point escape;
- an undeclared required bundle or image.

Do not edit a preview, bundle, LUT, or numerical payload after hashing.

## 16. Validate bundle and scene closure

Before install, prove all of these facts:

1. Every dependency bundle loads in manifest order and has zero scene paths.
2. The scene bundle reports exactly the declared `scenePath`.
3. The streamed scene has `MAP_ID_<mapId>` and
   `SPAWN_SET_<spawnSet>`.
4. PVE has at least one player marker and at least `minEnemies` enemy markers.
5. PVP has separated Team 1 and Team 2 markers, each with at least
   `ceil(maximumPlayers / 2)` accepted entries.
6. The scene has a Directional Light.
7. Every renderer has non-null mesh and material slots.
8. Every required texture and numerical payload is present in a declared
   dependency bundle.
9. The companion's exact ready contract passes, when the map requires one.
10. Source, stage, archive, and installed hashes are recorded.

## 17. Test the physical native flow

Static validation cannot prove the user flow. In a fresh process, use the
physical Cerberus laptop:

1. open `MODDED OPS`;
2. select the operation;
3. confirm the correct preview and briefing;
4. select a time and infiltration;
5. press Confirm once;
6. verify the exact scene, terrain, player, input, camera, and movement;
7. verify PVE firearms or, with a real host and remote, exact peer content,
   synchronized movement, firearm-specific hit registration, opposite teams,
   score, round respawn, Restart, and return;
8. return to the armory;
9. start the operation a second time;
10. test alive restart and KIA Restart Operation separately.

Do not label the package `SUPPORTED` until its documented live matrix passes.
PVP static agreement tests do not prove transport or gameplay. PVE co-op
bypasses the PVP peer agreement and requires its own online package/scene/AI/
movement/projectile equivalence matrix. Late join is unsupported by the
current PVP protocol.
