# Методика экспериментов Polar.DB vs SQLite

> Приложение к `POLAR_DB_ARTICLE_DRAFT.md`. Этот файл описывает экспериментальный протокол так, чтобы смысл raw benchmark results можно было восстановить без истории чатов. Точная аппаратная и .NET-среда вынесена также в `EXPERIMENT_ENVIRONMENT.md`.

## 1. Цель протокола

Benchmark harness сравнивает **одинаковые логические операции**, а не одинаковые внутренние алгоритмы двух engine.

Polar.DB и SQLite различаются моделью хранения, индексами и lifecycle. Поэтому протокол разделяет стадии, которые нельзя корректно сводить в одну latency metric:

- setup/load не включается в lookup-only timing;
- build измеряется отдельно;
- `open-only` отделён от `query-ready reopen`;
- volatile mutation отделена от операции с persistence boundary;
- point lookup отделён от equal-range lookup, возвращающего тысячи или миллионы строк;
- correctness результата проверяется независимо от elapsed time.

Сравнение интерпретируется только внутри одинаково определённой операции и одинакового postcondition.

## 2. Основная publication-ready серия

Основные результаты статьи получены в серии:

- run id: `20260810T030729570Z`;
- repository commit: `e093da0247ec58c7fb78fc381eca52fa002b0967`;
- git working tree: clean;
- build configuration: Release;
- `publicationReady = true`;
- .NET host/runtime: 10.0.7, x64;
- Polar.DB assembly: 2.1.3.0;
- Microsoft.Data.Sqlite assembly: 9.0.4.0;
- Server GC: `false`;
- Tiered Compilation: runtime default;
- Tiered PGO: runtime default;
- ReadyToRun: runtime default.

Environment snapshot после серии был снят на том же `main` commit при clean working tree. Это связывает аппаратное описание с тем же состоянием repository.

## 3. Аппаратная среда

Краткое описание машины:

- Microsoft Windows 11 Pro, version/build `10.0.26200`, x64;
- Intel Core i5-12400;
- 6 физических ядер, 12 логических процессоров;
- 31.78 GiB physical memory visible to Windows;
- 4 × 8 GiB DIMM, configured clock speed 3200 MT/s;
- Samsung SSD 980 PRO 250GB;
- SSD/NVMe;
- GPT;
- benchmark workspace на NTFS volume `D:`;
- volume size 232.16 GiB;
- free space при environment snapshot 19.56 GiB;
- Windows power plan: `Сбалансированная`.

Подробные модели RAM, WMI cache values, volume/disk distinction и ограничения этих показателей приведены в `EXPERIMENT_ENVIRONMENT.md`.

### 3.1. RAM configuration

Использовались четыре 8 GiB модуля двух типов:

- 2 × Patriot Memory `4400 C19 Series`;
- 2 × Gloway `TAC4U3200E16081C`.

WMI сообщает для всех четырёх `ConfiguredClockSpeed = 3200`. Поле `Speed` при этом равно 2133 для Patriot и 2400 для Gloway, поэтому для статьи следует использовать именно формулировку **configured speed 3200 MT/s**, а не утверждать, что SPD/JEDEC nominal speed всех модулей одинаков.

### 3.2. Storage

Physical disk benchmark workspace:

- `Samsung SSD 980 PRO 250GB`;
- MediaType: SSD;
- BusType: NVMe;
- physical size reported by Windows: 232.89 GiB.

Рабочий volume `D:`:

- NTFS;
- 232.16 GiB;
- 19.56 GiB free при post-run environment snapshot.

Свободное место snapshot не считается точным значением для каждого worker. Run-specific total/available volume bytes записываются benchmark manifests.

## 4. .NET environment

Environment snapshot показывает:

- active SDK: 10.0.203;
- SDK commit: `c23858a6d8`;
- MSBuild: `18.3.3+c23858a6d`;
- host: 10.0.7;
- host architecture: x64;
- RID: `win-x64`.

Installed SDKs:

- 8.0.420;
- 10.0.100;
- 10.0.203.

Installed Microsoft.NETCore.App runtimes:

