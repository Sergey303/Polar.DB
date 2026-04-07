# GetStarted.EntryShellsAndWrappers

Это маленький добивающий проект для старых `Program.cs`-оболочек из `samples/GetStarted*`.

Что внутри:
- новый минимальный `Program.cs`;
- wrapper-сценарии по старым entry-shell файлам;
- сохранённые оригиналы старых shell-файлов;
- отдельно сохранённый извлечённый `Main1()` из `samples/GetStarted1/Program.cs`, который ещё требует финальной тематической миграции.

Этот проект не пытается заменить тематические examples.
Его задача — закрыть исторические входные точки и не потерять контекст.

## Команды

```bash
dotnet run -- list
dotnet run -- all
dotnet run -- gs1-shell
dotnet run -- gs2-shell
dotnet run -- gs3-shell
dotnet run -- gs4-shell
dotnet run -- pending-main1
```
