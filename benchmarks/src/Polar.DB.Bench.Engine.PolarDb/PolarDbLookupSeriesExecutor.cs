using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.LookupSeries;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;
using static Polar.DB.Bench.Core.Services.FileWarmup;

namespace Polar.DB.Bench.Engine.PolarDb;

/// <summary>
/// Current-source Polar.DB executor for lookup-series workloads.
///
/// Each lookup run now measures two separate phases over the same generated probes:
/// 1) index-only: key -> offset/count, without reading payload records;
/// 2) materialized: key -> offset(s) -> payload object[] records.
/// </summary>
public static class PolarDbLookupSeriesExecutor
{
    private const string EngineKeyValue = "polar-db";

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

        var directIndexOnlyLookupMs = 0.0;
        var directIndexOnlyExpectedOffsets = 0;
        var directIndexOnlyReturnedOffsets = 0;
        var directIndexOnlyHit = false;

        var directMaterializedLookupMs = 0.0;
        var directMaterializedExpectedRows = 0;
        var directMaterializedReturnedRows = 0;
        var directMaterializedHit = false;

        var indexOnlyLookupMs = 0.0;
        var indexOnlyProbeHits = 0L;
        var indexOnlyProbeMisses = 0L;
        var indexOnlyReturnedOffsets = 0L;
        var indexOnlyExpectedOffsets = 0L;

        var materializedLookupMs = 0.0;
        var materializedProbeHits = 0L;
        var materializedProbeMisses = 0L;
        var materializedReturnedRows = 0L;
        var materializedExpectedRows = 0L;

        var mismatchSamples = new List<string>();
        var sequenceCountAfterRefresh = 0L;
        var appendOffsetAfterRefresh = 0L;
        var managedBefore = GC.GetTotalMemory(forceFullCollection: false);
        var totalStopwatch = Stopwatch.StartNew();

        USequence? sequence = null;
        USequence? active = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(artifactLayout.ArtifactsRootDirectory);

            sequence = CreateSequence(artifactLayout, options);
            var loadWatch = Stopwatch.StartNew();
            sequence.Load(GenerateRows(spec, options));
            loadWatch.Stop();
            loadMs = loadWatch.Elapsed.TotalMilliseconds;

            cancellationToken.ThrowIfCancellationRequested();
            var buildWatch = Stopwatch.StartNew();
            sequence.Build();
            buildWatch.Stop();
            buildMs = buildWatch.Elapsed.TotalMilliseconds;

            if (options.ReopenAfterBuild)
            {
                sequence.Close();
                sequence = null;

                cancellationToken.ThrowIfCancellationRequested();
                var reopenWatch = Stopwatch.StartNew();
                active = CreateSequence(artifactLayout, options);
                active.Refresh();
                reopenWatch.Stop();
                reopenRefreshMs = reopenWatch.Elapsed.TotalMilliseconds;
            }
            else
            {
                active = sequence;
                sequence = null;
            }

            sequenceCountAfterRefresh = active!.sequence.Count();
            appendOffsetAfterRefresh = active.sequence.ElementOffset();

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

            directIndexOnlyExpectedOffsets = directProbe.ExpectedCount;
            var directIndexWatch = Stopwatch.StartNew();
            directIndexOnlyHit = ExecuteIndexOnlyProbe(active, options, directProbe, out directIndexOnlyReturnedOffsets, out var directIndexMismatchReason);
            directIndexWatch.Stop();
            directIndexOnlyLookupMs = directIndexWatch.Elapsed.TotalMilliseconds;
            if (!directIndexOnlyHit && !string.IsNullOrWhiteSpace(directIndexMismatchReason))
            {
                mismatchSamples.Add("direct index-only " + directIndexMismatchReason);
            }

            directMaterializedExpectedRows = directProbe.ExpectedCount;
            var directMaterializedWatch = Stopwatch.StartNew();
            directMaterializedHit = ExecuteMaterializedProbe(active, options, directProbe, out directMaterializedReturnedRows, out var directMaterializedMismatchReason);
            directMaterializedWatch.Stop();
            directMaterializedLookupMs = directMaterializedWatch.Elapsed.TotalMilliseconds;
            if (!directMaterializedHit && !string.IsNullOrWhiteSpace(directMaterializedMismatchReason))
            {
                mismatchSamples.Add("direct materialized " + directMaterializedMismatchReason);
            }

