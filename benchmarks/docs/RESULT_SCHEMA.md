# Result Schema

## 1) Raw run (`*.run.json`)

Raw run is immutable factual execution data from one executor launch.
Raw files are stored under `benchmarks/experiments/<experiment>/raw/`.
Filename rule:

- single run: `<timestamp>__<engine>.run.json`;
- series run: `<timestamp>__<engine>__<role>-<seq>.run.json`.

Main groups:

- run identity (`run`, `at`, `engine`, `experiment`, `dataset`, `fairness`, optional `runtime`);
- stage4 series identity:
  - `set` (optional),
  - `seq` (optional),
  - `role` (`warmup` or `measured`, optional for old runs);
- technical result (`technical`, optional `technicalError`);
- semantic result (`semantic`, optional `semanticError`);
- measured metrics;
- artifact inventory;
- diagnostics and notes.

Engine runtime semantics in raw runs:

- `engine: "polar-db"` + no `nuget` in experiment spec -> `runtime.source = "source-current"`;
- `engine: "polar-db"` + `nuget: "X.Y.Z"` -> `runtime.source = "nuget-pinned"`, `runtime.nuget = "X.Y.Z"`;
- non-Polar engine without `nuget` -> `runtime.source = "nuget-latest"`;
- non-Polar engine with `nuget` -> `runtime.source = "nuget-pinned"`, `runtime.nuget = "X.Y.Z"`.

For imported `persons-load-build-reopen-random-lookup` workload, raw metrics also include:

- `directPointLookupMs`, `directPointLookupKey`, `directPointLookupHit`;
- `randomPointLookupCount` (normalized reference batch size, typically `10_000`).

Backward compatibility:

- old raw runs without stage4 fields remain valid and readable.

## 2) Analyzed result (`*.eval.json`)

Analyzed artifacts are local interpretation outputs for one experiment.
They are stored under `benchmarks/experiments/<experiment>/analyzed/`.

Examples:

- per-run policy interpretation: `*.eval.json`;
- latest local engine series snapshot: `latest-series.polar-db.json`, `latest-series.sqlite.json`.

Analyzed artifacts do not contain cross-engine comparison payloads.

- policy id;
- baseline id;
- check list and overall status;
- derived notes/metrics.

No raw facts are rewritten.

## 3) Comparisons folder (`comparisons/`)

`comparisons/` is the only place for comparison artifacts and derived expectations.

### `latest-engines.json`

- compares latest successful measured series per engine inside current experiment;
- generated automatically when experiment has multiple engines.

### `latest-history.json`

- compares successful measured series of the same experiment over time;
- generated only when `experiment.json -> compare.history.enabled` is `true`.

### `latest-other-experiments.json`

- compares current experiment with configured external experiment snapshots for context;
- generated only when `experiment.json -> compare.otherExperiments.enabled` is `true`;
- target experiments come from `compare.otherExperiments.experiments`.

### Legacy comparison (`*.comparison.json`)

Legacy single-run comparison artifact from stage3.
Comparison artifacts are stored under `benchmarks/experiments/<experiment>/comparisons/`.

- one selected run per engine;
- common comparable metrics;
- no policy decisions.

Still supported as fallback for old raw data without comparison sets.

## 4) Stage4 aggregated comparison (`*.comparison-series.json`)

Comparison-series artifact is an analysis-layer derivative built from one comparison set id (`set`).

Contains:

- experiment key;
- dataset profile;
- fairness profile;
- `set`;
- environment class;
- engine list;
- per-engine aggregated stats (measured runs only):
  - run counts (measured/warmup),
  - `count/min/max/average/median` for:
    - elapsed,
    - load,
    - build,
    - reopen,
    - lookup,
    - lookup batch count,
    - total artifact bytes,
    - primary bytes,
    - side bytes,
  - technical success count,
  - semantic success count.

Missing metric handling:

- stats include `missing`;
- aggregation skips missing values instead of silently substituting fake numbers.

## 5) Human-readable experiment page (`index.html`)

Each experiment folder contains one canonical HTML page:

- `benchmarks/experiments/<experiment>/index.html`

The page is derived from:

- `experiment.json` identity;
- latest local analyzed artifacts from `analyzed/`;
- latest comparison artifacts from `comparisons/`;
- artifact file lists from `raw/`, `analyzed/`, and `comparisons/`.

Formatting rules used in the page:

- large numeric values are shown in scientific form in main cells;
- bytes also include binary units (`KiB/MiB/GiB`);
- exact raw values are preserved in tooltip/title details.

The page includes three baseline charts:

1. history (`elapsed median` by series and engine);
2. phase breakdown (load/build/reopen/lookup for latest series);
3. artifact sizes (primary/side/total bytes for latest series).
