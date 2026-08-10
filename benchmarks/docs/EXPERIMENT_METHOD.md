# Методика экспериментов Polar.DB vs SQLite

> Приложение к черновику статьи `POLAR_DB_ARTICLE_DRAFT.md`. Этот файл описывает экспериментальный протокол настолько подробно, чтобы другой исследователь или агент мог восстановить смысл benchmark results без чтения истории чатов.

## 1. Цель протокола

Benchmark harness предназначен для сравнения **одинаковых логических операций**, а не одинаковых внутренних алгоритмов двух engine.

Polar.DB и SQLite различаются моделью хранения, индексами и lifecycle. Поэтому протокол запрещает сводить разные стадии к одной метрике. В частности:

- setup/load не включается в lookup-only timing;
- build измеряется отдельно;
- `open-only` отделён от `query-ready reopen`;
- volatile mutation отделена от операции с persistence boundary;
- point lookup отделён от массового equal-range lookup;
- expected result проверяется независимо от времени выполнения.

Сравнение считается содержательным только внутри одной строки/фазы с одинаковой семантикой результата.

## 2. Зафиксированная публикационная серия

Основной набор результатов статьи:

- series/run id: `20260810T030729570Z`;
- commit: `e093da0247ec58c7fb78fc381eca52fa002b0967`;
- git working tree: clean;
- build configuration: Release;
- `publicationReady = true`;
- .NET runtime: 10.0.7;
- framework description: `.NET 10.0.7`;
- OS reported by runtime: `Microsoft Windows 10.0.26200`;
- OS/process architecture: x64/x64;
- logical processor count visible to process: 12;
- Server GC: `false`;
- Tiered Compilation: runtime default;
- Tiered PGO: runtime default;
- ReadyToRun: runtime default;
- Polar.DB assembly version: 2.1.3.0;
- Microsoft.Data.Sqlite assembly version: 9.0.4.0;
- culture: `ru-RU`;
- time zone: `N. Central Asia Standard Time`.

Manifest фиксирует только generic CPU identifier (`Intel64 Family 6 Model 151 Stepping 5, GenuineIntel`) и объём логического диска. Для статьи дополнительно требуется точная модель CPU, RAM и storage device; поля для них приведены в разделе 15.

## 3. Структура запуска

Обычный запуск benchmark executable работает как coordinator.

Coordinator:

1. определяет run id;
2. записывает environment manifest;
3. выбирает порядок engine;
4. запускает SQLite worker отдельным процессом;
5. запускает Polar.DB worker отдельным процессом;
6. читает worker raw results;
7. проверяет совместимость среды и формы результатов;
8. объединяет результаты;
9. сохраняет raw JSON/CSV, manifest и HTML report.

SQLite и Polar.DB **не измеряются в одном процессе**. Это уменьшает взаимное влияние managed heap, static state, открытых handles и JIT state.

Worker commit обязан совпадать с coordinator commit. Build configuration и признак отключённых JIT optimizations также должны совпадать. При несовпадении coordinator завершает серию ошибкой.

## 4. Порядок engine

Если `POLAR_BENCH_ENGINE_ORDER` не задан, порядок SQLite/Polar.DB выбирается детерминированно из experiment id и run id. Поэтому в полной серии один engine не обязан всегда выполняться первым.

Для диагностических запусков порядок можно принудительно задать:

```powershell
$env:POLAR_BENCH_ENGINE_ORDER = 'sqlite-first'
```

или

```powershell
$env:POLAR_BENCH_ENGINE_ORDER = 'polar-first'
```

Для публикационной серии специально фиксировать один порядок не рекомендуется: это может создать систематическое преимущество первого или второго процесса.

## 5. Размеры наборов

Все текущие эксперименты выполняются на двух размерах:

- 50 000 записей;
- 5 000 000 записей.

В статье основной акцент сделан на 5 млн записей как на более показательном размере. Результаты 50 тыс. остаются частью серии и должны использоваться для проверки scaling behavior и отсутствия аномалий.

## 6. Логическая запись

Synthetic dataset строится детерминированно. Каждая запись содержит:

- integer primary identifier;
- long key;
- Guid key;
- string key;
- integer external key;
- long external key;
- Guid external key;
- string external key;
- payload.

Для обычных external-key сценариев значение external key зависит от `id mod 1000`. Таким образом, на 5 млн записей существует 1000 групп примерно по 5000 строк.

Для `famous external` сценария 40% записей (`id mod 5` равно 0 или 1) получают один и тот же hit key. На 5 млн записей один запрос по этому ключу возвращает 2 млн строк. Остальные записи распределяются между другими значениями.

