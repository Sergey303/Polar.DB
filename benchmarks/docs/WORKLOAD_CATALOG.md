# Workload Catalog

## Implemented common cross-engine workloads

### `bulk-load-point-lookup`

Semantic flow (imported and normalized from reference console experiments):

1. create storage for `persons(id, name, age)`;
2. bulk load dataset in reverse `id` order;
3. build/stabilize lookup structure for point access;
4. close/reopen to reach point-lookup-ready persisted state;
5. execute one direct lookup by key;
6. execute random point lookup batch (`lookup=10_000`);
7. collect artifact sizes.

Normalization choices for fair Polar.DB vs SQLite comparison:

- one shared dataset model: `persons(id,name,age)`;
- one shared reverse insert order;
- one shared lookup batch size (`10_000`);
- one shared workload meaning (internal APIs differ, semantics do not);
- one shared artifact accounting rule (`total`, `primary`, `side` bytes).

Experiment manifest:

- `experiments/persons-load-build-reopen-random-lookup/experiment.json`

### `append-cycles-reopen-lookup` (stage4)

Semantic flow:

1. load initial dataset and build lookup structure;
2. run multiple append batches;
3. after each batch close/reopen;
4. after reopen run random point lookup sample;
5. track artifact growth.

Experiment manifest:

- `experiments/persons-append-cycles-reopen-lookup/experiment.json`

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
