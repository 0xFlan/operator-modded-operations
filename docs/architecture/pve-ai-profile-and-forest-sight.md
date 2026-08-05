# Fixed PVE AI profile and vegetation sight

## Scope and ownership

This document describes the schema-v2 fixed PVE AI profile in Modded
Operations `0.3.19`. It also describes the interface that a dense map uses to
make native vegetation block AI sight.

Modded Operations owns the generic profile parser result and the native
`BotSpawnDetails` write. A map package owns the values. A map companion owns
the activation of map-specific vegetation colliders. This split keeps map
names, coordinates, terrain sizes, and asset names out of the generic
framework.

The implementation has no difficulty selector. It does not modify a vanilla
operation, PVP, or a PVE operation from a different package. A schema-v1 map
continues to use the framework legacy values.

## Exact files and source members

| Item | Exact location or member |
| --- | --- |
| Public profile type | `OperatorModAPI.ModdedPveAiProfileDefinition` |
| Package parser | `OperatorModAPI.ModdedOperationsPackageLoader` |
| Schema | `schemas/operator-map-package-v2.schema.json` |
| PVE population owner | `CerberusNativeTabFix.TrySpawnStandalonePveEnemies` |
| Marker settings | `CerberusNativeTabFix.ConfigureStandaloneBotDetails` |
| Profile diagnostic text | `CerberusNativeTabFix.FormatPveAiProfile` |
| Spawned-bot contract capture | `CerberusNativeTabFix.StartProfiledPveAiDiagnostics` |
| Bounded runtime sampler | `CerberusNativeTabFix.ProcessProfiledPveAiDiagnostics` |
| Movement and sight report | `CerberusNativeTabFix.LogProfiledPveAiSnapshot` |
| Native population call | `RaidManager.ServerSpawnAI(false)` |
| Native settings call | `RaidManager.ApplyBotSpawnSettings(GameObject, BotSpawnDetails)` |
| Native idle movement | `BrainAI.Wander(float)` |
| Native line of sight | `EyesAI.TestIfCanSeeAtHeight(...)` |

Use `<OPERATOR_INSTALL>` for the game directory. Do not put a personal user
name or local drive layout in a public report.

## Schema-v2 object

The PVE operation can contain this closed object:

```json
"pveAiProfile": {
  "id": "woodland-balanced-v1",
  "detectionRangeMeters": 45.0,
  "fieldOfViewDegrees": 90.0,
  "maximumEffectiveRangeMeters": -1.0,
  "wanderDistanceMeters": 38,
  "useComms": true,
  "counterSuppression": false
}
```

The loader rejects an unknown property. It also rejects these errors:

- a profile in schema version 1;
- a profile on PVP;
- an ID that is not a local ID or is longer than 64 characters;
- `detectionRangeMeters` outside 5 through 250;
- `fieldOfViewDegrees` outside 30 through 180;
- `maximumEffectiveRangeMeters` that is not `-1` and is outside 5 through
  300;
- non-integer `wanderDistanceMeters`, or a value outside 5 through 100.

The loader creates a new immutable `ModdedPveAiProfileDefinition`. It does not
retain the JSON object. The frozen operation catalog is root-independent and
cannot be changed after Core startup.

## Generic application order

`TrySpawnStandalonePveEnemies` performs this sequence on the network server:

1. It finds and ordinal-sorts `PVE_EnemySpawn_...` transforms.
2. It filters `GameManager.AllAITypes` for a root `BrainAI`, a root
   `NetworkIdentity`, and a `WeaponsAI` with `SpawnWeapon == true` and a
   nonempty `weaponList`.
3. It creates one scene-owned disabled `RaidManager` utility.
4. It creates or reuses one `BotSpawnDetails` on each marker.
5. It calls `ConfigureStandaloneBotDetails(details, pveAiProfile)`.
6. It puts the marker GameObjects in `RaidManager.botSpawnPoints`.
7. It calls `RaidManager.ServerSpawnAI(false)`.

The exact profile write is:

```csharp
details.DetectionRange = profile?.DetectionRangeMeters ?? 72f;
details.FOV = profile?.FieldOfViewDegrees ?? 105f;
details.maxEffectiveRange = profile?.MaximumEffectiveRangeMeters ?? 90f;
details.useComms = profile?.UseComms ?? true;
details.DoesCounterSuppression = profile?.CounterSuppression ?? true;
details.WanderDistance = profile?.WanderDistanceMeters ?? 18;
```

