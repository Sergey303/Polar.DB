using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

public sealed record RunMetric
{
    [JsonPropertyName("metric")]
    public required string MetricKey { get; init; }

    [JsonPropertyName("value")]
    public double Value { get; init; }
}
