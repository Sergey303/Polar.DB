using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Polar.DB;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;

namespace Polar.DB.Bench.Exec.PolarDbNuget;

public static class Program
{
    private const string WarmupRunRole = "warmup";
    private const string MeasuredRunRole = "measured";

    public static async Task<int> Main(string[] args)
    {
        RunnerOptions options;
        try
        {
            options = RunnerOptions.Parse(args);
            options.Validate();
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(RunnerOptions.UsageText);
            return 2;
        }

        try
        {
            return await RunAsync(options);
        }
        catch (NotSupportedException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static async Task<int> RunAsync(RunnerOptions options)
    {
        var spec = await LocalExperimentSpecLoader.LoadAsync(options.SpecPath!, options.EngineKey);
        var (engineFamily, runtime) = EngineRuntimeResolver.Resolve(spec);

        if (!engineFamily.Equals("polar-db", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Polar.DB NuGet runner can execute only polar-db targets, got '{engineFamily}'.");
        }

        if (string.IsNullOrWhiteSpace(spec.Nuget))
        {
            throw new InvalidOperationException(
                $"Target '{spec.TargetKey}' is not a pinned NuGet target. Use the regular executor for source-current.");
        }

        if (!string.Equals(runtime.Source, "nuget-pinned", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Target '{spec.TargetKey}' did not resolve to nuget-pinned runtime.");
        }

        var rawResultsDirectory = LocalExperimentSpecLoader.ResolveRawResultsDirectory(
            options.SpecPath!,
            options.RawResultsDirectory);

        Directory.CreateDirectory(rawResultsDirectory);
        Directory.CreateDirectory(options.WorkingDirectory!);

        var repositoryRoot =
            ResolveRepositoryRoot(options.WorkingDirectory!)
            ?? ResolveRepositoryRoot(Environment.CurrentDirectory)
            ?? Path.GetFullPath(Path.Combine(options.WorkingDirectory!, ".."));

        var workspace = new RunWorkspace
        {
            RootDirectory = repositoryRoot,
            WorkingDirectory = options.WorkingDirectory!,
            RawResultsDirectory = rawResultsDirectory,
            EnvironmentClass = options.EnvironmentClass,
            ArtifactsDirectory = Path.Combine(options.WorkingDirectory!, "artifacts")
        };

        Directory.CreateDirectory(workspace.ArtifactsDirectory);

        var executionPlan = BuildExecutionPlan(options);
        var measuredResults = new List<RunResult>(executionPlan.MeasuredCount);

        Console.WriteLine(
            $"Polar.DB NuGet runner: target='{spec.TargetKey}', package='{spec.Nuget}', assembly='{typeof(PType).Assembly.Location}'");

        for (var i = 0; i < executionPlan.TotalCount; i++)
        {
            var runRole = i < executionPlan.WarmupCount ? WarmupRunRole : MeasuredRunRole;
            var sequenceNumber = i + 1;

            var runner = new PolarDbNugetRun(spec, workspace, runtime);
            var rawResult = runner.Execute(CancellationToken.None);
            var taggedResult = AttachSeriesInfo(
                rawResult,
                executionPlan.ComparisonSetId,
                sequenceNumber,
                runRole,
                executionPlan.WarmupCount,
                executionPlan.MeasuredCount);

            taggedResult = AttachEngineRuntimeInfo(taggedResult, spec.TargetKey, runtime);

            var rawPath = BuildRawPath(
                workspace,
                taggedResult,
                runRole,
                sequenceNumber,
                executionPlan.TotalCount > 1);

            await using var stream = File.Create(rawPath);
            await JsonSerializer.SerializeAsync(stream, taggedResult, JsonDefaults.Default);

            Console.WriteLine($"Raw result written: {rawPath}");

            if (string.Equals(runRole, MeasuredRunRole, StringComparison.OrdinalIgnoreCase))
            {
                measuredResults.Add(taggedResult);
            }
        }

        return measuredResults.All(x => x.TechnicalSuccess) ? 0 : 1;
    }

    private static SeriesExecutionPlan BuildExecutionPlan(RunnerOptions options)
    {
        var hasComparisonSet = !string.IsNullOrWhiteSpace(options.ComparisonSetId);
        var warmupCount = options.WarmupCount ?? (hasComparisonSet ? 1 : 0);
        var measuredCount = options.MeasuredCount ?? (hasComparisonSet ? 3 : 1);

        if (warmupCount < 0)
            throw new InvalidOperationException("--warmup-count must be >= 0.");

        if (measuredCount <= 0)
            throw new InvalidOperationException("--measured-count must be >= 1.");

        var comparisonSetId = options.ComparisonSetId;
        if (string.IsNullOrWhiteSpace(comparisonSetId) && warmupCount + measuredCount > 1)
        {
            comparisonSetId = $"auto-{DateTimeOffset.UtcNow:yyyyMMddTHHmmss}";
        }

        return new SeriesExecutionPlan(
            comparisonSetId,
            warmupCount,
            measuredCount,
            warmupCount + measuredCount);
    }

    private static RunResult AttachSeriesInfo(
        RunResult result,
        string? comparisonSetId,
        int sequenceNumber,
        string runRole,
        int warmupCount,
        int measuredCount)
    {
        var tags = result.Tags is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(result.Tags, StringComparer.OrdinalIgnoreCase);

        tags["warmupCount"] = warmupCount.ToString(CultureInfo.InvariantCulture);
        tags["measuredCount"] = measuredCount.ToString(CultureInfo.InvariantCulture);
        tags["role"] = runRole;

        return result with
        {
            ComparisonSetId = comparisonSetId,
            RunSeriesSequenceNumber = sequenceNumber,
            RunRole = runRole,
            Tags = tags
        };
    }

    private static RunResult AttachEngineRuntimeInfo(
        RunResult result,
        string targetKey,
        EngineRuntimeDescriptor runtime)
    {
        var diagnostics = result.EngineDiagnostics is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(result.EngineDiagnostics, StringComparer.OrdinalIgnoreCase);

        diagnostics["runtimeSource"] = runtime.Source;
        if (!string.IsNullOrWhiteSpace(runtime.Nuget))
            diagnostics["runtimeNuget"] = runtime.Nuget;

        diagnostics["polarDbAssemblyLocation"] = typeof(PType).Assembly.Location;
        diagnostics["polarDbAssemblyVersion"] = typeof(PType).Assembly.GetName().Version?.ToString() ?? "unknown";
        diagnostics["runnerKind"] = "external-polar-db-nuget-compatible";

        return result with
        {
            EngineKey = targetKey,
            Runtime = runtime,
            EngineDiagnostics = diagnostics
        };
    }

    private static string BuildRawPath(
        RunWorkspace workspace,
        RunResult runResult,
        string runRole,
        int sequenceNumber,
        bool includeSeriesSuffix)
    {
        var timestampToken = runResult.TimestampUtc.ToString("yyyy-MM-ddTHH-mm-ssZ");
        var fileName = includeSeriesSuffix
            ? $"{timestampToken}__{runResult.EngineKey}__{runRole}-{sequenceNumber:D2}.run.json"
            : $"{timestampToken}__{runResult.EngineKey}.run.json";
        var rawPath = Path.Combine(workspace.RawResultsDirectory, fileName);

        if (!File.Exists(rawPath))
            return rawPath;

        var ext = ".run.json";
        var baseName = rawPath[..^ext.Length];
        var attempt = 2;
        var candidate = $"{baseName}.v{attempt}{ext}";
        while (File.Exists(candidate))
        {
            attempt++;
            candidate = $"{baseName}.v{attempt}{ext}";
        }

        return candidate;
    }

    private static string? ResolveRepositoryRoot(string startDirectory)
    {
        var fullPath = Path.GetFullPath(startDirectory);
        var directory = new DirectoryInfo(fullPath);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
                return directory.FullName;

            directory = directory.Parent;
        }

        return null;
    }

    private readonly record struct SeriesExecutionPlan(
        string? ComparisonSetId,
        int WarmupCount,
        int MeasuredCount,
        int TotalCount);

    private sealed record RunnerOptions
    {
        public static string UsageText =>
            "Usage: --spec <experiment.json|experiment-folder> --work <dir> --engine <target-key> " +
            "[--raw-out <dir>] [--env <class>] [--comparison-set <id>] " +
            "[--warmup-count <n>] [--measured-count <n>]";

        public string? EngineKey { get; init; }
        public string? SpecPath { get; init; }
        public string? WorkingDirectory { get; init; }
        public string? RawResultsDirectory { get; init; }
        public string EnvironmentClass { get; init; } = "local";
        public string? ComparisonSetId { get; init; }
        public int? WarmupCount { get; init; }
        public int? MeasuredCount { get; init; }

        public static RunnerOptions Parse(string[] args)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < args.Length - 1; i += 2)
            {
                map[args[i]] = args[i + 1];
            }

            return new RunnerOptions
            {
                EngineKey = map.GetValueOrDefault("--engine"),
                SpecPath = map.GetValueOrDefault("--spec"),
                WorkingDirectory = map.GetValueOrDefault("--work"),
                RawResultsDirectory = map.GetValueOrDefault("--raw-out"),
                EnvironmentClass = map.GetValueOrDefault("--env") ?? "local",
                ComparisonSetId = map.GetValueOrDefault("--comparison-set"),
                WarmupCount = ParseOptionalNonNegativeInt(map, "--warmup-count"),
                MeasuredCount = ParseOptionalPositiveInt(map, "--measured-count")
            };
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(SpecPath))
                throw new InvalidOperationException("Missing --spec path.");

            _ = LocalExperimentSpecLoader.ResolveSpecPath(SpecPath);

            if (string.IsNullOrWhiteSpace(EngineKey))
                throw new InvalidOperationException("Missing --engine target key.");

            if (string.IsNullOrWhiteSpace(WorkingDirectory))
                throw new InvalidOperationException("Missing --work.");

            _ = LocalExperimentSpecLoader.ResolveRawResultsDirectory(SpecPath, RawResultsDirectory);

            if (WarmupCount is < 0)
                throw new InvalidOperationException("--warmup-count must be >= 0.");

            if (MeasuredCount is <= 0)
                throw new InvalidOperationException("--measured-count must be >= 1.");
        }

        private static int? ParseOptionalNonNegativeInt(IReadOnlyDictionary<string, string> map, string key)
        {
            if (!map.TryGetValue(key, out var text))
                return null;

            if (int.TryParse(text, out var value) && value >= 0)
                return value;

            return -1;
        }

        private static int? ParseOptionalPositiveInt(IReadOnlyDictionary<string, string> map, string key)
        {
            if (!map.TryGetValue(key, out var text))
                return null;

            if (int.TryParse(text, out var value) && value > 0)
                return value;

            return 0;
        }
    }
}

