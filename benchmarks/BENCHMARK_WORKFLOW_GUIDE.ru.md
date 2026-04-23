# Руководство по рабочему процессу Benchmark Platform

Этот документ объясняет, как запускать и интерпретировать бенчмарки в текущей модели экспериментов:

- один эксперимент = одна папка;
- один эксперимент = один манифест `experiment.json`;
- все выходные артефакты лежат внутри папки эксперимента.

Используйте этот файл как практическую инструкцию для новых разработчиков.

## 1. Каноническая структура эксперимента

Каждый эксперимент лежит в:

`benchmarks/experiments/<experiment-slug>/`

Ожидаемая структура:

- `experiment.json` - канонический манифест (идентичность, dataset, workload, targets, compare flags);
- `raw/` - неизменяемые фактические результаты запусков (`*.run.json`);
- `analyzed/` - локальные интерпретации только этого эксперимента;
- `comparisons/` - все comparison-артефакты и derived expectations;
- `index.html` - человекочитаемая страница эксперимента.

## 2. Границы артефактов

Соблюдайте строгие границы:

- `raw/` = факты от executor, неизменяемые;
- `analyzed/` = локальные производные артефакты (без кросс-сравнений);
- `comparisons/` = сравнение targets, история, межэкспериментный контекст, derived expectations.

Не кладите comparison-артефакты в `analyzed/`.

## 3. База манифеста (`experiment.json`)

Минимальные логические блоки:

- `experiment`, `title`, `description`;
- `dataset`, `workload`, `fairness`;
- `targets`;
- `compare.history`;
- `compare.otherExperiments`.

Семантика runtime для target:

- **Target** — это вариант рантайма, идентифицируемый уникальным ключом (например, `polar-db-current`, `polar-db-2.1.1`, `sqlite`).
- Каждый target указывает **семейство движков** (`engine`) и опционально зафиксированную версию NuGet (`nuget`).
- Один эксперимент может содержать несколько targets из одного семейства движков (например, три варианта Polar.DB: текущий source, NuGet 2.1.1, NuGet 2.1.0).
- Ключ target — это идентификатор варианта рантайма; поле engine идентифицирует семейство движков.

Семантика runtime для семейства движков:

- `polar-db` без `nuget` -> текущий source из репозитория;
- `polar-db` с `nuget` -> зафиксированная версия Polar.DB из NuGet;
- не-Polar engine без `nuget` -> latest NuGet;
- не-Polar engine с `nuget` -> зафиксированная версия NuGet.

## 4. Сквозной процесс

### Шаг 1: запустить executor для каждого target

Для честного series-comparison запускайте targets с одинаковым `comparison set id`.

Пример:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Exec -- \
  --engine polar-db-current \
  --spec benchmarks/experiments/persons-load-build-reopen-random-lookup \
  --work benchmarks/work/polar-series \
  --comparison-set stage4-load-001

dotnet run --project benchmarks/Polar.DB.Bench.Exec -- \
  --engine sqlite \
  --spec benchmarks/experiments/persons-load-build-reopen-random-lookup \
  --work benchmarks/work/sqlite-series \
  --comparison-set stage4-load-001
```

Выход executor записывается в:

`benchmarks/experiments/<experiment>/raw/`

Шаблон имён файлов:

- одиночный запуск: `<timestamp>__<target-key>.run.json`;
- серийный запуск: `<timestamp>__<target-key>__<role>-<seq>.run.json`.

### Шаг 2: запустить analysis

Построить локальные analyzed-артефакты и comparison-артефакты:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Analysis -- \
  --raw-dir benchmarks/experiments/persons-load-build-reopen-random-lookup \
  --compare-experiment persons-load-build-reopen-random-lookup \
  --compare-set stage4-load-001
```

Analysis записывает:

- локальные снимки в `analyzed/` (например, `latest-series.polar-db-current.json`);
- comparison-артефакты в `comparisons/`.

### Шаг 3: запустить charts/reporting

Сгенерировать markdown/csv и обновить `index.html`:

```bash
dotnet run --project benchmarks/Polar.DB.Bench.Charts -- \
  --comparisons benchmarks/experiments/persons-load-build-reopen-random-lookup/comparisons \
  --reports-out benchmarks/experiments/persons-load-build-reopen-random-lookup
```

