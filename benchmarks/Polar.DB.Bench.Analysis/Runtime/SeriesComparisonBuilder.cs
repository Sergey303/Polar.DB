using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Analysis.Runtime;

/// <summary>
/// Builds stage4 comparison-series artifacts.
/// Input is a comparison set: related warmup/measured runs from both engines.
/// Output is one derived artifact with aggregated measured statistics per engine.
/// </summary>
internal sealed class SeriesComparisonBuilder
{
    private const string WarmupRunRole = "warmup";
    private const string MeasuredRunRole = "measured";

    private readonly string _polarEngineKey;
    private readonly string _sqliteEngineKey;

    public SeriesComparisonBuilder(string polarEngineKey, string sqliteEngineKey)
    {
        _polarEngineKey = polarEngineKey;
        _sqliteEngineKey = sqliteEngineKey;
    }

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

        var polarRuns = setRuns.Where(x => x.Result.EngineKey.Equals(_polarEngineKey, StringComparison.OrdinalIgnoreCase)).ToArray();
        var sqliteRuns = setRuns.Where(x => x.Result.EngineKey.Equals(_sqliteEngineKey, StringComparison.OrdinalIgnoreCase)).ToArray();

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
        var datasetProfileKey = ComparisonValueHelpers.ResolveSharedOrMixed(setRuns.Select(item => item.Result.DatasetProfileKey));
        var fairnessProfileKey = ComparisonValueHelpers.ResolveSharedOrMixed(setRuns.Select(item => item.Result.FairnessProfileKey));
        var environmentClass = ComparisonValueHelpers.ResolveSharedOrMixed(setRuns.Select(item => item.Result.Environment.EnvironmentClass));

        return new CrossEngineComparisonSeriesResult
        {
            ComparisonId = $"{timestampToken}__{experimentKey}__{datasetProfileKey}__{comparisonSetId}__polar-db-vs-sqlite",
            TimestampUtc = timestampUtc,
            ExperimentKey = experimentKey,
            ComparisonSetId = comparisonSetId,
            DatasetProfileKey = datasetProfileKey,
            FairnessProfileKey = fairnessProfileKey,
            EnvironmentClass = environmentClass,
            Engines = new[] { _polarEngineKey, _sqliteEngineKey },
            EngineSeries = new[]
            {
                BuildEngineSeriesEntry(_polarEngineKey, polarRuns),
                BuildEngineSeriesEntry(_sqliteEngineKey, sqliteRuns)
            },
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

        var elapsed = measuredRuns.Select(x => ComparisonMetricReader.ReadMetric(x.Result, "elapsedMsSingleRun", "elapsedMsTotal")).ToArray();
        var load = measuredRuns.Select(x => ComparisonMetricReader.ReadMetric(x.Result, "loadMs")).ToArray();
        var build = measuredRuns.Select(x => ComparisonMetricReader.ReadMetric(x.Result, "buildMs")).ToArray();
        var reopen = measuredRuns.Select(x => ComparisonMetricReader.ReadMetric(x.Result, "reopenRefreshMs", "reopenMs")).ToArray();
        var lookup = measuredRuns.Select(x => ComparisonMetricReader.ReadMetric(x.Result, "randomPointLookupMs")).ToArray();
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
