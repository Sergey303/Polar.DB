# Polar.DB Benchmarks — Stage 1 Package

This package is meant to be unpacked into `./benchmarks` at the repository root.

Stage 1 intentionally focuses on the platform skeleton:

- shared contracts and DTOs;
- raw/analyzed result formats;
- executor, analyzer, and charts project shells;
- sample experiment, baseline, and policy files;
- documentation for workloads, faults, fairness, and result schemas;
- adapter skeletons for Polar.DB and SQLite.

What Stage 1 **does not** pretend to complete:

- a production-grade Polar.DB engine adapter;
- a production-grade SQLite adapter;
- full CLI ergonomics;
- all experiment families from the long-term platform spec;
- chart rendering beyond stage-1 summaries and CSV export.

The raw design goal is to reduce coupling early:

1. **BenchExec** only executes and writes raw results.
2. **BenchAnalysis** reads raw results and writes enriched analyzed results.
3. **BenchCharts** reads results and emits summaries / chart-ready datasets.

## Suggested first integration steps

1. Unpack this archive into `./benchmarks`.
2. Add the projects to the solution.
3. Adjust any project reference paths if your repository layout differs.
4. Implement the Polar.DB adapter first for one common experiment family.
5. Add SQLite next using the same `ExperimentSpec` and result schema.
6. Keep raw results immutable from day one.

See `SOLUTION_INTEGRATION_STAGE1.md` and `BENCHMARK_PLATFORM_SPEC.md` first.
