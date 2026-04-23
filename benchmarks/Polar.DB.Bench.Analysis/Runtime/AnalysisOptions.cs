namespace Polar.DB.Bench.Analysis.Runtime;

public sealed class AnalysisOptions
{
    private const string RawDirectoryName = "raw";
    private const string AnalyzedDirectoryName = "analyzed";
    private const string ComparisonsDirectoryName = "comparisons";
    private const string ManifestFileName = "experiment.json";

    public static string UsageText =>
        "Usage policy mode: --raw <path> --policy <path> --baseline <path> [--analyzed-out <dir>]\n" +
        "Usage comparison mode: --raw-dir <experiment-dir|raw-dir> --compare-experiment <key> [--comparison-out <dir>] [--analyzed-out <dir>] [--compare-dataset <key>] [--compare-fairness <key>] [--compare-env <class>] [--compare-set <id>]";

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
    public string? ComparisonSetId { get; init; }

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
            ComparisonEnvironmentClass = map.GetValueOrDefault("--compare-env"),
            ComparisonSetId = map.GetValueOrDefault("--compare-set")
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

        try
        {
            _ = ResolveAnalyzedResultsDirectoryForPolicy(RawResultPath, AnalyzedResultsDirectory);
        }
        catch (InvalidOperationException ex)
        {
            error = ex.Message;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool ValidateComparisonMode(out string error)
    {
        if (!string.IsNullOrWhiteSpace(RawResultPath) ||
            !string.IsNullOrWhiteSpace(PolicyPath) ||
            !string.IsNullOrWhiteSpace(BaselinePath))
        {
            error = "Comparison mode cannot be combined with --raw/--policy/--baseline.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(RawResultsDirectory))
        {
            error = "Missing --raw-dir.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ComparisonExperimentKey))
        {
            error = "Missing --compare-experiment.";
            return false;
        }

        try
        {
            _ = ResolveRawResultsDirectory(RawResultsDirectory);
            _ = ResolveComparisonOutputDirectory(RawResultsDirectory, ComparisonOutputDirectory);
            _ = ResolveAnalyzedResultsDirectoryForComparison(RawResultsDirectory, AnalyzedResultsDirectory);
        }
        catch (InvalidOperationException ex)
        {
            error = ex.Message;
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static string ResolveRawResultsDirectory(string rawDirectoryOrExperimentDirectory)
    {
        if (string.IsNullOrWhiteSpace(rawDirectoryOrExperimentDirectory))
        {
            throw new InvalidOperationException("Missing --raw-dir.");
        }

        if (!Directory.Exists(rawDirectoryOrExperimentDirectory))
        {
            throw new InvalidOperationException("Missing or invalid --raw-dir.");
        }

        var fullPath = Path.GetFullPath(rawDirectoryOrExperimentDirectory);
        if (Directory.GetFiles(fullPath, "*.run.json", SearchOption.TopDirectoryOnly).Length > 0)
        {
            return fullPath;
        }

        var rawSubdirectory = Path.Combine(fullPath, RawDirectoryName);
        if (Directory.Exists(rawSubdirectory))
        {
            return rawSubdirectory;
        }

        throw new InvalidOperationException(
            "Invalid --raw-dir: expected experiment folder with raw/ or a directory containing *.run.json files.");
    }

    public static string ResolveComparisonOutputDirectory(
        string rawDirectoryOrExperimentDirectory,
        string? comparisonOutputDirectory)
    {
        if (!string.IsNullOrWhiteSpace(comparisonOutputDirectory))
        {
            return Path.GetFullPath(comparisonOutputDirectory);
        }

        var experimentDirectory = TryResolveExperimentDirectoryFromRawDirectory(rawDirectoryOrExperimentDirectory);
        if (!string.IsNullOrWhiteSpace(experimentDirectory))
        {
            return Path.Combine(experimentDirectory, ComparisonsDirectoryName);
        }

        throw new InvalidOperationException(
            "Missing --comparison-out. For non-canonical raw paths, comparison output directory must be provided explicitly.");
    }

    public static string ResolveAnalyzedResultsDirectoryForPolicy(
        string rawResultPath,
        string? analyzedResultsDirectory)
    {
        if (!string.IsNullOrWhiteSpace(analyzedResultsDirectory))
        {
            return Path.GetFullPath(analyzedResultsDirectory);
        }

        var experimentDirectory = TryResolveExperimentDirectoryFromRawFile(rawResultPath);
        if (!string.IsNullOrWhiteSpace(experimentDirectory))
        {
            return Path.Combine(experimentDirectory, AnalyzedDirectoryName);
        }

        throw new InvalidOperationException(
            "Missing --analyzed-out. For raw files outside experiment/raw, analyzed output directory must be provided explicitly.");
    }

    public static string ResolveAnalyzedResultsDirectoryForComparison(
        string rawDirectoryOrExperimentDirectory,
        string? analyzedResultsDirectory)
    {
        if (!string.IsNullOrWhiteSpace(analyzedResultsDirectory))
        {
            return Path.GetFullPath(analyzedResultsDirectory);
        }

        var experimentDirectory = TryResolveExperimentDirectoryFromRawDirectory(rawDirectoryOrExperimentDirectory);
        if (!string.IsNullOrWhiteSpace(experimentDirectory))
        {
            return Path.Combine(experimentDirectory, AnalyzedDirectoryName);
        }

        throw new InvalidOperationException(
            "Missing --analyzed-out. For non-canonical raw paths, analyzed output directory must be provided explicitly.");
    }

    private static string? TryResolveExperimentDirectoryFromRawFile(string rawResultPath)
    {
        if (string.IsNullOrWhiteSpace(rawResultPath) || !File.Exists(rawResultPath))
        {
            return null;
        }

        var directoryPath = Path.GetDirectoryName(Path.GetFullPath(rawResultPath));
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return null;
        }

        var directoryName = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!directoryName.Equals(RawDirectoryName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parentDirectory = Directory.GetParent(directoryPath)?.FullName;
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return null;
        }

        var manifestPath = Path.Combine(parentDirectory, ManifestFileName);
        return File.Exists(manifestPath) ? parentDirectory : null;
    }

    private static string? TryResolveExperimentDirectoryFromRawDirectory(string rawDirectoryOrExperimentDirectory)
    {
        if (string.IsNullOrWhiteSpace(rawDirectoryOrExperimentDirectory) ||
            !Directory.Exists(rawDirectoryOrExperimentDirectory))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(rawDirectoryOrExperimentDirectory);
        var manifestInCurrentDirectory = Path.Combine(fullPath, ManifestFileName);
        if (File.Exists(manifestInCurrentDirectory))
        {
            return fullPath;
        }

        var directoryName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (directoryName.Equals(RawDirectoryName, StringComparison.OrdinalIgnoreCase))
        {
            var parentDirectory = Directory.GetParent(fullPath)?.FullName;
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                var manifestInParent = Path.Combine(parentDirectory, ManifestFileName);
                if (File.Exists(manifestInParent))
                {
                    return parentDirectory;
                }
            }
        }

        return null;
    }
}
