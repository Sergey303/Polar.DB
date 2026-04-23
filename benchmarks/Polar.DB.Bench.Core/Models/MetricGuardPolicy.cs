using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

public sealed record MetricGuardPolicy
{
    [JsonPropertyName("metric")]
    public required string MetricKey { get; init; }

    [JsonPropertyName("mode")]
    public required string Mode { get; init; }

    [JsonPropertyName("max")]
    public double? AbsoluteMax { get; init; }

    [JsonPropertyName("maxRegression")]
    public double? MaxRegressionPercent { get; init; }

    [JsonPropertyName("slack")]
    public double? FixedSlack { get; init; }

    [JsonPropertyName("severity")]
    public required string Severity { get; init; }
}
