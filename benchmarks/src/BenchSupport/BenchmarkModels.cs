namespace PolarDbBenchmarks;

public enum ExperimentKind
{
    PkIntLookup, PkLongLookup, PkGuidLookup, PkStringLookup,
    ExternalIntLookup, ExternalLongLookup, ExternalGuidLookup, ExternalStringLookup,
    ExternalFamousIntLookup, ExternalFamousLongLookup,
    ExternalFamousGuidLookup, ExternalFamousStringLookup,
    BuildPrimaryIntOnly, ReopenOnly, AppendOnly, DeleteOnly
}

public enum BenchmarkEngine
{
    Sqlite,
    PolarDb
}

public static class ExperimentKindExtensions
{
    public static bool IsLookup(this ExperimentKind kind) =>
        kind is ExperimentKind.PkIntLookup or ExperimentKind.PkLongLookup
            or ExperimentKind.PkGuidLookup or ExperimentKind.PkStringLookup
            or ExperimentKind.ExternalIntLookup or ExperimentKind.ExternalLongLookup
            or ExperimentKind.ExternalGuidLookup or ExperimentKind.ExternalStringLookup
            or ExperimentKind.ExternalFamousIntLookup or ExperimentKind.ExternalFamousLongLookup
            or ExperimentKind.ExternalFamousGuidLookup or ExperimentKind.ExternalFamousStringLookup;

    public static bool IsPrimaryLookup(this ExperimentKind kind) =>
        kind is ExperimentKind.PkIntLookup or ExperimentKind.PkLongLookup
            or ExperimentKind.PkGuidLookup or ExperimentKind.PkStringLookup;

    public static bool IsFamousExternal(this ExperimentKind kind) =>
        kind is ExperimentKind.ExternalFamousIntLookup or ExperimentKind.ExternalFamousLongLookup
            or ExperimentKind.ExternalFamousGuidLookup or ExperimentKind.ExternalFamousStringLookup;
}

public sealed record ExperimentOptions(
    string ExperimentId, string Title, ExperimentKind Kind,
    IReadOnlyList<int> RowCounts, int WarmupOps, int MeasuredOps);

public sealed record BenchmarkResolvedOptions(
    string ExperimentId,
    string Title,
    ExperimentKind Kind,
    IReadOnlyList<int> RowCounts,
    string SamplingModel,
    int? WarmupOperations,
    int? MeasuredOperations);

public sealed record BenchmarkRunResult(int SetupRows, QueryResult Expected, IReadOnlyList<EngineResult> Engines);

public sealed record LookupRunResult(int SetupRows, IReadOnlyList<LookupPhaseResult> Phases);

public sealed record LookupPlanManifest(
    string Name,
    bool FileWarmup,
    int WarmupQueries,
    int MeasuredBatches,
    int QueriesPerBatch,
    int BatchQueries,
    int LatencySamples,
    int TotalMeasuredQueries);

public sealed record LookupPhaseResult(
    string Name,
    LookupPlanManifest Plan,
    QueryResult ExpectedBatch,
    QueryResult ExpectedLatency,
    IReadOnlyList<LookupEngineResult> Engines);

public sealed record Row(long Id, long LongKey, Guid GuidKey, string SKey,
    int ExternalId, long ExternalLong, Guid ExternalGuid, string ExternalKey, string Payload);

public sealed record QueryResult(long Rows, ulong Checksum);

public sealed record ResourceSnapshot(long ManagedBytes, long WorkingSetBytes, long PrivateBytes, long AvailableMemoryBytes);

public sealed record PrimaryBuildStageSamples(
    IReadOnlyList<double> ScanMs,
    IReadOnlyList<double> ToArrayMs,
    IReadOnlyList<double> SortMs,
    IReadOnlyList<double> WriteHashKeysMs,
    IReadOnlyList<double> WriteOffsetsMs,
    IReadOnlyList<double> GcMs,
    IReadOnlyList<double> ProfileTotalMs);

public sealed record EngineResult(
    string Engine,
    string Status,
    string Metric,
    IReadOnlyList<double> SamplesMs,
    long Rows,
    ulong Checksum,
    long ArtifactBytes,
    ResourceSnapshot ResourcesBefore,
    ResourceSnapshot ResourcesAfter,
    IReadOnlyList<double>? BuildSamplesMs = null,
    IReadOnlyList<double>? FlushSamplesMs = null,
    PrimaryBuildStageSamples? PrimaryBuildStages = null,
    IReadOnlyList<double>? LoadSamplesMs = null,
    IReadOnlyList<double>? OpenSamplesMs = null,
    IReadOnlyList<double>? DurableSamplesMs = null,
    int DurableBatchSize = 0);

public sealed record LookupEngineResult(
    string Engine, string Status, IReadOnlyList<double> BatchAvgSamplesMs,
    IReadOnlyList<double> LatencySamplesMs, long BatchQueries, long BatchRows,
    ulong BatchChecksum, long LatencyRows, ulong LatencyChecksum, long ArtifactBytes,
    ResourceSnapshot ResourcesBefore, ResourceSnapshot ResourcesAfter);

public sealed record BenchmarkEnvironmentManifest(
    string RunId,
    string ExperimentId,
    string ProcessRole,
    string? Engine,
    int ProcessId,
    DateTimeOffset CapturedUtc,
    string CommitSha,
    bool GitDirty,
    bool GitStatusKnown,
    string RuntimeVersion,
    string FrameworkDescription,
    string OsDescription,
    string OsArchitecture,
    string ProcessArchitecture,
    int ProcessorCount,
    bool ServerGc,
    string BuildConfiguration,
    bool IsDebugBuild,
    bool OptimizationsDisabled,
    bool PublicationReady,
    string TieredCompilationSetting,
    string TieredPgoSetting,
    string ReadyToRunSetting,
    string CpuDescription,
    string PolarDbAssemblyVersion,
    string SqliteAssemblyVersion,
    string CurrentDirectory,
    string CommandLine,
    string TimeZone,
    string Culture,
    long? DriveTotalBytes,
    long? DriveAvailableBytes,
    string? SqliteNativeVersion = null);

public sealed record BenchmarkRunManifest(
    string RunId,
    string ExperimentId,
    DateTimeOffset StartedUtc,
    BenchmarkEnvironmentManifest Coordinator,
    IReadOnlyList<BenchmarkEnvironmentManifest> EngineProcesses,
    IReadOnlyList<string> EngineOrder,
    string ReopenDefinition,
    string VolatileMutationDefinition,
    string DurableMutationDefinition);

public sealed record BenchmarkWorkerResult(
    string RunId,
    string ExperimentId,
    BenchmarkEngine Engine,
    BenchmarkEnvironmentManifest Manifest,
    IReadOnlyList<BenchmarkRunResult>? LifecycleRuns,
    IReadOnlyList<LookupRunResult>? LookupRuns);

public sealed record BenchmarkCombinedRaw(
    BenchmarkResolvedOptions Options,
    BenchmarkRunManifest Manifest,
    IReadOnlyList<BenchmarkRunResult>? LifecycleRuns,
    IReadOnlyList<LookupRunResult>? LookupRuns);
