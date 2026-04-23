using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;

namespace Polar.DB.Bench.Engine.PolarDb;

public sealed class PolarDbStorageEngineAdapter : IStorageEngineAdapter
{
    private static readonly PTypeRecord PersonRecordType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("age", new PType(PTypeEnumeration.integer)));

    public string EngineKey => "polar-db";

    public IReadOnlyCollection<EngineCapability> Capabilities { get; } = new[]
    {
        EngineCapability.BulkLoad,
        EngineCapability.PointLookup,
        EngineCapability.ReopenRecovery,
        EngineCapability.PhysicalArtifactInspection
    };

    public IEngineRun CreateRun(ExperimentSpec spec, RunWorkspace workspace)
    {
        return new PolarDbEngineRun(spec, workspace);
    }

    private sealed class PolarDbEngineRun : IEngineRun
    {
        private readonly ExperimentSpec _spec;
        private readonly RunWorkspace _workspace;

        public PolarDbEngineRun(ExperimentSpec spec, RunWorkspace workspace)
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

            var metrics = new List<RunMetric>();
            var notes = new List<string>();
            var artifacts = new List<ArtifactDescriptor>();
            var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var loadMs = 0.0;
            var buildMs = 0.0;
            var reopenRefreshMs = 0.0;
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
            var sequenceCountAfterRefresh = 0L;
            var appendOffsetAfterRefresh = 0L;

            USequence? sequence = null;
            USequence? reopened = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(artifactLayout.ArtifactsRootDirectory);

                sequence = CreateSequence(artifactLayout);

                var loadWatch = Stopwatch.StartNew();
                sequence.Load(GeneratePersons(_spec.Dataset.RecordCount, _spec.Dataset.Seed ?? 1));
                loadWatch.Stop();
                loadMs = loadWatch.Elapsed.TotalMilliseconds;

                cancellationToken.ThrowIfCancellationRequested();

                var buildWatch = Stopwatch.StartNew();
                sequence.Build();
                buildWatch.Stop();
                buildMs = buildWatch.Elapsed.TotalMilliseconds;

                sequence.Close();
                sequence = null;

                cancellationToken.ThrowIfCancellationRequested();

                var reopenWatch = Stopwatch.StartNew();
                reopened = CreateSequence(artifactLayout);
                reopened.Refresh();
                reopenWatch.Stop();
                reopenRefreshMs = reopenWatch.Elapsed.TotalMilliseconds;

                sequenceCountAfterRefresh = reopened.Sequence.Count();
                appendOffsetAfterRefresh = reopened.Sequence.AppendOffset;

                cancellationToken.ThrowIfCancellationRequested();

                var directLookupWatch = Stopwatch.StartNew();
                var directRow = reopened.GetByKey(directLookupKey);
                directLookupWatch.Stop();
                directLookupMs = directLookupWatch.Elapsed.TotalMilliseconds;
                directLookupHit = TryReadPersonKey(directRow, out var directRowKey) && directRowKey == directLookupKey;

                cancellationToken.ThrowIfCancellationRequested();

                var random = new Random((_spec.Dataset.Seed ?? 1) ^ CommonLookupSeedSalt);
                var maxKeyExclusive = checked((int)_spec.Dataset.RecordCount) + 1;
                var lookupWatch = Stopwatch.StartNew();

                for (var i = 0; i < lookupCount; i++)
                {
                    if ((i & 0x3FF) == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    var key = random.Next(1, maxKeyExclusive);
                    var row = reopened.GetByKey(key);
                    if (TryReadPersonKey(row, out var rowKey) && rowKey == key)
                    {
                        lookupHits++;
                    }
                }

                lookupWatch.Stop();
                lookupMs = lookupWatch.Elapsed.TotalMilliseconds;

                reopened.Close();
                reopened = null;

                semanticSuccess = directLookupHit && lookupHits == lookupCount;
                if (!semanticSuccess.Value)
                {
                    semanticFailureReason = BuildLoadBuildSemanticFailureReason(
                        directLookupKey,
                        directLookupHit,
                        lookupCount,
                        lookupHits);
                }
            }
            catch (Exception ex)
            {
                technicalSuccess = false;
                technicalFailureReason = ex.ToString();
            }
            finally
            {
                TryClose(sequence);
                TryClose(reopened);
                totalStopwatch.Stop();
            }

            var managedAfter = GC.GetTotalMemory(forceFullCollection: false);
            var gcInfo = GC.GetGCMemoryInfo();
            var processPeakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64;

            var collected = CollectArtifacts(artifactLayout, _workspace.WorkingDirectory);
            artifacts.AddRange(collected.Descriptors);

            metrics.Add(new RunMetric { MetricKey = "elapsedMsTotal", Value = totalStopwatch.Elapsed.TotalMilliseconds });
            metrics.Add(new RunMetric { MetricKey = "elapsedMsSingleRun", Value = totalStopwatch.Elapsed.TotalMilliseconds });
            metrics.Add(new RunMetric { MetricKey = "loadMs", Value = loadMs });
            metrics.Add(new RunMetric { MetricKey = "buildMs", Value = buildMs });
            metrics.Add(new RunMetric { MetricKey = "reopenRefreshMs", Value = reopenRefreshMs });
            metrics.Add(new RunMetric { MetricKey = "directPointLookupMs", Value = directLookupMs });
            metrics.Add(new RunMetric { MetricKey = "directPointLookupKey", Value = directLookupKey });
            metrics.Add(new RunMetric { MetricKey = "directPointLookupHit", Value = directLookupHit ? 1 : 0 });
            metrics.Add(new RunMetric { MetricKey = "randomPointLookupMs", Value = lookupMs });
            metrics.Add(new RunMetric { MetricKey = "randomPointLookupCount", Value = lookupCount });
            metrics.Add(new RunMetric { MetricKey = "randomPointLookupHits", Value = lookupHits });
            metrics.Add(new RunMetric { MetricKey = "randomPointLookupMisses", Value = Math.Max(0L, lookupCount - lookupHits) });
            metrics.Add(new RunMetric { MetricKey = "totalArtifactBytes", Value = collected.TotalBytes });
            metrics.Add(new RunMetric { MetricKey = "primaryDataBytes", Value = collected.PrimaryDataBytes });
            metrics.Add(new RunMetric { MetricKey = "indexBytes", Value = collected.IndexBytes });
            metrics.Add(new RunMetric { MetricKey = "stateFileBytes", Value = collected.StateBytes });
            metrics.Add(new RunMetric { MetricKey = "managedBytesBefore", Value = managedBefore });
            metrics.Add(new RunMetric { MetricKey = "managedBytesAfter", Value = managedAfter });
            metrics.Add(new RunMetric { MetricKey = "managedBytesDelta", Value = managedAfter - managedBefore });
            metrics.Add(new RunMetric { MetricKey = "heapSizeBytes", Value = gcInfo.HeapSizeBytes });
            metrics.Add(new RunMetric { MetricKey = "fragmentedBytes", Value = gcInfo.FragmentedBytes });
            metrics.Add(new RunMetric { MetricKey = "processPeakWorkingSetBytes", Value = processPeakWorkingSet });

            diagnostics["stateFileBytes"] = ToInvariant(collected.StateBytes);
            diagnostics["indexFileBytes"] = ToInvariant(collected.IndexBytes);
            diagnostics["primaryDataFileBytes"] = ToInvariant(collected.PrimaryDataBytes);
            diagnostics["totalArtifactBytes"] = ToInvariant(collected.TotalBytes);
            diagnostics["directLookupKey"] = directLookupKey.ToString(CultureInfo.InvariantCulture);
            diagnostics["directLookupHit"] = directLookupHit.ToString();
            diagnostics["directLookupMs"] = ToInvariant(directLookupMs);
            diagnostics["lookupCount"] = lookupCount.ToString(CultureInfo.InvariantCulture);
            diagnostics["lookupHitCount"] = lookupHits.ToString(CultureInfo.InvariantCulture);
            diagnostics["sequenceCountAfterRefresh"] = sequenceCountAfterRefresh.ToString(CultureInfo.InvariantCulture);
            diagnostics["sequenceAppendOffsetAfterRefresh"] = appendOffsetAfterRefresh.ToString(CultureInfo.InvariantCulture);
            diagnostics["semanticSuccess"] = semanticSuccess?.ToString() ?? "not-evaluated";

            if (!string.IsNullOrWhiteSpace(semanticFailureReason))
            {
                diagnostics["semanticFailureReason"] = semanticFailureReason;
            }

            if (!technicalSuccess && !string.IsNullOrWhiteSpace(technicalFailureReason))
            {
                diagnostics["technicalFailureReason"] = technicalFailureReason;
            }

            var state = TryReadState(artifactLayout.StateFilePath);
            if (state.HasValue)
            {
                diagnostics["stateCount"] = state.Value.Count.ToString(CultureInfo.InvariantCulture);
                diagnostics["stateAppendOffset"] = state.Value.AppendOffset.ToString(CultureInfo.InvariantCulture);
            }

            notes.Add("Stage4 real adapter run for Polar.DB.");
            notes.Add("Imported reference workload: load in reverse id order -> build -> reopen/refresh -> direct key lookup -> random point lookup batch.");
            notes.Add($"Reference-normalized lookup batch size: {lookupCount}.");

            return Task.FromResult(new RunResult
            {
                RunId = runId,
                TimestampUtc = timestampUtc,
                EngineKey = EngineKeyValue,
                ExperimentKey = _spec.ExperimentKey,
                DatasetProfileKey = _spec.Dataset.ProfileKey,
                FairnessProfileKey = _spec.FairnessProfile?.FairnessProfileKey ?? "unspecified",
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

            var metrics = new List<RunMetric>();
            var notes = new List<string>();
            var artifacts = new List<ArtifactDescriptor>();
            var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var loadMs = 0.0;
            var appendMs = 0.0;
            var buildMs = 0.0;
            var reopenRefreshMs = 0.0;
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
            var cycleArtifactBytes = new List<long>();
            var initialArtifactBytes = 0L;
            var expectedCount = _spec.Dataset.RecordCount;

            USequence? session = null;
            USequence? reopened = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(artifactLayout.ArtifactsRootDirectory);

                var initialLoadWatch = Stopwatch.StartNew();
                session = CreateSequence(artifactLayout);
                session.Load(GeneratePersons(_spec.Dataset.RecordCount, _spec.Dataset.Seed ?? 1));
                initialLoadWatch.Stop();
                loadMs += initialLoadWatch.Elapsed.TotalMilliseconds;

                cancellationToken.ThrowIfCancellationRequested();

                var initialBuildWatch = Stopwatch.StartNew();
                session.Build();
                initialBuildWatch.Stop();
                buildMs = initialBuildWatch.Elapsed.TotalMilliseconds;

                session.Close();
                session = null;

                var initialCollected = CollectArtifacts(artifactLayout, _workspace.WorkingDirectory);
                initialArtifactBytes = initialCollected.TotalBytes;
                cycleArtifactBytes.Add(initialCollected.TotalBytes);

                var appendRandom = new Random((_spec.Dataset.Seed ?? 1) ^ 0x55aa12ef);

                for (var cycle = 0; cycle < appendCycleShape.BatchCount; cycle++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    session = CreateSequence(artifactLayout);
                    session.Refresh();

                    var appendWatch = Stopwatch.StartNew();
                    var firstId = checked((int)(expectedCount + 1));
                    for (var i = 0; i < appendCycleShape.BatchSize; i++)
                    {
                        if ((i & 0x3FF) == 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        var id = checked(firstId + i);
                        session.AppendElement(CreatePersonRecord(id, appendRandom));
                    }

                    appendWatch.Stop();
                    appendMs += appendWatch.Elapsed.TotalMilliseconds;
                    loadMs += appendWatch.Elapsed.TotalMilliseconds;
                    expectedCount += appendCycleShape.BatchSize;

                    session.Close();
                    session = null;

                    var reopenWatch = Stopwatch.StartNew();
                    reopened = CreateSequence(artifactLayout);
                    reopened.Refresh();
                    reopenWatch.Stop();
                    reopenRefreshMs += reopenWatch.Elapsed.TotalMilliseconds;

                    var actualCount = reopened.Sequence.Count();
                    if (actualCount != expectedCount)
                    {
                        rowCountMismatches.Add($"cycle{cycle + 1}:count={actualCount},expected={expectedCount}");
                    }

                    var random = new Random((_spec.Dataset.Seed ?? 1) ^ (0x6e7f0000 + cycle));
                    var maxKeyExclusive = checked((int)expectedCount) + 1;
                    var lookupWatch = Stopwatch.StartNew();
                    for (var i = 0; i < lookupCountPerCycle; i++)
                    {
                        if ((i & 0x3FF) == 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        var key = random.Next(1, maxKeyExclusive);
                        var row = reopened.GetByKey(key) as object[];
                        lookupAttempts++;
                        if (row is not null && row.Length > 0 && row[0] is int rowKey && rowKey == key)
                        {
                            lookupHits++;
                        }
                    }

                    lookupWatch.Stop();
                    lookupMs += lookupWatch.Elapsed.TotalMilliseconds;

                    reopened.Close();
                    reopened = null;

                    var cycleCollected = CollectArtifacts(artifactLayout, _workspace.WorkingDirectory);
                    cycleArtifactBytes.Add(cycleCollected.TotalBytes);
                }

                semanticSuccess = lookupHits == lookupAttempts && rowCountMismatches.Count == 0;
                if (!semanticSuccess.Value)
                {
                    semanticFailureReason = BuildAppendSemanticFailureReason(
                        expectedCount,
                        rowCountMismatches,
                        lookupAttempts,
                        lookupHits);
                }
            }
            catch (Exception ex)
            {
                technicalSuccess = false;
                technicalFailureReason = ex.ToString();
            }
            finally
            {
                TryClose(session);
                TryClose(reopened);
                totalStopwatch.Stop();
            }

            var managedAfter = GC.GetTotalMemory(forceFullCollection: false);
            var gcInfo = GC.GetGCMemoryInfo();
            var processPeakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64;

            var collected = CollectArtifacts(artifactLayout, _workspace.WorkingDirectory);
            artifacts.AddRange(collected.Descriptors);
            var artifactGrowthBytes = Math.Max(0L, collected.TotalBytes - initialArtifactBytes);

            metrics.Add(new RunMetric { MetricKey = "elapsedMsTotal", Value = totalStopwatch.Elapsed.TotalMilliseconds });
            metrics.Add(new RunMetric { MetricKey = "elapsedMsSingleRun", Value = totalStopwatch.Elapsed.TotalMilliseconds });
            metrics.Add(new RunMetric { MetricKey = "loadMs", Value = loadMs });
            metrics.Add(new RunMetric { MetricKey = "appendMs", Value = appendMs });
            metrics.Add(new RunMetric { MetricKey = "buildMs", Value = buildMs });
            metrics.Add(new RunMetric { MetricKey = "reopenRefreshMs", Value = reopenRefreshMs });
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
            metrics.Add(new RunMetric { MetricKey = "primaryDataBytes", Value = collected.PrimaryDataBytes });
            metrics.Add(new RunMetric { MetricKey = "indexBytes", Value = collected.IndexBytes });
            metrics.Add(new RunMetric { MetricKey = "stateFileBytes", Value = collected.StateBytes });
            metrics.Add(new RunMetric { MetricKey = "managedBytesBefore", Value = managedBefore });
            metrics.Add(new RunMetric { MetricKey = "managedBytesAfter", Value = managedAfter });
            metrics.Add(new RunMetric { MetricKey = "managedBytesDelta", Value = managedAfter - managedBefore });
            metrics.Add(new RunMetric { MetricKey = "heapSizeBytes", Value = gcInfo.HeapSizeBytes });
            metrics.Add(new RunMetric { MetricKey = "fragmentedBytes", Value = gcInfo.FragmentedBytes });
            metrics.Add(new RunMetric { MetricKey = "processPeakWorkingSetBytes", Value = processPeakWorkingSet });

            diagnostics["appendBatchCount"] = appendCycleShape.BatchCount.ToString(CultureInfo.InvariantCulture);
            diagnostics["appendBatchSize"] = appendCycleShape.BatchSize.ToString(CultureInfo.InvariantCulture);
            diagnostics["lookupCountPerCycle"] = lookupCountPerCycle.ToString(CultureInfo.InvariantCulture);
            diagnostics["lookupCount"] = lookupAttempts.ToString(CultureInfo.InvariantCulture);
            diagnostics["lookupHitCount"] = lookupHits.ToString(CultureInfo.InvariantCulture);
            diagnostics["expectedCountAfterCycles"] = expectedCount.ToString(CultureInfo.InvariantCulture);
            diagnostics["initialArtifactBytes"] = initialArtifactBytes.ToString(CultureInfo.InvariantCulture);
            diagnostics["artifactGrowthBytes"] = artifactGrowthBytes.ToString(CultureInfo.InvariantCulture);
            diagnostics["cycleArtifactBytes"] = string.Join(",", cycleArtifactBytes);
            diagnostics["stateFileBytes"] = ToInvariant(collected.StateBytes);
            diagnostics["indexFileBytes"] = ToInvariant(collected.IndexBytes);
            diagnostics["primaryDataFileBytes"] = ToInvariant(collected.PrimaryDataBytes);
            diagnostics["totalArtifactBytes"] = ToInvariant(collected.TotalBytes);
            diagnostics["semanticSuccess"] = semanticSuccess?.ToString() ?? "not-evaluated";

            if (rowCountMismatches.Count > 0)
            {
                diagnostics["rowCountMismatches"] = string.Join(" | ", rowCountMismatches);
            }

            if (!string.IsNullOrWhiteSpace(semanticFailureReason))
            {
                diagnostics["semanticFailureReason"] = semanticFailureReason;
            }

            if (!technicalSuccess && !string.IsNullOrWhiteSpace(technicalFailureReason))
            {
                diagnostics["technicalFailureReason"] = technicalFailureReason;
            }

            notes.Add("Stage4 real adapter run for Polar.DB.");
            notes.Add("Experiment flow: initial load/build, append batches, reopen after each batch, random lookup sample.");

            return Task.FromResult(new RunResult
            {
                RunId = runId,
                TimestampUtc = timestampUtc,
                EngineKey = EngineKeyValue,
                ExperimentKey = _spec.ExperimentKey,
                DatasetProfileKey = _spec.Dataset.ProfileKey,
                FairnessProfileKey = _spec.FairnessProfile?.FairnessProfileKey ?? "unspecified",
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

        private const string EngineKeyValue = "polar-db";
        private const string LoadBuildExperimentKey = "persons-load-build-reopen-random-lookup";
        private const string LoadBuildWorkloadKey = "bulk-load-point-lookup";
        private const string AppendCyclesExperimentKey = "persons-append-cycles-reopen-lookup";
        private const string AppendCyclesWorkloadKey = "append-cycles-reopen-lookup";
        private const int CommonLookupSeedSalt = unchecked((int)0x1f2e3d4c);

        private static void ValidateSpec(ExperimentSpec spec)
        {
            if (spec.Dataset.RecordCount <= 0 || spec.Dataset.RecordCount > int.MaxValue - 1)
            {
                throw new NotSupportedException("Polar.DB stage4 adapter supports dataset sizes in range [1, Int32.MaxValue-1].");
            }

            var isLoadBuildExperiment = spec.ExperimentKey.Equals(LoadBuildExperimentKey, StringComparison.OrdinalIgnoreCase) &&
                                        spec.Workload.WorkloadKey.Equals(LoadBuildWorkloadKey, StringComparison.OrdinalIgnoreCase);

            var isAppendCyclesExperiment = spec.ExperimentKey.Equals(AppendCyclesExperimentKey, StringComparison.OrdinalIgnoreCase) &&
                                           spec.Workload.WorkloadKey.Equals(AppendCyclesWorkloadKey, StringComparison.OrdinalIgnoreCase);

            if (!isLoadBuildExperiment && !isAppendCyclesExperiment)
            {
                throw new NotSupportedException(
                    $"Experiment/workload '{spec.ExperimentKey}'/'{spec.Workload.WorkloadKey}' is not implemented in Polar.DB stage4 adapter.");
            }
        }

        private static int ResolveLookupCount(WorkloadSpec workload)
        {
            if (workload.LookupCount.HasValue && workload.LookupCount.Value > 0)
            {
                return workload.LookupCount.Value;
            }

            if (workload.BatchCount.HasValue && workload.BatchSize.HasValue && workload.BatchCount.Value > 0 && workload.BatchSize.Value > 0)
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

        private static IEnumerable<object> GeneratePersons(long recordCount, int seed)
        {
            var random = new Random(seed);
            for (var i = 0L; i < recordCount; i++)
            {
                var id = checked((int)(recordCount - i));
                yield return new object[]
                {
                    id,
                    $"={id}=",
                    random.Next(18, 90)
                };
            }
        }

        private static object[] CreatePersonRecord(int id, Random random)
        {
            return new object[]
            {
                id,
                $"={id}=",
                random.Next(18, 90)
            };
        }

        private static bool TryReadPersonKey(object? rowValue, out int rowKey)
        {
            if (rowValue is object[] row &&
                row.Length > 0 &&
                row[0] is int parsed)
            {
                rowKey = parsed;
                return true;
            }

            rowKey = 0;
            return false;
        }

        private static string BuildLoadBuildSemanticFailureReason(
            int directLookupKey,
            bool directLookupHit,
            long lookupCount,
            long lookupHits)
        {
            var reasons = new List<string>();
            if (!directLookupHit)
            {
                reasons.Add($"directLookupMiss key={directLookupKey}");
            }

            if (lookupHits != lookupCount)
            {
                reasons.Add($"lookupHits={lookupHits}, lookupCount={lookupCount}");
            }

            return reasons.Count == 0
                ? "Unknown semantic failure."
                : string.Join("; ", reasons);
        }

        private static string BuildAppendSemanticFailureReason(
            long expectedCountAfterCycles,
            IReadOnlyList<string> rowCountMismatches,
            long lookupAttempts,
            long lookupHits)
        {
            var reasons = new List<string>();
            if (rowCountMismatches.Count > 0)
            {
                reasons.Add($"rowCountMismatches={string.Join(";", rowCountMismatches)}");
            }

            if (lookupAttempts != lookupHits)
            {
                reasons.Add($"lookupHits={lookupHits}, lookupCount={lookupAttempts}");
            }

            if (reasons.Count == 0)
            {
                reasons.Add($"expectedCountAfterCycles={expectedCountAfterCycles}");
            }

            return string.Join("; ", reasons);
        }

        private static USequence CreateSequence(ArtifactLayout layout)
        {
            var streamCounter = 0;

            Stream StreamGenerator()
            {
                var path = Path.Combine(layout.ArtifactsRootDirectory, $"f{streamCounter++}.bin");
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            }

            return new USequence(
                PersonRecordType,
                layout.StateFilePath,
                StreamGenerator,
                isEmpty: _ => false,
                keyFunc: value => (int)((object[])value)[0],
                hashOfKey: key => (int)key,
                optimise: false);
        }

        private static ArtifactLayout CreateArtifactLayout(RunWorkspace workspace, string runId)
        {
            var artifactsRoot = workspace.ArtifactsDirectory
                ?? Path.Combine(workspace.WorkingDirectory, "artifacts");

            return new ArtifactLayout(
                Path.Combine(artifactsRoot, "polar-db", runId),
                Path.Combine(artifactsRoot, "polar-db", runId, "state.bin"));
        }

        private static ArtifactCollection CollectArtifacts(ArtifactLayout layout, string workingDirectory)
        {
            if (!Directory.Exists(layout.ArtifactsRootDirectory))
            {
                return new ArtifactCollection(Array.Empty<ArtifactDescriptor>(), 0L, 0L, 0L, 0L);
            }

            var descriptors = new List<ArtifactDescriptor>();
            var files = Directory
                .GetFiles(layout.ArtifactsRootDirectory, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            long primaryDataBytes = 0L;
            long indexBytes = 0L;
            long stateBytes = 0L;
            long totalBytes = 0L;

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var bytes = fileInfo.Exists ? fileInfo.Length : 0L;
                totalBytes += bytes;

                ArtifactRole role;
                string? notes;
                var name = Path.GetFileName(file);

                if (string.Equals(file, layout.StateFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    role = ArtifactRole.State;
                    notes = "Polar.DB state sidecar";
                    stateBytes += bytes;
                }
                else if (string.Equals(name, "f0.bin", StringComparison.OrdinalIgnoreCase))
                {
                    role = ArtifactRole.PrimaryData;
                    notes = "Polar.DB primary sequence data";
                    primaryDataBytes += bytes;
                }
                else if (name.StartsWith("f", StringComparison.OrdinalIgnoreCase) &&
                         name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                {
                    role = ArtifactRole.SecondaryIndex;
                    notes = "Polar.DB primary-key index segment";
                    indexBytes += bytes;
                }
                else
                {
                    role = ArtifactRole.Metadata;
                    notes = "Unclassified Polar.DB artifact";
                }

                descriptors.Add(new ArtifactDescriptor(
                    role,
                    ToRelativePath(workingDirectory, file),
                    bytes,
                    notes));
            }

            return new ArtifactCollection(descriptors, primaryDataBytes, indexBytes, stateBytes, totalBytes);
        }

        private static (long Count, long AppendOffset)? TryReadState(string stateFilePath)
        {
            if (!File.Exists(stateFilePath))
            {
                return null;
            }

            try
            {
                using var fs = new FileStream(stateFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length < 16L)
                {
                    return null;
                }

                using var reader = new BinaryReader(fs);
                var count = reader.ReadInt64();
                var appendOffset = reader.ReadInt64();
                return (count, appendOffset);
            }
            catch
            {
                return null;
            }
        }

        private static string ToRelativePath(string baseDirectory, string fullPath)
        {
            return Path.GetRelativePath(baseDirectory, fullPath).Replace('\\', '/');
        }

        private static string ToInvariant(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string ToInvariant(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static void TryClose(USequence? sequence)
        {
            if (sequence is null)
            {
                return;
            }

            try
            {
                sequence.Close();
            }
            catch
            {
                // ignored
            }
        }

        private readonly record struct ArtifactLayout(string ArtifactsRootDirectory, string StateFilePath);

        private readonly record struct ArtifactCollection(
            IReadOnlyList<ArtifactDescriptor> Descriptors,
            long PrimaryDataBytes,
            long IndexBytes,
            long StateBytes,
            long TotalBytes);
    }
}
