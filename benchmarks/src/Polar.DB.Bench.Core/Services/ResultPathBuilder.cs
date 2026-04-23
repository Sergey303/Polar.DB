using System.IO;

namespace Polar.DB.Bench.Core.Services;

/// <summary>
/// Naming rules for benchmark artifacts.
/// Raw names are stable factual run files; analyzed and comparison names are derived layers.
/// </summary>
public static class ResultPathBuilder
{
    public static string BuildRawResultPath(
        string rawResultsDirectory,
        string timestampToken,
        string engineKey,
        string? runRole,
        int? sequenceNumber)
    {
        var fileName = !string.IsNullOrWhiteSpace(runRole) && sequenceNumber is not null
            ? $"{timestampToken}__{engineKey}__{runRole}-{sequenceNumber:D2}.run.json"
            : $"{timestampToken}__{engineKey}.run.json";
        return Path.Combine(rawResultsDirectory, fileName);
    }

    public static string BuildAnalyzedResultPath(
        string analyzedResultsDirectory,
        string timestampToken,
        string experimentKey,
        string datasetProfileKey,
        string engineKey,
        string environmentClass)
    {
        var fileName = $"{timestampToken}.{experimentKey}.{datasetProfileKey}.{engineKey}.{environmentClass}.eval.json";
        return Path.Combine(analyzedResultsDirectory, fileName);
    }

    public static string BuildComparisonResultPath(
        string comparisonResultsDirectory,
        string timestampToken,
        string experimentKey,
        string datasetProfileKey,
        string fairnessProfileKey)
    {
        var fileName = $"{timestampToken}.{experimentKey}.{datasetProfileKey}.{fairnessProfileKey}.comparison.json";
        return Path.Combine(comparisonResultsDirectory, fileName);
    }

    public static string BuildComparisonSeriesResultPath(
        string comparisonResultsDirectory,
        string timestampToken,
        string experimentKey,
        string datasetProfileKey,
        string fairnessProfileKey,
        string comparisonSetId)
    {
        var fileName = $"{timestampToken}.{experimentKey}.{datasetProfileKey}.{fairnessProfileKey}.{comparisonSetId}.comparison-series.json";
        return Path.Combine(comparisonResultsDirectory, fileName);
    }
}
