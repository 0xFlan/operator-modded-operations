# Changelog

## 0.3.31 runtime hot-path and placement candidate — 2026-08-30

- Retire local-player insertion after one acknowledged placement for each exact
  scene generation. The framework no longer re-applies the spawn transform on a
  periodic maintenance loop, and it never writes a remote player's transform.
- Cache the exact companion READY marker after the initial full-scene contract
  check. Steady-state readiness validates that one cached object instead of
  traversing the complete scene every Update; FAILED still wins synchronously.
- Bound solo-PVE membership validation to one registry check per Unity frame and
  move diagnostics to their existing maintenance cadence.
- Preserve native Mirror ownership for player/AI movement, bullets, health,
  damage, animation, death, and PVP rounds. No custom transform or combat
  replication path was introduced.
- Pass `108/108` Python contracts, both zero-warning loader builds, the
  suite-wide binary/runtime-pair audit, a fresh LOT 12 restart run, and a
  120-second LOT 12 PVE run with all 18 local assertions passing. Separate-PC
  host/remote PVE and PVP acceptance remains required before multiplayer is
  labeled supported.
- Freeze BepInEx at 642,560 bytes / SHA-256
  `54890536492E645050C7C2125F7D1FF4FFC23C3BE23EBF95A2294E648439DEB7`
  and MelonLoader at 643,584 bytes / SHA-256
  `EBCAD6563366D614A12C2797622B7639A16377EAE829A4279A3914CFF498C635`.

## 0.3.30 transition-lifecycle hotfix build 2 candidate — 2026-08-28

- Preserve the exact `0.3.30` plugin and peer-protocol identity required by the
  existing LOT 12, Ukrainian Forest, Whiteout Pass, and Hypermarket companions.
  The new bytes and SHA-256 values identify this hotfix build without making
  those exact BepInEx dependency declarations incompatible.

- Fix the exact post-mission lifecycle that could leave a completed solo-PVE
  package operation owning `NativeTransitionStarted` after the native Operation
  Room had returned. Every packaged PVE/PVP unload now defers the
  return-versus-Restart decision until either the replacement package scene or
  the exact native Operation Room is observable.
- Retire the completed active-operation owner and solo-PVE membership freeze
  only after exact Operation Room, Mirror scene-name, package-scene-absence,
  and spawned-owner-absence checks pass. This releases cross-map cache
  ownership without weakening alive/KIA Restart handling.
- Refuse a fresh package Confirm before committing a durable scene-variant
  shuffle-bag choice while a prior package transition still owns teardown.
  The native-start path repeats the ownership check after asynchronous bundle
  I/O.
- Add regressions for solo-PVE return ownership and pre-selection transition
  admission. The complete Python suite passes `106/106`; both BepInEx and
  MelonLoader builds complete with zero warnings and zero errors.
- Freeze the BepInEx framework at 640,000 bytes / SHA-256
  `E77412F83C418EDDC5422F702382BEB75AFB6459430CD7B64B8EB0594549EBA8`
  and the MelonLoader framework at 641,536 bytes / SHA-256
  `2A6694E798A3AF2C3CF1565F50BF20556E83A48FD60CF7ED0EFBE754F3572903`.

## 0.3.30 multiplayer-test candidate — 2026-08-20

- Generalize the exact peer-session protocol to both standalone PVE and PVP.
  Every frozen peer must validate the selected-loader receipt, suite manifest,
  framework/API/companion binaries, package content, operation, variant, scene
  generation, runtime owner, owner-local player placement, and Restart epoch
  before gameplay commit. Late join and connection replacement remain
  deliberately fail-closed.
- Keep OPERATOR and Mirror authoritative for transform replication, weapons,
  bullets, Health, damage, and death. Repair dynamic game-mode clones through
  an exact custom Mirror spawn handler before client deserialization instead
  of relying on `OnStartClient` to repair native `syncObjects`.
- Add the PVE population barrier. The host alone calls the shipped
  `RaidManager.ServerSpawnAI(false)` once, then every peer must observe the
  same sorted server-authored AI netIds, team, initial quantized poses, Health,
  WeaponsAI, animation, and native SmoothSync contract before gameplay begins.
