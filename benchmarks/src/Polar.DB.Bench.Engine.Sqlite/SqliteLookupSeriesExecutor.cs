using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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
///
/// Lookup timing is measured as one operation and split into nested parts:
/// - lookupMs: total key lookup operation time;
/// - lookupIndexSearchMs: key -> rowid search time;
/// - lookupMaterializationMs: rowid -> payload materialization and validation time.
/// </summary>
internal static class SqliteLookupSeriesExecutor
{
    private const string EngineKeyValue = "sqlite";

    public static Task<RunResult> ExecuteAsync(
        ExperimentSpec spec,
        RunWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        var options = LookupSeriesWorkload.Resolve(spec);
        var manifest = EnvironmentCollector.Collect(
            environmentClass: workspace.EnvironmentClass,
            repositoryRoot: workspace.RootDirectory);

        var runId = RunIdFactory.Create(
            spec.ExperimentKey,
            spec.Dataset.ProfileKey,
            EngineKeyValue,
            manifest.EnvironmentClass);

        var timestampUtc = DateTimeOffset.UtcNow;
        var artifactLayout = CreateArtifactLayout(workspace, runId);
        var metrics = new List<RunMetric>();
        var artifacts = new List<ArtifactDescriptor>();
        var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var notes = new List<string>();

        var technicalSuccess = true;
        string? technicalFailureReason = null;
        bool? semanticSuccess = null;
        string? semanticFailureReason = null;

        var loadMs = 0.0;
        var buildMs = 0.0;
        var reopenRefreshMs = 0.0;

        var directLookupMs = 0.0;
        var directLookupIndexSearchMs = 0.0;
        var directLookupMaterializationMs = 0.0;
        var directExpectedRows = 0;
        var directReturnedRows = 0;
        var directHit = false;

        var lookupMs = 0.0;
        var lookupIndexSearchMs = 0.0;
        var lookupMaterializationMs = 0.0;
        var lookupProbeHits = 0L;
        var lookupProbeMisses = 0L;
        var lookupReturnedRows = 0L;
        var lookupExpectedRows = 0L;
        var lookupReturnedOffsets = 0L;
        var lookupExpectedOffsets = 0L;

        var mismatchSamples = new List<string>();
        var rowCountAfterReopen = 0L;
        var managedBefore = GC.GetTotalMemory(forceFullCollection: false);
        var totalStopwatch = Stopwatch.StartNew();

        SqliteConnection? connection = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(artifactLayout.ArtifactsRootDirectory);

            connection = OpenConnection(artifactLayout.DatabasePath);
            CreateSchema(connection, options);

            var loadWatch = Stopwatch.StartNew();
            BulkLoad(connection, spec, options, cancellationToken);
            loadWatch.Stop();
            loadMs = loadWatch.Elapsed.TotalMilliseconds;

            cancellationToken.ThrowIfCancellationRequested();
            var buildWatch = Stopwatch.StartNew();
            ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS ix_records_lookup_key ON records(lookup_key);");
            buildWatch.Stop();
            buildMs = buildWatch.Elapsed.TotalMilliseconds;

            if (options.ReopenAfterBuild)
            {
                connection.Dispose();
                connection = null;

                cancellationToken.ThrowIfCancellationRequested();
                var reopenWatch = Stopwatch.StartNew();
                connection = OpenConnection(artifactLayout.DatabasePath);
                rowCountAfterReopen = CountRows(connection);
                reopenWatch.Stop();
                reopenRefreshMs = reopenWatch.Elapsed.TotalMilliseconds;
            }
            else
            {
                rowCountAfterReopen = CountRows(connection);
            }

            if (IsWarmEnabled(spec.Workload.Parameters))
            {
                WarmDirectory(artifactLayout.ArtifactsRootDirectory, cancellationToken: cancellationToken);
            }

            var directProbe = LookupSeriesWorkload.CreateFirstProbe(
                options.KeyKind,
                options.Mode,
                spec.Dataset.Seed ?? 1,
                spec.Dataset.RecordCount,
                options.DuplicateGroupSize);

            var directResult = ExecuteLookupProbe(connection!, options, directProbe);
            directLookupMs = directResult.TotalMs;
            directLookupIndexSearchMs = directResult.IndexSearchMs;
            directLookupMaterializationMs = directResult.MaterializationMs;
            directExpectedRows = directProbe.ExpectedCount;
            directReturnedRows = directResult.ReturnedRows;
            directHit = directResult.Matched;
            if (!directHit && !string.IsNullOrWhiteSpace(directResult.MismatchReason))
            {
                mismatchSamples.Add("direct lookup " + directResult.MismatchReason);
            }

            var probes = CreateProbes(spec, options);
            for (var i = 0; i < probes.Length; i++)
            {
                if ((i & 0x3FF) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var probe = probes[i];
                var result = ExecuteLookupProbe(connection!, options, probe);

                lookupMs += result.TotalMs;
                lookupIndexSearchMs += result.IndexSearchMs;
                lookupMaterializationMs += result.MaterializationMs;
                lookupReturnedRows += result.ReturnedRows;
                lookupExpectedRows += probe.ExpectedCount;
                lookupReturnedOffsets += result.ReturnedOffsets;
                lookupExpectedOffsets += probe.ExpectedCount;

                if (result.Matched)
                {
                    lookupProbeHits++;
                }
                else
                {
                    lookupProbeMisses++;
                    if (mismatchSamples.Count < 10 && !string.IsNullOrWhiteSpace(result.MismatchReason))
                    {
                        mismatchSamples.Add("lookup " + result.MismatchReason);
                    }
                }
            }

            connection.Dispose();
            connection = null;

            semanticSuccess = directHit &&
                              lookupProbeMisses == 0 &&
                              rowCountAfterReopen == spec.Dataset.RecordCount;
            if (!semanticSuccess.Value)
            {
                semanticFailureReason = BuildSemanticFailureReason(
                    spec.Dataset.RecordCount,
                    rowCountAfterReopen,
                    options.LookupCount,
                    lookupProbeHits,
                    lookupProbeMisses,
                    lookupExpectedRows,
                    lookupReturnedRows,
                    mismatchSamples);
            }
        }
        catch (Exception ex)
        {
            technicalSuccess = false;
            technicalFailureReason = ex.ToString();
        }
        finally
        {
            try
            {
                connection?.Dispose();
            }
            catch
            {
                // Cleanup path must not hide the original benchmark failure.
            }

            totalStopwatch.Stop();
        }

        var managedAfter = GC.GetTotalMemory(forceFullCollection: false);
        var gcInfo = GC.GetGCMemoryInfo();
        var processPeakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64;
        var collected = CollectArtifacts(artifactLayout, workspace.WorkingDirectory);
        artifacts.AddRange(collected.Descriptors);

        metrics.Add(new RunMetric { MetricKey = "elapsedMsTotal", Value = totalStopwatch.Elapsed.TotalMilliseconds });
        metrics.Add(new RunMetric { MetricKey = "elapsedMsSingleRun", Value = totalStopwatch.Elapsed.TotalMilliseconds });
        metrics.Add(new RunMetric { MetricKey = "loadMs", Value = loadMs });
        metrics.Add(new RunMetric { MetricKey = "buildMs", Value = buildMs });
        metrics.Add(new RunMetric { MetricKey = "reopenRefreshMs", Value = reopenRefreshMs });

        metrics.Add(new RunMetric { MetricKey = "directLookupMs", Value = directLookupMs });
        metrics.Add(new RunMetric { MetricKey = "directLookupIndexSearchMs", Value = directLookupIndexSearchMs });
        metrics.Add(new RunMetric { MetricKey = "directLookupMaterializationMs", Value = directLookupMaterializationMs });
        metrics.Add(new RunMetric { MetricKey = "directLookupHit", Value = directHit ? 1 : 0 });
        metrics.Add(new RunMetric { MetricKey = "directLookupExpectedRows", Value = directExpectedRows });
        metrics.Add(new RunMetric { MetricKey = "directLookupReturnedRows", Value = directReturnedRows });

        metrics.Add(new RunMetric { MetricKey = "lookupMs", Value = lookupMs });
        metrics.Add(new RunMetric { MetricKey = "lookupIndexSearchMs", Value = lookupIndexSearchMs });
        metrics.Add(new RunMetric { MetricKey = "lookupMaterializationMs", Value = lookupMaterializationMs });
        metrics.Add(new RunMetric { MetricKey = "lookupProbeCount", Value = options.LookupCount });
        metrics.Add(new RunMetric { MetricKey = "lookupProbeHits", Value = lookupProbeHits });
        metrics.Add(new RunMetric { MetricKey = "lookupProbeMisses", Value = lookupProbeMisses });
        metrics.Add(new RunMetric { MetricKey = "lookupReturnedRows", Value = lookupReturnedRows });
        metrics.Add(new RunMetric { MetricKey = "lookupExpectedRows", Value = lookupExpectedRows });
        metrics.Add(new RunMetric { MetricKey = "lookupReturnedOffsets", Value = lookupReturnedOffsets });
        metrics.Add(new RunMetric { MetricKey = "lookupExpectedOffsets", Value = lookupExpectedOffsets });
        metrics.Add(new RunMetric { MetricKey = "lookupMsPerQuery", Value = options.LookupCount > 0 ? lookupMs / options.LookupCount : 0 });
        metrics.Add(new RunMetric { MetricKey = "lookupQueriesPerSecond", Value = lookupMs > 0 ? options.LookupCount / (lookupMs / 1000.0) : 0 });

        // Compatibility aliases for existing analysis/charts and older vocabulary.
        metrics.Add(new RunMetric { MetricKey = "directPointLookupMs", Value = directLookupMs });
        metrics.Add(new RunMetric { MetricKey = "directPointLookupHit", Value = directHit ? 1 : 0 });
        metrics.Add(new RunMetric { MetricKey = "lookupSeriesMs", Value = lookupMs });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupMs", Value = lookupMs });
        metrics.Add(new RunMetric { MetricKey = "lookupCount", Value = options.LookupCount });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupCount", Value = options.LookupCount });
        metrics.Add(new RunMetric { MetricKey = "lookupHitCount", Value = lookupProbeHits });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupHits", Value = lookupProbeHits });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupMisses", Value = lookupProbeMisses });
        metrics.Add(new RunMetric { MetricKey = "lookupReturnedRowCount", Value = lookupReturnedRows });
        metrics.Add(new RunMetric { MetricKey = "indexOnlyLookupMs", Value = lookupIndexSearchMs });
        metrics.Add(new RunMetric { MetricKey = "materializedLookupMs", Value = lookupMs });
        metrics.Add(new RunMetric { MetricKey = "materializationOnlyLookupMs", Value = lookupMaterializationMs });

        metrics.Add(new RunMetric { MetricKey = "duplicateGroupSize", Value = options.DuplicateGroupSize });
        metrics.Add(new RunMetric { MetricKey = "totalArtifactBytes", Value = collected.TotalBytes });
        metrics.Add(new RunMetric { MetricKey = "primaryDatabaseBytes", Value = collected.PrimaryDatabaseBytes });
        metrics.Add(new RunMetric { MetricKey = "walBytes", Value = collected.WalBytes });
        metrics.Add(new RunMetric { MetricKey = "sharedMemoryBytes", Value = collected.SharedMemoryBytes });
        metrics.Add(new RunMetric { MetricKey = "managedBytesBefore", Value = managedBefore });
        metrics.Add(new RunMetric { MetricKey = "managedBytesAfter", Value = managedAfter });
        metrics.Add(new RunMetric { MetricKey = "managedBytesDelta", Value = managedAfter - managedBefore });
        metrics.Add(new RunMetric { MetricKey = "heapSizeBytes", Value = gcInfo.HeapSizeBytes });
        metrics.Add(new RunMetric { MetricKey = "fragmentedBytes", Value = gcInfo.FragmentedBytes });
        metrics.Add(new RunMetric { MetricKey = "processPeakWorkingSetBytes", Value = processPeakWorkingSet });

        diagnostics["lookupMode"] = options.Mode.ToString();
        diagnostics["lookupKeyKind"] = options.KeyKind.ToString();
        diagnostics["lookupCount"] = options.LookupCount.ToString(CultureInfo.InvariantCulture);
        diagnostics["lookupMs"] = lookupMs.ToString(CultureInfo.InvariantCulture);
        diagnostics["lookupIndexSearchMs"] = lookupIndexSearchMs.ToString(CultureInfo.InvariantCulture);
        diagnostics["lookupMaterializationMs"] = lookupMaterializationMs.ToString(CultureInfo.InvariantCulture);
        diagnostics["lookupProbeHits"] = lookupProbeHits.ToString(CultureInfo.InvariantCulture);
        diagnostics["lookupProbeMisses"] = lookupProbeMisses.ToString(CultureInfo.InvariantCulture);
        diagnostics["lookupReturnedRows"] = lookupReturnedRows.ToString(CultureInfo.InvariantCulture);
        diagnostics["lookupExpectedRows"] = lookupExpectedRows.ToString(CultureInfo.InvariantCulture);
        diagnostics["duplicateGroupSize"] = options.DuplicateGroupSize.ToString(CultureInfo.InvariantCulture);
        diagnostics["reopenAfterBuild"] = options.ReopenAfterBuild.ToString();
        diagnostics["rowCountAfterReopen"] = rowCountAfterReopen.ToString(CultureInfo.InvariantCulture);
        diagnostics["dbBytes"] = collected.PrimaryDatabaseBytes.ToString(CultureInfo.InvariantCulture);
        diagnostics["walBytes"] = collected.WalBytes.ToString(CultureInfo.InvariantCulture);
        diagnostics["shmBytes"] = collected.SharedMemoryBytes.ToString(CultureInfo.InvariantCulture);
        diagnostics["totalArtifactBytes"] = collected.TotalBytes.ToString(CultureInfo.InvariantCulture);
        diagnostics["semanticSuccess"] = semanticSuccess?.ToString() ?? "not-evaluated";

        if (mismatchSamples.Count > 0)
        {
            diagnostics["lookupMismatchSamples"] = string.Join(" | ", mismatchSamples);
        }

        if (!string.IsNullOrWhiteSpace(semanticFailureReason))
        {
            diagnostics["semanticFailureReason"] = semanticFailureReason;
        }

        if (!technicalSuccess && !string.IsNullOrWhiteSpace(technicalFailureReason))
        {
            diagnostics["technicalFailureReason"] = technicalFailureReason;
        }

        notes.Add("Lookup-series run for SQLite adapter.");
        notes.Add("Lookup is measured as one total operation with nested index-search and materialization timings.");
        notes.Add(options.Mode == LookupSeriesMode.ExactOne
            ? "Exact-one: index search resolves one rowid; materialization reads and validates one payload row."
            : "All-matching: index search resolves rowid range/list; materialization reads and validates all matching payload rows.");

        return Task.FromResult(new RunResult
        {
            RunId = runId,
            TimestampUtc = timestampUtc,
            EngineKey = EngineKeyValue,
            ExperimentKey = spec.ExperimentKey,
            DatasetProfileKey = spec.Dataset.ProfileKey,
            FairnessProfileKey = spec.FairnessProfile?.FairnessProfileKey ?? "unspecified",
            Environment = manifest,
            TechnicalSuccess = technicalSuccess,
            TechnicalFailureReason = technicalFailureReason,
            SemanticSuccess = semanticSuccess,
            SemanticFailureReason = semanticFailureReason,
            Metrics = metrics,
            Artifacts = artifacts,
            EngineDiagnostics = diagnostics,
            Tags = new Dictionary<string, string>
            {
                ["research"] = spec.ResearchQuestionId ?? string.Empty,
                ["hypothesis"] = spec.HypothesisId ?? string.Empty,
                ["lookupMode"] = options.Mode.ToString(),
                ["lookupKeyKind"] = options.KeyKind.ToString(),
                ["lookupMeasurement"] = "total-with-search-and-materialization-breakdown"
            },
            Notes = notes
        });
    }

    private static LookupProbe[] CreateProbes(ExperimentSpec spec, LookupSeriesOptions options)
    {
        var random = new Random((spec.Dataset.Seed ?? 1) ^ LookupSeriesWorkload.CommonLookupSeedSalt);
        var probes = new LookupProbe[options.LookupCount];
        for (var i = 0; i < probes.Length; i++)
        {
            probes[i] = LookupSeriesWorkload.CreateRandomProbe(
                options.KeyKind,
                options.Mode,
                spec.Dataset.Seed ?? 1,
                spec.Dataset.RecordCount,
                options.DuplicateGroupSize,
                random);
        }

        return probes;
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? ".");

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        ExecuteNonQuery(connection, "PRAGMA journal_mode = WAL;");
        ExecuteNonQuery(connection, "PRAGMA synchronous = NORMAL;");
        ExecuteNonQuery(connection, "PRAGMA temp_store = MEMORY;");
        return connection;
    }

    private static void CreateSchema(SqliteConnection connection, LookupSeriesOptions options)
    {
        ExecuteNonQuery(connection, "DROP TABLE IF EXISTS records;");
        ExecuteNonQuery(connection,
            "CREATE TABLE records (" +
            "lookup_key " + ResolveLookupKeySqlType(options.KeyKind) + " NOT NULL, " +
            "ordinal INTEGER NOT NULL, " +
            "payload TEXT NOT NULL" +
            ");");
    }

    private static void BulkLoad(
        SqliteConnection connection,
        ExperimentSpec spec,
        LookupSeriesOptions options,
        CancellationToken cancellationToken)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO records (lookup_key, ordinal, payload) VALUES ($key, $ordinal, $payload);";

        var keyParameter = command.CreateParameter();
        keyParameter.ParameterName = "$key";
        command.Parameters.Add(keyParameter);

        var ordinalParameter = command.CreateParameter();
        ordinalParameter.ParameterName = "$ordinal";
        command.Parameters.Add(ordinalParameter);

        var payloadParameter = command.CreateParameter();
        payloadParameter.ParameterName = "$payload";
        command.Parameters.Add(payloadParameter);

        var seed = spec.Dataset.Seed ?? 1;
        var recordCount = checked((int)spec.Dataset.RecordCount);
        for (var ordinal = 1; ordinal <= recordCount; ordinal++)
        {
            if ((ordinal & 0x3FFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var key = LookupSeriesWorkload.CreateKey(
                options.KeyKind,
                options.Mode,
                seed,
                ordinal,
                options.DuplicateGroupSize);

            keyParameter.Value = ConvertKeyForSqlite(key, options.KeyKind);
            ordinalParameter.Value = ordinal;
            payloadParameter.Value = "payload-" + ordinal.ToString(CultureInfo.InvariantCulture);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static LookupProbeResult ExecuteLookupProbe(
        SqliteConnection connection,
        LookupSeriesOptions options,
        LookupProbe probe)
    {
        var totalWatch = Stopwatch.StartNew();

        var indexWatch = Stopwatch.StartNew();
        var rowIds = SelectRowIdsByKey(connection, options, probe.Key);
        indexWatch.Stop();

        var materializationWatch = Stopwatch.StartNew();
        var materialized = MaterializeAndValidateRows(connection, options, probe.Key, rowIds);
        materializationWatch.Stop();

        totalWatch.Stop();

        if (rowIds.Count != probe.ExpectedCount)
        {
            return new LookupProbeResult(
                totalWatch.Elapsed.TotalMilliseconds,
                indexWatch.Elapsed.TotalMilliseconds,
                materializationWatch.Elapsed.TotalMilliseconds,
                rowIds.Count,
                materialized.ReturnedRows,
                false,
                $"rowid count mismatch for key={probe.Key}: returned={rowIds.Count}, expected={probe.ExpectedCount}");
        }

        if (materialized.ReturnedRows != probe.ExpectedCount)
        {
            return new LookupProbeResult(
                totalWatch.Elapsed.TotalMilliseconds,
                indexWatch.Elapsed.TotalMilliseconds,
                materializationWatch.Elapsed.TotalMilliseconds,
                rowIds.Count,
                materialized.ReturnedRows,
                false,
                $"materialized row count mismatch for key={probe.Key}: returned={materialized.ReturnedRows}, expected={probe.ExpectedCount}");
        }

        if (materialized.WrongRows != 0)
        {
            return new LookupProbeResult(
                totalWatch.Elapsed.TotalMilliseconds,
                indexWatch.Elapsed.TotalMilliseconds,
                materializationWatch.Elapsed.TotalMilliseconds,
                rowIds.Count,
                materialized.ReturnedRows,
                false,
                $"materialized rows with wrong key for key={probe.Key}: wrongRows={materialized.WrongRows}");
        }

        return new LookupProbeResult(
            totalWatch.Elapsed.TotalMilliseconds,
            indexWatch.Elapsed.TotalMilliseconds,
            materializationWatch.Elapsed.TotalMilliseconds,
            rowIds.Count,
            materialized.ReturnedRows,
            true,
            null);
    }

    private static List<long> SelectRowIdsByKey(
        SqliteConnection connection,
        LookupSeriesOptions options,
        IComparable key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT rowid FROM records WHERE lookup_key = $key ORDER BY rowid;";
        command.Parameters.AddWithValue("$key", ConvertKeyForSqlite(key, options.KeyKind));

        using var reader = command.ExecuteReader();
        var rowIds = new List<long>();
        while (reader.Read())
        {
            rowIds.Add(reader.GetInt64(0));
        }

        return rowIds;
    }

    private static MaterializedRowsResult MaterializeAndValidateRows(
        SqliteConnection connection,
        LookupSeriesOptions options,
        IComparable expectedKey,
        IReadOnlyList<long> rowIds)
    {
        if (rowIds.Count == 0)
        {
            return new MaterializedRowsResult(0, 0);
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT lookup_key, ordinal, payload FROM records WHERE rowid = $rowid;";
        var rowIdParameter = command.CreateParameter();
        rowIdParameter.ParameterName = "$rowid";
        command.Parameters.Add(rowIdParameter);

        var returnedRows = 0;
        var wrongRows = 0;

        foreach (var rowId in rowIds)
        {
            rowIdParameter.Value = rowId;
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                wrongRows++;
                continue;
            }

            returnedRows++;
            var actualKey = ReadKey(reader, options.KeyKind);
            if (actualKey.CompareTo(expectedKey) != 0)
            {
                wrongRows++;
            }
        }

        return new MaterializedRowsResult(returnedRows, wrongRows);
    }

    private static long CountRows(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM records;";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static IComparable ReadKey(SqliteDataReader reader, LookupKeyKind keyKind)
    {
        return keyKind switch
        {
            LookupKeyKind.Int32 => checked((int)reader.GetInt64(0)),
            LookupKeyKind.Int64 => reader.GetInt64(0),
            LookupKeyKind.Guid => Guid.Parse(reader.GetString(0)),
            _ => throw new ArgumentOutOfRangeException(nameof(keyKind))
        };
    }

    private static object ConvertKeyForSqlite(IComparable key, LookupKeyKind keyKind)
    {
        return keyKind switch
        {
            LookupKeyKind.Int32 => Convert.ToInt32(key, CultureInfo.InvariantCulture),
            LookupKeyKind.Int64 => Convert.ToInt64(key, CultureInfo.InvariantCulture),
            LookupKeyKind.Guid => key switch
            {
                Guid guid => guid.ToString("D"),
                string text => Guid.Parse(text).ToString("D"),
                _ => Guid.Parse(Convert.ToString(key, CultureInfo.InvariantCulture)!).ToString("D")
            },
            _ => throw new ArgumentOutOfRangeException(nameof(keyKind))
        };
    }

    private static string ResolveLookupKeySqlType(LookupKeyKind keyKind)
    {
        return keyKind == LookupKeyKind.Guid ? "TEXT" : "INTEGER";
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string BuildSemanticFailureReason(
        long expectedCount,
        long actualCount,
        int lookupCount,
        long lookupHits,
        long lookupMisses,
        long lookupExpectedRows,
        long lookupReturnedRows,
        IReadOnlyList<string> mismatchSamples)
    {
        return "SQLite lookup-series semantic check failed: " +
               $"expectedCount={expectedCount}, actualCount={actualCount}, lookupCount={lookupCount}, " +
               $"lookupHits={lookupHits}, lookupMisses={lookupMisses}, " +
               $"lookupExpectedRows={lookupExpectedRows}, lookupReturnedRows={lookupReturnedRows}. " +
               (mismatchSamples.Count == 0 ? string.Empty : "Samples: " + string.Join(" | ", mismatchSamples));
    }

    private static SqliteLookupArtifactLayout CreateArtifactLayout(RunWorkspace workspace, string runId)
    {
        var root = Path.Combine(workspace.ArtifactsDirectory ?? workspace.WorkingDirectory, runId, "sqlite-lookup-series");
        return new SqliteLookupArtifactLayout(
            root,
            Path.Combine(root, "lookup.sqlite"));
    }

    private static SqliteLookupArtifactInventory CollectArtifacts(SqliteLookupArtifactLayout layout, string relativeRoot)
    {
        var descriptors = new List<ArtifactDescriptor>();
        var total = 0L;
        var primaryDatabase = 0L;
        var wal = 0L;
        var sharedMemory = 0L;

        if (!Directory.Exists(layout.ArtifactsRootDirectory))
        {
            return new SqliteLookupArtifactInventory(descriptors, 0, 0, 0, 0);
        }

        foreach (var file in Directory.EnumerateFiles(layout.ArtifactsRootDirectory, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            var role = ResolveRole(file);
            var relative = Path.GetRelativePath(relativeRoot, info.FullName);
            descriptors.Add(new ArtifactDescriptor(role, relative, info.Length));
            total += info.Length;
            if (role == ArtifactRole.PrimaryDatabase) primaryDatabase += info.Length;
            if (role == ArtifactRole.Wal) wal += info.Length;
            if (role == ArtifactRole.SharedMemory) sharedMemory += info.Length;
        }

        return new SqliteLookupArtifactInventory(descriptors, total, primaryDatabase, wal, sharedMemory);
    }

    private static ArtifactRole ResolveRole(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        if (name.EndsWith("-wal", StringComparison.OrdinalIgnoreCase)) return ArtifactRole.Wal;
        if (name.EndsWith("-shm", StringComparison.OrdinalIgnoreCase)) return ArtifactRole.SharedMemory;
        if (name.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) return ArtifactRole.PrimaryDatabase;
        return ArtifactRole.Unknown;
    }

    private sealed record LookupProbeResult(
        double TotalMs,
        double IndexSearchMs,
        double MaterializationMs,
        int ReturnedOffsets,
        int ReturnedRows,
        bool Matched,
        string? MismatchReason);

    private sealed record MaterializedRowsResult(int ReturnedRows, int WrongRows);

    private sealed record SqliteLookupArtifactLayout(
        string ArtifactsRootDirectory,
        string DatabasePath);

    private sealed record SqliteLookupArtifactInventory(
        IReadOnlyList<ArtifactDescriptor> Descriptors,
        long TotalBytes,
        long PrimaryDatabaseBytes,
        long WalBytes,
        long SharedMemoryBytes);
}
