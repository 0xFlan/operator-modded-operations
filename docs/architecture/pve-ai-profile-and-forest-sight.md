# Fixed PVE AI profile and vegetation sight

## Scope and ownership

This document describes the schema-v2 fixed PVE AI profile in Modded
Operations `0.3.28`. It also describes the interface that a dense map uses to
make native vegetation block AI sight.

The `0.3.28` publication retains the optional native reaction controls and
mandatory native-team cohort boundary introduced in `0.3.27`. It adds exact
Mirror ownership and bounded deferred validation for the native population.
The `0.3.27` publication remains hash-pinned historical comparison evidence;
final live acceptance of `0.3.28` remains a separate gate.

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
| Optional first-wander cap | `CerberusNativeTabFix.TryApplyProfiledPveInitialWanderDelayCap` |
| Hostile cohort selection | `CerberusNativeTabFix.TrySelectStandalonePvePrefabCohort` |
| Exact Mirror population capture | `CerberusNativeTabFix.CaptureOwnedStandalonePveServerPopulation` |
| Deferred startup validation | `CerberusNativeTabFix.ProcessPendingStandalonePveTeamValidation` |
| Spawned-team validation | `CerberusNativeTabFix.TryValidateStandalonePveSpawnedTeamContract` |
| Exact population cleanup | `CerberusNativeTabFix.DestroyOwnedStandalonePvePopulation` |
| Optional reaction cap | `CerberusNativeTabFix.TryApplyProfiledPveMaximumReactionTimeCap` |
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
  "initialWanderDelayMaxSeconds": 12.0,
  "reactionDisposition": "offensive",
  "maximumReactionTimeSeconds": 0.25,
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
- non-integer `wanderDistanceMeters`, or a value outside 5 through 100;
- present `initialWanderDelayMaxSeconds` outside 2 through 60, or explicit
  null. Omitting the property is valid and preserves prior behavior.
- present `reactionDisposition` other than exact lowercase `defensive`,
  `offensive`, or `random`, or explicit null. Omission preserves the native
  marker disposition without a framework write;
- present `maximumReactionTimeSeconds` outside 0.10 through 1.50, a non-finite
  value, or explicit null. Omission preserves native reaction timing without
  a framework write.

The loader creates a new immutable `ModdedPveAiProfileDefinition`. It does not
retain the JSON object. The frozen operation catalog is root-independent and
cannot be changed after Core startup.

## Generic application order

`TrySpawnStandalonePveEnemies` performs this sequence on the network server:

1. It finds and ordinal-sorts `PVE_EnemySpawn_...` transforms.
2. It resolves the live player's `TeamIdentifier.NetworkTeamID`. If native
   player startup has not resolved it yet, no raid or AI state is changed and
   the existing bounded maintenance path retries. No player team is hardcoded.
3. It filters `GameManager.AllAITypes` for a root `BrainAI`, a root
   `NetworkIdentity`, and a `WeaponsAI` with `SpawnWeapon == true` and a
   nonempty `weaponList`.
4. Every eligible dormant donor must also have a root `TeamIdentifier`,
   non-null `StartingTeamStats` with valid `ThisTeamId`, and a root
   `TeamIdentifierReference` component. Awake-owned back-pointers and target
   pools are deliberately not required on an inactive prefab. Donors are
   grouped by both exact stats-object identity and numeric team ID. The player
   team is excluded, and only a unique strict-largest hostile cohort may
   continue.
5. It requires the global native `GameManager.allAI` raw list count to be zero,
   so stale null or duplicate entries cannot later block vanilla
   all-enemies-dead completion.
6. It creates one scene-owned disabled `RaidManager` utility.
7. It creates or reuses one `BotSpawnDetails` on each marker and calls
   `ConfigureStandaloneBotDetails(details, pveAiProfile)`.
8. It snapshots `NetworkServer.spawned`, supplies only the selected cohort to
   `RaidManager.standardAI`, calls synchronous
   `RaidManager.ServerSpawnAI(false)`, restores the prior array, and captures
   every new registry identity even when native spawning throws.
