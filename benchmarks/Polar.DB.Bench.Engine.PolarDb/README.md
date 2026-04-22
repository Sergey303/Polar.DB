# Polar.DB Engine Adapter - Stage 2

Implemented in stage2:

- real project reference to `src/Polar.DB`;
- first real workload: `bulk-load-point-lookup`;
- experiment flow: load -> build -> reopen/refresh -> random point lookup;
- artifact topology collection (`f0.bin`, `f1.bin`, `f2.bin`, `state.bin`);
- raw result metrics and Polar.DB diagnostics.

Deferred to stage3:

- additional experiment families;
- deeper state/recovery diagnostics;
- parity with SQLite adapter.
