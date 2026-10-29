#!/usr/bin/env python3
"""Bootstrap paired independent-run ratios; never treat within-run queries as independent runs."""
import argparse
import csv
import json
import math
from pathlib import Path
import random
import statistics as stats


def observations(raw):
    """Return per-run median samples keyed by (row count, phase, metric)."""
    groups = {}
    for run in raw.get("lifecycleRuns") or []:
        for engine in run["engines"]:
            for field, label in (("samplesMs", engine["metric"]),
                                 ("openSamplesMs", "open-only")):
                if engine.get(field):
                    key = (run["setupRows"], "", label)
                    groups.setdefault(key, {})[engine["engine"]] = stats.median(engine[field])
    for run in raw.get("lookupRuns") or []:
        for phase in run["phases"]:
            for engine in phase["engines"]:
                for field, label in (("batchAvgSamplesMs", "lookup-batch-average"),
                                     ("latencySamplesMs", "lookup-latency")):
                    if engine.get(field):
                        key = (run["setupRows"], phase["name"], label)
                        groups.setdefault(key, {})[engine["engine"]] = stats.median(engine[field])
    paired = {}
    for key, values in groups.items():
        polar = values.get("polar-db-current") or values.get("polar-db")
        sqlite = values.get("sqlite")
        if polar is None or sqlite is None or polar <= 0 or sqlite <= 0:
            raise ValueError(f"Missing/invalid engine timing for {key}: {values}")
        paired[key] = (polar, sqlite)
    if not paired:
        raise ValueError("No supported metrics in raw file")
    return paired


def analyze(series_path, seed=20261029, resamples=10000):
    series = json.loads(series_path.read_text(encoding="utf-8"))
    runs = series["runs"]
    if len(runs) < 8 or len(runs) % 4:
        raise ValueError("Require at least eight complete runs, in balanced blocks of four")
    if len({run["runId"] for run in runs}) != len(runs):
        raise ValueError("Duplicate run IDs")
    root = series_path.resolve().parents[3]  # .../benchmarks/results/independent/series.json
    groups = {}
    reference = None
    schedule = ("sqlite-first", "polar-first", "polar-first", "sqlite-first")
    for i, run in enumerate(runs):
        if run["order"] != schedule[i % 4]:
            raise ValueError("Not ABBA counterbalanced at run " + str(i + 1))
        path = (root / run["raw"]).resolve()
        if not path.is_relative_to(root.resolve()):
            raise ValueError("Raw data outside repository")
        raw = json.loads(path.read_text(encoding="utf-8"))
        manifest = raw["manifest"]
        expected_order = (["sqlite", "polar-db"] if run["order"] == "sqlite-first"
                          else ["polar-db", "sqlite"])
        if manifest["runId"] != run["runId"] or manifest["engineOrder"] != expected_order:
            raise ValueError("Run ID or engine order mismatch")
        env = manifest["coordinator"]
        signature = (env["commitSha"], env["cpuDescription"], env["osDescription"],
                     env["runtimeVersion"], env["serverGc"], env["buildConfiguration"],
                     env["sqliteNativeVersion"], tuple(raw["options"]["rowCounts"]),
                     raw["options"]["kind"])
        if not env["publicationReady"] or env["gitDirty"] or not env["gitStatusKnown"]:
            raise ValueError("Non-publication or dirty run: " + run["runId"])
        if env["commitSha"] != series["commit"] or (reference and signature != reference):
            raise ValueError("Build, machine, input, or version differs between runs")
        for worker in manifest["engineProcesses"]:
            if worker["commitSha"] != env["commitSha"] or not worker["publicationReady"]:
                raise ValueError("Worker settings inconsistent")
        reference = signature
        found = observations(raw)
        if groups and set(found) != set(groups):
            raise ValueError("Different metric/phase/row-count set across runs")
        for key, values in found.items():
            groups.setdefault(key, []).append(values)
    rng = random.Random(seed)
    result = []
    for (rows, phase, metric), values in sorted(groups.items()):
        logs = [math.log(sqlite / polar) for polar, sqlite in values]
        center = stats.median(logs)
        boot = sorted(stats.median(rng.choices(logs, k=len(logs))) for _ in range(resamples))
        low = math.exp(boot[int(0.025 * (resamples - 1))])
        high = math.exp(boot[int(0.975 * (resamples - 1))])
        result.append({"rows": rows, "phase": phase, "metric": metric,
                       "independentRuns": len(values), "polarMedianMs": stats.median(v[0] for v in values),
                       "sqliteMedianMs": stats.median(v[1] for v in values),
                       "medianPairedRatioSqliteOverPolar": math.exp(center),
                       "bootstrap95Low": low, "bootstrap95High": high,
                       "interpretation": "exploratory" if len(values) < 12 else "replicated"})
    return result


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("series", type=Path, help="Series manifest produced by run-independent-series.py")
    p.add_argument("--output", type=Path, help="Optional CSV destination; default stdout")
    p.add_argument("--resamples", type=int, default=10000)
    args = p.parse_args()
    if args.resamples < 1000:
        p.error("Use >= 1000 bootstrap resamples")
    rows = analyze(args.series, resamples=args.resamples)
    cols = list(rows[0])
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        f = args.output.open("w", encoding="utf-8", newline="")
    else:
        import sys
        f = sys.stdout
    try:
        writer = csv.DictWriter(f, fieldnames=cols)
        writer.writeheader()
        writer.writerows(rows)
    finally:
        if args.output:
            f.close()


if __name__ == "__main__":
    main()
