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

## Current implementation checkpoint

The active source checkpoint is Modded Operations `0.3.33` with bundled
Operator Mod API `0.2.0-alpha.7`. The same framework source builds an isolated
BepInEx DLL and an isolated MelonLoader DLL; install exactly one loader variant.
Map packages remain shared data under `OPERATOR/OperatorMods`.

Modded PVE now exposes OPERATOR's shipped briefing enemy slider. The package's
inclusive `minEnemies`/`maxEnemies` range is available up to an absolute limit
of 100. Confirm captures the host's value atomically, Restart retains it, and a
new Operation Room Confirm captures a new value. PVP is unchanged and does not
show the control. Before the one native raid spawn, the selected value is
revalidated against the package and safe navigation-marker capacity. Existing
companions may keep utility spawn markers inactive; active-marker count remains
telemetry. Eligibility requires live navigation plus a stable name-ordered
subset whose accepted X/Z positions remain at least 2 m apart after snapping.

Peer agreement protocol v6 is active for both the PVE and PVP multiplayer-test
paths. Each
peer validates the selected-loader suite receipt, its exact receipt-owned
manifest sidecar and files, and the loaded Core/host/framework/declared-
companion paths before the native scene transition. PVE also binds the declared
range and host-confirmed enemy count. Scene, injected-owner, owner-local player
placement, and, for PVE, server-authored AI-population receipts must all pass
before the host commits gameplay. PVP uses the shipped native `PvpGameode`,
requires six markers per team for a declared 12-player operation, and requires
zero PVE AI. BepInEx and MelonLoader variants are both built and byte-pinned;
only one loader may be active in an OPERATOR install. Live separate host/remote
proof remains pending for each PVE/PVP and loader pairing;
offline builds, tests, and binary audits do not substitute for that matrix.

The PVE runtime barrier additionally verifies the shipped player
`NetworkIdentity`, `SmoothSyncMirror`, `NetworkAnimatorSmooth`, `Health`, and
locally owned weapon identities. Every server-authored AI must expose its exact
`BrainAI`, team, `Health`, `WeaponsAI`, `NetworkAnimatorSyncNPC`, and two native
`SmoothSyncMirror` behaviours on the same Mirror identity. The framework does
not replace player/AI transform replication, bullet commands, damage, or death;
those remain owned by OPERATOR and Mirror. Every evidence line carries one
process-run ID and a monotonic event sequence so a future host/client verifier
cannot combine different launches or rely on cross-region wall clocks.

For companions, `runtimeContentId` is the lowercase SHA-256 of the UTF-8 lines
`operator-loader-neutral-runtime-pair-v1`, plugin GUID, plugin version,
BepInEx DLL SHA-256, and MelonLoader DLL SHA-256, in that order with `\n`
separators and no trailing newline. Generate it with
`tools/operator_runtime_content_id.py`; runtime resolution recomputes it and
fails closed on any mismatch.

The frozen `0.3.33` all-map multiplayer test-candidate identities are:

```text
OperatorModdedOperations.dll                     bytes=644608
sha256=601D587C889A6AF17142986B1B63427033872384120EE086E4892F0031928DAB
OperatorModdedOperations.MelonLoader.dll         bytes=646144
sha256=FBB82582F22FB64387704CC685D4084765B2733F7C2C6B3616E19282433D7937
OperatorModAPI.dll                               bytes=204800
sha256=5B74AC25B4047D9AB8E9929D136C53B7543DA52B621FAF3D4AF42719D276E21E
OperatorModAPI.BepInEx.dll                       bytes=15872
sha256=6223553C5406AD3586F23EA2B5F05C6F4626FA62A03E0598667E09A5486E1E3B
OperatorModAPI.MelonLoader.dll                   bytes=26112
sha256=79A8491D2497C2A72859C6B05DD6BA8E475327FA54DB47C0C5F3881F59E6CF6A
```

