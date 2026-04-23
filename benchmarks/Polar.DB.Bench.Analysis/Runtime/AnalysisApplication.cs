using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;

namespace Polar.DB.Bench.Analysis.Runtime;

/// <summary>
/// Entry point for benchmark analysis commands.
/// It supports two flows:
/// local run interpretation artifacts in <c>analyzed/</c> and comparison artifacts in <c>comparisons/</c>.
/// </summary>
public static class AnalysisApplication
{
    private const string PolarEngineKey = "polar-db";
    private const string SqliteEngineKey = "sqlite";

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
        var files = new BenchmarkFileReader();
        var raw = await files.ReadAsync<RunResult>(options.RawResultPath!);
        var policy = await files.ReadAsync<PolicyContract>(options.PolicyPath!);
        var baseline = await files.ReadAsync<BaselineDescriptor>(options.BaselinePath!);
        var analyzedResultsDirectory = AnalysisOptions.ResolveAnalyzedResultsDirectoryForPolicy(
            options.RawResultPath!,
            options.AnalyzedResultsDirectory);

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

        Directory.CreateDirectory(analyzedResultsDirectory);
        var timestamp = raw.TimestampUtc.ToString("yyyy-MM-ddTHH-mm-ssZ");
        var outputPath = ResultPathBuilder.BuildAnalyzedResultPath(
            analyzedResultsDirectory,
            timestamp,
            raw.ExperimentKey,
            raw.DatasetProfileKey,
            raw.EngineKey,
            raw.Environment.EnvironmentClass);

        await files.WriteAsync(outputPath, analyzed);
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
        var files = new BenchmarkFileReader();
        var rawResultsDirectory = AnalysisOptions.ResolveRawResultsDirectory(options.RawResultsDirectory!);
        var comparisonOutputDirectory = AnalysisOptions.ResolveComparisonOutputDirectory(
            options.RawResultsDirectory!,
            options.ComparisonOutputDirectory);
        var analyzedResultsDirectory = AnalysisOptions.ResolveAnalyzedResultsDirectoryForComparison(
            options.RawResultsDirectory!,
            options.AnalyzedResultsDirectory);

        var rawRuns = await files.LoadRawRunsAsync(rawResultsDirectory);
        var selector = new ComparisonSelectionService(PolarEngineKey, SqliteEngineKey);
        var filtered = selector.SelectRuns(rawRuns, options);

        if (filtered.Length == 0)
        {
            throw new InvalidOperationException("No matching raw results were found for comparison mode.");
        }

        var comparisonSetId = selector.ResolveComparisonSetId(filtered, options.ComparisonSetId);
        Directory.CreateDirectory(comparisonOutputDirectory);
        Directory.CreateDirectory(analyzedResultsDirectory);

        if (!string.IsNullOrWhiteSpace(comparisonSetId))
        {
            var seriesBuilder = new SeriesComparisonBuilder(PolarEngineKey, SqliteEngineKey);
            var seriesResult = seriesBuilder.Build(filtered, options.ComparisonExperimentKey!, comparisonSetId);
            var timestampToken = seriesResult.TimestampUtc.ToString("yyyy-MM-ddTHH-mm-ssZ");
            var outputPath = ResultPathBuilder.BuildComparisonSeriesResultPath(
                comparisonOutputDirectory,
                timestampToken,
                seriesResult.ExperimentKey,
                seriesResult.DatasetProfileKey ?? "mixed",
                seriesResult.FairnessProfileKey ?? "mixed",
                ComparisonValueHelpers.ToFileToken(seriesResult.ComparisonSetId));

            await files.WriteAsync(outputPath, seriesResult);
            Console.WriteLine($"Comparison series result written: {outputPath}");
            await WriteLatestSeriesArtifactsAsync(files, analyzedResultsDirectory, seriesResult);
            return 0;
        }

        var legacyBuilder = new LegacyComparisonBuilder(PolarEngineKey, SqliteEngineKey);
        var legacyResult = legacyBuilder.Build(filtered, options.ComparisonExperimentKey!);
        var legacyTimestampToken = legacyResult.TimestampUtc.ToString("yyyy-MM-ddTHH-mm-ssZ");
        var legacyPath = ResultPathBuilder.BuildComparisonResultPath(
            comparisonOutputDirectory,
            legacyTimestampToken,
            legacyResult.ExperimentKey,
            legacyResult.DatasetProfileKey ?? "mixed",
            legacyResult.FairnessProfileKey ?? "mixed");

