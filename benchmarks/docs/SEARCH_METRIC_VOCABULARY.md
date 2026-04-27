# Search Metric Vocabulary

This document defines reserved metric keys for future search experiments.
All keys use dot-separated namespacing with `search.` prefix.

## Point Lookup

Single-record lookup by primary key or unique identifier.

| Key | Type | Description |
|-----|------|-------------|
| `search.point.ms` | double | Total elapsed milliseconds for all point queries |
| `search.point.queries` | int | Number of point queries executed |
| `search.point.hits` | int | Number of queries that found a matching record |
| `search.point.misses` | int | Number of queries that found no matching record |
| `search.point.hitRate` | double | hits / queries |
| `search.point.emptyRate` | double | misses / queries |
| `search.point.msPerQuery` | double | totalMs / queryCount |
| `search.point.queriesPerSecond` | double | queryCount / (totalMs / 1000) |

## Multi-Result Lookup

Queries that may return zero, one, or many matching records.

| Key | Type | Description |
|-----|------|-------------|
| `search.multi.ms` | double | Total elapsed milliseconds for all multi-result queries |
| `search.multi.queries` | int | Number of multi-result queries executed |
| `search.multi.resultRowsTotal` | int | Total rows returned across all queries |
| `search.multi.rowsPerQueryAverage` | double | resultRowsTotal / queries |
| `search.multi.rowsPerQueryP50` | double | Median rows returned per query |
| `search.multi.rowsPerQueryP95` | double | 95th percentile rows returned per query |
| `search.multi.msPerQuery` | double | totalMs / queryCount |
| `search.multi.msPerReturnedRow` | double | totalMs / resultRowsTotal |
| `search.multi.rowsPerSecond` | double | resultRowsTotal / (totalMs / 1000) |
| `search.multi.emptyResultCount` | int | Number of queries that returned zero rows |
| `search.multi.semanticWrongRows` | int | Number of queries where returned rows did not match expected result set |
| `search.multi.duplicateRows` | int | Number of duplicate rows detected in result sets |

## Scan / Filter

Full or partial scan with predicate filtering.

| Key | Type | Description |
|-----|------|-------------|
| `search.scan.ms` | double | Total elapsed milliseconds for all scan queries |
| `search.scan.queries` | int | Number of scan queries executed |
| `search.scan.rowsScanned` | int | Total rows examined during scans |
| `search.scan.rowsMatched` | int | Total rows that passed the filter predicate |
| `search.scan.msPerQuery` | double | totalMs / queryCount |
| `search.scan.rowsScannedPerSecond` | double | rowsScanned / (totalMs / 1000) |
| `search.scan.rowsMatchedPerSecond` | double | rowsMatched / (totalMs / 1000) |

## Formulas

```
msPerQuery = totalMs / queryCount
queriesPerSecond = queryCount / (totalMs / 1000)
msPerReturnedRow = totalMs / resultRowsTotal
rowsPerSecond = resultRowsTotal / (totalMs / 1000)
```

## Reserved Cache Metrics (not yet implemented)

These keys are reserved for future cache-aware search experiments.

| Key | Type | Description |
|-----|------|-------------|
| `search.cache.coldMs` | double | Query time with cold cache |
| `search.cache.warmMs` | double | Query time with warm cache |
| `search.cache.steadyMs` | double | Query time at steady state |
| `search.cache.hitRate` | double | Cache hit rate |
| `search.cache.missRate` | double | Cache miss rate |
| `search.cache.benefit` | double | coldMedian / warmMedian |
| `search.cache.paybackQueries` | double | cacheBuildCostMs / (coldMsPerQuery - warmMsPerQuery) |
| `search.cache.memoryBytes` | double | Memory consumed by cache structures |
| `search.cache.invalidationMs` | double | Time spent invalidating cache entries |

Cache formulas:

```
cacheBenefit = coldMedian / warmMedian
cachePaybackQueries = cacheBuildCostMs / (coldMsPerQuery - warmMsPerQuery)
```

## Rules

1. **Cached and uncached search metrics must not be mixed in one metric.**
   Always prefix with `search.cache.` for cached measurements.

2. **Single-result lookup and multi-result lookup must be reported separately.**
   Use `search.point.*` for single-record lookups and `search.multi.*` for queries
   that may return multiple rows.

3. **Query time without result cardinality is not enough for multi-result search.**
   Always report `resultRowsTotal` alongside `ms` for multi-result queries.
