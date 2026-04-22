using System.Globalization;
using System.Text.Json;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;

namespace Polar.DB.Bench.Analysis.Runtime;

public static class AnalysisApplication
{
    private const string PolarEngineKey = "polar-db";
    private const string SqliteEngineKey = "sqlite";
    private const string WarmupRunRole = "warmup";
    private const string MeasuredRunRole = "measured";

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
                item.Result.EngineKey.Equals(PolarEngineKey, StringComparison.OrdinalIgnoreCase) ||
                item.Result.EngineKey.Equals(SqliteEngineKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (filtered.Length == 0)
        {
            throw new InvalidOperationException("No matching raw results were found for comparison mode.");
        }

        var comparisonSetId = ResolveComparisonSetId(filtered, options.ComparisonSetId);
        if (!string.IsNullOrWhiteSpace(comparisonSetId))
        {
            // Stage4 compares engines inside one set so both sides are measured under the same run series.
            var setRuns = filtered
                .Where(item => item.Result.ComparisonSetId?.Equals(comparisonSetId, StringComparison.OrdinalIgnoreCase) == true)
                .ToArray();

            var seriesResult = BuildSeriesComparison(setRuns, options, comparisonSetId);
            Directory.CreateDirectory(options.ComparisonOutputDirectory!);

            var timestampToken = seriesResult.TimestampUtc.ToString("yyyy-MM-ddTHH-mm-ssZ");
            var outputPath = ResultPathBuilder.BuildComparisonSeriesResultPath(
                options.ComparisonOutputDirectory!,
                timestampToken,
                seriesResult.ExperimentKey,
                seriesResult.DatasetProfileKey ?? "mixed",
                seriesResult.FairnessProfileKey ?? "mixed",
                ToFileToken(seriesResult.ComparisonSetId));

            await using var stream = File.Create(outputPath);
            await JsonSerializer.SerializeAsync(stream, seriesResult, JsonDefaults.Default);
            Console.WriteLine($"Comparison series result written: {outputPath}");
            return 0;
        }

        var legacyResult = BuildLegacyComparison(filtered, options);
        Directory.CreateDirectory(options.ComparisonOutputDirectory!);
        var legacyTimestampToken = legacyResult.TimestampUtc.ToString("yyyy-MM-ddTHH-mm-ssZ");
        var legacyPath = ResultPathBuilder.BuildComparisonResultPath(
            options.ComparisonOutputDirectory!,
            legacyTimestampToken,
            legacyResult.ExperimentKey,
            legacyResult.DatasetProfileKey ?? "mixed",
            legacyResult.FairnessProfileKey ?? "mixed");

        await using (var stream = File.Create(legacyPath))
        {
            await JsonSerializer.SerializeAsync(stream, legacyResult, JsonDefaults.Default);
        }

        Console.WriteLine($"Comparison result written: {legacyPath}");
        return 0;
    }

    private static CrossEngineComparisonSeriesResult BuildSeriesComparison(
        IReadOnlyList<RawRunEntry> setRuns,
        AnalysisOptions options,
        string comparisonSetId)
    {
        if (setRuns.Count == 0)
        {
            throw new InvalidOperationException($"No runs found for comparison set '{comparisonSetId}'.");
        }

        var polarRuns = setRuns.Where(x => x.Result.EngineKey.Equals(PolarEngineKey, StringComparison.OrdinalIgnoreCase)).ToArray();
        var sqliteRuns = setRuns.Where(x => x.Result.EngineKey.Equals(SqliteEngineKey, StringComparison.OrdinalIgnoreCase)).ToArray();

        if (!polarRuns.Any(IsMeasuredRun))
        {
            throw new InvalidOperationException($"Comparison set '{comparisonSetId}' has no measured Polar.DB runs.");
        }

        if (!sqliteRuns.Any(IsMeasuredRun))
        {
            throw new InvalidOperationException($"Comparison set '{comparisonSetId}' has no measured SQLite runs.");
        }

        var timestampUtc = DateTimeOffset.UtcNow;
        var timestampToken = timestampUtc.ToString("yyyy-MM-ddTHH-mm-ssZ");
        var datasetProfileKey = ResolveSharedOrMixed(setRuns.Select(item => item.Result.DatasetProfileKey));
        var fairnessProfileKey = ResolveSharedOrMixed(setRuns.Select(item => item.Result.FairnessProfileKey));
        var environmentClass = ResolveSharedOrMixed(setRuns.Select(item => item.Result.Environment.EnvironmentClass));

        var engines = new[] { PolarEngineKey, SqliteEngineKey };
        var engineSeries = new[]
        {
            BuildEngineSeriesEntry(PolarEngineKey, polarRuns),
            BuildEngineSeriesEntry(SqliteEngineKey, sqliteRuns)
        };

        return new CrossEngineComparisonSeriesResult
        {
            ComparisonId = $"{timestampToken}__{options.ComparisonExperimentKey}__{datasetProfileKey}__{comparisonSetId}__polar-db-vs-sqlite",
            TimestampUtc = timestampUtc,
            ExperimentKey = options.ComparisonExperimentKey!,
            ComparisonSetId = comparisonSetId,
            DatasetProfileKey = datasetProfileKey,
            FairnessProfileKey = fairnessProfileKey,
            EnvironmentClass = environmentClass,
            Engines = engines,
            EngineSeries = engineSeries,
            Notes = new List<string>
            {
                "Comparison set groups related runs and avoids comparing unrelated latest single runs.",
                "Only measured runs are aggregated into min/max/average/median statistics.",
                "No policy evaluation is included in this artifact."
            }
        };
    }

