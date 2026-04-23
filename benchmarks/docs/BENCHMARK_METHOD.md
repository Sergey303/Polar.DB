# Benchmark Method

## Purpose

The platform keeps three layers separate:

- executor writes immutable raw runs;
- analysis builds local derived artifacts in `analyzed/` and comparison artifacts in `comparisons/`;
- charts produce human-readable summaries and the canonical experiment page `index.html`.

This separation lets us rerun analysis/reporting without re-running expensive engine workloads.

## Experiment index page

`benchmarks/experiments/<experiment>/index.html` is the main human-readable page.

- HTML generation is always on (no `generateHtml` flag).
- Main tables use scientific display for large values.
- Bytes also include practical binary units (`KiB/MiB/GiB`).
- Exact raw numeric values remain available via tooltip/title metadata.

Minimum charts on the page:

1. history chart (series/date vs elapsed median, one line per target);
2. phase breakdown chart (load/build/reopen/lookup for latest series);
3. artifact size chart (primary/side/total bytes for latest series).

The page also links machine-readable artifacts from `raw/`, `analyzed/`, and `comparisons/`.

## Stage4 comparison method

Stage4 comparison is series-based.

1. Run warmup + measured runs for one target with `--comparison-set <id>`.
2. Run warmup + measured runs for the second target with the same set id.
3. Build `comparison-series` artifact from that set.
4. Build markdown/csv report from `comparison-series`.

Why this is better than "latest run per target":

- both targets are compared inside one explicit run series;
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
- `analyzed/` = local interpretation for this experiment/target (for example `*.eval.json`, `latest-series.<target-key>.json`);
- `comparisons/` = cross-target/cross-object comparison artifacts and derived expectations (`latest-engines.json`, `latest-history.json`, `latest-other-experiments.json`, `*.comparison*.json`).

Raw filename rule:

- single run: `<timestamp>__<target-key>.run.json`
- series run: `<timestamp>__<target-key>__<role>-<seq>.run.json`

## Compare flags in manifest

`experiment.json` controls optional comparison layers:

- `compare.history.enabled` - enables `latest-history.json`;
- `compare.otherExperiments.enabled` - enables `latest-other-experiments.json`;
- `compare.otherExperiments.experiments` - list of experiment slugs for cross-experiment context.

`latest-engines.json` is generated automatically when experiment has multiple targets.

## Target runtime syntax in experiment manifest

`experiment.json` defines targets as a map:

- `"polar-db-current": { "engine": "polar-db" }` -> run current source from repository.
- `"polar-db-2.1.1": { "engine": "polar-db", "nuget": "2.1.1" }` -> run pinned Polar.DB NuGet version.
- `"sqlite": { "engine": "sqlite" }` -> run SQLite latest NuGet version.
- `"sqlite": { "engine": "sqlite", "nuget": "X.Y.Z" }` -> run pinned SQLite NuGet version.

If manifest contains multiple targets, pass `--engine <target-key>`.

## Typical commands

Single raw run (Polar.DB current source):

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine polar-db-current --spec benchmarks/experiments/persons-load-build-reopen-random-lookup --work benchmarks/work/polar-single --warmup-count 0 --measured-count 1
```

Single raw run (SQLite):

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine sqlite --spec benchmarks/experiments/persons-load-build-reopen-random-lookup --work benchmarks/work/sqlite-single --warmup-count 0 --measured-count 1
```

Series run with one comparison set:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine polar-db-current --spec benchmarks/experiments/persons-append-cycles-reopen-lookup --work benchmarks/work/polar-series --comparison-set stage4-append-001
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

This command also regenerates `benchmarks/experiments/persons-append-cycles-reopen-lookup/index.html`.
