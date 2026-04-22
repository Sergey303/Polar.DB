# Solution Integration — Stage 1

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

- `Polar.DB.Bench.Engine.PolarDb` is intentionally a skeleton.
- `Polar.DB.Bench.Engine.Sqlite` is intentionally a skeleton.
- `Polar.DB.Bench.Exec` already contains a small synthetic adapter so the raw → analyzed → charts flow can be tested before the real engines are wired.
- The stage-1 CLI is intentionally simple and file-driven.

## First realistic milestone

Implement one experiment end to end for both engines:

- `bulk-load-point-lookup`
- dataset profile `persons-100k-reverse`
- fairness profile `durability-balanced`

Only after that should you widen the experiment matrix.
