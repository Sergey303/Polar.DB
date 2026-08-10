# Среда публикационного benchmark-прогона Polar.DB

> Дополнение к `POLAR_DB_ARTICLE_DRAFT.md` и `EXPERIMENT_METHOD.md`. Данные ниже относятся к publication-ready серии `20260810T030729570Z` и были повторно сняты 2026-08-10 на том же `main` commit `e093da0247ec58c7fb78fc381eca52fa002b0967` при clean working tree.

## 1. Репозиторий и серия

- Repository path: `D:\projects\Polar.DB`.
- Branch при снимке среды: `main`.
- Commit: `e093da0247ec58c7fb78fc381eca52fa002b0967`.
- Working tree: clean.
- Publication benchmark run id: `20260810T030729570Z`.
- Build configuration в benchmark manifests: `Release`.
- `publicationReady`: `true`.

Аппаратный снимок был получен после benchmark-прогона, но на том же commit и clean branch. Он дополняет автоматически записанные manifests; не заменяет их.

## 2. Операционная система

- Edition: Microsoft Windows 11 Pro.
- Version: `10.0.26200`.
- Build: `26200`.
- Architecture: x64.
- Active power plan: `Сбалансированная` (`381b4222-f694-41f0-9685-ff5bb260df2e`).

Benchmark manifest сообщает ту же NT version как `Microsoft Windows 10.0.26200`; это runtime description и не следует интерпретировать как Windows 10 edition. WMI snapshot идентифицирует систему как Windows 11 Pro.

## 3. Процессор

- CPU: `12th Gen Intel(R) Core(TM) i5-12400`.
- Manufacturer: GenuineIntel.
- Physical cores: 6.
- Logical processors: 12.
- Logical processors, visible to .NET process: 12.
- WMI `MaxClockSpeed`: 2500 MHz.
- L2 cache reported by WMI: 7680 KiB.
- L3 cache reported by WMI: 18432 KiB.
- OS/process bitness: 64-bit / 64-bit.

`MaxClockSpeed` здесь является значением, возвращённым WMI; оно не используется как утверждение о фактической частоте каждого benchmark sample.

## 4. Оперативная память

- Total physical memory reported by system: 31.78 GiB.
- Installed modules: 4 × 8 GiB.
- Configured clock speed всех модулей: 3200 MT/s по данным WMI.

Модули:

| Module | Capacity | Manufacturer | Part number | WMI Speed | ConfiguredClockSpeed |
|---|---:|---|---|---:|---:|
| 1 | 8 GiB | Patriot Memory (PDP Systems) | `4400 C19 Series` | 2133 | 3200 |
| 2 | 8 GiB | Gloway International Co Ltd | `TAC4U3200E16081C` | 2400 | 3200 |
| 3 | 8 GiB | Patriot Memory (PDP Systems) | `4400 C19 Series` | 2133 | 3200 |
| 4 | 8 GiB | Gloway International Co Ltd | `TAC4U3200E16081C` | 2400 | 3200 |

Конфигурация смешанная: используются два типа модулей, однако WMI сообщает одинаковую configured speed 3200 для всех четырёх. В статье достаточно указывать 32 GiB (31.78 GiB visible), 4 × 8 GiB, 3200 MT/s; модели модулей сохраняются здесь для воспроизводимости.

## 5. Накопитель benchmark workspace

Repository и benchmark working files располагались на volume `D:`.

Physical disk:

- Model: `Samsung SSD 980 PRO 250GB`.
- Media type: SSD.
- Bus type: NVMe.
- Partition style: GPT.
- Physical size reported by Windows: 232.89 GiB.

Volume `D:`:

- File system: NTFS.
- Volume size: 232.16 GiB.
- Free space при environment snapshot: 19.56 GiB.

Свободное место было сравнительно небольшим, поэтому его следует сохранять в appendix как возможный фактор IO-sensitive воспроизводимости. Benchmark manifests также записывают total/available bytes volume для каждого процесса конкретного run.

