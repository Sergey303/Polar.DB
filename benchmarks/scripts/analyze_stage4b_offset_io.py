#!/usr/bin/env python3
"""Read-only post-hoc page-accounting analysis of the published Stage 4B archive.

Does not run benchmarks, modify evidence, or assign physical I/O to a specific file.
Reconstructs the exact immutable lookup plan from UniformLookupPlanner.cs.
"""
import argparse
import hashlib
import json
import statistics
import struct
import tarfile
from pathlib import Path

MASK = (1 << 64) - 1
SEED = 0x6A09E667F3BCC909
STREAM_SHA256 = "44106a815bb975ddbf956a475c5b6463832ce2bdaa139b59da6c18ffa50daf57"
ARCHIVE_SHA256 = "ce53fed176518e3aca32c334d8eb47c7bb72110f4be48267970b020185545a79"
PAGE_BYTES = 4096
OFFSET_BYTES = 8
HEADER_BYTES = 8
ROWS = 20_000_000
MEASURED_KEYS = 10_000


def original_key_stream():
    state = SEED
    seen = set()
    ids = []
    threshold = ((1 << 64) - ROWS) % ROWS
    while len(ids) < 12_500:
        state = (state + 0x9E3779B97F4A7C15) & MASK
        z = state
        z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & MASK
        z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & MASK
        z ^= z >> 31
        if z < threshold:
            continue
        value = z % ROWS + 1
        if value not in seen:
            ids.append(value)
            seen.add(value)
    digest = hashlib.sha256(b"".join(struct.pack("<i", v) for v in ids)).hexdigest()
    if digest != STREAM_SHA256:
        raise ValueError("The exact key stream differs from the Stage 4B seal")
    return ids


def file_state(result, point, name):
    return next(f for f in result[point]["Files"] if f["Name"] == name)


def analyze(archive):
    if hashlib.sha256(archive.read_bytes()).hexdigest() != ARCHIVE_SHA256:
        raise ValueError("Stage 4B archive SHA-256 mismatch")
    keys = original_key_stream()[500:10_500]
    if len(keys) != MEASURED_KEYS:
        raise AssertionError("Unexpected measured key count")
    # All IDs fit int32. BenchmarkChecksum.StableHash(long) = ID here;
    # the 20m-row no-update index sorts these hashes, so position = ID - 1.
    touched = {((value - 1) * OFFSET_BYTES + HEADER_BYTES) // PAGE_BYTES for value in keys}
    results = []
    with tarfile.open(archive, "r:gz") as tar:
        selected = [
            m for m in tar.getmembers()
            if "/stage4b-pinned-confirmation/raw/pk-int-lookup/" in m.name
            and m.name.endswith("-polar-db-offsets.json")
        ]
        if len(selected) != 8:
            raise ValueError(f"Expected 8 offset runs, found {len(selected)}")
        for m in sorted(selected, key=lambda x: x.name):
            result = json.load(tar.extractfile(m))
            if result["PlanKeyStreamSha256"] != STREAM_SHA256:
                raise ValueError(f"Plan mismatch: {m.name}")
            if result["Rows"] != ROWS or result["BatchRows"] != MEASURED_KEYS:
                raise ValueError(f"Unexpected workload: {m.name}")
            if result["PinnedCacheProtocol"] != "mlock-v1":
                raise ValueError(f"Unpinned record: {m.name}")
            pages_before = file_state(result, "ResidencyBeforeBatch", "f2.bin")
            pages_after = file_state(result, "ResidencyAfterBatch", "f2.bin")
            if pages_before["Pages"] != (HEADER_BYTES + ROWS * OFFSET_BYTES + PAGE_BYTES - 1) // PAGE_BYTES:
                raise ValueError(f"Offset file size differs: {m.name}")
            for f in ["f0.bin", "f1.bin"]:
                bef = file_state(result, "ResidencyBeforeBatch", f)
                aft = file_state(result, "ResidencyAfterBatch", f)
                if bef["ResidentPages"] != bef["Pages"] or aft["ResidentPages"] != aft["Pages"]:
                    raise ValueError(f"Unselected file not fully resident: {m.name}: {f}")
            snapshots = result["Snapshots"]
            delta_io = (
                snapshots["AfterBatch"]["ProcessIo"]["read_bytes"]
                - snapshots["AfterCacheTreatment"]["ProcessIo"]["read_bytes"]
            )
            samples = [v * 1000 for v in result["BatchAverageMs"]]
            if len(samples) != 100:
                raise ValueError(f"Unexpected number of batch groups: {m.name}")
            gain = pages_after["ResidentPages"] - pages_before["ResidentPages"]
            results.append({
                "run": result["RunId"],
                "mean_us": statistics.mean(samples),
                "first_1000_mean_us": statistics.mean(samples[:10]),
                "last_1000_mean_us": statistics.mean(samples[-10:]),
                "f2_resident_before": pages_before["ResidentPages"],
                "f2_resident_after": pages_after["ResidentPages"],
                "f2_page_gain": gain,
                "f2_resident_gain_bytes": gain * PAGE_BYTES,
                "process_batch_read_bytes": delta_io,
            })
    def mean(key):
        return statistics.mean(x[key] for x in results)
    minimum_bytes = len(touched) * PAGE_BYTES
    return {
        "source": str(archive),
        "archive_sha256": ARCHIVE_SHA256,
        "key_stream_sha256": STREAM_SHA256,
        "rows": ROWS,
        "batch_queries": MEASURED_KEYS,
        "page_size_bytes": PAGE_BYTES,
        "offsets_file_bytes": HEADER_BYTES + ROWS * OFFSET_BYTES,
        "offset_file_pages": (HEADER_BYTES + ROWS * OFFSET_BYTES + PAGE_BYTES - 1) // PAGE_BYTES,
        "distinct_required_offset_pages": len(touched),
        "minimum_offset_bytes_if_only_required_pages_loaded": minimum_bytes,
        "mean_page_gain": mean("f2_page_gain"),
        "mean_resident_gain_bytes": mean("f2_resident_gain_bytes"),
        "mean_process_batch_read_bytes": mean("process_batch_read_bytes"),
        "resident_page_gain_over_minimum": mean("f2_page_gain") / len(touched),
        "process_read_bytes_over_minimum": mean("process_batch_read_bytes") / minimum_bytes,
        "mean_query_us": mean("mean_us"),
        "mean_first_1000_us": mean("first_1000_mean_us"),
        "mean_last_1000_us": mean("last_1000_mean_us"),
        "read_bytes_are_process_wide": True,
        "causality_established": False,
        "runs": results,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--archive", type=Path,
        default=Path(__file__).resolve().parents[1] / "evidence" / "20261107"
        / "ram-study-research-snapshot.tar.gz",
    )
    args = parser.parse_args()
    print(json.dumps(analyze(args.archive), indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
