namespace Polar.DB.Bench.Charts.Runtime;

public sealed class ChartsOptions
{
    public static string UsageText =>
        "Usage analyzed mode: --analyzed <dir> --reports-out <dir>\n" +
        "Usage comparison mode: --comparisons <dir> --reports-out <dir>";

    public string? AnalyzedResultsDirectory { get; init; }
    public string? ComparisonResultsDirectory { get; init; }
    public string? ReportsDirectory { get; init; }

    public static ChartsOptions Parse(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length - 1; i += 2)
        {
            map[args[i]] = args[i + 1];
        }

        return new ChartsOptions
        {
            AnalyzedResultsDirectory = map.GetValueOrDefault("--analyzed"),
            ComparisonResultsDirectory = map.GetValueOrDefault("--comparisons"),
            ReportsDirectory = map.GetValueOrDefault("--reports-out")
        };
    }

    public bool IsValid(out string error)
    {
        if (string.IsNullOrWhiteSpace(ReportsDirectory))
        {
            error = "Missing --reports-out.";
            return false;
        }

        var hasAnalyzed = !string.IsNullOrWhiteSpace(AnalyzedResultsDirectory);
        var hasComparisons = !string.IsNullOrWhiteSpace(ComparisonResultsDirectory);

        if (hasAnalyzed == hasComparisons)
        {
            error = "Specify exactly one input mode: --analyzed or --comparisons.";
            return false;
        }

        if (hasAnalyzed && !Directory.Exists(AnalyzedResultsDirectory!))
        {
            error = "Missing or invalid --analyzed directory.";
            return false;
        }

        if (hasComparisons && !Directory.Exists(ComparisonResultsDirectory!))
        {
            error = "Missing or invalid --comparisons directory.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
