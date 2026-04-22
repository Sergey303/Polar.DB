# Fairness Profiles

## Purpose

A fairness profile makes cross-engine comparisons explicit.

The same experiment should not silently compare one engine in a “fast but unsafe” mode against another in a “safe but conservative” mode.

## Initial profiles

### `durability-balanced`
Balanced persistence expectations suitable for mainstream comparison.

### `max-throughput`
Favors throughput over stronger durability assumptions.

### `reopen-focused`
Optimized for repeated close/reopen scenarios.

### `crash-safety-focused`
Favors safer persistence semantics even if throughput drops.

## Stage 1 rule

Stage 1 only defines the schema and names.
Engine-specific mappings remain TODO in the Polar.DB and SQLite adapter projects.

## Stage 3 first baseline mapping

For the first common comparison (`persons-load-build-reopen-random-lookup`) the platform uses `durability-balanced`.

### Polar.DB interpretation (stage3 baseline)

- use the real adapter flow `Load -> Build -> Close -> Reopen/Refresh -> Lookup`;
- no synthetic shortcuts or skipped persistence phases;
- artifacts are measured as produced (`f0.bin`, index segments, `state.bin`).

### SQLite interpretation (stage3 baseline)

- `PRAGMA journal_mode=WAL`;
- `PRAGMA synchronous=FULL`;
- `PRAGMA temp_store=FILE`;
- schema and index are materialized via SQL (`CREATE TABLE`, bulk `INSERT`, `CREATE INDEX`);
- reopen and point lookup run against the persisted database file.

### Scope note

This is the first minimal fairness baseline, not a universal final policy system. Stage4 can extend mappings and profile families.
