# Native PVE completion, extraction, and ATAK

This document specifies the implementation retained and tightened in OPERATOR:
Modded Operations `0.3.28`. It uses the shipped OPERATOR PVE lifecycle and does
not add a second mission-completion system.

The source of record is:

```text
src/OperatorModdedOperations/CerberusNativeTabFix.cs
```

The important members are:

| Member | Purpose |
| --- | --- |
| `ValidateStandaloneSceneContract` | Reject a PVE scene that does not contain exactly one valid `PVE_ExfilZone_` marker. |
| `ConfigureStandalonePveController` | Copy the map-owned extraction transform and trigger to the network-owned PVE bootstrap. Create the shipped `ExfilZone` and `RaidManager`. |
| `CreateNativeAtakExfilMarker` | Reconstruct the current-build vanilla ATAK exfil visual from resident runtime resources. |
| `ResetStandalonePveExtractionState` | Clear old extraction occupants and reset all extraction SyncVars before a new operation. |
| `TrySpawnStandalonePveEnemies` | Create the map population through `RaidManager.ServerSpawnAI(false)` and restore the map-local exfil list after the native call. |
| `ProcessPendingStandalonePveTeamValidation` | Keep extraction locked while exact owned brains complete native startup, then validate their full team and target contract. |
| `SuppressStandalonePveExtraction` | Clear and lock both zone-level and global extraction state while validation is pending or failed. |
| `DestroyOwnedStandalonePvePopulation` | Destroy only exact Mirror identity references captured around the native population call. |
| `ReleaseStandaloneGameMode` | Remove owned Mirror registrations and runtime ATAK assets. Preserve a successful-operation result for the Operation Room. |

## Ownership boundary

The map bundle owns geometry. The framework owns the network lifecycle.

The map scene MUST contain one inactive marker with this contract:

```text
name: PVE_ExfilZone_00
component: UnityEngine.Transform
component: UnityEngine.BoxCollider
BoxCollider.isTrigger: true
BoxCollider.size: every axis is greater than zero
```

The marker transform is the extraction origin. The collider `center` and
`size` are local to that transform. The map MUST place the marker on the real
terrain surface. The map MUST keep the complete trigger inside the playable
wall.

The map MUST NOT add its own active `RaidManager`, `ExfilZone`, completion
timer, success popup, or ATAK screen. Those objects would compete with the
network owner that Modded Operations creates.

Modded Operations creates one Mirror-owned
`StandalonePveGameMode : InfiltrationManager`. It adds the following shipped
components to the same bootstrap object:

```csharp
var exfil = bootstrapRoot.AddComponent<ExfilZone>();
var raid = bootstrapRoot.AddComponent<RaidManager>();
raid.infiltrationManager = pve;
raid.exfilZones = new Il2CppSystem.Collections.Generic.List<ExfilZone>();
raid.exfilZones.Add(exfil);
RaidManager.singleton = raid;
```

The framework does not copy an `ExfilZone` from another scene. It uses the
package marker only as validated scene data. This rule prevents a donor scene
from becoming a hidden runtime dependency.

## Scene validation

`ValidateStandaloneSceneContract` applies these PVE checks before gameplay
starts:

```csharp
List<Transform> exfilMarkers = FindSceneMarkers(
    scene,
    StandalonePveExfilMarkerPrefix);
if (exfilMarkers.Count != 1)
    return false;

BoxCollider authoredExfil = exfilMarkers[0].GetComponent<BoxCollider>();
if (authoredExfil == null || !authoredExfil.isTrigger ||
    authoredExfil.size.x <= 0f || authoredExfil.size.y <= 0f ||
    authoredExfil.size.z <= 0f)
    return false;
```

Zero markers are invalid because a StandardPVE raid would have no completion
route. More than one marker is invalid because the package schema does not
yet declare selection or availability rules for multiple extraction zones.

## Initial locked state

`ConfigureStandalonePveController` copies the authored transform and collider
to the network bootstrap. It then initializes the zone as locked:

```csharp
exfil.NetworkPlayersInExfil = 0;
exfil.PlayersInExfil = 0;
exfil.NetworkcanExtract = false;
exfil.canExtract = false;
exfil._occupants = new Il2CppSystem.Collections.Generic.HashSet<int>();

manager.NetworkPlayersInAnyExfil = 0;
manager.PlayersInAnyExfil = 0;
manager.NetworkcanExtract = false;
manager.canExtract = false;
manager.NetworkisExtracting = false;
manager.isExtracting = false;
manager.NetworkextractionStartTime = 0d;
manager.extractionStartTime = 0d;
manager.ExfilTime = 15f;
manager.SuccessfulOperation = false;
```

