using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

public sealed record MetricCheckResult
{
    [JsonPropertyName("metric")]
    public required string MetricKey { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("actual")]
    public double? Actual { get; init; }

    [JsonPropertyName("max")]
    public double? ExpectedMax { get; init; }

    [JsonPropertyName("baseline")]
    public double? BaselineValue { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
