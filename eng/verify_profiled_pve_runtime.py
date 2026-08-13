from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import asdict, dataclass, field
from pathlib import Path


NUMBER = r"-?\d+(?:\.\d+)?"
CONTRACT_PATTERN = re.compile(
    rf"Profiled PVE native AI contract: "
    rf"operation=(?P<operation>[^,]+), "
    rf"profile=(?P<profile>[^,]+), "
    rf"source=(?P<source>[^,]+), "
    rf"brains=(?P<brains>\d+), "
    rf"nativeInitialWanderDelay=(?P<delay_min>{NUMBER})\.\."
    rf"(?P<delay_max>{NUMBER})s, "
    rf"detection=(?P<detection_min>{NUMBER})\.\."
    rf"(?P<detection_max>{NUMBER})m, "
    rf"fov=(?P<fov_min>{NUMBER})\.\.(?P<fov_max>{NUMBER}), "
    rf"wander=(?P<wander_min>-?\d+)\.\.(?P<wander_max>-?\d+)m, "
    rf"idleWander=(?P<idle_wander>\d+)/(?P<idle_total>\d+), "
    rf"comms=(?P<comms_enabled>\d+)/(?P<comms_total>\d+)\."
)
SNAPSHOT_PATTERN = re.compile(
    rf"Profiled PVE AI snapshot: "
    rf"operation=(?P<operation>[^,]+), "
    rf"profile=(?P<profile>[^,]+), "
    rf"scheduled=(?P<scheduled>{NUMBER})s, "
    rf"elapsed=(?P<elapsed>{NUMBER})s, "
    rf"live=(?P<live>\d+), "
    rf"moved>=1m=(?P<moved_one_meter>\d+), "
    rf"movedTowardInsertion>=5m=(?P<moved_toward_insertion>\d+), "
    rf"movementMean=(?P<movement_mean>{NUMBER})m, "
    rf"movementMax=(?P<movement_max>{NUMBER})m, "
    rf"actualSeenTarget=(?P<actual_seen_target>\d+), "
    rf"sameMaskSightProbe\(vegetation=(?P<vegetation>\d+),"
    rf"other=(?P<other>\d+),"
    rf"clearOrPlayer=(?P<clear_or_player>\d+)\), "
    rf"states=(?P<states>.+)\."
)
COMPLETION_PATTERN = re.compile(
    r"Profiled PVE AI diagnostic completed its bounded 120-second "
    r"read-only acceptance window for operation=(?P<operation>[^\r\n]+)\."
)
REQUIRED_SCHEDULE = (0, 10, 30, 60, 90, 120)


@dataclass(frozen=True)
class Contract:
    line: int
    operation: str
    profile: str
    source: str
    brains: int
    delay_min: float
    delay_max: float
    detection_min: float
    detection_max: float
    fov_min: float
    fov_max: float
    wander_min: int
    wander_max: int
    idle_wander: int
    idle_total: int
    comms_enabled: int
    comms_total: int


@dataclass(frozen=True)
class Snapshot:
    line: int
    operation: str
    profile: str
    scheduled: int
    elapsed: float
    live: int
    moved_one_meter: int
    moved_toward_insertion: int
    movement_mean: float
    movement_max: float
    actual_seen_target: int
    vegetation: int
    other: int
    clear_or_player: int
    states: str


@dataclass
class Run:
    contract: Contract
    snapshots: list[Snapshot] = field(default_factory=list)
    completion_line: int | None = None


def parse_contract(match: re.Match[str], line_number: int) -> Contract:
    values = match.groupdict()
    return Contract(
        line=line_number,
        operation=values["operation"],
        profile=values["profile"],
        source=values["source"],
        brains=int(values["brains"]),
        delay_min=float(values["delay_min"]),
        delay_max=float(values["delay_max"]),
        detection_min=float(values["detection_min"]),
        detection_max=float(values["detection_max"]),
        fov_min=float(values["fov_min"]),
        fov_max=float(values["fov_max"]),
        wander_min=int(values["wander_min"]),
        wander_max=int(values["wander_max"]),
        idle_wander=int(values["idle_wander"]),
        idle_total=int(values["idle_total"]),
        comms_enabled=int(values["comms_enabled"]),
        comms_total=int(values["comms_total"]),
    )


