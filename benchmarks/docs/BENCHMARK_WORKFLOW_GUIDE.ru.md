# Руководство по запуску benchmark-платформы

Полный цикл запускается одной командой:

```bash
dotnet run --project .\benchmarks\src\Polar.DB.Bench.Exec\Polar.DB.Bench.Exec.csproj --exp '.\benchmarks\experiments\persons-full-adapter-coverage-version-matrix\'
```

Команда загружает `experiment.json`, выполняет все `targets`, сохраняет сырые результаты, строит analysis/comparison артефакты и обновляет `index.html`.

## Что делает `full-adapter-coverage`

Для каждого target-движка выполняется:

1. Начальная reverse bulk load загрузка `persons(id,name,age)`.
2. Построение структуры lookup/индекса.
3. Опциональный reopen/refresh после начального build (`reopenAfterInitialLoad`).
4. Direct lookup по ключу (`directLookup`).
5. Начальный пакет случайных point lookup (`lookup`).
6. Циклы append (`batches` x `batchSize`).
7. Опциональный reopen/refresh после каждого batch (`reopenAfterEachBatch`).
8. Опциональная случайная lookup-выборка после каждого batch (`randomLookupAfterEachBatch`, `randomLookupPerBatch`).
9. Финальная фиксация размеров артефактов и рост относительно начального built-состояния.

## Где смотреть результаты

Артефакты эксперимента лежат в:

`benchmarks/experiments/persons-full-adapter-coverage-version-matrix/`

Основные папки:

- `raw/` неизменяемые run-результаты
- `analyzed/` производные analysis-артефакты
- `comparisons/` сравнения движков и истории
- `index.html` итоговый человекочитаемый отчет

## Ручной запуск одного target

```bash
dotnet run --project .\benchmarks\src\Polar.DB.Bench.Exec\Polar.DB.Bench.Exec.csproj --spec '.\benchmarks\experiments\persons-full-adapter-coverage-version-matrix\' --engine sqlite --work '.\benchmarks\work\persons-full-adapter-coverage-version-matrix\sqlite'
```
