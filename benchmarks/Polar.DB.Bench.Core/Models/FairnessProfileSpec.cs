namespace Polar.DB.Bench.Core.Models;

public sealed record FairnessProfileSpec
{
    public required string FairnessProfileKey { get; init; }
    public string? Notes { get; init; }
}
