# Changelog

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
