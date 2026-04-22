# Solution Integration - Stage 1

This package assumes it will live under `./benchmarks` at repository root.

## Expected relative layout

```text
repo-root/
  src/
  tests/
  benchmarks/
    Polar.DB.Bench.Core/
    Polar.DB.Bench.Exec/
    Polar.DB.Bench.Analysis/
    Polar.DB.Bench.Charts/
    Polar.DB.Bench.Engine.PolarDb/
    Polar.DB.Bench.Engine.Sqlite/
    docs/
    contracts/
    baselines/
    results/
    reports/
```

## Recommended solution folders

- Benchmarks
  - Polar.DB.Bench.Core
  - Polar.DB.Bench.Exec
  - Polar.DB.Bench.Analysis
  - Polar.DB.Bench.Charts
  - Polar.DB.Bench.Engine.PolarDb
  - Polar.DB.Bench.Engine.Sqlite

## Suggested `dotnet sln` commands

```bash
# Run from repository root

dotnet sln add benchmarks/Polar.DB.Bench.Core/Polar.DB.Bench.Core.csproj
dotnet sln add benchmarks/Polar.DB.Bench.Exec/Polar.DB.Bench.Exec.csproj
dotnet sln add benchmarks/Polar.DB.Bench.Analysis/Polar.DB.Bench.Analysis.csproj
dotnet sln add benchmarks/Polar.DB.Bench.Charts/Polar.DB.Bench.Charts.csproj
dotnet sln add benchmarks/Polar.DB.Bench.Engine.PolarDb/Polar.DB.Bench.Engine.PolarDb.csproj
dotnet sln add benchmarks/Polar.DB.Bench.Engine.Sqlite/Polar.DB.Bench.Engine.Sqlite.csproj
```

## Stage 1 notes

- `Polar.DB.Bench.Engine.PolarDb` now contains a stage2 real adapter for the first end-to-end experiment.
- `Polar.DB.Bench.Engine.Sqlite` is still intentionally a skeleton at this stage.
- `Polar.DB.Bench.Exec` keeps the synthetic adapter for pipeline smoke checks alongside real adapters.
- The CLI remains intentionally simple and file-driven.

## First realistic milestone

Current stage2 milestone is implemented for Polar.DB:

- experiment `persons-load-build-reopen-random-lookup`
- workload `bulk-load-point-lookup`
- fairness profile `durability-balanced`

SQLite parity remains deferred and should be handled separately from this milestone.
