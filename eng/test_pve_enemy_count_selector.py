from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = REPOSITORY_ROOT / "src" / "OperatorModdedOperations"


class PveEnemyCountSelectorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.framework = (SOURCE_ROOT / "CerberusNativeTabFix.cs").read_text(
            encoding="utf-8"
        )
        cls.selection = (SOURCE_ROOT / "PveEnemyCountSelection.cs").read_text(
            encoding="utf-8"
        )

    def test_absolute_cap_and_package_bounds_are_one_contract(self) -> None:
        self.assertIn("internal const int AbsoluteMaximum = 100;", self.selection)
        self.assertIn("packageMaximum > AbsoluteMaximum", self.selection)
        self.assertIn("Math.Min(packageMaximum, safeMarkerCapacity)", self.selection)

    def test_native_briefing_control_is_pve_only(self) -> None:
        self.assertIn("SetPrivateEnemyCountHierarchyActive(", self.framework)
        hierarchy = self.framework[
            self.framework.index("private static bool SetPrivateEnemyCountHierarchyActive") :
            self.framework.index("private static bool ReplaceNativeButtonAction")
        ]
        self.assertIn("Transform boardRoot = board.transform;", hierarchy)
        self.assertIn("cursor.gameObject.SetActive(true);", hierarchy)
        self.assertIn("if (cursor == boardRoot)", hierarchy)
        self.assertIn("manager.gameObject.SetActive(false);", hierarchy)
        self.assertNotIn("SetGameObjectsActive(board.SimulationParameters, true)", hierarchy)
        self.assertIn("ConfigureNativeEnemyCountSlider(", self.framework)
        self.assertIn("manager.mainSlider.wholeNumbers = true;", self.framework)
        self.assertIn("manager.mainSlider.minValue = minimum;", self.framework)
        self.assertIn("manager.mainSlider.maxValue = maximum;", self.framework)

    def test_selector_never_mutates_vanilla_operation_or_tier_state(self) -> None:
        private_board = self.framework[
            self.framework.index("private GameObject CreateCatalogOperationBoardShell") :
            self.framework.index("private static int GetDefaultPveEnemyCount")
        ]
        self.assertIn('preparationPanel.name = "MODDED_NATIVE_OPERATION_PREPARATION";', private_board)
        self.assertIn('privateTarget.name = "MODDED_OPERATIONS_PRIVATE_TARGET_PACKAGE";', private_board)
        self.assertIn('nativeBoardData.name = "MODDED_OPERATIONS_PRIVATE_OPBOARD_DATA";', private_board)
        self.assertIn("presentation.Board.EnemyCountSlider", private_board)
        self.assertNotIn("ActiveOperations =", self.framework)
        self.assertNotIn("ActiveOperations.Add", self.framework)
        self.assertNotIn("SimulationOperations =", self.framework)
        self.assertNotIn("SimulationOperations.Add", self.framework)
        self.assertNotIn("UnlockTier", self.framework)
        self.assertNotIn("Tier1", self.framework)
        self.assertNotIn("HarmonyPatch", self.framework)

    def test_confirm_captures_pending_and_active_count(self) -> None:
        self.assertIn(
            "out capturedPveEnemyCount,",
            self.framework,
        )
        self.assertIn("TryValidateNativeEnemyCountControl(", self.framework)
        self.assertIn("pendingLaunch.PveEnemyCount = capturedPveEnemyCount;", self.framework)
        self.assertIn("PveEnemyCount = capturedPveEnemyCount,", self.framework)
        self.assertIn("RequestedPveEnemyCount = pveEnemyCount,", self.framework)
        self.assertIn("return operation.RequestedPveEnemyCount;", self.framework)

    def test_loaded_scene_capacity_accepts_inactive_navigation_markers(self) -> None:
        self.assertIn("FindSafeStandalonePveEnemyMarkers(", self.framework)
        self.assertNotIn("!marker.gameObject.activeInHierarchy", self.framework)
        self.assertIn("if (marker.gameObject.activeInHierarchy)", self.framework)
        self.assertIn("activeMarkerCount++;", self.framework)
        self.assertIn("astar.IsPointOnNavmesh(marker.position)", self.framework)
        self.assertIn("safeMarkerCapacity", self.selection)
        self.assertIn("int targetCount = requestedCount;", self.framework)
        self.assertNotIn("Math.Min(requestedCount, markers.Count)", self.framework)

    def test_native_spawn_candidates_are_deterministically_pairwise_spaced(self) -> None:
        self.assertIn(
            "StandalonePveMinimumSpawnSeparationMeters = 2f;",
            self.framework,
        )
        self.assertIn("foreach (Transform accepted in safeMarkers)", self.framework)
        self.assertIn(
            "deltaX * deltaX + deltaZ * deltaZ < minimumSquared",
            self.framework,
        )
        self.assertIn("if (!separated)", self.framework)
        self.assertLess(
            self.framework.index("if (!separated)"),
            self.framework.index("safeMarkers.Add(marker);", self.framework.index("if (!separated)")),
        )
        self.assertIn(
            '"|minimumPlanarSeparationMillimeters="',
            self.framework,
        )


if __name__ == "__main__":
    unittest.main()
