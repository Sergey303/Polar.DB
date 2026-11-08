#!/usr/bin/env python3
"""Inspect or resume one interrupted historical independent benchmark series."""
import argparse
import fcntl
import json
import os
from pathlib import Path
import subprocess
import sys
import time

SCHEDULE = ("sqlite-first", "polar-first", "polar-first", "sqlite-first")
HISTORICAL_SHA = "abbb03cdc34cff1c11e7550494f65b03dad9afd1"
PROJECTS = {"PkIntLookup", "ExternalIntLookup", "BuildExternalIndexesOnly", "ReopenOnly"}


def git(root, *args):
    return subprocess.check_output(["git", "-C", str(root), *args], text=True).strip()


def validate_run(root, plan, idx, run, raw_path):
    name = f"{plan['seriesId']}-r{idx + 1:02d}"
    order = SCHEDULE[idx % 4]
    if run["runId"] != name or run["order"] != order:
        raise ValueError(f"Unexpected run id/order at pair {idx + 1}")
    if not raw_path.is_file() or not raw_path.resolve().is_relative_to(root):
        raise ValueError(f"Missing or unsafe raw result: {raw_path}")
    raw = json.loads(raw_path.read_text(encoding="utf-8"))
    manifest = raw["manifest"]
    expected = ["sqlite", "polar-db"] if order == "sqlite-first" else ["polar-db", "sqlite"]
    if manifest["runId"] != name or manifest["engineOrder"] != expected:
        raise ValueError("Run metadata/order mismatch: " + name)
    env = manifest["coordinator"]
    if (env["commitSha"] != HISTORICAL_SHA or not env["publicationReady"]
            or env["gitDirty"] or not env["gitStatusKnown"]):
        raise ValueError("Unclean coordinator: " + name)
    if len(manifest["engineProcesses"]) != 2 or any(
            w["commitSha"] != HISTORICAL_SHA or not w["publicationReady"]
            for w in manifest["engineProcesses"]):
        raise ValueError("Unexpected worker metadata: " + name)
    if raw["options"]["rowCounts"] != [plan["rows"]]:
        raise ValueError("Dataset row count mismatch: " + name)
    return raw


def seal(path, plan):
    temporary = path.with_name(path.name + ".writing")
    temporary.write_text(json.dumps(plan, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    os.replace(temporary, path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--series", type=Path, required=True)
    parser.add_argument("--execute", action="store_true", help="Run missing pairs, otherwise inspect only")
    parser.add_argument("--warmup", type=int, default=None)
    parser.add_argument("--samples", type=int, default=None)
    parser.add_argument("--timeout-seconds", type=int, default=900)
    args = parser.parse_args()
    path = args.series.resolve(strict=True)
    root = path.parents[3]
    if path.parent != root / "benchmarks/results/independent":
        parser.error("Series must reside in benchmarks/results/independent")
    plan = json.loads(path.read_text(encoding="utf-8"))
    if (plan.get("schema") != 1 or plan["seriesId"] != path.stem
            or plan["commit"] != HISTORICAL_SHA or plan["project"] not in PROJECTS
            or plan["rows"] not in (50000, 5000000) or len(plan["runs"]) > 8):
        parser.error("Unexpected series schema, source, experiment, size or count")
    if git(root, "rev-parse", "HEAD") != HISTORICAL_SHA:
        parser.error("Historical checkout SHA mismatch")
    if git(root, "status", "--porcelain", "--untracked-files=no"):
        parser.error("Historical tracked files changed")
    project = plan["project"]
    if project.endswith("Lookup") and (args.warmup is not None or args.samples is not None):
        parser.error("Lookup plans use fixed internal sampling")
    if not project.endswith("Lookup") and (args.warmup, args.samples) != (1, 3):
        parser.error("Lifecycle follow-ups require --warmup 1 --samples 3")
    dll = root / "benchmarks/src" / project / "bin/Release/net10.0" / (project + ".dll")
    if not dll.is_file():
        parser.error("Missing historical Release DLL: " + str(dll))
    if args.execute and subprocess.check_output(["dotnet", "--version"],
                                                cwd=root, text=True).strip() != "10.0.400":
        parser.error("Required historical .NET SDK 10.0.400 is not selected")
    # Lock only while executing; an interrupted script releases this OS lock automatically.
    lock_file = path.with_suffix(".resume.lock")
    lock = lock_file.open("a+") if args.execute else None
    if lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    if args.execute:
        for process in Path("/proc").iterdir():
            if not process.name.isdecimal() or int(process.name) == os.getpid():
                continue
            try:
                argv = (process / "cmdline").read_bytes().split(b"\x00")
            except (OSError, PermissionError):
                continue
            if any(Path(a.decode(errors="replace")).name == "run-independent-series.py" for a in argv):
                raise RuntimeError("Original coordinator still active; do not run concurrently")
    sys.path.insert(0, str(root / "benchmarks/scripts"))
    from disk_guard import run_guarded
    for idx in range(8):
        name = f"{plan['seriesId']}-r{idx + 1:02d}"
        order = SCHEDULE[idx % 4]
        found = list((root / "benchmarks/results/raw").glob(f"*/{name}/combined.raw.json"))
        if idx < len(plan["runs"]):
            raw_path = root / plan["runs"][idx]["raw"]
            validate_run(root, plan, idx, plan["runs"][idx], raw_path)
            continue
        if not args.execute:
            print(f"PENDING pair {idx + 1}/8: {project}, {plan['rows']} rows, order={order}")
            continue
        if len(found) > 1:
            raise RuntimeError("Ambiguous raw result for " + name)
        if found:
            raw_path = found[0]
            record = {"runId": name, "order": order, "raw": raw_path.relative_to(root).as_posix(),
                      "elapsedSeconds": None, "recoveredFromRaw": True}
            validate_run(root, plan, idx, record, raw_path)
            print("RECOVER validated completed raw result: " + name, flush=True)
        else:
            if (root / "benchmarks/work" / name).exists():
                raise RuntimeError("Incomplete work directory; inspect manually: " + name)
            environment = os.environ.copy()
            environment.update(POLAR_BENCH_RUN_ID=name, POLAR_BENCH_ENGINE_ORDER=order)
            command = ["dotnet", str(dll), f"--rows={plan['rows']}"]
            if args.warmup is not None:
                command.extend((f"--warmup={args.warmup}", f"--samples={args.samples}"))
            print(f"RUN pair {idx + 1}/8: {name}", flush=True)
            started = time.monotonic()
            run_guarded(command, root=root, env=environment, timeout_seconds=args.timeout_seconds,
                        min_free_gib=16.0, max_growth_gib=8.0)
            found = list((root / "benchmarks/results/raw").glob(f"*/{name}/combined.raw.json"))
            if len(found) != 1:
                raise RuntimeError("Expected exactly one completed raw result: " + name)
            raw_path = found[0]
            record = {"runId": name, "order": order, "raw": raw_path.relative_to(root).as_posix(),
                      "elapsedSeconds": round(time.monotonic() - started, 3)}
            validate_run(root, plan, idx, record, raw_path)
        plan["runs"].append(record)
        seal(path, plan)
        print(f"SAVED {len(plan['runs'])}/8 pairs", flush=True)
    print(f"VERIFIED {len(plan['runs'])}/8 pairs in {path}")


if __name__ == "__main__":
    main()
