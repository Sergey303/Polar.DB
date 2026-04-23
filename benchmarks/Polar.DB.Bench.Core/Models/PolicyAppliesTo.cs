using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

public sealed record PolicyAppliesTo
{
    [JsonPropertyName("experiment")]
    public string? ExperimentKey { get; init; }

    [JsonPropertyName("dataset")]
    public string? DatasetProfileKey { get; init; }

    [JsonPropertyName("engine")]
    public string? EngineKey { get; init; }

    [JsonPropertyName("fairness")]
    public string? FairnessProfileKey { get; init; }

    [JsonPropertyName("env")]
    public string? EnvironmentClass { get; init; }
}
