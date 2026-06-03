namespace PolarDbBenchmarks;

internal enum ExperimentKind
{
    PkIntLookup,
    PkLongLookup,
    PkGuidLookup,
    PkStringLookup,
    ExternalIntLookup,
    ExternalLongLookup,
    ExternalGuidLookup,
    ExternalStringLookup,
    ExternalFamousIntLookup,
    ExternalFamousLongLookup,
    ExternalFamousGuidLookup,
    ExternalFamousStringLookup,
    BuildOnly,
    ReopenOnly,
    AppendOnly,
    DeleteOnly
}

internal static class ExperimentKindExtensions
{
    public static bool IsLookup(this ExperimentKind kind) =>
        kind is ExperimentKind.PkIntLookup or ExperimentKind.PkLongLookup
            or ExperimentKind.PkGuidLookup or ExperimentKind.PkStringLookup
            or ExperimentKind.ExternalIntLookup or ExperimentKind.ExternalLongLookup
            or ExperimentKind.ExternalGuidLookup or ExperimentKind.ExternalStringLookup
            or ExperimentKind.ExternalFamousIntLookup or ExperimentKind.ExternalFamousLongLookup
            or ExperimentKind.ExternalFamousGuidLookup or ExperimentKind.ExternalFamousStringLookup;

    public static bool IsFamousExternal(this ExperimentKind kind) =>
        kind is ExperimentKind.ExternalFamousIntLookup or ExperimentKind.ExternalFamousLongLookup
            or ExperimentKind.ExternalFamousGuidLookup or ExperimentKind.ExternalFamousStringLookup;
}

internal sealed record ExperimentOptions(
    string ExperimentId,
    string Title,
    ExperimentKind Kind,
    IReadOnlyList<int> RowCounts,
    int WarmupOps,
    int MeasuredOps);

internal sealed record BenchmarkRunResult(int SetupRows, QueryResult Expected, IReadOnlyList<EngineResult> Engines);

internal sealed record Row(
    long Id,
    long LongKey,
    string GuidKey,
    string SKey,
    int ExternalId,
    long ExternalLong,
    string ExternalGuid,
    string ExternalKey,
    string Payload);

internal sealed record QueryResult(long Rows, ulong Checksum);

internal sealed record ResourceSnapshot(long ManagedBytes, long WorkingSetBytes, long PrivateBytes, long AvailableMemoryBytes);

internal sealed record EngineResult(
    string Engine,
    string Status,
    IReadOnlyList<double> SamplesMs,
    long Rows,
    ulong Checksum,
    long ArtifactBytes,
    ResourceSnapshot ResourcesBefore,
    ResourceSnapshot ResourcesAfter);
