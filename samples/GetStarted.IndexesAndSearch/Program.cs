using System.Text;

namespace GetStarted.IndexesAndSearch;

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
            Console.Error.WriteLine($"Unknown scenario: {command}");
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
        Console.WriteLine("GetStarted.IndexesAndSearch");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  list        - print all scenarios");
        Console.WriteLine("  all         - run all scenarios");
        Console.WriteLine("  <id>        - run one scenario by id");
        Console.WriteLine();
        PrintList(scenarios);
    }

    private static void PrintList(IReadOnlyList<ISampleScenario> scenarios)
    {
        Console.WriteLine("Available scenarios:");
        foreach (var scenario in scenarios)
        {
            var suffix = scenario.IsExtractedFragment ? " [extracted fragment]" : string.Empty;
            Console.WriteLine($"  {scenario.Id,-14} {scenario.Title}{suffix}");
            Console.WriteLine($"                 source: {scenario.SourcePath}");
        }
    }

    private static void RunScenario(ISampleScenario scenario)
    {
        Console.WriteLine(new string('=', 100));
        Console.WriteLine($"Scenario: {scenario.Id}");
        Console.WriteLine($"Title:    {scenario.Title}");
        Console.WriteLine($"Source:   {scenario.SourcePath}");
        Console.WriteLine($"Kind:     {(scenario.IsExtractedFragment ? "extracted fragment" : "full scenario")}");
        Console.WriteLine(new string('-', 100));
        scenario.Run();
        Console.WriteLine();
    }
}