- Replace direct host writes to client-owned player transforms with an
  owner-targeted placement request. Each owning process invokes the shipped
  local movement path, proves root/controller/camera grounding on exact scene
  support, and acknowledges its assignment before gameplay.
- Expose the shipped briefing enemy-count slider only on the private modded-PVE
  operation clone. Enforce the package range and a hard maximum of 100, retain
  the chosen count across Restart, recapture it on a fresh Confirm, and validate
  safe navigation-marker capacity before the one native population call. Do
  not mutate Tier 1, vanilla operation arrays, vanilla enemy ranges, or PVP.
- Build isolated BepInEx and MelonLoader adapters from shared framework/API/map
  source. An install may contain both adapter payloads but must activate exactly
  one loader. PVE and PVP remain separate live acceptance gates for each loader.
- Pass `102/102` Python tests, `16/16` scene-variant selector tests, native
  game-mode/policy/runtime-barrier tests, both zero-warning loader builds, and
  clean staged/install/archive audits. This remains `PROVEN-STATIC` until paired
  host/remote evidence proves PVE and PVP movement, combat, Restart, and return.
- Freeze the BepInEx framework at 632,832 bytes / SHA-256
  `772D9FC1470DCC115B22F8C232C4A3B90D0FC727C5B1D27A0F54ED95E5D1AE86`
  and the MelonLoader framework at 634,368 bytes / SHA-256
  `2C2D11C28BD2D470ABAC83F2FC558476384DA8D79302FA4B1ADD6F845A113F83`.

## 0.3.29 candidate — 2026-08-12

- Isolate standalone player-marker discovery by operation mode. PVE accepts
  the shared Team 1 markers and `PVE_PlayerSpawn_` markers but never consumes
  `PVP_Team1Spawn_`, Team 2, or `PVP_Team2Spawn_` markers. PVP accepts the
  shared/team-specific Team 1 and Team 2 markers but never consumes a PVE
  player marker.
- Require each PVP side to provide at least
  `ceil(operation.maximumPlayers / 2)` markers. The rule is manifest-driven
  and map-neutral: a 12-player operation requires six markers per side,
  without a Kill House, Forest, or fixed-player-count branch in framework
  source.
- Add a private, collision-checked Mirror peer-agreement envelope for
  standalone PVP. Protocol v2 freezes the authenticated remote connection set
  and binds exact SHA-256 identities for the loaded framework, API Core, API
  BepInEx host, and any manifest-declared map companion in addition to package
  ID/version/content hash, map/operation/mode/spawn-set, scene variant/path,
  time, and min/max-player identity with a per-launch nonce and SHA-256
  digest. Same-version/different-byte peers fail closed.
- Add the optional closed schema-v2 `runtimeCompanion` declaration supplied by
  bundled Operator Mod API `0.2.0-alpha.6`: exact plugin GUID/version/SHA-256
  plus distinct ready/failure marker names. PVP acknowledges scene readiness
  only after the unique ready marker exists in the exact generation scene;
  failure wins before or after readiness and aborts the complete session.
- Make a remote peer resolve that identity only through its frozen Operator
  Mod API catalog, preload the exact verified dependency and scene bundles
  when needed, then commit the exact remote `ActiveMapOperation` before it
  acknowledges content readiness. A mismatch, conflicting load, malformed or
  trailing envelope, message-ID collision, disconnect/join, rejection, or
  bounded timeout fails closed.
- Start the shipped native board only after every frozen remote peer reports
  exact content ready. After the scene transition, require every peer to
  construct and register the deterministic native `StandalonePvpGameMode :
  PvpGameode` template before the host can call `NetworkServer.Spawn`.
  Remote adoption additionally requires the exact agreed operation, mode,
  scene, content identity, and deterministic PVP asset ID.
