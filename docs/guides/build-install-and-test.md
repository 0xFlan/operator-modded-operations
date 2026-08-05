# Build, install, and test

## Build inputs

Obtain these files from an authorized local OPERATOR installation:

- `<OPERATOR_INSTALL>/BepInEx/core/*.dll`;
- `<OPERATOR_INSTALL>/BepInEx/interop/*.dll`;
- Operator Mod API `0.2.0-alpha.2` source or binaries.

The pinned source uses Unity `6000.3.8f1`. A different player build requires a
new interop and source audit.

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

Close OPERATOR before installation. Copy files to this layout:

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
map companion in the framework directory.

## Static checks

1. Confirm the plugin attribute has the intended version.
2. Search framework source for a specific map ID or map coordinate.
3. Confirm the package schema is version 1.
4. Confirm each package file length and SHA-256.
5. Confirm the scene bundle contains the declared `scenePath`.
6. Confirm dependency bundles contain no streamed scene.

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
use a host and a remote client. Verify team separation, death, score, round
respawn, and final operation end.

## Release rule

Record SHA-256 for source output, staged DLL, archive, and installed DLL. All
four values must match. Do not overwrite installed files while OPERATOR runs.
