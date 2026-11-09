#!/usr/bin/env python3
"""Verify published manuscript tables and provenance without .NET, Git or RDC."""
from __future__ import annotations

import argparse
import json
import math
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



def same_statistics(actual,expected,where='root') -> None:
    """Exact provenance/shape, numeric tolerance only for derived IEEE754 floats."""
    if type(actual) is not type(expected):
        raise RuntimeError(f'Type mismatch at {where}: {type(actual).__name__} vs {type(expected).__name__}')
    if isinstance(actual,dict):
        if actual.keys()!=expected.keys():
            raise RuntimeError(f'Keys differ at {where}')
        for key in actual:
            same_statistics(actual[key],expected[key],where+'.'+key)
    elif isinstance(actual,list):
        if len(actual)!=len(expected):
            raise RuntimeError(f'List size differs at {where}')
        for i,(x,y) in enumerate(zip(actual,expected)):
            same_statistics(x,y,f'{where}[{i}]')
    elif isinstance(actual,float):
        if not (math.isfinite(actual) and math.isfinite(expected) and
                math.isclose(actual,expected,rel_tol=1e-12,abs_tol=1e-12)):
            raise RuntimeError(f'Numerical mismatch at {where}: {actual} vs {expected}')
    elif actual!=expected:
        raise RuntimeError(f'Value mismatch at {where}: {actual} vs {expected}')


def verify() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", choices=("all", "main", "cache", "historical", "provenance", "paired20", "hosted20"),
                        default="all")
    args = parser.parse_args()
    selection = ("provenance", "main", "cache", "historical", "paired20", "hosted20") if args.only == "all" else (args.only,)
    for step in selection:
        print(f"CHECK {step}", flush=True)
        env = os.environ.copy()
        if step == "hosted20":
            artifact = ROOT / "benchmarks/evidence/20261109-index-evolution-hosted-42573634646"
            summary = []
            for osname in ("ubuntu", "windows", "macos"):
                directory = artifact / osname
                command = [sys.executable, str(SCRIPTS / "analyze-index-evolution-paired20.py"), str(directory)]
                completed = subprocess.run(command, cwd=ROOT, env=env, capture_output=True,
                                           text=True, encoding="utf-8")
                if completed.returncode:
                    print(completed.stdout, completed.stderr, file=sys.stderr)
                    raise RuntimeError("Cannot independently analyze hosted experiment: " + osname)
                recalc = json.loads(completed.stdout)
                expected = json.loads((directory / "summary.json").read_text(encoding="utf-8"))
                if recalc["manifestGitHead"] != "f6e8fac0376d5581090a5f29ed2a98cf6da246ba":
                    raise RuntimeError("Hosted source Git SHA mismatch: " + osname)
                same_statistics(recalc,expected,where=osname)
                sums = (directory / "SHA256SUMS").read_text(encoding="utf-8").splitlines()
                from hashlib import sha256
                for line in sums:
                    digest, name = line.split("  ", 1)
                    file = directory / name
                    if not file.is_file() or sha256(file.read_bytes()).hexdigest() != digest:
                        raise RuntimeError("Hosted SHA-256 mismatch: " + osname + "/" + name)
                summary.append(osname)
            subprocess.run([sys.executable, str(SCRIPTS / "check-index-evolution-article12.py")],
                           cwd=ROOT, env=env, check=True)
            print("OK: 3 hosted OS, 120 pairs, 240 process logs, article Table 12 and all SHA-256 verified")
            result = subprocess.CompletedProcess([], 0)
        elif step == "paired20":
            with tempfile.TemporaryDirectory(prefix="polar-paired20-verification-") as directory:
                artifact = ROOT / "benchmarks/evidence/20261109-index-evolution-paired20"
                command = [sys.executable, str(SCRIPTS / "analyze-index-evolution-paired20.py"),
                           str(artifact), "--out", str(Path(directory) / "summary.json")]
                result = subprocess.run(command, cwd=ROOT, env=env, check=False,
                                        capture_output=True, text=True, encoding="utf-8")
                if result.returncode != 0:
                    print(result.stdout, file=sys.stderr)
                    print(result.stderr, file=sys.stderr)
                if result.returncode == 0:
                    expected=(artifact/"summary.json").read_text(encoding="utf-8")
                    actual=(Path(directory)/"summary.json").read_text(encoding="utf-8")
                    if expected != actual:
                        raise RuntimeError("Paired20 published summary differs from recalculated data")
                    sums=(artifact/"SHA256SUMS").read_text(encoding="utf-8").splitlines()
                    from hashlib import sha256
                    for line in sums:
                        digest,name=line.split('  ',1)
                        file=artifact/name
                        if not file.is_file() or sha256(file.read_bytes()).hexdigest()!=digest:
                            raise RuntimeError("Paired20 evidence file checksum mismatch: "+name)
                    print("OK: 40 pairs, 80 logs, frozen schedule, ratio bootstrap, all published file digests")
        elif step == "main":
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