def parse_snapshot(match: re.Match[str], line_number: int) -> Snapshot:
    values = match.groupdict()
    scheduled_float = float(values["scheduled"])
    if not scheduled_float.is_integer():
        raise ValueError(
            f"line {line_number}: snapshot schedule is not an integer: "
            f"{scheduled_float}"
        )
    return Snapshot(
        line=line_number,
        operation=values["operation"],
        profile=values["profile"],
        scheduled=int(scheduled_float),
        elapsed=float(values["elapsed"]),
        live=int(values["live"]),
        moved_one_meter=int(values["moved_one_meter"]),
        moved_toward_insertion=int(values["moved_toward_insertion"]),
        movement_mean=float(values["movement_mean"]),
        movement_max=float(values["movement_max"]),
        actual_seen_target=int(values["actual_seen_target"]),
        vegetation=int(values["vegetation"]),
        other=int(values["other"]),
        clear_or_player=int(values["clear_or_player"]),
        states=values["states"],
    )


def parse_runs(log_path: Path) -> tuple[list[Run], list[str]]:
    runs: list[Run] = []
    parse_errors: list[str] = []
    current: Run | None = None
    with log_path.open("r", encoding="utf-8", errors="replace") as stream:
        for line_number, line in enumerate(stream, start=1):
            contract_match = CONTRACT_PATTERN.search(line)
            if contract_match:
                current = Run(parse_contract(contract_match, line_number))
                runs.append(current)
                continue

            snapshot_match = SNAPSHOT_PATTERN.search(line)
            if snapshot_match:
                if current is None:
                    parse_errors.append(
                        f"line {line_number}: snapshot has no preceding contract"
                    )
                    continue
                try:
                    current.snapshots.append(
                        parse_snapshot(snapshot_match, line_number)
                    )
                except ValueError as error:
                    parse_errors.append(str(error))
                continue

            completion_match = COMPLETION_PATTERN.search(line)
            if completion_match:
                if current is None:
                    parse_errors.append(
                        f"line {line_number}: completion has no preceding contract"
                    )
                    continue
                operation = completion_match.group("operation")
                if operation != current.contract.operation:
                    parse_errors.append(
                        f"line {line_number}: completion operation {operation!r} "
                        f"does not match current operation "
                        f"{current.contract.operation!r}"
                    )
                current.completion_line = line_number
    return runs, parse_errors


def approximately_equal(left: float, right: float, tolerance: float) -> bool:
    return abs(left - right) <= tolerance


