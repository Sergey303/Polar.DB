#!/usr/bin/env python3
"""Verify manuscript table 10 and its primary paired effect from archived Stage 4B raw JSON."""
import hashlib
import json
import math
import random
import re
import statistics
import tarfile
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ARCHIVE = ROOT / "benchmarks/evidence/20261107/ram-study-research-snapshot.tar.gz"
ARCHIVE_SHA256 = "ce53fed176518e3aca32c334d8eb47c7bb72110f4be48267970b020185545a79"
KEY_STREAM_SHA256 = "44106a815bb975ddbf956a475c5b6463832ce2bdaa139b59da6c18ffa50daf57"
ARTICLE = (ROOT / "benchmarks/docs/POLAR_DB_ARTICLE_DRAFT.md").read_text(encoding="utf-8")
CONDITIONS = (
    ("polar-db", "none"),
    ("polar-db", "hashes"),
    ("polar-db", "offsets"),
    ("polar-db", "payload"),
    ("sqlite", "none"),
    ("sqlite", "data"),
)


def rounded(cell, actual):
    text = cell.replace(" ", "").replace(",", ".")
    decimals = len(text.split(".")[1]) if "." in text else 0
    assert math.isfinite(actual) and abs(float(text) - actual) <= 0.501 * 10 ** -decimals, (
        f"Published {cell} differs from computed {actual}"
    )


def quantile(sorted_values, fraction):
    position = (len(sorted_values) - 1) * fraction
    lo = math.floor(position)
    hi = math.ceil(position)
    return sorted_values[lo] + (sorted_values[hi] - sorted_values[lo]) * (position - lo)


assert hashlib.sha256(ARCHIVE.read_bytes()).hexdigest() == ARCHIVE_SHA256, "Evidence SHA mismatch"
section = ARTICLE.split("Таблица 10 —", 1)
assert len(section) == 2, "Manuscript table 10 missing or duplicated"
table_and_discussion = section[1].split("### 4.8.", 1)[0]
match = re.search(r"^[^\n]*\n\n((?:\|[^\n]+\n)+)", table_and_discussion)
assert match, "Cannot read manuscript table 10"
rows = [
    [part.strip() for part in line.strip().strip("|").split("|")]
    for line in match.group(1).splitlines()[2:]
]
assert len(rows) == len(CONDITIONS), "Table 10 must contain six conditions"

samples = defaultdict(dict)
read_bytes = defaultdict(list)
seen = set()
with tarfile.open(ARCHIVE, "r:gz") as tar:
    metadata = next(m for m in tar if m.name.endswith("/stage4b-pinned-confirmation/STAGE4B_RESULTS.json"))
    report = json.load(tar.extractfile(metadata))
    assert report["planned"] == report["attempted"] == report["accepted"] == 48
    assert not report["missing"]
    for member in tar.getmembers():
        if "/stage4b-pinned-confirmation/raw/pk-int-lookup/" not in member.name:
            continue
        if not member.name.endswith(".json"):
            continue
        record = json.load(tar.extractfile(member))
        condition = (record["Engine"], record["CacheTreatment"])
        assert condition in CONDITIONS
        block = record["RunId"].split("-")[1]
        assert (condition, block) not in seen, "Duplicate block"
        seen.add((condition, block))
        assert record["PlanKeyStreamSha256"] == KEY_STREAM_SHA256
        assert record["Rows"] == 20_000_000 and record["BatchRows"] == 10_000
        assert len(record["BatchAverageMs"]) == 100
        samples[condition][block] = statistics.mean(record["BatchAverageMs"]) * 1000
        stages = record["Snapshots"]
        read_bytes[condition].append(
            stages["AfterBatch"]["ProcessIo"]["read_bytes"]
            - stages["AfterCacheTreatment"]["ProcessIo"]["read_bytes"]
        )
assert len(seen) == 48
for i, condition in enumerate(CONDITIONS):
    values = samples[condition]
    assert len(values) == 8 and len(read_bytes[condition]) == 8
    row = rows[i]
    assert row[1] == "8/8"
    mean_us = statistics.mean(values.values())
    read_mb = statistics.mean(read_bytes[condition]) / 1e6
    rounded(row[2], mean_us)
    rounded(row[3], read_mb)
    sealed = report["perCondition"][":".join(condition)]
    assert abs(sealed["meanUs"] - mean_us) < 1e-9
    assert abs(sealed["readMBMean"] - read_mb) < 1e-9

a = samples[("polar-db", "offsets")]
b = samples[("polar-db", "hashes")]
log_ratios = [math.log(a[k] / b[k]) for k in sorted(a)]
ratio = math.exp(statistics.mean(log_ratios))
difference_us = statistics.mean(a[k] - b[k] for k in sorted(a))
rng = random.Random(20261108)
bootstrap = sorted(
    math.exp(statistics.mean(log_ratios[rng.randrange(8)] for _ in range(8)))
    for _ in range(10_000)
)
low, high = quantile(bootstrap, .025), quantile(bootstrap, .975)
effect = report["comparisons"]["primaryOffsetsVsHashes"]
assert effect["pairs"] == 8 and effect["positivePairs"] == 8
assert abs(effect["geometricMeanRatio"] - ratio) < 1e-9
assert abs(effect["absoluteDifferenceUsMean"] - difference_us) < 1e-9
assert abs(effect["bootstrap95CI"][0] - low) < 1e-9
assert abs(effect["bootstrap95CI"][1] - high) < 1e-9
assert f"{ratio:.2f}".replace(".", ",") in table_and_discussion
assert f"{low:.2f}–{high:.2f}".replace(".", ",") in table_and_discussion
assert f"{difference_us:.3f}".replace(".", ",") in table_and_discussion
print(
    "OK: Stage 4B table 10 (6 conditions x 8 blocks), archived raw samples, "
    f"paired ratio {ratio:.5f}, bootstrap 95% [{low:.5f}, {high:.5f}], "
    f"paired mean difference {difference_us:.5f} us"
)
