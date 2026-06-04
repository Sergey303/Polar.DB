# Benchmarks

Search experiments print progress to the console for setup, warmup, batch
throughput samples, and single-query latency samples.

Search experiments use two phases in the same HTML report:

- cold after reopen: storage is reopened, no explicit file warmup, no lookup warmup;
- hot after file and lookup warmup: storage is reopened, files are sequentially read once,
  then unmeasured lookup batches are executed before measurement.

Reopen time is excluded from measured lookup time.

Search reports have two metric groups:

- batch throughput: batch average ms/query, rows/sec, batch size;
- single-query latency: median/p95/max measured on individual queries.

Current default batch settings:

- primary lookup: `100` lookups per batch sample;
- normal external lookup: enough lookups per sample to target about `20_000`
  returned rows per sample;
- famous external lookup: `1` lookup per sample.

Current sample counts:

- primary: `10` cold batch samples, `100` hot batch samples, `1000` latency samples;
- normal external: `5` cold batch samples, `30` hot batch samples, `30` latency samples;
- famous external: `1` cold batch sample, `1` hot batch sample, `1` latency sample.

Correctness is checked against batch measured queries and ignores materialized row order.

Do not commit generated files:

- `benchmarks/work/`
- `benchmarks/results/*.html`
- `*.sqlite`
- `*.sqlite-wal`
- `*.sqlite-shm`
- `*.db`
- `*.db-wal`
- `*.db-shm`
- `*.state`
- `*.index`
