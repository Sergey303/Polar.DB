using System.Diagnostics;
using System.Collections.Generic;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Engine.Sqlite;

internal sealed record MaterializedRowsResult(int ReturnedRows, int WrongRows);

internal sealed record SqliteLookupArtifactLayout(string ArtifactsRootDirectory, string DatabasePath);

internal sealed record SqliteLookupArtifactInventory(
    IReadOnlyList<ArtifactDescriptor> Descriptors,
    long TotalBytes,
    long PrimaryDatabaseBytes,
    long WalBytes,
    long SharedMemoryBytes);

internal sealed record LookupProbeResult(
    double TotalMs,
    double IndexSearchMs,
    double MaterializationMs,
    int ReturnedOffsets,
    int ReturnedRows,
    bool Matched,
    string? MismatchReason)
{
    public static LookupProbeResult Passed(
        Stopwatch total,
        Stopwatch index,
        Stopwatch materialization,
        int returnedOffsets,
        int returnedRows)
    {
        return new LookupProbeResult(
            total.Elapsed.TotalMilliseconds,
            index.Elapsed.TotalMilliseconds,
            materialization.Elapsed.TotalMilliseconds,
            returnedOffsets,
            returnedRows,
            true,
            null);
    }

    public static LookupProbeResult Failed(
        Stopwatch total,
        Stopwatch index,
        Stopwatch materialization,
        int returnedOffsets,
        int returnedRows,
        string reason)
    {
        return new LookupProbeResult(
            total.Elapsed.TotalMilliseconds,
            index.Elapsed.TotalMilliseconds,
            materialization.Elapsed.TotalMilliseconds,
            returnedOffsets,
            returnedRows,
            false,
            reason);
    }
}
