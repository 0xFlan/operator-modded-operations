from __future__ import annotations

import argparse
import hashlib
import json
import os
import subprocess
import sys
from pathlib import Path, PurePosixPath


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "publication/source-state-manifest.json"
SIDECAR = ROOT / "publication/source-state-manifest.sha256"
REPOSITORY_ID = "operator-modded-operations"
RELEASE_VERSION = "0.3.28"

ALLOWED_SUFFIXES = {
    ".cs",
    ".csproj",
    ".editorconfig",
    ".json",
    ".md",
    ".props",
    ".ps1",
    ".py",
    ".slnx",
    ".targets",
    ".txt",
    ".yaml",
    ".yml",
}
ALLOWED_NAMES = {".gitattributes", ".gitignore", "LICENSE"}
EXCLUDED_DIRECTORY_NAMES = {
    ".git",
    ".idea",
    ".local",
    ".vs",
    ".vscode",
    "__pycache__",
    "artifacts",
    "bin",
    "obj",
    "release",
    "TestResults",
}
EXCLUDED_PATHS = {
    OUTPUT.relative_to(ROOT).as_posix(),
    SIDECAR.relative_to(ROOT).as_posix(),
}

ARTIFACTS = (
    {
        "name": "OperatorModdedOperations.dll",
        "version": "0.3.28",
        "bytes": 223232,
        "sha256": "75BB479E863A94807ACCB78E6FA37271BE340B226E4EBDFB1BE48C0B8248852B",
        "sourceRelativePath": "src/OperatorModdedOperations/bin/Release/OperatorModdedOperations.dll",
        "decompiledOutput": "decompiled/release-0.3.28",
        "decompiledFiles": 7,
        "decompiledBytes": 427039,
        "decompiledTreeSha256": "59D672462C7AA4DA00268D3A2185EA79D2C43F078853F71E40EAE40FDEE361C8",
    },
)
DECOMPILER = {
    "name": "ILSpy command-line tool",
    "version": "10.1.1.8388",
    "executable": "ilspycmd.exe",
    "bytes": 162816,
    "sha256": "1B14D01FFB011887C8277B309E615CACCE72678CD5E87F56C29860190116F0DC",
}


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def run_git(repository: Path, *arguments: str) -> bytes:
    return subprocess.check_output(
        ["git", "-C", str(repository), *arguments],
        stderr=subprocess.STDOUT,
    )


def is_source_path(relative: str) -> bool:
    normalized = relative.replace("\\", "/").strip("/")
    if not normalized or normalized in EXCLUDED_PATHS:
        return False
    path = PurePosixPath(normalized)
    if any(part in EXCLUDED_DIRECTORY_NAMES for part in path.parts[:-1]):
        return False
    return path.name in ALLOWED_NAMES or path.suffix.lower() in ALLOWED_SUFFIXES


def discover_repositories() -> list[Path]:
    repositories = [ROOT]
    for current, directories, _ in os.walk(ROOT):
        current_path = Path(current)
        directories[:] = sorted(
            name for name in directories if name not in EXCLUDED_DIRECTORY_NAMES
        )
        if current_path != ROOT and (
            (current_path / ".git").is_dir() or (current_path / ".git").is_file()
        ):
            repositories.append(current_path)
            directories[:] = []
    return sorted(repositories, key=lambda path: path.relative_to(ROOT).as_posix())


def parse_status(repository: Path) -> list[dict[str, str]]:
    raw = run_git(
        repository,
        "-c",
        "core.quotepath=false",
        "status",
        "--porcelain=v1",
        "--untracked-files=all",
        "-z",
    ).decode("utf-8", errors="strict")
    fields = raw.split("\0")
    changes: list[dict[str, str]] = []
    index = 0
    while index < len(fields):
        field = fields[index]
        index += 1
        if not field:
            continue
        if len(field) < 4 or field[2] != " ":
            raise RuntimeError(f"Unexpected git status record: {field!r}")
        code = field[:2]
        path = field[3:].replace("\\", "/")
        row = {"code": code, "path": path}
        if "R" in code or "C" in code:
            if index >= len(fields) or not fields[index]:
                raise RuntimeError(f"Missing rename/copy source for {field!r}")
            row["originalPath"] = fields[index].replace("\\", "/")
            index += 1
        if is_source_path(path) or is_source_path(row.get("originalPath", "")):
            changes.append(row)
    return sorted(
        changes,
        key=lambda row: (row["path"], row.get("originalPath", ""), row["code"]),
    )


def repository_state(repository: Path) -> dict[str, object]:
    relative = repository.relative_to(ROOT).as_posix() or "."
    head = run_git(repository, "rev-parse", "HEAD").decode("ascii").strip()
    changes = parse_status(repository)
    return {
        "path": relative,
        "head": head,
        "dirty": bool(changes),
        "changes": changes,
    }