## 6. .NET

### 6.1. Активный SDK и host

`dotnet --info` при environment snapshot:

- Active SDK: `.NET SDK 10.0.203`.
- SDK commit: `c23858a6d8`.
- MSBuild: `18.3.3+c23858a6d`.
- Host/runtime version: `10.0.7`.
- Host architecture: x64.
- RID: `win-x64`.

Benchmark manifests основной серии также фиксируют runtime `.NET 10.0.7`.

### 6.2. Установленные SDK

- `8.0.420`.
- `10.0.100`.
- `10.0.203`.

### 6.3. Установленные runtimes

- Microsoft.AspNetCore.App 10.0.0.
- Microsoft.AspNetCore.App 10.0.7.
- Microsoft.NETCore.App 8.0.26.
- Microsoft.NETCore.App 9.0.11.
- Microsoft.NETCore.App 10.0.0.
- Microsoft.NETCore.App 10.0.7.
- Microsoft.WindowsDesktop.App 8.0.26.
- Microsoft.WindowsDesktop.App 9.0.11.
- Microsoft.WindowsDesktop.App 10.0.0.
- Microsoft.WindowsDesktop.App 10.0.7.

### 6.4. Runtime overrides

При environment snapshot не обнаружено явных переменных `DOTNET_*` / `COMPlus_*`, соответствующих Tiered Compilation, ReadyToRun и GC-настройкам, которые собирал скрипт. Benchmark manifests основной серии также записали Tiered Compilation, Tiered PGO и ReadyToRun как `runtime-default`, а Server GC — `false`.

## 7. Важный нюанс `global.json`

На benchmark commit в repository присутствует:

```json
{
  "sdk": {
    "version": "10.0.0",
    "rollForward": "latestMajor",
    "allowPrerelease": true
  }
}
```

`dotnet --info` сообщает, что `10.0.0` является недопустимым значением `sdk/version`: SDK feature bands начинаются с `x.y.100`. Поэтому `global.json` **не обеспечивал корректный SDK pin** для этой публикационной серии; фактически environment snapshot выбрал установленный SDK `10.0.203`.

Это не делает raw benchmark недействительным: manifest фиксирует фактический runtime, commit, Release build и publication-ready state. Однако для повторяемости будущих запусков `global.json` следует отдельно исправить и затем считать такие запуски новой экспериментальной серией, а не задним числом менять описание уже выполненного run.

## 8. Версии библиотек в benchmark manifest

Для publication series:

- Polar.DB assembly: `2.1.3.0`.
- Microsoft.Data.Sqlite assembly: `9.0.4.0`.

Эти версии относятся к реально загруженным benchmark worker assemblies и предпочтительнее предположений из package files.

## 9. Рекомендуемая краткая формулировка для статьи

> Эксперименты выполнялись под Windows 11 Pro build 26200 x64 на Intel Core i5-12400 (6 физических ядер, 12 логических процессоров) с 31,78 GiB доступной физической памяти (4 × 8 GiB, configured speed 3200 MT/s). Рабочие данные размещались на Samsung SSD 980 PRO 250GB NVMe, volume NTFS `D:`. Использовались .NET host 10.0.7 x64 и Release-сборки; при снимке среды активным SDK был 10.0.203. Основная серия соответствует commit `e093da0247ec58c7fb78fc381eca52fa002b0967` и clean working tree.

## 10. Что не следует утверждать

- Нельзя утверждать, что CPU работал на постоянных 2.5 GHz: это только значение WMI `MaxClockSpeed`.
- Нельзя считать free space 19.56 GiB точным значением на момент каждого отдельного benchmark worker: это snapshot после серии; для run-specific volume values нужно использовать manifests.
- Нельзя утверждать, что `global.json` закреплял SDK 10.0.203: наоборот, файл был некорректен.
- Нельзя переносить результаты этой Windows/NVMe/i5-12400 машины на другую платформу без нового запуска.
