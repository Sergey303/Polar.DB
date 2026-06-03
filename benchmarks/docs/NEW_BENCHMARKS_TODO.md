# New benchmarks TODO

Current step:
- lookup checksums now use order-sensitive combining instead of XOR;
- repeated external-key probes can no longer cancel each other out in checksum;
- per-query materialized rows are hashed through the same `HashRows` path;
- all changed `.cs` files are under 150 lines;
- no `partial` classes or methods are used.

Semantics:
- lookup excludes data generation, load, build, and reopen from measured samples;
- build-only measures index/build preparation after data load;
- reopen-only measures opening an existing prepared store with indexes;
- append-only measures indexed append without per-row flush/commit;
- delete-only uses Polar.DB logical tombstones and SQLite `DELETE` inside one transaction.

Still TODO:
- run `dotnet build Polar.DB.slnx` locally;
- run each benchmark and inspect the generated HTML;
- decide whether a separate raw append benchmark is needed.
