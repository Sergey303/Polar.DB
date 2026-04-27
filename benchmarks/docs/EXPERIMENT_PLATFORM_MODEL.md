# Experiment Platform Model

## Overview

The benchmark platform follows a strict pipeline:

```
experiment.json → targets → raw facts → analysis → report
```

Each stage is independent and produces immutable artifacts. Later stages never rewrite earlier artifacts.

## Core Concepts

### Experiment

An experiment is a **folder** containing an `experiment.json` manifest. The manifest describes:

- `experiment` — unique key (folder name)
- `title`, `research`, `hypothesis`, `description` — metadata
- `dataset` — data profile and record count
- `workload` — scenario type and parameters
- `targets` — runtime variants to compare
- `fault`, `fairness`, `requires` — constraints
- `schema`, `protocol`, `runs` — platform-level configuration (see below)
- `compare` — comparison artifact settings

### Target

A **target** is a specific runtime variant of an engine family. Examples:

- `polar-db-current` — current source build
- `polar-db-2.1.0` — pinned NuGet 2.1.0
- `polar-db-2.1.1` — pinned NuGet 2.1.1
- `sqlite` — SQLite adapter

Each target has an `engine` field (family) and optional `nuget` field (pinned version).

### Runner / Adapter

A runner **only executes** the target and writes raw facts. It **never**:

- Makes conclusions or comparisons
- Computes statistics (p95, MAD, trimmed mean)
- Decides if a result is "good" or "bad"

Runners produce `RunResult` — immutable raw facts stored as JSON files.

### Raw Facts (RunResult)

Raw results are **immutable**. Once written, they are never modified. Each raw file contains:

- Run ID, timestamp, engine key, experiment key
- Environment manifest
- Technical success flag + optional error
- Semantic success flag + optional error
- Measured metrics (elapsed ms, artifact sizes, etc.)
- Artifact inventory (file sizes, roles)
- Engine diagnostics and tags

### Analysis

Analysis is **recomputable**. It reads raw facts and produces derived artifacts (aggregates, comparisons). If raw facts are unchanged, analysis output should be deterministic.

### Charts / Report

Charts and reports are **derived** from analysis artifacts. They are the final human-readable output.

## Schema, Protocol, Runs

### `schema` (string?)

Schema version identifier, e.g. `"polar-bench-experiment/v1"`. Null/absent is allowed for backward compatibility with older experiment.json files.

### `protocol` (string?)

Protocol identifier describing the benchmark protocol, e.g. `"polar-db-reference-load-build-random-lookup/v1"`. Null/absent is allowed for backward compatibility.

### `runs` (ExperimentRunsSpec)

Controls how many warmup and measured iterations are executed:

```json
"runs": {
  "warmup": 5,
  "measured": 50,
  "notes": "Scientific run profile"
}
```

**Priority order** (highest to lowest):

1. CLI `--warmup-count` / `--measured-count` — always wins
2. `manifest.runs.warmup` / `manifest.runs.measured` — from experiment.json
3. ExecApplication defaults:
   - With comparison set: warmup=1, measured=3
   - Without comparison set: warmup=0, measured=1

Validation:
- `measured` must be >= 1
- `warmup` must be >= 0
- Invalid values throw `InvalidOperationException`

## Status Taxonomy

| Status | Meaning |
|---|---|
| `Success` | Technical + semantic checks passed |
| `NotSupported` | Engine cannot run this workload (not a failure) |
| `TechnicalFailed` | Process crash, timeout, infrastructure error |
| `SemanticFailed` | Wrong results, data corruption, regression |

**Key distinctions:**
- `NotSupported` ≠ `TechnicalFailed` — an engine that cannot run a scenario is not broken
- `TechnicalFailed` ≠ `SemanticFailed` — a run that completes but returns wrong data is a different class of problem

## CLI Overrides

All CLI flags override manifest values:

```
--exp <experiment-dir>
--warmup-count <n>
--measured-count <n>
--comparison-set <id>
--env <class>
```

Example: quick local smoke test with 0 warmup, 1 measured:

