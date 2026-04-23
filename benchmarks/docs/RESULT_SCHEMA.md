# Result Schema

## 1) Raw run (`*.run.json`)

Raw run is immutable factual execution data from one executor launch.

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

Analyzed result references one raw run and adds policy/baseline evaluation.

- policy id;
- baseline id;
- check list and overall status;
- derived notes/metrics.

No raw facts are rewritten.

## 3) Legacy comparison (`*.comparison.json`)

Legacy single-run comparison artifact from stage3.

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
