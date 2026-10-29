#!/usr/bin/env python3
"""Run counterbalanced independent benchmark coordinators on one machine."""
import argparse
import datetime as dt
import json
import os
from pathlib import Path
import subprocess
import time


def command(*args, cwd, env=None, timeout=None):
    subprocess.run(args, cwd=cwd, env=env, timeout=timeout, check=True)


def output(*args, cwd):
    return subprocess.check_output(args, cwd=cwd, text=True).strip()


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("project", help="Benchmark project directory name, e.g. PkIntLookup")
    p.add_argument("--rows", type=int, required=True, help="Explicit row count; use a publication dataset when ready")
    p.add_argument("--repeats", type=int, default=12, help="Multiple of four, at least eight")
    p.add_argument("--warmup", type=int, default=None)
    p.add_argument("--samples", type=int, default=None)
    p.add_argument("--timeout-seconds", type=int, default=900)
    p.add_argument("--no-build", action="store_true")
    args = p.parse_args()
    if args.repeats < 8 or args.repeats % 4 or args.rows < 1 or args.timeout_seconds < 1:
        p.error("Require repeats >= 8 and divisible by 4, rows >= 1, timeout >= 1")
    root = Path(__file__).resolve().parents[2]
    project = root / "benchmarks" / "src" / args.project / (args.project + ".csproj")
    dll = project.parent / "bin" / "Release" / "net10.0" / (args.project + ".dll")
    if not project.is_file():
        p.error("Unknown benchmark project: " + args.project)
    if output("git", "status", "--porcelain", "--untracked-files=no", cwd=root):
        p.error("Tracked worktree has modifications. Commit or discard before a publication series")
    sha = output("git", "rev-parse", "HEAD", cwd=root)
    if not args.no_build:
        command("dotnet", "build", "-c", "Release", str(project), cwd=root)
    if not dll.is_file():
        p.error("Release DLL not found: " + str(dll))
    stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    group = f"independent-{stamp}-{sha[:8]}-{args.project}"
    series_dir = root / "benchmarks" / "results" / "independent"
    series_dir.mkdir(parents=True, exist_ok=True)
    series_path = series_dir / (group + ".json")
    plan = {"schema": 1, "seriesId": group, "commit": sha,
            "project": args.project, "rows": args.rows, "runs": []}
    args_extra = [f"--rows={args.rows}"]
    if args.warmup is not None:
        args_extra.append(f"--warmup={args.warmup}")
    if args.samples is not None:
        args_extra.append(f"--samples={args.samples}")
    schedule = ("sqlite-first", "polar-first", "polar-first", "sqlite-first")
    try:
        for i in range(args.repeats):
            order = schedule[i % 4]
            run_id = f"{group}-r{i + 1:02d}"
            env = os.environ.copy()
            env.update(POLAR_BENCH_RUN_ID=run_id, POLAR_BENCH_ENGINE_ORDER=order)
            print(f"[{i + 1}/{args.repeats}] {run_id}: {order}", flush=True)
            started = time.monotonic()
            command("dotnet", str(dll), *args_extra, cwd=root,
                    env=env, timeout=args.timeout_seconds)
            matches = list((root / "benchmarks" / "results" / "raw").glob(
                f"*/{run_id}/combined.raw.json"))
            if len(matches) != 1:
                raise RuntimeError(f"Expected one immutable combined raw result, got {matches}")
            path = matches[0].relative_to(root).as_posix()
            raw = json.loads(matches[0].read_text(encoding="utf-8"))
            if raw["manifest"]["runId"] != run_id or raw["manifest"]["engineOrder"] != (
                ["sqlite", "polar-db"] if order == "sqlite-first" else ["polar-db", "sqlite"]):
                raise ValueError("Run id or engine order was not respected")
            plan["runs"].append({"runId": run_id, "order": order,
                                 "raw": path, "elapsedSeconds": round(time.monotonic() - started, 3)})
            series_path.write_text(json.dumps(plan, indent=2) + "\n", encoding="utf-8")
    except (Exception, KeyboardInterrupt):
        print("Interrupted: completed run metadata retained in " + str(series_path), flush=True)
        raise
    print("Series complete: " + str(series_path), flush=True)


if __name__ == "__main__":
    main()
