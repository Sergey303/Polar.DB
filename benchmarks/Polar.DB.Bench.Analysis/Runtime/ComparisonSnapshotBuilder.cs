using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Analysis.Runtime;

internal static class ComparisonSnapshotBuilder
{
    private const string WarmupRunRole = "warmup";
    private const string MeasuredRunRole = "measured";

    public static ComparisonSnapshot? BuildLatestSuccessfulMeasuredSnapshot(
        string experimentKey,
        IReadOnlyList<RawRunEntry> runs,
        IReadOnlyList<string> engineKeys)
    {
        var fromSets = BuildSuccessfulMeasuredSnapshots(experimentKey, runs, engineKeys);
        if (fromSets.Count > 0)
        {
            return fromSets[^1];
        }

        return BuildLegacyLatestSnapshot(experimentKey, runs, engineKeys);
    }

    public static IReadOnlyList<ComparisonSnapshot> BuildSuccessfulMeasuredSnapshots(
        string experimentKey,
        IReadOnlyList<RawRunEntry> runs,
        IReadOnlyList<string> engineKeys)
    {
        var experimentRuns = runs
            .Where(item => item.Result.ExperimentKey.Equals(experimentKey, StringComparison.OrdinalIgnoreCase))
            .Where(item => engineKeys.Any(engine => engine.Equals(item.Result.EngineKey, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var snapshots = experimentRuns
            .Where(item => !string.IsNullOrWhiteSpace(item.Result.ComparisonSetId))
            .GroupBy(item => item.Result.ComparisonSetId!, StringComparer.OrdinalIgnoreCase)
            .Select(group => TryBuildSetSnapshot(experimentKey, group.Key, group.ToArray(), engineKeys))
            .Where(snapshot => snapshot is not null)
            .Select(snapshot => snapshot!)
            .OrderBy(snapshot => snapshot.SnapshotTimestampUtc)
            .ToArray();

        return snapshots;
    }

    private static ComparisonSnapshot? TryBuildSetSnapshot(
        string experimentKey,
        string comparisonSetId,
        IReadOnlyList<RawRunEntry> setRuns,
        IReadOnlyList<string> engineKeys)
    {
        var engineSeries = new List<CrossEngineSeriesEngineEntry>(engineKeys.Count);
        var allMeasuredRuns = new List<RawRunEntry>();

        foreach (var engineKey in engineKeys)
        {
            var engineRuns = setRuns
                .Where(item => item.Result.EngineKey.Equals(engineKey, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var measuredRuns = engineRuns.Where(IsMeasuredRun).ToArray();
            if (measuredRuns.Length == 0)
            {
                return null;
            }

            if (!measuredRuns.Any(item => item.Result.TechnicalSuccess))
            {
                return null;
            }

            allMeasuredRuns.AddRange(measuredRuns);
            engineSeries.Add(BuildEngineSeriesEntry(engineKey, measuredRuns, engineRuns.Count(IsWarmupRun)));
        }

        return new ComparisonSnapshot
        {
            ExperimentKey = experimentKey,
            ComparisonSetId = comparisonSetId,
            SnapshotTimestampUtc = allMeasuredRuns.Max(item => item.Result.TimestampUtc),
            DatasetProfileKey = ComparisonValueHelpers.ResolveSharedOrMixed(allMeasuredRuns.Select(item => item.Result.DatasetProfileKey)),
            FairnessProfileKey = ComparisonValueHelpers.ResolveSharedOrMixed(allMeasuredRuns.Select(item => item.Result.FairnessProfileKey)),
            EnvironmentClass = ComparisonValueHelpers.ResolveSharedOrMixed(allMeasuredRuns.Select(item => item.Result.Environment.EnvironmentClass)),
            Engines = engineKeys.ToArray(),
            EngineSeries = engineSeries
        };
    }

    private static ComparisonSnapshot? BuildLegacyLatestSnapshot(
        string experimentKey,
        IReadOnlyList<RawRunEntry> runs,
        IReadOnlyList<string> engineKeys)
    {
        var selectedRuns = new List<RawRunEntry>(engineKeys.Count);
        foreach (var engineKey in engineKeys)
        {
            var latestRun = runs
                .Where(item => item.Result.ExperimentKey.Equals(experimentKey, StringComparison.OrdinalIgnoreCase))
                .Where(item => item.Result.EngineKey.Equals(engineKey, StringComparison.OrdinalIgnoreCase))
                .Where(IsMeasuredRun)
                .Where(item => item.Result.TechnicalSuccess)
                .OrderByDescending(item => item.Result.TimestampUtc)
                .FirstOrDefault();

            if (latestRun is null)
            {
                return null;
            }

            selectedRuns.Add(latestRun);
        }

        var engineSeries = selectedRuns
            .Select(item => BuildEngineSeriesEntry(item.Result.EngineKey, new[] { item }, warmupCount: 0))
            .ToArray();

        return new ComparisonSnapshot
        {
            ExperimentKey = experimentKey,
            ComparisonSetId = null,
            SnapshotTimestampUtc = selectedRuns.Max(item => item.Result.TimestampUtc),
            DatasetProfileKey = ComparisonValueHelpers.ResolveSharedOrMixed(selectedRuns.Select(item => item.Result.DatasetProfileKey)),
            FairnessProfileKey = ComparisonValueHelpers.ResolveSharedOrMixed(selectedRuns.Select(item => item.Result.FairnessProfileKey)),
            EnvironmentClass = ComparisonValueHelpers.ResolveSharedOrMixed(selectedRuns.Select(item => item.Result.Environment.EnvironmentClass)),
            Engines = engineKeys.ToArray(),
            EngineSeries = engineSeries
        };
    }

    private static CrossEngineSeriesEngineEntry BuildEngineSeriesEntry(
        string engineKey,
        IReadOnlyList<RawRunEntry> measuredRuns,
        int warmupCount)
    {
        var measuredCount = measuredRuns.Count;
        var technicalSuccessCount = measuredRuns.Count(item => item.Result.TechnicalSuccess);
        var semanticEvaluatedCount = measuredRuns.Count(item => item.Result.SemanticSuccess.HasValue);
        var semanticSuccessCount = measuredRuns.Count(item => item.Result.SemanticSuccess == true);

        var elapsed = measuredRuns.Select(item => ComparisonMetricReader.ReadMetric(item.Result, "elapsedMsSingleRun", "elapsedMsTotal")).ToArray();
        var load = measuredRuns.Select(item => ComparisonMetricReader.ReadMetric(item.Result, "loadMs")).ToArray();
        var build = measuredRuns.Select(item => ComparisonMetricReader.ReadMetric(item.Result, "buildMs")).ToArray();
        var reopen = measuredRuns.Select(item => ComparisonMetricReader.ReadMetric(item.Result, "reopenRefreshMs", "reopenMs")).ToArray();
        var lookup = measuredRuns.Select(item => ComparisonMetricReader.ReadMetric(item.Result, "randomPointLookupMs")).ToArray();
        var lookupCount = measuredRuns.Select(item => ComparisonMetricReader.ReadMetric(item.Result, "randomPointLookupCount")).ToArray();
        var totalBytes = measuredRuns.Select(item => ComparisonMetricReader.ReadTotalArtifactBytes(item.Result)).ToArray();
        var primaryBytes = measuredRuns.Select(item => ComparisonMetricReader.ReadPrimaryArtifactBytes(item.Result)).ToArray();
        var sideBytes = measuredRuns.Select(item => ComparisonMetricReader.ReadSideArtifactBytes(item.Result)).ToArray();

        return new CrossEngineSeriesEngineEntry
        {
            EngineKey = engineKey,
            MeasuredRunCount = measuredCount,
            WarmupRunCount = warmupCount,
            TechnicalSuccessCount = technicalSuccessCount,
            SemanticSuccessCount = semanticSuccessCount,
            SemanticEvaluatedCount = semanticEvaluatedCount,
            RawResultPaths = measuredRuns.Select(item => item.Path.Replace('\\', '/')).ToArray(),
            ElapsedMs = MetricSeriesStatsBuilder.Build(elapsed),
            LoadMs = MetricSeriesStatsBuilder.Build(load),
            BuildMs = MetricSeriesStatsBuilder.Build(build),
            ReopenMs = MetricSeriesStatsBuilder.Build(reopen),
            LookupMs = MetricSeriesStatsBuilder.Build(lookup),
            LookupBatchCount = MetricSeriesStatsBuilder.Build(lookupCount),
            TotalArtifactBytes = MetricSeriesStatsBuilder.Build(totalBytes),
            PrimaryArtifactBytes = MetricSeriesStatsBuilder.Build(primaryBytes),
            SideArtifactBytes = MetricSeriesStatsBuilder.Build(sideBytes)
        };
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
}
