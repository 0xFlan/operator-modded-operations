# Source publication and current-build native inspection

## 1. What this repository publishes

This repository publishes the complete readable source of OPERATOR: Modded
Operations. The principal source files are:

```text
src/OperatorModdedOperations/CerberusNativeTabFix.cs
src/OperatorModdedOperations/CerberusNativeTabFix.PvpPeerAgreement.cs
src/OperatorModdedOperations/NativeBundleAssetLoader.cs
src/OperatorModdedOperations/SceneVariantSelectionStore.cs
src/OperatorModdedOperations/OperatorModdedOperations.csproj
```

The release DLL is built from these files. It is not the only implementation
artifact. A reviewer can rebuild, diff, and audit the framework.

## 2. DLL decompilation snapshots

`decompiled/release-0.3.29` is ILSpy `10.1.1.8388` output from the exact
279,552-byte frozen Git source-checkpoint DLL with SHA-256
`95CEF59F62B2DF40ED69C066692953210CDC17A9C3D08DB95753DA7A9B4142CD`.
It is `PROVEN-STATIC` verification evidence, not a supported binary release or
host-plus-remote runtime proof.

`decompiled/release-0.3.28` is output from the exact
223,232-byte reviewed Release DLL with SHA-256
`75BB479E863A94807ACCB78E6FA37271BE340B226E4EBDFB1BE48C0B8248852B`.
It lets a reviewer inspect what the compiler emitted even when the release DLL
is distributed separately.

`decompiled/release-0.3.27`, `decompiled/release-0.3.26`, and
`decompiled/release-0.3.22` preserve prior snapshots.
`decompiled/archive/release-0.3.20` preserves the previous snapshot.
`decompiled/archive/release-0.3.19` preserves an earlier snapshot.
`decompiled/archive/release-0.3.18` preserves an earlier accepted snapshot.
`decompiled/archive/release-0.3.17` preserves the earlier rejected snapshot
with the old 52,241.375-lux daylight value. Do not copy that rejected value
into a current build.

The decompiled tree is not the edit source. It can use generated variable
names and can lose comments. Modify `src/OperatorModdedOperations`, rebuild,
then regenerate the snapshot from the final DLL.

The frozen `0.3.29` candidate has a separately labeled multiplayer-test
transfer and the source-checkpoint snapshot above. It does not have a promoted
runtime-release publication-source-state record. Host-plus-remote runtime gates
remain open; do not describe these bytes as `SUPPORTED`.

## 3. What is not decompiled mod source

OPERATOR is an IL2CPP game. The generated assemblies under
`<OPERATOR_INSTALL>/BepInEx/interop` expose managed type names, fields, method
signatures, and wrapper calls. They do not contain the complete original C#
method bodies.

This repository does not pretend that an interop wrapper is an original game
source file. It documents a current-build native contract as one of:

- exact generated signature evidence;
- exact serialized asset evidence;
- read-only native-body behavior evidence;
- controlled runtime evidence;
- an explicitly labeled inference.

Do not commit the game's interop assemblies, native binary, metadata, or
extracted first-party source to this repository.

## 4. Reproduce a signature inspection

Use the installed generated interop assemblies. Open the assembly that owns
the target type in a managed assembly browser. Record:

1. assembly filename and SHA-256;
2. exact namespace and type;
3. base type;
4. field name and field type;
5. method name, return type, and parameter types;
6. whether the method is a wrapper, generated command body, RPC body, or a
   normal managed helper;
7. every source call site in this framework.

Examples of required current-build signatures are:

```text
CerebusOpboard.Start_Operation()
InfilSelectorDisplayer.SpawnMap(GameObject)
PlayerMaster.SpawnPlayer()
RaidManager.ServerSpawnAI(bool)
GameManager.MovePlayerToSpawn(...)
NetworkClient.RegisterPrefab(...)
NetworkClient.UnregisterSpawnHandler(uint)
```

Use exact signatures from the pinned installation. Do not copy a signature
from another game version.

## 5. Distinguish wrapper and generated body

Mirror and Il2CppInterop can expose multiple methods with related names. For
example, a command can have a public wrapper and an exact generated server
body. Calling the wrong member can send another network command or skip the
server implementation.

The repeat-host player recovery uses the exact generated member
`UserCode_CMDSpawnPlayer__NetworkIdentity` only after the normal
`PlayerMaster.SpawnPlayer()` request fails for 300 frames. The generated body
is not the default path.