These are two related state sets. `ExfilZone` owns zone occupancy and the
zone-level unlock state. `GameManagerNetwork` owns global extraction
occupancy, the active countdown, and the successful-operation result. A mod
must reset both state sets.

An insertion and an extraction MAY use the same physical area. Initial
occupancy does not complete the operation because both `NetworkcanExtract`
values are false. When the native raid later unlocks extraction, the shipped
trigger and global timer use the player's real occupancy.

## Enemy death and native unlock

The framework does not count destroyed GameObjects to decide victory. It
creates enemies through the native path:

```csharp
gameManager.botAmount = targetCount;
gameManager.botHVTAmount = 0;
raid.ServerSpawnAI(false);
```

The shipped AI `Health.UserCode_Die` path directly invokes
`RaidManager.UpdateAICount`. That call is independent of the utility
`RaidManager` component's disabled `Update` loop. Native code removes or
invalidates the dead actor in `GameManager.allAI`, sets its one-shot
enemy-dead state, and enables extraction when the global population reaches
zero.

The synchronous spawn call does not prove that `BrainAI.Start` has already
registered each identity in `GameManager.allAI`. Modded Operations therefore
requires an empty raw global list, captures the exact `NetworkServer.spawned`
delta immediately, keeps extraction locked, and validates the full native
team/reference/pool/list contract from the next Unity frame through a 60-frame
deadline. Missing, extra, vanished, or mismatched exact-owned entries fail
closed and are destroyed before the game mode is released. The framework never
removes entries directly from `allAI` or team target lists; native component
teardown owns those lists.

`RaidManager.ServerSpawnAI(false)` can repopulate `raid.exfilZones` from
persistent objects that exist in `Resources`. A standalone map must finish
with its own serialized-equivalent list. Modded Operations therefore restores
the list immediately after the native population call:

```csharp
raid.exfilZones = new Il2CppSystem.Collections.Generic.List<ExfilZone>();
raid.exfilZones.Add(operation.PveExfilZone);
```

This reset is not a custom unlock. It removes stale donor-zone references and
leaves exactly one map-owned `ExfilZone` for the shipped raid code to unlock.

The accepted runtime contract is:

```text
OperationsManager.NetworkCurrentGameMode = StandardPVE
OperationsManager.IsSimulation = false
RaidManager.singleton = the current network bootstrap RaidManager
RaidManager.exfilZones.Count = 1
GameManager.allAI.Count > 0 at mission start
ExfilZone.NetworkcanExtract = false at mission start
GameManagerNetwork.NetworkcanExtract = false at mission start
GameManager.allAI.Count = 0 after all native Health deaths
ExfilZone.NetworkcanExtract = true after the native raid update
GameManagerNetwork.NetworkcanExtract = true after the native raid update
```

Do not patch `CompleteOperation` to make an enemy-elimination operation end
immediately. Vanilla StandardPVE requires the player to use extraction after
the combat condition is complete.

## Physical extraction and the success screen

The extraction trigger records network connection IDs in
`ExfilZone._occupants`. The zone publishes `NetworkPlayersInExfil`.
`GameManagerNetwork` publishes `NetworkPlayersInAnyExfil` and
`NetworkisExtracting`.

When a living player occupies an unlocked zone, the shipped global timer uses:

```text
GameManagerNetwork.ExfilTime = 15 seconds
GameManagerNetwork.NetworkextractionStartTime = server time at countdown start
GameManagerNetwork.GetRemainingExfilSeconds() = remaining countdown
```

When the countdown completes, the shipped flow calls its successful-operation
path. It unloads the additive map scene and displays
`GameManagerNetwork.missionCompletedPopup`. The After Action Report contains
the shipped mission result, elapsed time, total kills, living operators, and
rating. The shipped Continue control returns the player to the Operation
Room.

The framework MUST NOT clear `GameManagerNetwork.SuccessfulOperation` during
successful scene teardown. The Operation Room reads that result after the map
scene unloads. `ReleaseStandaloneGameMode` resets extraction state only when
`SuccessfulOperation` is false.

## Exact ATAK exfil visual

The current-build vanilla reference is the `level16` scene. Its extraction
marker is not an `atakMarker` component. It is a child GameObject that the
serialized `ExfilZone.ExfilMarker` field activates.

The audited source objects are:

| Resource | Installed reference |
| --- | --- |
| GameObject | `ATAK Exfil Marker` in `level16`; layer `17`; initially inactive |
| Mesh | `sharedassets3`, path ID `994`, name `Marker` |
| Material | `sharedassets3`, path ID `228`, name `ExfilZone` |
| Texture | `sharedassets1`, path ID `613`, name `ExfilZone`, `512 x 512`, DXT5 |
| Shader | Resident `HDRP/Unlit` |

The exact local transform is:

