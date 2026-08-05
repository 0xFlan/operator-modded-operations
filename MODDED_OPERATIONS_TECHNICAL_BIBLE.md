# OPERATOR: Modded Operations technical BIBLE

## 1. Purpose

This document is the technical authority for the public framework source in
this repository. It is written for a human maintainer and for an automated
coding agent. Use the exact names in this document. Do not infer a game
contract from a similar name.

The framework version is `0.3.19`. The plugin identity is
`operator.modded-operations`. The assembly is
`OperatorModdedOperations.dll`. The required Core version is
`0.2.0-alpha.3`.

## 2. Path tokens

| Token | Meaning |
| --- | --- |
| `<REPOSITORY_ROOT>` | This Git repository. |
| `<OPERATOR_INSTALL>` | The directory that contains `OPERATOR.exe`. |
| `<OPERATOR_MOD_API_REPOSITORY>` | A local Operator Mod API checkout. |
| `<PACKAGE_ROOT>` | One directory that contains `operator-map-package.json`. |
| `<MAP_COMPANION_ROOT>` | One map-owned BepInEx plugin directory. |

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

## 4. Startup and catalog flow

The plugin attribute is the first closed gate:

```csharp
[BepInPlugin("operator.modded-operations", "OPERATOR: Modded Operations", "0.3.19")]
[BepInProcess("OPERATOR.exe")]
[BepInDependency("operator.modapi", CerberusNativeTabFix.RequiredApiVersion)]
```

At `Load()`, the framework registers the two managed game-mode types through
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

## 8. Exact scene and readiness

The framework loads the exact `scenePath` from the accepted scene bundle. It
does not load Office, Georgia, or a donor mission. The scene contract checks
the expected package, map, scene path, map marker, current-scene spawn sets,
terrain declaration, and map-companion readiness when required.

The companion must finish its strict world contract before the framework
creates PVE actors. Do not replace this barrier with an arbitrary frame delay.

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

`ChooseStandalonePveEnemyCount` validates
`1 <= minEnemies <= maxEnemies <= 64`. It selects one inclusive,
deterministic host count. `TrySpawnStandalonePveEnemies` passes the valid
prefabs and the package-valid markers to a scene-owned `RaidManager`. It calls
`RaidManager.ServerSpawnAI(false)`. Do not use a one-argument manual
`NetworkServer.Spawn` as an AI replacement.

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

For day choices, `0.3.18` copies the audited
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
2. removes the operation-owned Mirror keys;
3. clears mode singletons only when they still point at the owned component;
4. restores player spawn globals only when identity still matches;
5. restores NVG state and destroys the owned Volume/profile;
6. unloads scene and bundle state owned by the operation;
7. clears cached player attempts, assignments, and generation handles.

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

The Git repository publishes the authored source and the hash-pinned
`decompiled/release-0.3.19` verification snapshot. The previous `0.3.18` and
rejected `0.3.17` snapshots are under `decompiled/archive`. The release ZIP contains
the compiled DLL, not the bracketed repository placeholders. Use
[the package placeholder](packaging/README-PACKAGE-PLACEHOLDER.md) as the exact
install-root-relative staging contract.

The framework archive contains Core and framework files only. A map archive
contains package data and its map companion only. A complete convenience
archive can contain both ownership domains.

Never ship QA flags, force-scene code, test controls, private logs, copied game
DLLs, or extracted game assets.

## 16. Verification matrix

| Gate | Required proof |
| --- | --- |
| UI | Physical click on `MODDED OPS`, row, Back, Execute, Cancel, and Confirm. |
| First Confirm | One physical Confirm starts the scene. No second laptop interaction. |
| Preview | Same verified image in preparation, fullscreen, and infiltration views. |
| PVE | Package count range, in-bounds markers, armed AI, reciprocal bullet damage. |
| PVP | Host and remote client on different authored sides, death, score, round respawn. |
| Player | Player object, camera, input, movement, correct terrain spawn, repeat launch. |
| Restart | Alive restart and KIA end-screen restart as separate gates. |
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
  -> scenePath equality check
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

Modded Operations `0.3.19` and Operator Mod API `0.2.0-alpha.3` add the
optional `pveAiProfile` object. Only a schema-v2 PVE operation can own it. PVP
rejects it. Schema v1 rejects it. There is no difficulty UI and no process-
global AI write.

The closed fields are `id`, `detectionRangeMeters`, `fieldOfViewDegrees`,
`maximumEffectiveRangeMeters`, `wanderDistanceMeters`, `useComms`, and
`counterSuppression`. Operator Mod API validates and freezes them. Framework
member `ConfigureStandaloneBotDetails` writes the selected values to each
native `BotSpawnDetails` before `RaidManager.ServerSpawnAI(false)`.

The current native `RaidManager.ApplyBotSpawnSettings` transfers detection
range to `BrainAI` and `EyesAI`, FOV to `BrainAI`, communications,
counter-suppression, effective range unless the marker value is `-1`, and
wander distance unless the marker value is `-1`. It does not consume marker
`DetectionTimeMultiplier` or `HearingRange` in the pinned build.

`BrainAI.Wander(float)` preserves the native prefab delay. It waits for
`WanderTimer * Patience`, then chooses around the current position with the
equivalent of `RandomNavSphere(position, 5, WanderDistance)`. Package authors
must therefore tune wander from playable geometry and spawn gaps. Repeated
native choices can expand a search; a larger radius does not remove the first
delay.

Foliage sight remains map content. The map companion must inspect how the
same installed vanilla prefab participates in the shipped `EyesAI` linecast.
It can activate an authored `AI_VisionBlock` collider when that is the native
prefab contract. The generic framework must not invent map-specific bush
names, counts, colliders, or coordinates.

The full schema bounds, source members, native offsets/RVAs, exact application
order, foliage collision rules, logging, and gates are in
[Fixed PVE AI profile and vegetation sight](docs/architecture/pve-ai-profile-and-forest-sight.md).
This candidate is `PROVEN-STATIC`. Do not label it `SUPPORTED` until its
physical first-launch and repeat-launch behavior matrix passes.
