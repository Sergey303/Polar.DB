namespace Polar.DB.Bench.Core.Models;

public sealed record PolicyContract
{
    public required string PolicyId { get; init; }
    public PolicyAppliesTo? AppliesTo { get; init; }
    public required IReadOnlyList<MetricGuardPolicy> Guards { get; init; }
}
