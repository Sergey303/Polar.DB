namespace Polar.DB.Bench.Core.Models;

public sealed record RunWorkspace
{
    public required string RootDirectory { get; init; }
    public required string WorkingDirectory { get; init; }
    public required string RawResultsDirectory { get; init; }
    public required string EnvironmentClass { get; init; }
    public string? ArtifactsDirectory { get; init; }
}
