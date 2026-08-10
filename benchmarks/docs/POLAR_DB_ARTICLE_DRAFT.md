# Polar.DB: экспериментальное исследование компромиссов между индексированным чтением, мутациями и восстановлением состояния

> Черновик статьи. Численные результаты обновлены по publication-ready серии `20260810T030729570Z` на commit `e093da0247ec58c7fb78fc381eca52fa002b0967`. Полный протокол вынесен в `EXPERIMENT_METHOD.md`, описание машины и .NET — в `EXPERIMENT_ENVIRONMENT.md`.

## Аннотация

Polar.DB — библиотека хранения структурированных данных для .NET, использующая append-oriented последовательности, бинарное представление записей и специализированные индексы. В работе исследуется не общий вопрос «какая СУБД быстрее», а профиль стоимости различных операций Polar.DB по сравнению с SQLite при одинаковой логической нагрузке.

Экспериментальная серия включает 16 сценариев на наборах 50 тыс. и 5 млн записей: построение integer primary index, point lookup по primary key четырёх типов, выборку по неуникальному external key четырёх типов, тяжёлую выборку по высокочастотному external key, append, logical delete и reopen. SQLite и Polar.DB выполнялись в отдельных процессах. В публикационные результаты принимались только Release-запуски с одинаковым commit/build state и совпавшими expected row counts/checksums.

На 5 млн записей Polar.DB показал преимущество при построении primary integer index (median 186,8 против 1488,9 мс), hot point lookup по primary key (примерно 1,65–3,72 раза), append/delete и размере дисковых artifacts. SQLite оказался сильнее при массовой выдаче по external index и особенно при переходе из закрытого состояния в query-ready: median 0,0133 мс против 701,7 мс в определении данного протокола. Результаты выявляют архитектурный компромисс: перенос static primary-index state в оперативную память уменьшает стоимость steady-state point lookup, но увеличивает стоимость подготовки query-ready состояния после reopen.

## 1. Постановка задачи

Сравнение систем хранения одной агрегированной величиной «операций в секунду» плохо описывает Polar.DB. В библиотеке физическая последовательность данных, primary index, external indexes и динамическое состояние после append образуют разные пути исполнения. Поэтому изменение одной части архитектуры может одновременно ускорять один класс операций и удорожать другой.

Исследовательский вопрос формулируется так:

**какие классы операций выигрывают и какие проигрывают при append-oriented хранении и RAM-resident состоянии primary index по сравнению с SQLite на одной машине и одинаковых логических запросах?**

Работа не претендует на универсальное ранжирование Polar.DB и SQLite. SQLite используется как зрелая embedded SQL контрольная точка; Polar.DB — специализированная библиотека с иной моделью хранения и иными инженерными целями.

## 2. Существенные свойства исследуемой версии Polar.DB

Для результатов статьи важны четыре свойства.

1. Записи хранятся в append-oriented бинарной последовательности. Логическое изменение может добавлять новую физическую запись вместо произвольного перемещения существующих данных.
2. Primary index отделён от основной последовательности. В query-ready состоянии static primary key/hash state и соответствующие physical offsets удерживаются в оперативной памяти.
3. Изменения после построения static snapshot образуют динамический слой и остаются authoritative для replace/tombstone, сохраняя last-write-wins semantics.
4. External indexes работают с неуникальными ключами и возвращают множества записей. Для них существенна не только стоимость поиска границы диапазона, но и обход, проверка актуальности и формирование результата.

Из этого следуют разные ожидаемые профили для point lookup, массовой выборки, мутаций и reopen. Эксперименты проверяют именно это различие.

## 3. Экспериментальная среда

Основная серия:

- run id: `20260810T030729570Z`;
- commit: `e093da0247ec58c7fb78fc381eca52fa002b0967`;
- working tree: clean;
- configuration: Release;
- .NET host/runtime: 10.0.7 x64;
- Polar.DB assembly: 2.1.3.0;
- Microsoft.Data.Sqlite assembly: 9.0.4.0;
- Server GC: выключен;
- Tiered Compilation, Tiered PGO и ReadyToRun: runtime defaults.

