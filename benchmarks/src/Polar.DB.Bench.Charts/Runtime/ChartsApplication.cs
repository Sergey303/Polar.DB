using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Entry point for benchmark chart/report generation.
/// It supports analyzed-result summaries and cross-engine comparison summaries.
/// </summary>
public static class ChartsApplication
{
    private const string ManifestFileName = "experiment.json";
    private const string RawDirectoryName = "raw";
    private const string AnalyzedDirectoryName = "analyzed";
    private const string ComparisonsDirectoryName = "comparisons";
    private const string IndexFileName = "index.html";

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
        await WriteExperimentIndexAsync(options, loader);

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
        }
        else
        {
            var legacyComparisons = await loader.LoadLegacyComparisonsAsync(options.ComparisonResultsDirectory!);
            var legacyRenderer = new LegacyComparisonReportRenderer();

            await File.WriteAllTextAsync(
                Path.Combine(options.ReportsDirectory!, "comparison-summary.md"),
                legacyRenderer.BuildMarkdown(legacyComparisons));
            await File.WriteAllTextAsync(
                Path.Combine(options.ReportsDirectory!, "comparison-summary.csv"),
                legacyRenderer.BuildCsv(legacyComparisons));
        }

        await WriteExperimentIndexAsync(options, loader);

        Console.WriteLine($"Comparison reports written to: {options.ReportsDirectory}");
        return 0;
    }

    private static async Task WriteExperimentIndexAsync(ChartsOptions options, ChartsArtifactLoader loader)
    {
        var experimentDirectory = ResolveExperimentDirectory(options);
        var manifestPath = Path.Combine(experimentDirectory, ManifestFileName);
        var manifest = await loader.TryLoadSingleAsync<ExperimentManifest>(manifestPath)
                       ?? throw new InvalidOperationException(
                           $"Failed to load experiment manifest at '{manifestPath}'.");

        var rawDirectory = Path.Combine(experimentDirectory, RawDirectoryName);
        var analyzedDirectory = Path.Combine(experimentDirectory, AnalyzedDirectoryName);
        var comparisonsDirectory = Path.Combine(experimentDirectory, ComparisonsDirectoryName);

        var latestEngines = await loader.TryLoadSingleAsync<LatestEnginesComparisonArtifact>(
            Path.Combine(comparisonsDirectory, "latest-engines.json"));
        var latestHistory = await loader.TryLoadSingleAsync<LatestHistoryComparisonArtifact>(
            Path.Combine(comparisonsDirectory, "latest-history.json"));
        var latestOtherExperiments = await loader.TryLoadSingleAsync<LatestOtherExperimentsComparisonArtifact>(
            Path.Combine(comparisonsDirectory, "latest-other-experiments.json"));

        var localAnalyzedSeries = Directory.Exists(analyzedDirectory)
            ? await loader.LoadLocalAnalyzedSeriesAsync(analyzedDirectory)
            : (IReadOnlyList<LocalAnalyzedSeriesResult>)Array.Empty<LocalAnalyzedSeriesResult>();

        var model = new ExperimentIndexModel(
            Manifest: manifest,
            LatestEngines: latestEngines,
            LatestHistory: latestHistory,
            LatestOtherExperiments: latestOtherExperiments,
            LocalAnalyzedSeries: localAnalyzedSeries,
            RawArtifacts: ListRawArtifactLinks(experimentDirectory, rawDirectory),
            AnalyzedArtifacts: ListArtifactLinks(experimentDirectory, analyzedDirectory, "*.json"),
            ComparisonArtifacts: ListArtifactLinks(experimentDirectory, comparisonsDirectory, "*.json"),
            GeneratedAtUtc: DateTimeOffset.UtcNow);

        var renderer = new ExperimentIndexRenderer();
        var html = renderer.BuildHtml(model);
        var indexPath = Path.Combine(experimentDirectory, IndexFileName);
        await File.WriteAllTextAsync(indexPath, html, System.Text.Encoding.UTF8);
        Console.WriteLine($"Experiment index written: {indexPath}");
    }

    private static string ResolveExperimentDirectory(ChartsOptions options)
    {
        static string? ResolveFromDirectory(string? directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return null;
            }

            var fullPath = Path.GetFullPath(directory);
            if (!Directory.Exists(fullPath))
            {
                return null;
            }

            var manifestInCurrent = Path.Combine(fullPath, ManifestFileName);
            if (File.Exists(manifestInCurrent))
            {
                return fullPath;
            }

            var parent = Directory.GetParent(fullPath)?.FullName;
            if (!string.IsNullOrWhiteSpace(parent) &&
                File.Exists(Path.Combine(parent, ManifestFileName)))
            {
                return parent;
            }

            return null;
        }

        return ResolveFromDirectory(options.ReportsDirectory)
               ?? ResolveFromDirectory(options.ComparisonResultsDirectory)
               ?? ResolveFromDirectory(options.AnalyzedResultsDirectory)
               ?? throw new InvalidOperationException(
                   "Failed to resolve experiment directory from charts arguments. Expected canonical experiment structure with experiment.json.");
    }

    private static IReadOnlyList<ArtifactFileLink> ListArtifactLinks(
        string experimentDirectory,
        string artifactDirectory,
        string pattern)
    {
        if (!Directory.Exists(artifactDirectory))
        {
            return Array.Empty<ArtifactFileLink>();
        }

        return Directory.GetFiles(artifactDirectory, pattern, SearchOption.TopDirectoryOnly)
            .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var relative = Path.GetRelativePath(experimentDirectory, path).Replace('\\', '/');
                var updatedUtc = File.GetLastWriteTimeUtc(path);
                return new ArtifactFileLink(relative, new DateTimeOffset(updatedUtc, TimeSpan.Zero));
            })
            .ToArray();
    }

    /// <summary>
    /// Lists raw artifact links from the raw directory, matching both canonical *.run.json and short r.*.json files.
    /// Deduplicates paths case-insensitively.
    /// </summary>
    private static IReadOnlyList<ArtifactFileLink> ListRawArtifactLinks(
        string experimentDirectory,
        string rawDirectory)
    {
        if (!Directory.Exists(rawDirectory))
        {
            return Array.Empty<ArtifactFileLink>();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<string>();

        foreach (var file in Directory.EnumerateFiles(rawDirectory, "*.run.json", SearchOption.TopDirectoryOnly))
        {
            if (seen.Add(file))
            {
                files.Add(file);
            }
        }

        foreach (var file in Directory.EnumerateFiles(rawDirectory, "r.*.json", SearchOption.TopDirectoryOnly))
        {
            if (seen.Add(file))
            {
                files.Add(file);
            }
        }

        return files
            .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var relative = Path.GetRelativePath(experimentDirectory, path).Replace('\\', '/');
                var updatedUtc = File.GetLastWriteTimeUtc(path);
                return new ArtifactFileLink(relative, new DateTimeOffset(updatedUtc, TimeSpan.Zero));
            })
            .ToArray();
    }
}