The null side of each expression is the schema-v1 compatibility path. Do not
replace it with the values of one map.

The framework also writes `DetectionTimeMultiplier = 1` and
`HearingRange = 20` when a profile exists. These are reserved baseline values.
The current native `RaidManager.ApplyBotSpawnSettings` body does not read
those two fields. They are not working controls in this game build.

## Exact native field transfer

The current Windows x86-64 `RaidManager.ApplyBotSpawnSettings` body starts at
RVA `0x009D9E30`. The important writes are:

| Marker field | Source object offset | `BrainAI` offset | Meaning |
| --- | ---: | ---: | --- |
| `DoesCounterSuppression` | `0x41` | `0x0C1` | Enables or disables native counter-suppression response. |
| `maxEffectiveRange` | `0x3C` | `0x2FC` | Overrides the native effective range unless the value is `-1`. |
| `WanderDistance` | `0x44` | `0x1C8` | Maximum native idle-search destination radius. |
| `FOV` | `0x38` | `0x204` | Native horizontal vision angle. |
| `DetectionRange` | `0x30` | `0x1DC` and `0x200` | Live and original detection range. |
| `useComms` | `0x40` | `0x088` | Native AI communications. |

The same body copies crouch/prone settings, patrol data, and navmesh-disable
state. It does not read marker offsets `0x2C` or `0x34`, which are
`DetectionTimeMultiplier` and `HearingRange`.

The framework must use `RaidManager.ServerSpawnAI(false)`. The current native
method instantiates a registered AI prefab, calls
`NetworkServer.Spawn(bot, GameManager.instance.gameObject)`, and then calls
`ApplyBotSpawnSettings`. A manual ownerless spawn can create grenade behavior
without a correct firearm lifecycle.

## Native wander behavior

The current `BrainAI.Wander(float)` body starts at RVA `0x008D7900`.
`BrainAI` has these current instance offsets:

| Field | Offset |
| --- | ---: |
| `Patience` | `0x1A0` |
| `WanderDistance` | `0x1C8` |
| `WanderTimer` | `0x1CC` |
| `wanderTime` | `0x4F4` |

The native method adds `dt` to `wanderTime`. It waits while:

```text
wanderTime < WanderTimer * Patience
```

After the wait, it calls the native equivalent of:

```csharp
Vector3 destination = RandomNavSphere(
    agent.transform.position,
    5f,
    WanderDistance);
agent.destination = destination;
wanderTime = 0f;
```

The method uses the current position, not the original spawn position. A bot
can therefore search farther after more than one interval. The profile keeps
the prefab-owned `WanderTimer` and `Patience`. It changes only the radius.
This preserves the native initial delay and avoids an immediate synchronized
rush at scene start.

For map tuning, calculate every horizontal player-marker to enemy-marker
distance. Set the radius below one half of the minimum distance when one
search move must not cross the midpoint. Also test the median. A random move
can go in any direction, so radius alone does not create a scripted advance.

## Vision range is not foliage occlusion

`DetectionRange` and `FOV` limit target acquisition. They do not make a leaf
mesh opaque to physics. A dense map needs native line-of-sight blockers.

In the current build, `EyesAI.DetectionLayerMask` is decimal `266689`. The
mask includes layer 18, named `AI_VisionBlock`. The current
`EyesAI.TestIfCanSeeAtHeight` method performs a `Physics.Linecast` with that
mask. Sight succeeds only when the first accepted hit belongs to the target
root.

OPERATOR sets `Physics.queriesHitTriggers` to true. Therefore, a trigger
collider on `AI_VisionBlock` can stop the AI vision linecast. The current
layer-collision matrix does not make layer 18 a player wall, and the audited
bullet mask excludes layer 18. This lets foliage block sight without creating
an invisible ballistic barrier.

Do not add arbitrary large box colliders to every tree. Use the collider that
the native vegetation prefab supplies when possible. Verify all of these
properties:

- the child has the exact audited name;
- the collider type and dimensions match the native prefab;
- the GameObject is on layer 18;
- `isTrigger` is true;
- the collider and its GameObject are enabled before the operation becomes
  ready;
- the object is below the exact loaded map root;
- the total active count equals the authored count.

## Dense-map tuning procedure

1. Measure the collision and bullet-interaction volume. Do not use a larger
   visual terrain apron.
2. Measure all player-to-enemy marker distances in the horizontal plane.
3. Record minimum, 25th percentile, median, mean, 75th percentile, and
   maximum.
