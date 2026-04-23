namespace Polar.DB.Bench.Analysis.Runtime;

/// <summary>
/// Selects raw runs that are valid inputs for cross-engine comparison.
/// It also resolves which comparison set should be used in stage4 mode.
/// This service is engine-family agnostic: it does not hardcode Polar.DB or SQLite keys.
/// </summary>
internal sealed class ComparisonSelectionService
{
    private const string MeasuredRunRole = "measured";

    /// <summary>
    /// Filters raw runs by experiment and optional dataset/fairness/environment selectors.
    /// No engine-family filtering is applied; all targets are included.
    /// </summary>
    public RawRunEntry[] SelectRuns(IReadOnlyList<RawRunEntry> allRuns, AnalysisOptions options)
    {
        return allRuns
            .Where(item => item.Result.ExperimentKey.Equals(options.ComparisonExperimentKey!, StringComparison.OrdinalIgnoreCase))
            .Where(item => MatchesOptional(item.Result.DatasetProfileKey, options.ComparisonDatasetProfileKey))
            .Where(item => MatchesOptional(item.Result.FairnessProfileKey, options.ComparisonFairnessProfileKey))
            .Where(item => MatchesOptional(item.Result.Environment.EnvironmentClass, options.ComparisonEnvironmentClass))
            .ToArray();
    }

    /// <summary>
    /// Resolves comparison-set id for stage4 aggregation.
    /// If an explicit set id is provided, it must exist. Otherwise the latest complete set is selected.
    /// A complete set is one that has at least one measured run per distinct engine family present in the set.
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

        // Select the latest set that has at least one measured run per distinct engine family.
        var latestCompleteSet = availableSets
            .OrderByDescending(set => set.Latest)
            .FirstOrDefault(set =>
            {
                var engineFamilies = set.Items
                    .Where(IsMeasuredRun)
                    .Select(item => item.Result.EngineKey)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return engineFamilies.Length >= 2;
            });

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