        await files.WriteAsync(legacyPath, legacyResult);
        Console.WriteLine($"Comparison result written: {legacyPath}");
        await WriteLatestSeriesArtifactsFromLegacyAsync(files, analyzedResultsDirectory, legacyResult);
        return 0;
    }

    private static async Task WriteLatestSeriesArtifactsAsync(
        BenchmarkFileReader files,
        string analyzedResultsDirectory,
        CrossEngineComparisonSeriesResult seriesResult)
    {
        foreach (var seriesEntry in seriesResult.EngineSeries)
        {
            var localArtifact = new LocalAnalyzedSeriesResult
            {
                ArtifactKind = "latest-series",
                AnalysisTimestampUtc = seriesResult.TimestampUtc,
                ExperimentKey = seriesResult.ExperimentKey,
                EngineKey = seriesEntry.EngineKey,
                ComparisonSetId = seriesResult.ComparisonSetId,
                DatasetProfileKey = seriesResult.DatasetProfileKey,
                FairnessProfileKey = seriesResult.FairnessProfileKey,
                EnvironmentClass = seriesResult.EnvironmentClass,
                MeasuredRunCount = seriesEntry.MeasuredRunCount,
                WarmupRunCount = seriesEntry.WarmupRunCount,
                TechnicalSuccessCount = seriesEntry.TechnicalSuccessCount,
                SemanticSuccessCount = seriesEntry.SemanticSuccessCount,
                SemanticEvaluatedCount = seriesEntry.SemanticEvaluatedCount,
                RawResultPaths = seriesEntry.RawResultPaths,
                ElapsedMs = seriesEntry.ElapsedMs,
                LoadMs = seriesEntry.LoadMs,
                BuildMs = seriesEntry.BuildMs,
                ReopenMs = seriesEntry.ReopenMs,
                LookupMs = seriesEntry.LookupMs,
                LookupBatchCount = seriesEntry.LookupBatchCount,
                TotalArtifactBytes = seriesEntry.TotalArtifactBytes,
                PrimaryArtifactBytes = seriesEntry.PrimaryArtifactBytes,
                SideArtifactBytes = seriesEntry.SideArtifactBytes,
                Notes = new List<string>
                {
                    "Local analyzed artifact for one engine.",
                    "Derived from latest measured comparison set in this experiment.",
                    "Cross-engine comparison artifacts are stored in comparisons/."
                }
            };

            var outputPath = Path.Combine(analyzedResultsDirectory, $"latest-series.{seriesEntry.EngineKey}.json");
            await files.WriteAsync(outputPath, localArtifact);
            Console.WriteLine($"Local analyzed series written: {outputPath}");
        }
    }

    private static async Task WriteLatestSeriesArtifactsFromLegacyAsync(
        BenchmarkFileReader files,
        string analyzedResultsDirectory,
        CrossEngineComparisonResult legacyResult)
    {
        foreach (var entry in legacyResult.Engines)
        {
            var localArtifact = new LocalAnalyzedSeriesResult
            {
                ArtifactKind = "latest-series",
                AnalysisTimestampUtc = legacyResult.TimestampUtc,
                ExperimentKey = legacyResult.ExperimentKey,
                EngineKey = entry.EngineKey,
                ComparisonSetId = null,
                DatasetProfileKey = legacyResult.DatasetProfileKey,
                FairnessProfileKey = legacyResult.FairnessProfileKey,
                EnvironmentClass = legacyResult.EnvironmentClass,
                MeasuredRunCount = 1,
                WarmupRunCount = 0,
                TechnicalSuccessCount = entry.TechnicalSuccess ? 1 : 0,
                SemanticSuccessCount = entry.SemanticSuccess == true ? 1 : 0,
                SemanticEvaluatedCount = entry.SemanticSuccess is null ? 0 : 1,
                RawResultPaths = new[] { entry.RawResultPath },
                ElapsedMs = CreateSingleValueStats(entry.ElapsedMsSingleRun),
                LoadMs = CreateSingleValueStats(entry.LoadMs),
                BuildMs = CreateSingleValueStats(entry.BuildMs),
                ReopenMs = CreateSingleValueStats(entry.ReopenMs),
                LookupMs = CreateSingleValueStats(entry.LookupMs),
                LookupBatchCount = null,
                TotalArtifactBytes = CreateSingleValueStats(entry.TotalArtifactBytes),
                PrimaryArtifactBytes = CreateSingleValueStats(entry.PrimaryArtifactBytes),
                SideArtifactBytes = CreateSingleValueStats(entry.SideArtifactBytes),
                Notes = new List<string>
                {
                    "Local analyzed artifact for one engine.",
                    "Derived from legacy single-run fallback (no comparison set id).",
                    "Cross-engine comparison artifacts are stored in comparisons/."
                }
            };

            var outputPath = Path.Combine(analyzedResultsDirectory, $"latest-series.{entry.EngineKey}.json");
            await files.WriteAsync(outputPath, localArtifact);
            Console.WriteLine($"Local analyzed series written: {outputPath}");
        }
    }

    private static MetricSeriesStats CreateSingleValueStats(double value)
    {
        return new MetricSeriesStats
        {
            Count = 1,
            MissingCount = 0,
            Min = value,
            Max = value,
            Average = value,
            Median = value
        };
    }
}
