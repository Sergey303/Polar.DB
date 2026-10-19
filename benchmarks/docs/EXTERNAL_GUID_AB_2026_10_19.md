# External Guid lookup — controlled comparison

**Date:** 2026-10-19

- Run: [GitHub Actions 39270957072](https://github.com/Sergey303/Polar.DB/actions/runs/39270957072).
- Baseline: `ff43f868c229515752c443de7e2c7af1a8c0b1ba`.
- Candidate: `03f52b4381e13a9dda4a019f3e6ba27ef5acfb64`.
- Candidate removes empty dynamic-index array copying and per-record LINQ
  predicate/iterator allocations; it does not change persisted index formats.
- Each workload runs **A–B–B–A** on the same Ubuntu runner, with the same
  dataset, release build, engine order and benchmark settings.
- Each measurement below is the **mean of two medians of batch-average
  milliseconds per query**. Lower is better.
- 50k and 5M are separate runner jobs. Do not compare their absolute latency
  as a same-hardware scaling test.
- The "cold" phase has no explicit warmup, but does **not** flush the OS
  page cache. These are exploratory paired observations, not confidence bounds.

## External Guid lookup: Polar.DB

| Rows | Phase | Baseline ms/query | Candidate ms/query | Change |
|---:|---|---:|---:|---:|
| 50,000 | No explicit warmup | 0.211143 | 0.210122 | -0.48% |
| 50,000 | Warmed | 0.129082 | 0.127571 | -1.17% |
| 5,000,000 | No explicit warmup | 8.976962 | 8.937225 | -0.44% |
| 5,000,000 | Warmed | 8.542969 | 8.567106 | +0.28% |

## Same-runner SQLite control

| Rows | Phase | Baseline ms/query | Candidate ms/query | Change |
|---:|---|---:|---:|---:|
| 50,000 | No explicit warmup | 0.192669 | 0.191664 | -0.52% |
| 50,000 | Warmed | 0.112280 | 0.110992 | -1.15% |
| 5,000,000 | No explicit warmup | 14.930737 | 14.695588 | -1.57% |
| 5,000,000 | Warmed | 11.635656 | 11.374806 | -2.24% |

At 50k the change in SQLite (whose code did not change) closely tracks
Polar.DB. At 5M Polar.DB shows no consistent improvement. **No convincing
lookup-latency speedup has been established.** The simpler allocation path can
remain, provided functional tests stay green, but it should not be advertised
as a performance win.

The controlled run archives all four CSV and environment manifests per job,
with a 30-day GitHub artifact retention period. Do not merge these timings
with the older single-run series as if they were identical experiments.

## Next performance investigation (before further optimization)

1. Measure the cost of `FindFirstEqual` separately from record retrieval.
2. Instrument or independently isolate `GetByOffset`, deserialization and
   `IsOriginalAndNotEmpty` primary-index validity checks per returned row.
3. Measure `HasKey` revalidation and caller-side `PolarRows.FromPolar` plus
   `BenchmarkRowAccumulator` independently.
4. Examine physical-offset locality within equal-key groups. Do **not**
   change result ordering or persisted indexes before proving both improved
   performance and backward-compatible semantics.
5. For any new optimization, repeat an A–B–B–A on the same runner with
   identical data, ensure no missing or duplicate rows, and rerun recovery
   and native/legacy Guid compatibility tests.
