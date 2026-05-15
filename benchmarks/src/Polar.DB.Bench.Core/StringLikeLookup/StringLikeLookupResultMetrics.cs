using System.Collections.Generic;
using System.Linq;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Core.StringLikeLookup;

public sealed record StringLikeCaseMeasurement(
    string QueryKey,
    StringLikeQueryKind Kind,
    long MatchedCount,
    long ExpectedCount,
    long RowsVisited,
    double TrimmedMeanMs,
    double P95Ms,
    double MinMs,
    double MaxMs)
{
    public bool Success => MatchedCount == ExpectedCount;

    public static StringLikeCaseMeasurement From(
        StringLikeQueryCase query,
        long matched,
        long rowsVisited,
        IReadOnlyList<double> samples) =>
        new (
            query.Key,
            query.Kind,
            matched,
            query.ExpectedCount,
            rowsVisited,
            StringLikeLookupStats.TrimmedMean(samples),
            StringLikeLookupStats.Percentile(samples, 95),
            samples.Count == 0 ? 0 : samples.Min(),
            samples.Count == 0 ? 0 : samples.Max());
}

public static class StringLikeLookupResultMetrics
{
    public static void AddCase(
        List<RunMetric> metrics,
        StringLikeQueryCase query,
        StringLikeCaseMeasurement result,
        string prefix = "stringLike")
    {
        Add(metrics, $"{prefix}.{query.Key}.matchedCount", result.MatchedCount);
        Add(metrics, $"{prefix}.{query.Key}.expectedCount", result.ExpectedCount);
        Add(metrics, $"{prefix}.{query.Key}.rowsVisited", result.RowsVisited);
        Add(metrics, $"{prefix}.{query.Key}.trimmedMeanMs", result.TrimmedMeanMs);
        Add(metrics, $"{prefix}.{query.Key}.p95Ms", result.P95Ms);
        Add(metrics, $"{prefix}.{query.Key}.minMs", result.MinMs);
        Add(metrics, $"{prefix}.{query.Key}.maxMs", result.MaxMs);
        Add(metrics, $"{prefix}.{query.Key}.success", result.Success ? 1 : 0);
    }

    public static void AddCommon(
        List<RunMetric> metrics,
        double elapsedMs,
        double loadMs,
        double buildMs,
        double reopenMs,
        long rowCountAfterReopen,
        IReadOnlyList<ArtifactDescriptor> artifacts)
    {
        Add(metrics, "elapsedMsTotal", elapsedMs);
        Add(metrics, "elapsedMsSingleRun", elapsedMs);
        Add(metrics, "loadMs", loadMs);
        Add(metrics, "buildMs", buildMs);
        Add(metrics, "reopenRefreshMs", reopenMs);
        Add(metrics, "rowCountAfterReopen", rowCountAfterReopen);
        Add(metrics, "totalArtifactBytes", artifacts.Sum(static x => x.Bytes));
    }

    private static void Add(List<RunMetric> metrics, string key, double value) =>
        metrics.Add(new RunMetric { MetricKey = key, Value = value });
}
