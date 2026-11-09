#!/usr/bin/env python3
"""Verify published manuscript tables and provenance without .NET, Git or RDC."""
from __future__ import annotations

import argparse
import os
from pathlib import Path, PurePosixPath
import subprocess
import sys
import tarfile
import tempfile

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "benchmarks/scripts"
MAIN_ARCHIVE = ROOT / "benchmarks/evidence/20261104/20261104T075733840Z-abbb03c-ext4.tar.gz"
PREFIX = "20261104T075733840Z-abbb03c-ext4/results/"


def extract_main_results(destination: Path) -> int:
    """Extract only the 19 compact result JSON/CSV pairs from a pinned tar."""
    count = 0
    names = set()
    with tarfile.open(MAIN_ARCHIVE, "r:gz") as tar:
        for member in tar:
            if not member.name.startswith(PREFIX):
                continue
            suffix = member.name[len(PREFIX):]
            relative = PurePosixPath(suffix)
            if len(relative.parts) != 1 or not suffix.endswith((".raw.csv", ".raw.json")):
                continue
            if not member.isfile() or member.size > 100_000_000 or suffix in names:
                raise RuntimeError(f"Unsafe or duplicate archived result: {member.name}")
            names.add(suffix)
            source = tar.extractfile(member)
            if source is None:
                raise RuntimeError("Cannot extract result " + member.name)
            with (destination / suffix).open("wb") as output:
                while True:
                    chunk = source.read(1 << 20)
                    if not chunk:
                        break
                    output.write(chunk)
            count += 1
    if count != 38:
        raise RuntimeError(f"Expected 38 primary CSV/JSON files, found {count}")
    return count


def verify() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", choices=("all", "main", "cache", "historical", "provenance"),
                        default="all")
    args = parser.parse_args()
    selection = ("provenance", "main", "cache", "historical") if args.only == "all" else (args.only,)
    for step in selection:
        print(f"CHECK {step}", flush=True)
        env = os.environ.copy()
        if step == "main":
            with tempfile.TemporaryDirectory(prefix="polar-main-article-") as directory:
                extract_main_results(Path(directory))
                env["POLAR_DB_MAIN_RESULTS_DIR"] = directory
                command = [sys.executable, str(SCRIPTS / "check-article-results.py")]
                result = subprocess.run(command, cwd=ROOT, env=env, check=False)
        else:
            script = {"provenance": "check-evidence-provenance.py",
                      "cache": "check-stage4b-article-results.py",
                      "historical": "check-index-evolution-results.py"}[step]
            result = subprocess.run([sys.executable, str(SCRIPTS / script)], cwd=ROOT, env=env,
                                    check=False)
        if result.returncode != 0:
            print(f"FAILED {step}: exit code {result.returncode}", file=sys.stderr)
            return result.returncode
    print(f"PASS publication checks: {', '.join(selection)}", flush=True)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(verify())
    except (OSError, ValueError, RuntimeError, tarfile.TarError) as error:
        print("FAILED: " + str(error), file=sys.stderr)
        sys.exit(2)
