# Result Schema

## 1) Raw run (`*.run.json`)

Raw run is immutable factual execution data from one executor launch.

Main groups:

- run identity (`runId`, timestamp, engine, experiment, dataset, fairness);
- stage4 series identity:
  - `comparisonSetId` (optional),
  - `runSeriesSequenceNumber` (optional),
  - `runRole` (`warmup` or `measured`, optional for old runs);
- technical result (`technicalSuccess`, optional failure reason);
- semantic result (`semanticSuccess`, optional failure reason);
- measured metrics;
- artifact inventory;
- engine diagnostics and notes.

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

Comparison-series artifact is an analysis-layer derivative built from one `comparisonSetId`.

Contains:

- experiment key;
- dataset profile;
- fairness profile;
- `comparisonSetId`;
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

- stats include `missingCount`;
- aggregation skips missing values instead of silently substituting fake numbers.
