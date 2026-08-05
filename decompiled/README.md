# Decompiled release snapshots

This directory contains decompiler output for released OPERATOR: Modded
Operations DLLs. It is secondary verification evidence. The maintained source
is in `src/OperatorModdedOperations`.

The current snapshot is:

```text
[MOD DLL] OperatorModdedOperations.dll
version: 0.3.19
bytes: 159232
SHA-256: 257F5449463BF2D2E2BD71CBC3AEA513A1788E882578CA98B631FB70E2EB1F25
decompiler: ILSpy command-line tool 10.1.1.8388
output: decompiled/release-0.3.19
```

The repository does not include the input DLL in normal Git history. Obtain
the DLL from the matching release asset. A reviewer can hash that DLL and run:

```powershell
ilspycmd --disable-updatecheck --nested-directories -p `
  -o .\decompiled\release-0.3.19 `
  '<MOD_DLL>'
```

Decompiler names and formatting can differ from the authored source. Use the
snapshot to verify the compiled release surface. Make changes in `src`, not in
the generated snapshot.

The `decompiled/archive` directory contains hash-pinned historical snapshots.
They can contain rejected behavior. They are comparison evidence, not current
implementation instructions.
