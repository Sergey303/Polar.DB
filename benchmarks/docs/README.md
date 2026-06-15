# Benchmarks report wording

Lookup HTML reports use explicit names:

- `Batches count` is the number of measured batches.
- `Queries/batch` is the number of lookup requests inside one measured batch.
- `Total queries = Batches count * Queries/batch`.
- `Returned rows` is the total number of materialized rows returned by all lookup requests.
- `Returned rows/query = Returned rows / Total queries`.

Timing tables now include `Rows/sec by trimmed` next to trimmed timing columns.
For latency tables this is calculated from trimmed single-query latency and the
average number of rows returned by a latency query.

Green cells mark winners for comparable metrics. Lower is better for timings and
memory sizes. Higher is better for rows/sec and available RAM.
