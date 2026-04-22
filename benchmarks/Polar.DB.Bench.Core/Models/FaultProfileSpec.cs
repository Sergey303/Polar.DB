namespace Polar.DB.Bench.Core.Models;

public sealed record FaultProfileSpec
{
    public required string FaultProfileKey { get; init; }
    public string? Notes { get; init; }
    public Dictionary<string, string>? Parameters { get; init; }
}
