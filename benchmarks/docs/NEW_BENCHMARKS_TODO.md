# New benchmarks TODO

Current step:
- all new support files are under 150 lines;
- no `partial` classes or methods are used;
- `mag_experiments` is not removed;
- lookup experiments use real Polar.DB and SQLite paths;
- BuildOnly, ReopenOnly, AppendOnly, and DeleteOnly now use the same support layer.

Semantics:
- lookup excludes data generation, load, build, and reopen from measured samples;
- build-only measures index/build preparation after data load;
- reopen-only measures opening an existing prepared store;
- append-only measures appending rows to an already indexed store;
- delete-only uses Polar.DB logical tombstones and SQLite `DELETE`.

Still TODO:
- run locally and adjust exact API calls if the branch differs;
- decide whether append-only should also have a raw append variant without index maintenance;
- add focused tests for benchmark helper code if it becomes more than a harness.
