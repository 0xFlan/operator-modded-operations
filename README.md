# OPERATOR: Modded Operations

OPERATOR: Modded Operations is a standalone map framework for the OPERATOR
mission laptop. The framework adds the `MODDED OPS` tab to the shipped
Cerberus laptop. It reads verified map packages from Operator Mod API. It then
uses the shipped briefing, infiltration, scene, player, AI, PVP, failure, and
restart systems.

Related public projects:

- [Ukrainian Forest reference map](https://github.com/0xFlan/operator-ukrainian-forest)
- [OPERATOR map-modding guide and reusable skill](https://github.com/0xFlan/operator-map-modding-guide)

The framework does not contain Ukrainian Forest code. It does not contain a
map name, a map coordinate, a map-material profile, a terrain size, or an A*
graph size. It can own named, map-neutral process contracts such as
`native-outdoor-v1` because one owner must apply and restore global HDRP and
NVG state. A map package selects the contract and owns its verified LUT. An
optional map companion owns exact-scene reconstruction.

## Current status

The source version in this repository is `0.3.22`. Local live testing on the
pinned game build accepts first launch, one-click Confirm, complete unload,
repeat launch, player grounding, 11:00 native daylight, and 02:00 four-tube
white-phosphor NVG behavior. Two complete Forest PVE runs also accept the
fixed profile, positive native search delays, native agent movement, movement
toward insertion, and foliage sight obstruction. Release-version completion
runs also accept one shipped `RaidManager`, one shipped `ExfilZone`, locked
initial extraction, all-enemies-dead native unlock, the exact current-build
ATAK exfil marker, physical occupancy, the 15-second extraction timer, and the
Mission Successful After Action Report. The additive-scene boundary
now enters the shipped loading canvas before terrain/material preparation, so
the portable brown proxy is not presented to the player. The two-peer PVP
matrix remains a separate release gate. Read
[the evidence status](docs/evidence/current-status.md) before you use the word
`SUPPORTED`.

## Repository contents

| Path | Purpose |
| --- | --- |
| `src/OperatorModdedOperations` | Exact BepInEx IL2CPP framework source. |
| `decompiled` | Hash-pinned decompiler snapshots of release DLLs. |
| `packaging` | Drag-and-drop release-layout placeholders. |
| `schemas/operator-map-package.schema.json` | Closed legacy schema v1. |
| `schemas/operator-map-package-v2.schema.json` | Closed schema v2 with a fixed PVE AI profile. |
| `examples/operator-map-package.example.json` | Complete PVE and PVP manifest example. |
| `MODDED_OPERATIONS_TECHNICAL_BIBLE.md` | Complete implementation and maintenance index. |
| `docs/architecture` | Ownership, UI, launch, game-mode, and teardown contracts. |
| `docs/guides` | Build, install, map-package, and test procedures. |
| `docs/reference` | Exact source members, native types, manifest fields, and diagnostics. |

Read [native PVE completion, extraction, and ATAK](docs/architecture/native-pve-completion-exfil-and-atak.md)
for the exact map marker, runtime owner, native unlock, ATAK visual, success,
and restart contract.

## Required local inputs

This repository does not redistribute OPERATOR or BepInEx files. Supply these
inputs from your own installation:

- OPERATOR with Unity `6000.3.8f1` for the pinned source state;
- BepInEx IL2CPP;
- generated interop assemblies under `<OPERATOR_INSTALL>/BepInEx/interop`;
- Operator Mod API `0.2.0-alpha.3`.

## Build

```powershell
dotnet build .\src\OperatorModdedOperations\OperatorModdedOperations.csproj `
  -c Release `
  -p:OperatorGameDir='<OPERATOR_INSTALL>' `
  -p:OperatorModApiProject='<OPERATOR_MOD_API_REPOSITORY>\src\OperatorModAPI\OperatorModAPI.csproj'
```

The output is
`src/OperatorModdedOperations/bin/Release/OperatorModdedOperations.dll`.

## Install

Install Core and the framework as separate owners:

```text
<OPERATOR_INSTALL>/
  BepInEx/
    core/
    interop/
    plugins/
      OperatorModAPI/
        OperatorModAPI.dll
        OperatorModAPI.BepInEx.dll
      OperatorModdedOperations/
        OperatorModdedOperations.dll
    OperatorMods/
      <package-id>/
        operator-map-package.json
        content/...
        media/...
```

Do not put a map companion in `BepInEx/OperatorMods`. Put the companion in its
own directory under `BepInEx/plugins`.

## Documentation start points

- To understand the system, read
  [the technical BIBLE](MODDED_OPERATIONS_TECHNICAL_BIBLE.md).
- To create a package, read
  [Create a map package](docs/guides/create-a-map-package.md).
- To understand the native laptop, read
  [Cerberus UI and mission presentation](docs/architecture/cerberus-ui-and-presentation.md).
- To diagnose a failed launch, read
  [Troubleshooting](docs/reference/troubleshooting.md).
- To trace selection, loading, terrain, player, AI, failure, restart, and
  armory return, read
  [Complete operation lifecycle](docs/architecture/full-operation-lifecycle.md).
- To reproduce the current-build type, signature, serialized-asset, and
  native-behavior evidence without publishing game binaries, read
  [Source publication and native inspection](docs/reference/source-publication-and-native-inspection.md).
- To compare the compiled release with the authored source, read
  [Decompiled release snapshots](decompiled/README.md).
- To stage the Nexus or GitHub Release archive, read
  [Drag-and-drop package placeholders](packaging/README-PACKAGE-PLACEHOLDER.md).

## Asset and code boundary

This repository contains original framework source and documentation. It does
not contain copied OPERATOR DLLs, scenes, textures, models, materials, audio,
or prefabs. Type names and hashes document compatibility. They do not grant a
right to redistribute game content.

The maintainer reports direct community-modding and publication support from
the OPERATOR lead developer and technical director for this process and these
mod files. Publish the compiled framework archive as a GitHub Release asset.
Do not publish OPERATOR executable files, game DLLs, user logs, screenshots,
tokens, or unrelated extracted projects.

## License

The repository source and documentation use the MIT License. OPERATOR,
BepInEx, Unity, Mirror, and other named products keep their own licenses.