- 8.0.26;
- 9.0.11;
- 10.0.0;
- 10.0.7.

Также установлены соответствующие WindowsDesktop runtimes и AspNetCore 10.0.0/10.0.7; полный перечень сохранён в `EXPERIMENT_ENVIRONMENT.md`.

При environment snapshot не обнаружено явных `DOTNET_*` / `COMPlus_*` overrides, соответствующих собираемым Tiered/ReadyToRun/GC параметрам. Benchmark manifests также зафиксировали runtime defaults.

## 5. Нюанс `global.json`

На benchmark commit repository содержит:

```json
{
  "sdk": {
    "version": "10.0.0",
    "rollForward": "latestMajor",
    "allowPrerelease": true
  }
}
```

`dotnet --info` сообщает, что `10.0.0` является недопустимым значением `sdk/version`, поскольку SDK feature bands начинаются с `x.y.100`. Следовательно, этот `global.json` **не обеспечивал корректный SDK pin** для publication series.

Фактически при environment snapshot был выбран SDK 10.0.203. Для выполненной серии это нужно трактовать как ограничение воспроизводимости, а не как основание переписывать уже полученные raw results.

Для будущего строгого повторения рекомендуется сначала исправить `global.json`, затем выполнить новую publication series с новым run id. Старую и новую серии нельзя смешивать как один и тот же экспериментальный baseline.

## 6. Process isolation

Обычный запуск benchmark executable работает как coordinator.

Coordinator:

1. определяет run id;
2. записывает environment manifest;
3. выбирает порядок engine;
4. запускает SQLite worker отдельным child process;
5. запускает Polar.DB worker отдельным child process;
6. читает worker raw results;
7. проверяет одинаковый commit и совместимый build state;
8. проверяет форму результатов;
9. объединяет результаты;
10. сохраняет raw JSON/CSV, manifest и HTML report.

SQLite и Polar.DB **не измеряются внутри одного процесса**. Это снижает взаимное влияние managed heap, static state, открытых handles и JIT state.

Worker commit обязан совпадать с coordinator commit. Build configuration и признак отключённых JIT optimizations также должны совпадать. Несовпадение делает run invalid.

## 7. Engine order

Если `POLAR_BENCH_ENGINE_ORDER` не задан, порядок SQLite/Polar.DB определяется детерминированно из experiment id и run id.

Для диагностических запусков допускаются:

```powershell
$env:POLAR_BENCH_ENGINE_ORDER = 'sqlite-first'
```

или

```powershell
$env:POLAR_BENCH_ENGINE_ORDER = 'polar-first'
```

Publication series не фиксирует один engine первым во всех экспериментах, чтобы не создавать постоянное order bias.

## 8. Размеры datasets

Все experiment kinds основной серии выполняются на:

- 50 000 записей;
- 5 000 000 записей.

Основной текст статьи показывает прежде всего 5 млн. Набор 50 тыс. остаётся контрольной точкой для scaling behavior и проверки аномалий.

## 9. Synthetic row

Dataset строится детерминированно и не зависит от внешнего файла или OS random source.

Логическая запись содержит:

- integer primary id;
- long primary candidate;
- Guid primary candidate;
- string primary candidate;
- integer external key;
- long external key;
- Guid external key;
- string external key;
- payload.

Детерминированность позволяет заранее вычислить expected row count и checksum для каждой выборки.

## 10. Ordinary external-key distribution

Для обычных external-key сценариев значения распределяются по 1000 группам (`id mod 1000`).

На 5 млн записей один успешный external lookup возвращает около:

```text
5 000 000 / 1000 = 5000 rows
```

Planner подбирает количество queries в measured batch так, чтобы sample материализовал примерно 20 000 строк.

Для 5 млн это обычно 4 queries × ~5000 rows/query.

## 11. Famous external-key distribution

Отдельный stress workload использует high-frequency key.

Запись считается hit, если:

```text
id mod 5 = 0 или 1
```

То есть hit key принадлежит 40% dataset.

На 5 млн записей один hit query возвращает:

```text
2 000 000 rows
```