def validate_run(run: Run, index: int, args: argparse.Namespace) -> list[str]:
    errors: list[str] = []
    label = f"run {index} (contract line {run.contract.line})"
    contract = run.contract

    if args.expected_profile and contract.profile != args.expected_profile:
        errors.append(
            f"{label}: profile is {contract.profile!r}; expected "
            f"{args.expected_profile!r}"
        )
    if not args.brain_min <= contract.brains <= args.brain_max:
        errors.append(
            f"{label}: brains={contract.brains}; expected "
            f"{args.brain_min}..{args.brain_max}"
        )
    if args.require_positive_delay and contract.delay_min <= 0:
        errors.append(
            f"{label}: native initial wander delay minimum is "
            f"{contract.delay_min}; expected a positive value"
        )
    for field_name, actual in (
        ("detection minimum", contract.detection_min),
        ("detection maximum", contract.detection_max),
    ):
        if not approximately_equal(actual, args.detection, args.tolerance):
            errors.append(
                f"{label}: {field_name}={actual}; expected {args.detection}"
            )
    for field_name, actual in (
        ("FOV minimum", contract.fov_min),
        ("FOV maximum", contract.fov_max),
    ):
        if not approximately_equal(actual, args.fov, args.tolerance):
            errors.append(f"{label}: {field_name}={actual}; expected {args.fov}")
    if contract.wander_min != args.wander or contract.wander_max != args.wander:
        errors.append(
            f"{label}: wander={contract.wander_min}..{contract.wander_max}; "
            f"expected {args.wander}..{args.wander}"
        )
    if contract.idle_wander != contract.brains or contract.idle_total != contract.brains:
        errors.append(
            f"{label}: idleWander={contract.idle_wander}/{contract.idle_total}; "
            f"expected {contract.brains}/{contract.brains}"
        )
    if (
        contract.comms_enabled != contract.brains
        or contract.comms_total != contract.brains
    ):
        errors.append(
            f"{label}: comms={contract.comms_enabled}/{contract.comms_total}; "
            f"expected {contract.brains}/{contract.brains}"
        )
    if run.completion_line is None:
        errors.append(f"{label}: bounded 120-second completion line is missing")

    schedule_counts = {
        scheduled: sum(
            1 for snapshot in run.snapshots if snapshot.scheduled == scheduled
        )
        for scheduled in REQUIRED_SCHEDULE
    }
    unexpected = sorted(
        snapshot.scheduled
        for snapshot in run.snapshots
        if snapshot.scheduled not in REQUIRED_SCHEDULE
    )
    for scheduled, count in schedule_counts.items():
        if count != 1:
            errors.append(
                f"{label}: scheduled={scheduled}s snapshot count is {count}; "
                "expected 1"
            )
    if unexpected:
        errors.append(f"{label}: unexpected schedules: {unexpected}")

    snapshots = {snapshot.scheduled: snapshot for snapshot in run.snapshots}
    for scheduled, snapshot in sorted(snapshots.items()):
        snapshot_label = f"{label}, snapshot {scheduled}s line {snapshot.line}"
        if snapshot.operation != contract.operation:
            errors.append(f"{snapshot_label}: operation does not match contract")
        if snapshot.profile != contract.profile:
            errors.append(f"{snapshot_label}: profile does not match contract")
        if snapshot.elapsed + args.elapsed_tolerance < scheduled:
            errors.append(
                f"{snapshot_label}: elapsed={snapshot.elapsed}s is earlier than "
                "its schedule"
            )
        if snapshot.live <= 0:
            errors.append(f"{snapshot_label}: no live tracked bots")
        sight_total = (
            snapshot.vegetation + snapshot.other + snapshot.clear_or_player
        )
        if sight_total != snapshot.live:
            errors.append(
                f"{snapshot_label}: sight probes={sight_total}; "
                f"live bots={snapshot.live}"
            )
        if snapshot.moved_one_meter > snapshot.live:
            errors.append(f"{snapshot_label}: moved count exceeds live count")
        if snapshot.moved_toward_insertion > snapshot.live:
            errors.append(
                f"{snapshot_label}: toward-insertion count exceeds live count"
            )

    initial = snapshots.get(0)
    if args.require_delayed_start and initial is not None:
        if initial.moved_one_meter != 0 or initial.movement_max >= 1:
            errors.append(
                f"{label}: initial snapshot already moved "
                f"{initial.moved_one_meter} bots; max={initial.movement_max}m"
            )
        if initial.actual_seen_target != 0:
            errors.append(
                f"{label}: initial snapshot has "
                f"actualSeenTarget={initial.actual_seen_target}; expected 0"
            )

    final = snapshots.get(120)
    if args.require_search_movement and final is not None:
        if final.moved_one_meter < 1:
            errors.append(f"{label}: no bot moved at least 1 m by 120 seconds")
        if final.movement_max < args.minimum_search_displacement:
            errors.append(
                f"{label}: maximum movement at 120 seconds is "
                f"{final.movement_max}m; expected at least "
                f"{args.minimum_search_displacement}m"
            )
        if (
            final.moved_toward_insertion
            < args.minimum_toward_insertion
        ):
            errors.append(
                f"{label}: only {final.moved_toward_insertion} bots moved at "
                "least 5 m toward insertion by 120 seconds; expected at least "
                f"{args.minimum_toward_insertion}"
            )

    if args.require_vegetation_block:
        vegetation_hits = sum(snapshot.vegetation for snapshot in run.snapshots)
        if vegetation_hits < 1:
            errors.append(f"{label}: no same-mask sight probe hit vegetation")

    return errors


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Verify bounded profiled-PVE AI runtime evidence in a BepInEx log."
        )
    )
    parser.add_argument("log", type=Path, help="Path to BepInEx LogOutput.log")
    parser.add_argument("--minimum-runs", type=int, default=1)
    parser.add_argument("--expected-profile")
    parser.add_argument("--brain-min", type=int, default=1)
    parser.add_argument("--brain-max", type=int, default=2**31 - 1)
    parser.add_argument("--detection", type=float, required=True)
    parser.add_argument("--fov", type=float, required=True)
    parser.add_argument("--wander", type=int, required=True)
    parser.add_argument("--tolerance", type=float, default=0.11)
    parser.add_argument("--elapsed-tolerance", type=float, default=0.25)
    parser.add_argument("--require-positive-delay", action="store_true")
    parser.add_argument("--require-delayed-start", action="store_true")
    parser.add_argument("--require-search-movement", action="store_true")
    parser.add_argument("--minimum-search-displacement", type=float, default=5.0)
    parser.add_argument("--minimum-toward-insertion", type=int, default=0)
    parser.add_argument("--require-vegetation-block", action="store_true")
    parser.add_argument("--json", action="store_true", dest="json_output")
    return parser


