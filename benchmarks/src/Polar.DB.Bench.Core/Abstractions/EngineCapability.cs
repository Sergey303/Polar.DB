namespace Polar.DB.Bench.Core.Abstractions;

public enum EngineCapability
{
    BulkLoad,
    PointLookup,
    RangeLookup,
    ReopenRecovery,
    FaultInjection,
    PhysicalArtifactInspection,
    DuplicateKeyScenario,
    AppendCycles
}
