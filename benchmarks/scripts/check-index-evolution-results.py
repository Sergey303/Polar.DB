#!/usr/bin/env python3
"""Check SHA-256, all historical index samples, and medians in the article."""
from pathlib import Path
from statistics import median
import csv
import hashlib

root = Path(__file__).resolve().parents[1]
source = root / "evidence/20261108-index-evolution/results.csv"
expected_sha = "1d709d1021f2edd4ca3fa0e243fd198b4e6ce3d5c1a1cfd16c9d6fe67d5f9ee4"
assert hashlib.sha256(source.read_bytes()).hexdigest() == expected_sha
records = list(csv.DictReader(source.read_text(encoding="utf-8").splitlines()))
assert len(records) == 12
sha = {"old": "39cefe13db8697cf5f8e15edaa4677ba52a2cdea",
       "new": "09ec0703f476f25ec79baff338843df8a8e83b35"}
sums = {50000: "24130409", 1000000: "482617660"}
medians = {
    (50000, "old"): (45.894, 8.197),
    (50000, "new"): (52.063, 3.406),
    (1000000, "old"): (366.297, 15.774),
    (1000000, "new"): (555.167, 6.537),
}
seen = set()
for row in records:
    n, engine, block = int(row["rows"]), row["engine"], int(row["block"])
    assert n in sums and engine in sha and block in (1, 2, 3)
    assert (n, engine, block) not in seen
    seen.add((n, engine, block))
    assert row["sha"] == sha[engine]
    assert (row["probes"], row["seed"], row["verified"]) == ("1000", "777", "true")
    assert row["checksum"] == sums[n]
    assert all(float(row[x]) > 0 for x in ("load_ms", "prerequisite_ms", "index_build_ms", "lookup_ms"))
assert len(seen) == 12
article = (root / "docs/POLAR_DB_ARTICLE_DRAFT.md").read_text(encoding="utf-8")
section = article.split("### 4.8. ", 1)[1].split("### 4.9. ", 1)[0]
for (n, engine), expected in medians.items():
    sample = [r for r in records if int(r["rows"]) == n and r["engine"] == engine]
    assert len(sample) == 3
    actual = tuple(round(median(float(r[m]) for r in sample), 3)
                   for m in ("index_build_ms", "lookup_ms"))
    assert actual == expected
    assert all(f"{x:.3f}".replace(".", ",") in section for x in actual)
    print(f"PASS n={n}, engine={engine}: build={actual[0]}ms, lookup={actual[1]}ms")
print("PASS: all 12 samples, all eight manuscript medians, and CSV SHA256")
