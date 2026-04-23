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
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;

namespace Polar.DB.Bench.Engine.Sqlite;

public sealed class SqliteStorageEngineAdapter : IStorageEngineAdapter
{
    public string EngineKey => "sqlite";

    public IReadOnlyCollection<EngineCapability> Capabilities { get; } = new[]
    {
        EngineCapability.BulkLoad,
        EngineCapability.PointLookup,
        EngineCapability.ReopenRecovery,
        EngineCapability.PhysicalArtifactInspection
    };

    public IEngineRun CreateRun(ExperimentSpec spec, RunWorkspace workspace)
    {
        return new SqliteEngineRun(spec, workspace);
    }

    private sealed class SqliteEngineRun : IEngineRun
    {
        private const string EngineKeyValue = "sqlite";
        private const string LoadBuildExperimentKey = "persons-load-build-reopen-random-lookup";
        private const string LoadBuildWorkloadKey = "bulk-load-point-lookup";
        private const string AppendCyclesExperimentKey = "persons-append-cycles-reopen-lookup";
        private const string AppendCyclesWorkloadKey = "append-cycles-reopen-lookup";
        private const string DurabilityBalancedProfileKey = "durability-balanced";
        private const int CommonLookupSeedSalt = unchecked((int)0x1f2e3d4c);
        private const string TableName = "person";
        private const string IdIndexName = "idx_person_id";
        private readonly ExperimentSpec _spec;
        private readonly RunWorkspace _workspace;

