namespace Polar.DB.Bench.Core.Models;

public sealed record WorkloadSpec
{
    public required string WorkloadKey { get; init; }
    public int? LookupCount { get; init; }
    public int? BatchCount { get; init; }
    public int? BatchSize { get; init; }
    public string? Notes { get; init; }
    public Dictionary<string, string>? Parameters { get; init; }
}
