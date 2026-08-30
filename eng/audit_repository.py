from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path
from urllib.parse import unquote


ROOT = Path(__file__).resolve().parents[1]
TEXT_SUFFIXES = {".cs", ".csproj", ".json", ".md", ".py", ".ps1", ".txt", ".yaml", ".yml"}
FORBIDDEN_BINARY_SUFFIXES = {".dll", ".pdb", ".zip"}
PRIVATE_PATTERNS = {
    "operating-system account path": re.compile(r"(?i)[A-Z]:[\\/]Users[\\/](?!<)"),
    "private author-workspace path": re.compile(r"(?i)D:[\\/]Operator_GroceryStore_Mod"),
    "private picture path": re.compile(r"(?i)OneDrive[\\/]Pictures"),
}
LINK_PATTERN = re.compile(r"(?<!!)\[[^\]]+\]\((?P<target>[^)]+)\)")


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def decompiled_tree_identity(root: Path) -> tuple[int, int, str]:
    files = sorted(
        (path for path in root.rglob("*") if path.is_file()),
        key=lambda path: path.relative_to(root).as_posix(),
    )
    total_bytes = 0
    records: list[str] = []
    for path in files:
        data = path.read_bytes()
        total_bytes += len(data)
        records.append(
            f"{path.relative_to(root).as_posix()}\0{len(data)}\0{sha256_bytes(data)}\n"
        )
    return len(files), total_bytes, sha256_bytes("".join(records).encode("utf-8"))


