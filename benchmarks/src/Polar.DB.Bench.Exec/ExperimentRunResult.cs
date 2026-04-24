using System;
using System.Collections.Generic;

namespace Polar.DB.Bench.Exec;

public sealed class ExperimentRunResult
{
    public required string RunId { get; init; }

    public required string ExperimentId { get; init; }

    public required string EngineKey { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required DateTimeOffset FinishedAtUtc { get; init; }

    public required bool Success { get; init; }

    public string? ErrorType { get; init; }

    public string? ErrorMessage { get; init; }

    public Dictionary<string, object?> Metrics { get; init; } = new();

    public List<ArtifactInfo> Artifacts { get; init; } = new();

    public CleanupReport? Cleanup { get; init; }
}

public sealed class ArtifactInfo
{
    public required string Path { get; init; }

    public required string Role { get; init; }

    public required long Bytes { get; init; }
}