    private static CrossEngineSeriesEngineEntry BuildEngineSeriesEntry(string engineKey, IReadOnlyList<RawRunEntry> engineRuns)
    {
        var measuredRuns = engineRuns.Where(IsMeasuredRun).ToArray();
        var warmupCount = engineRuns.Count(IsWarmupRun);
        var measuredCount = measuredRuns.Length;
        var technicalSuccessCount = measuredRuns.Count(x => x.Result.TechnicalSuccess);
        var semanticEvaluatedCount = measuredRuns.Count(x => x.Result.SemanticSuccess.HasValue);
        var semanticSuccessCount = measuredRuns.Count(x => x.Result.SemanticSuccess == true);

        var elapsed = measuredRuns.Select(x => ReadMetric(x.Result, "elapsedMsSingleRun", "elapsedMsTotal")).ToArray();
        var load = measuredRuns.Select(x => ReadMetric(x.Result, "loadMs")).ToArray();
        var build = measuredRuns.Select(x => ReadMetric(x.Result, "buildMs")).ToArray();
        var reopen = measuredRuns.Select(x => ReadMetric(x.Result, "reopenRefreshMs", "reopenMs")).ToArray();
        var lookup = measuredRuns.Select(x => ReadMetric(x.Result, "randomPointLookupMs")).ToArray();
        var totalBytes = measuredRuns.Select(x => ReadTotalArtifactBytes(x.Result)).ToArray();
        var primaryBytes = measuredRuns.Select(x => ReadPrimaryArtifactBytes(x.Result)).ToArray();
        var sideBytes = measuredRuns
            .Select(x => ReadSideArtifactBytes(x.Result))
            .ToArray();

        return new CrossEngineSeriesEngineEntry
        {
            EngineKey = engineKey,
            MeasuredRunCount = measuredCount,
            WarmupRunCount = warmupCount,
            TechnicalSuccessCount = technicalSuccessCount,
            SemanticSuccessCount = semanticSuccessCount,
            SemanticEvaluatedCount = semanticEvaluatedCount,
            RawResultPaths = measuredRuns.Select(x => x.Path.Replace('\\', '/')).ToArray(),
            ElapsedMs = BuildStats(elapsed),
            LoadMs = BuildStats(load),
            BuildMs = BuildStats(build),
            ReopenMs = BuildStats(reopen),
            LookupMs = BuildStats(lookup),
            TotalArtifactBytes = BuildStats(totalBytes),
            PrimaryArtifactBytes = BuildStats(primaryBytes),
            SideArtifactBytes = BuildStats(sideBytes)
        };
    }

    private static string? ResolveComparisonSetId(IReadOnlyList<RawRunEntry> filteredRuns, string? explicitSetId)
    {
        var availableSets = filteredRuns
            .Where(item => !string.IsNullOrWhiteSpace(item.Result.ComparisonSetId))
            .GroupBy(item => item.Result.ComparisonSetId!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                SetId = group.Key,
                Items = group.ToArray(),
                Latest = group.Max(item => item.Result.TimestampUtc)
            })
            .ToArray();

        if (!string.IsNullOrWhiteSpace(explicitSetId))
        {
            var selected = availableSets.FirstOrDefault(set => set.SetId.Equals(explicitSetId, StringComparison.OrdinalIgnoreCase));
            if (selected is null)
            {
                throw new InvalidOperationException(
                    $"Comparison set '{explicitSetId}' was not found among matching raw results.");
            }

            return selected.SetId;
        }

        var latestCompleteSet = availableSets
            .Where(set => set.Items.Any(item => item.Result.EngineKey.Equals(PolarEngineKey, StringComparison.OrdinalIgnoreCase) && IsMeasuredRun(item)))
            .Where(set => set.Items.Any(item => item.Result.EngineKey.Equals(SqliteEngineKey, StringComparison.OrdinalIgnoreCase) && IsMeasuredRun(item)))
            .OrderByDescending(set => set.Latest)
            .FirstOrDefault();

