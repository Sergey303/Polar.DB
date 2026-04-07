using GetStarted.EntryShellsAndWrappers.Abstractions;

namespace GetStarted.EntryShellsAndWrappers.Scenarios;

internal sealed class GetStarted1ShellWrapperScenario : ISampleScenario
{
    public string Id => "gs1-shell";
    public string Title => "Старый entry-shell проекта GetStarted1";

    public void Run()
    {
        Console.WriteLine("Исходный shell:");
        Console.WriteLine("  samples/GetStarted1/Program.cs");
        Console.WriteLine();
        Console.WriteLine("Что делал shell:");
        Console.WriteLine("  - держал общий dbpath");
        Console.WriteLine("  - вручную переключал вызовы между Main3 / Main21 / Test / Demo103");
        Console.WriteLine();
        Console.WriteLine("Что с ним делать в новой структуре:");
        Console.WriteLine("  - не переносить как финальный сценарий;");
        Console.WriteLine("  - заменить новым Program.cs в тематических проектах;");
        Console.WriteLine("  - использовать только как историческую карту старых точек входа.");
        Console.WriteLine();
        Console.WriteLine("Оригинал сохранён в LegacyEntryShells/GetStarted1.Program.cs.txt");
    }
}
