using Polar.DB.Bench.Core.Abstractions;
using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

public sealed record ExperimentManifest
{
    [JsonPropertyName("experiment")]
    public required string ExperimentKey { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("research")]
    public string? ResearchQuestionId { get; init; }

    [JsonPropertyName("hypothesis")]
    public string? HypothesisId { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("dataset")]
    public required DatasetSpec Dataset { get; init; }

    [JsonPropertyName("workload")]
    public required WorkloadSpec Workload { get; init; }

    [JsonPropertyName("fault")]
    public FaultProfileSpec? FaultProfile { get; init; }

    [JsonPropertyName("fairness")]
    public required FairnessProfileSpec FairnessProfile { get; init; }

    [JsonPropertyName("requires")]
    public IReadOnlyList<EngineCapability>? RequiredCapabilities { get; init; }

    [JsonPropertyName("engines")]
    public required IReadOnlyDictionary<string, ExperimentEngineSpec> Engines { get; init; }

    [JsonPropertyName("compare")]
    public ExperimentCompareSpec Compare { get; init; } = new();
}

public sealed record ExperimentEngineSpec
{
    [JsonPropertyName("nuget")]
    public string? Nuget { get; init; }
}

public sealed record ExperimentCompareSpec
{
    [JsonPropertyName("history")]
    public IReadOnlyList<string> History { get; init; } = [];

    [JsonPropertyName("otherExperiments")]
    public IReadOnlyList<string> OtherExperiments { get; init; } = [];
}