def source_files() -> list[dict[str, object]]:
    rows: list[dict[str, object]] = []
    for path in sorted(ROOT.rglob("*"), key=lambda item: item.as_posix()):
        if not path.is_file():
            continue
        relative = path.relative_to(ROOT).as_posix()
        if not is_source_path(relative):
            continue
        data = path.read_bytes()
        rows.append(
            {
                "path": relative,
                "bytes": len(data),
                "sha256": sha256_bytes(data),
            }
        )
    return rows


def validate_decompiled_artifacts() -> None:
    for artifact in ARTIFACTS:
        output = ROOT / str(artifact["decompiledOutput"])
        files = sorted(
            (path for path in output.rglob("*") if path.is_file()),
            key=lambda path: path.relative_to(output).as_posix(),
        )
        records: list[str] = []
        total_bytes = 0
        for path in files:
            data = path.read_bytes()
            total_bytes += len(data)
            records.append(
                f"{path.relative_to(output).as_posix()}\0{len(data)}\0"
                f"{sha256_bytes(data)}\n"
            )
        tree_sha256 = sha256_bytes("".join(records).encode("utf-8"))
        actual = (len(files), total_bytes, tree_sha256)
        expected = (
            int(artifact["decompiledFiles"]),
            int(artifact["decompiledBytes"]),
            str(artifact["decompiledTreeSha256"]),
        )
        if actual != expected:
            raise RuntimeError(
                f"Decompiler tree identity mismatch for {artifact['name']}: "
                f"expected={expected!r}; actual={actual!r}"
            )


def validate_release_artifacts() -> None:
    for artifact in ARTIFACTS:
        path = ROOT / str(artifact["sourceRelativePath"])
        if not path.is_file():
            raise RuntimeError(f"Release artifact is missing: {path.relative_to(ROOT)}")
        data = path.read_bytes()
        actual = (len(data), sha256_bytes(data))
        expected = (int(artifact["bytes"]), str(artifact["sha256"]))
        if actual != expected:
            raise RuntimeError(
                f"Release artifact identity mismatch for {artifact['name']}: "
                f"expected={expected!r}; actual={actual!r}"
            )


def build_manifest() -> dict[str, object]:
    validate_release_artifacts()
    validate_decompiled_artifacts()
    repositories = [repository_state(path) for path in discover_repositories()]
    files = source_files()
    inventory_preimage = "".join(
        f"{row['path']}\0{row['bytes']}\0{row['sha256']}\n" for row in files
    ).encode("utf-8")
    state_preimage = json.dumps(
        repositories, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    return {
        "schemaVersion": 1,
        "repository": REPOSITORY_ID,
        "releaseVersion": RELEASE_VERSION,
        "scope": {
            "policy": "explicit-publication-source-v1",
            "allowedSuffixes": sorted(ALLOWED_SUFFIXES),
            "allowedNames": sorted(ALLOWED_NAMES),
            "excludedDirectoryNames": sorted(EXCLUDED_DIRECTORY_NAMES),
            "excludedPaths": sorted(EXCLUDED_PATHS),
        },
        "repositories": repositories,
        "repositoryStateSha256": sha256_bytes(state_preimage),
        "artifacts": list(ARTIFACTS),
        "decompiler": DECOMPILER,
        "files": files,
        "fileCount": len(files),
        "inventorySha256": sha256_bytes(inventory_preimage),
    }


def serialized_manifest() -> bytes:
    return (
        json.dumps(build_manifest(), ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")


def write_outputs(data: bytes) -> None:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    temporary = OUTPUT.with_suffix(OUTPUT.suffix + ".tmp")
    temporary.write_bytes(data)
    temporary.replace(OUTPUT)
    digest = sha256_bytes(data)
    sidecar_data = f"{digest}  {OUTPUT.name}\n".encode("ascii")
    temporary_sidecar = SIDECAR.with_suffix(SIDECAR.suffix + ".tmp")
    temporary_sidecar.write_bytes(sidecar_data)
    temporary_sidecar.replace(SIDECAR)


def check_outputs(data: bytes) -> int:
    if not OUTPUT.is_file() or not SIDECAR.is_file():
        print("publication source-state outputs are missing", file=sys.stderr)
        return 1
    if OUTPUT.read_bytes() != data:
        print("publication source-state manifest is stale", file=sys.stderr)
        return 1
    expected_sidecar = f"{sha256_bytes(data)}  {OUTPUT.name}\n".encode("ascii")
    if SIDECAR.read_bytes() != expected_sidecar:
        print("publication source-state sidecar is stale", file=sys.stderr)
        return 1
    print(
        f"publication source-state passed: repository={REPOSITORY_ID}; "
        f"files={len(build_manifest()['files'])}; sha256={sha256_bytes(data)}"
    )
    return 0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    arguments = parser.parse_args()
    data = serialized_manifest()
    if arguments.check:
        return check_outputs(data)
    write_outputs(data)
    print(
        f"wrote {OUTPUT.relative_to(ROOT).as_posix()}: "
        f"bytes={len(data)} sha256={sha256_bytes(data)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
