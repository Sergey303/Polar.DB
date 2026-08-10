# Corrected reopen benchmark rerun

This note accompanies the corrected `reopen-only` benchmark.

## What changed

- SQLite dataset-creation pools are cleared before timing.
- Every measured SQLite reopen uses `Pooling=False`, so disposing the connection closes the physical connection instead of returning it to the pool.
- The benchmark manifest records native `sqlite_version()` in addition to the `Microsoft.Data.Sqlite` assembly version.
- `global.json` now pins the valid .NET SDK feature band `10.0.203` with `latestPatch` roll-forward.

## Validation and publication run

From the repository root on branch `agent/fix-reopen-benchmark`:

```powershell
dotnet --version
dotnet build .\benchmarks\src\ReopenOnly\ReopenOnly.csproj -c Release
dotnet run -c Release --project .\benchmarks\src\ReopenOnly\ReopenOnly.csproj
```

For a publication result, confirm that the generated manifest has:

- `publicationReady = true`;
- the expected Git commit;
- `gitDirty = false`;
- a non-empty `sqliteNativeVersion`;
- a `reopenDefinition` stating that SQLite pools are cleared and measured opens use `Pooling=False`.

Return the generated `reopen-only.raw.json`, `reopen-only.manifest.json`, and preferably the immutable raw run directory or ZIP for article update.
