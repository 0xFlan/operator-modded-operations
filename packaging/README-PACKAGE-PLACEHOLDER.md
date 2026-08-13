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

The next Modded Operations `0.3.29` archive must carry the exact Operator Mod
API `0.2.0-alpha.6` preview runtime shown above. The API and
framework retain separate runtime ownership folders, but end users receive
them as one Modded Operations download. Do not substitute an unknown Core
build, publish a standalone preview-API archive, or copy either runtime into a
map archive. A separate public Operator Mod API release is deferred until the
API reaches a full stable version.

For the Kill House release set, publish exactly two downloads: this Modded
Operations archive (including the two API runtime DLLs) and the separate Kill
House archive. Do not emit a combined archive, a third API archive, or an API
metadata/staging owner.

The bundled alpha.6 preview-runtime pins from the final non-packaging build
are:

```text
OperatorModAPI.dll
bytes=179200
sha256=0C27854DFDD3C9F0946F5BCBC61CE37DAE3037215BB5FC11C3400BD50190EB77

OperatorModAPI.BepInEx.dll
bytes=25600
sha256=A58E1FA50CE345931104B9980AFBAF356B8EEAC0E7A735BEF7BD21FC93727AD9
```

Do not put bracketed placeholder files in a release ZIP. Build the framework
DLL from `src/OperatorModdedOperations`, source both API DLLs from the exact
pinned alpha.6 build, stage all three at the paths above, and verify every
staged file and ZIP-entry SHA-256 value before publication.
