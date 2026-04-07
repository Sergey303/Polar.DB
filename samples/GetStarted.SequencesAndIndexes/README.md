# GetStarted.SequencesAndIndexes

Этот проект — второй тематический срез для реорганизации `samples/GetStarted*`.

Что внутри:
- новый `Program.cs`, который показывает список сценариев и умеет запускать один или все;
- старые примеры, переписанные под формат сценариев;
- сохранены комментарии вокруг перенесённых фрагментов максимально близко к исходному коду;
- хардкодные пути заменены на локальную папку `data/SequencesAndIndexes` рядом с исполняемым файлом;
- самые тяжёлые размеры данных уменьшены для учебного запуска, но исходные значения и смысл примеров отражены в комментариях.

Что сознательно не вошло:
- triple store;
- paged streams;
- key-value/node experiments;
- потоки/flows без индексной темы.

Запуск:
- `dotnet run --project samples/GetStarted.SequencesAndIndexes -- list`
- `dotnet run --project samples/GetStarted.SequencesAndIndexes -- all`
- `dotnet run --project samples/GetStarted.SequencesAndIndexes -- gs1-d102`
