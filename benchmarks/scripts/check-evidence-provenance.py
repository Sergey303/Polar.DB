#!/usr/bin/env python3
"""Read-only verification of published Polar.DB experiment provenance archives.

Checks SHA-256, main-series worker provenance and raw JSON digests, plus the
pre-registered Stage 4B protocol lock. Does not rerun benchmarks or require Git.
"""
import hashlib
import json
import tarfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MAIN = ROOT / "benchmarks/evidence/20261104"
STAGE = ROOT / "benchmarks/evidence/20261107"
MAIN_ARCHIVE = "20261104T075733840Z-abbb03c-ext4.tar.gz"
STAGE_ARCHIVE = "ram-study-research-snapshot.tar.gz"
MAIN_SOURCE = "abbb03cdc34cff1c11e7550494f65b03dad9afd1"
STAGE_SOURCE = "a11457ce502b416041ea7bde0b53d2dc047db491"
STAGE_DATA_SOURCE = "0fd84e3da854aa9f536a75861e7838af41955bdc"


def checksum(data):
    return hashlib.sha256(data).hexdigest()


def archive_bytes(directory, filename):
    sums = (directory / "SHA256SUMS").read_text(encoding="utf-8").splitlines()
    entries = [line.split() for line in sums if line.strip()]
    assert len(entries) == 1 and entries[0][1] == filename, "Unexpected checksum list"
    data = (directory / filename).read_bytes()
    assert checksum(data) == entries[0][0], f"Published archive differs: {filename}"
    return directory / filename


def required_file(tar, name):
    member = tar.getmember(name)
    assert member.isfile() and member.size >= 0
    return tar.extractfile(member).read()


main_path = archive_bytes(MAIN, MAIN_ARCHIVE)
stage_path = archive_bytes(STAGE, STAGE_ARCHIVE)
with tarfile.open(main_path, "r:gz") as tar:
    prefix = "20261104T075733840Z-abbb03c-ext4/"
    validation = json.loads(required_file(tar, prefix + "validation.json"))
    assert validation["commitSha"] == MAIN_SOURCE
    assert validation["experimentCount"] == 19
    assert validation["workerFiles"] == 38
    assert validation["checksPassed"] is True and validation["issues"] == []
    experiments = validation["experiments"]
    assert len(experiments) == 19
    counts = (0, 0)
    raw_sha_count = 0
    for experiment in experiments:
        name = experiment["experimentId"]
        base = prefix + "results/" + name
        raw = required_file(tar, base + ".raw.json")
        assert checksum(raw) == experiment["rawSha256"], f"Mismatch: {name}"
        raw_sha_count += 1
        assert len(required_file(tar, base + ".raw.csv")) > 0
        manifest = json.loads(required_file(tar, base + ".manifest.json"))
        assert manifest["experimentId"] == name
        assert manifest["runId"] == validation["runId"]
        processes = [manifest["coordinator"], *manifest["engineProcesses"]]
        assert len(processes) == 3, f"Expected coordinator and 2 workers: {name}"
        for record in processes:
            assert record["commitSha"] == MAIN_SOURCE
            assert record["gitStatusKnown"] and not record["gitDirty"]
            assert record["publicationReady"] is True
        assert set(x["engine"] for x in manifest["engineProcesses"]) == {"sqlite", "polar-db"}
    print(f"MAIN: {raw_sha_count}/19 raw JSON SHA-256 match; "
          "19 coordinator and 38 worker provenance records match source SHA")

with tarfile.open(stage_path, "r:gz") as tar:
    prefix = "ram-study/stage4b-pinned-confirmation/"
    raw_lock = required_file(tar, prefix + "LOCK.json")
    lock = json.loads(raw_lock)
    sha_text = required_file(tar, prefix + "LOCK_SHA256.txt").decode().split()[0]
    assert checksum(raw_lock) == sha_text, "Frozen lock changed"
    assert lock["runnerSha"] == STAGE_SOURCE
    assert lock["datasetSourceSha"] == STAGE_DATA_SOURCE
    assert lock["plannedBlocks"] == 8 and lock["plannedAttempts"] == 48
    assert len(lock["sha256"]) == 11
    for name, expected in lock["sha256"].items():
        assert checksum(required_file(tar, prefix + name)) == expected, (
            f"Frozen Stage 4B protocol file differs: {name}"
        )
    result = json.loads(required_file(tar, prefix + "STAGE4B_RESULTS.json"))
    assert result["planned"] == result["attempted"] == result["accepted"] == 48
    assert result["missing"] == []
    assert result["primaryDecision"] == "PRIMARY_REPLICATED"
    print("STAGE 4B: lock SHA-256 and 11/11 pinned protocol files match; "
          "48/48 attempts accounted for; locked source SHAs preserved")

print("OK: both published evidence archives and declared provenance verified")
