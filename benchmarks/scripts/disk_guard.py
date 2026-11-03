#!/usr/bin/env python3
"""Bound benchmark subprocesses by disk headroom, growth and elapsed time."""
import argparse
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import time

GIB = 1024 ** 3


class DiskBudgetExceeded(RuntimeError):
    pass


def stop_process_tree(process):
    """Stop workers spawned by a benchmark coordinator, not just the parent."""
    if os.name == "nt":
        subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                       check=False)
    else:
        try:
            os.killpg(process.pid, signal.SIGTERM)
        except ProcessLookupError:
            pass
        try:
            process.wait(timeout=2)
        except subprocess.TimeoutExpired:
            pass
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    try:
        process.wait(timeout=3)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait()


def run_guarded(command, *, root, timeout_seconds=900, min_free_gib=16.0,
                max_growth_gib=8.0, env=None, poll_seconds=0.5,
                read_free=None):
    """Fail closed before starting, and kill the process tree if limits trip."""
    root = Path(root).resolve()
    if not root.is_dir():
        raise ValueError(f"Missing disk root: {root}")
    if timeout_seconds <= 0 or poll_seconds <= 0:
        raise ValueError("Timeout and polling interval must be positive")
    if min_free_gib < 8 or max_growth_gib <= 0:
        raise ValueError("Reserve must be at least 8 GiB; growth must be positive")
    for relative in ("benchmarks/work", "benchmarks/results"):
        path = root / relative
        if path.exists() and os.stat(path).st_dev != os.stat(root).st_dev:
            raise RuntimeError(f"Benchmark output on another volume: {path}")
    reserve = int(min_free_gib * GIB)
    growth = int(max_growth_gib * GIB)
    available = read_free or (lambda: shutil.disk_usage(root).free)
    initial = available()
    if initial <= reserve + growth:
        raise DiskBudgetExceeded(
            f"Preflight: free={initial / GIB:.2f} GiB, "
            f"need > {min_free_gib + max_growth_gib:.2f} GiB")
    print(f"Disk guard: free={initial / GIB:.2f} GiB, "
          f"reserve={min_free_gib:g} GiB, max drop={max_growth_gib:g} GiB",
          flush=True)
    flags = (subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0)
    process = subprocess.Popen(command, cwd=root, env=env,
                               creationflags=flags, start_new_session=os.name != "nt")
    started = time.monotonic()
    try:
        while True:
            free = available()
            if free <= reserve or initial - free >= growth:
                raise DiskBudgetExceeded(
                    f"Disk limit: free={free / GIB:.2f} GiB, "
                    f"drop={(initial - free) / GIB:.2f} GiB")
            remaining = timeout_seconds - (time.monotonic() - started)
            if remaining <= 0:
                raise subprocess.TimeoutExpired(command, timeout_seconds)
            try:
                code = process.wait(timeout=min(poll_seconds, remaining))
            except subprocess.TimeoutExpired:
                continue
            if code:
                raise subprocess.CalledProcessError(code, command)
            return
    except BaseException:
        stop_process_tree(process)
        raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--min-free-gib", type=float, default=16.0)
    parser.add_argument("--max-growth-gib", type=float, default=8.0)
    parser.add_argument("--timeout-seconds", type=int, default=900)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    options = parser.parse_args()
    cmd = options.command
    if cmd and cmd[0] == "--":
        cmd = cmd[1:]
    if not cmd:
        parser.error("Command required after --")
    try:
        run_guarded(cmd, root=Path(__file__).resolve().parents[2],
                    timeout_seconds=options.timeout_seconds,
                    min_free_gib=options.min_free_gib,
                    max_growth_gib=options.max_growth_gib)
    except (DiskBudgetExceeded, subprocess.CalledProcessError,
            subprocess.TimeoutExpired, ValueError, RuntimeError) as error:
        print(f"BENCHMARK STOPPED: {error}", file=sys.stderr, flush=True)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
