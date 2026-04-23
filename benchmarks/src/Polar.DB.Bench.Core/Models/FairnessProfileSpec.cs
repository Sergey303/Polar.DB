using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

public sealed record FairnessProfileSpec
{
    [JsonPropertyName("type")]
    public required string FairnessProfileKey { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}