        public SqliteEngineRun(ExperimentSpec spec, RunWorkspace workspace)
        {
            _spec = spec;
            _workspace = workspace;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<RunResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            ValidateSpec(_spec);
            if (_spec.ExperimentKey.Equals(AppendCyclesExperimentKey, StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteAppendCyclesAsync(cancellationToken);
            }

            var manifest = EnvironmentCollector.Collect(
                environmentClass: _workspace.EnvironmentClass,
                repositoryRoot: _workspace.RootDirectory);

            var runId = RunIdFactory.Create(_spec.ExperimentKey, _spec.Dataset.ProfileKey, EngineKeyValue, manifest.EnvironmentClass);
            var timestampUtc = DateTimeOffset.UtcNow;
            var fairness = ResolveFairness(_spec.FairnessProfile?.FairnessProfileKey);

            var metrics = new List<RunMetric>();
            var notes = new List<string>();
            var artifacts = new List<ArtifactDescriptor>();
            var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var loadMs = 0.0;
            var buildMs = 0.0;
            var reopenMs = 0.0;
            var directLookupMs = 0.0;
            var lookupMs = 0.0;
            var directLookupKey = ResolveDirectLookupKey(_spec.Dataset.RecordCount);
            var directLookupHit = false;
            var lookupHits = 0L;
            var lookupCount = ResolveLookupCount(_spec.Workload);
            var managedBefore = GC.GetTotalMemory(forceFullCollection: false);
            var totalStopwatch = Stopwatch.StartNew();
            var artifactLayout = CreateArtifactLayout(_workspace, runId);
            var technicalSuccess = true;
            string? technicalFailureReason = null;
            bool? semanticSuccess = null;
            string? semanticFailureReason = null;
            var rowCountAfterReopen = 0L;
            var indexPresentAfterReopen = false;
            var journalMode = string.Empty;
            var synchronous = string.Empty;
            var tempStore = string.Empty;

            SqliteConnection? connection = null;
            SqliteConnection? reopened = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(artifactLayout.ArtifactsRootDirectory);

                var loadWatch = Stopwatch.StartNew();
                connection = CreateConnection(artifactLayout.PrimaryDatabasePath);
                connection.Open();
                ApplyFairness(connection, fairness, out journalMode, out synchronous, out tempStore);
                CreateSchema(connection);
                BulkInsertPersons(connection, _spec.Dataset.RecordCount, _spec.Dataset.Seed ?? 1, cancellationToken);
                loadWatch.Stop();
                loadMs = loadWatch.Elapsed.TotalMilliseconds;

                cancellationToken.ThrowIfCancellationRequested();

                var buildWatch = Stopwatch.StartNew();
                BuildLookupIndex(connection);
                buildWatch.Stop();
                buildMs = buildWatch.Elapsed.TotalMilliseconds;

                connection.Dispose();
                connection = null;

                cancellationToken.ThrowIfCancellationRequested();

                var reopenWatch = Stopwatch.StartNew();
                reopened = CreateConnection(artifactLayout.PrimaryDatabasePath);
                reopened.Open();
                reopenWatch.Stop();
                reopenMs = reopenWatch.Elapsed.TotalMilliseconds;

                rowCountAfterReopen = ReadRowCount(reopened);
                indexPresentAfterReopen = HasIndex(reopened, IdIndexName);

                cancellationToken.ThrowIfCancellationRequested();

                var directLookupWatch = Stopwatch.StartNew();
                directLookupHit = HasPersonById(reopened, directLookupKey);
                directLookupWatch.Stop();
                directLookupMs = directLookupWatch.Elapsed.TotalMilliseconds;

                cancellationToken.ThrowIfCancellationRequested();

                var random = new Random((_spec.Dataset.Seed ?? 1) ^ CommonLookupSeedSalt);
                var maxKeyExclusive = checked((int)_spec.Dataset.RecordCount) + 1;
                var lookupWatch = Stopwatch.StartNew();
                using var lookup = reopened.CreateCommand();
                lookup.CommandText = $"SELECT id FROM {TableName} WHERE id = $id LIMIT 1;";
                var parameter = lookup.CreateParameter();
                parameter.ParameterName = "$id";
                lookup.Parameters.Add(parameter);

                for (var i = 0; i < lookupCount; i++)
                {
                    if ((i & 0x3FF) == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    var key = random.Next(1, maxKeyExclusive);
                    parameter.Value = key;
                    var value = lookup.ExecuteScalar();
                    if (TryReadInt(value, out var rowKey) && rowKey == key)
                    {
                        lookupHits++;
                    }
                }

                lookupWatch.Stop();
                lookupMs = lookupWatch.Elapsed.TotalMilliseconds;

                reopened.Dispose();
                reopened = null;

                semanticSuccess = directLookupHit &&
                                  lookupHits == lookupCount &&
                                  rowCountAfterReopen == _spec.Dataset.RecordCount &&
                                  indexPresentAfterReopen;

                if (!semanticSuccess.Value)
                {
                    semanticFailureReason = BuildSemanticFailureReason(
                        directLookupKey,
                        directLookupHit,
                        _spec.Dataset.RecordCount,
                        rowCountAfterReopen,
                        lookupCount,
                        lookupHits,
                        indexPresentAfterReopen);
                }
            }
            catch (Exception ex)
            {
                technicalSuccess = false;
                technicalFailureReason = ex.ToString();
            }
            finally
            {
                TryDispose(connection);
                TryDispose(reopened);
                totalStopwatch.Stop();
            }

            var managedAfter = GC.GetTotalMemory(forceFullCollection: false);
            var gcInfo = GC.GetGCMemoryInfo();
            var processPeakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64;

            var collected = CollectArtifacts(artifactLayout, _workspace.WorkingDirectory);
            artifacts.AddRange(collected.Descriptors);

            var sideArtifactBytes = Math.Max(0L, collected.TotalBytes - collected.DatabaseBytes);

            metrics.Add(new RunMetric { MetricKey = "elapsedMsTotal", Value = totalStopwatch.Elapsed.TotalMilliseconds });
            metrics.Add(new RunMetric { MetricKey = "elapsedMsSingleRun", Value = totalStopwatch.Elapsed.TotalMilliseconds });
            metrics.Add(new RunMetric { MetricKey = "loadMs", Value = loadMs });
            metrics.Add(new RunMetric { MetricKey = "buildMs", Value = buildMs });
            metrics.Add(new RunMetric { MetricKey = "reopenRefreshMs", Value = reopenMs });
            metrics.Add(new RunMetric { MetricKey = "directPointLookupMs", Value = directLookupMs });
            metrics.Add(new RunMetric { MetricKey = "directPointLookupKey", Value = directLookupKey });
            metrics.Add(new RunMetric { MetricKey = "directPointLookupHit", Value = directLookupHit ? 1 : 0 });
            metrics.Add(new RunMetric { MetricKey = "randomPointLookupMs", Value = lookupMs });
            metrics.Add(new RunMetric { MetricKey = "randomPointLookupCount", Value = lookupCount });
            metrics.Add(new RunMetric { MetricKey = "randomPointLookupHits", Value = lookupHits });
            metrics.Add(new RunMetric { MetricKey = "randomPointLookupMisses", Value = Math.Max(0L, lookupCount - lookupHits) });
            metrics.Add(new RunMetric { MetricKey = "totalArtifactBytes", Value = collected.TotalBytes });
            metrics.Add(new RunMetric { MetricKey = "primaryDataBytes", Value = collected.DatabaseBytes });
            metrics.Add(new RunMetric { MetricKey = "primaryDatabaseBytes", Value = collected.DatabaseBytes });
            metrics.Add(new RunMetric { MetricKey = "sideArtifactBytes", Value = sideArtifactBytes });
            metrics.Add(new RunMetric { MetricKey = "walBytes", Value = collected.WalBytes });
            metrics.Add(new RunMetric { MetricKey = "shmBytes", Value = collected.ShmBytes });
            metrics.Add(new RunMetric { MetricKey = "journalBytes", Value = collected.JournalBytes });
            metrics.Add(new RunMetric { MetricKey = "temporaryBytes", Value = collected.TemporaryBytes });
            metrics.Add(new RunMetric { MetricKey = "managedBytesBefore", Value = managedBefore });
            metrics.Add(new RunMetric { MetricKey = "managedBytesAfter", Value = managedAfter });
            metrics.Add(new RunMetric { MetricKey = "managedBytesDelta", Value = managedAfter - managedBefore });
            metrics.Add(new RunMetric { MetricKey = "heapSizeBytes", Value = gcInfo.HeapSizeBytes });
            metrics.Add(new RunMetric { MetricKey = "fragmentedBytes", Value = gcInfo.FragmentedBytes });
            metrics.Add(new RunMetric { MetricKey = "processPeakWorkingSetBytes", Value = processPeakWorkingSet });

            diagnostics["fairnessProfileApplied"] = fairness.FairnessProfileKey;
            diagnostics["sqliteJournalMode"] = journalMode;
            diagnostics["sqliteSynchronous"] = synchronous;
            diagnostics["sqliteTempStore"] = tempStore;
            diagnostics["dbBytes"] = ToInvariant(collected.DatabaseBytes);
            diagnostics["walBytes"] = ToInvariant(collected.WalBytes);
            diagnostics["shmBytes"] = ToInvariant(collected.ShmBytes);
            diagnostics["journalBytes"] = ToInvariant(collected.JournalBytes);
            diagnostics["temporaryBytes"] = ToInvariant(collected.TemporaryBytes);
            diagnostics["totalArtifactBytes"] = ToInvariant(collected.TotalBytes);
            diagnostics["directLookupKey"] = directLookupKey.ToString(CultureInfo.InvariantCulture);
            diagnostics["directLookupHit"] = directLookupHit.ToString();
            diagnostics["directLookupMs"] = directLookupMs.ToString(CultureInfo.InvariantCulture);
            diagnostics["lookupCount"] = lookupCount.ToString(CultureInfo.InvariantCulture);
            diagnostics["lookupHitCount"] = lookupHits.ToString(CultureInfo.InvariantCulture);
            diagnostics["rowCountAfterReopen"] = rowCountAfterReopen.ToString(CultureInfo.InvariantCulture);
            diagnostics["indexPresentAfterReopen"] = indexPresentAfterReopen.ToString();
            diagnostics["semanticSuccess"] = semanticSuccess?.ToString() ?? "not-evaluated";

            if (!string.IsNullOrWhiteSpace(semanticFailureReason))
            {
                diagnostics["semanticFailureReason"] = semanticFailureReason;
            }

            if (!technicalSuccess && !string.IsNullOrWhiteSpace(technicalFailureReason))
            {
                diagnostics["technicalFailureReason"] = technicalFailureReason;
            }

            notes.Add("Stage4 real SQLite adapter run.");
            notes.Add("Imported reference workload: load in reverse id order -> build index -> reopen -> direct key lookup -> random point lookup batch.");
            notes.Add($"Reference-normalized lookup batch size: {lookupCount}.");
            notes.Add($"Fairness profile mapping: {fairness.FairnessProfileKey} -> journal_mode={fairness.JournalMode}, synchronous={fairness.Synchronous}, temp_store={fairness.TempStore}.");

            return Task.FromResult(new RunResult
            {
                RunId = runId,
                TimestampUtc = timestampUtc,
                EngineKey = EngineKeyValue,
                ExperimentKey = _spec.ExperimentKey,
                DatasetProfileKey = _spec.Dataset.ProfileKey,
                FairnessProfileKey = fairness.FairnessProfileKey,
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
                    ["research"] = _spec.ResearchQuestionId ?? string.Empty,
                    ["hypothesis"] = _spec.HypothesisId ?? string.Empty
                },
                Notes = notes
            });
        }