Этот experiment намеренно экстремален. Он исследует equal-range path при огромном результате и не должен называться «типичным запросом».

## 12. Каталог 16 экспериментов

Полная серия:

1. `build-primary-int-only-id-only-experiment`;
2. `pk-int-lookup`;
3. `pk-long-lookup`;
4. `pk-guid-lookup`;
5. `pk-string-lookup`;
6. `external-int-lookup`;
7. `external-long-lookup`;
8. `external-guid-lookup`;
9. `external-string-lookup`;
10. `external-famous-int-lookup`;
11. `external-famous-long-lookup`;
12. `external-famous-guid-lookup`;
13. `external-famous-string-lookup`;
14. `append-only`;
15. `delete-only`;
16. `reopen-only`.

Каждый experiment отвечает на отдельный исследовательский вопрос. Метрики разных kinds нельзя объединять без явного изменения смысла.

## 13. Primary-key lookup protocol

Одинаковые sampling rules применяются к `int`, `long`, `Guid` и `string` primary lookup.

### 13.1. Cold after reopen

- explicit file warmup: нет;
- lookup warmup: нет;
- measured batches: 30;
- queries/batch: 100;
- batch queries: 3000;
- single-query latency samples: 2000;
- total measured queries: 5000.

### 13.2. Hot after file and lookup warmup

- explicit file warmup: да;
- warmup samples: 5;
- queries/warmup sample: 100;
- warmup queries: 500;
- measured batches: 100;
- queries/batch: 100;
- batch queries: 10 000;
- latency samples: 2000;
- total measured queries: 12 000.

Batch key set и latency key set различаются и имеют независимые expected values.

### 13.3. Выбор ключей

Lookup keys выбираются детерминированно из dataset. Индекс исходной строки вычисляется мультипликативным шагом, поэтому измерение не является последовательным проходом по соседним ids.

Для cold, hot, warmup и latency используются разные seed offsets.

## 14. Ordinary external lookup protocol

Default parameters:

- cold measured batches: 15;
- hot measured batches: 30;
- hot warmup samples: 3;
- latency samples: 100;
- target returned rows/sample: около 20 000.

На 5 млн при ~5000 rows/query planner выбирает 4 queries/batch.

Single-query latency external lookup нельзя напрямую сравнивать с primary one-row latency без учёта returned rows.

## 15. Famous external lookup protocol

Default parameters:

- cold measured batches: 5;
- hot measured batches: 5;
- queries/batch: 1;
- hot warmup samples: 2;
- latency samples: 5.

На 5 млн каждый hit query материализует 2 млн строк.

## 16. Build experiment

`build-primary-int-only-id-only-experiment` изолирует построение integer primary index для id-only storage.

На benchmark commit `Program.cs` задаёт:

- warmup operations: 3;
- measured operations: 10.

Metric: `build + flush`.

Raw results отдельно сохраняют build и flush components, но headline metric относится к их сумме.

Нельзя интерпретировать этот эксперимент как полный database load или построение произвольной secondary-index topology.

## 17. Mutation protocol

### 17.1. Volatile mutation

Для append/delete:

- warmup operations: 200;
- measured operations: 2000.

Volatile sample заканчивается **до** общего persistence boundary.

Она показывает стоимость изменения рабочего состояния, а не полную durable write latency.

### 17.2. Durable mutation

Дополнительные batches:

- warmup batches: 2;
- measured batches: 15;
- batch size: 100 operations.

Report хранит среднее время одной операции внутри batch, включающего persistence boundary.

Для SQLite boundary включает:

- transaction commit;
- WAL checkpoint;
- file sync.

Для Polar.DB:

- `Flush`;
- file sync.

Durable values являются предпочтительными при практическом обсуждении сохранённых изменений.

## 18. Reopen protocol

Reopen разделён на две metrics.

### 18.1. Open-only

Только открытие/закрытие storage handles.

### 18.2. Query-ready reopen

Включает:

1. open;
2. metadata/index readiness;
3. один indexed primary-key lookup.

Это одинаковый внешний postcondition: после измеряемой операции storage способен выполнить indexed primary lookup.

