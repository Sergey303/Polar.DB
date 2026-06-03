using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

public sealed record EngineRuntimeDescriptor
{
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    [JsonPropertyName("nuget")]
    public string? Nuget { get; init; }
}