        return latestCompleteSet?.SetId;
    }

    private static CrossEngineComparisonResult BuildLegacyComparison(IReadOnlyList<RawRunEntry> filtered, AnalysisOptions options)
    {
        var latestByEngine = filtered
            .GroupBy(item => item.Result.EngineKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.Result.TimestampUtc).First())
            .ToDictionary(item => item.Result.EngineKey, item => item, StringComparer.OrdinalIgnoreCase);

        if (!latestByEngine.TryGetValue(PolarEngineKey, out var polar))
        {
            throw new InvalidOperationException("Comparison mode requires at least one matching Polar.DB raw result.");
        }

        if (!latestByEngine.TryGetValue(SqliteEngineKey, out var sqlite))
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
            .OrderBy(item => item.Result.EngineKey.Equals(PolarEngineKey, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .Select(item => BuildLegacyEntry(item.Result, item.Path))
            .ToArray();

        return new CrossEngineComparisonResult
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
                "Legacy fallback: no comparison-set metadata found in matching runs.",
                "Latest matching run per engine is selected by timestamp.",
                "Use --comparison-set and measured run series for stable stage4 comparison."
            }
        };
    }

    private static CrossEngineComparisonEntry BuildLegacyEntry(RunResult run, string rawPath)
    {
        var elapsedMs = ReadMetric(run, "elapsedMsSingleRun", "elapsedMsTotal") ?? 0.0;
        var loadMs = ReadMetric(run, "loadMs") ?? 0.0;
        var buildMs = ReadMetric(run, "buildMs") ?? 0.0;
        var reopenMs = ReadMetric(run, "reopenRefreshMs", "reopenMs") ?? 0.0;
        var lookupMs = ReadMetric(run, "randomPointLookupMs") ?? 0.0;
        var totalArtifactBytes = ReadTotalArtifactBytes(run) ?? 0.0;
        var primaryArtifactBytes = ReadPrimaryArtifactBytes(run) ?? 0.0;
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

    private static bool IsMeasuredRun(RawRunEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.Result.RunRole) ||
               entry.Result.RunRole.Equals(MeasuredRunRole, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWarmupRun(RawRunEntry entry)
    {
        return entry.Result.RunRole?.Equals(WarmupRunRole, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static double? ReadMetric(RunResult run, params string[] metricKeys)
    {
        foreach (var metricKey in metricKeys)
        {
            var metric = run.Metrics.FirstOrDefault(x => x.MetricKey.Equals(metricKey, StringComparison.OrdinalIgnoreCase));
            if (metric is not null)
            {
                return metric.Value;
            }
        }

        return null;
    }

    private static double? ReadTotalArtifactBytes(RunResult run)
    {
        var metric = ReadMetric(run, "totalArtifactBytes");
        if (metric.HasValue)
        {
            return metric.Value;
        }

        if (run.Artifacts.Count > 0)
        {
            return run.Artifacts.Sum(artifact => (double)artifact.Bytes);
        }

        return null;
    }

    private static double? ReadPrimaryArtifactBytes(RunResult run)
    {
        var metric = ReadMetric(run, "primaryDataBytes", "primaryDatabaseBytes");
        if (metric.HasValue)
        {
            return metric.Value;
        }

        if (run.Artifacts.Count > 0)
        {
            var primary = run.Artifacts
                .Where(artifact =>
                    artifact.Role is ArtifactRole.PrimaryData or
                    ArtifactRole.PrimaryDatabase)
                .Sum(artifact => (double)artifact.Bytes);

            if (primary > 0.0)
            {
                return primary;
            }
        }

        if (run.EngineDiagnostics is not null)
        {
            if (TryReadDiagnostic(run.EngineDiagnostics, "primaryDataFileBytes", out var polarPrimary))
            {
                return polarPrimary;
            }

            if (TryReadDiagnostic(run.EngineDiagnostics, "dbBytes", out var sqlitePrimary))
            {
                return sqlitePrimary;
            }
        }

        return null;
    }

    private static double? ReadSideArtifactBytes(RunResult run)
    {
        var metric = ReadMetric(run, "sideArtifactBytes");
        if (metric.HasValue)
        {
            return metric.Value;
        }

        var total = ReadTotalArtifactBytes(run);
        var primary = ReadPrimaryArtifactBytes(run);
        if (total.HasValue && primary.HasValue)
        {
            return Math.Max(0.0, total.Value - primary.Value);
        }

        return null;
    }

    private static MetricSeriesStats BuildStats(IReadOnlyList<double?> samples)
    {
        var values = samples
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .OrderBy(value => value)
            .ToArray();

        if (values.Length == 0)
        {
            return new MetricSeriesStats
            {
                Count = samples.Count,
                MissingCount = samples.Count,
                Min = null,
                Max = null,
                Average = null,
                Median = null
            };
        }

        return new MetricSeriesStats
        {
            Count = samples.Count,
            MissingCount = samples.Count - values.Length,
            Min = values[0],
            Max = values[^1],
            Average = values.Average(),
            Median = ReadMedian(values)
        };
    }

    private static double ReadMedian(double[] sortedValues)
    {
        if (sortedValues.Length == 0)
        {
            return 0.0;
        }

        var middle = sortedValues.Length / 2;
        if (sortedValues.Length % 2 == 1)
        {
            return sortedValues[middle];
        }

        return (sortedValues[middle - 1] + sortedValues[middle]) / 2.0;
    }

    private static bool TryReadDiagnostic(IReadOnlyDictionary<string, string> diagnostics, string key, out double value)
    {
        if (diagnostics.TryGetValue(key, out var text) &&
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
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

    private static string ToFileToken(string value)
    {
        var chars = value
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray();
        return new string(chars);
    }

    private sealed record RawRunEntry(RunResult Result, string Path);
}
