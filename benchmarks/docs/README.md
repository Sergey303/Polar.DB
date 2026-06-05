# Benchmarks

`build-primary-int-only` reports a build-stage breakdown:

- `Build` is `CREATE UNIQUE INDEX` for SQLite and `Sequence.Build()` for Polar.DB;
- `Flush` is `PRAGMA wal_checkpoint(TRUNCATE)` for SQLite and `Sequence.Flush()` for Polar.DB;
- `Total` is the measured build+flush window.

This makes the build experiment diagnostic: if Polar.DB still loses, the report
shows whether the time is spent in index construction or in persistence/flush.

Search reports still separate batch throughput from single-query latency.

Generated benchmark work files are temporary and may be removed after each run.
