namespace Polar.DB.Bench.Exec.PolarDbNuget.Workloads;

internal sealed class WorkloadResult
{
    public Dictionary<string, double> Metrics { get; } = new(StringComparer.OrdinalIgnoreCase);
}