Dataset не читается из внешнего файла и не зависит от генератора случайных чисел ОС. Это позволяет воспроизводить expected row counts/checksums.

## 7. Каталог из 16 экспериментов

Полная серия содержит:

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

Названия описывают отдельные исследовательские вопросы; результаты разных experiment kinds не следует смешивать в одну latency metric.

## 8. Primary-key lookup protocol

Для `int`, `long`, `Guid` и `string` primary key применяются одинаковые sampling rules.

### 8.1. Cold after reopen

- file warmup: нет;
- отдельный lookup warmup: нет;
- measured batches: 30;
- queries per batch: 100;
- batch queries: 3000;
- single-query latency samples: 2000;
- всего measured queries с учётом latency set: 5000.

### 8.2. Hot after file and lookup warmup

- выполняется explicit file warmup;
- warmup samples: 5;
- 100 queries на warmup sample;
- итого warmup queries: 500;
- measured batches: 100;
- queries per batch: 100;
- batch queries: 10 000;
- single-query latency samples: 2000;
- итого measured queries: 12 000.

Batch key set и latency key set различаются и имеют независимые expected values.

### 8.3. Выбор ключей

Ключи выбираются детерминированно из dataset. Последовательность не является простым проходом по соседним строкам: индекс исходной строки вычисляется через мультипликативный шаг, что рассеивает обращения по набору. Для cold, hot, warmup и latency используются разные seed offsets.

Это уменьшает риск случайного измерения только локального участка индекса.

## 9. Обычный external-key lookup

Для неуникальных external keys цель batch planner — получить примерно 20 000 returned rows на один measured sample.

На наборе 5 млн:

- около 5000 строк на один key;
- поэтому один batch sample содержит 4 lookup queries;
- cold: 15 measured batches;
- hot: 30 measured batches;
- hot warmup: 3 samples;
- latency: 100 отдельных queries.

В raw series это даёт 120 hot batch queries и около 600 000 materialized rows на engine для каждого ordinary external experiment.

Latency здесь означает latency одного запроса, который возвращает множество строк; её нельзя напрямую сопоставлять с one-row primary lookup.

## 10. Famous external-key lookup

Высокочастотный key возвращает 40% dataset.

На 5 млн записей:

- 2 000 000 строк/query;
- 1 query на measured batch;
- cold measured batches: 5;
- hot measured batches: 5;
- hot warmup samples: 2;
- latency samples: 5.

Сценарий намеренно стрессовый и проверяет поведение equal-range path, когда стоимость поиска границы мала по сравнению с обходом/materialization огромного результата.

Он не должен трактоваться как «типичный запрос».

## 11. Build experiment

`build-primary-int-only-id-only-experiment` изолирует построение integer primary index для id-only storage.

В publication raw series на каждый размер применены:

- warmup operations: 3;
- measured operations: 10.

Metric: `build + flush`.

В эту величину нельзя включать выводы о полном ETL, создании всех secondary indexes или произвольной схеме записи. Она отвечает только на вопрос стоимости изолированной операции, заданной этим experiment kind.

## 12. Mutation protocol

### 12.1. Volatile mutation

`append-only` и `delete-only` измеряют 2000 отдельных операций после 200 warmup operations.

Volatile metric заканчивается **до** общего persistence boundary. Поэтому она характеризует стоимость изменения рабочего состояния engine.

Она особенно важна для понимания внутренней append/update path, но не является полной durable latency.

### 12.2. Durable mutation

Дополнительно выполняются batches по 100 операций:

- 2 warmup batches;
- 15 measured batches;
- batch size: 100.

В report записывается **среднее время одной операции внутри batch**, включающего persistence boundary.

Для SQLite durable boundary включает:

- transaction commit;
- WAL checkpoint;
- file sync.

Для Polar.DB:

- `Flush`;
- file sync.

Именно durable metric следует использовать при сравнении практической стоимости гарантированно протолкнутой на storage группы изменений.

Volatile и durable показатели нельзя объединять или выдавать один за другой без указания semantics.

## 13. Reopen protocol

Reopen разделён на две величины.

### 13.1. Open-only

Измеряется открытие и закрытие storage handles без требования выполнить indexed query.

Это отвечает на вопрос стоимости доступа к уже существующим файлам на уровне handles/initial open.

### 13.2. Query-ready reopen

Измерение включает:

1. open;
2. metadata/index readiness;
3. один indexed primary-key lookup.

Поэтому query-ready reopen намеренно включает всю подготовку, необходимую приложению для немедленного indexed read.

