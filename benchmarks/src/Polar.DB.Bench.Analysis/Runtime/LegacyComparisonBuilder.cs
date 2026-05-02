using System;
using System.Collections.Generic;
using System.Linq;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Analysis.Runtime;

/// <summary>
/// Builds the legacy single-run comparison artifact.
/// This path exists for old raw data that does not have comparison-set metadata.
/// Failed runs and runs without the required phase metrics are excluded instead of being rendered as 0 ms.
/// </summary>
internal sealed class LegacyComparisonBuilder
{
    public CrossEngineComparisonResult Build(IReadOnlyList<RawRunEntry> filteredRuns, string experimentKey)
    {
        var eligibleRuns = filteredRuns
            .Where(item => item.Result.TechnicalSuccess && item.Result.SemanticSuccess != false)
            .Where(item => HasRequiredTimingMetrics(item.Result))
            .ToArray();

        var latestByEngine = eligibleRuns
            .GroupBy(item => item.Result.EngineKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.Result.TimestampUtc).First())
            .ToDictionary(item => item.Result.EngineKey, item => item, StringComparer.OrdinalIgnoreCase);

        if (latestByEngine.Count < 2)
        {
            var keys = string.Join(", ", latestByEngine.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            throw new InvalidOperationException(
                $"Legacy comparison mode requires at least two distinct successful engine targets with required metrics. Found: {latestByEngine.Count} ({keys}). " +
                "Failed/empty/missing-metric runs are intentionally excluded instead of being rendered as 0 ms.");
        }

        var selected = latestByEngine.Values.ToArray();
        var timestampUtc = DateTimeOffset.UtcNow;
        var timestampToken = timestampUtc.ToString("yyyy-MM-ddTHH-mm-ssZ");
        var datasetProfileKey = ComparisonValueHelpers.ResolveSharedOrMixed(selected.Select(item => item.Result.DatasetProfileKey));
        var fairnessProfileKey = ComparisonValueHelpers.ResolveSharedOrMixed(selected.Select(item => item.Result.FairnessProfileKey));
        var environmentClass = ComparisonValueHelpers.ResolveSharedOrMixed(selected.Select(item => item.Result.Environment.EnvironmentClass));

        var entries = selected
            .OrderBy(item => item.Result.EngineKey, StringComparer.OrdinalIgnoreCase)
            .Select(item => BuildEntry(item.Result, item.Path))
            .ToArray();

        return new CrossEngineComparisonResult
        {
            ComparisonId = $"{timestampToken}__{experimentKey}__{datasetProfileKey}__multi-target",
            TimestampUtc = timestampUtc,
            ExperimentKey = experimentKey,
            DatasetProfileKey = datasetProfileKey,
            FairnessProfileKey = fairnessProfileKey,
            EnvironmentClass = environmentClass,
            Engines = entries,
            Notes = new List<string>
            {
                "Legacy fallback: no comparison-set metadata found in matching runs.",
                "Latest successful matching run per target is selected by timestamp.",
                "Failed/empty/missing-metric runs are excluded instead of being rendered as 0 ms.",
                "Use --comparison-set and measured run series for stable stage4 comparison."
            }
        };
    }

    private static bool HasRequiredTimingMetrics(RunResult run)
    {
        return ComparisonMetricReader.ReadMetric(run, "elapsedMsSingleRun", "elapsedMsTotal").HasValue &&
               ComparisonMetricReader.ReadMetric(run, "loadMs").HasValue &&
               ComparisonMetricReader.ReadMetric(run, "buildMs").HasValue &&
               ComparisonMetricReader.ReadMetric(run, "reopenRefreshMs", "reopenMs").HasValue &&
               ComparisonMetricReader.ReadMetric(run, "materializedLookupMs", "randomPointLookupMs", "lookupMs").HasValue;
    }

    private static CrossEngineComparisonEntry BuildEntry(RunResult run, string rawPath)
    {
        var elapsedMs = RequireMetric(run, "elapsedMsSingleRun", "elapsedMsTotal");
        var loadMs = RequireMetric(run, "loadMs");
        var buildMs = RequireMetric(run, "buildMs");
        var reopenMs = RequireMetric(run, "reopenRefreshMs", "reopenMs");
        var lookupMs = RequireMetric(run, "materializedLookupMs", "randomPointLookupMs", "lookupMs");
        var totalArtifactBytes = ComparisonMetricReader.ReadTotalArtifactBytes(run) ?? 0.0;
        var primaryArtifactBytes = ComparisonMetricReader.ReadPrimaryArtifactBytes(run) ?? 0.0;
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

    private static double RequireMetric(RunResult run, params string[] metricKeys)
    {
        return ComparisonMetricReader.ReadMetric(run, metricKeys)
               ?? throw new InvalidOperationException(
                   $"Run '{run.RunId}' is missing required metric(s): {string.Join(", ", metricKeys)}.");
    }
}
