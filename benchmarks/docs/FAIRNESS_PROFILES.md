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
