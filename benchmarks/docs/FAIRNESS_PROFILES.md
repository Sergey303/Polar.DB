# Fairness Profiles

## Purpose

Fairness profile makes cross-engine setup explicit.

Without it, one engine can accidentally run in a "faster but weaker durability" mode while the other runs in a stricter mode.

## Active profile in common comparisons

### `durability-balanced`

Used for stage3/stage4 common experiments.

Interpretation in stage4:

- both engines follow the same semantic workload;
- each adapter applies engine-specific settings that correspond to balanced durability.

SQLite stage4 mapping:

- `PRAGMA journal_mode=WAL`
- `PRAGMA synchronous=FULL`
- `PRAGMA temp_store=FILE`

Polar.DB stage4 mapping:

- use real load/build/reopen/lookup flow with persisted artifacts (`f0.bin`, index files, `state.bin`);
- no synthetic shortcuts that bypass persistence behavior.

## Other profile names (reserved)

- `max-throughput`
- `reopen-focused`
- `crash-safety-focused`

These names are part of the catalog, but stage4 common adapters only implement `durability-balanced`.

## Stage4 comparison-set rule

Fairness profile is part of comparison filtering and of aggregated `comparison-series` artifacts.

That means one comparison set should keep the same fairness profile across targets and measured runs.