        private Task<RunResult> ExecuteAppendCyclesAsync(CancellationToken cancellationToken)
        {
            var manifest = EnvironmentCollector.Collect(
                environmentClass: _workspace.EnvironmentClass,
                repositoryRoot: _workspace.RootDirectory);

            var runId = RunIdFactory.Create(_spec.ExperimentKey, _spec.Dataset.ProfileKey, EngineKeyValue, manifest.EnvironmentClass);
            var timestampUtc = DateTimeOffset.UtcNow;
            var fairness = ResolveFairness(_spec.FairnessProfile?.FairnessProfileKey);

            var metrics = new List<RunMetric>();
            var notes = new List<string>();
            var artifacts = new List<ArtifactDescriptor>();
            var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var loadMs = 0.0;
            var appendMs = 0.0;
            var buildMs = 0.0;
            var reopenMs = 0.0;
            var lookupMs = 0.0;
            var lookupHits = 0L;
            var lookupAttempts = 0L;
            var lookupCountPerCycle = ResolveLookupCount(_spec.Workload);
            var appendCycleShape = ResolveAppendCycleShape(_spec.Workload);
            var managedBefore = GC.GetTotalMemory(forceFullCollection: false);
            var totalStopwatch = Stopwatch.StartNew();
            var artifactLayout = CreateArtifactLayout(_workspace, runId);
            var technicalSuccess = true;
            string? technicalFailureReason = null;
            bool? semanticSuccess = null;
            string? semanticFailureReason = null;
            var rowCountMismatches = new List<string>();
            var missingIndexCycles = new List<int>();
            var cycleArtifactBytes = new List<long>();
            var initialArtifactBytes = 0L;
            var expectedRowCount = _spec.Dataset.RecordCount;
            var journalMode = string.Empty;
            var synchronous = string.Empty;
            var tempStore = string.Empty;

            SqliteConnection? connection = null;
            SqliteConnection? reopened = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(artifactLayout.ArtifactsRootDirectory);

                var initialLoadWatch = Stopwatch.StartNew();
                connection = CreateConnection(artifactLayout.PrimaryDatabasePath);
                connection.Open();
                ApplyFairness(connection, fairness, out journalMode, out synchronous, out tempStore);
                CreateSchema(connection);
                BulkInsertPersons(connection, _spec.Dataset.RecordCount, _spec.Dataset.Seed ?? 1, cancellationToken);
                initialLoadWatch.Stop();
                loadMs += initialLoadWatch.Elapsed.TotalMilliseconds;

                cancellationToken.ThrowIfCancellationRequested();

                var initialBuildWatch = Stopwatch.StartNew();
                BuildLookupIndex(connection);
                initialBuildWatch.Stop();
                buildMs = initialBuildWatch.Elapsed.TotalMilliseconds;

                connection.Dispose();
                connection = null;

                var initialCollected = CollectArtifacts(artifactLayout, _workspace.WorkingDirectory);
                initialArtifactBytes = initialCollected.TotalBytes;
                cycleArtifactBytes.Add(initialCollected.TotalBytes);

                for (var cycle = 0; cycle < appendCycleShape.BatchCount; cycle++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    connection = CreateConnection(artifactLayout.PrimaryDatabasePath);
                    connection.Open();
                    ApplyFairness(connection, fairness, out _, out _, out _);

                    var appendWatch = Stopwatch.StartNew();
                    var firstId = checked((int)(expectedRowCount + 1));
                    InsertBatchPersons(
                        connection,
                        firstId,
                        appendCycleShape.BatchSize,
                        (_spec.Dataset.Seed ?? 1) ^ (0x33cc0000 + cycle),
                        cancellationToken);
                    appendWatch.Stop();
                    appendMs += appendWatch.Elapsed.TotalMilliseconds;
                    loadMs += appendWatch.Elapsed.TotalMilliseconds;
                    expectedRowCount += appendCycleShape.BatchSize;

                    connection.Dispose();
                    connection = null;

                    var reopenWatch = Stopwatch.StartNew();
                    reopened = CreateConnection(artifactLayout.PrimaryDatabasePath);
                    reopened.Open();
                    reopenWatch.Stop();
                    reopenMs += reopenWatch.Elapsed.TotalMilliseconds;

                    var actualCount = ReadRowCount(reopened);
                    if (actualCount != expectedRowCount)
                    {
                        rowCountMismatches.Add($"cycle{cycle + 1}:count={actualCount},expected={expectedRowCount}");
                    }

                    if (!HasIndex(reopened, IdIndexName))
                    {
                        missingIndexCycles.Add(cycle + 1);
                    }

                    var random = new Random((_spec.Dataset.Seed ?? 1) ^ (0x7d8e0000 + cycle));
                    var maxKeyExclusive = checked((int)expectedRowCount) + 1;
                    var lookupWatch = Stopwatch.StartNew();
                    using var lookup = reopened.CreateCommand();
                    lookup.CommandText = $"SELECT id FROM {TableName} WHERE id = $id LIMIT 1;";
                    var parameter = lookup.CreateParameter();
                    parameter.ParameterName = "$id";
                    lookup.Parameters.Add(parameter);

                    for (var i = 0; i < lookupCountPerCycle; i++)
                    {
                        if ((i & 0x3FF) == 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        var key = random.Next(1, maxKeyExclusive);
                        parameter.Value = key;
                        var value = lookup.ExecuteScalar();
                        lookupAttempts++;
                        if (TryReadInt(value, out var rowKey) && rowKey == key)
                        {
                            lookupHits++;
                        }
                    }

                    lookupWatch.Stop();
                    lookupMs += lookupWatch.Elapsed.TotalMilliseconds;

                    reopened.Dispose();
                    reopened = null;

                    var cycleCollected = CollectArtifacts(artifactLayout, _workspace.WorkingDirectory);
                    cycleArtifactBytes.Add(cycleCollected.TotalBytes);
                }

                semanticSuccess = lookupHits == lookupAttempts &&
                                  rowCountMismatches.Count == 0 &&
                                  missingIndexCycles.Count == 0;
                if (!semanticSuccess.Value)
                {
                    semanticFailureReason = BuildAppendSemanticFailureReason(
                        expectedRowCount,
                        rowCountMismatches,
                        lookupAttempts,
                        lookupHits,
                        missingIndexCycles);
                }
            }
            catch (Exception ex)
            {
                technicalSuccess = false;
                technicalFailureReason = ex.ToString();
            }
            finally
            {
                TryDispose(connection);
                TryDispose(reopened);
                totalStopwatch.Stop();
            }

            var managedAfter = GC.GetTotalMemory(forceFullCollection: false);
            var gcInfo = GC.GetGCMemoryInfo();
            var processPeakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64;

            var collected = CollectArtifacts(artifactLayout, _workspace.WorkingDirectory);
            artifacts.AddRange(collected.Descriptors);
            var artifactGrowthBytes = Math.Max(0L, collected.TotalBytes - initialArtifactBytes);
            var sideArtifactBytes = Math.Max(0L, collected.TotalBytes - collected.DatabaseBytes);

            metrics.Add(new RunMetric { MetricKey = "elapsedMsTotal", Value = totalStopwatch.Elapsed.TotalMilliseconds });
            metrics.Add(new RunMetric { MetricKey = "elapsedMsSingleRun", Value = totalStopwatch.Elapsed.TotalMilliseconds });
            metrics.Add(new RunMetric { MetricKey = "loadMs", Value = loadMs });
            metrics.Add(new RunMetric { MetricKey = "appendMs", Value = appendMs });
            metrics.Add(new RunMetric { MetricKey = "buildMs", Value = buildMs });
            metrics.Add(new RunMetric { MetricKey = "reopenRefreshMs", Value = reopenMs });
            metrics.Add(new RunMetric { MetricKey = "randomPointLookupMs", Value = lookupMs });
            metrics.Add(new RunMetric { MetricKey = "randomPointLookupCount", Value = lookupAttempts });
            metrics.Add(new RunMetric { MetricKey = "randomPointLookupHits", Value = lookupHits });
            metrics.Add(new RunMetric { MetricKey = "randomPointLookupMisses", Value = Math.Max(0L, lookupAttempts - lookupHits) });
            metrics.Add(new RunMetric { MetricKey = "appendBatchCount", Value = appendCycleShape.BatchCount });
            metrics.Add(new RunMetric { MetricKey = "appendBatchSize", Value = appendCycleShape.BatchSize });
            metrics.Add(new RunMetric { MetricKey = "lookupCountPerCycle", Value = lookupCountPerCycle });
            metrics.Add(new RunMetric { MetricKey = "initialArtifactBytes", Value = initialArtifactBytes });
            metrics.Add(new RunMetric { MetricKey = "artifactGrowthBytes", Value = artifactGrowthBytes });
            metrics.Add(new RunMetric { MetricKey = "totalArtifactBytes", Value = collected.TotalBytes });
            metrics.Add(new RunMetric { MetricKey = "primaryDataBytes", Value = collected.DatabaseBytes });
            metrics.Add(new RunMetric { MetricKey = "primaryDatabaseBytes", Value = collected.DatabaseBytes });
            metrics.Add(new RunMetric { MetricKey = "sideArtifactBytes", Value = sideArtifactBytes });
            metrics.Add(new RunMetric { MetricKey = "walBytes", Value = collected.WalBytes });
            metrics.Add(new RunMetric { MetricKey = "shmBytes", Value = collected.ShmBytes });
            metrics.Add(new RunMetric { MetricKey = "journalBytes", Value = collected.JournalBytes });
            metrics.Add(new RunMetric { MetricKey = "temporaryBytes", Value = collected.TemporaryBytes });
            metrics.Add(new RunMetric { MetricKey = "managedBytesBefore", Value = managedBefore });
            metrics.Add(new RunMetric { MetricKey = "managedBytesAfter", Value = managedAfter });
            metrics.Add(new RunMetric { MetricKey = "managedBytesDelta", Value = managedAfter - managedBefore });
            metrics.Add(new RunMetric { MetricKey = "heapSizeBytes", Value = gcInfo.HeapSizeBytes });
            metrics.Add(new RunMetric { MetricKey = "fragmentedBytes", Value = gcInfo.FragmentedBytes });
            metrics.Add(new RunMetric { MetricKey = "processPeakWorkingSetBytes", Value = processPeakWorkingSet });

            diagnostics["fairnessProfileApplied"] = fairness.FairnessProfileKey;
            diagnostics["sqliteJournalMode"] = journalMode;
            diagnostics["sqliteSynchronous"] = synchronous;
            diagnostics["sqliteTempStore"] = tempStore;
            diagnostics["appendBatchCount"] = appendCycleShape.BatchCount.ToString(CultureInfo.InvariantCulture);
            diagnostics["appendBatchSize"] = appendCycleShape.BatchSize.ToString(CultureInfo.InvariantCulture);
            diagnostics["lookupCountPerCycle"] = lookupCountPerCycle.ToString(CultureInfo.InvariantCulture);
            diagnostics["lookupCount"] = lookupAttempts.ToString(CultureInfo.InvariantCulture);
            diagnostics["lookupHitCount"] = lookupHits.ToString(CultureInfo.InvariantCulture);
            diagnostics["expectedCountAfterCycles"] = expectedRowCount.ToString(CultureInfo.InvariantCulture);
            diagnostics["initialArtifactBytes"] = initialArtifactBytes.ToString(CultureInfo.InvariantCulture);
            diagnostics["artifactGrowthBytes"] = artifactGrowthBytes.ToString(CultureInfo.InvariantCulture);
            diagnostics["cycleArtifactBytes"] = string.Join(",", cycleArtifactBytes);
            diagnostics["dbBytes"] = ToInvariant(collected.DatabaseBytes);
            diagnostics["walBytes"] = ToInvariant(collected.WalBytes);
            diagnostics["shmBytes"] = ToInvariant(collected.ShmBytes);
            diagnostics["journalBytes"] = ToInvariant(collected.JournalBytes);
            diagnostics["temporaryBytes"] = ToInvariant(collected.TemporaryBytes);
            diagnostics["totalArtifactBytes"] = ToInvariant(collected.TotalBytes);
            diagnostics["semanticSuccess"] = semanticSuccess?.ToString() ?? "not-evaluated";

            if (rowCountMismatches.Count > 0)
            {
                diagnostics["rowCountMismatches"] = string.Join(" | ", rowCountMismatches);
            }

            if (missingIndexCycles.Count > 0)
            {
                diagnostics["missingIndexCycles"] = string.Join(",", missingIndexCycles);
            }

            if (!string.IsNullOrWhiteSpace(semanticFailureReason))
            {
                diagnostics["semanticFailureReason"] = semanticFailureReason;
            }

            if (!technicalSuccess && !string.IsNullOrWhiteSpace(technicalFailureReason))
            {
                diagnostics["technicalFailureReason"] = technicalFailureReason;
            }

            notes.Add("Stage4 real SQLite adapter run.");
            notes.Add("Experiment flow: initial load/build, append INSERT batches, reopen after each batch, random point lookup sample.");
            notes.Add($"Fairness profile mapping: {fairness.FairnessProfileKey} -> journal_mode={fairness.JournalMode}, synchronous={fairness.Synchronous}, temp_store={fairness.TempStore}.");

            return Task.FromResult(new RunResult
            {
                RunId = runId,
                TimestampUtc = timestampUtc,
                EngineKey = EngineKeyValue,
                ExperimentKey = _spec.ExperimentKey,
                DatasetProfileKey = _spec.Dataset.ProfileKey,
                FairnessProfileKey = fairness.FairnessProfileKey,
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
                    ["research"] = _spec.ResearchQuestionId ?? string.Empty,
                    ["hypothesis"] = _spec.HypothesisId ?? string.Empty
                },
                Notes = notes
            });
        }

