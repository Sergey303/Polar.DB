# Benchmark reports and reproducibility

Benchmark executables reference the shared `benchmarks/src/BenchSupport/BenchSupport.csproj`
library. Common benchmark sources are compiled once instead of being linked separately into
every executable.

Each normal benchmark launch is a coordinator. It starts SQLite and Polar.DB in separate
child processes, waits for both workers, validates compatible result shapes and build
settings, and then creates the combined report.

Generated latest-result artifacts:

- `<experiment>.html` — derived report;
- `<experiment>.manifest.json` — environment, Git commit, build configuration and worker process metadata;
- `<experiment>.raw.json` — combined structured raw result;
- `<experiment>.raw.csv` — one row per measured sample.

Immutable worker and combined artifacts are stored below:

`benchmarks/results/raw/<experiment>/<run-id>/`

A full series can be started from the repository root:

```powershell
pwsh -File .\benchmarks\scripts\run-new-benchmarks.ps1
```

The script assigns one `POLAR_BENCH_RUN_ID` to the whole series. Engine order is
alternated deterministically by default. Set `POLAR_BENCH_ENGINE_ORDER` to
`sqlite-first` or `polar-first` only when a fixed order is required.

For the next primary-offset experiment, see [`PRIMARY_OFFSET_RESIDENCY_PLAN.md`](PRIMARY_OFFSET_RESIDENCY_PLAN.md). It records the planned Windows/Linux comparison of the current FileStream path, `System.IO.RandomAccess`, memory-mapped access and, only if needed, a bounded managed page cache.

The lifecycle suite also contains focused scenarios used to isolate current storage work:

- `BuildPrimaryIntOnly` — fixed Int32 primary-index build, including Load/Build allocations and GC counts;
- `BuildExternalIndexesOnly` — build and flush of the four typed external indexes over prepared primary storage;
- `TraversalOnly` — full logical row traversal without setup/reopen time in the measured interval;
- `ReopenWithTail` — fixed-Int32 query-ready reopen with 10,000 rows beyond the persisted primary snapshot, isolating tail replay from variable-record recovery scanning.

For lifecycle smoke runs, row counts and operation counts can be overridden:

```powershell
dotnet run --project .\benchmarks\src\AppendOnly\AppendOnly.csproj -- `
  --rows=1000 --warmup=10 --samples=100
```

`--warmup` and `--samples` override lifecycle operation counts. Lookup workloads
use explicit per-phase plans from `BenchmarkDefaults`. Each lookup phase in raw JSON records:

- whether files were explicitly warmed;
- the number of warmup queries;
- measured batch count;
- queries per batch;
- total batch queries;
- latency sample count;
- total measured queries.

Lookup batch and latency key sets have independent expected row counts and checksums.
Both are validated and displayed separately in HTML.

## Publication runs

The shared benchmark library embeds the MSBuild configuration in its assembly metadata.
Every coordinator and worker manifest records:

- `buildConfiguration`;
- whether the build is detected as Debug;
- whether JIT optimizations are disabled;
- whether the run is `publicationReady`;
- explicit `DOTNET_` or `COMPlus_` overrides for tiered compilation, tiered PGO and ReadyToRun.

A run is publication-ready only when the library is an optimized `Release` build. Debug or
otherwise non-optimized runs remain usable for smoke testing, but the console and HTML report
show a prominent `NON-PUBLICATION RUN` warning.

## Metric wording

Lookup HTML reports use explicit names:

- `Batches count` is the number of measured batches.
- `Queries/batch` is the number of lookup requests inside one measured batch.
- `Total queries = Batches count * Queries/batch`.
- `Returned rows` is the total number of materialized rows returned by all lookup requests.
- `Returned rows/query = Returned rows / Total queries`.

Raw CSV uses a generic `value` column plus an explicit `unit` column. Timing rows use
`unit=ms`; managed-allocation rows use `unit=bytes`; GC collection deltas use
`unit=count`. For `lookup-batch-average` rows, `value` is the average milliseconds per
query inside the measured batch and `batch_size` is the actual number of queries used to
produce that sample. For single-query latency rows, `batch_size` is `1`.

Timing tables include `Rows/sec by trimmed` next to trimmed timing columns.
For latency tables this is calculated from trimmed single-query latency and the
average number of rows returned by a latency query.

Lifecycle reports distinguish:

- `open-only` from `query-ready reopen`;
- `volatile mutation` from a batch containing a persistence boundary;
- per-operation raw mutation samples from per-batch durable averages;
- Load/Build timing from process-wide managed allocated-byte and Gen0/1/2 collection deltas.

For build and query scenarios, resource snapshots are captured while the built/query-ready
store is still alive when applicable. This makes retained index memory visible instead of
measuring only the process after the store has been closed.

Green cells mark winners for comparable metrics. Lower is better for timings and
memory sizes. Higher is better for rows/sec and available RAM.
