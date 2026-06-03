# New benchmarks TODO

Current step:
- added primary-key lookup copies for `long` and GUID-like keys;
- GUID lookup stores canonical GUID strings because the current benchmark schema uses Polar.DB primitive strings;
- added `external-famous-string-lookup`;
- the famous external-key dataset has 40% rows with key `to-be-or-not-to-be`;
- heavy famous external lookup searches exactly that high-cardinality key;
- heavy external lookup uses `1` warmup and `3` measured operations to avoid materializing billions of rows;
- all changed `.cs` files are under 150 lines;
- no `partial` classes or methods are used.

Defaults:
- row orders: `50_000`, `5_000_000`;
- lookup: `300` warmup operations, `2_000` measured operations;
- heavy external famous lookup: `1` warmup operation, `3` measured operations;
- build-only: `1` warmup run, `3` measured runs;
- reopen-only: `3` warmup runs, `20` measured runs;
- append/delete: `50` warmup operations, `2_000` measured operations.

Still TODO:
- run `dotnet test tests/Polar.DB.Tests/Polar.DB.Tests.csproj` locally;
- run `dotnet build Polar.DB.slnx` locally;
- inspect whether `external-famous-string-lookup` needs smaller row orders on 16 GiB machines.