Аппаратная платформа:

- Windows 11 Pro, build 26200, x64;
- Intel Core i5-12400, 6 физических ядер / 12 логических процессоров;
- 31,78 GiB видимой физической памяти, 4 × 8 GiB, configured speed 3200 MT/s;
- Samsung SSD 980 PRO 250GB, NVMe;
- benchmark workspace на NTFS volume `D:`;
- активный план питания Windows: «Сбалансированная».

При отдельном environment snapshot активным SDK был .NET SDK 10.0.203. В repository `global.json` на этом commit содержит `sdk/version = 10.0.0`, которое `dotnet --info` считает недопустимым значением для SDK feature band. Поэтому этот файл не обеспечивал корректный SDK pin. Это ограничение воспроизводимости зафиксировано отдельно; оно не меняет фактически записанные benchmark manifests и raw results уже выполненной серии.

Полный machine/.NET snapshot приведён в `EXPERIMENT_ENVIRONMENT.md`.

## 4. Методика в кратком виде

SQLite и Polar.DB не выполняются в одном процессе. Coordinator запускает отдельный worker для каждого engine, проверяет commit и build settings, после чего объединяет результаты.

Каждый эксперимент выполняется на 50 000 и 5 000 000 записей. В основном тексте ниже показаны прежде всего результаты на 5 млн; 50 тыс. используются как контроль масштаба и сохраняются в raw artifacts.

Lookup-сценарии разделены на `Cold after reopen` и `Hot after file and lookup warmup`. Термин cold означает отсутствие explicit warmup в harness и **не означает принудительно очищенный OS page cache**.

Для mutations отдельно измеряются:

- `volatile mutation` — операция до persistence boundary;
- `durable mutation` — средняя стоимость операции внутри batch, включающего persistence boundary.

Для reopen отдельно измеряются:

- `open-only`;
- `query-ready reopen` — open + подготовка metadata/index state + один indexed primary-key lookup.

Количество строк и checksum результата проверяются независимо от времени выполнения. Полный sampling plan и определения приведены в `EXPERIMENT_METHOD.md`.

## 5. Результаты

### 5.1. Построение primary integer index

Изолированный сценарий `build-primary-int-only-id-only-experiment`, 5 млн идентификаторов:

| Engine | Build + flush, median | Размер artifact |
|---|---:|---:|
| Polar.DB | **186,8 мс** | 100,0 MB |
| SQLite | 1488,9 мс | 118,6 MB |

В данном сценарии Polar.DB выполняет измеряемую операцию примерно в **7,97 раза быстрее**. Этот коэффициент относится только к специально выделенному build primary integer index и не описывает полный ETL, загрузку произвольной схемы или построение всех возможных индексов.

### 5.2. Hot point lookup по primary key

На 5 млн записей:

| Тип ключа | Polar.DB, median мс/query | SQLite, median мс/query | Отношение |
|---|---:|---:|---:|
| `int` | **0,00406** | 0,00794 | Polar.DB ≈ 1,95× |
| `long` | **0,00392** | 0,00847 | Polar.DB ≈ 2,16× |
| `Guid` | **0,00381** | 0,01416 | Polar.DB ≈ 3,72× |
| `string` | **0,00458** | 0,00755 | Polar.DB ≈ 1,65× |

Для всех четырёх типов в этой серии Polar.DB быстрее в steady-state hot lookup. Measured lookup не включает создание dataset, build или reopen: он характеризует уже подготовленное query-ready состояние.

В primary lookup workloads дисковые artifacts Polar.DB занимают примерно 575 MB против 1688,6 MB у SQLite, то есть около **2,94 раза меньше**.

### 5.3. Обычный неуникальный external key

Обычный dataset использует 1000 различных external-key значений. При 5 млн записей успешный запрос возвращает около 5000 строк.

