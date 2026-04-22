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

    private static async Task<T> ReadAsync<T>(string path)
    {
        await using var stream = File.OpenRead(path);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonDefaults.Default);
        return value ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name} from '{path}'.");
    }
}
