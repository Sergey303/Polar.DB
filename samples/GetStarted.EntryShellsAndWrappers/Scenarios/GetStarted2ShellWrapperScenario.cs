using GetStarted.EntryShellsAndWrappers.Abstractions;

namespace GetStarted.EntryShellsAndWrappers.Scenarios;

internal sealed class GetStarted2ShellWrapperScenario : ISampleScenario
{
    public string Id => "gs2-shell";
    public string Title => "Старый entry-shell проекта GetStarted2";

    public void Run()
    {
        Console.WriteLine("Исходный shell:");
        Console.WriteLine("  samples/GetStarted2/Program.cs");
        Console.WriteLine();
        Console.WriteLine("Что делал shell:");
        Console.WriteLine("  - включал UTF-8 для консоли;");
        Console.WriteLine("  - запускал Main201();");
        Console.WriteLine();
        Console.WriteLine("В новой структуре:");
        Console.WriteLine("  - смысловой сценарий уже уехал в GetStarted.StructuresAndSerialization;");
        Console.WriteLine("  - этот файл нужен только как историческая оболочка.");
        Console.WriteLine();
        Console.WriteLine("Оригинал сохранён в LegacyEntryShells/GetStarted2.Program.cs.txt");
    }
}