def main() -> int:
    args = build_parser().parse_args()
    if args.minimum_runs < 1:
        print("error: --minimum-runs must be positive", file=sys.stderr)
        return 2
    if args.brain_min < 0 or args.brain_max < args.brain_min:
        print("error: invalid brain range", file=sys.stderr)
        return 2
    if args.minimum_toward_insertion < 0:
        print(
            "error: --minimum-toward-insertion cannot be negative",
            file=sys.stderr,
        )
        return 2
    if not args.log.is_file():
        print(f"error: log file does not exist: {args.log}", file=sys.stderr)
        return 2

    runs, parse_errors = parse_runs(args.log)
    errors = list(parse_errors)
    if len(runs) < args.minimum_runs:
        errors.append(
            f"found {len(runs)} profiled-PVE runs; expected at least "
            f"{args.minimum_runs}"
        )
        selected_runs = runs
    else:
        selected_runs = runs[-args.minimum_runs :]

    for index, run in enumerate(selected_runs, start=1):
        errors.extend(validate_run(run, index, args))

    report = {
        "pass": not errors,
        "log": str(args.log),
        "runs_found": len(runs),
        "runs_checked": len(selected_runs),
        "required_schedule_seconds": list(REQUIRED_SCHEDULE),
        "errors": errors,
        "runs": [
            {
                "contract": asdict(run.contract),
                "completion_line": run.completion_line,
                "snapshots": [asdict(snapshot) for snapshot in run.snapshots],
            }
            for run in selected_runs
        ],
    }
    if args.json_output:
        print(json.dumps(report, indent=2, sort_keys=True))
    else:
        status = "PASS" if report["pass"] else "FAIL"
        print(
            f"{status}: runs_found={report['runs_found']} "
            f"runs_checked={report['runs_checked']}"
        )
        for index, run in enumerate(selected_runs, start=1):
            final = next(
                (snapshot for snapshot in run.snapshots if snapshot.scheduled == 120),
                None,
            )
            final_summary = (
                "final=missing"
                if final is None
                else (
                    f"final_live={final.live} moved={final.moved_one_meter} "
                    f"toward={final.moved_toward_insertion} "
                    f"max={final.movement_max:.2f}m"
                )
            )
            print(
                f"run={index} operation={run.contract.operation} "
                f"profile={run.contract.profile} brains={run.contract.brains} "
                f"delay={run.contract.delay_min:.2f}.."
                f"{run.contract.delay_max:.2f}s {final_summary}"
            )
        for error in errors:
            print(f"error: {error}")
    return 0 if report["pass"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
