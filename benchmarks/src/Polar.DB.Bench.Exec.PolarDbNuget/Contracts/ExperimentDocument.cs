namespace Polar.DB.Bench.Exec.PolarDbNuget.Contracts;

internal sealed class ExperimentDocument
{
    public string ExperimentId { get; set; } = "polar-db-nuget-smoke";
    public string ScenarioKey { get; set; } = "load-build-refresh-lookup";
    public DatasetSpec Dataset { get; set; } = new();
    public WorkloadSpec Workload { get; set; } = new();
}

internal sealed class DatasetSpec
{
    public int RecordCount { get; set; } = 10_000;
    public string KeyPattern { get; set; } = "sequential";
    public int DuplicateModulo { get; set; } = 0;
    public int Seed { get; set; } = 12345;
}

internal sealed class WorkloadSpec
{
    public int LookupCount { get; set; } = 10_000;
    public bool BuildBeforeLookup { get; set; } = true;
    public bool RefreshBeforeLookup { get; set; } = true;
    public string LookupPattern { get; set; } = "random-existing";
}
