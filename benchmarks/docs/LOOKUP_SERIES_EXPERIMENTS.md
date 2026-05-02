# Lookup series experiments

The active lookup baseline is intentionally reduced to two Int32 experiments:

| Experiment | Meaning |
|---|---|
| `lookup-exact-one-int` | Unique key lookup: every key matches exactly one row. |
| `lookup-all-matching-int` | Duplicate key lookup: every key usually matches `duplicateGroupSize` rows. |

Each experiment produces two lookup measurements over the same generated probes.

## 1. Index-only phase

The executor resolves key matches without reading payload records.

For Polar.DB this uses:

```csharp
GetExactlyOneOffsetByKey(key)
GetOffsetsByKey(key)
CountByKey(key)
```

For SQLite this uses `SELECT id ... WHERE lookup_key = $lookupKey`, deliberately not selecting `payload`.

Metrics:

```text
indexOnlyLookupMs
indexOnlyProbeCount
indexOnlyProbeHits
indexOnlyProbeMisses
indexOnlyReturnedOffsets
indexOnlyExpectedOffsets
```

## 2. Materialized phase

The executor resolves the same probes and reads payload records.

For Polar.DB this uses:

```csharp
GetExactlyOneByKey(key)
GetAllByKey(key)
```

For SQLite this uses `SELECT id, lookup_key, payload ... WHERE lookup_key = $lookupKey`.

Metrics:

```text
materializedLookupMs
materializedProbeCount
materializedProbeHits
materializedProbeMisses
materializedReturnedRows
materializedExpectedRows
```

## Interpretation

- If SQLite wins mostly in `indexOnlyLookupMs`, Polar.DB needs a better persistent/index layout.
- If Polar.DB is close in `indexOnlyLookupMs` but loses in `materializedLookupMs`, the main cost is payload materialization/deserialization.
- Existing compatibility metrics (`randomPointLookupMs`, `lookupHitCount`, `lookupReturnedRows`) are still written and map to the materialized phase.
