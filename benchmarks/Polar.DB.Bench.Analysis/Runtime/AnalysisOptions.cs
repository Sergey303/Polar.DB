namespace Polar.DB.Bench.Analysis.Runtime;

public sealed class AnalysisOptions
{
    public static string UsageText =>
        "Usage policy mode: --raw <path> --policy <path> --baseline <path> --analyzed-out <dir>\n" +
        "Usage comparison mode: --raw-dir <dir> --compare-experiment <key> --comparison-out <dir> [--compare-dataset <key>] [--compare-fairness <key>] [--compare-env <class>]";

    public string? RawResultPath { get; init; }
    public string? PolicyPath { get; init; }
    public string? BaselinePath { get; init; }
    public string? AnalyzedResultsDirectory { get; init; }
    public string? RawResultsDirectory { get; init; }
    public string? ComparisonOutputDirectory { get; init; }
    public string? ComparisonExperimentKey { get; init; }
    public string? ComparisonDatasetProfileKey { get; init; }
    public string? ComparisonFairnessProfileKey { get; init; }
    public string? ComparisonEnvironmentClass { get; init; }

    public bool IsComparisonMode =>
        !string.IsNullOrWhiteSpace(RawResultsDirectory) ||
        !string.IsNullOrWhiteSpace(ComparisonOutputDirectory) ||
        !string.IsNullOrWhiteSpace(ComparisonExperimentKey);

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
            AnalyzedResultsDirectory = map.GetValueOrDefault("--analyzed-out"),
            RawResultsDirectory = map.GetValueOrDefault("--raw-dir"),
            ComparisonOutputDirectory = map.GetValueOrDefault("--comparison-out"),
            ComparisonExperimentKey = map.GetValueOrDefault("--compare-experiment"),
            ComparisonDatasetProfileKey = map.GetValueOrDefault("--compare-dataset"),
            ComparisonFairnessProfileKey = map.GetValueOrDefault("--compare-fairness"),
            ComparisonEnvironmentClass = map.GetValueOrDefault("--compare-env")
        };
    }

    public bool IsValid(out string error)
    {
        if (IsComparisonMode)
        {
            return ValidateComparisonMode(out error);
        }

        return ValidatePolicyMode(out error);
    }

    private bool ValidatePolicyMode(out string error)
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

    private bool ValidateComparisonMode(out string error)
    {
        if (!string.IsNullOrWhiteSpace(RawResultPath) ||
            !string.IsNullOrWhiteSpace(PolicyPath) ||
            !string.IsNullOrWhiteSpace(BaselinePath) ||
            !string.IsNullOrWhiteSpace(AnalyzedResultsDirectory))
        {
            error = "Comparison mode cannot be combined with --raw/--policy/--baseline/--analyzed-out.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(RawResultsDirectory) || !Directory.Exists(RawResultsDirectory))
        {
            error = "Missing or invalid --raw-dir.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ComparisonExperimentKey))
        {
            error = "Missing --compare-experiment.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ComparisonOutputDirectory))
        {
            error = "Missing --comparison-out.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
