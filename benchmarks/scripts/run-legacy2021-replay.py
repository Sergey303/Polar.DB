#!/usr/bin/env python3
"""Portable historical Main306 index smoke; diagnostic, not article evidence."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import platform
import re
import sys
import tempfile
import uuid

from index_evolution_support import (
    OLD, NEW, checked, disk_guard, file_sha256, logged, require_tools, write_snapshot,
)

ROOT = Path(__file__).resolve().parents[2]
SUITE = "Legacy2021Replay"
FILES = ("Legacy2021Replay.csproj", "Program.cs", "IndexReplay.cs", "BinaryReplay.cs")


def run() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("rows", nargs="?", default=10000, type=int)
    parser.add_argument("probes", nargs="?", default=1000, type=int)
    parser.add_argument("seed", nargs="?", default=777, type=int)
    parser.add_argument("--output", type=Path, help="Unique, nonexisting destination")
    parser.add_argument("--min-free-gib", type=float, default=0.5)
    args = parser.parse_args()
    if not (2 <= args.rows <= 100_000 and 1 <= args.probes <= 100_000):
        parser.error("Safety bounds: rows=2..100000, probes=1..100000")
    if not (0 < args.min_free_gib <= 1000):
        parser.error("--min-free-gib must be between 0 and 1000")
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    target = (args.output.expanduser().absolute() if args.output else
              ROOT / "benchmarks/results/index-evolution" / ("legacy-" + stamp + "-" + uuid.uuid4().hex[:8]))
    if target.exists() or target.is_symlink():
        raise RuntimeError(f"Refusing to overwrite existing output: {target}")
    minimum = int(args.min_free_gib * 2**30)
    disk_guard(target, minimum)
    env = require_tools(ROOT)
    sdk = next(line.split()[0] for line in env["sdk"] if line.startswith("10."))
    src = ROOT / "benchmarks/src" / SUITE
    missing = [name for name in FILES if not (src / name).is_file()]
    if missing:
        raise RuntimeError(f"Missing legacy replay sources: {missing}")
    target.mkdir(parents=True, exist_ok=False)
    manifest = {
        "schema": 1, "status": "running", "purpose": "historical Main306 smoke, not statistical",
        "rows": args.rows, "probes": args.probes, "seed": args.seed,
        "sources": {"old": OLD, "new": NEW},
        "sdk": sdk, "availableRuntimes": env["runtime"],
        "platform": platform.platform(),
        "sourceSha256": {f: file_sha256(src / f) for f in FILES},
        "runnerSha256": file_sha256(Path(__file__)), "completed": [],
    }
    dest = target / "manifest.json"

    def save() -> None:
        dest.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
                        encoding="utf-8")

    save()
    print(f"RESULTS_DIRECTORY={target}", flush=True)
    try:
        for engine, sha in (("old", OLD), ("new", NEW)):
            disk_guard(target, minimum)
            checkout = target / "_build" / engine
            write_snapshot(ROOT, sha, checkout, legacy=engine == "old", suite=SUITE,
                           files=FILES)
            (checkout / "global.json").write_text(
                json.dumps({"sdk": {"version": sdk, "rollForward": "latestPatch"}}),
                encoding="utf-8")
            project = checkout / "benchmarks/src" / SUITE / (SUITE + ".csproj")
            command = ["dotnet", "build", str(project), "-c", "Release", "--nologo",
                       "-v", "quiet", "-p:GeneratePackageOnBuild=false"]
            if engine == "new":
                command += ["-p:TargetFrameworks=net10.0"]
            logged(command, target / f"build-{engine}.log", checkout, 600)
            dll = project.parent / "bin/Release/net10.0" / (SUITE + ".dll")
            if not dll.exists():
                raise RuntimeError("No compiled replay executable: " + str(dll))
            log = target / f"index-{engine}.log"
            with tempfile.TemporaryDirectory(prefix="_data-", dir=target) as data:
                environment = {**os.environ, "REPLAY_ENGINE_SHA": sha}
                logged(["dotnet", str(dll), "index", str(Path(data) / "database"),
                        str(args.rows), str(args.probes), str(args.seed)], log,
                       dll.parent, 900, environment)
            contents = log.read_text(encoding="utf-8")
            if not re.search(rf"^verified_lookup_count={args.probes}$", contents, re.M):
                raise RuntimeError("Missing exact verified count in " + str(log))
            if not re.search(rf"^engine-source={sha}$", contents, re.M):
                raise RuntimeError("Incorrect pinned engine SHA in " + str(log))
            manifest["completed"].append(engine)
            save()
            print(f"PASS {engine}: {args.probes} verified lookups", flush=True)
        manifest["status"] = "complete"
        save()
        print(f"COMPLETE: two historical builds at {target}", flush=True)
        return 0
    except Exception as error:
        manifest["status"] = "failed"
        manifest["failure"] = str(error)
        save()
        print(f"FAILED: {error}; partial logs preserved at {target}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    try:
        sys.exit(run())
    except (OSError, RuntimeError, ValueError) as error:
        print("PRE-FLIGHT FAILED: " + str(error), file=sys.stderr)
        sys.exit(2)
