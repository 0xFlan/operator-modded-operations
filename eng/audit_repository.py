from __future__ import annotations

import json
import re
import sys
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
            "src/OperatorModdedOperations/NativeBundleAssetLoader.cs",
            "src/OperatorModdedOperations/OperatorModdedOperations.csproj",
            "decompiled/README.md",
            "decompiled/release-0.3.19/CerberusNativeTabFix.cs",
            "packaging/README-PACKAGE-PLACEHOLDER.md",
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
        decompiled = (ROOT / "decompiled/release-0.3.19/CerberusNativeTabFix.cs").read_text(
            encoding="utf-8", errors="replace"
        )
        decompiled_readme = (ROOT / "decompiled/README.md").read_text(
            encoding="utf-8", errors="replace"
        )
        package_placeholder = (ROOT / "packaging/README-PACKAGE-PLACEHOLDER.md").read_text(
            encoding="utf-8", errors="replace"
        )
        required_authored = (
            '[BepInPlugin("operator.modded-operations", "OPERATOR: Modded Operations", "0.3.19")]',
            "operation.Operation.PveAiProfile",
            "details.WanderDistance = profile?.WanderDistanceMeters ?? 18;",
            "sun.colorTemperature = night ? 9754f : 5500f;",
            "sun.intensity = night ? 40f : 30000f;",
            "sun.bounceIntensity = night ? 1f : 5f;",
            "bloom.intensity.Override(night ? 0.3f : 0.03f);",
            "lensFlare.intensity.Override(night ? 1f : 0.5f);",
            'night ? "PVP-map night" : "PVP Woods Warehouse day"',
        )
        required_decompiled = (
            '[BepInPlugin("operator.modded-operations", "OPERATOR: Modded Operations", "0.3.19")]',
            "operation.Operation.PveAiProfile",
            "details.WanderDistance = ((profile != null) ? profile.WanderDistanceMeters : 18);",
            "light.colorTemperature = (flag ? 9754f : 5500f);",
            "light.intensity = (flag ? 40f : 30000f);",
            'flag ? "PVP-map night" : "PVP Woods Warehouse day"',
        )
        for fragment in required_authored:
            if fragment not in authored:
                errors.append(f"current authored render contract is missing: {fragment}")
        for fragment in required_decompiled:
            if fragment not in decompiled:
                errors.append(f"current decompiled release contract is missing: {fragment}")
        for label, text in (("authored", authored), ("decompiled", decompiled)):
            for rejected in ("52241.375f", "6727f", "0.359f"):
                if rejected in text:
                    errors.append(f"rejected daylight value remains in current {label} source: {rejected}")

        required_decompilation_identity = (
            "[MOD DLL] OperatorModdedOperations.dll",
            "version: 0.3.19",
            "bytes: 152576",
            "SHA-256: E98A6989717BAE78159159504AAE1A3571041935D72947A6EDBDE1641A99CC7A",
            "decompiler: ILSpy command-line tool 10.1.1.8388",
            "output: decompiled/release-0.3.19",
        )
        for fragment in required_decompilation_identity:
            if fragment not in decompiled_readme:
                errors.append(f"decompiled release identity is missing: {fragment}")

        required_framework_placeholders = (
            "[FRAMEWORK DLL] OperatorModdedOperations.dll",
            "[SHIPPED CERBERUS LAPTOP ROOT]",
            "[SHIPPED CERBERUS OPERATIONS TAB]",
            "[SHIPPED OPERATION ROW]",
            "[SHIPPED CERBERUS OPERATION BOARD]",
            "[SHIPPED INFILTRATION SELECTOR]",
            "[SHIPPED FAILURE AND RESTART UI]",
        )
        for fragment in required_framework_placeholders:
            if fragment not in package_placeholder:
                errors.append(f"framework placeholder contract is missing: {fragment}")

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
