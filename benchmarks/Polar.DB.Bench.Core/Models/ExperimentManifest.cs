using Polar.DB.Bench.Core.Abstractions;
using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

/// <summary>
/// Canonical experiment manifest.
/// One experiment = one folder with one experiment.json.
/// </summary>
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

/// <summary>
/// Engine runtime specification.
/// Empty object = current source (for polar-db) or latest NuGet (for non-Polar engines).
/// With nuget field = pinned NuGet version.
/// </summary>
public sealed record ExperimentEngineSpec
{
    [JsonPropertyName("nuget")]
    public string? Nuget { get; init; }
}

/// <summary>
/// Simplified compare-config.
/// Both fields are bool-only.
/// history: true = build latest-history.json artifact; false = skip.
/// otherExperiments: true = auto-discover other experiment folders and build cross-experiment context; false = skip.
/// When compare section is absent, defaults are: history=true, otherExperiments=true.
/// </summary>
public sealed record ExperimentCompareSpec
{
    /// <summary>
    /// true = build latest-history.json artifact for this experiment over time.
    /// false = do not build history artifact.
    /// Default: true.
    /// </summary>
    [JsonPropertyName("history")]
    public bool History { get; init; } = true;

    /// <summary>
    /// true = auto-discover other experiment folders under benchmarks/experiments/
    /// and build latest-other-experiments.json as informative cross-experiment context.
    /// false = do not build cross-experiment context.
    /// Default: true.
    /// </summary>
    [JsonPropertyName("otherExperiments")]
    public bool OtherExperiments { get; init; } = true;
}