`index.html` генерируется всегда (без флага `generateHtml`).

## 5. Comparison-артефакты

В папке `comparisons/` должны быть:

- `latest-engines.json` - последнее успешное measured-сравнение targets внутри эксперимента;
- `latest-history.json` - история того же эксперимента во времени (управляется `compare.history`);
- `latest-other-experiments.json` - контекст против указанных других экспериментов (управляется `compare.otherExperiments`);
- при необходимости legacy `*.comparison.json` или `*.comparison-series.json`.

Автоматическое поведение:

- сравнение targets включается автоматически, если в манифесте больше одного target;
- history и other-experiments управляются флагами манифеста.

## 6. Модель содержимого `index.html`

Человекочитаемая страница должна показывать:

1. идентичность эксперимента (title, description, dataset, workload, targets);
2. последнее сравнение targets;
3. историю внутри эксперимента;
4. межэкспериментный контекст (если включён);
5. ссылки на machine-readable артефакты из `raw/`, `analyzed/`, `comparisons/`.

## 7. Правила форматирования чисел в HTML

Основной показ для больших значений - scientific notation.

Примеры:

- bytes: `157907232` -> `1.579 × 10^8 B (150.6 MiB)`;
- milliseconds: `8421.3` -> `8.421 × 10^3 ms (8.421 s)`.

Правила:

- основные ячейки таблиц должны быть читаемыми;
- точное raw-значение должно оставаться в tooltip/title;
- bytes должны показывать бинарные единицы (`KiB`, `MiB`, `GiB`).

## 8. Минимальный набор графиков

Используйте простые статические графики (inline SVG), код должен оставаться поддерживаемым.

Обязательные графики:

1. History chart:
   - x = series/date;
   - y = elapsed median;
   - отдельная линия/бар на каждый target.
2. Phase breakdown chart:
   - load / build / reopen / lookup;
   - latest series по targets.
3. Artifact size chart:
   - primary / side / total bytes;
   - latest series по targets.

Не превращайте reporting в большой frontend-приложение.

## 9. Быстрый чеклист для нового эксперимента

1. Создать `benchmarks/experiments/<slug>/`.
2. Добавить `experiment.json`.
3. Создать подпапки: `raw/`, `analyzed/`, `comparisons/`.
4. Запустить executor для сконфигурированных targets с общим `comparison set id`.
5. Запустить analysis.
6. Запустить charts и пересобрать `index.html`.
7. Проверить:
   - raw-факты лежат в `raw/`;
   - локальная интерпретация лежит в `analyzed/`;
   - все comparison-артефакты лежат в `comparisons/`;
   - `index.html` читается и содержит корректные ссылки.

## 10. Что читать дальше

Для деталей:

- `benchmarks/BENCHMARK_PLATFORM_SPEC.md`
- `benchmarks/docs/BENCHMARK_METHOD.md`
- `benchmarks/docs/RESULT_SCHEMA.md`
- `benchmarks/docs/archive/` — историческая документация stage-1
- `benchmarks/docs/examples/` — примеры policy и baseline файлов

## 11. Очищенная структура верхнего уровня

Каноническая структура бенчмарков теперь состоит из:

- `experiments/` — одна папка на эксперимент, каждая содержит `experiment.json`, `raw/`, `analyzed/`, `comparisons/`, `index.html`
- `docs/` — активная документация
- `docs/archive/` — исторические материалы stage-1
- `docs/examples/` — примеры policy и baseline файлов
- Папки проектов бенчмарков (`Polar.DB.Bench.*`)
- `.gitignore`, `Directory.Build.props`, `BENCHMARK_PLATFORM_SPEC.md`, `BENCHMARK_WORKFLOW_GUIDE.md`

Следующие папки верхнего уровня больше не являются частью активной канонической структуры:

- `contracts/` — примеры файлов перемещены в `docs/examples/`
- `baselines/` — примеры файлов перемещены в `docs/examples/`
- `results/` — устарело; результаты экспериментов хранятся внутри папки каждого эксперимента
- `reports/` — устарело; отчёты генерируются для каждого эксперимента
- `work/` — неканоническое рабочее пространство, создаётся по требованию
