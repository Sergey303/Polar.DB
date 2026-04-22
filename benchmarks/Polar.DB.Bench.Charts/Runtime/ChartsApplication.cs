using System.Text;
using System.Text.Json;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;

namespace Polar.DB.Bench.Charts.Runtime;

public static class ChartsApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        var options = ChartsOptions.Parse(args);
        if (!options.IsValid(out var validationError))
        {
            Console.Error.WriteLine(validationError);
            Console.Error.WriteLine(ChartsOptions.UsageText);
            return 2;
        }

        if (!string.IsNullOrWhiteSpace(options.ComparisonResultsDirectory))
        {
            return await RunComparisonModeAsync(options);
        }

        return await RunAnalyzedModeAsync(options);
    }

    private static async Task<int> RunAnalyzedModeAsync(ChartsOptions options)
    {
        var files = Directory.GetFiles(options.AnalyzedResultsDirectory!, "*.eval.json", SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var results = new List<AnalyzedResult>();
        foreach (var file in files)
        {
            await using var stream = File.OpenRead(file);
            var value = await JsonSerializer.DeserializeAsync<AnalyzedResult>(stream, JsonDefaults.Default);
            if (value is not null)
            {
                results.Add(value);
            }
        }

        Directory.CreateDirectory(options.ReportsDirectory!);
        await File.WriteAllTextAsync(Path.Combine(options.ReportsDirectory!, "summary.md"), BuildMarkdown(results));
        await File.WriteAllTextAsync(Path.Combine(options.ReportsDirectory!, "summary.csv"), BuildCsv(results));

        Console.WriteLine($"Reports written to: {options.ReportsDirectory}");
        return 0;
    }

    private static async Task<int> RunComparisonModeAsync(ChartsOptions options)
    {
        var files = Directory.GetFiles(options.ComparisonResultsDirectory!, "*.comparison.json", SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var comparisons = new List<CrossEngineComparisonResult>();
        foreach (var file in files)
        {
            await using var stream = File.OpenRead(file);
            var value = await JsonSerializer.DeserializeAsync<CrossEngineComparisonResult>(stream, JsonDefaults.Default);
            if (value is not null)
            {
                comparisons.Add(value);
            }
        }

        Directory.CreateDirectory(options.ReportsDirectory!);
        await File.WriteAllTextAsync(
            Path.Combine(options.ReportsDirectory!, "comparison-summary.md"),
            BuildComparisonMarkdown(comparisons));
        await File.WriteAllTextAsync(
            Path.Combine(options.ReportsDirectory!, "comparison-summary.csv"),
            BuildComparisonCsv(comparisons));

        Console.WriteLine($"Comparison reports written to: {options.ReportsDirectory}");
        return 0;
    }

    private static string BuildMarkdown(IReadOnlyList<AnalyzedResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Benchmark Summary");
        sb.AppendLine();
        sb.AppendLine($"Generated at: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine();
        sb.AppendLine("| RunId | Status | Policy | Baseline |");
        sb.AppendLine("| --- | --- | --- | --- |");
        foreach (var result in results)
        {
            sb.AppendLine($"| {Escape(result.RunId)} | {Escape(result.OverallStatus)} | {Escape(result.PolicyId ?? string.Empty)} | {Escape(result.BaselineId ?? string.Empty)} |");
        }

        sb.AppendLine();
        sb.AppendLine("Current charts output is markdown and CSV summary only.");
        return sb.ToString();
    }

    private static string BuildCsv(IReadOnlyList<AnalyzedResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RunId,OverallStatus,PolicyId,BaselineId");
        foreach (var result in results)
        {
            sb.AppendLine($"{Csv(result.RunId)},{Csv(result.OverallStatus)},{Csv(result.PolicyId ?? string.Empty)},{Csv(result.BaselineId ?? string.Empty)}");
        }

        return sb.ToString();
    }

    private static string BuildComparisonMarkdown(IReadOnlyList<CrossEngineComparisonResult> comparisons)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Cross-Engine Comparison Summary");
        sb.AppendLine();
        sb.AppendLine($"Generated at: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine();
        sb.AppendLine("| ComparisonId | Experiment | Dataset | Fairness | Polar elapsed ms | SQLite elapsed ms | Polar load ms | SQLite load ms | Polar build ms | SQLite build ms | Polar reopen ms | SQLite reopen ms | Polar lookup ms | SQLite lookup ms | Polar total bytes | SQLite total bytes | Polar primary bytes | SQLite db bytes | Polar side bytes | SQLite side bytes | Polar semantic | SQLite semantic | Polar technical | SQLite technical |");
        sb.AppendLine("| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | --- | --- |");

        foreach (var comparison in comparisons.OrderBy(x => x.TimestampUtc))
        {
            var polar = FindEngine(comparison, "polar-db");
            var sqlite = FindEngine(comparison, "sqlite");

            sb.AppendLine(
                $"| {Escape(comparison.ComparisonId)} | {Escape(comparison.ExperimentKey)} | {Escape(comparison.DatasetProfileKey ?? string.Empty)} | {Escape(comparison.FairnessProfileKey ?? string.Empty)} | {FormatNumber(polar?.ElapsedMsSingleRun)} | {FormatNumber(sqlite?.ElapsedMsSingleRun)} | {FormatNumber(polar?.LoadMs)} | {FormatNumber(sqlite?.LoadMs)} | {FormatNumber(polar?.BuildMs)} | {FormatNumber(sqlite?.BuildMs)} | {FormatNumber(polar?.ReopenMs)} | {FormatNumber(sqlite?.ReopenMs)} | {FormatNumber(polar?.LookupMs)} | {FormatNumber(sqlite?.LookupMs)} | {FormatNumber(polar?.TotalArtifactBytes)} | {FormatNumber(sqlite?.TotalArtifactBytes)} | {FormatNumber(polar?.PrimaryArtifactBytes)} | {FormatNumber(sqlite?.PrimaryArtifactBytes)} | {FormatNumber(polar?.SideArtifactBytes)} | {FormatNumber(sqlite?.SideArtifactBytes)} | {FormatBool(polar?.SemanticSuccess)} | {FormatBool(sqlite?.SemanticSuccess)} | {FormatBool(polar?.TechnicalSuccess)} | {FormatBool(sqlite?.TechnicalSuccess)} |");
        }

        return sb.ToString();
    }

    private static string BuildComparisonCsv(IReadOnlyList<CrossEngineComparisonResult> comparisons)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ComparisonId,ExperimentKey,DatasetProfileKey,FairnessProfileKey,PolarElapsedMsSingleRun,SqliteElapsedMsSingleRun,PolarLoadMs,SqliteLoadMs,PolarBuildMs,SqliteBuildMs,PolarReopenMs,SqliteReopenMs,PolarLookupMs,SqliteLookupMs,PolarTotalArtifactBytes,SqliteTotalArtifactBytes,PolarPrimaryArtifactBytes,SqlitePrimaryArtifactBytes,PolarSideArtifactBytes,SqliteSideArtifactBytes,PolarSemanticSuccess,SqliteSemanticSuccess,PolarTechnicalSuccess,SqliteTechnicalSuccess");

        foreach (var comparison in comparisons.OrderBy(x => x.TimestampUtc))
        {
            var polar = FindEngine(comparison, "polar-db");
            var sqlite = FindEngine(comparison, "sqlite");

            sb.AppendLine(
                $"{Csv(comparison.ComparisonId)}," +
                $"{Csv(comparison.ExperimentKey)}," +
                $"{Csv(comparison.DatasetProfileKey ?? string.Empty)}," +
                $"{Csv(comparison.FairnessProfileKey ?? string.Empty)}," +
                $"{Csv(FormatNumber(polar?.ElapsedMsSingleRun))}," +
                $"{Csv(FormatNumber(sqlite?.ElapsedMsSingleRun))}," +
                $"{Csv(FormatNumber(polar?.LoadMs))}," +
                $"{Csv(FormatNumber(sqlite?.LoadMs))}," +
                $"{Csv(FormatNumber(polar?.BuildMs))}," +
                $"{Csv(FormatNumber(sqlite?.BuildMs))}," +
                $"{Csv(FormatNumber(polar?.ReopenMs))}," +
                $"{Csv(FormatNumber(sqlite?.ReopenMs))}," +
                $"{Csv(FormatNumber(polar?.LookupMs))}," +
                $"{Csv(FormatNumber(sqlite?.LookupMs))}," +
                $"{Csv(FormatNumber(polar?.TotalArtifactBytes))}," +
                $"{Csv(FormatNumber(sqlite?.TotalArtifactBytes))}," +
                $"{Csv(FormatNumber(polar?.PrimaryArtifactBytes))}," +
                $"{Csv(FormatNumber(sqlite?.PrimaryArtifactBytes))}," +
                $"{Csv(FormatNumber(polar?.SideArtifactBytes))}," +
                $"{Csv(FormatNumber(sqlite?.SideArtifactBytes))}," +
                $"{Csv(FormatBool(polar?.SemanticSuccess))}," +
                $"{Csv(FormatBool(sqlite?.SemanticSuccess))}," +
                $"{Csv(FormatBool(polar?.TechnicalSuccess))}," +
                $"{Csv(FormatBool(sqlite?.TechnicalSuccess))}");
        }

        return sb.ToString();
    }

    private static CrossEngineComparisonEntry? FindEngine(CrossEngineComparisonResult comparison, string engineKey)
    {
        return comparison.Engines.FirstOrDefault(x => x.EngineKey.Equals(engineKey, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatNumber(double? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static string FormatBool(bool? value)
    {
        return value?.ToString() ?? string.Empty;
    }

    private static string Escape(string value) => value.Replace("|", "\\|");

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
