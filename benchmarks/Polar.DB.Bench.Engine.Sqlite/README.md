# SQLite Engine Adapter - Stage 4

Implemented common experiments:

- `persons-load-build-reopen-random-lookup` (`bulk-load-point-lookup`);
- `persons-append-cycles-reopen-lookup` (`append-cycles-reopen-lookup`).

## Fairness mapping (`durability-balanced`)

- `PRAGMA journal_mode=WAL`
- `PRAGMA synchronous=FULL`
- `PRAGMA temp_store=FILE`

## Stage4 append cycles flow

1. initial load + index build;
2. append batches via `INSERT` transactions;
3. close/reopen connection after each batch;
4. random `SELECT ... WHERE id = ?` sample after reopen;
5. artifact growth metrics (`db`, `wal`, `shm`, `journal`, temporary files).

## Run examples

Single run:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine sqlite --spec benchmarks/experiments/persons-load-build-reopen-random-lookup.sqlite.json --work benchmarks/work/sqlite-single --raw-out benchmarks/results/raw --warmup-count 0 --measured-count 1
```

Series run in one comparison set (defaults to 1 warmup + 3 measured):

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine sqlite --spec benchmarks/experiments/persons-append-cycles-reopen-lookup.sqlite.json --work benchmarks/work/sqlite-series --raw-out benchmarks/results/raw --comparison-set stage4-append-001
```
