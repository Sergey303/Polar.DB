# Benchmark Method

## Stage 1 method goals

Stage 1 is about platform shape and reproducibility, not final scientific conclusions.

## Measurement split

- **Executor** measures and writes raw results.
- **Analyzer** evaluates raw results using policies and baselines.
- **Charts** aggregate raw/analyzed results into human-readable summaries.

## Run categories

Stage 1 distinguishes:

- synthetic pipeline validation runs;
- future real engine runs;
- future engine-deep research runs.

## Statistical expectations

Stage 1 schemas already support:

- warmup count;
- measured count;
- median;
- p95;
- min/max;
- standard deviation.

Real experiments should prefer median over single-run timing.

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
