# Launch, game modes, and restart

## Confirm transaction

`BeginCatalogOperationLaunch` captures the exact `MissionLaptop` and
`PlayerNetworking` owners. It does this before asynchronous input/output work.
The method joins an existing same-map prefetch when one exists. It does not
start a second bundle load.

`PendingMapLaunch` stores:

| Field | Contract |
| --- | --- |
| `Map` | Immutable Core catalog map. |
| `Operation` | Immutable selected operation. |
| `TimeCode` | Valid member of the operation time list. |
| `LaunchLaptop` | Exact laptop that received Confirm. |
| `LaunchPlayer` | Exact player owner at Confirm time. |
| `LoadingBundles` | Framework-owned bundle set. |
| `BundlePaths` | Verified dependencies, then scene bundle. |

The runner uses `AssetBundle.LoadFromFileAsync`. It loads dependencies in
manifest order. It loads the scene bundle last. `ValidateLoadedSceneBundle`
requires the declared `scenePath` and rejects undeclared scene paths.

After validation, `InvokeNativeCatalogLaunch` restores the captured laptop if
the native field became null. It primes `InfilSelectorDisplayer.SpawnMap`.
It then calls the shipped `CerebusOpboard.Start_Operation()` method.

Do not call these methods for a package launch:

```text
OperationsManager.StartOperation
OperationsManager.CMD_StartOperation
OperationsManager.DebugStartOperation
```

They are not equivalent to the complete Cerberus board handoff.

## Scene preparation barrier

`OnSceneLoaded` creates one `ActiveMapOperation`. The runner waits for all
required scene conditions. The conditions include the exact scene path,
package marker, runtime terrain, spawn marker sets, and companion readiness.
The framework does not use one fixed delay as proof of readiness.

## PVE owner

`StandalonePveGameMode` derives from `InfiltrationManager`. The framework
assigns both `InfiltrationManager.instance` and `GameMode.singleton` to the
same component. It calls the shipped initialization bodies.

`ChooseStandalonePveEnemyCount` requires this range:

```text
1 <= minEnemies <= maxEnemies <= 64
```

The host chooses one inclusive deterministic count. The package does not
create AI prefabs. `TrySpawnStandalonePveEnemies` filters the installed
`GameManager.AllAITypes` list. Each accepted prefab has `BrainAI`,
`NetworkIdentity`, and an armed `WeaponsAI` configuration. The framework then
uses `RaidManager.ServerSpawnAI(false)`.

`ConfigureStandalonePveController` also requires one package-authored
`PVE_ExfilZone_` marker. It copies the marker transform and trigger to the
Mirror-owned PVE bootstrap and adds one shipped `RaidManager` plus one shipped
`ExfilZone`. Extraction starts locked. The shipped raid unlocks the zone when
the native live-AI population reaches zero. A living player must occupy the
trigger for the native 15-second countdown. Completion uses the shipped
Mission Successful After Action Report.

The current framework also assigns the exact current-build ATAK visual to
`ExfilZone.ExfilMarker`. Read
[Native PVE completion, extraction, and ATAK](native-pve-completion-exfil-and-atak.md)
for exact code, mesh/material/texture data, and teardown.

## PVP owner

`StandalonePvpGameMode` derives from `PvpGameode`. It installs distinct team
arrays:

```csharp
pvp.Team1SpawnPoints = team1;
pvp.Team2SpawnPoints = team2;
```

The pinned game build uses team IDs `1` and `2`. The framework reads
`PlayerMaster.MyTeamIdentifier.TeamID`. It does not infer a team from a string.
The framework supplies all fields that shipped round logic reads. These fields
include audio sources, clip arrays, timers, score text, result roots, fade
state names, round limits, and outcome text.

## Player creation

`ConfigureStandalonePlayerSpawnContract` creates current-scene `SpawnPoint`
objects from the verified package marker set. It captures and later restores:

- `GameManager.SpawnPointsInScene`;
- `GameManager.instance.Pspawns`;
- `GameManager.instance.PnextSpawnIndex`;
- `GameManager.instance.RandomSpawns`.

The first owned request calls `PlayerMaster.SpawnPlayer()`. This keeps the
shipped camera, input, locomotion, and network setup. A bounded host recovery
can call the exact generated server body only after the first request fails to
create a new player object.

## Restart and return

The shipped persistent `GameManagerNetwork` owns Mission Failed and Restart
Operation UI. Modded Operations does not clone that UI.

Release occurs in reverse ownership order. The framework stops stale
completions, removes only owned Mirror IDs, restores only captured globals,
destroys owned mode and render objects, unloads framework-owned bundles, and
clears generation state. A second launch must create a new generation. It must
not reuse player attempts, mode singletons, or map-ready flags from generation
one. Successful teardown preserves `GameManagerNetwork.SuccessfulOperation`
until the Operation Room consumes it.
