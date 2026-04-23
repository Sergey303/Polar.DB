# Cross-Target Comparison Summary

Generated at: 2026-04-23T18:32:04.1574073+00:00

This summary compares measured runs inside the same comparison set.
Fairness profile means each target maps one shared intent (durability/performance balance) to engine-specific settings.
Primary bytes are the main data file(s). Side bytes are WAL/state/index/other side artifacts.
Technical success means run infrastructure completed. Semantic success means workload-level checks passed.

| ComparisonId | Set | Experiment | Experiment label | Dataset | Fairness | Target | Lookup batch avg | Measured runs | Elapsed ms (min/avg/med/max) | Load ms (min/avg/med/max) | Build ms (min/avg/med/max) | Reopen ms (min/avg/med/max) | Lookup ms (min/avg/med/max) | Total bytes (min/avg/med/max) | Primary bytes (min/avg/med/max) | Side bytes (min/avg/med/max) | Technical success | Semantic success |
| --- | --- | --- | --- | --- | --- | --- | ---: | ---: | --- | --- | --- | --- | --- | --- | --- | --- | ---: | ---: |
| 2026-04-23T18-32-01Z__persons-full-adapter-coverage-version-matrix__persons-400k-plus-append-3x200k-reverse__simple-20260423T183042__multi-target | simple-20260423T183042 | persons-full-adapter-coverage-version-matrix | persons-full-adapter-coverage-version-matrix | persons-400k-plus-append-3x200k-reverse | durability-balanced | polar-db-2.1.0 | 25000 | 3 | 5763.858/5825.86/5797.434/5916.288 | 93.568/110.699/96.824/141.705 | 291.701/329.357/344.165/352.204 | 1251.669/1283.925/1271.699/1328.408 | 753.637/758.248/758.75/762.358 | 21688936/21688936/21688936/21688936 | 16888904/16888904/16888904/16888904 | 4800032/4800032/4800032/4800032 | 3/3 | 3/3 |
| 2026-04-23T18-32-01Z__persons-full-adapter-coverage-version-matrix__persons-400k-plus-append-3x200k-reverse__simple-20260423T183042__multi-target | simple-20260423T183042 | persons-full-adapter-coverage-version-matrix | persons-full-adapter-coverage-version-matrix | persons-400k-plus-append-3x200k-reverse | durability-balanced | polar-db-2.1.1 | 25000 | 3 | 5547.794/5635.324/5571.805/5786.375 | 92.357/96.344/93.705/102.969 | 260.108/272.377/265.129/291.894 | 1267.65/1293.963/1286.098/1328.141 | 717.68/731.617/722.787/754.383 | 21688936/21688936/21688936/21688936 | 16888904/16888904/16888904/16888904 | 4800032/4800032/4800032/4800032 | 3/3 | 3/3 |
| 2026-04-23T18-32-01Z__persons-full-adapter-coverage-version-matrix__persons-400k-plus-append-3x200k-reverse__simple-20260423T183042__multi-target | simple-20260423T183042 | persons-full-adapter-coverage-version-matrix | persons-full-adapter-coverage-version-matrix | persons-400k-plus-append-3x200k-reverse | durability-balanced | polar-db-current | 25000 | 3 | 5706.314/5769.621/5747.3/5855.25 | 90.241/90.412/90.339/90.656 | 267.722/278.835/269.285/299.498 | 1285.6/1321.851/1316.256/1363.696 | 729.737/755.517/749.054/787.76 | 21688936/21688936/21688936/21688936 | 16888904/16888904/16888904/16888904 | 4800032/4800032/4800032/4800032 | 3/3 | 3/3 |
| 2026-04-23T18-32-01Z__persons-full-adapter-coverage-version-matrix__persons-400k-plus-append-3x200k-reverse__simple-20260423T183042__multi-target | simple-20260423T183042 | persons-full-adapter-coverage-version-matrix | persons-full-adapter-coverage-version-matrix | persons-400k-plus-append-3x200k-reverse | durability-balanced | sqlite | 25000 | 3 | 1509.366/1551.875/1518.057/1628.201 | 379.815/402.283/396.524/430.51 | 90.405/94.527/94.002/99.175 | 3.298/3.554/3.52/3.844 | 140.994/148.943/146.575/159.26 | 35065856/35065856/35065856/35065856 | 35065856/35065856/35065856/35065856 | 0/0/0/0 | 3/3 | 3/3 |

Metric format is min/avg/median/max. If some runs miss a metric, a suffix like [n=2/3] shows available values.

Comparison notes:
- Comparison set groups related runs and avoids comparing unrelated latest single runs.
- Only measured runs are aggregated into min/max/average/median statistics.
- No policy evaluation is included in this artifact.