        private static void ValidateSpec(ExperimentSpec spec)
        {
            if (spec.Dataset.RecordCount <= 0 || spec.Dataset.RecordCount > int.MaxValue - 1)
            {
                throw new NotSupportedException("SQLite stage4 adapter supports dataset sizes in range [1, Int32.MaxValue-1].");
            }

            var isLoadBuildExperiment = spec.ExperimentKey.Equals(LoadBuildExperimentKey, StringComparison.OrdinalIgnoreCase) &&
                                        spec.Workload.WorkloadKey.Equals(LoadBuildWorkloadKey, StringComparison.OrdinalIgnoreCase);

            var isAppendCyclesExperiment = spec.ExperimentKey.Equals(AppendCyclesExperimentKey, StringComparison.OrdinalIgnoreCase) &&
                                           spec.Workload.WorkloadKey.Equals(AppendCyclesWorkloadKey, StringComparison.OrdinalIgnoreCase);

            if (!isLoadBuildExperiment && !isAppendCyclesExperiment)
            {
                throw new NotSupportedException($"Experiment/workload '{spec.ExperimentKey}'/'{spec.Workload.WorkloadKey}' is not implemented in SQLite stage4 adapter.");
            }
        }

        private static FairnessMapping ResolveFairness(string? fairnessProfileKey)
        {
            var key = fairnessProfileKey ?? DurabilityBalancedProfileKey;
            if (!key.Equals(DurabilityBalancedProfileKey, StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    $"Fairness profile '{key}' is not implemented for SQLite stage4 adapter. Supported: {DurabilityBalancedProfileKey}.");
            }

            return new FairnessMapping(
                DurabilityBalancedProfileKey,
                JournalMode: "WAL",
                Synchronous: "FULL",
                TempStore: "FILE");
        }