4. Select a detection range below the initial minimum. Leave enough distance
   for the player to move from the insertion point before direct acquisition.
5. Select a field of view that requires a plausible facing direction. A
   90-degree value gives a 45-degree half-angle.
6. Set a one-move wander radius below half of the minimum initial gap if one
   move must stay on the enemy side. Keep the native time delay unless exact
   evidence shows a defect.
7. Keep communications on if the design needs groups to search after one AI
   has a valid contact. Turn counter-suppression off if blind reaction fire
   through vegetation is too aggressive.
8. Preserve native weapon accuracy unless a separate exact test proves that
   an accuracy field is stable and map-owned. Projectile patching can break
   reciprocal firearm damage.
9. Enable and count map-owned vegetation sight blockers before readiness.
10. Test 10 through 15 enemies, day and night, first launch, restart, quit,
    and repeat launch.

## Required runtime diagnostics

The framework log must contain one line in this form:

```text
Standalone PVE released a server-owned AI population through shipped RaidManager.ServerSpawnAI: count=<N>, requestedRange=10-15, chosen=<N>, markers=<M>, firearmCapablePrefabs=<P>, aiProfile=<ID>(range=45.0m,fov=90.0,maxEffective=-1.0m,wander=38m,comms=True,counterSuppression=False).
```

When a PVE operation has `pveAiProfile`, the framework also starts one
read-only 120-second diagnostic. The gate is the presence of a profile. The
generic source does not contain a map ID. Schema-v1 PVE, PVP, and vanilla
operations do not enter this path.

Before `RaidManager.ServerSpawnAI(false)`, the framework records the instance
IDs already in `GameManager.allAI`. After the native call returns, it tracks
only new `BrainAI` IDs. This prevents an old scene actor from entering the
report. It does not write a `BrainAI`, `EyesAI`, navigation agent, weapon, or
target field.

The first line reports the values on the live spawned bots, not only the JSON
inputs:

```text
Profiled PVE native AI contract: operation=<operationId>, profile=<profileId>, source=spawn, brains=<N>, nativeInitialWanderDelay=<min>..<max>s, detection=<min>..<max>m, fov=<min>..<max>, wander=<min>..<max>m, comms=<enabled>/<N>.
```

`nativeInitialWanderDelay` is the live product
`BrainAI.WanderTimer * BrainAI.Patience`. A zero minimum is a warning. Do not
claim delayed search from the native method body alone. Require a positive
live value and physical movement evidence.

The framework takes snapshots at 0, 10, 30, 60, 90, and 120 seconds. Each
snapshot reports:

- live tracked bots;
- bots at least 1 m from their captured spawn position;
- bots at least 5 m closer to the captured insertion position;
- mean and maximum horizontal displacement;
- bots with a non-null `CurrentSeenTarget`;
- the `BrainAI.CurrentState` counts;
- one read-only linecast from each bot eye to the player with that bot's
  actual `EyesAI.DetectionLayerMask`.

The sight probe groups first hits as layer-18 vegetation, other geometry, or
clear/player. It is geometry evidence. It is not a replacement for the
native `CurrentSeenTarget` field or the physical reciprocal-firearm test.
The exact line is:

```text
Profiled PVE AI snapshot: operation=<operationId>, profile=<profileId>, scheduled=<S>s, elapsed=<T>s, live=<N>, moved>=1m=<N>, movedTowardInsertion>=5m=<N>, movementMean=<M>m, movementMax=<M>m, actualSeenTarget=<N>, sameMaskSightProbe(vegetation=<N>,other=<N>,clearOrPlayer=<N>), states=<stateCounts>.
```

The map companion must log the blocker count. A dense map must reject its
scene when its exact authored blocker count is incomplete. A successful
static build is not a runtime acceptance result.

## Acceptance tests

The candidate passes static acceptance only when:

- schema v1 rejects `pveAiProfile`;
- schema v2 accepts the exact PVE profile and freezes all fields;
- PVP rejects the object;
- the public API baseline includes `WanderDistanceMeters`;
- the generic source has no map ID, map display name, coordinate, terrain
  size, or vegetation asset name;
- the map companion activates blockers before its `applied = true` state;
- build, schema, validator, repository, and package checks pass.

Runtime acceptance also requires a new game process. Verify that the player
is not detected at the insertion point, an unobstructed target inside 45 m can
be acquired, a target behind audited dense foliage loses line of sight, bots
begin native delayed search movement, firearms work in both directions, and
PVP/vanilla behavior is unchanged.