Для Polar.DB query-ready включает подготовку RAM-resident primary state, поэтому open-only и query-ready закономерно различаются значительно.

Parameters:

- warmup operations: 5;
- measured operations: 30.

Нельзя сравнивать Polar.DB query-ready с SQLite open-only или наоборот.

## 19. Correctness protocol

Timing result принимается только вместе с correctness evidence.

### 19.1. Lifecycle

Для dataset заранее вычисляются:

- expected rows;
- expected checksum.

Engine должен вернуть совпадающие фактические значения.

### 19.2. Lookup

Для каждой cold/hot phase независимо вычисляются:

- expected rows для batch key set;
- expected checksum для batch key set;
- expected rows для latency key set;
- expected checksum для latency key set.

Batch и latency sets валидируются отдельно.

При разборе publication series `20260810T030729570Z` все 112 проверенных expected/factual соответствий совпали.

Это исключает «ускорение» за счёт пропущенных строк, неправильного key set или неполной выдачи.

## 20. Значение lookup samples

Для `lookup-batch-average` один raw sample `value_ms` означает **среднее время одного query внутри measured batch**.

То есть при 100 queries/batch sample не является latency всего batch.

Для latency rows:

- batch size = 1;
- sample = один query.

Report также хранит:

- batches count;
- queries/batch;
- total queries;
- returned rows;
- returned rows/query.

## 21. Statistics в статье

Raw JSON/CSV являются первичными данными. HTML — derived report.

В текущем черновике headline timings представлены как **median raw samples**, если явно не указано иное.

Для lookup headline hot numbers берутся из `batchAvgSamplesMs` hot phase.

Для lifecycle используются median соответствующих arrays:

- `samplesMs`;
- `openSamplesMs` для open-only;
- `durableSamplesMs` для durable mutation.

HTML дополнительно выводит trimmed statistics и throughput-derived metrics, но они не должны незаметно подменять median в тексте статьи.

## 22. Artifact size

`artifactBytes` — размер файлов, созданных engine в benchmark case.

Это:

- disk footprint конкретного workload;
- не RAM usage;
- не размер «СУБД вообще»;
- не peak temporary IO.

При обсуждении Polar.DB disk footprint нужно учитывать, что query-ready path использует дополнительные RAM arrays.

## 23. Resource snapshots

Harness сохраняет, где применимо:

- available system memory;
- managed heap bytes;
- process private bytes;
- working set bytes.

Эти значения являются snapshots, а не точной peak-memory telemetry. В текущей статье они используются как diagnostic evidence, но не как строгая peak-memory comparison.

## 24. Publication-ready gate

Run помечается publication-ready только если:

- build configuration = Release;
- JIT optimizer не отключён.

Manifest также сохраняет явные overrides для:

- Tiered Compilation;
- Tiered PGO;
- ReadyToRun.

В основной серии они равны runtime defaults.

Coordinator дополнительно проверяет одинаковый commit и build state worker processes.

Debug/smoke results нельзя смешивать с publication tables.

## 25. Immutable artifacts

Latest derived artifacts:

- `<experiment>.html`;
- `<experiment>.manifest.json`;
- `<experiment>.raw.json`;
- `<experiment>.raw.csv`.

Immutable run artifacts:

```text
benchmarks/results/raw/<experiment>/<run-id>/
```

Для основной серии:

```text
benchmarks/results/raw/<experiment>/20260810T030729570Z/
```

Внутри сохраняются combined raw, manifest, CSV и отдельные worker JSON.

## 26. Полный запуск

Из repository root:

```powershell
pwsh -File .\benchmarks\scripts\run-new-benchmarks.ps1
```

Скрипт:

1. создаёт один series id;
2. задаёт его как `POLAR_BENCH_RUN_ID`;
3. собирает shared benchmark library в Release;
4. последовательно запускает 16 experiment projects в Release.

Перед publication run рекомендуется выполнить полный gate:

```powershell
git status --short
git branch --show-current
git rev-parse HEAD

$solution = Get-ChildItem -File -Filter *.slnx | Select-Object -First 1

dotnet restore $solution.FullName
dotnet build $solution.FullName -c Release --no-restore
dotnet test $solution.FullName -c Release --no-build
```

