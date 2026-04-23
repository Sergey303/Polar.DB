# Benchmark Workflow Guide

Run the full benchmark workflow from one command:

```bash
dotnet run --project .\benchmarks\src\Polar.DB.Bench.Exec\Polar.DB.Bench.Exec.csproj --exp '.\benchmarks\experiments\persons-full-adapter-coverage-version-matrix\'
```

This command loads `experiment.json`, runs all `targets`, writes raw run files, builds analysis/comparison artifacts, and refreshes `index.html`.

## What `full-adapter-coverage` does

For each target engine, the workload executes:

1. Initial reverse bulk load of `persons(id,name,age)`.
2. Lookup structure build/index prepare.
3. Optional reopen/refresh after initial build (`reopenAfterInitialLoad`).
4. Direct lookup by key (`directLookup`).
5. Initial random point lookup batch (`lookup`).
6. Append cycles (`batches` x `batchSize`).
7. Optional reopen/refresh after each batch (`reopenAfterEachBatch`).
8. Optional random lookup sample after each batch (`randomLookupAfterEachBatch`, `randomLookupPerBatch`).
9. Final artifact capture and growth tracking from the initial built state.

## Output layout

Experiment artifacts are stored in:

`benchmarks/experiments/persons-full-adapter-coverage-version-matrix/`

Main folders:

- `raw/` immutable run results
- `analyzed/` derived analysis artifacts
- `comparisons/` engine/history comparisons
- `index.html` human-readable report

## Manual single-target run

```bash
dotnet run --project .\benchmarks\src\Polar.DB.Bench.Exec\Polar.DB.Bench.Exec.csproj --spec '.\benchmarks\experiments\persons-full-adapter-coverage-version-matrix\' --engine sqlite --work '.\benchmarks\work\persons-full-adapter-coverage-version-matrix\sqlite'
```
