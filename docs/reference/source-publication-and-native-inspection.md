# Source publication and current-build native inspection

## 1. What this repository publishes

This repository publishes the complete readable source of OPERATOR: Modded
Operations. The principal source files are:

```text
src/OperatorModdedOperations/CerberusNativeTabFix.cs
src/OperatorModdedOperations/NativeBundleAssetLoader.cs
src/OperatorModdedOperations/OperatorModdedOperations.csproj
```

The release DLL is built from these files. It is not the only implementation
artifact. A reviewer can rebuild, diff, and audit the framework.

## 2. Release DLL decompilation snapshot

`decompiled/release-0.3.19` is ILSpy `10.1.1.8388` output from the exact
152,576-byte release DLL with SHA-256
`E98A6989717BAE78159159504AAE1A3571041935D72947A6EDBDE1641A99CC7A`.
It lets a reviewer inspect what the compiler emitted even when the release DLL
is distributed separately.

`decompiled/archive/release-0.3.18` preserves the previous accepted snapshot.
`decompiled/archive/release-0.3.17` preserves the earlier rejected snapshot
with the old 52,241.375-lux daylight value. Do not copy that rejected value
into a current build.

The decompiled tree is not the edit source. It can use generated variable
names and can lose comments. Modify `src/OperatorModdedOperations`, rebuild,
then regenerate the snapshot from the final DLL.

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

## 7. Native board behavior evidence

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

## 8. Serialized asset inspection

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
assembly hashes, output DLL bytes, and output DLL SHA-256. A matching DLL hash
proves the build artifact. It does not prove runtime support.

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
