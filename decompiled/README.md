# Decompiled release snapshots

This directory contains decompiler output for released OPERATOR: Modded
Operations DLLs and explicitly labeled Git source checkpoints. It is secondary
verification evidence. The maintained source is in `src/OperatorModdedOperations`.

The current Git source-checkpoint snapshot is:

```text
[MOD DLL] OperatorModdedOperations.dll
version: 0.3.29
status: PROVEN-STATIC multiplayer-test source checkpoint; not a supported binary release
bytes: 279552
SHA-256: 95CEF59F62B2DF40ED69C066692953210CDC17A9C3D08DB95753DA7A9B4142CD
decompiler: ILSpy command-line tool 10.1.1.8388
decompiler executable bytes: 162816
decompiler executable SHA-256: 1B14D01FFB011887C8277B309E615CACCE72678CD5E87F56C29860190116F0DC
output: decompiled/release-0.3.29
output files: 7
output bytes: 543935
output tree SHA-256: 059C0050B8EF727FAC73745B917185668ACA8C35FDC7C15DFA36C14AA7381714
```

The output-tree digest hashes the UTF-8 concatenation of each ordinal-sorted
record `relative-path NUL decimal-bytes NUL uppercase-file-SHA-256 LF`.

The last public runtime-release snapshot remains:

```text
[MOD DLL] OperatorModdedOperations.dll
version: 0.3.28
bytes: 223232
SHA-256: 75BB479E863A94807ACCB78E6FA37271BE340B226E4EBDFB1BE48C0B8248852B
decompiler: ILSpy command-line tool 10.1.1.8388
decompiler executable bytes: 162816
decompiler executable SHA-256: 1B14D01FFB011887C8277B309E615CACCE72678CD5E87F56C29860190116F0DC
output: decompiled/release-0.3.28
output files: 7
output bytes: 427039
output tree SHA-256: 59D672462C7AA4DA00268D3A2185EA79D2C43F078853F71E40EAE40FDEE361C8
```

The repository does not include the input DLL in normal Git history. Obtain
the DLL from the matching release asset. A reviewer can hash that DLL and run:

```powershell
ilspycmd --disable-updatecheck --nested-directories -p `
  -o .\decompiled\release-0.3.29 `
  '<MOD_DLL>'
```

Decompiler names and formatting can differ from the authored source. Use the
snapshot to verify the compiled release surface. Make changes in `src`, not in
the generated snapshot.

The `decompiled/release-0.3.28` tree remains the prior public runtime-release
snapshot and publication-manifest identity. The prior
`decompiled/release-0.3.27`, `decompiled/release-0.3.26`, and
`decompiled/release-0.3.22` trees and the snapshots beneath
`decompiled/archive` are hash-pinned historical evidence. Historical trees
can contain rejected behavior. They are comparison evidence, not current
implementation instructions.
