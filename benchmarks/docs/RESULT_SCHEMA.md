# Result Schema

## Raw result

A raw result is immutable and contains only factual execution data.

Top-level areas:

- run identity;
- experiment identity;
- engine identity;
- fairness profile;
- environment manifest;
- success/failure;
- measured metrics;
- artifact inventory;
- engine diagnostics;
- free-form notes.

## Analyzed result

An analyzed result references one raw result and enriches it with:

- policy id;
- baseline id;
- derived metrics;
- check-level status entries;
- overall status;
- comparison notes.

## Status split

Execution status and policy status are separate concerns.

- execution failure belongs to executor results;
- policy result belongs to analyzed results.
