using GetStarted.EntryShellsAndWrappers.Scenarios;

var scenarios = ScenarioCatalog.All;

if (args.Length == 0 || args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("Available scenarios:");
    foreach (var scenario in scenarios)
    {
        Console.WriteLine($"{scenario.Id,-18} {scenario.Title}");
    }
    Console.WriteLine();
    Console.WriteLine("Use:");
    Console.WriteLine("  dotnet run -- list");
    Console.WriteLine("  dotnet run -- all");
    Console.WriteLine("  dotnet run -- <scenario-id>");
    return;
}

if (args[0].Equals("all", StringComparison.OrdinalIgnoreCase))
{
    foreach (var scenario in scenarios)
    {
        Console.WriteLine(new string('=', 80));
        Console.WriteLine($"{scenario.Id} — {scenario.Title}");
        Console.WriteLine(new string('=', 80));
        scenario.Run();
        Console.WriteLine();
    }
    return;
}

var selected = scenarios.FirstOrDefault(x => x.Id.Equals(args[0], StringComparison.OrdinalIgnoreCase));
if (selected is null)
{
    Console.WriteLine($"Unknown scenario id: {args[0]}");
    Console.WriteLine("Run `dotnet run -- list` to see available scenarios.");
    return;
}

selected.Run();