9. The exact identity and root-`BrainAI` delta must equal the requested count.
   Extraction stays locked while validation probes run every Update from the
   next frame through a 60-frame deadline. Every registered brain must bind the
   stored netId, stored identity reference, current Mirror registry entry, and
   exact root.
10. After all exact owned brains enter `GameManager.allAI`, it validates every
   identifier/reference/pool/stats/team state and proves the cohort is absent
   from enemy and possible-target collections.
   Only an exact serialized selected donor whose SyncVar is unresolved may be
   repaired through `TeamIdentifier.NetworkTeamID`. Any other mismatch removes
   the new population and fails closed. Global friendly fire and native target
   lists are never edited.
11. If the frozen profile declares `initialWanderDelayMaxSeconds`, it advances
   only the newly added native Wander brains' first clocks and starts the
   read-only diagnostic window.
12. If it declares `maximumReactionTimeSeconds`, it caps only each new brain's
    `_baseReactionTime` and `ReactionTime` on the server, without raising a
    faster native value or changing difficulty, reaction progress, targets, or
    states.

The exact profile write is:

```csharp
details.DetectionRange = profile?.DetectionRangeMeters ?? 72f;
details.FOV = profile?.FieldOfViewDegrees ?? 105f;
details.maxEffectiveRange = profile?.MaximumEffectiveRangeMeters ?? 90f;
details.useComms = profile?.UseComms ?? true;
details.DoesCounterSuppression = profile?.CounterSuppression ?? true;
details.WanderDistance = profile?.WanderDistanceMeters ?? 18;
if (profile != null)
    details.idleState = BrainAI.IdleStates.Wander;
if (profile?.ReactionDisposition != null)
    details.reactType = /* exact native Defensive, Offensive, or Random */;
```

The null side of each expression is the schema-v1 compatibility path. Do not
replace it with the values of one map.

The framework also writes `DetectionTimeMultiplier = 1` and
`HearingRange = 20` when a profile exists. These are reserved baseline values.
The current native `RaidManager.ApplyBotSpawnSettings` body does not read
those two fields. They are not working controls in this game build.

The same native settings body copies `BotSpawnDetails.reactType` to
`BrainAI.reactType`. The framework writes it only for an explicitly declared
profile value. `maximumReactionTimeSeconds` is not a marker field: after
native `Awake` has established the brain's baseline, the authoritative server
writes the lower of that baseline and the declared cap to `_baseReactionTime`,
then writes the lower of current reaction time and that result to
`ReactionTime`. Those are the only two writes in the optional cap path.

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
| `reactType` | `0x24` | `0x2D8` | Native defensive, offensive, or random response disposition. |

The same body copies crouch/prone settings, patrol data, and navmesh-disable
state. It does not read marker offsets `0x2C` or `0x34`, which are
`DetectionTimeMultiplier` and `HearingRange`.

The body also copies `BotSpawnDetails.idleState` at offset `0x20` to
`BrainAI.idleStates` at offset `0x2D4`. Native
`BrainAI.UpdateStateMachine(float)` reads `CurrentState` at offset `0x2D0`.
When that state is `Idle`, it dispatches by `idleStates`: `Idle` calls
`BrainAI.Idle()`, `Wander` calls `BrainAI.Wander(dt)`, and `Patrol` calls
`BrainAI.Patrol(dt)`. Therefore, `WanderDistance=38` without
`idleState=Wander` leaves every bot stationary. Modded Operations writes the
vanilla `Wander` marker choice only when a schema-v2 PVE profile exists.

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
the prefab-owned `WanderTimer` and `Patience`. The optional initial-delay cap
does not replace either field. When absent, the framework never writes
`wanderTime`. When present, the authoritative server handles each newly
spawned operation brain once, refuses responding or non-Wander brains, and
advances only `wanderTime` until 50–100% of the cap remains. The stagger is
FNV-1a-derived and does not consume Unity's process-global random state. The
first native destination resets `wanderTime` to zero, so every later cycle is
the complete prefab-authored cadence.

## Native navigation owner and the correct movement position

Do not measure the `BrainAI` root to decide whether an OPERATOR bot moved.
The extracted shipped prefab
`Assets/GameObject/BOT V2.prefab` uses two different hierarchy nodes:

