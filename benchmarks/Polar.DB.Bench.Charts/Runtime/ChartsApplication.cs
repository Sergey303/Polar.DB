namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Entry point for benchmark chart/report generation.
/// It supports analyzed-result summaries and cross-engine comparison summaries.
/// </summary>
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
        var loader = new ChartsArtifactLoader();
        var renderer = new AnalyzedSummaryRenderer();
        var results = await loader.LoadAnalyzedResultsAsync(options.AnalyzedResultsDirectory!);

        Directory.CreateDirectory(options.ReportsDirectory!);
        await File.WriteAllTextAsync(Path.Combine(options.ReportsDirectory!, "summary.md"), renderer.BuildMarkdown(results));
        await File.WriteAllTextAsync(Path.Combine(options.ReportsDirectory!, "summary.csv"), renderer.BuildCsv(results));

        Console.WriteLine($"Reports written to: {options.ReportsDirectory}");
        return 0;
    }

    private static async Task<int> RunComparisonModeAsync(ChartsOptions options)
    {
        var loader = new ChartsArtifactLoader();
        var seriesComparisons = await loader.LoadSeriesComparisonsAsync(options.ComparisonResultsDirectory!);

        Directory.CreateDirectory(options.ReportsDirectory!);
        if (seriesComparisons.Count > 0)
        {
            var seriesRenderer = new SeriesComparisonReportRenderer();
            await File.WriteAllTextAsync(
                Path.Combine(options.ReportsDirectory!, "comparison-summary.md"),
                seriesRenderer.BuildMarkdown(seriesComparisons));
            await File.WriteAllTextAsync(
                Path.Combine(options.ReportsDirectory!, "comparison-summary.csv"),
                seriesRenderer.BuildCsv(seriesComparisons));

            Console.WriteLine($"Comparison reports written to: {options.ReportsDirectory}");
            return 0;
        }

        var legacyComparisons = await loader.LoadLegacyComparisonsAsync(options.ComparisonResultsDirectory!);
        var legacyRenderer = new LegacyComparisonReportRenderer();

        await File.WriteAllTextAsync(
            Path.Combine(options.ReportsDirectory!, "comparison-summary.md"),
            legacyRenderer.BuildMarkdown(legacyComparisons));
        await File.WriteAllTextAsync(
            Path.Combine(options.ReportsDirectory!, "comparison-summary.csv"),
            legacyRenderer.BuildCsv(legacyComparisons));

        Console.WriteLine($"Comparison reports written to: {options.ReportsDirectory}");
        return 0;
    }
}
