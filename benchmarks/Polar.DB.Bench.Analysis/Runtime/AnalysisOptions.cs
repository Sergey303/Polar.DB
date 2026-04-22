namespace Polar.DB.Bench.Analysis.Runtime;

public sealed class AnalysisOptions
{
    public static string UsageText => "Usage: --raw <path> --policy <path> --baseline <path> --analyzed-out <dir>";

    public string? RawResultPath { get; init; }
    public string? PolicyPath { get; init; }
    public string? BaselinePath { get; init; }
    public string? AnalyzedResultsDirectory { get; init; }

    public static AnalysisOptions Parse(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length - 1; i += 2)
        {
            map[args[i]] = args[i + 1];
        }

        return new AnalysisOptions
        {
            RawResultPath = map.GetValueOrDefault("--raw"),
            PolicyPath = map.GetValueOrDefault("--policy"),
            BaselinePath = map.GetValueOrDefault("--baseline"),
            AnalyzedResultsDirectory = map.GetValueOrDefault("--analyzed-out")
        };
    }

    public bool IsValid(out string error)
    {
        if (string.IsNullOrWhiteSpace(RawResultPath) || !File.Exists(RawResultPath))
        {
            error = "Missing or invalid --raw path.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(PolicyPath) || !File.Exists(PolicyPath))
        {
            error = "Missing or invalid --policy path.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(BaselinePath) || !File.Exists(BaselinePath))
        {
            error = "Missing or invalid --baseline path.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(AnalyzedResultsDirectory))
        {
            error = "Missing --analyzed-out.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
