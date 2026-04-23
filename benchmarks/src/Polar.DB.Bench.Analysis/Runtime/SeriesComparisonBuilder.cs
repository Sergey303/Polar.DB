using System;
using System.Collections.Generic;
using System.Linq;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Analysis.Runtime;

/// <summary>
/// Builds stage4 comparison-series artifacts.
/// Input is a comparison set: related warmup/measured runs from all targets.
/// Output is one derived artifact with aggregated measured statistics per target.
/// This builder is engine-family agnostic: it does not hardcode Polar.DB or SQLite keys.
/// </summary>
internal sealed class SeriesComparisonBuilder
{
    private const string WarmupRunRole = "warmup";
    private const string MeasuredRunRole = "measured";
    private const string ImportedReferenceExperimentKey = "persons-load-build-reopen-random-lookup";

    /// <summary>
    /// Creates one comparison-series artifact for one comparison set id.
    /// Warmup runs remain in raw facts but are excluded from metric aggregation.
    /// </summary>
    public CrossEngineComparisonSeriesResult Build(
        IReadOnlyList<RawRunEntry> filteredRuns,
        string experimentKey,
        string comparisonSetId)
    {
        var setRuns = filteredRuns
            .Where(item => item.Result.ComparisonSetId?.Equals(comparisonSetId, StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();

        if (setRuns.Length == 0)
        {
            throw new InvalidOperationException($"No runs found for comparison set '{comparisonSetId}'.");
        }

        // Group by engine key (which represents the runtime variant / target).
        var engineGroups = setRuns
            .GroupBy(item => item.Result.EngineKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (engineGroups.Length < 2)
        {
            throw new InvalidOperationException(
                $"Comparison set '{comparisonSetId}' must contain at least two distinct engine targets. Found: {engineGroups.Length}.");
        }

        foreach (var group in engineGroups)
        {
            if (!group.Any(IsMeasuredRun))
            {
                throw new InvalidOperationException(
                    $"Comparison set '{comparisonSetId}' has no measured runs for target '{group.Key}'.");
            }
        }

        var timestampUtc = DateTimeOffset.UtcNow;
        var timestampToken = timestampUtc.ToString("yyyy-MM-ddTHH-mm-ssZ");
        var datasetProfileKey = ComparisonValueHelpers.ResolveSharedOrMixed(setRuns.Select(item => item.Result.DatasetProfileKey));
        var fairnessProfileKey = ComparisonValueHelpers.ResolveSharedOrMixed(setRuns.Select(item => item.Result.FairnessProfileKey));
        var environmentClass = ComparisonValueHelpers.ResolveSharedOrMixed(setRuns.Select(item => item.Result.Environment.EnvironmentClass));
        var notes = new List<string>
        {
            "Comparison set groups related runs and avoids comparing unrelated latest single runs.",
            "Only measured runs are aggregated into min/max/average/median statistics.",
            "No policy evaluation is included in this artifact."
        };

        if (experimentKey.Equals(ImportedReferenceExperimentKey, StringComparison.OrdinalIgnoreCase))
        {
            notes.Add("Imported reference workload semantics: reverse bulk load, point-lookup-ready reopen state, one direct lookup, then random lookup batch.");
            notes.Add("Reference-normalized random lookup batch target: 10000 operations.");
        }

        return new CrossEngineComparisonSeriesResult
        {
            ComparisonId = $"{timestampToken}__{experimentKey}__{datasetProfileKey}__{comparisonSetId}__multi-target",
            TimestampUtc = timestampUtc,
            ExperimentKey = experimentKey,
            ComparisonSetId = comparisonSetId,
            DatasetProfileKey = datasetProfileKey,
            FairnessProfileKey = fairnessProfileKey,
            EnvironmentClass = environmentClass,
            Engines = engineGroups.Select(g => g.Key).ToArray(),
            EngineSeries = engineGroups
                .Select(group => BuildEngineSeriesEntry(group.Key, group.ToArray()))
                .ToArray(),
            Notes = notes
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

        var elapsed = measuredRuns.Select(x => ComparisonMetricReader.ReadMetric(x.Result, "elapsedMsSingleRun", "elapsedMsTotal")).ToArray();
        var load = measuredRuns.Select(x => ComparisonMetricReader.ReadMetric(x.Result, "loadMs")).ToArray();
        var build = measuredRuns.Select(x => ComparisonMetricReader.ReadMetric(x.Result, "buildMs")).ToArray();
        var reopen = measuredRuns.Select(x => ComparisonMetricReader.ReadMetric(x.Result, "reopenRefreshMs", "reopenMs")).ToArray();
        var lookup = measuredRuns.Select(x => ComparisonMetricReader.ReadMetric(x.Result, "randomPointLookupMs")).ToArray();
        var lookupCount = measuredRuns.Select(x => ComparisonMetricReader.ReadMetric(x.Result, "randomPointLookupCount")).ToArray();
        var totalBytes = measuredRuns.Select(x => ComparisonMetricReader.ReadTotalArtifactBytes(x.Result)).ToArray();
        var primaryBytes = measuredRuns.Select(x => ComparisonMetricReader.ReadPrimaryArtifactBytes(x.Result)).ToArray();
        var sideBytes = measuredRuns.Select(x => ComparisonMetricReader.ReadSideArtifactBytes(x.Result)).ToArray();

        return new CrossEngineSeriesEngineEntry
        {
            EngineKey = engineKey,
            MeasuredRunCount = measuredCount,
            WarmupRunCount = warmupCount,
            TechnicalSuccessCount = technicalSuccessCount,
            SemanticSuccessCount = semanticSuccessCount,
            SemanticEvaluatedCount = semanticEvaluatedCount,
            RawResultPaths = measuredRuns.Select(x => x.Path.Replace('\\', '/')).ToArray(),
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
