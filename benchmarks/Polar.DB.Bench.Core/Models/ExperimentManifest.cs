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

    /// <summary>
    /// Runtime targets for this experiment.
    /// A target is a runtime variant identified by a unique key (e.g. "polar-db-current", "polar-db-2.1.1", "sqlite").
    /// Each target specifies an engine family and optionally a pinned NuGet version.
    /// One experiment can contain multiple targets from the same engine family
    /// (e.g. three Polar.DB variants: current source, NuGet 2.1.1, NuGet 2.1.0).
    /// The target key is the runtime variant id; the engine field identifies the engine family.
    /// </summary>
    [JsonPropertyName("targets")]
    public required IReadOnlyDictionary<string, ExperimentTargetSpec> Targets { get; init; }

    [JsonPropertyName("compare")]
    public ExperimentCompareSpec Compare { get; init; } = new();
}

/// <summary>
/// Target runtime specification for one variant inside an experiment.
/// A target represents one specific runtime variant of an engine family.
/// - engine: the engine family identifier (e.g. "polar-db", "sqlite").
/// - nuget (optional): pinned NuGet package version.
///   For Polar.DB: absent means current source from repository; present means pinned NuGet version.
///   For non-Polar engines: absent means latest/default package/runtime already used by the platform.
/// </summary>
public sealed record ExperimentTargetSpec
{
    [JsonPropertyName("engine")]
    public required string Engine { get; init; }

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
