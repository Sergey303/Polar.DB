# Cross-Target Comparison Summary

Generated at: 2026-04-24T05:15:39.8492950+00:00

This summary compares measured runs inside the same comparison set.
Fairness profile means each target maps one shared intent (durability/performance balance) to engine-specific settings.
Primary bytes are the main data file(s). Side bytes are WAL/state/index/other side artifacts.
Technical success means run infrastructure completed. Semantic success means workload-level checks passed.

| ComparisonId | Set | Experiment | Experiment label | Dataset | Fairness | Target | Lookup batch avg | Measured runs | Elapsed ms (min/avg/med/max) | Load ms (min/avg/med/max) | Build ms (min/avg/med/max) | Reopen ms (min/avg/med/max) | Lookup ms (min/avg/med/max) | Total bytes (min/avg/med/max) | Primary bytes (min/avg/med/max) | Side bytes (min/avg/med/max) | Technical success | Semantic success |
| --- | --- | --- | --- | --- | --- | --- | ---: | ---: | --- | --- | --- | --- | --- | --- | --- | --- | ---: | ---: |
| 2026-04-24T05-15-37Z__persons-full-adapter-coverage-version-matrix__persons-400k-plus-append-3x200k-reverse__simple-20260424T051455__multi-target | simple-20260424T051455 | persons-full-adapter-coverage-version-matrix | persons-full-adapter-coverage-version-matrix | persons-400k-plus-append-3x200k-reverse | durability-balanced | polar-db-2.1.1 | 25000 | 3 | 4147.849/4361.475/4455.396/4481.179 | 64.908/77.088/81.252/85.103 | 241.227/274.894/274.852/308.603 | 1071.501/1153.016/1137.243/1250.304 | 644.04/671.313/683.777/686.122 | 21688936/21688936/21688936/21688936 | 16888904/16888904/16888904/16888904 | 4800032/4800032/4800032/4800032 | 3/3 | 3/3 |
| 2026-04-24T05-15-37Z__persons-full-adapter-coverage-version-matrix__persons-400k-plus-append-3x200k-reverse__simple-20260424T051455__multi-target | simple-20260424T051455 | persons-full-adapter-coverage-version-matrix | persons-full-adapter-coverage-version-matrix | persons-400k-plus-append-3x200k-reverse | durability-balanced | polar-db-current | 25000 | 3 | 4027.498/4130.555/4091.803/4272.362 | 70.816/76.504/77.747/80.947 | 234.905/239.152/240.204/242.347 | 1016.329/1070.504/1057.613/1137.572 | 613.96/641.234/649.253/660.49 | 21688936/21688936/21688936/21688936 | 16888904/16888904/16888904/16888904 | 4800032/4800032/4800032/4800032 | 3/3 | 3/3 |
| 2026-04-24T05-15-37Z__persons-full-adapter-coverage-version-matrix__persons-400k-plus-append-3x200k-reverse__simple-20260424T051455__multi-target | simple-20260424T051455 | persons-full-adapter-coverage-version-matrix | persons-full-adapter-coverage-version-matrix | persons-400k-plus-append-3x200k-reverse | durability-balanced | sqlite | 25000 | 3 | 1364.129/1378.746/1364.458/1407.651 | 350.761/363.227/352.53/386.391 | 86.592/87.221/87.504/87.567 | 3.256/3.315/3.273/3.416 | 130.098/133.398/131.402/138.694 | 35065856/35065856/35065856/35065856 | 35065856/35065856/35065856/35065856 | 0/0/0/0 | 3/3 | 3/3 |

Metric format is min/avg/median/max. If some runs miss a metric, a suffix like [n=2/3] shows available values.

Comparison notes:
- Comparison set groups related runs and avoids comparing unrelated latest single runs.
- Only measured runs are aggregated into min/max/average/median statistics.
- No policy evaluation is included in this artifact.
