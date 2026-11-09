#!/usr/bin/env python3
"""Cross-platform 2021/2026 index benchmark: pure stdlib orchestration."""
from __future__ import annotations
import argparse
import csv
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import platform
import shutil
import sys
import tempfile
import uuid
from index_evolution_support import (
    OLD, NEW, FILES, COLUMNS, append_csv, checked, disk_guard, file_sha256,
    logged, parse_log, require_tools, write_snapshot,
)

ROOT = Path(__file__).resolve().parents[2]


def options() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=("smoke", "full"), default="smoke")
    parser.add_argument("--output", type=Path, help="New output folder; never overwritten")
    parser.add_argument("--rows", type=int, default=10_000, help="Smoke only; 2..1000000")
    parser.add_argument("--probes", type=int, default=1000, help="Smoke only; 1..100000")
    parser.add_argument("--pairs", type=int, default=1, help="Smoke only; 1..3")
    parser.add_argument("--min-free-gib", type=float, help="Free space on output volume")
    args = parser.parse_args()
    if args.mode == "full" and (args.rows != 10_000 or args.probes != 1000 or args.pairs != 1):
        parser.error("Full mode is frozen: 50k/1m rows, 1000 probes, 3 pairs, seed 777")
    if args.mode == "full" and args.min_free_gib is not None and args.min_free_gib < 2:
        parser.error("Full historical protocol requires at least 2 GiB free")
    if args.mode == "smoke" and not (2 <= args.rows <= 1_000_000 and
                                     1 <= args.probes <= 100_000 and 1 <= args.pairs <= 3):
        parser.error("Smoke bounds: rows=2..1000000, probes=1..100000, pairs=1..3")
    if args.min_free_gib is not None and not (0 < args.min_free_gib <= 1000):
        parser.error("--min-free-gib must be in (0,1000]")
    return args


def run(args: argparse.Namespace) -> int:
    sizes = [50_000, 1_000_000] if args.mode == "full" else [args.rows]
    pairs = 3 if args.mode == "full" else args.pairs
    probes = 1000 if args.mode == "full" else args.probes
    minimum = int((args.min_free_gib if args.min_free_gib is not None
                   else (2.0 if args.mode == "full" else 0.5)) * 2**30)
    if args.output is None:
        stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
        target = ROOT / "benchmarks/results/index-evolution" / (stamp + "-" + uuid.uuid4().hex[:8])
    else:
        target = args.output.expanduser().absolute()
    if target.exists() or target.is_symlink():
        raise RuntimeError(f"Refusing to modify an existing output path: {target}")
    disk_guard(target, minimum)
    sdk = require_tools(ROOT)
    csv_path = target / "results.csv"
    manifest_path = target / "manifest.json"
    selected_sdk = next(line.split()[0] for line in sdk["sdk"] if line.startswith("10."))
    source_dir = ROOT / "benchmarks/src/IndexEvolutionDirect"
    missing = [name for name in FILES if not (source_dir / name).is_file()]
    if missing: raise RuntimeError(f"Missing benchmark sources: {missing}")
    target.mkdir(parents=True, exist_ok=False)
    print(f"RESULTS_DIRECTORY={target}", flush=True)
    manifest = {
        "schema": 1, "status": "running", "mode": args.mode,
        "historicalSources": {"old": OLD, "new": NEW},
        "rows": sizes, "pairsPerSize": pairs, "probes": probes, "seed": 777,
        "plannedRuns": len(sizes) * pairs * 2, "acceptedRuns": 0,
        "host": {"platform": platform.platform(), "machine": platform.machine(),
                 "python": platform.python_version(), "cpuCount": os.cpu_count(),
                 "sdkList": sdk["sdk"], "runtimeList": sdk["runtime"],
                 "sdkSelected": selected_sdk},
        "sourceFilesSha256": {name: file_sha256(source_dir / name) for name in FILES},
        "runnerSha256": file_sha256(Path(__file__)),
        "supportSha256": file_sha256(Path(__file__).with_name("index_evolution_support.py")),
        "repoHead": checked(["git", "-C", str(ROOT), "rev-parse", "HEAD"]).strip(),
        "repoTrackedDirty": bool(checked(["git", "-C", str(ROOT), "status", "--porcelain", "-uno"]).strip()),
    }

    def save() -> None:
        manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n",
                                 encoding="utf-8")

    save()
    try:
        with csv_path.open("w", newline="", encoding="utf-8") as out:
            csv.DictWriter(out, fieldnames=COLUMNS).writeheader()
        builds: dict[str, Path] = {}
        for engine, sha in (("old", OLD), ("new", NEW)):
            disk_guard(target, minimum)
            checkout = target / "_build" / engine
            write_snapshot(ROOT, sha, checkout, legacy=engine == "old")
            (checkout / "global.json").write_text(
                json.dumps({"sdk": {"version": selected_sdk, "rollForward": "latestPatch"}}),
                encoding="utf-8")
            project = checkout / "benchmarks/src/IndexEvolutionDirect/IndexEvolutionDirect.csproj"
            command = ["dotnet", "build", str(project), "-c", "Release", "-v", "quiet",
                       "--nologo", "-p:GeneratePackageOnBuild=false"]
            command += (["-p:DefineConstants=LEGACY2021"] if engine == "old"
                        else ["-p:TargetFrameworks=net10.0"])
            logged(command, target / f"build-{engine}.log", cwd=checkout, timeout=600)
            dll = project.parent / "bin/Release/net10.0/IndexEvolutionDirect.dll"
            if not dll.is_file():
                raise RuntimeError(f"No compiled executable at {dll}")
            builds[engine] = dll
            print(f"BUILT {engine} {sha}", flush=True)
        for rows in sizes:
            for block in range(1, pairs + 1):
                order = ("old", "new") if block % 2 else ("new", "old")
                checksum = None
                for engine in order:
                    disk_guard(target, minimum)
                    sha = OLD if engine == "old" else NEW
                    log = target / f"measure-{rows}-{block}-{engine}.log"
                    with tempfile.TemporaryDirectory(prefix="_data-", dir=target) as data:
                        env = {**os.environ, "INDEX_EVOLUTION_SHA": sha}
                        logged(["dotnet", str(builds[engine]), str(Path(data) / "database"),
                                str(rows), str(probes), "777"], log,
                               cwd=builds[engine].parent, timeout=900, environment=env)
                    entry = parse_log(log, engine, sha, rows, probes, 777, block)
                    if checksum is not None and entry["checksum"] != checksum:
                        raise RuntimeError(f"Pair checksum mismatch for {rows=} {block=}")
                    checksum = entry["checksum"]
                    append_csv(csv_path, entry)
                    manifest["acceptedRuns"] += 1
                    save()
                    print(f"PASS rows={rows} block={block} engine={engine}", flush=True)
        manifest["status"] = "complete"
        manifest["resultsSha256"] = file_sha256(csv_path)
        save()
        print(f"COMPLETE {manifest['acceptedRuns']} runs; CSV={csv_path}", flush=True)
        return 0
    except Exception as exc:
        manifest["status"] = "failed"
        manifest["failure"] = str(exc)
        save()
        print(f"FAILED: {exc}. Partial data/logs preserved at {target}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    try:
        sys.exit(run(options()))
    except (RuntimeError, OSError, ValueError) as exc:
        print(f"PRE-FLIGHT FAILED: {exc}", file=sys.stderr)
        sys.exit(2)
