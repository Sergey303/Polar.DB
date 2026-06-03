# New benchmarks TODO

Current step:
- primary lookup matrix now has `int`, `long`, GUID-like string, and string keys;
- external lookup matrix now has `int`, `long`, GUID-like string, and string keys;
- famous external lookup matrix now has `int`, `long`, GUID-like string, and string keys;
- famous external-key datasets keep 40% rows on the searched key for each key type;
- heavy famous external lookup still uses `1` warmup operation and `3` measured operations;
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