- Preserve content agreement across same-operation alive/KIA Restart while
  resetting and re-running the scene and native-lifecycle barriers, including
  load-before-unload callback ordering. Bind every scene-ready acknowledgement
  to a nonzero host-issued, monotonically increasing scene-generation epoch.
  The host retries the bounded epoch request; a remote maps it to an exact
  local package-scene generation, resends a matching acknowledgement on a
  duplicate request, and never reuses readiness from the preceding generation.
  Reject stale/unversioned acknowledgements, counter overflow, and replacement
  connections that reuse a numeric connection ID. Bound remote owner adoption/readiness
  and host owner publication/all-players-loaded. Native-start, spawn-contract,
  `OnStartClient`, and `Server_AllPlayersLoaded` exceptions cancel and tear
  down the exact generation; PVP never falls back to position-only play.
  Remove only the
  framework-owned Mirror handler keys at network/plugin teardown. PVE launch,
  spawn, AI, completion, and restart paths do not enter the PVP agreement.
- Pass all 78 Python source/regression tests, including 39 focused PVP
  agreement/restart tests, plus the separate 9-test C# scene-variant selector
  suite and a Release build with zero
  warnings and zero errors. This source candidate is `PROVEN-STATIC`; the
  host-plus-remote exact-content transport, first spawn, movement replication,
  firearm-specific hit registration, team, score, round, restart, and return
  matrix remains required before multiplayer can be labeled `SUPPORTED`.
- Freeze the review binaries at these identities:
  `OperatorModdedOperations.dll` 279,552 bytes / SHA-256
  `95CEF59F62B2DF40ED69C066692953210CDC17A9C3D08DB95753DA7A9B4142CD`,
  `OperatorModAPI.dll` 179,200 bytes / SHA-256
  `0C27854DFDD3C9F0946F5BCBC61CE37DAE3037215BB5FC11C3400BD50190EB77`,
  and `OperatorModAPI.BepInEx.dll` 25,600 bytes / SHA-256
  `A58E1FA50CE345931104B9980AFBAF356B8EEAC0E7A735BEF7BD21FC93727AD9`.
- Keep membership immutable for the complete PVP agreement. Late join is not
  supported; any join, disconnect, or replacement connection fails closed,
  including numeric connection-ID reuse. PVE co-op bypasses this PVP protocol,
  so online PVE content/scene/AI/combat equivalence remains unproven.
- Create the separately labeled controlled-test transfer
  `OperatorModdedOperations_v0.3.29_API-alpha.6_MULTIPLAYER_TEST_ONLY.zip`,
  486,369 bytes / SHA-256
  `4507C858888339B19F318F7D23B55F771B53E102CDA2B335F95B52BDF91FC1B8`.
  It is not a Nexus binary release and is not runtime proof. Publish the exact
  `0.3.29` decompiler tree as a Git source checkpoint while the runtime-release
  publication record remains pinned to `0.3.28` with bundled API
  `0.2.0-alpha.5`.
- Keep the established two-download model for promotion: one Modded Operations
  archive bundles Operator Mod API `0.2.0-alpha.6`, while each map is a
  separate archive. The API is not published separately before a full stable
  release, and map archives do not duplicate either runtime.

## 0.3.28 — 2026-08-12

- Fix a native AI-startup ordering regression that could remove a successfully
  spawned PVE population and leave a mission empty. The shipped synchronous
  `RaidManager.ServerSpawnAI(false)` call returns before each `BrainAI.Start`
  joins `GameManager.allAI`; an immediate global-list validation therefore
  observed zero registered brains even though Mirror had created them.
- Snapshot Mirror's authoritative `NetworkServer.spawned` registry around the
  one native population call. Own the exact new netId, `NetworkIdentity`, and
  root-`BrainAI` set; require that exact structural delta to equal the package
  count before continuing.
- Defer the complete native team/reference/pool/target validation until the
  next Unity frame, probe it every Update, and bound missing
  `BrainAI.Start` registration to 60 frames. Once every exact owned brain is
  registered, any pointer, team, target-graph, raw-count, identity, or extra-
  population mismatch is definitive and fails closed.
- Keep both the package exfil zone and global extraction state locked while
  validation is pending or failed. Preserve shipped Health-death,
  `RaidManager.UpdateAICount`, all-enemies-dead unlock, physical extraction,
  and Mission Successful ownership after validation passes.
- On spawn failure, validation failure, Restart, or unload, destroy only the
  exact captured Mirror identity set before releasing the standalone game
  mode. Do not remove entries directly from `GameManager.allAI`, friendly/
  enemy collections, possible-target lists, or another mod's population.
