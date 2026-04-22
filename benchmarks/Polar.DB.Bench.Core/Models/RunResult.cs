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
    public required string RunId { get; init; }

    /// <summary>
    /// UTC time when this raw run was produced.
    /// </summary>
    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>
    /// Engine identifier, for example <c>polar-db</c> or <c>sqlite</c>.
    /// </summary>
    public required string EngineKey { get; init; }

    /// <summary>
    /// Workload/experiment identifier.
    /// Runs are only directly comparable when they share the same experiment key.
    /// </summary>
    public required string ExperimentKey { get; init; }

    /// <summary>
    /// Dataset profile used by this run.
    /// </summary>
    public required string DatasetProfileKey { get; init; }

    /// <summary>
    /// Fairness profile used by this run.
    /// This is the shared intent that each engine maps to engine-specific settings.
    /// </summary>
    public required string FairnessProfileKey { get; init; }

    /// <summary>
    /// Optional stage4 set id that groups related warmup/measured runs across engines.
    /// Comparison set exists to prevent accidental "latest run vs latest run" comparisons of unrelated executions.
    /// </summary>
    public string? ComparisonSetId { get; init; }

    /// <summary>
    /// Optional sequence number inside one comparison set.
    /// </summary>
    public int? RunSeriesSequenceNumber { get; init; }

    /// <summary>
    /// Optional role of this run inside the set: usually <c>warmup</c> or <c>measured</c>.
    /// Warmup stabilizes runtime state and is stored as fact, but excluded from final aggregate stats.
    /// </summary>
    public string? RunRole { get; init; }

    /// <summary>
    /// Environment manifest captured during execution.
    /// </summary>
    public required EnvironmentManifest Environment { get; init; }

    /// <summary>
    /// True when technical execution completed (process/workflow level).
    /// </summary>
    public required bool TechnicalSuccess { get; init; }

    /// <summary>
    /// Optional technical failure description.
    /// </summary>
    public string? TechnicalFailureReason { get; init; }

    /// <summary>
    /// Optional semantic success flag.
    /// Semantic checks validate workload-level meaning, not only process completion.
    /// </summary>
    public bool? SemanticSuccess { get; init; }

    /// <summary>
    /// Optional semantic failure description.
    /// </summary>
    public string? SemanticFailureReason { get; init; }

    /// <summary>
    /// Measured metrics collected by the executor.
    /// These values are raw inputs for later comparison artifacts.
    /// </summary>
    public required IReadOnlyList<RunMetric> Metrics { get; init; }

    /// <summary>
    /// Artifact inventory with byte sizes and roles.
    /// Primary bytes describe main data files; side bytes usually cover WAL/state/index and similar side artifacts.
    /// </summary>
    public required IReadOnlyList<ArtifactDescriptor> Artifacts { get; init; }

    /// <summary>
    /// Optional engine-specific diagnostics.
    /// Useful for interpretation, but not required for common cross-engine metrics.
    /// </summary>
    public Dictionary<string, string>? EngineDiagnostics { get; init; }

    /// <summary>
    /// Optional extra tags written by the executor.
    /// </summary>
    public Dictionary<string, string>? Tags { get; init; }

    /// <summary>
    /// Optional free-form run notes.
    /// </summary>
    public List<string>? Notes { get; init; }
}
