# Drag-and-drop package placeholders

The Git repository stores source and documentation. A release workflow stages
this install-root-relative tree:

```text
<OPERATOR_INSTALL>/
  README_MODDED_OPERATIONS.md
  CHANGELOG_MODDED_OPERATIONS.md
  LICENSE_MODDED_OPERATIONS.txt
  LICENSE_OPERATOR_MOD_API_RUNTIME.txt
  BUNDLED_OPERATOR_MOD_API_RUNTIME_NOTICE.md
  CHECKSUMS_OPERATOR_MOD_API_RUNTIME.sha256
  CHECKSUMS_MODDED_OPERATIONS.sha256
  BepInEx/
    plugins/
      OperatorModAPI/
        [PINNED API CORE DLL] OperatorModAPI.dll
        [PINNED API BEPINEX HOST DLL] OperatorModAPI.BepInEx.dll
      OperatorModdedOperations/
        [FRAMEWORK DLL] OperatorModdedOperations.dll
  Mods/
    [PINNED API CORE DLL] OperatorModAPI.dll
    [PINNED API MELONLOADER HOST DLL] OperatorModAPI.MelonLoader.dll
    [FRAMEWORK MELONLOADER DLL] OperatorModdedOperations.MelonLoader.dll
  LoaderSelector/
    select_operator_mod_loader.ps1
    operator_loader_selector_manifest.json
```

The framework has no private scene, map, texture, model, material, or prefab
payload. It clones the shipped Cerberus UI at runtime. These first-party
objects remain in the installed game and are not copied into this package:

```text
[SHIPPED CERBERUS LAPTOP ROOT]
[SHIPPED CERBERUS OPERATIONS TAB]
[SHIPPED OPERATION ROW]
[SHIPPED CERBERUS OPERATION BOARD]
[SHIPPED INFILTRATION SELECTOR]
[SHIPPED FAILURE, SUCCESS, AND RESTART UI]
[SHIPPED EXFILZONE TEXTURE]
[SHIPPED HDRP/UNLIT SHADER]
```

The next Modded Operations `0.3.35` archive must carry the exact Operator Mod
API `0.2.0-alpha.8` preview runtime shown above. The API and
framework retain separate runtime ownership folders, but end users receive
them as one Modded Operations download. Do not substitute an unknown Core
build, publish a standalone preview-API archive, or copy either runtime into a
map archive. A separate public Operator Mod API release is deferred until the
API reaches a full stable version.

Both managed runtime trees may remain installed. Before OPERATOR starts, the
selector must leave exactly one approved native loader bootstrap active. A
deliberately dual-active native state is unsupported and must make both API
hosts fail closed before Modded Operations mutates the process.

For the Kill House release set, publish exactly two downloads: this Modded
Operations archive (including the two API runtime DLLs) and the separate Kill
House archive. Do not emit a combined archive, a third API archive, or an API
metadata/staging owner.

The bundled alpha.8 preview-runtime pins from the final non-packaging build
are:

```text
OperatorModAPI.dll
bytes=209920
sha256=30EA90556EED4D911107F4985F4866ECDB76AE9F11A2C9DCB3B1B5509F1FD0B

OperatorModAPI.BepInEx.dll
bytes=15872
sha256=E1B93289FB5C4846FA0DE8182ABB7C91E0AB1F2A3B75586CDE9BAD69C6962B98

OperatorModAPI.MelonLoader.dll
bytes=26624
sha256=1794332260BE62B45B4AB87538E601AD420A09D40CFB28CBAA1D76A865286828
```

Do not put bracketed placeholder files in a release ZIP. Build the framework
DLLs from `src/OperatorModdedOperations`, source the Core and both API hosts
from the exact pinned alpha.8 build, stage both managed layouts and the two
selector files at the paths above, and verify every
staged file and ZIP-entry SHA-256 value before publication.