PVE marker resolution first uses explicit `PVE_PlayerSpawn_*` markers. Shared
Team 1 markers are accepted only as a legacy fallback when no explicit PVE
markers exist. PVP uses only its team-specific marker sets, preventing a map
that carries both modes from mixing their insertion points.

The complete Python suite passes `110/110`; the scene-variant selector passes
`16/16`; the native game-mode, native-policy, and runtime-barrier test projects
pass; and both loader builds complete with zero warnings and zero errors. This
is `PROVEN-STATIC`, not `SUPPORTED` multiplayer evidence.

## Historical checkpoint record (0.3.29 and earlier)

The end-user distribution keeps the established framework-plus-map workflow.
When `0.3.29` is promoted, one Modded Operations archive will include the exact
Operator Mod API `0.2.0-alpha.6` preview runtime under
`BepInEx/plugins/OperatorModAPI` and the framework under
`BepInEx/plugins/OperatorModdedOperations`. These remain separately owned
runtime folders, but they are not separate downloads. The preview API is not a
standalone public release; that begins only when Operator Mod API reaches a
full stable version. Map archives must not duplicate either runtime.

The authored framework is now the `0.3.29` source candidate. Standalone PVP
uses mode-isolated spawn discovery and requires at least
`ceil(maximumPlayers / 2)` markers per side. Before native launch, the host
freezes the authenticated remote peer set and requires every peer to match the
exact framework DLL, API Core DLL, API host DLL, game build, package
ID/version/content hash, declared companion GUID/version/DLL hash and marker
contract, operation, spawn set, scene variant/path, time, and player-range
identity. The package content identity covers the manifest and every declared
package file, so locally edited same-version content does not match. A remote peer
preloads the locally verified package and commits the matching operation
before its content-ready acknowledgement. After scene load, every peer must
construct and register the deterministic native `PvpGameode` template and pass
the declared exact-scene companion readiness marker before the host can spawn
it. Scene readiness is a request/acknowledgement barrier bound to a nonzero,
host-issued generation epoch. Restart increments that epoch exactly once, and
the remote acknowledges only the corresponding exact local scene generation;
stale readiness cannot cross load-before-unload callback ordering. Companion
failure markers remain fatal after scene readiness. Native-owner
adoption, readiness initialization, and all-players-loaded also have bounded
fail-closed deadlines. Membership changes, identity mismatches, handler
collisions, malformed messages, and timeouts abort the native transition. The
frozen set records connection-object identity as well as numeric connection ID,
so ID reuse cannot inherit readiness. Late join is unsupported: a join,
replacement, or disconnect during agreement fails closed instead of changing
the session roster. PVE paths do not enter this barrier. This means the PVP
agreement does not prove PVE co-op content, scene, AI, movement, or combat
equivalence; online PVE remains unproven.

The frozen `0.3.29` candidate identities are:

```text
OperatorModdedOperations.dll  bytes=279552
sha256=95CEF59F62B2DF40ED69C066692953210CDC17A9C3D08DB95753DA7A9B4142CD
OperatorModAPI.dll            bytes=179200
sha256=0C27854DFDD3C9F0946F5BCBC61CE37DAE3037215BB5FC11C3400BD50190EB77
OperatorModAPI.BepInEx.dll    bytes=25600
sha256=A58E1FA50CE345931104B9980AFBAF356B8EEAC0E7A735BEF7BD21FC93727AD9
```

The source, all 78 Python regression tests, including 39 focused PVP
agreement/restart tests, the separate 9-test C# scene-variant selector suite,
and a zero-warning Release build are `PROVEN-STATIC`. Host-plus-remote exact-content
transport, first spawn, movement replication, firearm-specific hit
registration, opposite teams, scoring, round respawn, Restart, and return
still require a controlled live matrix before standalone PVP is called
`SUPPORTED`.

