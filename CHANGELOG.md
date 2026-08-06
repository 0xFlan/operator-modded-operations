# Changelog

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
