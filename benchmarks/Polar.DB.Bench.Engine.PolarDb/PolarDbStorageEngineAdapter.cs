using System.Diagnostics;
using System.Globalization;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;
using PolarDbLib = global::Polar.DB;

namespace Polar.DB.Bench.Engine.PolarDb;

public sealed class PolarDbStorageEngineAdapter : IStorageEngineAdapter
{
    private static readonly PolarDbLib.PTypeRecord PersonRecordType = new(
        new PolarDbLib.NamedType("id", new PolarDbLib.PType(PolarDbLib.PTypeEnumeration.integer)),
        new PolarDbLib.NamedType("name", new PolarDbLib.PType(PolarDbLib.PTypeEnumeration.sstring)),
        new PolarDbLib.NamedType("age", new PolarDbLib.PType(PolarDbLib.PTypeEnumeration.integer)));

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
            var lookupMs = 0.0;
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

            PolarDbLib.USequence? sequence = null;
            PolarDbLib.USequence? reopened = null;

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

                var random = new Random((_spec.Dataset.Seed ?? 1) ^ 0x1f2e3d4c);
                var maxKeyExclusive = checked((int)_spec.Dataset.RecordCount) + 1;
                var lookupWatch = Stopwatch.StartNew();

                for (var i = 0; i < lookupCount; i++)
                {
                    if ((i & 0x3FF) == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    var key = random.Next(1, maxKeyExclusive);
                    var row = reopened.GetByKey(key) as object[];
                    if (row is not null && row.Length > 0 && row[0] is int rowKey && rowKey == key)
                    {
                        lookupHits++;
                    }
                }

                lookupWatch.Stop();
                lookupMs = lookupWatch.Elapsed.TotalMilliseconds;

                reopened.Close();
                reopened = null;

                semanticSuccess = lookupHits == lookupCount;
                if (!semanticSuccess.Value)
                {
                    semanticFailureReason = $"Point lookups succeeded partially: {lookupHits}/{lookupCount}.";
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

            notes.Add("Stage2 real adapter run for Polar.DB.");
            notes.Add("Experiment flow: load -> build -> reopen/refresh -> random point lookup.");

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
                    ["researchQuestionId"] = _spec.ResearchQuestionId ?? string.Empty,
                    ["hypothesisId"] = _spec.HypothesisId ?? string.Empty
                },
                Notes = notes
            });
        }

        private const string EngineKeyValue = "polar-db";
        private const string SupportedExperimentKey = "persons-load-build-reopen-random-lookup";
        private const string SupportedWorkloadKey = "bulk-load-point-lookup";

        private static void ValidateSpec(ExperimentSpec spec)
        {
            if (spec.Dataset.RecordCount <= 0 || spec.Dataset.RecordCount > int.MaxValue - 1)
            {
                throw new NotSupportedException("Polar.DB stage2 adapter supports dataset sizes in range [1, Int32.MaxValue-1].");
            }

            if (!spec.ExperimentKey.Equals(SupportedExperimentKey, StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    $"Experiment '{spec.ExperimentKey}' is not implemented in stage2 Polar.DB adapter.");
            }

            if (!spec.Workload.WorkloadKey.Equals(SupportedWorkloadKey, StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    $"Workload '{spec.Workload.WorkloadKey}' is not implemented in stage2 Polar.DB adapter.");
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

        private static PolarDbLib.USequence CreateSequence(ArtifactLayout layout)
        {
            var streamCounter = 0;

            Stream StreamGenerator()
            {
                var path = Path.Combine(layout.ArtifactsRootDirectory, $"f{streamCounter++}.bin");
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            }

            return new PolarDbLib.USequence(
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
                string? notes = null;
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

        private static void TryClose(PolarDbLib.USequence? sequence)
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