Record both members and their call direction before you use one.

## 6. Native behavior evidence used by PVE

The framework source calls:

```csharp
raid.ServerSpawnAI(false);
```

Read-only current-build inspection established this behavior order:

```text
select a prefab from RaidManager.standardAI
-> instantiate the AI
-> NetworkServer.Spawn(bot, GameManager.instance.gameObject)
-> apply BotSpawnDetails
```

This owner argument is material. The rejected manual adapter did not preserve
the same ownership sequence. Server grenades worked, but firearm damage was
incomplete.

The documentation states the bounded behavior that the adapter depends on. It
does not reproduce the full proprietary method body.

## 7. Native loading-presentation evidence

Read-only supported-build inspection established these native addresses:

```text
GameManagerNetwork.ShowLoadingScreen  RVA 0x00916210
GameManagerNetwork.HideLoadingScreen  RVA 0x0090E950
GameManagerNetwork.get_LoadingScreenVisible RVA 0x0091A840
```

`OnAllPlayersLoaded(false)` enters `ShowLoadingScreen`. The method activates
the shipped loading canvas, freezes the current player body, clears velocity,
and closes the infiltration UI. The property getter returns the private
`_hideLoadingScreenSoon` byte at offset `0x2A4`; it is not the canvas active
state. Runtime diagnostics therefore read `LoadingScreen.activeSelf` and
`activeInHierarchy`.

The framework calls the exact shipped show method when an additive package
scene enters and before runtime terrain/material preparation. The persistent
native manager keeps ownership of the hide transition.

## 8. Native board behavior evidence

The supported launch entry is:

```csharp
CloseNativeMapConfirmation(presentation.Board, false);
PrimeNativeInfiltrationSelector(board, operation);
board.Start_Operation();
```

The board method consumes the complete private owner graph and transfers data
to the persistent operation manager. Direct calls to
`OperationsManager.StartOperation`, `CMD_StartOperation`, or
`DebugStartOperation` do not reproduce the same transaction.

The framework source is the executable proof of the adapter order. The
installed interop types prove the called signatures. Physical first-Confirm
testing proves the user flow.

## 9. Serialized asset inspection

Use a Unity asset reader for scene, prefab, material, and shader records. Set
the fallback version to `6000.3.8f1` for version-stripped Unity files when the
tool requires it.

For every serialized fact, record:

```text
owner file
path ID
class ID or type
object name
PPtr file ID and path ID
resolved external owner
value
```

A path ID without its owner file is ambiguous.

## 9. Publish evidence without private data

Use placeholders:

```text
<OPERATOR_INSTALL>
<INTEROP_ASSEMBLY>
<INSTALLED_NATIVE_BINARY>
[AUTHORIZED PREFAB ASSET]
[BUILT ASSETBUNDLE]
```

Do not publish a user path, private log, access token, or unrelated extracted
project. Commit an audit result, method signature, source member, and
reproduction procedure instead.

## 10. Rebuild proof

Build with:

```powershell
dotnet build .\src\OperatorModdedOperations\OperatorModdedOperations.csproj `
  -c Release `
  -p:OperatorGameDir='<OPERATOR_INSTALL>' `
  -p:OperatorModApiProject='<OPERATOR_MOD_API_REPOSITORY>\src\OperatorModAPI\OperatorModAPI.csproj'
```

Require zero warnings and zero errors. Record the source commit, interop
assembly hashes, output DLL bytes, and output DLL SHA-256. For an explicit
public promotion, create a new versioned decompiler snapshot and source-state
record from the exact release source and binary. The checked-in
`publication/source-state-manifest.json` remains the immutable `0.3.28` record;
normal source work must not rewrite it to describe an unpromoted candidate. The
repository audit verifies that historical record's sidecar and pinned identity
without requiring an ignored local DLL. A matching DLL hash proves the build
artifact. It does not prove runtime support.

## 11. Review checklist

- Complete framework source is present.
- A decompiled snapshot matches the final release DLL hash.
- No release behavior exists only in an unpublished binary.
- Native method signatures name the pinned installed evidence.
- Native behavior summaries distinguish evidence from inference.
- Serialized records include owner file and path ID.
- Omitted assets have explicit placeholders.
- No private path or game binary is committed.
- Physical launch, PVE, PVP, restart, and teardown evidence is tracked
  separately.
- Test-transfer artifacts are labeled as non-public and do not replace a
  promoted decompiler/source-state record.
