# Benchmark Method

## Purpose

The platform keeps three layers separate:

- executor writes immutable raw runs;
- analysis builds local derived artifacts in `analyzed/` and comparison artifacts in `comparisons/`;
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

## Experiment storage model

One experiment now lives in one folder and has one canonical manifest:

- `benchmarks/experiments/<experiment-slug>/experiment.json`
- `benchmarks/experiments/<experiment-slug>/raw/`
- `benchmarks/experiments/<experiment-slug>/analyzed/`
- `benchmarks/experiments/<experiment-slug>/comparisons/`
- `benchmarks/experiments/<experiment-slug>/index.html`

`Bench.Exec --spec` accepts either the experiment folder or direct path to `experiment.json`.
If `--raw-out` is omitted, executor writes to `<experiment>/raw` automatically.

Roles:

- `raw/` = immutable run facts (`*.run.json`) from executor only;
- `analyzed/` = local interpretation for this experiment/engine (for example `*.eval.json`, `latest-series.<engine>.json`);
- `comparisons/` = cross-engine/cross-object comparison artifacts and derived expectations (`latest-engines.json`, `latest-history.json`, `latest-other-experiments.json`, `*.comparison*.json`).

Raw filename rule:

- single run: `<timestamp>__<engine>.run.json`
- series run: `<timestamp>__<engine>__<role>-<seq>.run.json`

## Compare flags in manifest

`experiment.json` controls optional comparison layers:

- `compare.history.enabled` - enables `latest-history.json`;
- `compare.otherExperiments.enabled` - enables `latest-other-experiments.json`;
- `compare.otherExperiments.experiments` - list of experiment slugs for cross-experiment context.

`latest-engines.json` is generated automatically when experiment has multiple engines.

## Engine runtime syntax in experiment manifest

`experiment.json` defines engines as a map:

- `"polar-db": {}` -> run current source from repository.
- `"polar-db": { "nuget": "2.1.1" }` -> run pinned Polar.DB NuGet version.
- `"sqlite": {}` -> run SQLite latest NuGet version.
- `"sqlite": { "nuget": "X.Y.Z" }` -> run pinned SQLite NuGet version.

If manifest contains multiple engines, pass `--engine <key>`.

## Typical commands

Single raw run (Polar.DB):

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine polar-db --spec benchmarks/experiments/persons-load-build-reopen-random-lookup --work benchmarks/work/polar-single --warmup-count 0 --measured-count 1
```

Single raw run (SQLite):

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine sqlite --spec benchmarks/experiments/persons-load-build-reopen-random-lookup --work benchmarks/work/sqlite-single --warmup-count 0 --measured-count 1
```

Series run with one comparison set:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine polar-db --spec benchmarks/experiments/persons-append-cycles-reopen-lookup --work benchmarks/work/polar-series --comparison-set stage4-append-001
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine sqlite --spec benchmarks/experiments/persons-append-cycles-reopen-lookup --work benchmarks/work/sqlite-series --comparison-set stage4-append-001
```

Build aggregated comparison:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Analysis -- --raw-dir benchmarks/experiments/persons-append-cycles-reopen-lookup --compare-experiment persons-append-cycles-reopen-lookup --compare-set stage4-append-001
```

Build markdown/csv report:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Charts -- --comparisons benchmarks/experiments/persons-append-cycles-reopen-lookup/comparisons --reports-out benchmarks/experiments/persons-append-cycles-reopen-lookup
```
