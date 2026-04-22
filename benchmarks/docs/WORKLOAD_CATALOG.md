# Workload Catalog

## Common cross-engine workloads

### `bulk-load-point-lookup`
Load records, stabilize for point lookup, reopen, then execute random point lookups.

Implemented common experiment specs:

- `experiments/persons-load-build-reopen-random-lookup.polar-db.json`
- `experiments/persons-load-build-reopen-random-lookup.sqlite.json`

### `append-cycles-reopen`
Append in batches, reopen after every cycle, then validate point lookup and artifact growth.

### `duplicate-key-first-match`
Load duplicate-rich records, measure first-match lookup and range traversal.

### `mixed-read-write`
A future mixed flow with reads, appends, and periodic reopen.

## Engine-deep workloads for Polar.DB

### `polar-state-strategy`
Compare state management approaches.

### `polar-refresh-complexity`
Study refresh cost vs file size and corruption shape.

### `polar-appendoffset-invariant`
Study the cost of maintaining logical-end invariants.

### `polar-variable-size-rewrite`
Study safety and cost tradeoffs for variable-size rewrites.

## Engine-deep workloads for SQLite

### `sqlite-wal-checkpoint`
Study WAL growth and checkpoint cost.

### `sqlite-page-growth`
Study footprint and fragmentation behavior.