| Prefab node | Serialized file ID | Required components |
| --- | ---: | --- |
| `BOT V2` root | `1709254077376921` | `BrainAI`, root network identity, and the authoritative network-owner state |
| `SK_Insurgent_P8` child | `1730242686860875` | `AgentController` and enabled `Pathfinding.FollowerEntity` |

The `BrainAI` root can remain at its network-owner position while the model
child follows an A* path. The rejected diagnostic used
`brain.transform.position`, observed `0.00 m`, and incorrectly concluded
that native wander was stopped. The accepted diagnostic uses the live native
controller when its entity exists:

```csharp
private static Vector3 GetProfiledPveNavigationPosition(BrainAI brain)
{
    if (brain == null)
        return Vector3.zero;
    try
    {
        AgentController controller = brain.agent;
        if (controller != null && controller.entityExists)
            return controller.position;
    }
    catch
    {
        // The entity can register or unregister while a snapshot is taken.
    }
    return brain.transform.position;
}
```

`AgentController.position`, `velocity`, `destination`, `hasPath`, and
`pathPending` are the native navigation evidence. The root position is only a
bounded fallback during entity registration or teardown.

The path service also requires the same co-located pair that a vanilla map
uses. In the extracted shipped scene `Assets/Scenes/level16.unity`, GameObject
file ID `141959` is named `Astar Navmesh`. It contains both:

- `AstarPath`, script GUID `1e4c63e1f2966ee0f81106971d67c21e`;
- enabled `Pathfinding.RVO.RVOSimulator`, script GUID
  `22186c4d47c31b5848d7d9a4f063bae1`.

A standalone map companion must publish the scanned `AstarPath` before
`RaidManager.ServerSpawnAI(false)` and must reuse or create the enabled,
co-located `RVOSimulator`. Do not write bot transforms each frame. The
vanilla `FollowerEntity` owns path search, local avoidance, and movement.

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

The framework log must first record the exact captured population and pending
native-startup window in this form:

```text
Standalone PVE issued an exact server-owned AI population through shipped RaidManager.ServerSpawnAI; native BrainAI.Start validation is pending: count=<N>, requestedRange=10-15, chosen=<N>, markers=<M>, firearmCapablePrefabs=<P>, selectedCohortPrefabs=<C>, playerTeam=<T>, hostileTeam=<T>, ownedNetIds=<N>, issuedFrame=<F>, earliestFrame=<F+1>, deadlineFrame=<F+60>, aiProfile=<ID>(...).
```

After every exact owned brain registers and the team/target closure passes,
the log must contain:

```text
Standalone PVE released its exact server-owned AI population after deferred native startup validation: count=<N>, validationFrame=<F>, issuedFrame=<F>, rawAllAI=<N>, teamContract=<summary>.
```

When a PVE operation has `pveAiProfile`, the framework also starts one
read-only 120-second diagnostic. The gate is the presence of a profile. The
generic source does not contain a map ID. Schema-v1 PVE, PVP, and vanilla
operations do not enter this path.

Before `RaidManager.ServerSpawnAI(false)`, the framework snapshots Mirror's
authoritative `NetworkServer.spawned` netIds. After the synchronous call, it
owns only the exact new netId/identity/root-`BrainAI` delta. The diagnostic
does not start until the bounded deferred validator binds every one of those
same brains in `GameManager.allAI` and accepts the complete native team and
target closure. It then resets time zero and captures initial positions before
emitting the live contract. This prevents a false zero-bot contract, a
zero-bot time-zero snapshot, or an unrelated actor entering the report. It
does not write a `BrainAI`, `EyesAI`, navigation agent, weapon, or target
field.

The first line reports the values on the live spawned bots, not only the JSON
inputs:

