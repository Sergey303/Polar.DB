"""Portable, stdlib-only helpers for immutable Polar.DB historical comparisons."""
from __future__ import annotations

import csv
import hashlib
import io
import math
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import tarfile

OLD = "39cefe13db8697cf5f8e15edaa4677ba52a2cdea"
NEW = "09ec0703f476f25ec79baff338843df8a8e83b35"
FILES = ("IndexEvolutionDirect.csproj", "Program.cs", "IndexAdapter.cs",
         "LegacyAdapter.cs", "ModernAdapter.cs")
COLUMNS = ("block", "engine", "sha", "rows", "probes", "seed", "load_ms",
           "prerequisite_ms", "index_build_ms", "lookup_ms", "checksum", "verified")


def checked(command: list[str], cwd: Path | None = None, timeout: int = 120) -> str:
    result = subprocess.run(command, cwd=cwd, capture_output=True, text=True,
                            encoding="utf-8", errors="replace", timeout=timeout)
    if result.returncode:
        raise RuntimeError(f"Command failed ({result.returncode}): {command}\n"
                           f"{result.stdout[-1500:]}\n{result.stderr[-1500:]}")
    return result.stdout


def require_tools(repo: Path) -> dict:
    for tool in ("git", "dotnet"):
        if not shutil.which(tool):
            raise RuntimeError(f"Missing required executable: {tool}")
    sdk = checked(["dotnet", "--list-sdks"])
    runtime = checked(["dotnet", "--list-runtimes"])
    if not any(line.startswith("10.") for line in sdk.splitlines()):
        raise RuntimeError("A .NET 10 SDK is required; found:\n" + sdk)
    if not any(line.startswith("Microsoft.NETCore.App 10.") for line in runtime.splitlines()):
        raise RuntimeError("A .NET 10 runtime is required; found:\n" + runtime)
    for sha in (OLD, NEW):
        try:
            checked(["git", "-C", str(repo), "cat-file", "-e", sha + "^{commit}"])
        except RuntimeError as error:
            raise RuntimeError(f"Pinned commit {sha} not found. Fetch full Git history "
                               "(e.g. git fetch --unshallow, if applicable).") from error
    return {"sdk": sdk.splitlines(), "runtime": runtime.splitlines()}


def disk_guard(path: Path, min_free_bytes: int) -> None:
    current = path.resolve()
    while not current.exists():
        if current.parent == current:
            raise RuntimeError(f"No existing parent for output path {path}")
        current = current.parent
    available = shutil.disk_usage(current).free
    if available < min_free_bytes:
        raise RuntimeError(f"Insufficient free space on {current}: "
                           f"{available / 2**30:.2f} GiB, "
                           f"require {min_free_bytes / 2**30:.2f} GiB")


def write_snapshot(repo: Path, sha: str, target: Path, legacy: bool,
                   suite: str = "IndexEvolutionDirect", files: tuple[str, ...] = FILES) -> None:
    entries = ["src/Polar.DB"] if legacy else ["src/Polar.DB", "README.md", "LICENSE", "icon.png"]
    result = subprocess.run(["git", "-C", str(repo), "archive", sha, *entries],
                            capture_output=True, timeout=120)
    if result.returncode:
        raise RuntimeError("Cannot extract source SHA " + sha + ": " +
                           result.stderr.decode(errors="replace")[-1200:])
    with tarfile.open(fileobj=io.BytesIO(result.stdout), mode="r:") as archive:
        for member in archive:
            name = PurePosixPath(member.name)
            if name.is_absolute() or ".." in name.parts:
                raise RuntimeError("Unsafe Git archive entry: " + member.name)
            dest = target.joinpath(*name.parts)
            if member.isdir():
                dest.mkdir(parents=True, exist_ok=True)
            elif member.isfile():
                dest.parent.mkdir(parents=True, exist_ok=True)
                with archive.extractfile(member) as source, dest.open("wb") as output:
                    shutil.copyfileobj(source, output)
            else:
                raise RuntimeError("Unsupported Git archive entry: " + member.name)
    if legacy:
        project = target / "src/Polar.DB/Polar.DB.csproj"
        source = project.read_text(encoding="utf-8")
        marker = "<TargetFramework>netstandard2.0</TargetFramework>"
        if source.count(marker) != 1:
            raise RuntimeError("Pinned 2021 project target differs; refusing source adaptation")
        project.write_text(source.replace(marker, "<TargetFramework>net10.0</TargetFramework>"),
                           encoding="utf-8")
    bench = target / "benchmarks/src" / suite
    bench.mkdir(parents=True)
    for name in files:
        shutil.copy2(repo / "benchmarks/src" / suite / name, bench / name)


def logged(command: list[str], log: Path, cwd: Path, timeout: int,
           environment: dict[str, str] | None = None) -> None:
    with log.open("w", encoding="utf-8", errors="replace") as output:
        try:
            status = subprocess.run(command, cwd=cwd, stdout=output, stderr=subprocess.STDOUT,
                                    env=environment, timeout=timeout).returncode
        except subprocess.TimeoutExpired as exc:
            raise RuntimeError(f"Timed out after {timeout}s, see {log}") from exc
    if status:
        raise RuntimeError(f"Exit code {status} from {command[0]}; see {log}")


def parse_log(log: Path, engine: str, sha: str, rows: int, probes: int,
              seed: int, block: int) -> dict[str, str]:
    results = [line[7:] for line in log.read_text(encoding="utf-8").splitlines()
               if line.startswith("RESULT ")]
    if len(results) != 1:
        raise RuntimeError(f"Expected exactly one RESULT line in {log}")
    tokens = results[0].split()
    if any(token.count("=") != 1 for token in tokens):
        raise RuntimeError(f"Malformed RESULT in {log}")
    values = dict(token.split("=", 1) for token in tokens)
    expected = {"engine": sha, "rows": str(rows), "probes": str(probes),
                "seed": str(seed), "verified": "true"}
    if any(values.get(k) != v for k, v in expected.items()):
        raise RuntimeError(f"Incorrect RESULT metadata in {log}")
    metrics = ("load_ms", "prerequisite_ms", "index_build_ms", "lookup_ms")
    if any(not math.isfinite(float(values[m])) or float(values[m]) < 0 for m in metrics):
        raise RuntimeError(f"Invalid timing metric in {log}")
    if int(values["checksum"]) < 0:
        raise RuntimeError(f"Invalid checksum in {log}")
    return {"block": str(block), "engine": engine, "sha": sha,
            **{k: values[k] for k in COLUMNS if k not in ("block", "engine", "sha")}}


def file_sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as file:
        for block in iter(lambda: file.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def append_csv(path: Path, record: dict[str, str]) -> None:
    with path.open("a", newline="", encoding="utf-8") as out:
        writer = csv.DictWriter(out, fieldnames=COLUMNS)
        writer.writerow(record)
