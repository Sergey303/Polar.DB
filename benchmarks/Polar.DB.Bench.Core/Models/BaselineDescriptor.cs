using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

public sealed record BaselineDescriptor
{
    [JsonPropertyName("baseline")]
    public required string BaselineId { get; init; }

    [JsonPropertyName("experiment")]
    public required string ExperimentKey { get; init; }

    [JsonPropertyName("dataset")]
    public required string DatasetProfileKey { get; init; }

    [JsonPropertyName("engine")]
    public required string EngineKey { get; init; }

    [JsonPropertyName("fairness")]
    public required string FairnessProfileKey { get; init; }

    [JsonPropertyName("env")]
    public required string EnvironmentClass { get; init; }

    [JsonPropertyName("metrics")]
    public required Dictionary<string, double> Metrics { get; init; }
}
