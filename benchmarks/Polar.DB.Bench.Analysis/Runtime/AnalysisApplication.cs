using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;

namespace Polar.DB.Bench.Analysis.Runtime;

/// <summary>
/// Entry point for benchmark analysis commands.
/// It supports two flows:
/// policy evaluation for one raw run and cross-engine comparison artifact generation.
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
        var rawRuns = await files.LoadRawRunsAsync(options.RawResultsDirectory!);
        var selector = new ComparisonSelectionService(PolarEngineKey, SqliteEngineKey);
        var filtered = selector.SelectRuns(rawRuns, options);

        if (filtered.Length == 0)
        {
            throw new InvalidOperationException("No matching raw results were found for comparison mode.");
        }

        var comparisonSetId = selector.ResolveComparisonSetId(filtered, options.ComparisonSetId);
        Directory.CreateDirectory(options.ComparisonOutputDirectory!);

        if (!string.IsNullOrWhiteSpace(comparisonSetId))
        {
            var seriesBuilder = new SeriesComparisonBuilder(PolarEngineKey, SqliteEngineKey);
            var seriesResult = seriesBuilder.Build(filtered, options.ComparisonExperimentKey!, comparisonSetId);
            var timestampToken = seriesResult.TimestampUtc.ToString("yyyy-MM-ddTHH-mm-ssZ");
            var outputPath = ResultPathBuilder.BuildComparisonSeriesResultPath(
                options.ComparisonOutputDirectory!,
                timestampToken,
                seriesResult.ExperimentKey,
                seriesResult.DatasetProfileKey ?? "mixed",
                seriesResult.FairnessProfileKey ?? "mixed",
                ComparisonValueHelpers.ToFileToken(seriesResult.ComparisonSetId));

            await files.WriteAsync(outputPath, seriesResult);
            Console.WriteLine($"Comparison series result written: {outputPath}");
            return 0;
        }

        var legacyBuilder = new LegacyComparisonBuilder(PolarEngineKey, SqliteEngineKey);
        var legacyResult = legacyBuilder.Build(filtered, options.ComparisonExperimentKey!);
        var legacyTimestampToken = legacyResult.TimestampUtc.ToString("yyyy-MM-ddTHH-mm-ssZ");
        var legacyPath = ResultPathBuilder.BuildComparisonResultPath(
            options.ComparisonOutputDirectory!,
            legacyTimestampToken,
            legacyResult.ExperimentKey,
            legacyResult.DatasetProfileKey ?? "mixed",
            legacyResult.FairnessProfileKey ?? "mixed");

        await files.WriteAsync(legacyPath, legacyResult);
        Console.WriteLine($"Comparison result written: {legacyPath}");
        return 0;
    }
}
