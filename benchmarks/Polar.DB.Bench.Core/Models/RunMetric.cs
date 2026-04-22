namespace Polar.DB.Bench.Core.Models;

public sealed record RunMetric
{
    public required string MetricKey { get; init; }
    public double Value { get; init; }
}