- Retain the `0.3.27` unique hostile-team cohort, optional native reaction
  disposition/reaction cap, first-wander cap, scene-variant, bundle-cache, and
  Mirror-bootstrap contracts without changing Operator Mod API. The required
  bundled preview runtime remains `0.2.0-alpha.5`.
- Pass the complete 39-test Python suite and a Release build with zero warnings
  and zero errors. Pin authored `CerberusNativeTabFix.cs` SHA-256
  `3A33AC1C1B97CA31E77F5198EE77BBCD24EFE3CD2DE9284758540AC94E9F4302`.
- Pin the reviewed 223,232-byte Release DLL at SHA-256
  `75BB479E863A94807ACCB78E6FA37271BE340B226E4EBDFB1BE48C0B8248852B`.
  Publish its seven-file, 427,039-byte ILSpy `10.1.1.8388` tree at
  `decompiled/release-0.3.28`, tree SHA-256
  `59D672462C7AA4DA00268D3A2185EA79D2C43F078853F71E40EAE40FDEE361C8`,
  and regenerate the complete source-state manifest.
- Preserve the two-download release model: the Modded Operations archive
  continues to bundle the exact alpha.5 API pair; Kill House remains the
  separate map download; no standalone preview-API archive is published.

## 0.3.27 — 2026-08-11

- Require the bundled Operator Mod API `0.2.0-alpha.5` preview runtime. Consume
  its two optional, schema-v2 PVE-only response fields:
  `reactionDisposition` and `maximumReactionTimeSeconds`.
- Prevent standalone PVE bots from becoming native enemies of one another.
  Before population, filter the pinned game's firearm-capable
  `GameManager.AllAITypes` entries through their root `TeamIdentifier`, exact
  `StartingTeamStats` object, numeric team ID, `TeamIdentifierReference`, and
  `TargetPool`. Exclude the live player's native team and permit spawning only
  from the unique strict-largest remaining cohort. A missing player team,
  invalid donor contract, or tied leading cohort fails closed.
- Scope `RaidManager.standardAI` to that one cohort only for the synchronous
  shipped `ServerSpawnAI(false)` call, then restore its prior array. Validate
  every new brain's native team/reference/pool/stats contract and target graph.
  A serialized selected donor whose SyncVar is still unresolved can be repaired
  only through `TeamIdentifier.NetworkTeamID`; every other mismatch removes
  the new population and fails closed. The framework does not change global
  friendly fire or hand-edit `BrainAI` target/team lists.
- When explicitly requested, set the native marker `reactType` before spawn to
  `Defensive`, `Offensive`, or `Random`. Omission performs no disposition
  write. An optional maximum reaction time caps only newly spawned,
  operation-owned brains on the authoritative server after native `Awake`,
  writing only `_baseReactionTime` and `ReactionTime` and never raising a
  faster native value. It does not change difficulty, reaction progress,
  targets, combat states, or existing bots.
- Tighten PVE diagnostics: `live` now requires an enabled, active brain with an
  enabled existing navigation entity; reports include team identity,
  disposition, base/current reaction ranges, cap counters, cohort references in
  friendly/enemy/possible-target collections, and classify
  `CurrentSeenTarget` as player, friendly brain, enemy brain, or other.
- Preserve the established optional six-second first-wander mechanism and all
  package-omission behavior. No schema-v1, PVP, vanilla operation, global
  prefab, or unrelated mod is modified.
- Add source-level regression gates for unique cohort ordering, player-team
  exclusion, scoped `standardAI` restoration, SyncVar-only team repair,
  optional/server-only/one-shot reaction capping, its exact two writes, and
  disposition omission. The framework's 29 Python tests pass and its Release
  build completes with zero warnings and zero errors.
- Pin the reviewed Release DLL at 216,064 bytes with SHA-256
  `CD2B326E76FC95439352E999BB138EB4499B08895C5BE552807D2C8A7FF62579`,
  publish its ILSpy `10.1.1.8388` snapshot, and regenerate the complete
  source-state manifest.
