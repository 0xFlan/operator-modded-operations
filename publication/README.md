# Publication source state

`source-state-manifest.json` is the immutable historical record that binds the
complete explicit public-source scope for OPERATOR: Modded Operations `0.3.28`
to file lengths and SHA-256 values. It
also records the root and nested Git repository state, the exact bound DLL,
and the exact ILSpy tool used for the decompiler snapshot. It contains no
absolute machine paths or copied game binaries.

Do not regenerate this record from a newer working tree. The generator remains
available to reproduce it only from the exact `0.3.28` source checkout with its
pinned Release DLL:

```powershell
python .\eng\generate_publication_source_state.py
```

Generation refuses a missing or mismatched Release DLL and refuses any
decompiler-tree drift, so the manifest cannot silently claim different binary
inputs.

The normal repository audit verifies the checked-in manifest bytes, sidecar,
and pinned `0.3.28` identity without requiring an ignored local binary. From an
exact historical checkout with the pinned binary, the stricter generator check
is:

```powershell
python .\eng\generate_publication_source_state.py --check
```

The adjacent `.sha256` file pins the exact manifest bytes. Build outputs,
archives, local evidence, QA state, and the manifest files themselves are
outside the source inventory by explicit policy.
