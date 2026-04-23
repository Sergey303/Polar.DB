using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

public sealed record PolicyContract
{
    [JsonPropertyName("policy")]
    public required string PolicyId { get; init; }

    [JsonPropertyName("applies")]
    public PolicyAppliesTo? AppliesTo { get; init; }

    [JsonPropertyName("guards")]
    public required IReadOnlyList<MetricGuardPolicy> Guards { get; init; }
}
