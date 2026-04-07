using System.Text;

namespace GetStarted.FinalResidualMigration;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var scenarios = ScenarioCatalog.All;

        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp(scenarios);
            return 0;
        }

        var command = args[0].Trim();

        if (string.Equals(command, "list", StringComparison.OrdinalIgnoreCase))
        {
            PrintList(scenarios);
            return 0;
        }

        if (string.Equals(command, "all", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var scenario in scenarios)
            {
                RunScenario(scenario);
            }

            return 0;
        }

        var selected = scenarios.FirstOrDefault(s => string.Equals(s.Id, command, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            Console.Error.WriteLine($"Неизвестный сценарий: {command}");
            Console.Error.WriteLine();
            PrintList(scenarios);
            return 1;
        }

        RunScenario(selected);
        return 0;
    }

    private static bool IsHelp(string arg) =>
        string.Equals(arg, "help", StringComparison.OrdinalIgnoreCase)
        || string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
        || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase)
        || arg == "?";

    private static void PrintHelp(IReadOnlyList<ISampleScenario> scenarios)
    {
        Console.WriteLine("GetStarted.FinalResidualMigration");
        Console.WriteLine();
        Console.WriteLine("Команды:");
        Console.WriteLine("  list        - показать список сценариев");
        Console.WriteLine("  all         - запустить все сценарии подряд");
        Console.WriteLine("  <id>        - запустить один сценарий по id");
        Console.WriteLine();
        PrintList(scenarios);
    }

    private static void PrintList(IReadOnlyList<ISampleScenario> scenarios)
    {
        Console.WriteLine("Доступные сценарии:");
        foreach (var scenario in scenarios)
        {
            var suffix = scenario.IsExtractedFragment ? " [извлечённый фрагмент]" : string.Empty;
            Console.WriteLine($"  {scenario.Id,-16} {scenario.Title}{suffix}");
            Console.WriteLine($"                 source: {scenario.SourcePath}");
        }
    }

    private static void RunScenario(ISampleScenario scenario)
    {
        Console.WriteLine(new string('=', 100));
        Console.WriteLine($"Scenario: {scenario.Id}");
        Console.WriteLine($"Title:    {scenario.Title}");
        Console.WriteLine($"Source:   {scenario.SourcePath}");
        Console.WriteLine($"Kind:     {(scenario.IsExtractedFragment ? "извлечённый фрагмент" : "полный сценарий")}");
        Console.WriteLine(new string('-', 100));
        scenario.Run();
        Console.WriteLine();
    }
}
