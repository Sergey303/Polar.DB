# Benchmark Method

## Stage 1 method goals

Stage 1 is about platform shape and reproducibility, not final scientific conclusions.

## Measurement split

- **Executor** measures and writes raw results.
- **Analyzer** evaluates raw results using policies and baselines.
- **Charts** aggregate raw/analyzed results into human-readable summaries.

## Run categories

Current repository state distinguishes:

- synthetic pipeline validation runs;
- stage2 real Polar.DB runs;
- future engine-deep research runs.

## Statistical expectations

Stage 1 schemas already support:

- warmup count;
- measured count;
- median;
- p95;
- min/max;
- standard deviation.

Current stage2 raw results are single-run timings (`elapsedMsSingleRun`). Median/p95 become valid after measured multi-run execution is introduced.

## Reproducibility minimum

Every run should preserve:

- experiment id;
- dataset profile;
- fairness profile;
- engine key;
- environment class;
- git commit if available;
- deterministic seed if used;
- produced artifact inventory.
