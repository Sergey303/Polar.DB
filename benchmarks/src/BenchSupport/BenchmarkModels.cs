namespace PolarDbBenchmarks;

internal enum ExperimentKind
{
    PkIntLookup,
    PkStringLookup,
    ExternalIntLookup,
    ExternalStringLookup,
    BuildOnly,
    ReopenOnly,
    AppendOnly,
    DeleteOnly
}

internal static class ExperimentKindExtensions
{
    public static bool IsLookup(this ExperimentKind kind) =>
        kind is ExperimentKind.PkIntLookup
            or ExperimentKind.PkStringLookup
            or ExperimentKind.ExternalIntLookup
            or ExperimentKind.ExternalStringLookup;
}

internal sealed record ExperimentOptions(
    string ExperimentId,
    string Title,
    ExperimentKind Kind,
    int SetupRows,
    int WarmupOps,
    int MeasuredOps);

internal sealed record Row(
    long Id,
    string SKey,
    int ExternalId,
    string ExternalKey,
    string Payload);

internal sealed record QueryResult(long Rows, ulong Checksum);

internal sealed record EngineResult(
    string Engine,
    string Status,
    IReadOnlyList<double> SamplesMs,
    long Rows,
    ulong Checksum,
    long ArtifactBytes);
