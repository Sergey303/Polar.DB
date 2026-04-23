using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

public sealed record FaultProfileSpec
{
    [JsonPropertyName("type")]
    public required string FaultProfileKey { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonPropertyName("options")]
    public Dictionary<string, string>? Parameters { get; init; }
}
