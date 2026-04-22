namespace Polar.DB.Bench.Core.Models;

/// <summary>
/// Legacy cross-engine comparison artifact (stage3 style).
/// It compares one selected run per engine and exists as a fallback for old data without comparison sets.
/// </summary>
/// <remarks>
/// This record is a derived artifact produced by analysis.
/// It does not modify raw run files.
/// </remarks>
public sealed record CrossEngineComparisonResult
{
    /// <summary>
    /// Stable artifact id composed from timestamp and comparison dimensions.
    /// </summary>
    public required string ComparisonId { get; init; }

    /// <summary>
    /// UTC time when this derived comparison artifact was generated.
    /// </summary>
    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>
    /// Experiment key shared by compared runs.
    /// </summary>
    public required string ExperimentKey { get; init; }

    /// <summary>
    /// Shared dataset profile or <c>mixed</c> when selected runs are not uniform.
    /// </summary>
    public string? DatasetProfileKey { get; init; }

    /// <summary>
    /// Shared fairness profile or <c>mixed</c>.
    /// </summary>
    public string? FairnessProfileKey { get; init; }

    /// <summary>
    /// Shared environment class or <c>mixed</c>.
    /// </summary>
    public string? EnvironmentClass { get; init; }

    /// <summary>
    /// Per-engine entries (normally Polar.DB and SQLite).
    /// </summary>
    public required IReadOnlyList<CrossEngineComparisonEntry> Engines { get; init; }

    /// <summary>
    /// Optional explanatory notes produced by analysis.
    /// </summary>
    public List<string>? Notes { get; init; }
}

/// <summary>
/// One engine row inside a legacy comparison artifact.
/// Values are copied or derived from one selected raw run.
/// </summary>
public sealed record CrossEngineComparisonEntry
{
    /// <summary>
    /// Engine identifier.
    /// </summary>
    public required string EngineKey { get; init; }

    /// <summary>
    /// Raw run id used for this comparison row.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// Path to the raw run file used to build this row.
    /// </summary>
    public required string RawResultPath { get; init; }

    /// <summary>
    /// Timestamp of the raw run.
    /// </summary>
    public required DateTimeOffset RunTimestampUtc { get; init; }

    /// <summary>
    /// Technical execution success from the raw run.
    /// </summary>
    public required bool TechnicalSuccess { get; init; }

    /// <summary>
    /// Optional semantic success from the raw run.
    /// </summary>
    public bool? SemanticSuccess { get; init; }

    /// <summary>
    /// Elapsed milliseconds for this single run.
    /// </summary>
    public double ElapsedMsSingleRun { get; init; }

    /// <summary>
    /// Load phase duration in milliseconds.
    /// </summary>
    public double LoadMs { get; init; }

    /// <summary>
    /// Build phase duration in milliseconds.
    /// </summary>
    public double BuildMs { get; init; }

    /// <summary>
    /// Reopen/refresh duration in milliseconds.
    /// </summary>
    public double ReopenMs { get; init; }

    /// <summary>
    /// Lookup phase duration in milliseconds.
    /// </summary>
    public double LookupMs { get; init; }

    /// <summary>
    /// Total bytes of all recorded artifacts.
    /// </summary>
    public double TotalArtifactBytes { get; init; }

    /// <summary>
    /// Bytes of primary data file(s).
    /// </summary>
    public double PrimaryArtifactBytes { get; init; }

    /// <summary>
    /// Bytes of side artifacts (for example WAL/state/index files).
    /// </summary>
    public double SideArtifactBytes { get; init; }
}

/// <summary>
/// Stage4 cross-engine comparison artifact built from one comparison set.
/// This is the preferred format for stable comparisons across measured run series.
/// </summary>
/// <remarks>
/// Input: raw runs that share one comparison set id.
/// Output: aggregated per-engine statistics over measured runs only.
/// Warmup runs remain part of raw facts but are excluded from final stats.
/// </remarks>
public sealed record CrossEngineComparisonSeriesResult
{
    /// <summary>
    /// Stable artifact id composed from timestamp and comparison dimensions.
    /// </summary>
    public required string ComparisonId { get; init; }

    /// <summary>
    /// UTC time when the derived series artifact was generated.
    /// </summary>
    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>
    /// Experiment key shared by all runs in the set.
    /// </summary>
    public required string ExperimentKey { get; init; }

    /// <summary>
    /// Comparison set id that links related runs across engines.
    /// </summary>
    public required string ComparisonSetId { get; init; }