A separately labeled transfer archive exists only for that multiplayer matrix:
`OperatorModdedOperations_v0.3.29_API-alpha.6_MULTIPLAYER_TEST_ONLY.zip`,
486,369 bytes, SHA-256
`4507C858888339B19F318F7D23B55F771B53E102CDA2B335F95B52BDF91FC1B8`.
It is test packaging, not a Nexus binary release and not live multiplayer
evidence. The repository now publishes the exact `0.3.29` decompiler snapshot
as a Git source checkpoint, but no binary release, publication-source-state
promotion, or `SUPPORTED` claim exists. The last public runtime-release record
remains `0.3.28` with bundled API `0.2.0-alpha.5`.

The following section records the historical `0.3.29` source checkpoint; the
active candidate is `0.3.33` as identified at the top of this document. The
last hash-pinned public runtime-release record in that historical section is
`0.3.28`.
Manifest-driven scene variants are
strictly opt-in: only a map with `SceneVariants.Count > 1`
enters the selector. Each fresh Operation Room Confirm chooses from a
persistent per-package-and-map shuffle bag after exact scene-bundle
validation. Alive Restart and KIA Restart retain the active scene, while the
persisted history prevents an immediate repeat after an OPERATOR process
restart. A single-scene map does not create or access variant state, consume
variant RNG, or emit variant-selection logs. Shared variant bundles must
exactly match the union of their declared scene inventories.

Verified package bundles now use a safe cross-map lifetime. Same-map alive
and KIA Restart keep the active bundle resident. A different selected-map
prefetch can overlap that restart owner, but fresh launch ownership transfer
returns the completed cache to one map. Eviction runs only in the Operation
Room with no active package-scene handle and no loaded scene owned by the
candidate bundle.

The exact reviewed `0.3.28` Release DLL is 223,232 bytes with SHA-256
`75BB479E863A94807ACCB78E6FA37271BE340B226E4EBDFB1BE48C0B8248852B`.
It snapshots Mirror's authoritative spawned registry around the one native AI
population call, owns the exact netId/identity/brain delta, and defers full
native team/reference/target validation from the next Unity frame through a
bounded 60-frame deadline. Extraction remains locked while validation is
pending or failed, and failure/restart/unload destroys only that exact owned
population before releasing the standalone game mode. The exact binary,
ILSpy tree, and complete dirty/untracked publication source state are
hash-pinned. The 39-test Python suite passes and the Release build completes
with zero warnings and zero errors. This is publication and static evidence;
the exact `0.3.28` bytes are not yet relabeled as runtime-accepted evidence.

The prior `0.3.27` publication remains historical comparison evidence: its
Release DLL is 216,064 bytes with SHA-256
`CD2B326E76FC95439352E999BB138EB4499B08895C5BE552807D2C8A7FF62579`.
It retains the prior Mirror bootstrap, cache-lifetime, scene-variant, and
bounded-first-wander corrections. It adds native-team cohort isolation so
standalone PVE bots cannot select another spawned cohort member as an enemy. It also consumes
optional native reaction disposition and maximum-reaction-time fields; omitted
fields perform no corresponding AI write. Its stricter diagnostics report the
team and target graph and count only enabled brains with existing navigation
entities as live. The exact binary, ILSpy tree, and complete publication source
state are hash-pinned; this static publication record does not relabel older
runtime observations as current-hash live evidence.
The last complete runtime candidate remains `0.3.24`, 195,584 bytes with SHA-256
`0B61F0C3CCEC667B5FD38BAD7884C8F7349479F61AE3682F4DD4BB08C8243992`.
Kill House runtime checks retained `KH05_SplitSpine` across alive Restart,
selected `KH09_Pinwheel` in the next process and retained it across alive
Restart, and retained `KH08_DoubleBack` across the shipped KIA Restart path.
The complete same-process lifecycle then selected `KH10_WideLabyrinth`, kept
KH10 across alive Restart, returned to the Operation Room through native
mission completion, selected `KH03_SerpentineApartment` on the next fresh
Confirm, and kept KH03 across KIA Restart.

