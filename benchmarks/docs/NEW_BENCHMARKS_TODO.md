# New benchmarks TODO

Current step:
- GUID benchmark keys now use `System.Guid` in the benchmark model;
- SQLite stores GUID keys as 16-byte BLOB values;
- Polar.DB stores GUID keys as two `longinteger` fields and exposes `Guid` values to indexes;
- primary GUID lookup and external GUID lookup compare real `Guid` values, not strings;
- all changed `.cs` files are under 150 lines;
- no `partial` classes or methods are used.

Defaults:
- row orders: `50_000`, `5_000_000`;
- normal lookup: `300` warmup operations, `2_000` measured operations;
- famous external lookup: `1` warmup operation, `3` measured operations;
- build-only: `1` warmup run, `3` measured runs;
- reopen-only: `3` warmup runs, `20` measured runs;
- append/delete: `50` warmup operations, `2_000` measured operations.

Still TODO:
- run `dotnet test tests/Polar.DB.Tests/Polar.DB.Tests.csproj` locally;
- run `dotnet build Polar.DB.slnx` locally;
- inspect whether famous external lookup needs smaller row orders on 16 GiB machines.
