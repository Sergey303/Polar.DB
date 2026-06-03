# New benchmarks TODO

This package is the first refactoring step.

Rules:
- one standalone console project per experiment under `benchmarks/src/<Project>/`;
- each project writes one HTML file to `benchmarks/results/<test>.html`;
- do not depend on old benchmark runner, analysis, charts, contracts, or experiment.json files;
- compare with SQLite where the operation has a fair SQLite equivalent;
- warm up measured operations where meaningful;
- materialize/serialize returned values before checksum;
- do not include setup time into lookup/append/delete/reopen-only metrics.

Current state:
- SQLite lookup shell is implemented in the four lookup projects.
- Polar.DB path is intentionally marked as TODO until exact local API calls are verified.
- build/reopen/append/delete projects are scaffolded and must be tightened after local compile.