def check_historical_publication(errors: list[str]) -> None:
    manifest_path = ROOT / "publication/source-state-manifest.json"
    sidecar_path = ROOT / "publication/source-state-manifest.sha256"
    if not manifest_path.is_file() or not sidecar_path.is_file():
        return

    manifest_bytes = manifest_path.read_bytes()
    expected_sidecar = (
        f"{sha256_bytes(manifest_bytes)}  {manifest_path.name}\n".encode("ascii")
    )
    if sidecar_path.read_bytes() != expected_sidecar:
        errors.append("historical publication source-state sidecar is stale")
        return

    try:
        manifest = json.loads(manifest_bytes.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        errors.append(f"historical publication source-state manifest is invalid: {exc}")
        return

    expected_artifact = {
        "name": "OperatorModdedOperations.dll",
        "version": "0.3.28",
        "bytes": 223232,
        "sha256": "75BB479E863A94807ACCB78E6FA37271BE340B226E4EBDFB1BE48C0B8248852B",
        "decompiledOutput": "decompiled/release-0.3.28",
        "decompiledFiles": 7,
        "decompiledBytes": 427039,
        "decompiledTreeSha256": "59D672462C7AA4DA00268D3A2185EA79D2C43F078853F71E40EAE40FDEE361C8",
    }
    artifacts = manifest.get("artifacts")
    actual_artifact = artifacts[0] if isinstance(artifacts, list) and len(artifacts) == 1 else {}
    if manifest.get("repository") != "operator-modded-operations":
        errors.append("historical publication repository identity is invalid")
    if manifest.get("releaseVersion") != "0.3.28":
        errors.append("historical publication release identity is invalid")
    for key, expected in expected_artifact.items():
        if actual_artifact.get(key) != expected:
            errors.append(f"historical publication artifact identity is invalid: {key}")


def repository_files() -> list[Path]:
    return [
        path
        for path in ROOT.rglob("*")
        if path.is_file()
        and ".git" not in path.parts
        and "bin" not in path.parts
        and "obj" not in path.parts
    ]


def required_paths() -> tuple[str, ...]:
    common = ("README.md", "LICENSE", "CHANGELOG.md", "SECURITY.md")
    if ROOT.name == "operator-modded-operations":
        return common + (
            "src/OperatorModdedOperations/CerberusNativeTabFix.cs",
            "src/OperatorModdedOperations/CerberusNativeTabFix.PvpPeerAgreement.cs",
            "src/OperatorModdedOperations/CerberusNativeTabFix.PeerGameModeSpawn.cs",
            "src/OperatorModdedOperations/CerberusNativeTabFix.PeerRuntimeBarriers.cs",
            "src/OperatorModdedOperations/PveEnemyCountSelection.cs",
            "src/OperatorModdedOperations/FrameworkEvidence.cs",
            "src/OperatorModdedOperations/NativeBundleAssetLoader.cs",
            "src/OperatorModdedOperations/SceneVariantSelectionStore.cs",
            "src/OperatorModdedOperations/OperatorModdedOperations.csproj",
            "schemas/operator-map-package-v2.schema.json",
            "decompiled/README.md",
            "decompiled/release-0.3.29/CerberusNativeTabFix.cs",
            "decompiled/release-0.3.28/CerberusNativeTabFix.cs",
            "decompiled/release-0.3.28/SceneVariantSelectionStore.cs",
            "eng/generate_publication_source_state.py",
            "eng/verify_profiled_pve_runtime.py",
            "eng/test_verify_profiled_pve_runtime.py",
            "packaging/README-PACKAGE-PLACEHOLDER.md",
            "publication/README.md",
            "publication/source-state-manifest.json",
            "publication/source-state-manifest.sha256",
            "MODDED_OPERATIONS_TECHNICAL_BIBLE.md",
        )
    if ROOT.name == "operator-ukrainian-forest":
        return common + (
            "source/runtime/OperatorUkrainianForestPlugin.cs",
            "source/runtime/NativeAssetBundleLoader.cs",
            "source/runtime/OperatorUkrainianForest.csproj",
            "source/runtime_bundle_project/Assets/Editor/BuildHillyUkrainianForestBundle.cs",
            "source/runtime_bundle_project/Packages/manifest.json",
            "source/runtime_bundle_project/Packages/packages-lock.json",
            "source/runtime_bundle_project/ProjectSettings/ProjectVersion.txt",
            "tools/validate_ukrainian_forest_bundle.py",
            "tools/audit_pvp_map_volume_profile.py",
            "decompiled/README.md",
            "decompiled/release-0.4.16/OperatorUkrainianForest/OperatorUkrainianForestPlugin.cs",
            "packaging/README-PACKAGE-PLACEHOLDER.md",
            "UKRAINIAN_FOREST_TECHNICAL_BIBLE.md",
        )
    return common


def check_markdown_links(path: Path, text: str, errors: list[str]) -> None:
    for match in LINK_PATTERN.finditer(text):
        target = match.group("target").strip().strip("<>")
        if target.startswith(("http://", "https://", "mailto:", "#", "<")):
            continue
        relative = unquote(target.split("#", 1)[0])
        if not relative:
            continue
        destination = (path.parent / relative).resolve()
        if not destination.exists():
            errors.append(f"broken Markdown link: {path.relative_to(ROOT)} -> {target}")


def main() -> int:
    errors: list[str] = []
    files = repository_files()

    for relative in required_paths():
        if not (ROOT / relative).is_file():
            errors.append(f"missing required source/publication file: {relative}")

    if ROOT.name == "operator-modded-operations":
        authored = (ROOT / "src/OperatorModdedOperations/CerberusNativeTabFix.cs").read_text(
            encoding="utf-8", errors="replace"
        )
        authored_pvp = (
            ROOT / "src/OperatorModdedOperations/CerberusNativeTabFix.PvpPeerAgreement.cs"
        ).read_text(encoding="utf-8", errors="replace")
        decompiled = (ROOT / "decompiled/release-0.3.28/CerberusNativeTabFix.cs").read_text(
            encoding="utf-8", errors="replace"
        )
        checkpoint_decompiled = (
            ROOT / "decompiled/release-0.3.29/CerberusNativeTabFix.cs"
        ).read_text(encoding="utf-8", errors="replace")
        decompiled_readme = (ROOT / "decompiled/README.md").read_text(
            encoding="utf-8", errors="replace"
        )
        package_placeholder = (ROOT / "packaging/README-PACKAGE-PLACEHOLDER.md").read_text(
            encoding="utf-8", errors="replace"
        )
        authored_schema = (ROOT / "schemas/operator-map-package-v2.schema.json").read_text(
            encoding="utf-8", errors="replace"
        )
        required_authored = (
            '[BepInPlugin("operator.modded-operations", "OPERATOR: Modded Operations", "0.3.31")]',
            'internal const string RequiredApiVersion = "0.2.0-alpha.7";',
            "operation.Operation.PveAiProfile",
            "details.WanderDistance = profile?.WanderDistanceMeters ?? 18;",
            "CaptureServerSpawnedNetIds",
            "CaptureOwnedStandalonePveServerPopulation",
            "ProcessPendingStandalonePveTeamValidation",
            "DestroyOwnedStandalonePvePopulation",
            "operation.PveTeamValidationEarliestFrame = Time.frameCount + 1;",
            "Time.frameCount + StandalonePveTeamValidationDeadlineFrames;",
            "SuppressStandalonePveExtraction(operation);",
            "StartProfiledPveAiDiagnostics",
            "ProcessProfiledPveAiDiagnostics",
            "LogProfiledPveAiSnapshot",
            "ProfiledPveAiDiagnosticSnapshotSeconds",
            "ShowNativeLoadingScreenForPackageScene(",
            ".ShowLoadingScreen();",
            "GetProfiledPveNavigationPosition(",
            "TryApplyProfiledPveInitialWanderDelayCap(",
            "InitialWanderDelayMaxSeconds",
            "TryApplyProfiledPveMaximumReactionTimeCap(",
            "MaximumReactionTimeSeconds",
            "profile?.ReactionDisposition",
            "brain._baseReactionTime = cappedBase;",
            "brain.ReactionTime = cappedCurrent;",
            "HasDeclaredSceneVariants(",
            "map.SceneVariants.Count > 1",
            "sun.colorTemperature = night ? 9754f : (arcticOvercast ? 6500f : 5500f);",
            "sun.intensity = night ? 40f : (arcticOvercast ? 65000f : 30000f);",
            "sun.bounceIntensity = night ? 1f : (arcticOvercast ? 2f : 5f);",
            "bloom.intensity.Override(night ? 0.3f : 0.03f);",
            "lensFlare.intensity.Override(night ? 1f : 0.5f);",
            'night ? "PVP-map night" : "PVP Woods Warehouse day"',
            'StandalonePveExfilMarkerPrefix = "PVE_ExfilZone_"',
            "ConfigureStandalonePveController(",
            "CreateNativeAtakExfilMarker(",
            "ResetStandalonePveExtractionState()",
            "!network.SuccessfulOperation",
        )
        required_decompiled = (
            '[BepInPlugin("operator.modded-operations", "OPERATOR: Modded Operations", "0.3.28")]',
            'internal const string RequiredApiVersion = "0.2.0-alpha.5";',
            "operation.Operation.PveAiProfile",
            "details.WanderDistance = ((profile != null) ? profile.WanderDistanceMeters : 18);",
            "CaptureServerSpawnedNetIds",
            "CaptureOwnedStandalonePveServerPopulation",
            "ProcessPendingStandalonePveTeamValidation",
            "DestroyOwnedStandalonePvePopulation",
            "operation.PveTeamValidationEarliestFrame = Time.frameCount + 1;",
            "operation.PveTeamValidationDeadlineFrame = Time.frameCount + 60;",
            "SuppressStandalonePveExtraction(operation);",
            "StartProfiledPveAiDiagnostics",
            "ProcessProfiledPveAiDiagnostics",
            "LogProfiledPveAiSnapshot",
            "ProfiledPveAiDiagnosticSnapshotSeconds",
            "ShowNativeLoadingScreenForPackageScene(",
            ".ShowLoadingScreen();",
            "GetProfiledPveNavigationPosition(",
            "TryApplyProfiledPveInitialWanderDelayCap(",
            "InitialWanderDelayMaxSeconds",
            "TryApplyProfiledPveMaximumReactionTimeCap(",
            "MaximumReactionTimeSeconds",
            "ReactionDisposition",
            "_baseReactionTime",
            "ReactionTime",
            "HasDeclaredSceneVariants(",
            "map.SceneVariants.Count > 1",
            "colorTemperature = (flag2 ? 9754f : (flag ? 6500f : 5500f));",
            "intensity = (flag2 ? 40f : (flag ? 65000f : 30000f));",
            'flag2 ? "PVP-map night" : "PVP Woods Warehouse day"',
            'StandalonePveExfilMarkerPrefix = "PVE_ExfilZone_"',
            "ConfigureStandalonePveController(",
            "CreateNativeAtakExfilMarker(",
            "ResetStandalonePveExtractionState()",
            "!val.SuccessfulOperation",
        )
        for fragment in required_authored:
            if fragment not in authored:
                errors.append(f"current authored render contract is missing: {fragment}")
        required_authored_pvp = (
            "private const ushort PvpAgreementProtocolVersion = 6;",
            "suite-install-receipt-v1",
            "operator-loader-neutral-runtime-pair-v1",
            "package-runtime-ready-v1",
            "PveAgreementV6RuntimeContractComplete = true",
            "PvpAgreementV6RuntimeContractComplete = true",
            "SuiteManifestSha256",
            "CompanionSha256",
            "TryHashLoadedAssembly",
            "TryAdvancePvpPackageRuntimeReadiness",
            "FailActivePvpNativeLifecycle",
        )
        for fragment in required_authored_pvp:
            if fragment not in authored_pvp:
                errors.append(f"current authored PVP contract is missing: {fragment}")
        for fragment in required_decompiled:
            if fragment not in decompiled:
                errors.append(f"current decompiled release contract is missing: {fragment}")
        required_checkpoint_decompiled = (
            '[BepInPlugin("operator.modded-operations", "OPERATOR: Modded Operations", "0.3.29")]',
            "private const ushort PvpAgreementProtocolVersion = 2;",
            "scene-ready-epoch-v1",
            "runtime-binary-sha256-v1",
            "package-runtime-ready-v1",
        )
        for fragment in required_checkpoint_decompiled:
            if fragment not in checkpoint_decompiled:
                errors.append(f"0.3.29 decompiled checkpoint is missing: {fragment}")
        required_schema = (
            '"sceneVariants"',
            '"$ref": "#/$defs/sceneVariant"',
            '"sceneVariant"',
            '"reactionDisposition"',
            '"enum": ["defensive", "offensive", "random"]',
            '"maximumReactionTimeSeconds": { "type": "number", "minimum": 0.10, "maximum": 1.50 }',
            '"runtimeCompanion"',
            '"$ref": "#/$defs/runtimeCompanion"',
            '"readyMarkerName"',
            '"failureMarkerName"',
        )
        for fragment in required_schema:
            if fragment not in authored_schema:
                errors.append(f"current schema-v2 response profile is missing: {fragment}")
        for label, text in (("authored", authored), ("decompiled", decompiled)):
            for rejected in ("52241.375f", "6727f", "0.359f"):
                if rejected in text:
                    errors.append(f"rejected daylight value remains in current {label} source: {rejected}")

        required_decompilation_identity = (
            "[MOD DLL] OperatorModdedOperations.dll",
            "version: 0.3.28",
            "bytes: 223232",
            "SHA-256: 75BB479E863A94807ACCB78E6FA37271BE340B226E4EBDFB1BE48C0B8248852B",
            "decompiler: ILSpy command-line tool 10.1.1.8388",
            "decompiler executable bytes: 162816",
            "decompiler executable SHA-256: 1B14D01FFB011887C8277B309E615CACCE72678CD5E87F56C29860190116F0DC",
            "output: decompiled/release-0.3.28",
            "output files: 7",
            "output bytes: 427039",
            "output tree SHA-256: 59D672462C7AA4DA00268D3A2185EA79D2C43F078853F71E40EAE40FDEE361C8",
        )
        for fragment in required_decompilation_identity:
            if fragment not in decompiled_readme:
                errors.append(f"decompiled release identity is missing: {fragment}")
        required_checkpoint_identity = (
            "version: 0.3.29",
            "bytes: 279552",
            "SHA-256: 95CEF59F62B2DF40ED69C066692953210CDC17A9C3D08DB95753DA7A9B4142CD",
            "output: decompiled/release-0.3.29",
            "output files: 7",
            "output bytes: 543935",
            "output tree SHA-256: 059C0050B8EF727FAC73745B917185668ACA8C35FDC7C15DFA36C14AA7381714",
        )
        for fragment in required_checkpoint_identity:
            if fragment not in decompiled_readme:
                errors.append(f"decompiled checkpoint identity is missing: {fragment}")
        expected_checkpoint_tree = (
            7,
            543935,
            "059C0050B8EF727FAC73745B917185668ACA8C35FDC7C15DFA36C14AA7381714",
        )
        actual_checkpoint_tree = decompiled_tree_identity(
            ROOT / "decompiled/release-0.3.29"
        )
        if actual_checkpoint_tree != expected_checkpoint_tree:
            errors.append(
                "decompiled 0.3.29 checkpoint tree identity mismatch: "
                f"expected={expected_checkpoint_tree!r}; actual={actual_checkpoint_tree!r}"
            )

        required_framework_placeholders = (
            "[FRAMEWORK DLL] OperatorModdedOperations.dll",
            "[SHIPPED CERBERUS LAPTOP ROOT]",
            "[SHIPPED CERBERUS OPERATIONS TAB]",
            "[SHIPPED OPERATION ROW]",
            "[SHIPPED CERBERUS OPERATION BOARD]",
            "[SHIPPED INFILTRATION SELECTOR]",
            "[SHIPPED FAILURE, SUCCESS, AND RESTART UI]",
            "[SHIPPED EXFILZONE TEXTURE]",
            "[SHIPPED HDRP/UNLIT SHADER]",
            "0C27854DFDD3C9F0946F5BCBC61CE37DAE3037215BB5FC11C3400BD50190EB77",
            "A58E1FA50CE345931104B9980AFBAF356B8EEAC0E7A735BEF7BD21FC93727AD9",
        )
        for fragment in required_framework_placeholders:
            if fragment not in package_placeholder:
                errors.append(f"framework placeholder contract is missing: {fragment}")

        check_historical_publication(errors)

    if ROOT.name == "operator-ukrainian-forest":
        authored = (ROOT / "source/runtime/OperatorUkrainianForestPlugin.cs").read_text(
            encoding="utf-8", errors="replace"
        )
        decompiled = (
            ROOT
            / "decompiled/release-0.4.16/OperatorUkrainianForest/OperatorUkrainianForestPlugin.cs"
        ).read_text(encoding="utf-8", errors="replace")
        decompiled_readme = (ROOT / "decompiled/README.md").read_text(
            encoding="utf-8", errors="replace"
        )
        source_placeholder = (
            ROOT / "docs/reference/source-publication-and-asset-placeholders.md"
        ).read_text(encoding="utf-8", errors="replace")

        required_authored = (
            'public const string PluginGuid = "operator.ukrainianforest";',
            'public const string PluginVersion = "0.4.16";',
            "ConfigureForestVegetationVisionBlockers(mapRoot.transform)",
            'LayerMask.NameToLayer("AI_VisionBlock")',
        )
        required_decompiled = (
            '[BepInPlugin("operator.ukrainianforest", "Operator Ukrainian Forest", "0.4.16")]',
            'public const string PluginVersion = "0.4.16";',
            "ConfigureForestVegetationVisionBlockers(mapRoot.transform)",
            'LayerMask.NameToLayer("AI_VisionBlock")',
        )
        for fragment in required_authored:
            if fragment not in authored:
                errors.append(f"current authored Forest identity is missing: {fragment}")
        for fragment in required_decompiled:
            if fragment not in decompiled:
                errors.append(f"current decompiled Forest identity is missing: {fragment}")

        required_decompilation_identity = (
            "[MOD DLL] OperatorUkrainianForest.dll",
            "version: 0.4.16",
            "bytes: 297472",
            "SHA-256: 1B93F389137EEB003A37FF3DAB30B11DC21B159D0D4FD5A78E25BA6393407D7D",
            "decompiler: ILSpy command-line tool 10.1.1.8388",
            "output: decompiled/release-0.4.16",
        )
        for fragment in required_decompilation_identity:
            if fragment not in decompiled_readme:
                errors.append(f"decompiled Forest release identity is missing: {fragment}")

        required_asset_placeholders = (
            "[AUTHORIZED PREFAB ASSET]",
            "[AUTHORIZED TEXTURE SET]",
            "[DEPENDENCY ASSETBUNDLE]",
            "[SCENE ASSETBUNDLE]",
            "[PREVIEW IMAGE]",
            "[RGBAHALF 32x32x32 LUT BYTES]",
        )
        for fragment in required_asset_placeholders:
            if fragment not in source_placeholder:
                errors.append(f"Forest asset placeholder class is missing: {fragment}")

    for path in files:
        relative = path.relative_to(ROOT)
        if path.suffix.lower() in FORBIDDEN_BINARY_SUFFIXES:
            errors.append(f"binary release artifact is in Git source tree: {relative}")
        if path.stat().st_size >= 90 * 1024 * 1024:
            errors.append(f"file exceeds normal Git blob limit policy: {relative}")
        if path.suffix.lower() not in TEXT_SUFFIXES:
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        for label, pattern in PRIVATE_PATTERNS.items():
            if pattern.search(text):
                errors.append(f"{label}: {relative}")
        if path.suffix.lower() == ".md":
            check_markdown_links(path, text, errors)
        if path.suffix.lower() == ".json":
            try:
                json.loads(text)
            except json.JSONDecodeError as exc:
                errors.append(f"invalid JSON: {relative}:{exc.lineno}:{exc.colno}: {exc.msg}")

    if errors:
        print("repository audit failed:")
        for error in errors:
            print(f"- {error}")
        return 1

    print(
        f"repository audit passed: root={ROOT.name}; files={len(files)}; "
        f"required={len(required_paths())}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
