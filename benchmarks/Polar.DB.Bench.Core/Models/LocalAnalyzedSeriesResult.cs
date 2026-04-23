using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

/// <summary>
/// Local analyzed artifact for one engine within one experiment.
/// This artifact stores local interpretation derived from raw runs and does not contain cross-engine comparison output.
/// </summary>
public sealed record LocalAnalyzedSeriesResult
{
    [JsonPropertyName("artifact")]
    public required string ArtifactKind { get; init; }

    [JsonPropertyName("at")]
    public required DateTimeOffset AnalysisTimestampUtc { get; init; }

    [JsonPropertyName("experiment")]
    public required string ExperimentKey { get; init; }

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

    [JsonPropertyName("notes")]
    public List<string>? Notes { get; init; }
}
