using System.Text.Json.Serialization;

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
    [JsonPropertyName("comparison")]
    public required string ComparisonId { get; init; }

    /// <summary>
    /// UTC time when this derived comparison artifact was generated.
    /// </summary>
    [JsonPropertyName("at")]
    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>
    /// Experiment key shared by compared runs.
    /// </summary>
    [JsonPropertyName("experiment")]
    public required string ExperimentKey { get; init; }

    /// <summary>
    /// Shared dataset profile or <c>mixed</c> when selected runs are not uniform.
    /// </summary>
    [JsonPropertyName("dataset")]
    public string? DatasetProfileKey { get; init; }

    /// <summary>
    /// Shared fairness profile or <c>mixed</c>.
    /// </summary>
    [JsonPropertyName("fairness")]
    public string? FairnessProfileKey { get; init; }

    /// <summary>
    /// Shared environment class or <c>mixed</c>.
    /// </summary>
    [JsonPropertyName("env")]
    public string? EnvironmentClass { get; init; }

    /// <summary>
    /// Per-engine entries (normally Polar.DB and SQLite).
    /// </summary>
    [JsonPropertyName("engines")]
    public required IReadOnlyList<CrossEngineComparisonEntry> Engines { get; init; }

    /// <summary>
    /// Optional explanatory notes produced by analysis.
    /// </summary>
    [JsonPropertyName("notes")]
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
    [JsonPropertyName("engine")]
    public required string EngineKey { get; init; }

    /// <summary>
    /// Raw run id used for this comparison row.
    /// </summary>
    [JsonPropertyName("run")]
    public required string RunId { get; init; }

    /// <summary>
    /// Path to the raw run file used to build this row.
    /// </summary>
    [JsonPropertyName("raw")]
    public required string RawResultPath { get; init; }

    /// <summary>
    /// Timestamp of the raw run.
    /// </summary>
    [JsonPropertyName("at")]
    public required DateTimeOffset RunTimestampUtc { get; init; }

    /// <summary>
    /// Technical execution success from the raw run.
    /// </summary>
    [JsonPropertyName("technical")]
    public required bool TechnicalSuccess { get; init; }

    /// <summary>
    /// Optional semantic success from the raw run.
    /// </summary>
    [JsonPropertyName("semantic")]
    public bool? SemanticSuccess { get; init; }

    /// <summary>
    /// Elapsed milliseconds for this single run.
    /// </summary>
    [JsonPropertyName("elapsed")]
    public double ElapsedMsSingleRun { get; init; }

    /// <summary>
    /// Load phase duration in milliseconds.
    /// </summary>
    [JsonPropertyName("load")]
    public double LoadMs { get; init; }

    /// <summary>
    /// Build phase duration in milliseconds.
    /// </summary>
    [JsonPropertyName("build")]
    public double BuildMs { get; init; }

    /// <summary>
    /// Reopen/refresh duration in milliseconds.
    /// </summary>
    [JsonPropertyName("reopen")]
    public double ReopenMs { get; init; }

    /// <summary>
    /// Lookup phase duration in milliseconds.
    /// </summary>
    [JsonPropertyName("lookup")]
    public double LookupMs { get; init; }

    /// <summary>
    /// Total bytes of all recorded artifacts.
    /// </summary>
    [JsonPropertyName("totalBytes")]
    public double TotalArtifactBytes { get; init; }

    /// <summary>
    /// Bytes of primary data file(s).
    /// </summary>
    [JsonPropertyName("primaryBytes")]
    public double PrimaryArtifactBytes { get; init; }

    /// <summary>
    /// Bytes of side artifacts (for example WAL/state/index files).
    /// </summary>
    [JsonPropertyName("sideBytes")]
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
    [JsonPropertyName("comparison")]
    public required string ComparisonId { get; init; }

    /// <summary>
    /// UTC time when the derived series artifact was generated.
    /// </summary>
    [JsonPropertyName("at")]
    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>
    /// Experiment key shared by all runs in the set.
    /// </summary>
    [JsonPropertyName("experiment")]
    public required string ExperimentKey { get; init; }

    /// <summary>
    /// Comparison set id that links related runs across engines.
    /// </summary>
    [JsonPropertyName("set")]
    public required string ComparisonSetId { get; init; }

    /// <summary>
    /// Shared dataset profile or <c>mixed</c>.
    /// </summary>
    [JsonPropertyName("dataset")]
    public string? DatasetProfileKey { get; init; }

    /// <summary>
    /// Shared fairness profile or <c>mixed</c>.
    /// </summary>
    [JsonPropertyName("fairness")]
    public string? FairnessProfileKey { get; init; }

    /// <summary>
    /// Shared environment class or <c>mixed</c>.
    /// </summary>
    [JsonPropertyName("env")]
    public string? EnvironmentClass { get; init; }

    /// <summary>
    /// Engine list included in the comparison.
    /// </summary>
    [JsonPropertyName("engines")]
    public required IReadOnlyList<string> Engines { get; init; }

    /// <summary>
    /// Aggregated statistics per engine.
    /// </summary>
    [JsonPropertyName("series")]
    public required IReadOnlyList<CrossEngineSeriesEngineEntry> EngineSeries { get; init; }

    /// <summary>
    /// Optional explanatory notes about aggregation behavior.
    /// </summary>
    [JsonPropertyName("notes")]
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
    [JsonPropertyName("engine")]
    public required string EngineKey { get; init; }

    /// <summary>
    /// Number of measured runs included in aggregation.
    /// </summary>
    [JsonPropertyName("measured")]
    public required int MeasuredRunCount { get; init; }

    /// <summary>
    /// Number of warmup runs found for this engine in the set.
    /// Warmup runs are stored for context but not aggregated into metric stats.
    /// </summary>
    [JsonPropertyName("warmup")]
    public required int WarmupRunCount { get; init; }

    /// <summary>
    /// Number of measured runs with technical success.
    /// </summary>
    [JsonPropertyName("technicalOk")]
    public required int TechnicalSuccessCount { get; init; }

    /// <summary>
    /// Number of measured runs with semantic success.
    /// </summary>
    [JsonPropertyName("semanticOk")]
    public required int SemanticSuccessCount { get; init; }

    /// <summary>
    /// Number of measured runs where semantic outcome was evaluated.
    /// </summary>
    [JsonPropertyName("semanticChecked")]
    public required int SemanticEvaluatedCount { get; init; }

    /// <summary>
    /// Paths of measured raw run files used in this aggregation.
    /// </summary>
    [JsonPropertyName("raw")]
    public required IReadOnlyList<string> RawResultPaths { get; init; }

    /// <summary>
    /// Aggregate stats for elapsed milliseconds.
    /// </summary>
    [JsonPropertyName("elapsed")]
    public required MetricSeriesStats ElapsedMs { get; init; }

    /// <summary>
    /// Aggregate stats for load phase milliseconds.
    /// </summary>
    [JsonPropertyName("load")]
    public required MetricSeriesStats LoadMs { get; init; }

    /// <summary>
    /// Aggregate stats for build phase milliseconds.
    /// </summary>
    [JsonPropertyName("build")]
    public required MetricSeriesStats BuildMs { get; init; }

    /// <summary>
    /// Aggregate stats for reopen/refresh milliseconds.
    /// </summary>
    [JsonPropertyName("reopen")]
    public required MetricSeriesStats ReopenMs { get; init; }

    /// <summary>
    /// Aggregate stats for lookup milliseconds.
    /// </summary>
    [JsonPropertyName("lookup")]
    public required MetricSeriesStats LookupMs { get; init; }

    /// <summary>
    /// Aggregate stats for random point lookup operation count.
    /// Optional for backward compatibility with older comparison-series artifacts.
    /// For normalized reference workload this should typically stay near 10_000 when present.
    /// </summary>
    [JsonPropertyName("lookupBatch")]
    public MetricSeriesStats? LookupBatchCount { get; init; }

    /// <summary>
    /// Aggregate stats for total artifact bytes.
    /// </summary>
    [JsonPropertyName("totalBytes")]
    public required MetricSeriesStats TotalArtifactBytes { get; init; }

    /// <summary>
    /// Aggregate stats for primary data bytes.
    /// </summary>
    [JsonPropertyName("primaryBytes")]
    public required MetricSeriesStats PrimaryArtifactBytes { get; init; }

    /// <summary>
    /// Aggregate stats for side artifact bytes.
    /// </summary>
    [JsonPropertyName("sideBytes")]
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
    [JsonPropertyName("count")]
    public required int Count { get; init; }

    /// <summary>
    /// Number of runs where this metric is missing.
    /// </summary>
    [JsonPropertyName("missing")]
    public required int MissingCount { get; init; }

    /// <summary>
    /// Minimum value across available samples.
    /// </summary>
    [JsonPropertyName("min")]
    public double? Min { get; init; }

    /// <summary>
    /// Maximum value across available samples.
    /// </summary>
    [JsonPropertyName("max")]
    public double? Max { get; init; }

    /// <summary>
    /// Arithmetic average across available samples.
    /// </summary>
    [JsonPropertyName("avg")]
    public double? Average { get; init; }

    /// <summary>
    /// Median across available samples.
    /// </summary>
    [JsonPropertyName("median")]
    public double? Median { get; init; }
}