Established local testing on the pinned game build also accepts first launch,
one-click Confirm, complete unload, repeat launch, player grounding, 11:00
native daylight, and 02:00 four-tube white-phosphor NVG behavior. Two complete
Forest PVE runs accept the fixed profile, positive native search delays,
native agent movement, movement toward insertion, and foliage sight
obstruction. Release-version completion
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
| `src/OperatorModdedOperations` | Shared source for isolated BepInEx and MelonLoader framework assemblies. |
| `decompiled` | Hash-pinned decompiler snapshots of release DLLs. |
| `publication` | Deterministic source-state manifest and SHA-256 sidecar. |
| `packaging` | Drag-and-drop release-layout placeholders. |
| `schemas/operator-map-package.schema.json` | Closed legacy schema v1. |
| `schemas/operator-map-package-v2.schema.json` | Closed schema v2 with fixed PVE AI and optional runtime-companion contracts. |
| `examples/operator-map-package.example.json` | Complete PVE and PVP manifest example. |
| `MODDED_OPERATIONS_TECHNICAL_BIBLE.md` | Complete implementation and maintenance index. |
| `docs/architecture` | Ownership, UI, launch, game-mode, and teardown contracts. |
| `docs/guides` | Build, install, map-package, and test procedures. |
| `docs/reference` | Exact source members, native types, manifest fields, and diagnostics. |

Read [native PVE completion, extraction, and ATAK](docs/architecture/native-pve-completion-exfil-and-atak.md)
for the exact map marker, runtime owner, native unlock, ATAK visual, success,
and restart contract.

## Required local build inputs

Building this repository requires these local inputs. This is a maintainer
build list, not an end-user download list:

- OPERATOR with Unity `6000.3.8f1` for the pinned source state;
- either the supported BepInEx IL2CPP or MelonLoader toolchain;
- generated interop assemblies under `<OPERATOR_INSTALL>/BepInEx/interop`;
- Operator Mod API `0.2.0-alpha.7` source or exact prebuilt binaries.

A promoted Modded Operations archive supplies its pinned preview API runtime
to end users. The separately labeled `0.3.29` multiplayer test transfer does
the same for controlled peer testing only. OPERATOR, BepInEx, and generated
interop assemblies are not redistributed by this repository.

## Build

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

The outputs are `src/OperatorModdedOperations/bin/Release/OperatorModdedOperations.dll`
and `src/OperatorModdedOperations/bin/Release/MelonLoader/OperatorModdedOperations.MelonLoader.dll`.

## Install

Install one matching suite for one loader. Never place both host variants in
the same game tree. Core and the framework use these separately owned runtime
layouts:

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
  Mods/
    OperatorModAPI.dll
    OperatorModAPI.MelonLoader.dll
    OperatorModdedOperations.MelonLoader.dll
  OperatorMods/
    <package-id>/
      operator-map-package.json
      content/...
      media/...
```

The `BepInEx` and `Mods` portions above are alternatives, not a combined
installation. Do not put a map companion in `OperatorMods`; put its selected
binary under `BepInEx/plugins` or `Mods`. Install each map as its own download.
The transactional runtime-suite installer owns only receipt-listed executable
payloads and its sidecar/receipt. It does not install, update, remove, or
rewrite `OperatorMods` packages.

Online support requires two distinct OPERATOR processes starting together.
Both must pass exact suite/package agreement, leave loading, remain grounded,
complete the mode-specific combat flow, Restart, and return/close cleanly while
late join and membership changes fail within their bound. PVE additionally
requires the host-selected count, AI replication, completion, extraction, and
capacity/performance/teardown at the map's claimed maximum. Solo, host-only,
and join-in-progress success are insufficient.

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
- To verify the exact dirty/untracked publication source state, read
  [Publication source state](publication/README.md).
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
