# Native Guid lookup — 19 October 2026

The native Guid series was executed on product commit
[`addd9203897d3d06375542937f66c96ed8fca813`](https://github.com/Sergey303/Polar.DB/commit/addd9203897d3d06375542937f66c96ed8fca813).
Raw JSON/CSV, HTML, environment manifests and separate worker results are in
[GitHub Actions run 39234860884](https://github.com/Sergey303/Polar.DB/actions/runs/39234860884).

## Setup and interpretation

- Ubuntu 24.04.5 LTS, AMD EPYC 7763, four logical processors exposed to the runner.
- .NET 10.0.13, SQLite managed 9.0.4; Release build, optimizations enabled.
- Polar.DB and SQLite executed in isolated child processes, alternated by the coordinator.
- 50,000 and 5,000,000 deterministic rows; each size ran independently.
- The timings below are the median of **batch-average milliseconds per lookup**,
  converted to microseconds for primary lookup. They are not p95 latencies.
- The primary workload has 100 queries/batch. The 5M external workload has
  four queries/batch, returning 5,000 rows/query (300,000 rows over 60 queries).
- The "cold" phase means no explicit warmup, not a cleared OS page cache.
  The hot phase warms files and the lookup path.
- Raw manifests and samples are tied to the same commit. Historical pre-native
  Guid benchmark series must **not** be merged with these results.

## Primary Guid lookup — median batch-average (microseconds/query)

| Rows | Phase | Polar.DB | SQLite |
|---:|---|---:|---:|
| 50,000 | No explicit warmup | 4.296 | 13.418 |
| 50,000 | Warmed | 4.591 | 13.719 |
| 5,000,000 | No explicit warmup | 5.700 | 14.620 |
| 5,000,000 | Warmed | 5.560 | 16.570 |

## External Guid lookup — median batch-average (milliseconds/query)

| Rows | Phase | Polar.DB | SQLite |
|---:|---|---:|---:|
| 50,000 | No explicit warmup | 0.212449 | 0.193737 |
| 50,000 | Warmed | 0.130950 | 0.113415 |
| 5,000,000 | No explicit warmup | 14.36005 | 19.29417 |
| 5,000,000 | Warmed | 14.08521 | 17.42621 |

At 50k, Polar.DB is faster on primary Guid lookup, but SQLite is faster for
external Guid lookup. At 5M Polar.DB is faster in both measured categories.
The external case materializes multiple rows per query; timings therefore include
record access and result materialization, not just index traversal.

## Limits and next reproducibility steps

This is a **single GitHub-hosted Linux machine/series**, with repeated samples
inside each run rather than independent environment replications. It does not
measure an OS effect, a before/after native Guid speedup on identical versions,
or causal contributions of the individual serialization and indexing changes.
Repeat on Windows and independent runners before drawing stronger conclusions.

GitHub Actions artifacts are retained for 30 days in this workflow. The
machine-readable results and manifest should be exported to a durable archive
before retention expires if long-term publication is intended.
