using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

/// <summary>
/// Local analyzed artifact for one raw run.
/// This model stores interpretation of a single run (policy/baseline checks) and is kept inside experiment <c>analyzed/</c>.
/// </summary>
/// <remarks>
/// Cross-engine or cross-experiment comparison artifacts are not represented by this model and must be stored in <c>comparisons/</c>.
/// </remarks>
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
