using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

public sealed record DatasetSpec
{
    [JsonPropertyName("profile")]
    public required string ProfileKey { get; init; }

    [JsonPropertyName("count")]
    public long RecordCount { get; init; }

    [JsonPropertyName("seed")]
    public int? Seed { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}