        private static int ResolveLookupCount(WorkloadSpec workload)
        {
            if (workload.LookupCount.HasValue && workload.LookupCount.Value > 0)
            {
                return workload.LookupCount.Value;
            }

            if (workload.BatchCount.HasValue &&
                workload.BatchSize.HasValue &&
                workload.BatchCount.Value > 0 &&
                workload.BatchSize.Value > 0)
            {
                var expanded = (long)workload.BatchCount.Value * workload.BatchSize.Value;
                return (int)Math.Min(expanded, int.MaxValue);
            }

            return 10_000;
        }

        private static int ResolveDirectLookupKey(long recordCount)
        {
            return checked((int)((recordCount + 1) / 2));
        }

        private static (int BatchCount, int BatchSize) ResolveAppendCycleShape(WorkloadSpec workload)
        {
            if (!workload.BatchCount.HasValue || workload.BatchCount.Value <= 0)
            {
                throw new NotSupportedException("append-cycles-reopen-lookup requires workload.batchCount >= 1.");
            }

            if (!workload.BatchSize.HasValue || workload.BatchSize.Value <= 0)
            {
                throw new NotSupportedException("append-cycles-reopen-lookup requires workload.batchSize >= 1.");
            }

            return (workload.BatchCount.Value, workload.BatchSize.Value);
        }

