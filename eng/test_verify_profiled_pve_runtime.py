from __future__ import annotations

import argparse
import tempfile
import unittest
from pathlib import Path

import verify_profiled_pve_runtime as verifier


def make_run(operation: str, delay_min: float = 4.0) -> str:
    lines = [
        "[Info] Profiled PVE native AI contract: "
        f"operation={operation}, profile=dense-forest-balanced-v1, "
        "source=spawn, brains=10, "
        f"nativeInitialWanderDelay={delay_min:.2f}..12.00s, "
        "detection=45.0..45.0m, fov=90.0..90.0, wander=38..38m, "
        "comms=10/10."
    ]
    for scheduled in verifier.REQUIRED_SCHEDULE:
        moved = 0 if scheduled == 0 else 4
        toward = 0 if scheduled == 0 else 1
        movement_mean = 0.0 if scheduled == 0 else 6.0
        movement_max = 0.0 if scheduled == 0 else 12.0
        lines.append(
            "[Info] Profiled PVE AI snapshot: "
            f"operation={operation}, profile=dense-forest-balanced-v1, "
            f"scheduled={scheduled}s, elapsed={scheduled + 0.1:.2f}s, "
            f"live=10, moved>=1m={moved}, "
            f"movedTowardInsertion>=5m={toward}, "
            f"movementMean={movement_mean:.2f}m, "
            f"movementMax={movement_max:.2f}m, actualSeenTarget=0, "
            "sameMaskSightProbe(vegetation=5,other=2,clearOrPlayer=3), "
            "states=Wander=10."
        )
    lines.append(
        "[Info] Profiled PVE AI diagnostic completed its bounded "
        "120-second read-only acceptance window for "
        f"operation={operation}."
    )
    return "\n".join(lines)


def validation_arguments() -> argparse.Namespace:
    return argparse.Namespace(
        expected_profile="dense-forest-balanced-v1",
        brain_min=10,
        brain_max=15,
        require_positive_delay=True,
        detection=45.0,
        fov=90.0,
        wander=38,
        tolerance=0.11,
        elapsed_tolerance=0.25,
        require_delayed_start=True,
        require_search_movement=True,
        minimum_search_displacement=5.0,
        minimum_toward_insertion=1,
        require_vegetation_block=True,
    )


class ProfiledPveRuntimeVerifierTests(unittest.TestCase):
    def parse(self, text: str) -> tuple[list[verifier.Run], list[str]]:
        with tempfile.TemporaryDirectory() as temporary_directory:
            path = Path(temporary_directory) / "LogOutput.log"
            path.write_text(text, encoding="utf-8")
            return verifier.parse_runs(path)

    def test_accepts_two_complete_exact_runs(self) -> None:
        runs, parse_errors = self.parse(make_run("pve-a") + "\n" + make_run("pve-b"))
        self.assertEqual(parse_errors, [])
        self.assertEqual(len(runs), 2)
        errors: list[str] = []
        for index, run in enumerate(runs, start=1):
            errors.extend(verifier.validate_run(run, index, validation_arguments()))
        self.assertEqual(errors, [])

    def test_rejects_zero_native_delay(self) -> None:
        runs, parse_errors = self.parse(make_run("pve-a", delay_min=0.0))
        self.assertEqual(parse_errors, [])
        errors = verifier.validate_run(runs[0], 1, validation_arguments())
        self.assertTrue(any("positive value" in error for error in errors))

    def test_rejects_missing_snapshot_and_completion(self) -> None:
        text = make_run("pve-a")
        text = "\n".join(
            line
            for line in text.splitlines()
            if "scheduled=60s" not in line and "diagnostic completed" not in line
        )
        runs, parse_errors = self.parse(text)
        self.assertEqual(parse_errors, [])
        errors = verifier.validate_run(runs[0], 1, validation_arguments())
        self.assertTrue(any("scheduled=60s snapshot count" in error for error in errors))
        self.assertTrue(any("completion line is missing" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
