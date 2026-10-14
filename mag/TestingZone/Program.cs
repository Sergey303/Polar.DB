using System.Diagnostics;

try
{
    var options = RunOptions.Parse(args);
    Console.WriteLine($"TestingZone scenario={options.Scenario} phase={options.Phase} count={options.Count} data={options.Directory} optimise={options.Optimise}");
    var wall = Stopwatch.StartNew();
    foreach (var phase in options.Phase == "cycle" ? new[] { "load", "reopen" } : new[] { options.Phase })
    {
        if (options.Scenario is "base" or "all")
            BaseExperiment.Run(options.Directory, options.Count, phase);
        if (options.Scenario is "mutation" or "all")
            MutationExperiment.Run(options.Directory, phase);
        if (options.Scenario is "indexed" or "all")
        {
            int indexedCount = options.Scenario == "all" ? Math.Min(options.Count, 25000) : options.Count;
            if (indexedCount < 20) throw new ArgumentException("Indexed scenario requires at least 20 records.");
            if (options.Scenario == "all" && indexedCount != options.Count)
                Console.WriteLine($"NOTE indexed workload capped at {indexedCount} records; base uses {options.Count}.");
            IndexedExperiment.Run(options.Directory, indexedCount, phase, options.Optimise);
        }
    }
    Console.WriteLine($"PASS total_ms={wall.Elapsed.TotalMilliseconds:F1}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FAIL {ex}");
    Environment.ExitCode = 1;
}

internal sealed record RunOptions(string Scenario, string Phase, int Count, string Directory, bool Optimise)
{
    internal static RunOptions Parse(string[] args)
    {
        // Alexander's original reproduction: TestingZone 5000000 true; TestingZone 5000000 false.
        if (args.Length == 2 && int.TryParse(args[0], out int legacyCount) &&
            bool.TryParse(args[1], out bool load))
            return Validate(new("base", load ? "load" : "reopen", legacyCount,
                Path.Combine(Environment.CurrentDirectory, "TestingZone-data"), false));

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (args.Length % 2 != 0) throw new ArgumentException(Usage);
        for (int i = 0; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) ||
                !values.TryAdd(args[i][2..], args[i + 1]))
                throw new ArgumentException(Usage);
        }
        foreach (string key in values.Keys)
            if (key is not ("scenario" or "phase" or "count" or "dir" or "optimise"))
                throw new ArgumentException(Usage);

        string Get(string key, string fallback) => values.GetValueOrDefault(key, fallback);
        if (!int.TryParse(Get("count", "2000"), out int count) ||
            !bool.TryParse(Get("optimise", "false"), out bool optimise))
            throw new ArgumentException(Usage);

        return Validate(new(Get("scenario", "all"), Get("phase", "cycle"), count,
            Path.GetFullPath(Get("dir", Path.Combine(Environment.CurrentDirectory, "TestingZone-data"))),
            optimise));
    }

    private static RunOptions Validate(RunOptions options)
    {
        if (options.Scenario is not ("base" or "mutation" or "indexed" or "all") ||
            options.Phase is not ("load" or "reopen" or "cycle") ||
            options.Count < 1)
            throw new ArgumentException(Usage);
        return options;
    }

    private const string Usage =
        "Usage: TestingZone <count> <true|false> OR --scenario base|mutation|indexed|all " +
        "--phase load|reopen|cycle --count N --dir PATH --optimise true|false";
}
