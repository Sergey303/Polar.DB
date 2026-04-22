namespace Polar.DB.Bench.Core.Models;

public sealed record EnvironmentManifest
{
    public required string EnvironmentClass { get; init; }
    public required string MachineName { get; init; }
    public required string OsDescription { get; init; }
    public required string OsArchitecture { get; init; }
    public required string ProcessArchitecture { get; init; }
    public required string FrameworkDescription { get; init; }
    public required bool Is64BitProcess { get; init; }
    public int ProcessorCount { get; init; }
    public string? CurrentDirectory { get; init; }
    public string? UserName { get; init; }
    public GitManifest? Git { get; init; }
}
