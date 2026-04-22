using System.Text.Json;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;

namespace Polar.DB.Bench.Analysis.Runtime;

public static class AnalysisApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        var options = AnalysisOptions.Parse(args);
        if (!options.IsValid(out var validationError))
        {
            Console.Error.WriteLine(validationError);
            Console.Error.WriteLine(AnalysisOptions.UsageText);
            return 2;
        }

        if (options.IsComparisonMode)
        {
            return await RunComparisonModeAsync(options);
        }

        return await RunPolicyModeAsync(options);
    }

    private static async Task<int> RunPolicyModeAsync(AnalysisOptions options)
    {
        var raw = await ReadAsync<RunResult>(options.RawResultPath!);
        var policy = await ReadAsync<PolicyContract>(options.PolicyPath!);
        var baseline = await ReadAsync<BaselineDescriptor>(options.BaselinePath!);

        var checks = PolicyEvaluator.Evaluate(raw, policy, baseline);
        var overallStatus = checks.Select(x => x.Status).Contains("Broken")
            ? "Broken"
            : checks.Select(x => x.Status).Contains("Regressed")
                ? "Regressed"
                : checks.Select(x => x.Status).Contains("Advisory")
                    ? "Advisory"
                    : "Passed";

        var analyzed = new AnalyzedResult
        {
            RunId = raw.RunId,
            RawResultPath = options.RawResultPath!,
            AnalysisTimestampUtc = DateTimeOffset.UtcNow,
            PolicyId = policy.PolicyId,
            BaselineId = baseline.BaselineId,
            OverallStatus = overallStatus,
            Checks = checks,
            DerivedMetrics = new Dictionary<string, double>(),
            Notes = new List<string>
            {
                "Benchmark analyzer output.",
                "Policy and baseline can be updated without rerunning the executor."
            }
        };

        Directory.CreateDirectory(options.AnalyzedResultsDirectory!);
        var timestamp = raw.TimestampUtc.ToString("yyyy-MM-ddTHH-mm-ssZ");
        var outputPath = ResultPathBuilder.BuildAnalyzedResultPath(
            options.AnalyzedResultsDirectory!,
            timestamp,
            raw.ExperimentKey,
            raw.DatasetProfileKey,
            raw.EngineKey,
            raw.Environment.EnvironmentClass);

        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, analyzed, JsonDefaults.Default);
        Console.WriteLine($"Analyzed result written: {outputPath}");

        return overallStatus switch
        {
            "Broken" => 4,
            "Regressed" => 3,
            _ => 0
        };
    }

    private static async Task<int> RunComparisonModeAsync(AnalysisOptions options)
    {
        var rawRuns = await LoadRawRunsAsync(options.RawResultsDirectory!);

        var filtered = rawRuns
            .Where(item => item.Result.ExperimentKey.Equals(options.ComparisonExperimentKey!, StringComparison.OrdinalIgnoreCase))
            .Where(item => MatchesOptional(item.Result.DatasetProfileKey, options.ComparisonDatasetProfileKey))
            .Where(item => MatchesOptional(item.Result.FairnessProfileKey, options.ComparisonFairnessProfileKey))
            .Where(item => MatchesOptional(item.Result.Environment.EnvironmentClass, options.ComparisonEnvironmentClass))
            .Where(item =>
                item.Result.EngineKey.Equals("polar-db", StringComparison.OrdinalIgnoreCase) ||
                item.Result.EngineKey.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (filtered.Length == 0)
        {
            throw new InvalidOperationException("No matching raw results were found for comparison mode.");
        }

        var latestByEngine = filtered
            .GroupBy(item => item.Result.EngineKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.Result.TimestampUtc).First())
            .ToDictionary(item => item.Result.EngineKey, item => item, StringComparer.OrdinalIgnoreCase);

        if (!latestByEngine.TryGetValue("polar-db", out var polar))
        {
            throw new InvalidOperationException("Comparison mode requires at least one matching Polar.DB raw result.");
        }

        if (!latestByEngine.TryGetValue("sqlite", out var sqlite))
        {
            throw new InvalidOperationException("Comparison mode requires at least one matching SQLite raw result.");
        }

        var selected = new[] { polar, sqlite };
        var timestampUtc = DateTimeOffset.UtcNow;
        var timestampToken = timestampUtc.ToString("yyyy-MM-ddTHH-mm-ssZ");
        var datasetProfileKey = ResolveSharedOrMixed(selected.Select(item => item.Result.DatasetProfileKey));
        var fairnessProfileKey = ResolveSharedOrMixed(selected.Select(item => item.Result.FairnessProfileKey));
        var environmentClass = ResolveSharedOrMixed(selected.Select(item => item.Result.Environment.EnvironmentClass));

        var entries = selected
            .OrderBy(item => item.Result.EngineKey.Equals("polar-db", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .Select(item => BuildEntry(item.Result, item.Path))
            .ToArray();

        var comparison = new CrossEngineComparisonResult
        {
            ComparisonId = $"{timestampToken}__{options.ComparisonExperimentKey}__{datasetProfileKey}__polar-db-vs-sqlite",
            TimestampUtc = timestampUtc,
            ExperimentKey = options.ComparisonExperimentKey!,
            DatasetProfileKey = datasetProfileKey,
            FairnessProfileKey = fairnessProfileKey,
            EnvironmentClass = environmentClass,
            Engines = entries,
            Notes = new List<string>
            {
                "Cross-engine stage3 comparison summary based on raw run facts.",
                "No policy evaluation is included in this artifact.",
                "Latest matching run per engine is selected by timestamp."
            }
        };

        Directory.CreateDirectory(options.ComparisonOutputDirectory!);
        var outputPath = ResultPathBuilder.BuildComparisonResultPath(
            options.ComparisonOutputDirectory!,
            timestampToken,
            comparison.ExperimentKey,
            comparison.DatasetProfileKey ?? "mixed",
            comparison.FairnessProfileKey ?? "mixed");

        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, comparison, JsonDefaults.Default);
        Console.WriteLine($"Comparison result written: {outputPath}");

        return 0;
    }

    private static async Task<T> ReadAsync<T>(string path)
    {
        await using var stream = File.OpenRead(path);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonDefaults.Default);
        return value ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name} from '{path}'.");
    }

    private static async Task<IReadOnlyList<RawRunEntry>> LoadRawRunsAsync(string rawResultsDirectory)
    {
        var files = Directory.GetFiles(rawResultsDirectory, "*.run.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var runs = new List<RawRunEntry>(files.Length);
        foreach (var file in files)
        {
            var run = await ReadAsync<RunResult>(file);
            runs.Add(new RawRunEntry(run, file));
        }

        return runs;
    }

    private static bool MatchesOptional(string value, string? filter)
    {
        return string.IsNullOrWhiteSpace(filter) ||
               value.Equals(filter, StringComparison.OrdinalIgnoreCase);
    }

    private static CrossEngineComparisonEntry BuildEntry(RunResult run, string rawPath)
    {
        var elapsedMs = ReadMetric(run, "elapsedMsSingleRun", "elapsedMsTotal");
        var loadMs = ReadMetric(run, "loadMs");
        var buildMs = ReadMetric(run, "buildMs");
        var reopenMs = ReadMetric(run, "reopenRefreshMs", "reopenMs");
        var lookupMs = ReadMetric(run, "randomPointLookupMs");
        var totalArtifactBytes = ReadMetric(run, "totalArtifactBytes");
        if (totalArtifactBytes <= 0.0)
        {
            totalArtifactBytes = run.Artifacts.Sum(artifact => artifact.Bytes);
        }

        var primaryArtifactBytes = ReadMetric(run, "primaryDataBytes", "primaryDatabaseBytes");
        if (primaryArtifactBytes <= 0.0)
        {
            primaryArtifactBytes = ReadPrimaryFromArtifacts(run);
        }

        if (primaryArtifactBytes <= 0.0 && run.EngineDiagnostics is not null)
        {
            if (TryReadDiagnostic(run.EngineDiagnostics, "primaryDataFileBytes", out var polarPrimary))
            {
                primaryArtifactBytes = polarPrimary;
            }
            else if (TryReadDiagnostic(run.EngineDiagnostics, "dbBytes", out var sqlitePrimary))
            {
                primaryArtifactBytes = sqlitePrimary;
            }
        }

        var sideArtifactBytes = Math.Max(0.0, totalArtifactBytes - primaryArtifactBytes);

        return new CrossEngineComparisonEntry
        {
            EngineKey = run.EngineKey,
            RunId = run.RunId,
            RawResultPath = rawPath.Replace('\\', '/'),
            RunTimestampUtc = run.TimestampUtc,
            TechnicalSuccess = run.TechnicalSuccess,
            SemanticSuccess = run.SemanticSuccess,
            ElapsedMsSingleRun = elapsedMs,
            LoadMs = loadMs,
            BuildMs = buildMs,
            ReopenMs = reopenMs,
            LookupMs = lookupMs,
            TotalArtifactBytes = totalArtifactBytes,
            PrimaryArtifactBytes = primaryArtifactBytes,
            SideArtifactBytes = sideArtifactBytes
        };
    }

    private static double ReadMetric(RunResult run, params string[] metricKeys)
    {
        foreach (var metricKey in metricKeys)
        {
            var metric = run.Metrics.FirstOrDefault(x => x.MetricKey.Equals(metricKey, StringComparison.OrdinalIgnoreCase));
            if (metric is not null)
            {
                return metric.Value;
            }
        }

        return 0.0;
    }

    private static double ReadPrimaryFromArtifacts(RunResult run)
    {
        var primary = run.Artifacts
            .Where(artifact =>
                artifact.Role is Polar.DB.Bench.Core.Abstractions.ArtifactRole.PrimaryData or
                Polar.DB.Bench.Core.Abstractions.ArtifactRole.PrimaryDatabase)
            .Sum(artifact => artifact.Bytes);
        return primary;
    }

    private static bool TryReadDiagnostic(IReadOnlyDictionary<string, string> diagnostics, string key, out double value)
    {
        if (diagnostics.TryGetValue(key, out var text) &&
            double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = 0.0;
        return false;
    }

    private static string ResolveSharedOrMixed(IEnumerable<string> values)
    {
        var list = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return list.Length == 1 ? list[0] : "mixed";
    }

    private sealed record RawRunEntry(RunResult Result, string Path);
}
