using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

public sealed record WorkloadSpec
{
    [JsonPropertyName("type")]
    public required string WorkloadKey { get; init; }

    [JsonPropertyName("lookup")]
    public int? LookupCount { get; init; }

    [JsonPropertyName("batches")]
    public int? BatchCount { get; init; }

    [JsonPropertyName("batchSize")]
    public int? BatchSize { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonPropertyName("options")]
    public Dictionary<string, string>? Parameters { get; init; }
}
