# New benchmarks TODO

Current step:
- the missing `benchmarks/src/BenchSupport` files are included;
- no removal script is included because old benchmark folders are already gone;
- all new `.cs` files are under 150 lines;
- no `partial` classes or methods are used;
- `LookupBench.cs` and `LifecycleBench.cs` include `System.Text` explicitly.

Semantics:
- lookup excludes data generation, load, build, and reopen from measured samples;
- build-only measures index/build preparation after data load;
- reopen-only measures opening an existing prepared store;
- append-only measures appending rows to an already indexed store;
- delete-only uses Polar.DB logical tombstones and SQLite `DELETE`.

Still TODO:
- run `dotnet build Polar.DB.slnx` locally;
- run each benchmark and inspect the generated HTML;
- if append-only must be raw append, split it from append-with-index-maintenance.
