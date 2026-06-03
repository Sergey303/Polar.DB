namespace Polar.DB.Bench.Core.Models;

/// <summary>
/// Simple taxonomy of benchmark run outcomes.
/// This enum is a lightweight classification for documentation and future analysis.
/// It is NOT deeply integrated into RunResult yet — RunResult uses
/// TechnicalSuccess (bool) and SemanticSuccess (bool?) for backward compatibility.
///
/// TODO: Consider migrating RunResult to use this enum directly
/// when the raw result schema version is bumped.
/// </summary>
public enum BenchmarkRunStatus
{
    /// <summary>
    /// The run completed successfully: technical execution succeeded
    /// and all semantic checks passed.
    /// </summary>
    Success,

    /// <summary>
    /// The target engine or runtime does not support this experiment's workload.
    /// This is not a failure — the engine simply cannot execute the requested scenario.
    /// </summary>
    NotSupported,

    /// <summary>
    /// The run failed due to a technical issue: process crash, timeout,
    /// missing dependency, or any infrastructure-level problem.
    /// The raw result may still contain partial data.
    /// </summary>
    TechnicalFailed,

    /// <summary>
    /// The run completed technically (process finished, no crash),
    /// but the semantic checks failed — e.g. wrong lookup results,
    /// data corruption, or unexpected behavior.
    /// This indicates a correctness or regression issue in the engine.
    /// </summary>
    SemanticFailed
}
