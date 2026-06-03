# New benchmarks TODO

Current step:
- old benchmark folders are already gone, so no removal script is needed;
- lifecycle experiments now use the fully indexed setup on both engines;
- build-only artifact size is taken from the last measured run, not from all warmup/run folders;
- SQLite append/delete are measured inside one transaction, closer to Polar.DB append/tombstone measurement;
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
