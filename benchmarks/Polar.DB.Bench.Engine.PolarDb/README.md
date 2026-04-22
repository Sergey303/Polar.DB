# Polar.DB Engine Adapter - Stage 2

Implemented in stage2:

- real project reference to `src/Polar.DB`;
- first real experiment: `persons-load-build-reopen-random-lookup`;
- workload key used by that experiment: `bulk-load-point-lookup`;
- experiment flow: load -> build -> reopen/refresh -> random point lookup;
- artifact topology collection (`f0.bin`, `f1.bin`, `f2.bin`, `state.bin`);
- raw result metrics and Polar.DB diagnostics.

Run example:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine polar-db --spec benchmarks/experiments/persons-load-build-reopen-random-lookup.polar-db.json --work benchmarks/results/work/polar-db-first --raw-out benchmarks/results/raw
```

Deferred to stage3:

- additional experiment families;
- deeper state/recovery diagnostics;
- parity with SQLite adapter.
