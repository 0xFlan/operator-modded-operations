# Build, install, and test

## Build inputs

Obtain these files from an authorized local OPERATOR installation:

- `<OPERATOR_INSTALL>/BepInEx/core/*.dll`;
- `<OPERATOR_INSTALL>/BepInEx/interop/*.dll`;
- BepInEx IL2CPP semantic identity `6.0.0-be.785` with Il2CppInterop `1.5.3`;
- Operator Mod API `0.2.0-alpha.6` source or exact bundled-preview binaries.

The pinned source uses Unity `6000.3.8f1`. A different player build requires a
new interop and source audit. Operator Mod API is a separate maintainer input
only when building from source. A matching framework archive bundles its exact
preview runtime; testers and end users do not download preview API separately.
The current public publication remains `0.3.28` / alpha.5. The frozen
`0.3.29` / alpha.6 archive is explicitly multiplayer-test-only until promotion.

## Build command

```powershell
dotnet build .\src\OperatorModdedOperations\OperatorModdedOperations.csproj `
  -c Release `
  -p:OperatorGameDir='<OPERATOR_INSTALL>' `
  -p:OperatorModApiProject='<OPERATOR_MOD_API_REPOSITORY>\src\OperatorModAPI\OperatorModAPI.csproj'
```

If you use a prebuilt Core assembly, set `OperatorModApiDir` instead of
`OperatorModApiProject` as described in the project file.

The expected output is:

```text
src/OperatorModdedOperations/bin/Release/OperatorModdedOperations.dll
```

The build must have zero warnings and zero errors.

## Install layout

Close OPERATOR before installation. Install one matching Modded Operations
archive; it copies the preview API and framework into separately owned plugin
folders in this layout. Do not present a `0.3.29` multiplayer-test-only archive
as a public release:

```text
<OPERATOR_INSTALL>/
  BepInEx/
    plugins/
      OperatorModAPI/
        OperatorModAPI.dll
        OperatorModAPI.BepInEx.dll
      OperatorModdedOperations/
        OperatorModdedOperations.dll
    OperatorMods/
      <package-id>/
        operator-map-package.json
        content/
        media/
```

Do not put `OperatorModdedOperations.dll` in a package directory. Do not put a
map companion in the framework directory. Install a map as its own download,
and do not install a separate preview Operator Mod API archive.

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
5. Select Execute.
6. Verify the infiltration preview and marker.
7. Select Confirm once.
8. Verify that the operation starts without a second laptop interaction.
9. Verify player camera, input, movement, and correct spawn.
10. End or quit the operation.
11. Verify the armory return.
12. Repeat the same operation.
13. Test native Restart Operation after a PVE failure.

For PVE, verify 10 or more actors only when the package declares that count.
Verify guns, bullets, damage, grenades, and reciprocal line of sight. For PVP,
use a host and a remote client with matching framework, API, package, and
declared companion hashes. Verify the exact remote package preload/commit,
synchronized first spawn and movement, team separation, firearm-specific hit
registration and death, score, round respawn, retained-content Restart, and
final operation end. Also test a deliberate identity mismatch and a roster
change; both must fail closed. Late join is unsupported.

PVE co-op bypasses the PVP peer agreement. Test online PVE separately for exact
content/scene selection, enemy placement, movement, projectile registration,
and damage before making an online-PVE support claim.

## Release rule

Record SHA-256 for the source output DLL, staged DLL, DLL entry extracted from
the archive, and installed DLL. Those four DLL hashes must match. Record a
separate SHA-256 for the complete release archive; an archive hash cannot equal
its contained DLL hash. The Modded Operations archive must also contain the
exact pinned `OperatorModAPI.dll` and `OperatorModAPI.BepInEx.dll` preview
runtime, their license/notices, and no map payload. Do not publish a separate
preview-API archive. A map is a separate download and must not duplicate the
framework or API.

For the frozen candidate, the controlled-test transfer is:

```text
OperatorModdedOperations_v0.3.29_API-alpha.6_MULTIPLAYER_TEST_ONLY.zip
bytes=486369
sha256=4507C858888339B19F318F7D23B55F771B53E102CDA2B335F95B52BDF91FC1B8
```

Keep `MULTIPLAYER_TEST_ONLY` and `NOT NEXUS` labeling in the filename/readme
until the host-plus-remote matrix passes. Do not regenerate the public
decompiler or publication source-state record merely for this transfer; those
remain pinned to `0.3.28` / alpha.5 until an explicit promotion. Do not
overwrite installed files while OPERATOR runs.
