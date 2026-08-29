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
| `SceneSelection` | Exact selected variant ID/path once committed. |
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
1 <= minEnemies <= maxEnemies <= 100
```

The native briefing exposes an integer selector over this closed range.
Confirm captures the displayed value into the pending launch and active
operation; a native restart retains that value. After scene preparation, the
host requires at least the selected number of active `PVE_EnemySpawn_`
markers on the live navigation graph. The package does not create AI prefabs.
`TrySpawnStandalonePveEnemies` filters the installed
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
Each side must expose at least `ceil(maximumPlayers / 2)` accepted spawn
markers. A 12-player declaration therefore requires six Team 1 markers and six
Team 2 markers.
The framework supplies all fields that shipped round logic reads. These fields
include audio sources, clip arrays, timers, score text, result roots, fade
state names, round limits, and outcome text.

### Standalone peer agreement

Networked standalone Confirm does not call the native board immediately. The host first
requires an active native host with at least one authenticated, ready remote
peer, snapshots the exact remote connection IDs, and checks the current
population against the operation's declared min/max range. One private Mirror
message ID is collision-checked separately in the client and server handler
registries.

The host offer binds the protocol and per-launch nonce to exact SHA-256 values
for the loaded framework DLL, API Core DLL, API BepInEx host DLL, and any
manifest-declared runtime companion. It also binds the API version, game build,
capability set, package ID/version/content hash, companion GUID/version and
marker names, map, operation, mode, spawn set, scene variant/path, time, and
min/max-player values. Package content identity covers the manifest and every
declared package file, not only the package version. A remote rejects any mismatch against its frozen local
catalog and loaded runtime. If its
exact bundle cache is absent, it uses the normal asynchronous verified package
loader; only after bundle validation does it commit the matching
`ActiveMapOperation` and send `ContentReady`.

The host invokes the native board only when every snapshotted remote has sent
`ContentReady`. During the transition, every peer independently validates the
scene contract, creates `StandalonePvpGameMode : PvpGameode`, registers asset
ID `0x4D4F5002`, and waits for the declared companion's unique ready marker in
that exact scene generation before contributing to `SceneReady`. A declared
failure marker wins even after readiness. The host's
`NetworkServer.Spawn` path remains closed until its local template and every
remote acknowledgement are ready and the connection-ID set is unchanged.
Remote adoption also checks the exact asset ID, PVP subtype, operation, scene,
and content agreement.

`SceneReady` is not an unversioned latch. Before the initial native scene
transition, the host issues epoch `1`. Each retained-content Restart advances
that epoch exactly once and broadcasts a bounded, retried
`SceneReadyRequest(epoch)`. A remote maps the request to the corresponding
monotonic local package-scene generation and returns `SceneReady(epoch)` only
after that exact generation passes template, spawn, companion, and scene
checks. If the remote replacement loads first, its readiness remains pending
until the host advances the epoch. If the host advances first, the request
targets a strictly newer remote generation. Duplicate requests resend the
current acknowledgement; stale, zero, overflowing, or out-of-phase epochs are
rejected. The frozen membership also retains exact connection-object identity,
so a replacement connection cannot inherit an acknowledgement by reusing its
numeric ID.

Content readiness has a 90-second bound. Scene/companion readiness, host owner
publication, remote owner adoption/readiness, owner-local player placement, and
host all-players-loaded have bounded windows. Networked PVE additionally waits
for every remote to validate the exact server-authored AI population before its
gameplay commit. A malformed/trailing envelope, collision,
mismatch, rejection, disconnect/join, timeout, preparation failure, or native
lifecycle exception stops the PVP path and enters the shipped host return or
remote disconnect before exact local teardown. Alive and KIA Restart retain
the already-proven content identity but clear and repeat the scene/native
barriers. Late join is unsupported: membership is immutable, and any join,
disconnect, or replacement connection aborts rather than extending or
repairing the roster. Protocol v6 enables this architecture for the BepInEx
networked-PVE candidate and leaves PVP fail-closed until its physical acceptance
matrix passes. Static agreement still does not prove scene physics, replicated
movement, projectile registration, damage, extraction, or restart behavior.

The `0.3.29` implementation and regression suite are `PROVEN-STATIC` only.
Before PVP is `SUPPORTED`, a real host and remote must prove exact content
transport, synchronized first spawn and movement, firearm-specific hit
registration, opposite sides, score/round/respawn, retained-content Restart,
and final return. A separately labeled multiplayer test archive does not close
those runtime gates.

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

## Injected Mirror game-mode baseline

ClassInjector attaches `StandalonePveGameMode` and `StandalonePvpGameMode`
through the generated `IntPtr` constructor. That wrapper does not invoke
`Mirror.NetworkBehaviour`'s native parameterless constructor. On the pinned
build, the resulting injected root therefore has `syncObjects=null`, while a
native `ExfilZone` or `RaidManager` has the constructor baseline: a non-null
empty `Il2CppSystem.Collections.Generic.List<Mirror.SyncObject>`. Mirror's
first observer calls `ClearAllDirtyBits`, which dereferences that list.

`0.3.25` supplies only the missing empty list on operation-owned runtime root
behaviours. It validates every root behaviour before both prefab registration
and `NetworkServer.Spawn`. The host records one native spawn attempt before
the synchronous call and fails closed instead of retrying a partially entered
spawn. Release calls `NetworkServer.UnSpawn`, unregisters only the deterministic
owned ID, and then destroys the root/list ownership. It never restores null or
calls `NetworkClient.ClearSpawners()`.

Release occurs in reverse ownership order. The framework stops stale
completions, removes only owned Mirror IDs, restores only captured globals,
destroys owned mode and render objects, unloads framework-owned bundles, and
clears generation state. A second launch must create a new generation. It must
not reuse player attempts, mode singletons, or map-ready flags from generation
one. Successful teardown preserves `GameManagerNetwork.SuccessfulOperation`
until the Operation Room consumes it.