        private static ArtifactLayout CreateArtifactLayout(RunWorkspace workspace, string runId)
        {
            var artifactsRoot = workspace.ArtifactsDirectory ?? Path.Combine(workspace.WorkingDirectory, "artifacts");
            var root = Path.Combine(artifactsRoot, "sqlite", runId);
            return new ArtifactLayout(root, Path.Combine(root, "primary.db"));
        }

        private static SqliteConnection CreateConnection(string primaryDatabasePath)
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = primaryDatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                Pooling = false
            }.ToString();

            return new SqliteConnection(connectionString);
        }

        private static void ApplyFairness(
            SqliteConnection connection,
            FairnessMapping fairness,
            out string journalMode,
            out string synchronous,
            out string tempStore)
        {
            ExecuteNonQuery(connection, $"PRAGMA journal_mode={fairness.JournalMode};");
            journalMode = ExecuteScalarText(connection, "PRAGMA journal_mode;");

            ExecuteNonQuery(connection, $"PRAGMA synchronous={fairness.Synchronous};");
            synchronous = ExecuteScalarText(connection, "PRAGMA synchronous;");

            ExecuteNonQuery(connection, $"PRAGMA temp_store={fairness.TempStore};");
            tempStore = ExecuteScalarText(connection, "PRAGMA temp_store;");
        }

        private static void CreateSchema(SqliteConnection connection)
        {
            ExecuteNonQuery(connection, $"DROP TABLE IF EXISTS {TableName};");
            ExecuteNonQuery(connection, $"CREATE TABLE {TableName} (id INTEGER NOT NULL, name TEXT NOT NULL, age INTEGER NOT NULL);");
        }

        private static void BulkInsertPersons(
            SqliteConnection connection,
            long recordCount,
            int seed,
            CancellationToken cancellationToken)
        {
            using var transaction = connection.BeginTransaction();
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = $"INSERT INTO {TableName}(id, name, age) VALUES ($id, $name, $age);";

            var idParameter = insert.CreateParameter();
            idParameter.ParameterName = "$id";
            insert.Parameters.Add(idParameter);

            var nameParameter = insert.CreateParameter();
            nameParameter.ParameterName = "$name";
            insert.Parameters.Add(nameParameter);

            var ageParameter = insert.CreateParameter();
            ageParameter.ParameterName = "$age";
            insert.Parameters.Add(ageParameter);

            var random = new Random(seed);
            for (var i = 0L; i < recordCount; i++)
            {
                if ((i & 0x3FF) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var id = checked((int)(recordCount - i));
                idParameter.Value = id;
                nameParameter.Value = $"={id}=";
                ageParameter.Value = random.Next(18, 90);
                insert.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        private static void InsertBatchPersons(
            SqliteConnection connection,
            int firstId,
            int batchSize,
            int seed,
            CancellationToken cancellationToken)
        {
            using var transaction = connection.BeginTransaction();
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = $"INSERT INTO {TableName}(id, name, age) VALUES ($id, $name, $age);";

            var idParameter = insert.CreateParameter();
            idParameter.ParameterName = "$id";
            insert.Parameters.Add(idParameter);

            var nameParameter = insert.CreateParameter();
            nameParameter.ParameterName = "$name";
            insert.Parameters.Add(nameParameter);

            var ageParameter = insert.CreateParameter();
            ageParameter.ParameterName = "$age";
            insert.Parameters.Add(ageParameter);

            var random = new Random(seed);
            for (var i = 0; i < batchSize; i++)
            {
                if ((i & 0x3FF) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var id = checked(firstId + i);
                idParameter.Value = id;
                nameParameter.Value = $"={id}=";
                ageParameter.Value = random.Next(18, 90);
                insert.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        private static void BuildLookupIndex(SqliteConnection connection)
        {
            ExecuteNonQuery(connection, $"CREATE INDEX {IdIndexName} ON {TableName}(id);");
        }

        private static long ReadRowCount(SqliteConnection connection)
        {
            return ExecuteScalarInt64(connection, $"SELECT COUNT(*) FROM {TableName};");
        }

        private static bool HasIndex(SqliteConnection connection, string indexName)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = $indexName;";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$indexName";
            parameter.Value = indexName;
            command.Parameters.Add(parameter);

            var value = command.ExecuteScalar();
            return TryReadInt64(value, out var count) && count > 0;
        }

        private static bool HasPersonById(SqliteConnection connection, int id)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT id FROM {TableName} WHERE id = $id LIMIT 1;";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$id";
            parameter.Value = id;
            command.Parameters.Add(parameter);

            var value = command.ExecuteScalar();
            return TryReadInt(value, out var foundId) && foundId == id;
        }

        private static void ExecuteNonQuery(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private static string ExecuteScalarText(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            var value = command.ExecuteScalar();
            return value switch
            {
                null => string.Empty,
                long longValue => longValue.ToString(CultureInfo.InvariantCulture),
                int intValue => intValue.ToString(CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            };
        }

        private static long ExecuteScalarInt64(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            var value = command.ExecuteScalar();
            return TryReadInt64(value, out var number) ? number : 0L;
        }

        private static bool TryReadInt(object? value, out int number)
        {
            if (value is int intValue)
            {
                number = intValue;
                return true;
            }

            if (value is long longValue && longValue is >= int.MinValue and <= int.MaxValue)
            {
                number = (int)longValue;
                return true;
            }

            number = 0;
            return false;
        }

        private static bool TryReadInt64(object? value, out long number)
        {
            if (value is long longValue)
            {
                number = longValue;
                return true;
            }

            if (value is int intValue)
            {
                number = intValue;
                return true;
            }

            if (value is string text && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                number = parsed;
                return true;
            }

            number = 0L;
            return false;
        }

        private static ArtifactCollection CollectArtifacts(ArtifactLayout layout, string workingDirectory)
        {
            if (!Directory.Exists(layout.ArtifactsRootDirectory))
            {
                return new ArtifactCollection(Array.Empty<ArtifactDescriptor>(), 0L, 0L, 0L, 0L, 0L, 0L);
            }

            var descriptors = new List<ArtifactDescriptor>();
            var files = Directory
                .GetFiles(layout.ArtifactsRootDirectory, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            long databaseBytes = 0L;
            long walBytes = 0L;
            long shmBytes = 0L;
            long journalBytes = 0L;
            long temporaryBytes = 0L;
            long totalBytes = 0L;

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                var bytes = info.Exists ? info.Length : 0L;
                totalBytes += bytes;

                var role = ArtifactRole.Metadata;
                string? note = null;
                var name = Path.GetFileName(file);

                if (string.Equals(file, layout.PrimaryDatabasePath, StringComparison.OrdinalIgnoreCase))
                {
                    role = ArtifactRole.PrimaryDatabase;
                    note = "SQLite primary database file.";
                    databaseBytes += bytes;
                }
                else if (name.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase))
                {
                    role = ArtifactRole.Wal;
                    note = "SQLite write-ahead log.";
                    walBytes += bytes;
                }
                else if (name.EndsWith(".db-shm", StringComparison.OrdinalIgnoreCase))
                {
                    role = ArtifactRole.SharedMemory;
                    note = "SQLite shared memory for WAL coordination.";
                    shmBytes += bytes;
                }
                else if (name.EndsWith(".db-journal", StringComparison.OrdinalIgnoreCase))
                {
                    role = ArtifactRole.Journal;
                    note = "SQLite rollback journal.";
                    journalBytes += bytes;
                }
                else if (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("etilqs", StringComparison.OrdinalIgnoreCase))
                {
                    role = ArtifactRole.Temporary;
                    note = "SQLite temporary artifact.";
                    temporaryBytes += bytes;
                }

                descriptors.Add(new ArtifactDescriptor(
                    role,
                    ToRelativePath(workingDirectory, file),
                    bytes,
                    note));
            }

            return new ArtifactCollection(descriptors, databaseBytes, walBytes, shmBytes, journalBytes, temporaryBytes, totalBytes);
        }

        private static string BuildSemanticFailureReason(
            int directLookupKey,
            bool directLookupHit,
            long expectedRowCount,
            long actualRowCount,
            long lookupCount,
            long lookupHits,
            bool indexPresent)
        {
            var reasons = new List<string>();
            if (!directLookupHit)
            {
                reasons.Add($"directLookupMiss key={directLookupKey}");
            }

            if (actualRowCount != expectedRowCount)
            {
                reasons.Add($"rowCountAfterReopen={actualRowCount}, expected={expectedRowCount}");
            }

            if (lookupHits != lookupCount)
            {
                reasons.Add($"lookupHits={lookupHits}, lookupCount={lookupCount}");
            }

            if (!indexPresent)
            {
                reasons.Add($"index '{IdIndexName}' is missing after reopen");
            }

            return reasons.Count == 0
                ? "Unknown semantic failure."
                : string.Join("; ", reasons);
        }

        private static string BuildAppendSemanticFailureReason(
            long expectedCountAfterCycles,
            IReadOnlyList<string> rowCountMismatches,
            long lookupCount,
            long lookupHits,
            IReadOnlyList<int> missingIndexCycles)
        {
            var reasons = new List<string>();
            if (rowCountMismatches.Count > 0)
            {
                reasons.Add($"rowCountMismatches={string.Join(";", rowCountMismatches)}");
            }

            if (lookupHits != lookupCount)
            {
                reasons.Add($"lookupHits={lookupHits}, lookupCount={lookupCount}");
            }

            if (missingIndexCycles.Count > 0)
            {
                reasons.Add($"missingIndexCycles={string.Join(",", missingIndexCycles)}");
            }

            if (reasons.Count == 0)
            {
                reasons.Add($"expectedCountAfterCycles={expectedCountAfterCycles}");
            }

            return string.Join("; ", reasons);
        }

        private static string ToRelativePath(string baseDirectory, string fullPath)
        {
            return Path.GetRelativePath(baseDirectory, fullPath).Replace('\\', '/');
        }

        private static string ToInvariant(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static void TryDispose(SqliteConnection? connection)
        {
            if (connection is null)
            {
                return;
            }

            try
            {
                connection.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        private readonly record struct ArtifactLayout(string ArtifactsRootDirectory, string PrimaryDatabasePath);

        private readonly record struct ArtifactCollection(
            IReadOnlyList<ArtifactDescriptor> Descriptors,
            long DatabaseBytes,
            long WalBytes,
            long ShmBytes,
            long JournalBytes,
            long TemporaryBytes,
            long TotalBytes);

        private readonly record struct FairnessMapping(
            string FairnessProfileKey,
            string JournalMode,
            string Synchronous,
            string TempStore);
    }
}
