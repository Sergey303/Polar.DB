namespace Polar.DB.Bench.Core.Models;

public sealed record AnalyzedResult
{
    public required string RunId { get; init; }
    public required string RawResultPath { get; init; }
    public required DateTimeOffset AnalysisTimestampUtc { get; init; }
    public string? PolicyId { get; init; }
    public string? BaselineId { get; init; }
    public required string OverallStatus { get; init; }
    public required IReadOnlyList<MetricCheckResult> Checks { get; init; }
    public Dictionary<string, double>? DerivedMetrics { get; init; }
    public List<string>? Notes { get; init; }
}
