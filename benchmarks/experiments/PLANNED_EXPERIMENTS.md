# Planned Experiments

These are required for the full research program, but they should not be added as active `experiment.json` files until the platform can execute them truthfully.

## 1. Scale series

Purpose:

> Find the point where algorithmic or file-system behavior changes.

Planned families:

- reference lifecycle at 10m, 50m, 100m;
- append lifecycle at larger final sizes;
- search diagnostics at larger category cardinality.

Required support:

- explicit resource limits;
- timeout reporting;
- environment class tagging;
- possibly generated dataset profiles.

## 2. Load-order matrix

Purpose:

> Separate performance caused by engine design from performance caused by favorable input ordering.

Planned load orders:

- sorted id;
- reverse id;
- random id;
- partially sorted blocks.

Required support:

- adapter must honor `workload.options.loadOrder`.

## 3. Lookup distribution matrix

Purpose:

> Measure not only average lookup speed, but behavior under realistic skew.

Planned distributions:

- uniform;
- hot 5%;
- Zipf-like;
- missing-key heavy;
- mixed existing/missing.

Required support:

- shared deterministic query generator;
- raw metrics for hit/miss latency separately.

## 4. Cache protocol

Purpose:

> Separate cold storage performance from warmed process/cache behavior.

Planned phases:

- cold process;
- reopened warm process;
- repeated hot query set;
- cache-disabled / cache-enabled variants.

Required support:

- explicit cache state in protocol and metrics.

## 5. Fault and recovery

Purpose:

> Measure not only speed, but trustworthiness under failure.

Planned scenarios:

- interrupted append;
- interrupted build;
- missing state file;
- corrupted index file;
- corrupted primary data file.

Required support:

- fault injection;
- semantic status taxonomy;
- recovery artifacts.

## 6. Concurrency

Purpose:

> Understand behavior when reads and writes overlap.

Planned scenarios:

- multiple readers;
- append while reading;
- reopen while reading;
- query bursts.

Required support:

- multi-worker harness;
- synchronization protocol;
- per-worker metrics.

## 7. Storage economics

Purpose:

> Track not only elapsed time, but bytes per record and growth behavior.

Planned metrics:

- primary bytes per record;
- index bytes per record;
- state bytes;
- WAL/side artifacts;
- artifact growth per append cycle;
- write amplification proxy.

Required support:

- consistent artifact role classification.
