# run-all-experiments-parallel.ps1

Версия v4.

Главное исправление:

- Используется реальный CLI текущего `Polar.DB.Bench.Exec`:
  - правильно: `--exp <experiment-folder-or-name>`
  - неправильно: `--experiment`
  - неправильно: `--results-root`

Поэтому ошибка `Unknown argument: '--experiment'` в этой версии должна уйти.

## Правила

- Launcher не использует `benchmarks\work`.
- Launcher-логи кладутся внутрь папки каждого эксперимента:

    benchmarks\experiments\<name>\artifacts\<timestamp>\_launcher

- При падении stderr/stdout раскрываются в консоль.
- Полный отчёт падения:

    benchmarks\experiments\<name>\artifacts\<timestamp>\_launcher\failure-full.txt

Важно: текущий `Bench.Exec` CLI не принимает output root. Поэтому launcher может положить свои логи в папку эксперимента, но если сам C# runner внутри `RunPaths` пишет куда-то ещё, это надо править в C# коде runner-а.

## Установка

Из корня репозитория:

    cd D:\projects\Polar.DB

    Expand-Archive -LiteralPath "$env:USERPROFILE\Downloads\polardb_run_all_experiments_parallel_v4.zip" -DestinationPath "D:\projects\Polar.DB" -Force

## Запуск

    cd D:\projects\Polar.DB

    powershell -ExecutionPolicy Bypass -File .\benchmarks\scripts\run-all-experiments-parallel.ps1 -MaxParallel 4

Для тяжёлых storage-экспериментов лучше начать с двух параллельных процессов:

    powershell -ExecutionPolicy Bypass -File .\benchmarks\scripts\run-all-experiments-parallel.ps1 -MaxParallel 2

Проверить список без запуска:

    powershell -ExecutionPolicy Bypass -File .\benchmarks\scripts\run-all-experiments-parallel.ps1 -DryRun

Запуск с быстрыми настройками количества прогонов, если поддержано experiment runner-ом:

    powershell -ExecutionPolicy Bypass -File .\benchmarks\scripts\run-all-experiments-parallel.ps1 -WarmupCount 0 -MeasuredCount 1 -MaxParallel 2

Жёстко упасть, если папка `benchmarks\work` уже существует:

    powershell -ExecutionPolicy Bypass -File .\benchmarks\scripts\run-all-experiments-parallel.ps1 -FailIfWorkDirectoryExists
