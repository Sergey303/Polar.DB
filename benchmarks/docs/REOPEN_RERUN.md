# Corrected reopen benchmark rerun

This note records the corrected `reopen-only` benchmark used by the Polar.DB article.

## Correction

The previous SQLite reopen measurement used the default `Microsoft.Data.Sqlite` connection string. Connection pooling is enabled by default, so repeated `Open()` calls could reuse pooled physical connections. That made the old SQLite reopen values unsuitable for comparison with Polar.DB reopen + index preparation.

The corrected benchmark:

- clears existing SQLite pools after dataset preparation;
- uses `Pooling=False` for every measured SQLite reopen;
- records native `sqlite_version()` in the environment manifest;
- explicitly records the non-pooled reopen definition;
- uses a valid .NET SDK pin in `global.json` (`10.0.203`, `latestPatch`).

## Publication run

Run id:

`20260811T060237246Z-eed96558-reopen-only`

Source and environment:

- commit: `eed965582a1f0b1e6d0796c09528f0b7876b1ba4`;
- working tree: clean;
- build: Release;
- `publicationReady = true` for coordinator and both engine workers;
- .NET runtime: `10.0.7`, x64;
- SDK used to build/run from the repository: `10.0.203`;
- `Microsoft.Data.Sqlite` assembly: `9.0.4.0`;
- native SQLite: `3.53.3`;
- engine order: Polar.DB, then SQLite;
- warmup operations: 5;
- measured operations: 30;
- row counts: 50,000 and 5,000,000.

The manifest definition is:

> Open-only measures opening and closing storage handles. Query-ready measures opening, metadata/index readiness, and one indexed primary-key lookup. SQLite clears existing pools after dataset preparation and uses Pooling=False for every measured reopen.

## Results

Median values, milliseconds:

| Rows | Engine | Open-only | Query-ready reopen |
|---:|---|---:|---:|
| 50,000 | SQLite | 0.08370 | 0.82720 |
| 50,000 | Polar.DB | 1.36820 | 15.67085 |
| 5,000,000 | SQLite | 0.12035 | 0.81565 |
| 5,000,000 | Polar.DB | 1.16225 | 775.71060 |

For query-ready reopen, Polar.DB / SQLite median ratio is about 18.9 at 50,000 rows and about 951 at 5,000,000 rows. These ratios describe this benchmark definition and machine only. The test does not clear the Windows file cache, so the result must not be described as a cold-storage startup comparison.

The important architectural observation is scaling rather than the largest ratio: SQLite keeps its persistent B-tree index in the database and a single lookup after a non-pooled open remains below 1 ms in this run, while Polar.DB reconstructs/loads RAM-resident primary-index state and its query-ready reopen grows from about 15.7 ms at 50,000 rows to about 775.7 ms at 5,000,000 rows.

## Correctness

For both row counts, SQLite and Polar.DB returned the expected final row count and checksum:

- 50,000 rows: checksum `982062805716110519`;
- 5,000,000 rows: checksum `1291763481180701370`.

All 30 measured samples are retained in the raw run archive. The short smoke run `20260811T060232899Z-eed96558-reopen-only` was used only to validate the corrected harness and is not a publication result.

## Validation

The local Release build completed successfully. GitHub CI and CodeQL for commit `eed965582a1f0b1e6d0796c09528f0b7876b1ba4` both completed successfully.

## Reproduction

From the repository root:

```powershell
dotnet --version
dotnet build .\benchmarks\src\ReopenOnly\ReopenOnly.csproj -c Release
dotnet run -c Release --project .\benchmarks\src\ReopenOnly\ReopenOnly.csproj
```

A publication run must be retained under its immutable `benchmarks/results/raw/reopen-only/<run-id>/` directory together with `combined.raw.json`, `manifest.json`, the two worker JSON files and `samples.csv`.