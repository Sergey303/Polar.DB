using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Analysis.Runtime;

/// <summary>
/// Builds the legacy single-run comparison artifact.
/// This path exists for old raw data that does not have comparison-set metadata.
/// This builder is engine-family agnostic: it does not hardcode Polar.DB or SQLite keys.
/// </summary>
internal sealed class LegacyComparisonBuilder
{
    /// <summary>
    /// Selects the latest matching run for each distinct engine key and creates one legacy comparison artifact.
    /// </summary>
    public CrossEngineComparisonResult Build(IReadOnlyList<RawRunEntry> filteredRuns, string experimentKey)
    {
        var latestByEngine = filteredRuns
            .GroupBy(item => item.Result.EngineKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.Result.TimestampUtc).First())
            .ToDictionary(item => item.Result.EngineKey, item => item, StringComparer.OrdinalIgnoreCase);

        if (latestByEngine.Count < 2)
        {
            var keys = string.Join(", ", latestByEngine.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            throw new InvalidOperationException(
                $"Legacy comparison mode requires at least two distinct engine targets. Found: {latestByEngine.Count} ({keys}).");
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
                "Latest matching run per target is selected by timestamp.",
                "Use --comparison-set and measured run series for stable stage4 comparison."
            }
        };
    }

    private static CrossEngineComparisonEntry BuildEntry(RunResult run, string rawPath)
    {
        var elapsedMs = ComparisonMetricReader.ReadMetric(run, "elapsedMsSingleRun", "elapsedMsTotal") ?? 0.0;
        var loadMs = ComparisonMetricReader.ReadMetric(run, "loadMs") ?? 0.0;
        var buildMs = ComparisonMetricReader.ReadMetric(run, "buildMs") ?? 0.0;
        var reopenMs = ComparisonMetricReader.ReadMetric(run, "reopenRefreshMs", "reopenMs") ?? 0.0;
        var lookupMs = ComparisonMetricReader.ReadMetric(run, "randomPointLookupMs") ?? 0.0;
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
}
