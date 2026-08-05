# Drag-and-drop package placeholders

The Git repository stores source and documentation. A release workflow stages
this install-root-relative tree:

```text
<OPERATOR_INSTALL>/
  BepInEx/
    plugins/
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
[SHIPPED FAILURE AND RESTART UI]
```

The required Operator Mod API files are a separate product. Do not silently
copy an unknown Core build into the framework archive. Declare the exact Core
version on the release page.

Do not put bracketed placeholder files in a release ZIP. Build the DLL from
`src/OperatorModdedOperations`, stage it at the exact path above, and verify
the staged file and ZIP-entry SHA-256 values before publication.
