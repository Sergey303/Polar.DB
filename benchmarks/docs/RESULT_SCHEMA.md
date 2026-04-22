# Result Schema

## Raw result

A raw result is immutable and contains only factual execution data.

Top-level areas:

- run identity;
- experiment identity;
- engine identity;
- fairness profile;
- environment manifest;
- technical execution status;
- semantic experiment outcome;
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
- semantic degradation belongs to executor raw facts (metrics/diagnostics), not infrastructure failure;
- policy result belongs to analyzed results.
