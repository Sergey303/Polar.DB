# Workload Catalog

## Implemented common cross-engine workloads

### `bulk-load-point-lookup`

Semantic flow:

1. bulk load dataset;
2. build lookup structure;
3. close/reopen;
4. run random point lookup sample.

Experiment specs:

- `experiments/persons-load-build-reopen-random-lookup.polar-db.json`
- `experiments/persons-load-build-reopen-random-lookup.sqlite.json`

### `append-cycles-reopen-lookup` (stage4)

Semantic flow:

1. load initial dataset and build lookup structure;
2. run multiple append batches;
3. after each batch close/reopen;
4. after reopen run random point lookup sample;
5. track artifact growth.

Experiment specs:

- `experiments/persons-append-cycles-reopen-lookup.polar-db.json`
- `experiments/persons-append-cycles-reopen-lookup.sqlite.json`

## Planned common workloads

- `duplicate-key-first-match`
- `mixed-read-write`

## Planned engine-deep workloads

Polar.DB:

- `polar-state-strategy`
- `polar-refresh-complexity`
- `polar-appendoffset-invariant`
- `polar-variable-size-rewrite`

SQLite:

- `sqlite-wal-checkpoint`
- `sqlite-page-growth`
