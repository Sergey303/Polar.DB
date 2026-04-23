using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

public sealed record AnalyzedResult
{
    [JsonPropertyName("run")]
    public required string RunId { get; init; }

    [JsonPropertyName("raw")]
    public required string RawResultPath { get; init; }

    [JsonPropertyName("analyzedAt")]
    public required DateTimeOffset AnalysisTimestampUtc { get; init; }

    [JsonPropertyName("policy")]
    public string? PolicyId { get; init; }

    [JsonPropertyName("baseline")]
    public string? BaselineId { get; init; }

    [JsonPropertyName("status")]
    public required string OverallStatus { get; init; }

    [JsonPropertyName("checks")]
    public required IReadOnlyList<MetricCheckResult> Checks { get; init; }

    [JsonPropertyName("derived")]
    public Dictionary<string, double>? DerivedMetrics { get; init; }

    [JsonPropertyName("notes")]
    public List<string>? Notes { get; init; }
}