Run следует начинать только на ожидаемом commit и clean working tree после зелёного полного test suite.

## 27. Интерпретация cold/hot

### Cold

`Cold after reopen` означает отсутствие explicit file/lookup warmup в harness.

Это **не гарантирует очищенный OS page cache**. Поэтому в статье нельзя писать «полностью холодный диск».

### Hot

Hot phase выполняет explicit file warmup и lookup warmup, но остаётся wall-clock измерением на обычной Windows системе.

На результат могут влиять scheduler, power management, background IO и thermal state.

## 28. Power plan и частота CPU

Основная машина работала с Windows power plan `Сбалансированная`.

Протокол не фиксирует CPU clock на постоянном значении и не отключает boost/power-management transitions. Поэтому результаты характеризуют реальную desktop environment пользователя, а не лабораторно закреплённую частоту CPU.

Для будущего отдельного high-rigor run можно либо закрепить power profile, либо явно сравнить несколько профилей, но это будет новая серия.

## 29. Version-to-version primary offset cache observation

Предыдущая publication-ready серия:

- run id: `20260808T095444545Z`;
- commit: `3f8476f34b72b1ec18f875b3ac35143a00adac42`.

Основная серия:

- run id: `20260810T030729570Z`;
- commit: `e093da0247ec58c7fb78fc381eca52fa002b0967`.

Production diff исследуемого primary path добавляет хранение static offsets в RAM рядом с hash/key snapshot.

На 5 млн hot lookup наблюдалось:

- int: 5.284 → 4.065 мкс/query, −23.1%;
- long: 5.488 → 3.921 мкс/query, −28.6%;
- Guid: 5.862 → 3.811 мкс/query, −35.0%;
- string: 10.049 → 4.585 мкс/query, −54.4%.

Query-ready reopen одновременно изменился примерно 564.5 → 701.7 мс, +24.3%.

Эти две полные серии запускались в разные моменты, поэтому сравнение является **version-to-version observation**, а не строгим isolated causal A/B.

## 30. Главные ограничения

1. Одна Windows-машина: i5-12400, 32 GiB, Samsung 980 PRO NVMe.
2. Balanced power plan; CPU frequency не фиксировалась.
3. `global.json` невалидно pin'ит SDK; environment snapshot фактически использует SDK 10.0.203.
4. Cold phase не очищает OS page cache.
5. Ordinary external lookup возвращает тысячи строк/query; famous external — миллионы. Эти latency нельзя сравнивать с one-row point lookup как одинаковую работу.
6. Volatile mutation не является durable write latency.
7. `artifactBytes` не является RAM consumption.
8. Resource snapshots не являются точным peak-memory profile.
9. Version-to-version observation не заменяет interleaved A/B.
10. Результаты Windows/NVMe нельзя без нового run переносить на Linux, SATA или другой CPU.

## 31. Что допустимо утверждать

Допустимо:

- публиковать median конкретного experiment/phase/row count;
- сравнивать engines внутри одной логически одинаковой операции;
- обсуждать measured trade-off между steady-state primary lookup и query-ready reopen;
- сравнивать disk artifact footprint в рамках одинакового workload;
- показывать, что heavy external materialization в этой серии быстрее у SQLite;
- отдельно показывать volatile и durable mutation results.

Недопустимо без дополнительных экспериментов:

- «Polar.DB быстрее SQLite вообще»;
- «cold = физически очищенный диск»;
- «volatile append = durable write»;
- переносить результаты на другую ОС/машину;
- считать весь version-to-version speedup строго причинным эффектом одной строки оптимизации;
- утверждать, что `global.json` закреплял SDK 10.0.203.

## 32. Связанные файлы

- `POLAR_DB_ARTICLE_DRAFT.md` — основной научный текст и наиболее существенные результаты.
- `EXPERIMENT_METHOD.md` — этот протокол.
- `EXPERIMENT_ENVIRONMENT.md` — machine/.NET snapshot и reproducibility notes.
- `benchmarks/docs/README.md` — техническая документация benchmark harness.