internal sealed class PolarDbNugetRun
{
    private static readonly PTypeRecord PersonRecordType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("age", new PType(PTypeEnumeration.integer)));

    private const string FullCoverageExperimentKey = "persons-full-adapter-coverage-version-matrix";
    private const string AppendCyclesExperimentKey = "persons-append-cycles-reopen-lookup";
    private const int DefaultLookupCount = 10_000;
    private const int DefaultRandomLookupPerBatch = 5_000;
    private const int CommonLookupSeedSalt = unchecked((int)0x5f3759df);

    private readonly ExperimentSpec _spec;
    private readonly RunWorkspace _workspace;
    private readonly EngineRuntimeDescriptor _runtime;

    public PolarDbNugetRun(ExperimentSpec spec, RunWorkspace workspace, EngineRuntimeDescriptor runtime)
    {
        _spec = spec;
        _workspace = workspace;
        _runtime = runtime;
    }

    public RunResult Execute(CancellationToken cancellationToken)
    {
        var manifest = EnvironmentCollector.Collect(
            environmentClass: _workspace.EnvironmentClass,
            repositoryRoot: _workspace.RootDirectory);

        var runId = RunIdFactory.Create(
            _spec.ExperimentKey,
            _spec.Dataset.ProfileKey,
            _spec.TargetKey,
            manifest.EnvironmentClass);

        var timestampUtc = DateTimeOffset.UtcNow;
        var artifactLayout = CreateArtifactLayout(_workspace, runId);
        Directory.CreateDirectory(artifactLayout.ArtifactsRootDirectory);

        var metrics = new List<RunMetric>();
        var notes = new List<string>();
        var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var loadMs = 0.0;
        var buildMs = 0.0;
        var reopenRefreshMs = 0.0;
        var directLookupMs = 0.0;
        var randomLookupMs = 0.0;
        var appendMs = 0.0;
        var lookupHits = 0L;
        var lookupAttempts = 0L;
        var directLookupKey = ResolveDirectLookupKey(_spec.Dataset.RecordCount);
        var directLookupHit = false;
        var expectedCount = _spec.Dataset.RecordCount;
        var rowCountMismatches = new List<string>();
        var cycleArtifactBytes = new List<long>();
        var initialArtifactBytes = 0L;

        var lookupCount = ResolveLookupCount(_spec.Workload);
        var appendCycleShape = ResolveAppendCycleShape(_spec.Workload);
        var lookupCountPerCycle = ResolveIntOption(_spec.Workload, "randomLookupPerBatch", DefaultRandomLookupPerBatch, 1);
        var directLookupEnabled = ResolveBooleanOption(_spec.Workload, "directLookup", fallback: true);
        var reopenAfterInitialLoad = ResolveBooleanOption(_spec.Workload, "reopenAfterInitialLoad", fallback: true);
        var reopenAfterEachBatch = ResolveBooleanOption(_spec.Workload, "reopenAfterEachBatch", fallback: true);
        var randomLookupAfterEachBatch = ResolveBooleanOption(_spec.Workload, "randomLookupAfterEachBatch", fallback: true);

        var managedBefore = GC.GetTotalMemory(forceFullCollection: false);
        var totalStopwatch = Stopwatch.StartNew();
        var technicalSuccess = true;
        string? technicalFailureReason = null;
        bool? semanticSuccess = null;
        string? semanticFailureReason = null;

        USequence? active = null;
        USequence? reopened = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            active = CreateSequence(artifactLayout);

            var loadWatch = Stopwatch.StartNew();
            active.Load(GeneratePersons(_spec.Dataset.RecordCount, _spec.Dataset.Seed ?? 1));
            loadWatch.Stop();
            loadMs = loadWatch.Elapsed.TotalMilliseconds;

            cancellationToken.ThrowIfCancellationRequested();

            var buildWatch = Stopwatch.StartNew();
            active.Build();
            buildWatch.Stop();
            buildMs = buildWatch.Elapsed.TotalMilliseconds;

            if (reopenAfterInitialLoad || IsAppendCyclesExperiment())
            {
                active.Close();
                active = null;

                var initialCollected = CollectArtifacts(artifactLayout, _workspace.WorkingDirectory);
                initialArtifactBytes = initialCollected.TotalBytes;
                cycleArtifactBytes.Add(initialCollected.TotalBytes);

                var initialReopenWatch = Stopwatch.StartNew();
                reopened = CreateSequence(artifactLayout);
                reopened.Refresh();
                initialReopenWatch.Stop();
                reopenRefreshMs += initialReopenWatch.Elapsed.TotalMilliseconds;

                active = reopened;
                reopened = null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (directLookupEnabled && !IsAppendCyclesExperiment())
            {
                var directLookupWatch = Stopwatch.StartNew();
                var directRow = active!.GetByKey(directLookupKey);
                directLookupWatch.Stop();
                directLookupMs = directLookupWatch.Elapsed.TotalMilliseconds;
                directLookupHit = TryReadPersonKey(directRow, out var directRowKey) && directRowKey == directLookupKey;
            }

            if (!IsAppendCyclesExperiment())
            {
                var initialLookup = ExecuteRandomLookups(
                    active!,
                    lookupCount,
                    checked((int)_spec.Dataset.RecordCount) + 1,
                    (_spec.Dataset.Seed ?? 1) ^ CommonLookupSeedSalt,
                    cancellationToken);
                randomLookupMs += initialLookup.ElapsedMs;
                lookupHits += initialLookup.Hits;
                lookupAttempts += lookupCount;
            }

            var appendRandom = new Random((_spec.Dataset.Seed ?? 1) ^ 0x55aa12ef);
            for (var cycle = 0; cycle < appendCycleShape.BatchCount; cycle++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (active is null)
                {
                    active = CreateSequence(artifactLayout);
                    active.Refresh();
                }

                var appendWatch = Stopwatch.StartNew();
                var firstId = checked((int)(expectedCount + 1));
                AppendBatchOldApi(active, firstId, appendCycleShape.BatchSize, appendRandom, cancellationToken);
                appendWatch.Stop();
                appendMs += appendWatch.Elapsed.TotalMilliseconds;
                expectedCount += appendCycleShape.BatchSize;

                if (reopenAfterEachBatch || IsAppendCyclesExperiment())
                {
                    active.Close();
                    active = null;

                    var reopenWatch = Stopwatch.StartNew();
                    reopened = CreateSequence(artifactLayout);
                    reopened.Refresh();
                    reopenWatch.Stop();
                    reopenRefreshMs += reopenWatch.Elapsed.TotalMilliseconds;

                    active = reopened;
                    reopened = null;
                }

                if (randomLookupAfterEachBatch || IsAppendCyclesExperiment())
                {
                    var cycleLookup = ExecuteRandomLookups(
                        active!,
                        lookupCountPerCycle,
                        checked((int)expectedCount) + 1,
                        (_spec.Dataset.Seed ?? 1) ^ (0x6e7f0000 + cycle),
                        cancellationToken);
                    randomLookupMs += cycleLookup.ElapsedMs;
                    lookupHits += cycleLookup.Hits;
                    lookupAttempts += lookupCountPerCycle;
                }

                var cycleCollected = CollectArtifacts(artifactLayout, _workspace.WorkingDirectory);
                cycleArtifactBytes.Add(cycleCollected.TotalBytes);
            }

            active?.Close();
            active = null;

            var directLookupSemanticSuccess = IsAppendCyclesExperiment() || !directLookupEnabled || directLookupHit;
            semanticSuccess = directLookupSemanticSuccess &&
                              lookupHits == lookupAttempts &&
                              rowCountMismatches.Count == 0;

            if (!semanticSuccess.Value)
            {
                semanticFailureReason = BuildSemanticFailureReason(
                    directLookupEnabled,
                    directLookupKey,
                    directLookupHit,
                    lookupAttempts,
                    lookupHits,
                    rowCountMismatches,
                    expectedCount);
            }
        }
        catch (Exception ex)
        {
            technicalSuccess = false;
            technicalFailureReason = ex.ToString();
        }
        finally
        {
            TryClose(active);
            TryClose(reopened);
            totalStopwatch.Stop();
        }

        var managedAfter = GC.GetTotalMemory(forceFullCollection: false);
        var gcInfo = GC.GetGCMemoryInfo();
        var processPeakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64;
        var collectedFinal = CollectArtifacts(artifactLayout, _workspace.WorkingDirectory);
        var artifactGrowthBytes = Math.Max(0L, collectedFinal.TotalBytes - initialArtifactBytes);
        var state = TryReadState(artifactLayout.StateFilePath);

        metrics.Add(new RunMetric { MetricKey = "elapsedMsTotal", Value = totalStopwatch.Elapsed.TotalMilliseconds });
        metrics.Add(new RunMetric { MetricKey = "elapsedMsSingleRun", Value = totalStopwatch.Elapsed.TotalMilliseconds });
        metrics.Add(new RunMetric { MetricKey = "loadMs", Value = loadMs });
        metrics.Add(new RunMetric { MetricKey = "buildMs", Value = buildMs });
        metrics.Add(new RunMetric { MetricKey = "reopenRefreshMs", Value = reopenRefreshMs });
        metrics.Add(new RunMetric { MetricKey = "directPointLookupMs", Value = directLookupMs });
        metrics.Add(new RunMetric { MetricKey = "directPointLookupKey", Value = directLookupKey });
        metrics.Add(new RunMetric { MetricKey = "directPointLookupHit", Value = directLookupHit ? 1 : 0 });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupMs", Value = randomLookupMs });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupCount", Value = lookupAttempts });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupHits", Value = lookupHits });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupMisses", Value = Math.Max(0L, lookupAttempts - lookupHits) });
        metrics.Add(new RunMetric { MetricKey = "appendMs", Value = appendMs });
        metrics.Add(new RunMetric { MetricKey = "appendBatchCount", Value = appendCycleShape.BatchCount });
        metrics.Add(new RunMetric { MetricKey = "appendBatchSize", Value = appendCycleShape.BatchSize });
        metrics.Add(new RunMetric { MetricKey = "lookupCountPerCycle", Value = lookupCountPerCycle });
        metrics.Add(new RunMetric { MetricKey = "initialArtifactBytes", Value = initialArtifactBytes });
        metrics.Add(new RunMetric { MetricKey = "artifactGrowthBytes", Value = artifactGrowthBytes });
        metrics.Add(new RunMetric { MetricKey = "totalArtifactBytes", Value = collectedFinal.TotalBytes });
        metrics.Add(new RunMetric { MetricKey = "primaryDataBytes", Value = collectedFinal.PrimaryDataBytes });
        metrics.Add(new RunMetric { MetricKey = "indexBytes", Value = collectedFinal.IndexBytes });
        metrics.Add(new RunMetric { MetricKey = "stateFileBytes", Value = collectedFinal.StateBytes });
        metrics.Add(new RunMetric { MetricKey = "managedBytesBefore", Value = managedBefore });
        metrics.Add(new RunMetric { MetricKey = "managedBytesAfter", Value = managedAfter });
        metrics.Add(new RunMetric { MetricKey = "managedBytesDelta", Value = managedAfter - managedBefore });
        metrics.Add(new RunMetric { MetricKey = "heapSizeBytes", Value = gcInfo.HeapSizeBytes });
        metrics.Add(new RunMetric { MetricKey = "fragmentedBytes", Value = gcInfo.FragmentedBytes });
        metrics.Add(new RunMetric { MetricKey = "processPeakWorkingSetBytes", Value = processPeakWorkingSet });

        diagnostics["fairnessProfileApplied"] = _spec.FairnessProfile?.FairnessProfileKey ?? "unspecified";
        diagnostics["appendBatchCount"] = appendCycleShape.BatchCount.ToString(CultureInfo.InvariantCulture);
        diagnostics["appendBatchSize"] = appendCycleShape.BatchSize.ToString(CultureInfo.InvariantCulture);
        diagnostics["lookupCountPerCycle"] = lookupCountPerCycle.ToString(CultureInfo.InvariantCulture);
        diagnostics["lookupCount"] = lookupAttempts.ToString(CultureInfo.InvariantCulture);
        diagnostics["lookupHitCount"] = lookupHits.ToString(CultureInfo.InvariantCulture);
        diagnostics["expectedCountAfterCycles"] = expectedCount.ToString(CultureInfo.InvariantCulture);
        diagnostics["initialArtifactBytes"] = initialArtifactBytes.ToString(CultureInfo.InvariantCulture);
        diagnostics["artifactGrowthBytes"] = artifactGrowthBytes.ToString(CultureInfo.InvariantCulture);
        diagnostics["cycleArtifactBytes"] = string.Join(",", cycleArtifactBytes);
        diagnostics["stateFileBytes"] = ToInvariant(collectedFinal.StateBytes);
        diagnostics["indexFileBytes"] = ToInvariant(collectedFinal.IndexBytes);
        diagnostics["primaryDataFileBytes"] = ToInvariant(collectedFinal.PrimaryDataBytes);
        diagnostics["totalArtifactBytes"] = ToInvariant(collectedFinal.TotalBytes);
        diagnostics["directLookupKey"] = directLookupKey.ToString(CultureInfo.InvariantCulture);
        diagnostics["directLookupHit"] = directLookupHit.ToString();
        diagnostics["directLookupMs"] = ToInvariant(directLookupMs);
        diagnostics["directLookupEnabled"] = directLookupEnabled.ToString();
        diagnostics["reopenAfterInitialLoad"] = reopenAfterInitialLoad.ToString();
        diagnostics["reopenAfterEachBatch"] = reopenAfterEachBatch.ToString();
        diagnostics["randomLookupAfterEachBatch"] = randomLookupAfterEachBatch.ToString();
        diagnostics["semanticSuccess"] = semanticSuccess?.ToString() ?? "not-evaluated";
        diagnostics["polarDbAssemblyLocation"] = typeof(PType).Assembly.Location;
        diagnostics["polarDbAssemblyVersion"] = typeof(PType).Assembly.GetName().Version?.ToString() ?? "unknown";
        diagnostics["runtimeSource"] = _runtime.Source;
        diagnostics["runnerKind"] = "external-polar-db-nuget-compatible";

        if (!string.IsNullOrWhiteSpace(_runtime.Nuget))
            diagnostics["runtimeNuget"] = _runtime.Nuget;

        if (state.HasValue)
        {
            diagnostics["stateCount"] = state.Value.Count.ToString(CultureInfo.InvariantCulture);
            diagnostics["stateAppendOffset"] = state.Value.AppendOffset.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(semanticFailureReason))
            diagnostics["semanticFailureReason"] = semanticFailureReason;

        if (!technicalSuccess && !string.IsNullOrWhiteSpace(technicalFailureReason))
            diagnostics["technicalFailureReason"] = technicalFailureReason;

        notes.Add("External Polar.DB NuGet runner using only the public USequence API available in pinned packages.");
        notes.Add("NuGet compatibility path intentionally appends through USequence.AppendElement because old packages do not expose AppendElements and hide the underlying sequence field.");

        return new RunResult
        {
            RunId = runId,
            TimestampUtc = timestampUtc,
            EngineKey = _spec.TargetKey,
            ExperimentKey = _spec.ExperimentKey,
            DatasetProfileKey = _spec.Dataset.ProfileKey,
            FairnessProfileKey = _spec.FairnessProfile?.FairnessProfileKey ?? "unspecified",
            Environment = manifest,
            Runtime = _runtime,
            TechnicalSuccess = technicalSuccess,
            TechnicalFailureReason = technicalFailureReason,
            SemanticSuccess = semanticSuccess,
            SemanticFailureReason = semanticFailureReason,
            Metrics = metrics,
            Artifacts = collectedFinal.Descriptors,
            EngineDiagnostics = diagnostics,
            Tags = new Dictionary<string, string>
            {
                ["research"] = _spec.ResearchQuestionId ?? string.Empty,
                ["hypothesis"] = _spec.HypothesisId ?? string.Empty
            },
            Notes = notes
        };
    }

    private bool IsAppendCyclesExperiment()
    {
        return _spec.ExperimentKey.Equals(AppendCyclesExperimentKey, StringComparison.OrdinalIgnoreCase) ||
               _spec.Workload.WorkloadKey.Equals("append-cycles", StringComparison.OrdinalIgnoreCase);
    }

    private static USequence CreateSequence(PolarDbArtifactLayout artifactLayout)
    {
        var nextStream = 0;
        Stream StreamGen()
        {
            var path = nextStream switch
            {
                0 => artifactLayout.PrimaryDataFilePath,
                1 => artifactLayout.PrimaryKeyIndexHashFilePath,
                2 => artifactLayout.PrimaryKeyIndexOffsetFilePath,
                _ => Path.Combine(artifactLayout.ArtifactsRootDirectory, $"f{nextStream}.bin")
            };

            nextStream++;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        return new USequence(
            PersonRecordType,
            artifactLayout.StateFilePath,
            StreamGen,
            IsEmptyPersonRecord,
            PersonKey,
            HashKey,
            optimise: true);
    }

    private static bool IsEmptyPersonRecord(object value)
    {
        return value is object[] row && row.Length > 0 && row[0] is int id && id <= 0;
    }

    private static IComparable PersonKey(object value)
    {
        if (value is object[] row && row.Length > 0 && row[0] is int id)
            return id;

        throw new InvalidDataException("Person record has no integer id at field 0.");
    }

    private static int HashKey(IComparable key)
    {
        return key is int id ? id : key.GetHashCode();
    }

    private static IEnumerable<object> GeneratePersons(long count, int seed)
    {
        var random = new Random(seed);
        for (var id = checked((int)count); id >= 1; id--)
        {
            yield return CreatePersonRecord(id, random);
        }
    }

    private static void AppendBatchOldApi(
        USequence sequence,
        int firstId,
        int count,
        Random random,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < count; i++)
        {
            if ((i & 0x3FF) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            sequence.AppendElement(CreatePersonRecord(checked(firstId + i), random));
        }
    }

    private static object CreatePersonRecord(int id, Random random)
    {
        // Two ASCII chars before the decimal id keep the same primary data byte shape as the current benchmark adapter:
        // 4-byte id + BinaryWriter string(length-prefixed UTF-8) + 4-byte age.
        return new object[]
        {
            id,
            "p-" + id.ToString(CultureInfo.InvariantCulture),
            random.Next(18, 90)
        };
    }

    private static LookupResult ExecuteRandomLookups(
        USequence sequence,
        int lookupCount,
        int maxKeyExclusive,
        int seed,
        CancellationToken cancellationToken)
    {
        var random = new Random(seed);
        var hits = 0L;
        var watch = Stopwatch.StartNew();

        for (var i = 0; i < lookupCount; i++)
        {
            if ((i & 0x3FF) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            var key = random.Next(1, maxKeyExclusive);
            var row = sequence.GetByKey(key);
            if (TryReadPersonKey(row, out var rowKey) && rowKey == key)
                hits++;
        }

        watch.Stop();
        return new LookupResult(watch.Elapsed.TotalMilliseconds, hits);
    }

    private static bool TryReadPersonKey(object? row, out int key)
    {
        if (row is object[] values && values.Length > 0 && values[0] is int id)
        {
            key = id;
            return true;
        }

        key = 0;
        return false;
    }

    private static int ResolveDirectLookupKey(long recordCount)
    {
        if (recordCount <= 0) return 1;
        return checked((int)Math.Max(1L, recordCount / 2L));
    }

    private static int ResolveLookupCount(WorkloadSpec workload)
    {
        return workload.LookupCount is > 0 ? workload.LookupCount.Value : DefaultLookupCount;
    }

    private static AppendCycleShape ResolveAppendCycleShape(WorkloadSpec workload)
    {
        return new AppendCycleShape(
            workload.BatchCount is > 0 ? workload.BatchCount.Value : 0,
            workload.BatchSize is > 0 ? workload.BatchSize.Value : 0);
    }

    private static int ResolveIntOption(WorkloadSpec workload, string key, int fallback, int minimum)
    {
        if (workload.Parameters is not null &&
            workload.Parameters.TryGetValue(key, out var raw) &&
            int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return Math.Max(minimum, value);
        }

        return fallback;
    }

    private static bool ResolveBooleanOption(WorkloadSpec workload, string key, bool fallback)
    {
        if (workload.Parameters is not null &&
            workload.Parameters.TryGetValue(key, out var raw) &&
            bool.TryParse(raw, out var value))
        {
            return value;
        }

        return fallback;
    }

    private static PolarDbArtifactLayout CreateArtifactLayout(RunWorkspace workspace, string runId)
    {
        var root = Path.Combine(workspace.ArtifactsDirectory, "polar-db", runId);
        return new PolarDbArtifactLayout(
            root,
            Path.Combine(root, "f0.bin"),
            Path.Combine(root, "f1.bin"),
            Path.Combine(root, "f2.bin"),
            Path.Combine(root, "state.bin"));
    }

    private static CollectedArtifacts CollectArtifacts(PolarDbArtifactLayout layout, string workingDirectory)
    {
        var descriptors = new List<ArtifactDescriptor>();
        long primaryBytes = 0L;
        long indexBytes = 0L;
        long stateBytes = 0L;

        Add(layout.PrimaryDataFilePath, ArtifactRole.PrimaryData, "Polar.DB primary sequence data", ref primaryBytes);
        Add(layout.PrimaryKeyIndexHashFilePath, ArtifactRole.SecondaryIndex, "Polar.DB primary-key index segment", ref indexBytes);
        Add(layout.PrimaryKeyIndexOffsetFilePath, ArtifactRole.SecondaryIndex, "Polar.DB primary-key index segment", ref indexBytes);
        Add(layout.StateFilePath, ArtifactRole.State, "Polar.DB state sidecar", ref stateBytes);

        return new CollectedArtifacts(
            descriptors,
            primaryBytes + indexBytes + stateBytes,
            primaryBytes,
            indexBytes,
            stateBytes);

        void Add(string path, ArtifactRole role, string notes, ref long bucket)
        {
            if (!File.Exists(path)) return;

            var bytes = new FileInfo(path).Length;
            bucket += bytes;
            descriptors.Add(new ArtifactDescriptor(
                role,
                Path.GetRelativePath(workingDirectory, path).Replace(Path.DirectorySeparatorChar, '/'),
                bytes,
                notes));
        }
    }

    private static (long Count, long AppendOffset)? TryReadState(string stateFilePath)
    {
        if (!File.Exists(stateFilePath)) return null;
        if (new FileInfo(stateFilePath).Length < sizeof(long) * 2) return null;

        using var stream = new FileStream(stateFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(stream);
        return (reader.ReadInt64(), reader.ReadInt64());
    }

    private static void TryClose(USequence? sequence)
    {
        if (sequence is null) return;
        try { sequence.Close(); }
        catch { }
    }

    private static string BuildSemanticFailureReason(
        bool directLookupEnabled,
        int directLookupKey,
        bool directLookupHit,
        long lookupAttempts,
        long lookupHits,
        IReadOnlyCollection<string> rowCountMismatches,
        long expectedCount)
    {
        var parts = new List<string>();
        if (directLookupEnabled && !directLookupHit)
            parts.Add($"direct lookup key {directLookupKey} missed");

        if (lookupHits != lookupAttempts)
            parts.Add($"random lookup hits {lookupHits}/{lookupAttempts}");

        if (rowCountMismatches.Count > 0)
            parts.Add("row count mismatches: " + string.Join(" | ", rowCountMismatches));

        parts.Add($"expected count after cycles: {expectedCount}");
        return string.Join("; ", parts);
    }

    private static string ToInvariant(double value) => value.ToString(CultureInfo.InvariantCulture);
    private static string ToInvariant(long value) => value.ToString(CultureInfo.InvariantCulture);

    private readonly record struct AppendCycleShape(int BatchCount, int BatchSize);
    private readonly record struct LookupResult(double ElapsedMs, long Hits);

    private sealed record PolarDbArtifactLayout(
        string ArtifactsRootDirectory,
        string PrimaryDataFilePath,
        string PrimaryKeyIndexHashFilePath,
        string PrimaryKeyIndexOffsetFilePath,
        string StateFilePath);

    private sealed record CollectedArtifacts(
        IReadOnlyList<ArtifactDescriptor> Descriptors,
        long TotalBytes,
        long PrimaryDataBytes,
        long IndexBytes,
        long StateBytes);
}

internal static class LocalExperimentSpecLoader
{
    private const string ManifestFileName = "experiment.json";
    private const string RawDirectoryName = "raw";

    public static async Task<ExperimentSpec> LoadAsync(
        string specPath,
        string? cliTarget,
        CancellationToken cancellationToken = default)
    {
        var resolvedSpecPath = ResolveSpecPath(specPath);

        await using var stream = File.OpenRead(resolvedSpecPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Experiment spec JSON must be an object.");

        if (document.RootElement.TryGetProperty("targets", out _))
        {
            var manifest = document.RootElement.Deserialize<ExperimentManifest>(JsonDefaults.Default)
                           ?? throw new InvalidOperationException("Failed to deserialize experiment manifest.");

            return ConvertManifestToSpec(manifest, cliTarget);
        }

        return document.RootElement.Deserialize<ExperimentSpec>(JsonDefaults.Default)
               ?? throw new InvalidOperationException("Failed to deserialize experiment spec.");
    }

    public static string ResolveSpecPath(string specPath)
    {
        if (string.IsNullOrWhiteSpace(specPath))
            throw new InvalidOperationException("Missing --spec path.");

        if (Directory.Exists(specPath))
        {
            var manifestPath = Path.Combine(specPath, ManifestFileName);
            if (File.Exists(manifestPath))
                return manifestPath;

            throw new InvalidOperationException(
                $"Experiment directory '{specPath}' does not contain '{ManifestFileName}'.");
        }

        if (File.Exists(specPath))
            return specPath;

        throw new InvalidOperationException($"Missing or invalid --spec path: '{specPath}'.");
    }

    public static string? TryResolveExperimentDirectory(string specPath)
    {
        if (string.IsNullOrWhiteSpace(specPath)) return null;

        if (Directory.Exists(specPath))
        {
            var fullDirectoryPath = Path.GetFullPath(specPath);
            var manifestPath = Path.Combine(fullDirectoryPath, ManifestFileName);
            return File.Exists(manifestPath) ? fullDirectoryPath : null;
        }

        if (File.Exists(specPath))
        {
            var fullFilePath = Path.GetFullPath(specPath);
            if (!fullFilePath.EndsWith(ManifestFileName, StringComparison.OrdinalIgnoreCase))
                return null;

            return Path.GetDirectoryName(fullFilePath);
        }

        return null;
    }

    public static string ResolveRawResultsDirectory(string specPath, string? rawResultsDirectory)
    {
        if (!string.IsNullOrWhiteSpace(rawResultsDirectory))
            return Path.GetFullPath(rawResultsDirectory);

        var experimentDirectory = TryResolveExperimentDirectory(specPath);
        if (string.IsNullOrWhiteSpace(experimentDirectory))
        {
            throw new InvalidOperationException(
                "Missing --raw-out. For non-canonical spec paths, raw output directory must be provided explicitly.");
        }

        return Path.Combine(experimentDirectory, RawDirectoryName);
    }

    private static ExperimentSpec ConvertManifestToSpec(ExperimentManifest manifest, string? cliTarget)
    {
        if (manifest.Targets.Count == 0)
            throw new InvalidOperationException($"Experiment '{manifest.ExperimentKey}' does not declare any targets.");

        var selectedTargetKey = ResolveTargetKey(cliTarget, manifest.Targets);
        var targetSpec = manifest.Targets[selectedTargetKey];

        return new ExperimentSpec
        {
            ExperimentKey = manifest.ExperimentKey,
            ResearchQuestionId = manifest.ResearchQuestionId,
            HypothesisId = manifest.HypothesisId,
            Description = manifest.Description,
            TargetKey = selectedTargetKey,
            Engine = targetSpec.Engine,
            Nuget = NormalizeNuget(targetSpec.Nuget),
            Dataset = manifest.Dataset,
            Workload = manifest.Workload,
            FaultProfile = manifest.FaultProfile,
            FairnessProfile = manifest.FairnessProfile,
            RequiredCapabilities = manifest.RequiredCapabilities
        };
    }

    private static string ResolveTargetKey(
        string? cliTarget,
        IReadOnlyDictionary<string, ExperimentTargetSpec> targets)
    {
        var normalizedCliTarget = Normalize(cliTarget);
        if (normalizedCliTarget is not null)
        {
            foreach (var targetKey in targets.Keys)
            {
                if (targetKey.Equals(normalizedCliTarget, StringComparison.OrdinalIgnoreCase))
                    return targetKey;
            }

            foreach (var (targetKey, targetSpec) in targets)
            {
                if (targetSpec.Engine.Equals(normalizedCliTarget, StringComparison.OrdinalIgnoreCase))
                    return targetKey;
            }

            var configured = string.Join(", ", targets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            throw new InvalidOperationException(
                $"Target '{normalizedCliTarget}' is not configured in experiment manifest. Configured targets: {configured}.");
        }

        if (targets.Count == 1)
            return targets.Keys.First();

        var targetList = string.Join(", ", targets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        throw new InvalidOperationException(
            $"Experiment manifest defines multiple targets ({targetList}). Pass --engine <target-key>.");
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized.ToLowerInvariant();
    }

    private static string? NormalizeNuget(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
