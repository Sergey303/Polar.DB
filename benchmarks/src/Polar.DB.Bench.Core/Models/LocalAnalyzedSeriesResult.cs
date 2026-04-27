using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

    /// <summary>
    /// Local analyzed artifact for one target within one experiment.
    /// This artifact stores local interpretation derived from raw runs and does not contain cross-target comparison output.
    /// </summary>
public sealed record LocalAnalyzedSeriesResult
{
    [JsonPropertyName("artifact")]
    public required string ArtifactKind { get; init; }

    [JsonPropertyName("at")]
    public required DateTimeOffset AnalysisTimestampUtc { get; init; }

    [JsonPropertyName("experiment")]
    public required string ExperimentKey { get; init; }

    /// <summary>
    /// Target key (runtime variant identifier), for example <c>polar-db-current</c> or <c>sqlite</c>.
    /// The JSON property name <c>engine</c> is preserved for backward compatibility.
    /// </summary>
    [JsonPropertyName("engine")]
    public required string EngineKey { get; init; }

    [JsonPropertyName("set")]
    public string? ComparisonSetId { get; init; }

    [JsonPropertyName("dataset")]
    public string? DatasetProfileKey { get; init; }

    [JsonPropertyName("fairness")]
    public string? FairnessProfileKey { get; init; }

    [JsonPropertyName("env")]
    public string? EnvironmentClass { get; init; }

    [JsonPropertyName("measured")]
    public required int MeasuredRunCount { get; init; }

    [JsonPropertyName("warmup")]
    public required int WarmupRunCount { get; init; }

    [JsonPropertyName("technicalOk")]
    public required int TechnicalSuccessCount { get; init; }

    [JsonPropertyName("semanticOk")]
    public required int SemanticSuccessCount { get; init; }

    [JsonPropertyName("semanticChecked")]
    public required int SemanticEvaluatedCount { get; init; }

    [JsonPropertyName("raw")]
    public required IReadOnlyList<string> RawResultPaths { get; init; }

    [JsonPropertyName("elapsed")]
    public required MetricSeriesStats ElapsedMs { get; init; }

    [JsonPropertyName("load")]
    public required MetricSeriesStats LoadMs { get; init; }

    [JsonPropertyName("build")]
    public required MetricSeriesStats BuildMs { get; init; }

    [JsonPropertyName("reopen")]
    public required MetricSeriesStats ReopenMs { get; init; }

    [JsonPropertyName("lookup")]
    public required MetricSeriesStats LookupMs { get; init; }

    [JsonPropertyName("lookupBatch")]
    public MetricSeriesStats? LookupBatchCount { get; init; }

    [JsonPropertyName("totalBytes")]
    public required MetricSeriesStats TotalArtifactBytes { get; init; }

    [JsonPropertyName("primaryBytes")]
    public required MetricSeriesStats PrimaryArtifactBytes { get; init; }

    [JsonPropertyName("sideBytes")]
    public required MetricSeriesStats SideArtifactBytes { get; init; }

    /// <summary>
    /// Generic metrics dictionary containing stats for all raw metric keys
    /// that Analysis sees for this target series.
    /// This includes search metrics, memory metrics, and any future metrics
    /// not exposed as fixed properties.
    /// </summary>
    [JsonPropertyName("metrics")]
    public IReadOnlyDictionary<string, MetricSeriesStats>? Metrics { get; init; }

    [JsonPropertyName("notes")]
    public List<string>? Notes { get; init; }
}