    /// <summary>
    /// Shared dataset profile or <c>mixed</c>.
    /// </summary>
    public string? DatasetProfileKey { get; init; }

    /// <summary>
    /// Shared fairness profile or <c>mixed</c>.
    /// </summary>
    public string? FairnessProfileKey { get; init; }

    /// <summary>
    /// Shared environment class or <c>mixed</c>.
    /// </summary>
    public string? EnvironmentClass { get; init; }

    /// <summary>
    /// Engine list included in the comparison.
    /// </summary>
    public required IReadOnlyList<string> Engines { get; init; }

    /// <summary>
    /// Aggregated statistics per engine.
    /// </summary>
    public required IReadOnlyList<CrossEngineSeriesEngineEntry> EngineSeries { get; init; }

    /// <summary>
    /// Optional explanatory notes about aggregation behavior.
    /// </summary>
    public List<string>? Notes { get; init; }
}

/// <summary>
/// Aggregated metrics for one engine inside a comparison series.
/// Counts and stats are derived from measured runs in the set.
/// </summary>
public sealed record CrossEngineSeriesEngineEntry
{
    /// <summary>
    /// Engine identifier.
    /// </summary>
    public required string EngineKey { get; init; }

    /// <summary>
    /// Number of measured runs included in aggregation.
    /// </summary>
    public required int MeasuredRunCount { get; init; }

    /// <summary>
    /// Number of warmup runs found for this engine in the set.
    /// Warmup runs are stored for context but not aggregated into metric stats.
    /// </summary>
    public required int WarmupRunCount { get; init; }

    /// <summary>
    /// Number of measured runs with technical success.
    /// </summary>
    public required int TechnicalSuccessCount { get; init; }

    /// <summary>
    /// Number of measured runs with semantic success.
    /// </summary>
    public required int SemanticSuccessCount { get; init; }

    /// <summary>
    /// Number of measured runs where semantic outcome was evaluated.
    /// </summary>
    public required int SemanticEvaluatedCount { get; init; }

    /// <summary>
    /// Paths of measured raw run files used in this aggregation.
    /// </summary>
    public required IReadOnlyList<string> RawResultPaths { get; init; }

    /// <summary>
    /// Aggregate stats for elapsed milliseconds.
    /// </summary>
    public required MetricSeriesStats ElapsedMs { get; init; }

    /// <summary>
    /// Aggregate stats for load phase milliseconds.
    /// </summary>
    public required MetricSeriesStats LoadMs { get; init; }

    /// <summary>
    /// Aggregate stats for build phase milliseconds.
    /// </summary>
    public required MetricSeriesStats BuildMs { get; init; }

    /// <summary>
    /// Aggregate stats for reopen/refresh milliseconds.
    /// </summary>
    public required MetricSeriesStats ReopenMs { get; init; }

    /// <summary>
    /// Aggregate stats for lookup milliseconds.
    /// </summary>
    public required MetricSeriesStats LookupMs { get; init; }

    /// <summary>
    /// Aggregate stats for total artifact bytes.
    /// </summary>
    public required MetricSeriesStats TotalArtifactBytes { get; init; }

    /// <summary>
    /// Aggregate stats for primary data bytes.
    /// </summary>
    public required MetricSeriesStats PrimaryArtifactBytes { get; init; }

    /// <summary>
    /// Aggregate stats for side artifact bytes.
    /// </summary>
    public required MetricSeriesStats SideArtifactBytes { get; init; }
}

/// <summary>
/// Statistical summary for one metric across a run series.
/// </summary>
/// <remarks>
/// <see cref="Count"/> is the number of expected samples.
/// <see cref="MissingCount"/> tracks how many runs did not provide this metric.
/// Min/Max/Average/Median are null when there are no actual values.
/// </remarks>
public sealed record MetricSeriesStats
{
    /// <summary>
    /// Number of runs considered for this metric slot.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Number of runs where this metric is missing.
    /// </summary>
    public required int MissingCount { get; init; }

    /// <summary>
    /// Minimum value across available samples.
    /// </summary>
    public double? Min { get; init; }

    /// <summary>
    /// Maximum value across available samples.
    /// </summary>
    public double? Max { get; init; }

    /// <summary>
    /// Arithmetic average across available samples.
    /// </summary>
    public double? Average { get; init; }

    /// <summary>
    /// Median across available samples.
    /// </summary>
    public double? Median { get; init; }
}