- Preserve the two-download release model: this archive bundles the exact
  alpha.5 API pair; the Kill House map remains separate; no standalone
  preview-API archive or metadata owner is emitted.

## 0.3.26

- Require Operator Mod API `0.2.0-alpha.4` and consume its optional,
  schema-v2 PVE-only `initialWanderDelayMaxSeconds` field.
- Preserve the established end-user install model: the Modded Operations
  archive bundles the exact `0.2.0-alpha.4` API Core and BepInEx host under
  `BepInEx/plugins/OperatorModAPI` alongside the separately owned framework
  folder. No standalone preview-API archive is published; a separate Operator
  Mod API release is deferred until the API reaches a full stable version.
- When and only when that field is present, advance each newly spawned,
  operation-owned, non-responding Wander bot's `wanderTime` once on the
  authoritative server. Deterministically stagger the first remaining delay
  over 50–100% of the package cap without consuming Unity's global random
  state.
- Preserve `WanderTimer`, `Patience`, `ReactionTime`, native combat, cover,
  doors, and every later wander cycle. A package that omits the field returns
  before any AI state write, retaining the previous profile behavior.
- Log native delay separately from the cap, handled/advanced/preserved/skipped
  counts, and observed post-cap remaining delay. Continue the existing
  0/10/30/60/90/120-second read-only movement and sight snapshots.
- Add static ownership/regression gates for the optional-field, server,
  one-time, native-state, and no-global-RNG boundaries. The API suite passes
  168 tests, the complete Python suite passes 23 tests, the selector suite
  passes 9 tests, and Release builds with zero warnings and zero errors.
- Release builds now omit CodeView/PDB records so published DLLs do not expose private build paths.
- The `0.3.26` Release DLL is 202,752 bytes with SHA-256
  `74550C0F9B8E868957E9417B8ECE0016FAE523448DCE0550DD9013902E098B6B`.
- Publish its ILSpy `10.1.1.8388` snapshot, exact decompiler identity, and a
  deterministic source-state manifest with a SHA-256 sidecar. The publication
  closure is `PROVEN-STATIC`; it does not promote the optional-cap runtime
  behavior beyond its separately recorded live evidence.

## 0.3.25 candidate

- Initialize a missing `Mirror.NetworkBehaviour.syncObjects` reference on each
  operation-owned runtime game-mode root to the exact empty-list baseline that
  Mirror's native parameterless constructor supplies to serialized/native
  components. ClassInjector creates the injected PVE/PVP subtype through its
  `IntPtr` wrapper constructor, so that one native constructor field was null.
- Refuse Mirror prefab registration and `NetworkServer.Spawn` unless every
  root `NetworkBehaviour` has a non-null `syncObjects` list.
- Record the native spawn attempt before the synchronous Mirror call and fail
  closed after one attempt. Do not retry a partially entered spawn or leave the
  player on the transient `Map Loaded... !BUG!` recovery path.
- Teardown in Mirror order: unspawn the live operation root, unregister only
  its deterministic prefab/spawn-handler ID, then destroy the operation-owned
  root and its tracked list ownership together. Never restore a null list and
  never call `NetworkClient.ClearSpawners()`.
- Bound completed cross-map bundle retention at the safe Operation Room
  boundary. Preserve the active/restart owner and any in-flight selected-map
  prefetch; after fresh native launch transfers ownership, evict prior
  distinct-map bundles with `Unload(false)`.
- Do not infer a safe boundary from the active scene name alone. Require a
  zero active package-scene handle and prove that no cached or in-flight scene
  bundle owns any loaded scene before eviction. Same-map Restart continues to
  reuse its resident bundle; stale-entry cleanup cannot bypass the active,
  pending, or loaded-scene owner gates.
- Add five source-level Mirror regression gates plus six map-neutral bundle
  lifetime gates covering safe-boundary detection, loaded-scene protection,
  same-map restart reuse, different-map ownership transfer, in-flight owner
  preservation, and variant/single-scene isolation. The complete Python suite
  passes 20 tests; the scene-variant selector suite passes 9 tests; Release
  builds with zero warnings and zero errors.