Для текущей Polar.DB это существенно: static primary hash/key state и offsets поднимаются в RAM. Поэтому `open-only` и `query-ready` различаются на несколько порядков.

Для reopen:

- warmup operations: 5;
- measured operations: 30.

Нельзя сравнивать Polar.DB `query-ready` с SQLite `open-only` или наоборот.

## 14. Correctness protocol

Timing result принимается только вместе с проверкой логического результата.

### 14.1. Lifecycle

Для каждого dataset заранее вычисляется expected:

- row count;
- checksum.

После выполнения engine возвращает фактические row count/checksum. Они должны совпадать.

### 14.2. Lookup

Для каждой cold/hot phase отдельно вычисляются:

- expected rows для batch key set;
- expected checksum для batch key set;
- expected rows для latency key set;
- expected checksum для latency key set.

Batch и latency sets проверяются независимо.

В серии `20260810T030729570Z` все проверенные expected/factual значения совпали; при разборе архива было проверено 112 соответствий из 112.

Такой подход предотвращает ситуацию, когда engine выглядит «быстрее» из-за пропущенных строк, неправильного key set или неполной materialization.

## 15. Измеряемые величины

Raw artifacts содержат исходные samples; HTML является производным представлением.

### Lookup

Для batch phase:

- `Batches count`;
- `Queries/batch`;
- `Total queries`;
- returned rows;
- returned rows/query;
- массив `batchAvgSamplesMs`.

Каждое значение `batchAvgSamplesMs` — среднее время **одного query внутри данного measured batch**, а не длительность всего batch.

Для latency phase:

- один query на sample;
- `latencySamplesMs`;
- batch size в CSV равен 1.

### Lifecycle

Хранятся массивы raw milliseconds:

- `samplesMs`;
- при необходимости `openSamplesMs`;
- для mutations `durableSamplesMs`.

В статье используются median values raw samples, если явно не указано иное. HTML дополнительно содержит trimmed statistics и throughput derivatives.

### Artifact size

`artifactBytes` — суммарный размер файлов, сформированных engine в рамках соответствующего benchmark case. Это disk footprint конкретного benchmark artifact, а не оценка RAM usage и не «размер СУБД вообще».

### Resource snapshots

Перед и после измеряемых фаз фиксируются, где применимо:

- available system memory;
- managed heap bytes;
- private bytes;
- working set bytes.

Эти snapshots полезны для диагностики, но в текущем черновике не рассматриваются как точная peak-memory metric.

## 16. Publication-ready gate

Harness помечает run как publication-ready только если:

- build configuration = Release;
- JIT optimizer не отключён.

Manifest также сохраняет явные overrides:

- `DOTNET_TieredCompilation` / `COMPlus_TieredCompilation`;
- `DOTNET_TieredPGO` / `COMPlus_TieredPGO`;
- `DOTNET_ReadyToRun` / `COMPlus_ReadyToRun`.

В основной серии все три оставлены `runtime-default`.

Кроме этого coordinator проверяет одинаковый commit и совместимые build settings всех worker processes.

Debug/smoke runs не должны смешиваться с publication tables.

## 17. Хранение артефактов

Latest derived artifacts:

- `<experiment>.html`;
- `<experiment>.manifest.json`;
- `<experiment>.raw.json`;
- `<experiment>.raw.csv`.

Immutable series artifacts:

```text
benchmarks/results/raw/<experiment>/<run-id>/
```

Для основной серии:

```text
benchmarks/results/raw/<experiment>/20260810T030729570Z/
```

Внутри сохраняются:

- `combined.raw.json`;
- `manifest.json`;
- `samples.csv`;
- `sqlite.worker.json`;
- `polar-db.worker.json`.

Raw JSON/CSV являются первичными данными для количественного анализа; HTML следует рассматривать как derived report.

## 18. Полный запуск

Из корня repository:

```powershell
pwsh -File .\benchmarks\scripts\run-new-benchmarks.ps1
```

Скрипт присваивает один `POLAR_BENCH_RUN_ID` всей серии и последовательно запускает текущий каталог экспериментов.

Перед публикационным запуском рекомендуется:

```powershell
git status --short
git branch --show-current
git rev-parse HEAD

$solution = Get-ChildItem -File -Filter *.slnx | Select-Object -First 1

dotnet restore $solution.FullName
dotnet build $solution.FullName -c Release --no-restore
dotnet test $solution.FullName -c Release --no-build
```

Publication benchmark следует запускать только на ожидаемом commit, clean working tree и после зелёного полного test gate.

## 19. Интерпретационные ограничения

### Cold не означает аппаратно «холодный диск»

