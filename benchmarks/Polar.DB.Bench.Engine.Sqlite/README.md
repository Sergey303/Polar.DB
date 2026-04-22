# SQLite Engine Adapter - Stage 3

Implemented in stage3:

- real provider: `Microsoft.Data.Sqlite`;
- first real common experiment: `persons-load-build-reopen-random-lookup`;
- workload key used by that experiment: `bulk-load-point-lookup`;
- semantic flow: load -> build index -> reopen -> random point lookup;
- artifact topology collection (`primary.db`, `primary.db-wal`, `primary.db-shm`, `primary.db-journal`, temp files when present in workspace);
- raw result metrics and SQLite diagnostics;
- explicit fairness mapping for `durability-balanced`.

Stage3 durability-balanced mapping:

- `PRAGMA journal_mode=WAL`
- `PRAGMA synchronous=FULL`
- `PRAGMA temp_store=FILE`

Run example:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- --engine sqlite --spec benchmarks/experiments/persons-load-build-reopen-random-lookup.sqlite.json --work benchmarks/work/stage3/sqlite --raw-out benchmarks/results/raw
```

Deferred to stage4:

- additional workload families;
- deeper SQLite internals studies (checkpoint/page-growth);
- multi-run statistical profile for cross-engine comparisons.
