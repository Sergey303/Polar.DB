namespace Polar.DB.Bench.Core.Models;

public sealed record CrossEngineComparisonResult
{
    public required string ComparisonId { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public required string ExperimentKey { get; init; }
    public string? DatasetProfileKey { get; init; }
    public string? FairnessProfileKey { get; init; }
    public string? EnvironmentClass { get; init; }
    public required IReadOnlyList<CrossEngineComparisonEntry> Engines { get; init; }
    public List<string>? Notes { get; init; }
}

public sealed record CrossEngineComparisonEntry
{
    public required string EngineKey { get; init; }
    public required string RunId { get; init; }
    public required string RawResultPath { get; init; }
    public required DateTimeOffset RunTimestampUtc { get; init; }
    public required bool TechnicalSuccess { get; init; }
    public bool? SemanticSuccess { get; init; }
    public double ElapsedMsSingleRun { get; init; }
    public double LoadMs { get; init; }
    public double BuildMs { get; init; }
    public double ReopenMs { get; init; }
    public double LookupMs { get; init; }
    public double TotalArtifactBytes { get; init; }
    public double PrimaryArtifactBytes { get; init; }
    public double SideArtifactBytes { get; init; }
}

public sealed record CrossEngineComparisonSeriesResult
{
    public required string ComparisonId { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public required string ExperimentKey { get; init; }
    public required string ComparisonSetId { get; init; }
    public string? DatasetProfileKey { get; init; }
    public string? FairnessProfileKey { get; init; }
    public string? EnvironmentClass { get; init; }
    public required IReadOnlyList<string> Engines { get; init; }
    public required IReadOnlyList<CrossEngineSeriesEngineEntry> EngineSeries { get; init; }
    public List<string>? Notes { get; init; }
}

public sealed record CrossEngineSeriesEngineEntry
{
    public required string EngineKey { get; init; }
    public required int MeasuredRunCount { get; init; }
    public required int WarmupRunCount { get; init; }
    public required int TechnicalSuccessCount { get; init; }
    public required int SemanticSuccessCount { get; init; }
    public required int SemanticEvaluatedCount { get; init; }
    public required IReadOnlyList<string> RawResultPaths { get; init; }
    public required MetricSeriesStats ElapsedMs { get; init; }
    public required MetricSeriesStats LoadMs { get; init; }
    public required MetricSeriesStats BuildMs { get; init; }
    public required MetricSeriesStats ReopenMs { get; init; }
    public required MetricSeriesStats LookupMs { get; init; }
    public required MetricSeriesStats TotalArtifactBytes { get; init; }
    public required MetricSeriesStats PrimaryArtifactBytes { get; init; }
    public required MetricSeriesStats SideArtifactBytes { get; init; }
}

public sealed record MetricSeriesStats
{
    public required int Count { get; init; }
    public required int MissingCount { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public double? Average { get; init; }
    public double? Median { get; init; }
}
