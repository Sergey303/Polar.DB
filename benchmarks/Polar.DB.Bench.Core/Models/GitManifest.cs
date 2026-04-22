namespace Polar.DB.Bench.Core.Models;

public sealed record GitManifest
{
    public string? Commit { get; init; }
    public string? Branch { get; init; }
}