```text
Profiled PVE native AI contract: operation=<operationId>, profile=<profileId>, source=<spawn|native-network-spawn>, brains=<N>, nativeInitialWanderDelay=<min>..<max>s, detection=<min>..<max>m, fov=<min>..<max>, wander=<min>..<max>m, idleWander=<N>/<N>, comms=<enabled>/<N>.
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
`CurrentSeenTarget` can refer to any target known to that bot. The
`actualSeenTarget` count does not prove that the target is the local player.
Correlate it with the same-mask player probe, distance, bot state, and a
physical play observation before making a player-detection claim.
The exact line is:

```text
Profiled PVE AI snapshot: operation=<operationId>, profile=<profileId>, scheduled=<S>s, elapsed=<T>s, live=<N>, moved>=1m=<N>, movedTowardInsertion>=5m=<N>, movementMean=<M>m, movementMax=<M>m, actualSeenTarget=<N>, sameMaskSightProbe(vegetation=<N>,other=<N>,clearOrPlayer=<N>), states=<stateCounts>.
```

Use the repository verifier after the two required Forest runs:

```powershell
python eng\verify_profiled_pve_runtime.py `
  "<OPERATOR_INSTALL>\BepInEx\LogOutput.log" `
  --minimum-runs 2 `
  --expected-profile dense-forest-balanced-v1 `
  --brain-min 10 `
  --brain-max 15 `
  --detection 45 `
  --fov 90 `
  --wander 38 `
  --require-positive-delay `
  --require-search-movement `
  --minimum-search-displacement 5 `
  --minimum-toward-insertion 1 `
  --require-vegetation-block
```

The verifier selects the last two profiled-PVE runs. It requires one contract,
all six unique snapshots, and one completion line for each run. It also checks
the exact profile values, population range, live sight-probe accounting,
positive native delay, no movement or non-null `CurrentSeenTarget` in the
zero-second snapshot when the optional `--require-delayed-start` gate is
selected, search movement by 120 seconds, movement toward insertion, and at
least one vegetation-blocked probe. Exit code 0 means that the log contract
passes. Exit code 1 means that evidence is missing or rejected. Exit code 2
means that the command or log path is invalid.

Do not use the optional delayed-start gate as proof that a restart did or did
not detect the player. `CurrentSeenTarget` can be a non-player native target.
The release command above intentionally verifies the positive native wander
delay and zero initial movement, then correlates player-line geometry and
physical behavior separately.

This tool verifies the recorded fields. It cannot prove what the player saw,
whether a thin gap permitted acquisition, or whether firearms damaged both
sides. Record those physical observations separately.

The map companion must log the blocker count. A dense map must reject its
scene when its exact authored blocker count is incomplete. A successful
static build is not a runtime acceptance result.

## Accepted Forest runtime evidence

The final release bytes passed two complete 120-second windows in one fresh
process. The second window followed the shipped Restart Operation route.
Both runs used the same `dense-forest-balanced-v1` profile and passed the
repository verifier with a 10-through-15 population range.

| Run | Live AI | Positive native delay | Moved at least 1 m at 120 s | Moved at least 5 m toward insertion | Maximum displacement | Vegetation probe evidence |
| --- | ---: | --- | ---: | ---: | ---: | --- |
| Initial operation | `15` | `9.31..36.78 s` | `15` | `6` | `51.19 m` | Present |
| Native restart | `14` | `10.97..33.02 s` | `14` | `4` | `49.34 m` | Present |

The corresponding world contract reported `7079` transforms, `5533`
renderers, `1931` active renderers, zero portable/error-shader renderers,
`198` active colliders, `36/36` grounded PVE markers, and `36/36` markers on
the scanned graph. These are acceptance values for this package revision;
they are not generic constants for another map.

## Acceptance tests

Static acceptance requires:

- schema v1 rejects `pveAiProfile`;
- schema v2 accepts the exact PVE profile and freezes all fields;
- PVP rejects the object;
- the public API baseline includes `WanderDistanceMeters`;
- the generic source has no map ID, map display name, coordinate, terrain
  size, or vegetation asset name;
- the map companion activates blockers before its `applied = true` state;
- build, schema, validator, repository, and package checks pass.

The first launch and same-process native restart pass the bounded population,
delay, search-movement, toward-insertion movement, and vegetation-obstruction
gates. Reciprocal firearms, a direct physical acquisition/loss observation,
and the two-peer PVP matrix remain separate gates.
