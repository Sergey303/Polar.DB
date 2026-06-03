using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

/// <summary>
/// Immutable raw facts for one executor launch.
/// This is the base artifact of the benchmark platform: analysis and reports are derived from this data.
/// </summary>
/// <remarks>
/// Input boundary: executor measurements, artifact inventory, and engine diagnostics collected during one run.
/// Output boundary: this file itself is not rewritten by analysis; later stages create separate derived artifacts.
/// </remarks>
public sealed record RunResult
{
    /// <summary>
    /// Unique id of this raw run artifact.
    /// </summary>
    [JsonPropertyName("run")]
    public required string RunId { get; init; }

    /// <summary>
    /// UTC time when this raw run was produced.
    /// </summary>
    [JsonPropertyName("at")]
    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>
    /// Target key (runtime variant identifier), for example <c>polar-db-current</c>, <c>polar-db-2.1.1</c>, or <c>sqlite</c>.
    /// This is the resolved target key from the experiment manifest, not the engine family name.
    /// The JSON property name <c>engine</c> is preserved for backward compatibility with existing raw run files.
    /// </summary>
    [JsonPropertyName("engine")]
    public required string EngineKey { get; init; }

    /// <summary>
    /// Workload/experiment identifier.
    /// Runs are only directly comparable when they share the same experiment key.
    /// </summary>
    [JsonPropertyName("experiment")]
    public required string ExperimentKey { get; init; }

    /// <summary>
    /// Dataset profile used by this run.
    /// </summary>
    [JsonPropertyName("dataset")]
    public required string DatasetProfileKey { get; init; }

    /// <summary>
    /// Fairness profile used by this run.
    /// This is the shared intent that each engine maps to engine-specific settings.
    /// </summary>
    [JsonPropertyName("fairness")]
    public required string FairnessProfileKey { get; init; }

    /// <summary>
    /// Optional stage4 set id that groups related warmup/measured runs across engines.
    /// Comparison set exists to prevent accidental "latest run vs latest run" comparisons of unrelated executions.
    /// </summary>
    [JsonPropertyName("set")]
    public string? ComparisonSetId { get; init; }

    /// <summary>
    /// Optional sequence number inside one comparison set.
    /// </summary>
    [JsonPropertyName("seq")]
    public int? RunSeriesSequenceNumber { get; init; }

    /// <summary>
    /// Optional role of this run inside the set: usually <c>warmup</c> or <c>measured</c>.
    /// Warmup stabilizes runtime state and is stored as fact, but excluded from final aggregate stats.
    /// </summary>
    [JsonPropertyName("role")]
    public string? RunRole { get; init; }

    /// <summary>
    /// Environment manifest captured during execution.
    /// </summary>
    [JsonPropertyName("env")]
    public required EnvironmentManifest Environment { get; init; }

    /// <summary>
    /// Engine runtime source identity for this run.
    /// </summary>
    [JsonPropertyName("runtime")]
    public EngineRuntimeDescriptor? Runtime { get; init; }

    /// <summary>
    /// True when technical execution completed (process/workflow level).
    /// </summary>
    [JsonPropertyName("technical")]
    public required bool TechnicalSuccess { get; init; }

    /// <summary>
    /// Optional technical failure description.
    /// </summary>
    [JsonPropertyName("technicalError")]
    public string? TechnicalFailureReason { get; init; }

    /// <summary>
    /// Optional semantic success flag.
    /// Semantic checks validate workload-level meaning, not only process completion.
    /// </summary>
    [JsonPropertyName("semantic")]
    public bool? SemanticSuccess { get; init; }

    /// <summary>
    /// Optional semantic failure description.
    /// </summary>
    [JsonPropertyName("semanticError")]
    public string? SemanticFailureReason { get; init; }

    /// <summary>
    /// Measured metrics collected by the executor.
    /// These values are raw inputs for later comparison artifacts.
    /// </summary>
    [JsonPropertyName("metrics")]
    public required IReadOnlyList<RunMetric> Metrics { get; init; }

    /// <summary>
    /// Artifact inventory with byte sizes and roles.
    /// Primary bytes describe main data files; side bytes usually cover WAL/state/index and similar side artifacts.
    /// </summary>
    [JsonPropertyName("artifacts")]
    public required IReadOnlyList<ArtifactDescriptor> Artifacts { get; init; }

    /// <summary>
    /// Optional engine-specific diagnostics.
    /// Useful for interpretation, but not required for common cross-engine metrics.
    /// </summary>
    [JsonPropertyName("diagnostics")]
    public Dictionary<string, string>? EngineDiagnostics { get; init; }

    /// <summary>
    /// Optional extra tags written by the executor.
    /// </summary>
    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; init; }

    /// <summary>
    /// Optional free-form run notes.
    /// </summary>
    [JsonPropertyName("notes")]
    public List<string>? Notes { get; init; }
}
