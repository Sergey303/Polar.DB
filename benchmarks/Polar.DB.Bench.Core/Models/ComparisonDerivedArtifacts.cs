using System.Text.Json.Serialization;

namespace Polar.DB.Bench.Core.Models;

/// <summary>
/// Latest cross-engine comparison snapshot for one experiment.
/// Stored under experiment <c>comparisons/</c>.
/// </summary>
public sealed record LatestEnginesComparisonArtifact
{
    [JsonPropertyName("artifact")]
    public required string ArtifactKind { get; init; }

    [JsonPropertyName("at")]
    public required DateTimeOffset AnalysisTimestampUtc { get; init; }

    [JsonPropertyName("experiment")]
    public required string ExperimentKey { get; init; }

    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    [JsonPropertyName("snapshot")]
    public ComparisonSnapshot? Snapshot { get; init; }

    [JsonPropertyName("expectations")]
    public IReadOnlyList<string>? DerivedExpectations { get; init; }

    [JsonPropertyName("notes")]
    public IReadOnlyList<string>? Notes { get; init; }
}

/// <summary>
/// History comparison artifact for one experiment.
/// Stored under experiment <c>comparisons/</c>.
/// </summary>
public sealed record LatestHistoryComparisonArtifact
{
    [JsonPropertyName("artifact")]
    public required string ArtifactKind { get; init; }

    [JsonPropertyName("at")]
    public required DateTimeOffset AnalysisTimestampUtc { get; init; }

    [JsonPropertyName("experiment")]
    public required string ExperimentKey { get; init; }

    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    [JsonPropertyName("snapshots")]
    public required IReadOnlyList<ComparisonSnapshot> Snapshots { get; init; }

    [JsonPropertyName("expectations")]
    public IReadOnlyList<string>? DerivedExpectations { get; init; }

    [JsonPropertyName("notes")]
    public IReadOnlyList<string>? Notes { get; init; }
}

/// <summary>
/// Cross-experiment context artifact.
/// Stored under experiment <c>comparisons/</c>.
/// </summary>
public sealed record LatestOtherExperimentsComparisonArtifact
{
    [JsonPropertyName("artifact")]
    public required string ArtifactKind { get; init; }

    [JsonPropertyName("at")]
    public required DateTimeOffset AnalysisTimestampUtc { get; init; }

    [JsonPropertyName("experiment")]
    public required string ExperimentKey { get; init; }

    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    [JsonPropertyName("current")]
    public ComparisonSnapshot? CurrentExperimentSnapshot { get; init; }

    [JsonPropertyName("others")]
    public required IReadOnlyList<ComparisonSnapshot> OtherExperimentSnapshots { get; init; }

    [JsonPropertyName("expectations")]
    public IReadOnlyList<string>? DerivedExpectations { get; init; }

    [JsonPropertyName("notes")]
    public IReadOnlyList<string>? Notes { get; init; }
}

/// <summary>
/// One measured-series snapshot used by derived comparison artifacts.
/// </summary>
public sealed record ComparisonSnapshot
{
    [JsonPropertyName("experiment")]
    public required string ExperimentKey { get; init; }

    [JsonPropertyName("set")]
    public string? ComparisonSetId { get; init; }

    [JsonPropertyName("at")]
    public required DateTimeOffset SnapshotTimestampUtc { get; init; }

    [JsonPropertyName("dataset")]
    public string? DatasetProfileKey { get; init; }

    [JsonPropertyName("fairness")]
    public string? FairnessProfileKey { get; init; }

    [JsonPropertyName("env")]
    public string? EnvironmentClass { get; init; }

    [JsonPropertyName("engines")]
    public required IReadOnlyList<string> Engines { get; init; }

    [JsonPropertyName("series")]
    public required IReadOnlyList<CrossEngineSeriesEngineEntry> EngineSeries { get; init; }
}
