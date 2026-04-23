# Polar.DB Engine Adapter - Stage 4

Implemented common experiments:

- `persons-load-build-reopen-random-lookup` (`bulk-load-point-lookup`);
- `persons-append-cycles-reopen-lookup` (`append-cycles-reopen-lookup`).

Both use real Polar.DB flow and real artifact collection.

For `persons-load-build-reopen-random-lookup` the semantic flow is reference-imported and normalized:
reverse bulk load -> build -> reopen/refresh -> one direct key lookup -> random lookup batch (`10_000`).

## Stage4 append cycles flow

1. initial load/build;
2. append batches via `AppendElement`;
3. close/reopen (`Refresh`) after each batch;
4. random point lookup sample after each reopen;
5. artifact growth metrics in raw result.

## Run examples

Single run:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine polar-db-current --spec benchmarks/experiments/persons-load-build-reopen-random-lookup --work benchmarks/work/polar-single --warmup-count 0 --measured-count 1
```

Series run in one comparison set (defaults to 1 warmup + 3 measured):

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine polar-db-current --spec benchmarks/experiments/persons-append-cycles-reopen-lookup --work benchmarks/work/polar-series --comparison-set stage4-append-001
```