Cold phase означает отсутствие explicit file warmup в harness после reopen. Она не выполняет принудительный сброс OS page cache. Поэтому термин должен использоваться как **cold according to benchmark protocol**, а не как гарантированный physical cold-start.

### Hot не означает отсутствие всех системных шумов

Hot phase выполняет explicit file/lookup warmup, но по-прежнему измеряется на обычной ОС и может испытывать scheduling, background IO, thermal/power-management effects.

### Массовая выдача и point lookup — разные задачи

0,004 мс для one-row primary lookup нельзя сравнивать с 20–5000 мс для запроса, материализующего тысячи или миллионы строк, без нормализации на returned rows и описания semantics.

### Volatile mutation не является durable write

Большие коэффициенты преимущества volatile append/delete должны сопровождаться durable metric. Иначе сравнение будет методологически вводить в заблуждение.

### Reopen включает разный объём архитектурно необходимой работы

Query-ready определён одинаковой внешней целью — открыть storage и быть способным выполнить indexed primary lookup. Внутренние действия engine закономерно различаются. Большая разница является результатом архитектуры, а не ошибкой fairness, пока внешний postcondition одинаков.

## 20. Дополнительное сравнение версии primary offset cache

Для анализа изменения primary lookup используется предыдущая publication-ready серия:

- run id: `20260808T095444545Z`;
- commit: `3f8476f34b72b1ec18f875b3ac35143a00adac42`.

Текущая серия:

- run id: `20260810T030729570Z`;
- commit: `e093da0247ec58c7fb78fc381eca52fa002b0967`.

Между этими точками production diff затрагивает `UKeyIndex`: static offsets primary snapshot стали храниться в RAM рядом с key/hash array. Остальные изменения в рассматриваемом diff — regression tests.

Наблюдаемое изменение hot primary lookup на 5 млн:

- int: −23,1%;
- long: −28,6%;
- Guid: −35,0%;
- string: −54,4%.

Query-ready reopen одновременно увеличился примерно на 24,3%.

Это **не строгий causal A/B**, поскольку серии выполнялись в разные моменты. Для публикации этот материал следует называть version-to-version observation. Для оценки причинного эффекта нужен отдельный interleaved A/B run на одной неизменной системе.

## 21. Аппаратная среда: поля, которые ещё требуется зафиксировать

Перед финальной версией статьи необходимо добавить:

- точную модель CPU;
- physical cores / logical processors;
- nominal/max clock;
- объём RAM;
- число модулей RAM;
- configured memory speed;
- модель physical disk, на котором расположен `D:\projects\Polar.DB`;
- media type и bus type (NVMe/SATA и т. п.);
- размер volume и свободное место в момент описания;
- точную редакцию/version/build Windows;
- активный Windows power plan;
- `dotnet --info`;
- installed .NET SDKs/runtimes;
- commit и git cleanliness, если среда фиксируется повторно.

Эти данные не нужно смешивать с benchmark manifest: manifest остаётся машинно записанным evidence конкретного run, а hardware appendix дополняет его человекочитаемым описанием платформы.

## 22. Шаблон описания среды для статьи

После получения system snapshot раздел можно заполнить в форме:

> Эксперименты выполнялись на одном компьютере под управлением Windows [edition/version/build], x64. Процессор — [CPU], [physical] физических и [logical] логических ядер/потоков. Оперативная память — [RAM] GB, [modules] модулей, configured speed [MHz]. Рабочие файлы benchmark размещались на [disk model], [media/bus type], volume D:. Использовался .NET SDK [SDK], runtime .NET 10.0.7 x64; benchmark assemblies собирались в Release. На момент publication series commit Polar.DB был `e093da0247ec58c7fb78fc381eca52fa002b0967`, рабочее дерево — clean.

## 23. Что допустимо утверждать по текущей серии

Допустимо:

- указывать measured medians для конкретного experiment/phase/row count;
- сравнивать engines внутри одной одинаково определённой операции;
- обсуждать trade-off между fast primary steady-state lookup и expensive query-ready reopen;
- обсуждать disk artifact footprint;
- отмечать, что heavy external result materialization сильнее у SQLite в данной серии.

Недопустимо без дополнительных экспериментов:

- «Polar.DB быстрее SQLite вообще»;
- переносить результаты Windows на Linux;
- считать volatile append полной durable latency;
- считать `cold after reopen` полностью очищенным OS cache;
- приписывать весь version-to-version speedup только одной micro-optimization как строго доказанный causal effect;
- экстраполировать 5 млн на произвольные размеры данных без scaling study.
