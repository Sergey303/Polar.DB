namespace Polar.DB.Bench.Charts.Runtime;

public sealed class ChartsOptions
{
    public static string UsageText => "Usage: --analyzed <dir> --reports-out <dir>";

    public string? AnalyzedResultsDirectory { get; init; }
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
            ReportsDirectory = map.GetValueOrDefault("--reports-out")
        };
    }

    public bool IsValid(out string error)
    {
        if (string.IsNullOrWhiteSpace(AnalyzedResultsDirectory) || !Directory.Exists(AnalyzedResultsDirectory))
        {
            error = "Missing or invalid --analyzed directory.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ReportsDirectory))
        {
            error = "Missing --reports-out.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
