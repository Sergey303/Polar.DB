# Lookup series experiments

This update adds two common lookup workload families for current-source Polar.DB and SQLite.

## Workload keys

- `lookup-exact-one` — every generated lookup key must match exactly one row. The current Polar.DB adapter uses `USequence.GetExactlyOneByKey`.
- `lookup-all-matching` — generated keys may have duplicates. The current Polar.DB adapter uses `USequence.GetAllByKey` and validates the full duplicate range.

## Supported ready-file key kinds

The workload intentionally supports only stable ready-file lookup keys:

- `int`
- `long`
- `guid`

For Polar.DB, Guid is stored as `sstring` in the record and converted to `Guid` inside the key function. This avoids requiring a new native Polar Guid PType.

## Experiment folders

The archive contains six experiment folders:

- `lookup-exact-one-int`
- `lookup-all-matching-int`
- `lookup-exact-one-long`
- `lookup-all-matching-long`
- `lookup-exact-one-guid`
- `lookup-all-matching-guid`

Each experiment declares only these targets:

- `polar-db-current`
- `sqlite`

Pinned NuGet targets are intentionally not included because the new series depends on the new lookup methods added to current Polar.DB.

## Metrics

The executors write both new explicit metrics and compatibility metrics:

- `lookupSeriesMs`
- `lookupProbeCount`
- `lookupProbeHits`
- `lookupProbeMisses`
- `lookupReturnedRows`
- `lookupExpectedRows`
- `duplicateGroupSize`
- `randomPointLookupMs`
- `randomPointLookupCount`
- `randomPointLookupHits`
- `randomPointLookupMisses`

The `randomPointLookup*` aliases keep existing analysis/charts useful while the new `lookup*` metrics make the new series explicit.
