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
