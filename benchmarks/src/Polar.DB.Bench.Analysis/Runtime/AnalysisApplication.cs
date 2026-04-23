using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    private const string ManifestFileName = "experiment.json";

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
        var experimentDirectory = AnalysisOptions.ResolveExperimentDirectory(options.RawResultsDirectory!);
        var comparisonOutputDirectory = AnalysisOptions.ResolveComparisonOutputDirectory(
            options.RawResultsDirectory!,
            options.ComparisonOutputDirectory);
        var analyzedResultsDirectory = AnalysisOptions.ResolveAnalyzedResultsDirectoryForComparison(
            options.RawResultsDirectory!,
            options.AnalyzedResultsDirectory);
        var manifestPath = Path.Combine(experimentDirectory, ManifestFileName);
        var manifest = await files.ReadAsync<ExperimentManifest>(manifestPath);

        if (!manifest.ExperimentKey.Equals(options.ComparisonExperimentKey!, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"--compare-experiment='{options.ComparisonExperimentKey}' does not match manifest experiment '{manifest.ExperimentKey}'.");
        }

        var compareConfig = ExperimentCompareConfigResolver.Resolve(manifest);
        var configuredTargetKeys = manifest.Targets.Keys
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rawRuns = await files.LoadRawRunsAsync(rawResultsDirectory);
        var selector = new ComparisonSelectionService();
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
            var seriesBuilder = new SeriesComparisonBuilder();
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
        }
        else
        {
            var legacyBuilder = new LegacyComparisonBuilder();
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
        }

        await WriteLatestComparisonArtifactsAsync(
            files,
            comparisonOutputDirectory,
            experimentDirectory,
            manifest,
            compareConfig,
            rawRuns,
            configuredTargetKeys);

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

    private static async Task WriteLatestComparisonArtifactsAsync(
        BenchmarkFileReader files,
        string comparisonOutputDirectory,
        string experimentDirectory,
        ExperimentManifest manifest,
        ResolvedCompareConfig compareConfig,
        IReadOnlyList<RawRunEntry> currentExperimentRuns,
        IReadOnlyList<string> configuredTargetKeys)
    {
        var latestSnapshot = ComparisonSnapshotBuilder.BuildLatestSuccessfulMeasuredSnapshot(
            manifest.ExperimentKey,
            currentExperimentRuns,
            configuredTargetKeys);
        var historySnapshots = ComparisonSnapshotBuilder.BuildSuccessfulMeasuredSnapshots(
            manifest.ExperimentKey,
            currentExperimentRuns,
            configuredTargetKeys);

        if (historySnapshots.Count == 0 && latestSnapshot is not null)
        {
            historySnapshots = new[] { latestSnapshot };
        }

        var latestEnginesArtifact = BuildLatestEnginesArtifact(
            manifest.ExperimentKey,
            configuredTargetKeys,
            latestSnapshot);
        var latestHistoryArtifact = BuildLatestHistoryArtifact(
            manifest.ExperimentKey,
            compareConfig.HistoryEnabled,
            historySnapshots);
        var latestOtherExperimentsArtifact = await BuildLatestOtherExperimentsArtifactAsync(
            files,
            experimentDirectory,
            manifest.ExperimentKey,
            compareConfig.OtherExperimentsEnabled,
            latestSnapshot);

        var latestEnginesPath = Path.Combine(comparisonOutputDirectory, "latest-engines.json");
        await files.WriteAsync(latestEnginesPath, latestEnginesArtifact);
        Console.WriteLine($"Derived comparison artifact written: {latestEnginesPath}");

        var latestHistoryPath = Path.Combine(comparisonOutputDirectory, "latest-history.json");
        await files.WriteAsync(latestHistoryPath, latestHistoryArtifact);
        Console.WriteLine($"Derived comparison artifact written: {latestHistoryPath}");

        var latestOtherPath = Path.Combine(comparisonOutputDirectory, "latest-other-experiments.json");
        await files.WriteAsync(latestOtherPath, latestOtherExperimentsArtifact);
        Console.WriteLine($"Derived comparison artifact written: {latestOtherPath}");
    }

    private static LatestEnginesComparisonArtifact BuildLatestEnginesArtifact(
        string experimentKey,
        IReadOnlyList<string> configuredTargetKeys,
        ComparisonSnapshot? latestSnapshot)
    {
        var enabled = configuredTargetKeys.Count > 1;
        var notes = new List<string>
        {
            "Compares latest successful measured series per target inside this experiment.",
            "Generated automatically when experiment manifest contains multiple targets."
        };

        if (!enabled)
        {
            notes.Add("Disabled: experiment has fewer than two configured targets.");
        }
        else if (latestSnapshot is null)
        {
            notes.Add("No complete successful measured series was found for all configured targets.");
        }

        return new LatestEnginesComparisonArtifact
        {
            ArtifactKind = "latest-engines",
            AnalysisTimestampUtc = DateTimeOffset.UtcNow,
            ExperimentKey = experimentKey,
            Enabled = enabled,
            Snapshot = enabled ? latestSnapshot : null,
            DerivedExpectations = BuildTargetExpectations(latestSnapshot),
            Notes = notes
        };
    }

    private static LatestHistoryComparisonArtifact BuildLatestHistoryArtifact(
        string experimentKey,
        bool historyEnabled,
        IReadOnlyList<ComparisonSnapshot> snapshots)
    {
        var notes = new List<string>
        {
            "Compares successful measured series of the same experiment over time."
        };
        if (!historyEnabled)
        {
            notes.Add("Disabled by experiment compare.history flag.");
        }

        return new LatestHistoryComparisonArtifact
        {
            ArtifactKind = "latest-history",
            AnalysisTimestampUtc = DateTimeOffset.UtcNow,
            ExperimentKey = experimentKey,
            Enabled = historyEnabled,
            Snapshots = historyEnabled ? snapshots : Array.Empty<ComparisonSnapshot>(),
            DerivedExpectations = BuildHistoryExpectations(historyEnabled ? snapshots : Array.Empty<ComparisonSnapshot>()),
            Notes = notes
        };
    }

    /// <summary>
    /// Builds cross-experiment context artifact.
    /// When otherExperimentsEnabled is true, auto-discovers other experiment folders
    /// under the experiments root directory (parent of current experiment folder).
    /// No manual experiment lists needed.
    /// This is informative context, not strict apples-to-apples comparison.
    /// </summary>
    private static async Task<LatestOtherExperimentsComparisonArtifact> BuildLatestOtherExperimentsArtifactAsync(
        BenchmarkFileReader files,
        string currentExperimentDirectory,
        string currentExperimentKey,
        bool otherExperimentsEnabled,
        ComparisonSnapshot? currentSnapshot)
    {
        var notes = new List<string>
        {
            "Cross-experiment comparison is context only; workloads may be semantically different.",
            "No workload similarity or apples-to-apples scoring is inferred in this artifact."
        };

        if (!otherExperimentsEnabled)
        {
            notes.Add("Disabled by experiment compare.otherExperiments flag.");
            return new LatestOtherExperimentsComparisonArtifact
            {
                ArtifactKind = "latest-other-experiments",
                AnalysisTimestampUtc = DateTimeOffset.UtcNow,
                ExperimentKey = currentExperimentKey,
                Enabled = false,
                CurrentExperimentSnapshot = currentSnapshot,
                OtherExperimentSnapshots = Array.Empty<ComparisonSnapshot>(),
                DerivedExpectations = Array.Empty<string>(),
                Notes = notes
            };
        }

        var experimentsRoot = Directory.GetParent(currentExperimentDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(experimentsRoot))
        {
            notes.Add("Cannot resolve experiments root directory.");
            return new LatestOtherExperimentsComparisonArtifact
            {
                ArtifactKind = "latest-other-experiments",
                AnalysisTimestampUtc = DateTimeOffset.UtcNow,
                ExperimentKey = currentExperimentKey,
                Enabled = true,
                CurrentExperimentSnapshot = currentSnapshot,
                OtherExperimentSnapshots = Array.Empty<ComparisonSnapshot>(),
                DerivedExpectations = Array.Empty<string>(),
                Notes = notes
            };
        }

        // Auto-discover other experiment folders from the experiments root.
        var otherExperimentKeys = DiscoverOtherExperimentKeys(experimentsRoot, currentExperimentKey);

        var snapshots = new List<ComparisonSnapshot>();
        foreach (var otherExperimentKey in otherExperimentKeys)
        {
            var otherExperimentDirectory = Path.Combine(experimentsRoot, otherExperimentKey);
            var otherManifestPath = Path.Combine(otherExperimentDirectory, ManifestFileName);
            if (!Directory.Exists(otherExperimentDirectory) || !File.Exists(otherManifestPath))
            {
                notes.Add($"Skipped '{otherExperimentKey}': experiment directory or manifest not found.");
                continue;
            }

            var otherManifest = await files.ReadAsync<ExperimentManifest>(otherManifestPath);
            var otherRawDirectory = Path.Combine(otherExperimentDirectory, "raw");
            if (!Directory.Exists(otherRawDirectory))
            {
                notes.Add($"Skipped '{otherExperimentKey}': raw directory not found.");
                continue;
            }

            var otherRuns = await files.LoadRawRunsAsync(otherRawDirectory);
            var otherTargetKeys = otherManifest.Targets.Keys
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var otherSnapshot = ComparisonSnapshotBuilder.BuildLatestSuccessfulMeasuredSnapshot(
                otherManifest.ExperimentKey,
                otherRuns,
                otherTargetKeys);

            if (otherSnapshot is null)
            {
                notes.Add($"Skipped '{otherExperimentKey}': no complete successful measured series.");
                continue;
            }

            snapshots.Add(otherSnapshot);
        }

        return new LatestOtherExperimentsComparisonArtifact
        {
            ArtifactKind = "latest-other-experiments",
            AnalysisTimestampUtc = DateTimeOffset.UtcNow,
            ExperimentKey = currentExperimentKey,
            Enabled = true,
            CurrentExperimentSnapshot = currentSnapshot,
            OtherExperimentSnapshots = snapshots
                .OrderBy(snapshot => snapshot.ExperimentKey, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            DerivedExpectations = BuildOtherExperimentExpectations(currentSnapshot, snapshots),
            Notes = notes
        };
    }

    /// <summary>
    /// Discovers other experiment folders under the experiments root.
    /// Looks for subdirectories that contain experiment.json.
    /// Excludes the current experiment.
    /// </summary>
    private static IReadOnlyList<string> DiscoverOtherExperimentKeys(
        string experimentsRoot,
        string currentExperimentKey)
    {
        if (!Directory.Exists(experimentsRoot))
        {
            return Array.Empty<string>();
        }

        return Directory.GetDirectories(experimentsRoot)
            .Select(dir => Path.GetFileName(dir))
            .Where(name =>
                !string.IsNullOrWhiteSpace(name) &&
                !name.Equals(currentExperimentKey, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(Path.Combine(experimentsRoot, name, ManifestFileName)))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildTargetExpectations(ComparisonSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return Array.Empty<string>();
        }

        var expectations = new List<string>();
        foreach (var engine in snapshot.EngineSeries.OrderBy(item => item.EngineKey, StringComparer.OrdinalIgnoreCase))
        {
            if (engine.ElapsedMs.Average is not null)
            {
                expectations.Add(
                    $"Expected measured elapsed for '{engine.EngineKey}' near {engine.ElapsedMs.Average.Value.ToString("0.###", CultureInfo.InvariantCulture)} ms under the same fairness profile.");
            }
        }

        var rankedByElapsed = snapshot.EngineSeries
            .Where(item => item.ElapsedMs.Average is not null)
            .OrderBy(item => item.ElapsedMs.Average)
            .ToArray();
        if (rankedByElapsed.Length >= 2)
        {
            var fastest = rankedByElapsed[0];
            var second = rankedByElapsed[1];
            expectations.Add(
                $"In this snapshot '{fastest.EngineKey}' is faster than '{second.EngineKey}' by elapsed average context.");
        }

        return expectations;
    }

    private static IReadOnlyList<string> BuildHistoryExpectations(IReadOnlyList<ComparisonSnapshot> snapshots)
    {
        if (snapshots.Count < 2)
        {
            return snapshots.Count == 1
                ? new[] { "History has one snapshot only; trend expectations require at least two measured series." }
                : Array.Empty<string>();
        }

        var latest = snapshots[^1];
        var previous = snapshots[^2];
        var expectations = new List<string>();

        foreach (var latestEngine in latest.EngineSeries)
        {
            var previousEngine = previous.EngineSeries
                .FirstOrDefault(item => item.EngineKey.Equals(latestEngine.EngineKey, StringComparison.OrdinalIgnoreCase));
            if (previousEngine is null ||
                latestEngine.ElapsedMs.Average is null ||
                previousEngine.ElapsedMs.Average is null ||
                previousEngine.ElapsedMs.Average.Value == 0.0)
            {
                continue;
            }

            var deltaPercent =
                (latestEngine.ElapsedMs.Average.Value - previousEngine.ElapsedMs.Average.Value) /
                previousEngine.ElapsedMs.Average.Value * 100.0;
            expectations.Add(
                $"History trend for '{latestEngine.EngineKey}': elapsed average changed by {deltaPercent.ToString("0.##", CultureInfo.InvariantCulture)}% vs previous series.");
        }

        return expectations;
    }

    private static IReadOnlyList<string> BuildOtherExperimentExpectations(
        ComparisonSnapshot? currentSnapshot,
        IReadOnlyList<ComparisonSnapshot> otherSnapshots)
    {
        var expectations = new List<string>
        {
            "Use these comparisons as context for planning and diagnostics, not as strict apples-to-apples conclusions."
        };

        if (currentSnapshot is null || otherSnapshots.Count == 0)
        {
            return expectations;
        }

        expectations.Add(
            $"Current experiment '{currentSnapshot.ExperimentKey}' is shown with {otherSnapshots.Count} auto-discovered external context snapshots.");
        return expectations;
    }
}
