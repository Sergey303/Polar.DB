namespace Polar.DB.Bench.Core.Models;

public sealed record DatasetSpec
{
    public required string ProfileKey { get; init; }
    public long RecordCount { get; init; }
    public int? Seed { get; init; }
    public string? Notes { get; init; }
}
