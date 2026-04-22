namespace Polar.DB.Bench.Core.Services;

public static class ResultPathBuilder
{
    public static string BuildRawResultPath(
        string rawResultsDirectory,
        string timestampToken,
        string experimentKey,
        string datasetProfileKey,
        string engineKey,
        string environmentClass)
    {
        var fileName = $"{timestampToken}.{experimentKey}.{datasetProfileKey}.{engineKey}.{environmentClass}.run.json";
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
}
