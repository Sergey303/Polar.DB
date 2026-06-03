# New benchmarks TODO

Current step:
- `pk-int-lookup`, `pk-string-lookup`, `external-int-lookup`, and
  `external-string-lookup` now have real Polar.DB and SQLite paths.
- Lookup measurement excludes data generation, load, build, and reopen.
- Both engines materialize returned values into the same `Row` shape before checksum.
- Result HTML contains median, p95, min, max, trimmed mean, rows, checksum, and artifact bytes.

Still TODO:
- split BuildOnly, ReopenOnly, AppendOnly, DeleteOnly into the same support style;
- decide whether append-only means raw append or append with index maintenance;
- return NotSupported for delete if Polar.DB has no real delete API;
- run local compile and adjust exact API calls if the repository branch differs.