Hot phase:

| Тип external key | Polar.DB, median мс/query | SQLite, median мс/query |
|---|---:|---:|
| `int` | 24,56 | **22,08** |
| `long` | 23,87 | **22,18** |
| `Guid` | 24,20 | **24,14** |
| `string` | 27,09 | **22,39** |

Здесь преимущество primary point lookup не переносится автоматически на secondary/external path. Для `Guid` результаты практически равны, для остальных типов SQLite быстрее примерно на 8–21%.

Дисковые artifacts ordinary external lookup составляют около 635 MB у Polar.DB против 1688,6 MB у SQLite.

### 5.4. Высокочастотный external key

В `famous external` workload одно значение external key принадлежит 40% набора. На 5 млн строк один успешный запрос возвращает 2 млн записей.

Median hot lookup:

| Тип external key | Polar.DB, мс/query | SQLite, мс/query |
|---|---:|---:|
| `int` | 5408 | **3654** |
| `long` | 5563 | **3580** |
| `Guid` | 5324 | **3721** |
| `string` | 5499 | **3643** |

SQLite быстрее примерно в **1,43–1,55 раза**. При таком workload стоимость поиска границы индекса становится небольшой частью общего времени; основную роль играет обход и формирование многомиллионного результата.

### 5.5. Append и logical delete

На 5 млн исходных записей:

| Операция | Polar.DB | SQLite | Отношение |
|---|---:|---:|---:|
| append, volatile median | **0,0006 мс** | 0,1108 мс | ≈184,7× |
| append, durable median | **0,310 мс** | 0,678 мс | ≈2,19× |
| delete, volatile median | **0,0005 мс** | 0,0408 мс | ≈81,6× |
| delete, durable median | **0,276 мс** | 0,354 мс | ≈1,28× |

Большие коэффициенты volatile path нельзя выдавать за преимущество durable storage на два порядка: после включения persistence boundary различие резко сокращается. Для прикладной оценки сохранённых изменений существеннее durable values.

### 5.6. Reopen

На 5 млн записей:

| Engine | Open-only median | Query-ready reopen median |
|---|---:|---:|
| Polar.DB | 1,084 мс | 701,7 мс |
| SQLite | **0,00065 мс** | **0,0133 мс** |

Это центральный отрицательный результат серии. Polar.DB подготавливает RAM-resident primary-index structures, благодаря которым последующий point lookup выполняется быстро, но платит за это при подготовке query-ready состояния.

Поэтому корректная формулировка должна связывать две метрики:

**в исследованной версии Polar.DB ускоряет steady-state primary point lookup ценой существенно более дорогого query-ready reopen.**

Для workload с длительно живущим открытым storage это может быть приемлемым обменом. Для частого создания короткоживущих storage instances — наоборот, reopen может доминировать над стоимостью самих запросов.

## 6. Наблюдение после добавления primary offset cache

Предыдущая publication-ready серия `20260808T095444545Z` была выполнена на commit `3f8476f34b72b1ec18f875b3ac35143a00adac42`. Между ней и основной серией production diff исследуемого primary path добавляет RAM-resident static offsets рядом с primary hash/key snapshot; прочие изменения в рассматриваемом diff — regression tests.

Hot primary lookup на 5 млн изменился так:

| Ключ | До, мкс/query | После, мкс/query | Изменение |
|---|---:|---:|---:|
| `int` | 5,284 | **4,065** | −23,1% |
| `long` | 5,488 | **3,921** | −28,6% |
| `Guid` | 5,862 | **3,811** | −35,0% |
| `string` | 10,049 | **4,585** | −54,4% |

Одновременно median query-ready reopen вырос примерно с 564,5 до 701,7 мс, то есть на **24,3%**.

Это согласуется с ожидаемым trade-off, но сравнение двух полных серий, выполненных в разные моменты, не является строгим causal A/B experiment. Поэтому числа следует называть **version-to-version observation**, а не точной изолированной стоимостью одной оптимизации.

