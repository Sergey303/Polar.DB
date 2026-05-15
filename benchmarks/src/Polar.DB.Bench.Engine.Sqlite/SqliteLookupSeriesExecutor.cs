using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.LookupSeries;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;
using static Polar.DB.Bench.Core.Services.FileWarmup;

namespace Polar.DB.Bench.Engine.Sqlite;

/// <summary>
/// SQLite executor for lookup-series workloads.
/// The split is now comparable with Polar.DB: key -> rowid search is measured
/// separately from rowid -> payload materialization.
/// </summary>
internal static partial class SqliteLookupSeriesExecutor
{
    private const string EngineKeyValue = "sqlite";

    public static Task<RunResult> ExecuteAsync(
        ExperimentSpec spec,
        RunWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        var options = LookupSeriesWorkload.Resolve(spec);
        var state = SqliteLookupRunState.Create(spec, workspace, options);
        var totalStopwatch = Stopwatch.StartNew();
        SqliteConnection? connection = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            connection = OpenConnection(state.ArtifactLayout.DatabasePath, state.FairnessProfileKey);
            CreateSchema(connection, options);

            state.LoadMs = Measure(() => BulkLoad(connection, spec, options, cancellationToken));
            state.BuildMs = Measure(() => ExecuteNonQuery(
                connection,
                "CREATE INDEX IF NOT EXISTS ix_records_lookup_key ON records(lookup_key);"));

            connection = ReopenIfNeeded(connection, state, options, cancellationToken);

            if (IsWarmEnabled(spec.Workload.Parameters))
            {
                WarmDirectory(state.ArtifactLayout.ArtifactsRootDirectory, cancellationToken: cancellationToken);
            }

            using var lookup = new SqliteLookupCommands(connection, options);
            RunDirectProbe(spec, options, lookup, state);
            RunProbeSeries(spec, options, lookup, state, cancellationToken);

            state.SemanticSuccess = state.DirectHit &&
                                    state.LookupProbeMisses == 0 &&
                                    state.RowCountAfterReopen == spec.Dataset.RecordCount;
            if (state.SemanticSuccess == false)
            {
                state.SemanticFailureReason = BuildSemanticFailureReason(
                    spec.Dataset.RecordCount,
                    state.RowCountAfterReopen,
                    options.LookupCount,
                    state.LookupProbeHits,
                    state.LookupProbeMisses,
                    state.LookupExpectedRows,
                    state.LookupReturnedRows,
                    state.MismatchSamples);
            }
        }
        catch (Exception ex)
        {
            state.TechnicalSuccess = false;
            state.TechnicalFailureReason = ex.ToString();
        }
        finally
        {
            TryDispose(connection);
            totalStopwatch.Stop();
        }

        state.ElapsedMs = totalStopwatch.Elapsed.TotalMilliseconds;
        return Task.FromResult(BuildRunResult(state));
    }

    private static SqliteConnection ReopenIfNeeded(
        SqliteConnection connection,
        SqliteLookupRunState state,
        LookupSeriesOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.ReopenAfterBuild)
        {
            state.RowCountAfterReopen = CountRows(connection);
            return connection;
        }

        connection.Dispose();
        cancellationToken.ThrowIfCancellationRequested();
        SqliteConnection reopened = null!;
        state.ReopenRefreshMs = Measure(() =>
        {
            reopened = OpenConnection(state.ArtifactLayout.DatabasePath, state.FairnessProfileKey);
            state.RowCountAfterReopen = CountRows(reopened);
        });

        return reopened;
    }

    private static double Measure(Action action)
    {
        var watch = Stopwatch.StartNew();
        action();
        watch.Stop();
        return watch.Elapsed.TotalMilliseconds;
    }

    private static void TryDispose(IDisposable? disposable)
    {
        try { disposable?.Dispose(); }
        catch { }
    }
}
