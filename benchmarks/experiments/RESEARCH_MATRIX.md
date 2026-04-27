# Research Matrix

This file describes the grand measurement plan. Not every row is executable yet.

## Executable now

| Research question | Experiment | Targets | Status |
|---|---|---|---|
| Reference lifecycle smoke | `persons-reference-load-build-reopen-lookup-100k` | current Polar.DB, SQLite | executable |
| Reference lifecycle main | `persons-load-build-reopen-random-lookup` | current Polar.DB, SQLite | executable |
| Reference lifecycle scale pressure | `persons-reference-load-build-reopen-lookup-5m` | current Polar.DB, SQLite | executable |
| Append/reopen lifecycle | `persons-append-cycles-reopen-lookup` | current Polar.DB, SQLite | executable |
| Broad current-vs-SQLite coverage | `persons-full-adapter-coverage-version-matrix` | current Polar.DB, SQLite | executable |
| Search diagnostics | `polar-db-search-point-and-category-1m` | current Polar.DB, 2.1.1, 2.1.0 | executable |
| Historical exact reference | `polar-db-u-sequence-reverse-load-build-random-lookup-1m` | current Polar.DB, 2.1.1, 2.1.0 | executable |

## Planned, not yet executable as canonical manifests

| Axis | Planned experiment family | Requires platform support |
|---|---|---|
| Bigger scale ladder | 10m / 50m / 100m reference runs | configurable scale profiles, timeout/resource reporting |
| Load-order matrix | sorted vs reverse vs random input | explicit load-order option in adapters |
| Lookup distribution | uniform vs hot/cold vs Zipf | deterministic query-distribution generator |
| Missing-key-heavy lookup | 0%, 50%, 100% missing keys | explicit missing-key workload outside search-only runner |
| Range/secondary-index queries | range scan, secondary index lookup | engine capability and adapter support |
| Cache behavior | cold/warm/hot lookup phases | explicit cache-state protocol |
| Long append history | many small batches vs few large batches | configurable append-cycle profiles |
| Crash recovery | kill during append/build, reopen recovery | fault-injection runner support |
| Corruption detection | damaged state/index/data files | controlled corruption support and semantic status |
| Concurrency | readers during append, multiple readers | multi-process/multi-thread runner |
| Memory pressure | constrained memory / large heap tracking | resource-limited execution harness |
| File-system sensitivity | HDD/SSD/network/tempfs profiles | environment tags and run isolation |
