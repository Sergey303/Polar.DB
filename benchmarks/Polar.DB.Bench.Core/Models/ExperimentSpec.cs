using Polar.DB.Bench.Core.Abstractions;
using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

/// <summary>
/// Resolved experiment spec for one specific target.
/// This is the runtime spec used by the executor after target selection from the manifest.
/// </summary>
public sealed record ExperimentSpec
{
    [JsonPropertyName("experiment")]
    public required string ExperimentKey { get; init; }

    [JsonPropertyName("research")]
    public string? ResearchQuestionId { get; init; }

    [JsonPropertyName("hypothesis")]
    public string? HypothesisId { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// The resolved target key (runtime variant id), e.g. "polar-db-current", "polar-db-2.1.1", "sqlite".
    /// </summary>
    [JsonPropertyName("target")]
    public required string TargetKey { get; init; }

    /// <summary>
    /// The engine family identifier, e.g. "polar-db", "sqlite".
    /// </summary>
    [JsonPropertyName("engine")]
    public required string Engine { get; init; }

    /// <summary>
    /// Optional pinned NuGet version.
    /// </summary>
    [JsonPropertyName("nuget")]
    public string? Nuget { get; init; }

    [JsonPropertyName("dataset")]
    public required DatasetSpec Dataset { get; init; }

    [JsonPropertyName("workload")]
    public required WorkloadSpec Workload { get; init; }

    [JsonPropertyName("fault")]
    public FaultProfileSpec? FaultProfile { get; init; }

    [JsonPropertyName("fairness")]
    public FairnessProfileSpec? FairnessProfile { get; init; }

    [JsonPropertyName("requires")]
    public IReadOnlyList<EngineCapability>? RequiredCapabilities { get; init; }
}