            var probes = CreateProbes(spec, options);

            var indexOnlyWatch = Stopwatch.StartNew();
            for (var i = 0; i < probes.Length; i++)
            {
                if ((i & 0x3FF) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var probe = probes[i];
                var matched = ExecuteIndexOnlyProbe(active, options, probe, out var returnedOffsets, out var mismatchReason);
                indexOnlyReturnedOffsets += returnedOffsets;
                indexOnlyExpectedOffsets += probe.ExpectedCount;

                if (matched)
                {
                    indexOnlyProbeHits++;
                }
                else
                {
                    indexOnlyProbeMisses++;
                    if (mismatchSamples.Count < 10 && !string.IsNullOrWhiteSpace(mismatchReason))
                    {
                        mismatchSamples.Add("index-only " + mismatchReason);
                    }
                }
            }
            indexOnlyWatch.Stop();
            indexOnlyLookupMs = indexOnlyWatch.Elapsed.TotalMilliseconds;

            var materializedWatch = Stopwatch.StartNew();
            for (var i = 0; i < probes.Length; i++)
            {
                if ((i & 0x3FF) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var probe = probes[i];
                var matched = ExecuteMaterializedProbe(active, options, probe, out var returnedRows, out var mismatchReason);
                materializedReturnedRows += returnedRows;
                materializedExpectedRows += probe.ExpectedCount;

                if (matched)
                {
                    materializedProbeHits++;
                }
                else
                {
                    materializedProbeMisses++;
                    if (mismatchSamples.Count < 10 && !string.IsNullOrWhiteSpace(mismatchReason))
                    {
                        mismatchSamples.Add("materialized " + mismatchReason);
                    }
                }
            }
            materializedWatch.Stop();
            materializedLookupMs = materializedWatch.Elapsed.TotalMilliseconds;

            active.Close();
            active = null;

            semanticSuccess = directIndexOnlyHit &&
                              directMaterializedHit &&
                              indexOnlyProbeMisses == 0 &&
                              materializedProbeMisses == 0 &&
                              sequenceCountAfterRefresh == spec.Dataset.RecordCount;
            if (!semanticSuccess.Value)
            {
                semanticFailureReason = BuildSemanticFailureReason(
                    spec.Dataset.RecordCount,
                    sequenceCountAfterRefresh,
                    options.LookupCount,
                    indexOnlyProbeHits,
                    indexOnlyProbeMisses,
                    indexOnlyExpectedOffsets,
                    indexOnlyReturnedOffsets,
                    materializedProbeHits,
                    materializedProbeMisses,
                    materializedExpectedRows,
                    materializedReturnedRows,
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
            TryClose(sequence);
            TryClose(active);
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

        metrics.Add(new RunMetric { MetricKey = "directIndexOnlyLookupMs", Value = directIndexOnlyLookupMs });
        metrics.Add(new RunMetric { MetricKey = "directIndexOnlyLookupHit", Value = directIndexOnlyHit ? 1 : 0 });
        metrics.Add(new RunMetric { MetricKey = "directIndexOnlyExpectedOffsets", Value = directIndexOnlyExpectedOffsets });
        metrics.Add(new RunMetric { MetricKey = "directIndexOnlyReturnedOffsets", Value = directIndexOnlyReturnedOffsets });

        metrics.Add(new RunMetric { MetricKey = "directMaterializedLookupMs", Value = directMaterializedLookupMs });
        metrics.Add(new RunMetric { MetricKey = "directMaterializedLookupHit", Value = directMaterializedHit ? 1 : 0 });
        metrics.Add(new RunMetric { MetricKey = "directMaterializedExpectedRows", Value = directMaterializedExpectedRows });
        metrics.Add(new RunMetric { MetricKey = "directMaterializedReturnedRows", Value = directMaterializedReturnedRows });

        metrics.Add(new RunMetric { MetricKey = "indexOnlyLookupMs", Value = indexOnlyLookupMs });
        metrics.Add(new RunMetric { MetricKey = "indexOnlyProbeCount", Value = options.LookupCount });
        metrics.Add(new RunMetric { MetricKey = "indexOnlyProbeHits", Value = indexOnlyProbeHits });
        metrics.Add(new RunMetric { MetricKey = "indexOnlyProbeMisses", Value = indexOnlyProbeMisses });
        metrics.Add(new RunMetric { MetricKey = "indexOnlyReturnedOffsets", Value = indexOnlyReturnedOffsets });
        metrics.Add(new RunMetric { MetricKey = "indexOnlyExpectedOffsets", Value = indexOnlyExpectedOffsets });

        metrics.Add(new RunMetric { MetricKey = "materializedLookupMs", Value = materializedLookupMs });
        metrics.Add(new RunMetric { MetricKey = "materializedProbeCount", Value = options.LookupCount });
        metrics.Add(new RunMetric { MetricKey = "materializedProbeHits", Value = materializedProbeHits });
        metrics.Add(new RunMetric { MetricKey = "materializedProbeMisses", Value = materializedProbeMisses });
        metrics.Add(new RunMetric { MetricKey = "materializedReturnedRows", Value = materializedReturnedRows });
        metrics.Add(new RunMetric { MetricKey = "materializedExpectedRows", Value = materializedExpectedRows });

        // Compatibility aliases for existing analysis/charts.
        metrics.Add(new RunMetric { MetricKey = "directPointLookupMs", Value = directMaterializedLookupMs });
        metrics.Add(new RunMetric { MetricKey = "directPointLookupHit", Value = directMaterializedHit ? 1 : 0 });
        metrics.Add(new RunMetric { MetricKey = "directLookupExpectedRows", Value = directMaterializedExpectedRows });
        metrics.Add(new RunMetric { MetricKey = "directLookupReturnedRows", Value = directMaterializedReturnedRows });
        metrics.Add(new RunMetric { MetricKey = "lookupSeriesMs", Value = materializedLookupMs });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupMs", Value = materializedLookupMs });
        metrics.Add(new RunMetric { MetricKey = "lookupProbeCount", Value = options.LookupCount });
        metrics.Add(new RunMetric { MetricKey = "lookupCount", Value = options.LookupCount });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupCount", Value = options.LookupCount });
        metrics.Add(new RunMetric { MetricKey = "lookupProbeHits", Value = materializedProbeHits });
        metrics.Add(new RunMetric { MetricKey = "lookupHitCount", Value = materializedProbeHits });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupHits", Value = materializedProbeHits });
        metrics.Add(new RunMetric { MetricKey = "lookupProbeMisses", Value = materializedProbeMisses });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupMisses", Value = materializedProbeMisses });
        metrics.Add(new RunMetric { MetricKey = "lookupReturnedRows", Value = materializedReturnedRows });
        metrics.Add(new RunMetric { MetricKey = "lookupReturnedRowCount", Value = materializedReturnedRows });
        metrics.Add(new RunMetric { MetricKey = "lookupExpectedRows", Value = materializedExpectedRows });

        metrics.Add(new RunMetric { MetricKey = "duplicateGroupSize", Value = options.DuplicateGroupSize });
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

        diagnostics["lookupMode"] = options.Mode.ToString();
        diagnostics["lookupKeyKind"] = options.KeyKind.ToString();
        diagnostics["lookupCount"] = options.LookupCount.ToString(CultureInfo.InvariantCulture);
        diagnostics["indexOnlyLookupMs"] = indexOnlyLookupMs.ToString(CultureInfo.InvariantCulture);
        diagnostics["indexOnlyProbeHits"] = indexOnlyProbeHits.ToString(CultureInfo.InvariantCulture);
        diagnostics["indexOnlyProbeMisses"] = indexOnlyProbeMisses.ToString(CultureInfo.InvariantCulture);
        diagnostics["indexOnlyReturnedOffsets"] = indexOnlyReturnedOffsets.ToString(CultureInfo.InvariantCulture);
        diagnostics["indexOnlyExpectedOffsets"] = indexOnlyExpectedOffsets.ToString(CultureInfo.InvariantCulture);
        diagnostics["materializedLookupMs"] = materializedLookupMs.ToString(CultureInfo.InvariantCulture);
        diagnostics["materializedProbeHits"] = materializedProbeHits.ToString(CultureInfo.InvariantCulture);
        diagnostics["materializedProbeMisses"] = materializedProbeMisses.ToString(CultureInfo.InvariantCulture);
        diagnostics["materializedReturnedRows"] = materializedReturnedRows.ToString(CultureInfo.InvariantCulture);
        diagnostics["materializedExpectedRows"] = materializedExpectedRows.ToString(CultureInfo.InvariantCulture);
        diagnostics["duplicateGroupSize"] = options.DuplicateGroupSize.ToString(CultureInfo.InvariantCulture);
        diagnostics["reopenAfterBuild"] = options.ReopenAfterBuild.ToString();
        diagnostics["sequenceCountAfterRefresh"] = sequenceCountAfterRefresh.ToString(CultureInfo.InvariantCulture);
        diagnostics["sequenceAppendOffsetAfterRefresh"] = appendOffsetAfterRefresh.ToString(CultureInfo.InvariantCulture);
        diagnostics["primaryDataFileBytes"] = collected.PrimaryDataBytes.ToString(CultureInfo.InvariantCulture);
        diagnostics["indexFileBytes"] = collected.IndexBytes.ToString(CultureInfo.InvariantCulture);
        diagnostics["stateFileBytes"] = collected.StateBytes.ToString(CultureInfo.InvariantCulture);
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

        notes.Add("Lookup-series run for current-source Polar.DB adapter.");
        notes.Add("The run measures index-only lookup separately from materialized payload lookup.");
        notes.Add(options.Mode == LookupSeriesMode.ExactOne
            ? "Exact-one: index-only resolves one offset; materialized resolves and reads one object[]."
            : "All-matching: index-only resolves offset range/count; materialized resolves offsets and reads all object[] rows.");

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
                ["lookupMeasurement"] = "index-only-and-materialized"
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

    private static bool ExecuteIndexOnlyProbe(
        USequence sequence,
        LookupSeriesOptions options,
        LookupProbe probe,
        out int returnedOffsets,
        out string? mismatchReason)
    {
        returnedOffsets = 0;
        mismatchReason = null;

        if (options.Mode == LookupSeriesMode.ExactOne)
        {
            if (sequence.TryGetExactlyOneOffsetByKey(probe.Key, out _))
            {
                returnedOffsets = 1;
                return true;
            }

            returnedOffsets = sequence.CountByKey(probe.Key);
            mismatchReason = $"exact-one offset count mismatch for key={probe.Key}: returned={returnedOffsets}, expected=1";
            return false;
        }

        returnedOffsets = sequence.GetOffsetsByKey(probe.Key).Count;
        if (returnedOffsets == probe.ExpectedCount)
        {
            return true;
        }

        mismatchReason = $"all-matching offset count mismatch for key={probe.Key}: returned={returnedOffsets}, expected={probe.ExpectedCount}";
        return false;
    }

    private static bool ExecuteMaterializedProbe(
        USequence sequence,
        LookupSeriesOptions options,
        LookupProbe probe,
        out int returnedRows,
        out string? mismatchReason)
    {
        returnedRows = 0;
        mismatchReason = null;

        if (options.Mode == LookupSeriesMode.ExactOne)
        {
            try
            {
                var row = sequence.GetExactlyOneByKey(probe.Key);
                returnedRows = 1;
                if (RowHasKey(row, options.KeyKind, probe.Key))
                {
                    return true;
                }

                mismatchReason = $"exact-one returned wrong key. expected={probe.Key}";
                return false;
            }
            catch (Exception ex)
            {
                mismatchReason = $"exact-one failed for key={probe.Key}: {ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }

        var rows = sequence.GetAllByKey(probe.Key).ToArray();
        returnedRows = rows.Length;
        if (rows.Length != probe.ExpectedCount)
        {
            mismatchReason = $"all-matching count mismatch for key={probe.Key}: returned={rows.Length}, expected={probe.ExpectedCount}";
            return false;
        }

        if (rows.All(row => RowHasKey(row, options.KeyKind, probe.Key)))
        {
            return true;
        }

        mismatchReason = $"all-matching returned at least one row with wrong key. expected={probe.Key}";
        return false;
    }

    private static USequence CreateSequence(PolarDbLookupArtifactLayout layout, LookupSeriesOptions options)
    {
        var streamIndex = 0;
        var streamPaths = new[]
        {
            layout.PrimaryDataPath,
            layout.PrimaryHashKeysIndexPath,
            layout.PrimaryOffsetsIndexPath
        };

        Stream StreamGen()
        {
            if (streamIndex >= streamPaths.Length)
            {
                throw new InvalidOperationException("PolarDB lookup-series stream generator was called too many times.");
            }

            return new FileStream(
                streamPaths[streamIndex++],
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read);
        }

        return new USequence(
            CreateRecordType(options.KeyKind),
            layout.StateFilePath,
            StreamGen,
            IsEmpty,
            value => ReadKey(value, options.KeyKind),
            LookupSeriesWorkload.HashSupportedKey,
            optimise: true);
    }

    private static PTypeRecord CreateRecordType(LookupKeyKind keyKind)
    {
        var keyType = keyKind switch
        {
            LookupKeyKind.Int32 => new PType(PTypeEnumeration.integer),
            LookupKeyKind.Int64 => new PType(PTypeEnumeration.longinteger),
            LookupKeyKind.Guid => new PType(PTypeEnumeration.sstring),
            _ => throw new ArgumentOutOfRangeException(nameof(keyKind))
        };

        return new PTypeRecord(
            new NamedType("lookupKey", keyType),
            new NamedType("ordinal", new PType(PTypeEnumeration.integer)),
            new NamedType("payload", new PType(PTypeEnumeration.sstring)));
    }

    private static IEnumerable<object> GenerateRows(ExperimentSpec spec, LookupSeriesOptions options)
    {
        var seed = spec.Dataset.Seed ?? 1;
        for (var ordinal = 1; ordinal <= spec.Dataset.RecordCount; ordinal++)
        {
            var key = LookupSeriesWorkload.CreateKey(
                options.KeyKind,
                options.Mode,
                seed,
                checked((int)ordinal),
                options.DuplicateGroupSize);

            object storedKey = options.KeyKind == LookupKeyKind.Guid
                ? ((Guid)key).ToString("D")
                : key;

            yield return new object[]
            {
                storedKey,
                checked((int)ordinal),
                $"payload-{ordinal.ToString(CultureInfo.InvariantCulture)}"
            };
        }
    }

    private static bool RowHasKey(object row, LookupKeyKind keyKind, IComparable expectedKey)
    {
        return ReadKey(row, keyKind).CompareTo(expectedKey) == 0;
    }

    private static IComparable ReadKey(object value, LookupKeyKind keyKind)
    {
        if (value is not object[] row || row.Length == 0)
        {
            throw new InvalidOperationException("Lookup row must be object[] with lookupKey at index 0.");
        }

        return keyKind switch
        {
            LookupKeyKind.Int32 => row[0] switch
            {
                int value32 => value32,
                long value64 => checked((int)value64),
                _ => Convert.ToInt32(row[0], CultureInfo.InvariantCulture)
            },
            LookupKeyKind.Int64 => row[0] switch
            {
                long value64 => value64,
                int value32 => (long)value32,
                _ => Convert.ToInt64(row[0], CultureInfo.InvariantCulture)
            },
            LookupKeyKind.Guid => row[0] switch
            {
                Guid guid => guid,
                string text => Guid.Parse(text),
                _ => Guid.Parse(Convert.ToString(row[0], CultureInfo.InvariantCulture)!)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(keyKind))
        };
    }

    private static bool IsEmpty(object value)
    {
        return value is not object[] row || row.Length < 2 || row[1] is not int ordinal || ordinal <= 0;
    }

    private static string BuildSemanticFailureReason(
        long expectedCount,
        long actualCount,
        int lookupCount,
        long indexOnlyHits,
        long indexOnlyMisses,
        long indexOnlyExpectedOffsets,
        long indexOnlyReturnedOffsets,
        long materializedHits,
        long materializedMisses,
        long materializedExpectedRows,
        long materializedReturnedRows,
        IReadOnlyList<string> mismatchSamples)
    {
        return "Polar.DB lookup-series semantic check failed: " +
               $"expectedCount={expectedCount}, actualCount={actualCount}, lookupCount={lookupCount}, " +
               $"indexOnlyHits={indexOnlyHits}, indexOnlyMisses={indexOnlyMisses}, " +
               $"indexOnlyExpectedOffsets={indexOnlyExpectedOffsets}, indexOnlyReturnedOffsets={indexOnlyReturnedOffsets}, " +
               $"materializedHits={materializedHits}, materializedMisses={materializedMisses}, " +
               $"materializedExpectedRows={materializedExpectedRows}, materializedReturnedRows={materializedReturnedRows}. " +
               (mismatchSamples.Count == 0 ? string.Empty : "Samples: " + string.Join(" | ", mismatchSamples));
    }

    private static PolarDbLookupArtifactLayout CreateArtifactLayout(RunWorkspace workspace, string runId)
    {
        var root = Path.Combine(workspace.ArtifactsDirectory ?? workspace.WorkingDirectory, runId, "polar-db-current-lookup-series");
        return new PolarDbLookupArtifactLayout(
            root,
            Path.Combine(root, "sequence.polar.db"),
            Path.Combine(root, "primary.hkeys.index"),
            Path.Combine(root, "primary.offsets.index"),
            Path.Combine(root, "sequence.state"));
    }

    private static PolarDbLookupArtifactInventory CollectArtifacts(PolarDbLookupArtifactLayout layout, string relativeRoot)
    {
        var descriptors = new List<ArtifactDescriptor>();
        var total = 0L;
        var primary = 0L;
        var index = 0L;
        var state = 0L;

        if (!Directory.Exists(layout.ArtifactsRootDirectory))
        {
            return new PolarDbLookupArtifactInventory(descriptors, 0, 0, 0, 0);
        }

        foreach (var file in Directory.EnumerateFiles(layout.ArtifactsRootDirectory, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            var role = ResolveRole(file);
            var relative = Path.GetRelativePath(relativeRoot, info.FullName);
            descriptors.Add(new ArtifactDescriptor(role, relative, info.Length));
            total += info.Length;
            if (role == ArtifactRole.PrimaryData) primary += info.Length;
            if (role == ArtifactRole.SecondaryIndex) index += info.Length;
            if (role == ArtifactRole.State) state += info.Length;
        }

        return new PolarDbLookupArtifactInventory(descriptors, total, primary, index, state);
    }

    private static ArtifactRole ResolveRole(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        if (name.Contains("state", StringComparison.OrdinalIgnoreCase)) return ArtifactRole.State;
        if (name.Contains("index", StringComparison.OrdinalIgnoreCase)) return ArtifactRole.SecondaryIndex;
        return ArtifactRole.PrimaryData;
    }

    private static void TryClose(USequence? sequence)
    {
        try
        {
            sequence?.Close();
        }
        catch
        {
            // Cleanup path must not hide the original benchmark failure.
        }
    }

    private sealed record PolarDbLookupArtifactLayout(
        string ArtifactsRootDirectory,
        string PrimaryDataPath,
        string PrimaryHashKeysIndexPath,
        string PrimaryOffsetsIndexPath,
        string StateFilePath);

    private sealed record PolarDbLookupArtifactInventory(
        IReadOnlyList<ArtifactDescriptor> Descriptors,
        long TotalBytes,
        long PrimaryDataBytes,
        long IndexBytes,
        long StateBytes);
}