- The `0.3.25` Release candidate DLL is 200,704 bytes with SHA-256
  `E5354D32336C10FAB4A86EFA5CAC073604B075B58B0FE9761FC291DD6148E016`.
  This candidate is `PROVEN-STATIC`; it has not been deployed or promoted to
  the existing `0.3.24` runtime evidence.

## 0.3.24

- Add framework-owned, manifest-driven scene-variant selection. A map opts in
  only when `SceneVariants.Count > 1`; single-scene maps retain the original
  exact `ScenePath` path without selector state, variant RNG, or variant logs.
- Keep one persistent shuffle bag per package ID and map ID. A fresh Operation
  Room Confirm advances the bag, avoids an immediate repeat, and preserves
  that history across OPERATOR process restarts.
- Pin the selected scene in pending and active operation state. Alive Restart
  and the shipped KIA Restart path reload the same selected scene instead of
  consuming another selection.
- On an asynchronous cold launch, select only after exact dependency and scene
  bundle validation succeeds. A previously verified cached launch may select
  immediately from the same closed manifest inventory.
- When maps share a scene bundle and any map declares variants, require the
  bundle scene paths to exactly equal the union of the participating maps'
  declared scene inventories. Continue to reject missing, extra, or unrelated
  scene addresses.
- Remove the need for a map companion to mutate `ScenePath` or Harmony-patch
  the framework's scene-bundle validator. Variant selection and validation are
  map-neutral framework responsibilities.
- Accept runtime checks in which `KH05_SplitSpine` survives alive Restart, the
  next OPERATOR process selects `KH09_Pinwheel` and retains it across alive
  Restart, and `KH08_DoubleBack` survives the shipped KIA Restart path. The
  complete same-process gate also proves fresh KH10 -> alive Restart KH10 ->
  native completion and Operation Room -> fresh KH03 -> KIA Restart KH03.
- Publish the exact framework DLL with SHA-256
  `0B61F0C3CCEC667B5FD38BAD7884C8F7349479F61AE3682F4DD4BB08C8243992`.

## 0.3.22

- Require exactly one package-authored `PVE_ExfilZone_` Transform with a
  positive trigger BoxCollider for every standalone StandardPVE scene.
- Create one shipped `RaidManager` and one shipped `ExfilZone` on the
  Mirror-owned `StandalonePveGameMode : InfiltrationManager` bootstrap.
- Start extraction locked. Reset the zone and global occupant sets,
  `NetworkcanExtract`, `NetworkisExtracting`, extraction start time, and the
  15-second timer for each operation generation.
- Keep enemy elimination in the shipped lifecycle. Native AI `Health` deaths
  drain `GameManager.allAI`; the shipped `RaidManager` unlocks extraction;
  physical trigger occupancy starts the native timer; the shipped Mission
  Successful After Action Report ends the operation.
- Restore `raid.exfilZones` to exactly the current map zone after
  `RaidManager.ServerSpawnAI(false)` so persistent donor objects cannot own
  completion.
- Reconstruct the current-build `level16` `ATAK Exfil Marker`: layer 17,
  four-vertex `Marker` mesh, `HDRP/Unlit` `ExfilZone` material, resident
  512-by-512 `ExfilZone` texture, exact transform, UVs, and render queue 2501.
- Let the shipped `ExfilZone.ExfilMarker` activation show the icon only after
  native extraction unlock.
- Destroy only operation-owned ATAK mesh/material assets during teardown.
  Preserve `GameManagerNetwork.SuccessfulOperation` during successful map
  unload so the Operation Room can consume the result.
- Accept the complete insertion-area extraction flow with release-version
  framework and Forest assemblies and exact package bytes.

## 0.3.20

- Enter the shipped `GameManagerNetwork.ShowLoadingScreen()` presentation at
  the exact additive-scene boundary before terrain and material preparation.
  This closes the one-frame gap that could expose a package's portable brown
  proxy.
- Log `LoadingScreen.activeSelf` and `activeInHierarchy`. Do not treat the
  misnamed `LoadingScreenVisible` getter as the canvas state.
- Start the profiled-PVE diagnostic after native network population becomes
  available.
- Measure native bot movement through `BrainAI.agent.position`. The shipped
  `BOT V2` root is the stationary network owner; its `AgentController` and
  `FollowerEntity` live on the moving model child.
