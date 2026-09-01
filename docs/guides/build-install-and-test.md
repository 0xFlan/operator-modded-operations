# Build, install, and test

## Build inputs

Obtain these files from an authorized local OPERATOR installation:

- `<OPERATOR_INSTALL>/BepInEx/core/*.dll`;
- `<OPERATOR_INSTALL>/BepInEx/interop/*.dll`;
- BepInEx IL2CPP semantic identity `6.0.0-be.785` with Il2CppInterop `1.5.3`;
- MelonLoader `0.7.3` net6 files when building the Melon variant;
- Operator Mod API `0.2.0-alpha.7` source or exact bundled-preview binaries.

The pinned source uses Unity `6000.3.8f1`. A different player build requires a
new interop and source audit. Operator Mod API is a separate maintainer input
only when building from source. A matching framework archive bundles its exact
preview runtime; testers and end users do not download preview API separately.
The active source checkpoint is the `0.3.33` mode-isolated spawn and all-map
multiplayer build / alpha.7. Bounded BepInEx single-machine gates pass.
MelonLoader live gameplay and separate host/remote runtime acceptance remain
required before promotion.

## Build command

```powershell
dotnet build .\src\OperatorModdedOperations\OperatorModdedOperations.csproj `
  -c Release `
  -p:OperatorLoader=BepInEx `
  -p:OperatorGameDir='<OPERATOR_INSTALL>' `
  -p:OperatorModApiProject='<OPERATOR_MOD_API_REPOSITORY>\src\OperatorModAPI\OperatorModAPI.csproj'

dotnet build .\src\OperatorModdedOperations\OperatorModdedOperations.csproj `
  -c Release `
  -p:OperatorLoader=MelonLoader `
  -p:OperatorGameDir='<OPERATOR_INSTALL>' `
  -p:OperatorModApiProject='<OPERATOR_MOD_API_REPOSITORY>\src\OperatorModAPI\OperatorModAPI.csproj'
```

If you use a prebuilt Core assembly, set `OperatorModApiDir` instead of
`OperatorModApiProject` as described in the project file.

The expected output is:

```text
src/OperatorModdedOperations/bin/Release/OperatorModdedOperations.dll
src/OperatorModdedOperations/bin/Release/MelonLoader/OperatorModdedOperations.MelonLoader.dll
```

The build must have zero warnings and zero errors.

## Install layout

Close OPERATOR before installation. Install exactly one matching loader suite;
the BepInEx and MelonLoader entries below are alternatives. The transaction
must refuse a mixed tree:

```text
<OPERATOR_INSTALL>/
  BepInEx/
    plugins/
      OperatorModAPI/
        OperatorModAPI.dll
        OperatorModAPI.BepInEx.dll
      OperatorModdedOperations/
        OperatorModdedOperations.dll
  Mods/
    OperatorModAPI.dll
    OperatorModAPI.MelonLoader.dll
    OperatorModdedOperations.MelonLoader.dll
  OperatorMods/
    <package-id>/
      operator-map-package.json
      content/
      media/
```

Do not put a runtime DLL in a package directory. Do not put a map companion in
the framework directory. Install a map as its own shared data download.
The runtime-suite transaction publishes an exact manifest sidecar and writes
its selected-loader ownership receipt last. It owns only receipt-listed runtime
files; it does not install, update, remove, or rewrite `OperatorMods` packages.

## Static checks

1. Confirm the plugin attribute has the intended version.
2. Search framework source for a specific map ID or map coordinate.
3. Confirm the package uses a closed supported schema. Use schema v2 for
   `sceneVariants`, `pveAiProfile`, or `initialWanderDelayMaxSeconds`; retain
   schema v1 only for packages that use none of those fields.
4. Confirm each package file length and SHA-256.
5. Confirm the scene bundle contains exactly the declared `scenePath` or the
   exact union of every declared `sceneVariants` path for maps sharing it.
6. Confirm dependency bundles contain no streamed scene.
7. For PVP, confirm the operation has at least
   `ceil(maximumPlayers / 2)` accepted markers per side. A 12-player operation
   requires six Team 1 and six Team 2 markers.
8. When `runtimeCompanion` is declared, confirm its GUID, version, DLL SHA-256,
   and distinct ready/failure marker names match the loaded companion on every
   peer.

## Physical test sequence

Use the in-world laptop pointer. Do not invoke button events from a console.

1. Open Cerberus.
2. Select `MODDED OPS`.
3. Select a package row.
4. Verify the preparation preview and briefing.
5. On PVE, change the private modded enemy slider within the package range and
   confirm Tier 1/vanilla operation rows and their enemy settings are unchanged.
6. Select Execute.
7. Verify the infiltration preview and marker.
8. Select Confirm once.
9. Verify that the operation starts without a second laptop interaction.
10. Verify player camera, input, movement, and correct spawn.
11. End or quit the operation.
12. Verify the armory return.
13. Repeat the same operation.
14. Test native Restart Operation after a PVE failure.

For PVE, test only within the package's certified range. The global hard cap is
100; inactive utility markers are eligible only when navigation-valid and at
least 2 m apart after snapping. Verify spawn/frame-time, guns, bullets, damage,
grenades, completion, Restart, and teardown at the claimed maximum. For PVP,
use a host and a remote client with matching suite receipts/sidecars, complete
package closures, and companion runtime-pair identities. Verify remote preload/commit,
synchronized first spawn and movement, team separation, firearm-specific hit
registration and death, score, round respawn, retained-content Restart, and
final operation end. Also test a deliberate identity mismatch and a roster
change; both must fail closed. Late join is unsupported.

Protocol v6 gives online PVE a mode-bound agreement path and binds the
host-confirmed count. Its content, scene, injected-owner, owner-local placement,
and server-authored population barriers must all pass before gameplay commit.
Test it first in two distinct BepInEx processes starting together, then repeat
as a separate MelonLoader gate. Both must leave
loading and remain grounded; prove identical content/scene/count identity, AI
replication, movement, projectile registration, damage, completion/extraction,
Restart on both peers, and clean failure/return/close. Bounded late-join/
membership refusal is required. Solo, host-only, or join-in-progress success is
not online-PVE proof. PVP, PVE, BepInEx, and MelonLoader remain separate
acceptance gates.

## Release rule

Record SHA-256 for the source output DLL, staged DLL, DLL entry extracted from
the archive, and installed DLL. Those four DLL hashes must match. Record a
separate SHA-256 for the complete release archive; an archive hash cannot equal
its contained DLL hash. The Modded Operations archive must also contain the
exact pinned Core and both loader-specific API hosts/framework adapters, their
license/notices, and no map payload. Exactly one installed loader activates its
matching branch. Do not publish a separate
preview-API archive. A map is a separate download and must not duplicate the
framework or API.

For the frozen candidate, the controlled-test transfer is:

```text
OperatorModdedOperations_v0.3.33_DUAL_LOADER_TEST.zip
bytes=645747
sha256=E8BC0F03779B935ECDA609519806D7C67CA389140DCB79D15165C3E037CFB265
```

Keep explicit test-only and `NOT NEXUS` labeling in the filename/readme
until the host-plus-remote matrix passes. Do not regenerate the public
decompiler or publication source-state record merely for this transfer; prior
publication checkpoints remain historical until an explicit promotion. Do not
overwrite installed files while OPERATOR runs.
