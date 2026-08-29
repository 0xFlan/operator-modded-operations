from __future__ import annotations

import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
FRAMEWORK_PATH = (
    REPOSITORY_ROOT
    / "src"
    / "OperatorModdedOperations"
    / "CerberusNativeTabFix.cs"
)
SELECTOR_PATH = (
    REPOSITORY_ROOT
    / "src"
    / "OperatorModdedOperations"
    / "SceneVariantSelectionStore.cs"
)


def extract_method(source: str, name: str) -> str:
    return extract_method_from_source(source, name)


def extract_method_from_source(source: str, name: str) -> str:
    declaration = re.search(
        rf"\bprivate\s+(?:static\s+)?(?:bool|void)\s+{re.escape(name)}\s*\(",
        source,
    )
    if declaration is None:
        raise AssertionError(f"Could not find private method {name}.")
    opening = source.find("{", declaration.end())
    if opening < 0:
        raise AssertionError(f"Could not find opening brace for {name}.")
    closing = find_matching_brace(source, opening)
    return source[declaration.start() : closing + 1]


def find_matching_brace(source: str, opening: int) -> int:
    depth = 0
    index = opening
    state = "code"
    while index < len(source):
        current = source[index]
        following = source[index + 1] if index + 1 < len(source) else ""

        if state == "line-comment":
            if current in "\r\n":
                state = "code"
        elif state == "block-comment":
            if current == "*" and following == "/":
                state = "code"
                index += 1
        elif state == "string":
            if current == "\\":
                index += 1
            elif current == '"':
                state = "code"
        elif state == "verbatim-string":
            if current == '"' and following == '"':
                index += 1
            elif current == '"':
                state = "code"
        elif state == "character":
            if current == "\\":
                index += 1
            elif current == "'":
                state = "code"
        else:
            if current == "/" and following == "/":
                state = "line-comment"
                index += 1
            elif current == "/" and following == "*":
                state = "block-comment"
                index += 1
            elif current == '"':
                prefix = source[max(opening, index - 2) : index]
                state = "verbatim-string" if "@" in prefix else "string"
            elif current == "'":
                state = "character"
            elif current == "{":
                depth += 1
            elif current == "}":
                depth -= 1
                if depth == 0:
                    return index
        index += 1

    raise AssertionError("C# method body has no matching closing brace.")


class SceneVariantFrameworkIntegrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.framework = FRAMEWORK_PATH.read_text(encoding="utf-8")
        cls.selector = SELECTOR_PATH.read_text(encoding="utf-8")

    def test_manifest_gate_is_exactly_more_than_one_declared_variant(self) -> None:
        method = extract_method(self.framework, "HasDeclaredSceneVariants")
        compact = re.sub(r"\s+", "", method)
        self.assertIn(
            "returnmap!=null&&map.SceneVariants!=null&&map.SceneVariants.Count>1;",
            compact,
        )

    def test_store_selection_is_reached_only_after_nonvariant_return(self) -> None:
        method = extract_method(self.framework, "TrySelectFreshLaunchScene")
        call = "sceneVariantSelectionStore.Select("
        self.assertEqual(self.framework.count(call), 1)
        self.assertEqual(method.count(call), 1)

        gate_position = method.index("if (!HasDeclaredSceneVariants(map))")
        call_position = method.index(call)
        bypass_region = method[gate_position:call_position]
        self.assertIn("return true;", bypass_region)
        self.assertLess(gate_position, call_position)

    def test_pending_launch_validates_bundle_before_committing_selection(self) -> None:
        method = extract_method(self.framework, "ProcessPendingLaunch")
        validation_position = method.index("ValidateLoadedSceneBundle(")
        selection_position = method.index("TrySelectFreshLaunchScene(")
        cache_position = method.index("loadedMapBundles[pending.Map.Id] =")
        native_launch_position = method.index("InvokeNativeCatalogLaunch(")

        self.assertLess(validation_position, selection_position)
        self.assertLess(selection_position, cache_position)
        self.assertLess(cache_position, native_launch_position)
        self.assertRegex(
            method,
            r"pending\.LaunchRequested\s*&&\s*"
            r"pending\.SceneSelection\s*==\s*null\s*&&\s*"
            r"!TrySelectFreshLaunchScene",
        )

    def test_fresh_launch_ownership_is_proved_before_variant_commit(self) -> None:
        launch = extract_method(self.framework, "BeginCatalogOperationLaunch")
        ownership_position = launch.index(
            "CanCommitFreshCatalogLaunchSelection()"
        )
        selection_position = launch.index("TrySelectFreshLaunchScene(")
        self.assertLess(ownership_position, selection_position)

        agreement = (
            REPOSITORY_ROOT
            / "src"
            / "OperatorModdedOperations"
            / "CerberusNativeTabFix.PvpPeerAgreement.cs"
        ).read_text(encoding="utf-8")
        guard = extract_method_from_source(
            agreement,
            "CanCommitFreshCatalogLaunchSelection",
        )
        predicate = extract_method_from_source(
            agreement,
            "CommittedPackageTransitionOwnsNativeTeardown",
        )
        self.assertIn("CommittedPackageTransitionOwnsNativeTeardown()", guard)
        self.assertIn("activeOperation.NativeTransitionStarted", predicate)
        self.assertIn("activeOperation.SceneHandle != 0", predicate)
        self.assertIn("hostPvpAgreement?.NativeTransitionCommitted", predicate)
        self.assertIn("remotePvpAgreement?.NativeTransitionCommittedEpoch", predicate)

    def test_scene_callbacks_never_select_and_match_the_active_selection(self) -> None:
        loaded = extract_method(self.framework, "OnSceneLoaded")
        unloaded = extract_method(self.framework, "OnSceneUnloaded")
        matcher = extract_method(self.framework, "SceneMatchesOperation")

        for callback in (loaded, unloaded):
            self.assertNotIn("TrySelectFreshLaunchScene", callback)
            self.assertNotIn("sceneVariantSelectionStore.Select", callback)

        self.assertIn("SceneMatchesOperation(scene, operation)", loaded)
        self.assertIn("scene.handle != operation.SceneHandle", unloaded)
        self.assertIn("operation.SceneSelection", matcher)
        self.assertIn("SelectionBelongsToMap(operation.Map, operation.SceneSelection)", matcher)
        self.assertIn("operation.SceneSelection.ScenePath", matcher)

    def test_shared_variant_bundle_uses_exact_inventory_gate(self) -> None:
        validator = extract_method(self.framework, "ValidateLoadedSceneBundle")
        self.assertIn(
            "mapsSharingBundle.Any(HasDeclaredSceneVariants)",
            validator,
        )
        self.assertIn("bundledScenes.SetEquals(declaredScenes)", validator)

    def test_framework_has_no_kill_house_identity_or_unity_rng(self) -> None:
        combined = self.framework + "\n" + self.selector
        lowered = combined.lower()
        for forbidden in ("killhouse", "kill house", "kill-house", "kh01", "operator.kill"):
            self.assertNotIn(forbidden, lowered)

        self.assertNotRegex(combined, r"\bUnityEngine\s*\.\s*Random\s*\.")
        self.assertNotRegex(combined, r"\bRandom\s*\.\s*(?:Range|value)\b")
        self.assertIn("RandomNumberGenerator.GetInt32", self.selector)


if __name__ == "__main__":
    unittest.main()
