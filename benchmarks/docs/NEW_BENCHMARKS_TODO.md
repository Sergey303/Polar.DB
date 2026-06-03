# New benchmarks TODO

Current step:
- correctness now ignores materialized row order;
- `HashRows` is a multiset checksum: same rows in different order produce the same checksum;
- a focused xUnit test verifies that same-count but different rows still produce a different checksum;
- all changed `.cs` files are under 150 lines;
- no `partial` classes or methods are used.

Defaults:
- row orders: `50_000`, `5_000_000`;
- lookup: `300` warmup operations, `2_000` measured operations;
- build-only: `1` warmup run, `3` measured runs;
- reopen-only: `3` warmup runs, `20` measured runs;
- append/delete: `50` warmup operations, `2_000` measured operations.

Still TODO:
- delete obsolete `benchmarks/src/BenchSupport/BenchmarkArgs.cs` if it exists locally;
- remove already committed generated HTML files from git history/current index;
- run `dotnet test tests/Polar.DB.Tests/Polar.DB.Tests.csproj` locally;
- run `dotnet build Polar.DB.slnx` locally;
- inspect the large-order runs and adjust `BenchmarkDefaults.RowCounts` if neither engine approaches memory pressure;
- decide whether a separate raw append benchmark is needed.
