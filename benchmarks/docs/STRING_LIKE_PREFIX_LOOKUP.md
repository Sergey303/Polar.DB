# String LIKE Prefix Lookup

## Цель

Проверить скорость строкового lookup, где SQLite использует `LIKE`, а PolarDB использует эквивалентный prefix comparator/range traversal.

## Почему не только одна строка

Поиск ровно одной строки полезен как sanity check, но он почти превращает `LIKE` в `=`.

Главная серия должна возвращать разное число результатов:

| Case | Pattern | Смысл |
|---|---|---|
| `exact1` | full string | контрольная точка, 1 результат |
| `prefix1` | full string + `%` | prefix LIKE с 1 результатом |
| `prefixSmall` | `grp/sub/%` | десятки или сотни результатов |
| `prefixMedium` | `grp/%` | тысячи результатов |
| `containsScan` | `%sub%` | отдельный scan/filter, не lookup |

## Fairness

Для честного сравнения:

- SQLite обязан выполнять SQL `LIKE`.
- PolarDB обязан выполнять prefix comparator/range traversal для `prefix%`.
- `containsScan` не сравнивается с index lookup как будто это та же операция.

## Dataset

Имя записи имеет форму:

```text
grp0042/sub0007/item00012345
```

Это даёт управляемую селективность:

- полный item — 1 запись;
- `grp/sub/` — малая группа;
- `grp/` — средняя группа;
- `sub` внутри строки — scan-сценарий.

## Lifecycle

Эксперимент специально разделяет подготовку на три фазы:

- `Load` — запись данных;
- `Build` — построение индекса и фиксация состояния;
- `Reopen` — закрытие и повторное открытие перед lookup.

Это нужно, чтобы увидеть цену текущих инвариантов PolarDB вокруг logical end, `AppendOffset`, state и recovery.

## Метрики

Минимальный набор:

- load elapsed;
- build elapsed;
- reopen elapsed;
- matched count;
- elapsed trimmed mean;
- elapsed p95;
- min/max;
- query count;
- environment snapshot;
- engine diagnostics;
- artifact bytes.

## Проверка корректности

Для каждого case нужно проверять, что `MatchedCount` совпадает между SQLite и PolarDB.

Если counts отличаются, время сравнивать нельзя: сначала надо чинить семантику workload.
