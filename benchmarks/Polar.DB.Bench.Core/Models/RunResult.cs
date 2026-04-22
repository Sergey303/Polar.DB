namespace Polar.DB.Bench.Core.Models;

public sealed record RunResult
{
    public required string RunId { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public required string EngineKey { get; init; }
    public required string ExperimentKey { get; init; }
    public required string DatasetProfileKey { get; init; }
    public required string FairnessProfileKey { get; init; }
    public required EnvironmentManifest Environment { get; init; }
    public required bool TechnicalSuccess { get; init; }
    public string? TechnicalFailureReason { get; init; }
    public bool? SemanticSuccess { get; init; }
    public string? SemanticFailureReason { get; init; }
    public required IReadOnlyList<RunMetric> Metrics { get; init; }
    public required IReadOnlyList<ArtifactDescriptor> Artifacts { get; init; }
    public Dictionary<string, string>? EngineDiagnostics { get; init; }
    public Dictionary<string, string>? Tags { get; init; }
    public List<string>? Notes { get; init; }
}