## 7. Обсуждение

Результаты не образуют линейного рейтинга систем.

Polar.DB особенно силён там, где workload совпадает с его моделью: специализированный primary-index build, длительно живущее query-ready состояние, point lookup и append-oriented изменения. RAM-resident offsets дополнительно смещают профиль в сторону дешёвого steady-state чтения.

SQLite сильнее в двух важных классах задач. Во-первых, переход к query-ready состоянию практически не требует той подготовки RAM snapshot, которую выполняет Polar.DB. Во-вторых, при materialization больших equal-range результатов по неуникальному индексу SQLite оказывается конкурентнее, а при миллионах возвращаемых строк — заметно быстрее.

Отдельным преимуществом Polar.DB в данной серии является disk footprint. Однако меньшие файлы не означают автоматически меньшее полное потребление ресурсов: query-ready Polar.DB использует дополнительные RAM arrays. Disk footprint и runtime memory необходимо рассматривать совместно.

Практический выбор поэтому зависит как минимум от:

- отношения числа point queries к числу reopen;
- среднего времени жизни открытого storage instance;
- доли массовых secondary-index запросов;
- требований к durable mutation latency;
- ограничений по дисковому footprint и доступной RAM.

## 8. Ограничения

1. Измерения выполнены на одной Windows-машине: i5-12400, 32 GiB RAM, Samsung 980 PRO NVMe. Результаты нельзя напрямую переносить на другой CPU, Linux, SATA storage или иной memory pressure.
2. Активный power plan — «Сбалансированная», поэтому частота CPU и power-management state не фиксировались на постоянном уровне.
3. `global.json` benchmark commit содержит недопустимое значение SDK `10.0.0`; фактический active SDK environment snapshot — 10.0.203. Для будущей строгой повторяемости SDK pin следует исправить и выполнить новую серию.
4. Сравниваются конкретные реализации одинаковых логических операций, а не все возможные настройки SQLite и Polar.DB.
5. Cold phase не очищает принудительно OS page cache.
6. `famous external` workload намеренно экстремален: 40% набора имеет один ключ.
7. Version-to-version comparison primary offset cache не заменяет interleaved A/B experiment.
8. Текущая серия измеряет wall-clock latency и resource snapshots, но не является microarchitectural profiling CPU cache misses, branch prediction или block-level IO.

## 9. Выводы

Для Polar.DB полезнее говорить не об общей «скорости базы», а о профиле стоимости операций.

На 5 млн записей текущая версия:

- выполняет изолированный build primary integer index существенно быстрее выбранной SQLite реализации;
- выполняет hot primary-key point lookup примерно в 1,65–3,72 раза быстрее для `int`, `long`, `Guid` и `string`;
- показывает особенно дешёвый volatile append/delete и сохраняет умеренное преимущество после включения persistence boundary;
- использует существенно меньшие дисковые artifacts в исследованных lookup workloads;
- немного или заметно уступает SQLite при выдаче тысяч и миллионов строк через неуникальный external index;
- платит высокой стоимостью query-ready reopen за RAM-resident primary-index state.

Главный результат — выявленный компромисс **steady-state primary lookup ↔ query-ready reopen**. Именно его, а не одиночный коэффициент ускорения, следует использовать при дальнейшем развитии Polar.DB и выборе workloads, для которых такая архитектура оправдана.

## Артефакты воспроизводимости

Основная publication-ready серия:

```text
benchmarks/results/raw/<experiment>/20260810T030729570Z/
```

Для каждого эксперимента сохраняются raw JSON/CSV, manifest и worker results. Полный запуск:

```powershell
pwsh -File .\benchmarks\scripts\run-new-benchmarks.ps1
```

Подробности:

- `EXPERIMENT_METHOD.md` — определения операций, sampling plan, warmup, correctness и ограничения;
- `EXPERIMENT_ENVIRONMENT.md` — CPU, RAM, SSD, Windows, .NET и нюанс `global.json`.
