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
                state = "string"
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


def extract_method(source: str, name: str) -> str:
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


class MapBundleCacheLifetimeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.framework = FRAMEWORK_PATH.read_text(encoding="utf-8")

    def test_eviction_requires_the_real_additive_scene_safe_boundary(self) -> None:
        boundary = extract_method(
            self.framework,
            "IsSafeMapBundleEvictionBoundary",
        )
        self.assertIn("SceneManager.GetActiveScene()", boundary)
        self.assertIn('"Operation Room"', boundary)
        self.assertIn("activeOperation.SceneHandle != 0", boundary)
        self.assertIn("BundleReferencesLoadedScene(bundles)", boundary)
        self.assertIn(
            "BundleReferencesLoadedScene(pendingLaunch?.LoadingBundles)",
            boundary,
        )

    def test_loaded_scene_reference_is_a_hard_unload_guard(self) -> None:
        references = extract_method(self.framework, "BundleReferencesLoadedScene")
        trim = extract_method(
            self.framework,
            "TrimCompletedMapBundleCacheAtSafeBoundary",
        )
        unload = extract_method(self.framework, "UnloadMapBundles")
        stale = extract_method(
            self.framework,
            "TryDiscardStaleCompletedMapBundle",
        )
        prefetch = extract_method(self.framework, "BeginSelectedMapPrefetch")
        launch = extract_method(self.framework, "BeginCatalogOperationLaunch")

        self.assertIn("bundles.SceneBundle.GetAllScenePaths()", references)
        self.assertIn("SceneManager.sceneCount", references)
        self.assertIn("scene.IsValid()", references)
        self.assertIn("scene.isLoaded", references)
        self.assertLess(
            trim.index("if (BundleReferencesLoadedScene(entry.Value))"),
            trim.index("UnloadMapBundles(entry.Value)"),
        )
        self.assertIn("SceneBundle?.Unload(false)", unload)
        self.assertIn("dependency?.Unload(false)", unload)
        self.assertNotIn("Unload(true)", unload)
        stale_guard = stale.index(
            "if (activeRestartOwner || pendingPrefetchOwner ||"
        )
        stale_unload = stale.index("UnloadMapBundles(bundles)")
        self.assertLess(stale_guard, stale_unload)
        self.assertIn("BundleReferencesLoadedScene(bundles)", stale)
        for owner in (prefetch, launch):
            self.assertIn("TryDiscardStaleCompletedMapBundle(", owner)
            self.assertNotIn("loaded.SceneBundle?.Unload(false)", owner)

    def test_same_map_restart_reuses_the_completed_bundle(self) -> None:
        prefetch = extract_method(self.framework, "BeginSelectedMapPrefetch")
        unloaded = extract_method(self.framework, "OnSceneUnloaded")
        trim = extract_method(
            self.framework,
            "TrimCompletedMapBundleCacheAtSafeBoundary",
        )

        cache_hit = prefetch.index("loadedMapBundles.TryGetValue(map.Id")
        cache_return = prefetch.index("return;", cache_hit)
        stale_discard = prefetch.index("TryDiscardStaleCompletedMapBundle(")
        self.assertLess(cache_hit, cache_return)
        self.assertLess(cache_return, stale_discard)
        self.assertIn("retainedMapIds.Contains(entry.Key)", trim)
        self.assertNotIn("TrimCompletedMapBundleCacheAtSafeBoundary", unloaded)
        self.assertNotIn("UnloadMapBundles", unloaded)
        self.assertIn("Restart Operation route", unloaded)

    def test_different_map_evicts_only_after_fresh_owner_transfer(self) -> None:
        launch = extract_method(self.framework, "InvokeNativeCatalogLaunch")
        active_transfer = launch.index("activeOperation = new ActiveMapOperation")
        trim = launch.index("TrimCompletedMapBundleCacheAtSafeBoundary(")
        native_start = launch.index("InvokeNativeBoardStart(")

        self.assertLess(active_transfer, trim)
        self.assertLess(trim, native_start)
        self.assertIn('"fresh operation ownership transfer"', launch)

    def test_inflight_prefetch_and_active_restart_owner_are_both_preserved(self) -> None:
        trim = extract_method(
            self.framework,
            "TrimCompletedMapBundleCacheAtSafeBoundary",
        )
        pending = extract_method(self.framework, "ProcessPendingLaunch")

        self.assertIn("retainedMapIds.Add(activeOperation.Map.Id)", trim)
        self.assertIn("retainedMapIds.Add(pendingLaunch.Map.Id)", trim)
        registration = pending.index(
            "loadedMapBundles[pending.Map.Id] = pending.LoadingBundles"
        )
        pending_clear = pending.index("pendingLaunch = null", registration)
        trim_after_registration = pending.index(
            "TrimCompletedMapBundleCacheAtSafeBoundary(",
            pending_clear,
        )
        self.assertLess(registration, pending_clear)
        self.assertLess(pending_clear, trim_after_registration)

    def test_bundle_cache_is_map_neutral_and_variant_selection_is_unchanged(self) -> None:
        lowered = self.framework.lower()
        for forbidden in (
            "killhouse",
            "kill house",
            "kill-house",
            "kh01",
            "operator.kill",
        ):
            self.assertNotIn(forbidden, lowered)

        trim = extract_method(
            self.framework,
            "TrimCompletedMapBundleCacheAtSafeBoundary",
        )
        self.assertNotIn("SceneVariants", trim)
        self.assertNotIn("SceneSelection", trim)
        self.assertIn("loadedMapBundles.ToArray()", trim)
        self.assertIn("loadedMapBundles.Remove(entry.Key)", trim)


if __name__ == "__main__":
    unittest.main()
