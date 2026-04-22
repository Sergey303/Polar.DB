namespace Polar.DB.Bench.Analysis.Runtime;

/// <summary>
/// Selects raw runs that are valid inputs for cross-engine comparison.
/// It also resolves which comparison set should be used in stage4 mode.
/// </summary>
internal sealed class ComparisonSelectionService
{
    private const string MeasuredRunRole = "measured";

    private readonly string _polarEngineKey;
    private readonly string _sqliteEngineKey;

    public ComparisonSelectionService(string polarEngineKey, string sqliteEngineKey)
    {
        _polarEngineKey = polarEngineKey;
        _sqliteEngineKey = sqliteEngineKey;
    }

    /// <summary>
    /// Filters raw runs by experiment and optional dataset/fairness/environment selectors.
    /// Only Polar.DB and SQLite runs are kept because this comparison artifact is a two-engine report.
    /// </summary>
    public RawRunEntry[] SelectRuns(IReadOnlyList<RawRunEntry> allRuns, AnalysisOptions options)
    {
        return allRuns
            .Where(item => item.Result.ExperimentKey.Equals(options.ComparisonExperimentKey!, StringComparison.OrdinalIgnoreCase))
            .Where(item => MatchesOptional(item.Result.DatasetProfileKey, options.ComparisonDatasetProfileKey))
            .Where(item => MatchesOptional(item.Result.FairnessProfileKey, options.ComparisonFairnessProfileKey))
            .Where(item => MatchesOptional(item.Result.Environment.EnvironmentClass, options.ComparisonEnvironmentClass))
            .Where(item =>
                item.Result.EngineKey.Equals(_polarEngineKey, StringComparison.OrdinalIgnoreCase) ||
                item.Result.EngineKey.Equals(_sqliteEngineKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <summary>
    /// Resolves comparison-set id for stage4 aggregation.
    /// If an explicit set id is provided, it must exist. Otherwise the latest complete set is selected.
    /// </summary>
    public string? ResolveComparisonSetId(IReadOnlyList<RawRunEntry> filteredRuns, string? explicitSetId)
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
            .Where(set => set.Items.Any(item => item.Result.EngineKey.Equals(_polarEngineKey, StringComparison.OrdinalIgnoreCase) && IsMeasuredRun(item)))
            .Where(set => set.Items.Any(item => item.Result.EngineKey.Equals(_sqliteEngineKey, StringComparison.OrdinalIgnoreCase) && IsMeasuredRun(item)))
            .OrderByDescending(set => set.Latest)
            .FirstOrDefault();

        return latestCompleteSet?.SetId;
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
}
