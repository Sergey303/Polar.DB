# Benchmark Method

## Purpose

The platform keeps three layers separate:

- executor writes immutable raw runs;
- analysis builds derived artifacts (policy checks and comparisons);
- charts produce human-readable summaries.

This separation lets us rerun analysis/reporting without re-running expensive engine workloads.

## Stage4 comparison method

Stage4 comparison is series-based.

1. Run warmup + measured runs for one engine with `--comparison-set <id>`.
2. Run warmup + measured runs for the second engine with the same set id.
3. Build `comparison-series` artifact from that set.
4. Build markdown/csv report from `comparison-series`.

Why this is better than "latest run per engine":

- both engines are compared inside one explicit run series;
- measured statistics (`min/max/avg/median`) are calculated from multiple runs;
- warmup runs are stored but excluded from aggregation.

## Warmup vs measured

- Warmup run: stabilizes runtime state and storage caches, not used in final stats.
- Measured run: used for aggregation and final comparison.

Default behavior:

- with `--comparison-set`: `warmup=1`, `measured=3`;
- without `--comparison-set`: legacy single run (`warmup=0`, `measured=1`).

## Typical commands

Single raw run (Polar.DB):

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine polar-db --spec benchmarks/experiments/persons-load-build-reopen-random-lookup.polar-db.json --work benchmarks/work/polar-single --raw-out benchmarks/results/raw --warmup-count 0 --measured-count 1
```

Single raw run (SQLite):

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine sqlite --spec benchmarks/experiments/persons-load-build-reopen-random-lookup.sqlite.json --work benchmarks/work/sqlite-single --raw-out benchmarks/results/raw --warmup-count 0 --measured-count 1
```

Series run with one comparison set:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine polar-db --spec benchmarks/experiments/persons-append-cycles-reopen-lookup.polar-db.json --work benchmarks/work/polar-series --raw-out benchmarks/results/raw --comparison-set stage4-append-001
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine sqlite --spec benchmarks/experiments/persons-append-cycles-reopen-lookup.sqlite.json --work benchmarks/work/sqlite-series --raw-out benchmarks/results/raw --comparison-set stage4-append-001
```

Build aggregated comparison:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Analysis -- --raw-dir benchmarks/results/raw --compare-experiment persons-append-cycles-reopen-lookup --compare-set stage4-append-001 --comparison-out benchmarks/results/comparisons
```

Build markdown/csv report:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Charts -- --comparisons benchmarks/results/comparisons --reports-out benchmarks/reports
```