```text
position = (0, 0, 0)
rotation = (-0.00000030159049, -0.70710683, -0.70710677, 0.00000032782552)
scale = (0.65, 0.65, 0.65)
```

The exact quad data is:

```csharp
vertices = new[] {
    new Vector3(-9.59999943f, -5.40000010f, 0f),
    new Vector3(-9.59999943f,  5.40000010f, 0f),
    new Vector3( 9.59999943f,  5.40000010f, 0f),
    new Vector3( 9.59999943f, -5.40000010f, 0f)
};
uv = new[] {
    new Vector2(0.000227630138f, 0.437887728f),
    new Vector2(0.000227630138f, 1.00013173f),
    new Vector2(0.999772370000f, 1.00013173f),
    new Vector2(0.999772370000f, 0.437887728f)
};
triangles = new[] { 2, 1, 0, 3, 2, 0 };
```

`CreateNativeAtakExfilMarker` finds the globally resident vanilla texture by
exact name and dimensions. It creates a fresh mesh and material. It uses
render queue `2501`, texture offset `(0,-0.22)`, white color, opaque surface,
Z-write enabled, back-face culling, and no alpha-test keyword. The method then
assigns this GameObject to `ExfilZone.ExfilMarker` and leaves it inactive.
The shipped `RaidManager.EnableGlobalExtraction`/`ExfilZone.SetMarker` flow
activates it when extraction becomes available.

This technique does not redistribute the vanilla texture. It binds the
resident texture from the installed game. It also avoids a donor scene and
keeps ATAK behavior in the generic framework.

Every runtime-created mesh and material is recorded in
`ActiveMapOperation.RuntimePveAssets`. Teardown destroys only those owned
objects.

## Map-author example: Ukrainian Forest

Ukrainian Forest package `0.3.21` authors:

```text
PVE_ExfilZone_00 root = (0.000, 0.112, 7.000)
BoxCollider.center = (1.3259258, 2.066852, 1.6703243)
BoxCollider.size = (25.236944, 7.376298, 15.531027)
timer = 15 seconds in the framework/native manager
```

The world-space horizontal bounds are:

```text
x = -11.2925462 through 13.9443978
z = 0.9048108 through 16.4358378
```

The zone covers the northern Team 1/PVE insertion groups at local `z=7` and
`z=12`. The southern ordinary enemy markers remain at `z>=80`. The player
starts at the north, clears the map, and extracts by returning north.

These coordinates are map-specific. A different map must measure and author
its own transform. The component and lifecycle contract is generic.

## Verification evidence

The accepted current-build run records the following ordered events:

```text
SMOKE_PVE_COMPLETION_NATIVE_CONTRACT ... mode=StandardPVE, simulation=false, raidExfils=1, prematureUnlock=false
SMOKE_PVE_ALL_AI_DEAD remainingAI=0
SMOKE_PVE_NATIVE_EXTRACTION_UNLOCKED zone=true, global=true, marker=active=True,layer=17,mesh=Marker,material=ExfilZone,shader=HDRP/Unlit,texture=ExfilZone
SMOKE_PVE_NATIVE_EXTRACTION_TIMER_STARTED zonePlayers=1, globalPlayers=1, configuredSeconds=15.0
SMOKE_PVE_NATIVE_MISSION_COMPLETE_POPUP successfulOperation=true
```

The release evidence for the insertion-area extraction is under:

```text
<workspace>/reports/observer_qa/forest_20260805_215437
<workspace>/reports/observer_qa/forest_20260805_220738
```

Both runs reached the native Mission Successful After Action Report. The QA
driver used native Health damage to shorten the test. OPERATOR correctly
reported that automated action as cheating and did not save the score. That
message is a property of the private test, not normal gameplay.

## Failure rules

| Symptom | Check |
| --- | --- |
| Mission ends at insertion before combat | Confirm both initial `NetworkcanExtract` values are false and extraction state was reset. |
| All AI are dead but no extraction appears | Confirm enemies used native `Health` and `RaidManager.ServerSpawnAI(false)`. Confirm `raid.exfilZones` contains exactly the current zone after that call. |
| Countdown never starts | Confirm a live network-owned player is inside the real BoxCollider and both zone/global unlock values are true. |
| ATAK has no exfil icon | Confirm the resident `ExfilZone` texture is `512 x 512`, layer is `17`, the mesh has four vertices, and the material uses `HDRP/Unlit`. |
| Restart loops at `MAP LOADED !BUG!` | Remove the old Mirror prefab by deterministic asset ID and unregister its spawn handler during teardown. |
| Success screen appears but the next operation is corrupted | Preserve `SuccessfulOperation` during successful unload. Clear only map-owned singletons, Mirror IDs, occupants, and runtime assets. |

Do not add a fallback that calls the success RPC when any of these contracts
fail. Fail closed and fix the missing native state.