```
dotnet run --project ...\Polar.DB.Bench.Exec.csproj -- --exp ...\experiment-folder --warmup-count 0 --measured-count 1
```

## Final Metric Set

The platform retains the following derived metrics for analysis and reporting.

### Reliability

| Metric | Description |
|--------|-------------|
| `technicalSuccessRate` | Fraction of runs where infrastructure completed without error |
| `semanticSuccessRate` | Fraction of runs where workload-level checks passed |

### Timing

| Metric | Description |
|--------|-------------|
| `min` | Minimum observed value |
| `average` | Arithmetic mean |
| `p50` / `median` | 50th percentile (median) |
| `p95` | 95th percentile |
| `p99` | 99th percentile |
| `trimmedMean10` | Average after removing lowest and highest 10% |
| `MAD` | Median absolute deviation from the median |
| `jitterRatio` | (p95 - p50) / p50 |
| `outlierCount` | Values with robust z-score > 3.5 |

### Throughput

| Metric | Description |
|--------|-------------|
| `recordsPerSecond` | Records processed per second |
| `queriesPerSecond` | Queries executed per second |

### Search

| Metric | Description |
|--------|-------------|
| `msPerQuery` | Average milliseconds per query |
| `resultRowsTotal` | Total rows returned |
| `msPerReturnedRow` | Milliseconds per returned row |
| `hitRate` | Fraction of queries that found matches |
| `emptyRate` | Fraction of queries that returned no results |

### Storage

| Metric | Description |
|--------|-------------|
| `totalArtifactBytes` | Total bytes of all recorded artifacts |
| `bytesPerRecord` | Total bytes divided by record count |
| `indexBytesPerRecord` | Index/overhead bytes per record |

### Comparison

| Metric | Description |
|--------|-------------|
| `speedup vs baseline` | Ratio of baseline time to target time |
| `slowdown vs baseline` | Ratio of target time to baseline time |

### Generic Metrics Dictionary

All raw metric keys that are not exposed as fixed properties (ElapsedMs, LoadMs, etc.)
are propagated through the Analysis pipeline into a generic `Metrics` dictionary
(`IReadOnlyDictionary<string, MetricSeriesStats>`) on both:

- `CrossEngineSeriesEngineEntry` — per-target aggregated stats in comparison artifacts
- `LocalAnalyzedSeriesResult` — per-target stats in local analyzed artifacts

This dictionary is serialized as `"metrics"` in JSON and is the primary mechanism for
extending the platform with new metric types (search, memory, cache, etc.) without
modifying the core model classes.

### Report Integration

The Charts renderer (`Polar.DB.Bench.Charts.Runtime`) uses `MetricReportCatalog` to display
known metrics in thematic HTML sections:

| Section | Metrics |
|---------|---------|
| Executive Summary | ElapsedMs, TotalArtifactBytes, LookupMs |
| Correctness / Status | technicalSuccessRate, semanticSuccessRate, wrong rows |
| Timing & Stability | ElapsedMs, LoadMs, BuildMs, ReopenMs, LookupMs |
| Search: Point Lookup | search.point.* timing, hit rate, throughput |
| Search: Missing Point Lookup | search.point.misses, search.point.emptyRate |
| Search: Scan / Filter | search.scan.* timing, rows scanned/matched |
| Storage Economics | TotalArtifactBytes, PrimaryArtifactBytes, SideArtifactBytes, bytesPerRecord |
| Memory / Runtime | memory.peakBytes, memory.workingSetBytes, gc*, runtime.* |
| All Metrics Appendix | All metrics not covered by known sections (collapsed by default) |

The MD/CSV report renderers include a **Generic Metrics** section listing all metrics
from the `Metrics` dictionary.

### Future Cache (reserved)

| Metric | Description |
|--------|-------------|
| `cold/warm/hot` | Query time under different cache states |
| `cacheBenefit` | coldMedian / warmMedian |
| `cachePaybackQueries` | cacheBuildCostMs / (coldMsPerQuery - warmMsPerQuery) |
| `cacheHitRate` | Fraction of cache hits |