- Accept two complete 120-second exact-package Forest runs with the final
  release bytes: 15 and 14 native bots, positive native delays, all bots moved
  at least 1 m, 6 and 4 bots moved toward insertion, and authored vegetation
  sight-obstruction evidence.
- Keep the profile PVE-only and operation-local. Add no difficulty UI and no
  effect on PVP, vanilla operations, or another package.

## 0.3.19 candidate

- Add schema v2 and the immutable PVE-only `pveAiProfile` contract.
- Apply package-owned detection range, field of view, effective-range
  sentinel, wander radius, communications, and counter-suppression through
  each native `BotSpawnDetails` before `RaidManager.ServerSpawnAI(false)`.
- Preserve the legacy 72 m, 105 degree, 90 m, 18 m, communications-on, and
  counter-suppression-on values for schema-v1 maps and schema-v2 PVE
  operations that omit a profile.
- Add no AI difficulty UI and make no change to PVP, vanilla operations, or a
  different map package.
- Document the exact current-build `BrainAI.Wander` delay and
  `RandomNavSphere(currentPosition, 5, WanderDistance)` behavior.
- Add one read-only diagnostic for schema-v2 PVE operations that have an AI
  profile. It records the live spawned-bot delay and profile values. It then
  reports movement, movement toward insertion, target state, AI state, and
  same-mask sight probes at 0, 10, 30, 60, 90, and 120 seconds.
- Keep the diagnostic generic. Its gate is `PveAiProfile != null`; framework
  source contains no Ukrainian Forest ID, coordinate, or terrain size.
- Static build and contract tests pass. A fresh in-game Forest session remains
  the runtime acceptance gate for this candidate.

## 0.3.18

- Replace the rejected generic `PVP map` day transplant with the exact
  `level11/PVP Woods Warehouse` `native-outdoor-v1` contract: 30,000 lux,
  5,500 K, bounce intensity 5, bloom 0.03, lens flare 0.5, saturation -15,
  white balance -3.6/-8.6, and the donor `LiftGammaGain` values.
- Retain the verified 02:00 night branch: 40 lux, 9,754 K, night ambient,
  white-phosphor color index 0, and full four-tube GPNVG visibility outside
  the ECOTI channel.
- Log profile source, sun, bloom, lens flare, night ambient, white-phosphor,
  and external-LUT state at the point where the live Volume is applied.
- Accept first launch, one-click Confirm, unload to the menu, Lone Wolf
  re-entry, repeat launch, above-terrain spawn, and 02:00 NVG in a local live
  test on 2026-08-04.
- Publish complete authored source and an ILSpy `10.1.1.8388` snapshot of the
  exact 151,552-byte DLL with SHA-256
  `71F21527FF959DBCF3C7AD1894937F56A9D931E0BF1A6B038C857249861A745C`.

## 0.3.17

- Add `LoadVerifiedMapDependencyAsset<T>`.
- Keep dependency-bundle ownership in Modded Operations.
- Let map companions borrow exact verified bundle assets without a second
  bundle load.
- Keep map-local Cerberus/UI code outside the active framework/companion
  boundary. Capture the exact laptop and player owner before asynchronous
  package I/O, then use the shipped board transaction on the first Confirm.
- Reconstruct manifest-declared TerrainData and bind the same object to
  `Terrain` and `TerrainCollider` before player or mode readiness.
- Use `PlayerMaster.SpawnPlayer()` as the normal player route, with one bounded
  generated host-body recovery only after the normal request fails.
- Use `RaidManager.ServerSpawnAI(false)` for native owner-aware PVE population.
- Add the `PvpGameode`-derived standalone PVP owner, one-based team sides,
  round/UI/audio contracts, repeat-generation cleanup, KIA failure handoff,
  Restart Operation, and armory teardown.
- Publish complete authored source, an ILSpy `10.1.1.8388` snapshot of the
  exact 150,528-byte DLL, exhaustive lifecycle documentation, and explicit
  drag-and-drop placeholders.

## 0.3.16

- Add selected-map prefetch and one-click Confirm join behavior.
- Add standalone PVE and PVP ownership and repeat-generation repairs.
