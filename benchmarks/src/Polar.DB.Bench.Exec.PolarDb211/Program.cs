using Polar;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.Models;
using BenchDatasetSpec = Polar.DB.Bench.Core.Models.DatasetSpec;
using BenchWorkloadSpec = Polar.DB.Bench.Core.Models.WorkloadSpec;

namespace Polar.DB.Bench.Exec.PolarDb211;

internal static class Program
{
    private const string PackageVersion = "2.1.1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly PTypeRecord PersonRecordType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("age", new PType(PTypeEnumeration.real)));

    public static int Main(string[] args)
    {
        RunnerOptions options;
        try
        {
            options = RunnerOptions.Parse(args, defaultEngineKey: "polar-db-" + PackageVersion);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(RunnerOptions.Usage);
            return 2;
        }

        var result = ExecuteSafe(options);
        WriteResult(options.OutputPath, result);

        if (!result.TechnicalSuccess)
        {
            Console.Error.WriteLine("External Polar.DB " + PackageVersion + " typed run failed.");
            Console.Error.WriteLine(result.TechnicalFailureReason);
        }

        // Возвращаем 0, если raw-result удалось записать. Ошибка движка должна попадать в RunResult,
        // а не обрывать всю серию target-ов до Analysis/Charts.
        return 0;
    }

    private static RunResult ExecuteSafe(RunnerOptions options)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            var manifest = ReadManifest(options.ExperimentPath);
            var spec = BuildSpec(manifest, options.EngineKey);
            PrepareWorkDirectory(options.WorkDirectory);

            return Execute(spec, options, started);
        }
        catch (Exception ex)
        {
            return BuildFailureResult(options, started, ex.ToString());
        }
    }

    private static RunResult Execute(ExperimentSpec spec, RunnerOptions options, DateTimeOffset started)
    {
        var metrics = new List<RunMetric>();
        var notes = new List<string>
        {
            "Typed external Polar.DB NuGet runner.",
            "Package version: " + PackageVersion
        };
        var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["runtimeSource"] = "nuget-pinned",
            ["runtimeNuget"] = PackageVersion,
            ["runnerProject"] = typeof(Program).Assembly.GetName().Name ?? "unknown"
        };

        var totalWatch = Stopwatch.StartNew();
        var technicalSuccess = true;
        string? technicalFailure = null;
        bool? semanticSuccess = null;
        string? semanticFailure = null;

        var artifactLayout = ArtifactLayout.Create(options.WorkDirectory);
        Directory.CreateDirectory(artifactLayout.Root);

        try
        {
            var workloadKey = spec.Workload.WorkloadKey;
            if (IsFullCoverage(workloadKey))
            {
                ExecuteFullCoverage(spec, artifactLayout, metrics, diagnostics, out semanticSuccess, out semanticFailure);
            }
            else
            {
                ExecuteLoadBuildReopenLookup(spec, artifactLayout, metrics, diagnostics, out semanticSuccess, out semanticFailure);
            }
        }
        catch (Exception ex)
        {
            technicalSuccess = false;
            technicalFailure = ex.ToString();
            diagnostics["technicalFailureReason"] = technicalFailure;
        }
        finally
        {
            totalWatch.Stop();
        }

        var artifacts = CollectArtifacts(options.WorkDirectory).ToList();
        metrics.Add(new RunMetric { MetricKey = "elapsedMsTotal", Value = totalWatch.Elapsed.TotalMilliseconds });
        metrics.Add(new RunMetric { MetricKey = "elapsedMsSingleRun", Value = totalWatch.Elapsed.TotalMilliseconds });
        metrics.Add(new RunMetric { MetricKey = "totalArtifactBytes", Value = artifacts.Sum(x => x.Bytes) });
        metrics.Add(new RunMetric { MetricKey = "processPeakWorkingSetBytes", Value = Process.GetCurrentProcess().PeakWorkingSet64 });
        metrics.Add(new RunMetric { MetricKey = "managedBytesAfter", Value = GC.GetTotalMemory(forceFullCollection: false) });

        return new RunResult
        {
            RunId = CreateRunId(spec.ExperimentKey, options.EngineKey),
            TimestampUtc = started,
            EngineKey = options.EngineKey,
            ExperimentKey = spec.ExperimentKey,
            DatasetProfileKey = spec.Dataset.ProfileKey,
            FairnessProfileKey = spec.FairnessProfile?.FairnessProfileKey ?? "unspecified",
            ComparisonSetId = options.ComparisonSetId,
            RunSeriesSequenceNumber = options.SequenceNumber,
            RunRole = options.RunRole,
            Environment = CollectEnvironment(options.EnvironmentClass),
            Runtime = new EngineRuntimeDescriptor { Source = "nuget-pinned", Nuget = PackageVersion },
            TechnicalSuccess = technicalSuccess,
            TechnicalFailureReason = technicalFailure,
            SemanticSuccess = semanticSuccess,
            SemanticFailureReason = semanticFailure,
            Metrics = metrics,
            Artifacts = artifacts,
            EngineDiagnostics = diagnostics,
            Tags = new Dictionary<string, string>
            {
                ["research"] = spec.ResearchQuestionId ?? string.Empty,
                ["hypothesis"] = spec.HypothesisId ?? string.Empty,
                ["warmupCount"] = options.WarmupCount?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                ["measuredCount"] = options.MeasuredCount?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                ["role"] = options.RunRole ?? string.Empty
            },
            Notes = notes
        };
    }

    private static void ExecuteLoadBuildReopenLookup(
        ExperimentSpec spec,
        ArtifactLayout layout,
        List<RunMetric> metrics,
        Dictionary<string, string> diagnostics,
        out bool? semanticSuccess,
        out string? semanticFailure)
    {
        // Exact mirror of uploaded Program (1).cs:
        // Load reverse ids -> Build -> direct GetByKey -> 10000 random GetByKey.
        var npersons = checked((int)Math.Max(0, spec.Dataset.RecordCount));
        var lookupCount = Math.Max(0, spec.Workload.LookupCount ?? 10_000);
        var kperson = npersons * 2 / 3;
        const bool optimise = false;

        USequence? useq = null;
        long lookupHits = 0;
        var directLookupHit = false;

        var loadMs = 0.0;
        var buildMs = 0.0;
        var directLookupMs = 0.0;
        var randomLookupMs = 0.0;

        try
        {
            useq = CreateSequence(layout.DataPath, layout.StatePath, optimise);

            var loadWatch = Stopwatch.StartNew();
            useq.Load(GenerateReferencePersons(npersons));
            loadWatch.Stop();
            loadMs = loadWatch.Elapsed.TotalMilliseconds;

            var buildWatch = Stopwatch.StartNew();
            useq.Build();
            buildWatch.Stop();
            buildMs = buildWatch.Elapsed.TotalMilliseconds;

            var directWatch = Stopwatch.StartNew();
            var directRow = useq.GetByKey(kperson);
            directWatch.Stop();
            directLookupMs = directWatch.Elapsed.TotalMilliseconds;
            directLookupHit = TryReadId(directRow, out var directId) && directId == kperson;

            var rnd = new Random();
            var lookupWatch = Stopwatch.StartNew();
            for (var i = 0; i < lookupCount; i++)
            {
                var key = rnd.Next(npersons);
                var row = useq.GetByKey(key);
                if (TryReadId(row, out var id) && id == key)
                {
                    lookupHits++;
                }
            }
            lookupWatch.Stop();
            randomLookupMs = lookupWatch.Elapsed.TotalMilliseconds;
        }
        finally
        {
            Close(useq);
        }

        semanticSuccess = directLookupHit && lookupHits == lookupCount;
        semanticFailure = semanticSuccess.Value
            ? null
            : $"Expected direct lookup and {lookupCount} random lookup hits, got direct={directLookupHit}, randomHits={lookupHits}.";

        metrics.Add(new RunMetric { MetricKey = "recordCount", Value = npersons });
        metrics.Add(new RunMetric { MetricKey = "loadMs", Value = loadMs });
        metrics.Add(new RunMetric { MetricKey = "buildMs", Value = buildMs });
        metrics.Add(new RunMetric { MetricKey = "reopenRefreshMs", Value = 0 });
        metrics.Add(new RunMetric { MetricKey = "directPointLookupMs", Value = directLookupMs });
        metrics.Add(new RunMetric { MetricKey = "directPointLookupKey", Value = kperson });
        metrics.Add(new RunMetric { MetricKey = "directPointLookupHit", Value = directLookupHit ? 1 : 0 });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupMs", Value = randomLookupMs });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupCount", Value = lookupCount });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupHits", Value = lookupHits });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupMisses", Value = Math.Max(0, lookupCount - lookupHits) });

        diagnostics["referenceProgram"] = "Program (1).cs";
        diagnostics["referenceScenario"] = "Load reverse id 0..N-1 -> Build -> GetByKey(N*2/3) -> 10000 Random().Next(N) GetByKey";
        diagnostics["optimise"] = optimise.ToString();
        diagnostics["directLookupKey"] = kperson.ToString(System.Globalization.CultureInfo.InvariantCulture);
        diagnostics["directLookupHit"] = directLookupHit.ToString();
        diagnostics["lookupCount"] = lookupCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        diagnostics["lookupHitCount"] = lookupHits.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void ExecuteFullCoverage(
        ExperimentSpec spec,
        ArtifactLayout layout,
        List<RunMetric> metrics,
        Dictionary<string, string> diagnostics,
        out bool? semanticSuccess,
        out string? semanticFailure)
    {
        var initialCount = checked((int)Math.Max(0, spec.Dataset.RecordCount));
        var lookupCount = Math.Max(0, spec.Workload.LookupCount ?? 10_000);
        var batchCount = Math.Max(0, spec.Workload.BatchCount ?? 0);
        var batchSize = Math.Max(0, spec.Workload.BatchSize ?? 0);
        var lookupPerBatch = ResolveInt(spec.Workload, "randomLookupPerBatch", 5_000, min: 0);
        var seed = spec.Dataset.Seed ?? 1;
        var reverse = ShouldLoadReverse(spec);
        var optimise = ResolveBool(spec.Workload, "optimise", fallback: false);

        USequence? active = null;
        long lookupHits = 0;
        long lookupAttempts = 0;
        var expectedCount = initialCount;
        var rowCountMismatches = new List<string>();

        var loadMs = 0.0;
        var buildMs = 0.0;
        var reopenMs = 0.0;
        var directLookupMs = 0.0;
        var randomLookupMs = 0.0;
        var appendMs = 0.0;
        var directLookupKey = Math.Max(1, initialCount / 2);
        var directLookupHit = false;

        try
        {
            active = CreateSequence(layout.DataPath, layout.StatePath, optimise);
            var loadWatch = Stopwatch.StartNew();
            LoadPersons(active, GeneratePersons(initialCount, seed, reverse));
            loadWatch.Stop();
            loadMs = loadWatch.Elapsed.TotalMilliseconds;

            var buildWatch = Stopwatch.StartNew();
            active.Build();
            buildWatch.Stop();
            buildMs = buildWatch.Elapsed.TotalMilliseconds;

            Close(active);
            var reopenWatch = Stopwatch.StartNew();
            active = CreateSequence(layout.DataPath, layout.StatePath, optimise);
            active.Refresh();
            reopenWatch.Stop();
            reopenMs += reopenWatch.Elapsed.TotalMilliseconds;

            var directWatch = Stopwatch.StartNew();
            var directRow = active.GetByKey(directLookupKey);
            directWatch.Stop();
            directLookupMs = directWatch.Elapsed.TotalMilliseconds;
            directLookupHit = TryReadId(directRow, out var directId) && directId == directLookupKey;

            var initialLookup = RunRandomLookups(active, lookupCount, expectedCount, seed ^ 0x5A17_2026);
            randomLookupMs += initialLookup.ElapsedMs;
            lookupHits += initialLookup.Hits;
            lookupAttempts += lookupCount;

            for (var batch = 0; batch < batchCount; batch++)
            {
                var appendWatch = Stopwatch.StartNew();
                var firstId = expectedCount + 1;
                for (var i = 0; i < batchSize; i++)
                {
                    active.AppendElement(CreatePerson(firstId + i, firstId + i));
                }
                appendWatch.Stop();
                appendMs += appendWatch.Elapsed.TotalMilliseconds;
                expectedCount += batchSize;

                Close(active);
                reopenWatch.Restart();
                active = CreateSequence(layout.DataPath, layout.StatePath, optimise);
                active.Refresh();
                reopenWatch.Stop();
                reopenMs += reopenWatch.Elapsed.TotalMilliseconds;

                var cycleLookup = RunRandomLookups(active, lookupPerBatch, expectedCount, seed ^ (0x6e7f0000 + batch));
                randomLookupMs += cycleLookup.ElapsedMs;
                lookupHits += cycleLookup.Hits;
                lookupAttempts += lookupPerBatch;
            }
        }
        finally
        {
            Close(active);
        }

        semanticSuccess = directLookupHit && lookupHits == lookupAttempts && rowCountMismatches.Count == 0;
        semanticFailure = semanticSuccess.Value
            ? null
            : $"directLookupHit={directLookupHit}; lookupHits={lookupHits}; lookupAttempts={lookupAttempts}; rowCountMismatches={string.Join(" | ", rowCountMismatches)}";

        AddCommonMetrics(metrics, initialCount, loadMs, buildMs, reopenMs, directLookupMs, directLookupKey, directLookupHit, randomLookupMs, lookupAttempts, lookupHits);
        metrics.Add(new RunMetric { MetricKey = "appendMs", Value = appendMs });
        metrics.Add(new RunMetric { MetricKey = "appendBatchCount", Value = batchCount });
        metrics.Add(new RunMetric { MetricKey = "appendBatchSize", Value = batchSize });
        metrics.Add(new RunMetric { MetricKey = "lookupCountPerCycle", Value = lookupPerBatch });
        diagnostics["expectedCountAfterCycles"] = expectedCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        diagnostics["lookupHitCount"] = lookupHits.ToString(System.Globalization.CultureInfo.InvariantCulture);
        diagnostics["lookupCount"] = lookupAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture);
        diagnostics["directLookupHit"] = directLookupHit.ToString();
    }

    private static USequence CreateSequence(string dataPath, string statePath, bool optimise)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(statePath)) ?? ".";
        Directory.CreateDirectory(root);

        var cnt = 0;
        Func<Stream> streamGen = () => new FileStream(
            Path.Combine(root, "f" + (cnt++) + ".bin"),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite);

        Func<object, bool> isEmpty = ob => false;
        Func<object, IComparable> keyFunc = ob => (int)((object[])ob)[0];
        Func<IComparable, int> hashOfKey = ic => (int)ic;

        return new USequence(
            PersonRecordType,
            statePath,
            streamGen,
            isEmpty,
            keyFunc,
            hashOfKey,
            optimise);
    }

    private static void LoadPersons(USequence sequence, IEnumerable<object> persons)
    {
        sequence.Load(persons);
    }

    private static IEnumerable<object> GenerateReferencePersons(int npersons)
    {
        for (var i = 0; i < npersons; i++)
        {
            yield return new object[] { npersons - i - 1, "n" + i, 33.3 };
        }
    }

    private static IEnumerable<object> GeneratePersons(int count, int seed, bool reverse)
    {
        if (reverse)
        {
            for (var id = count; id >= 1; id--)
            {
                yield return CreatePerson(id, count - id + 1);
            }

            yield break;
        }

        for (var id = 1; id <= count; id++)
        {
            yield return CreatePerson(id, id);
        }
    }

    private static object[] CreatePerson(int id, int ordinal)
    {
        return new object[] { id, "n" + ordinal, 33.3 };
    }

    private static (long Hits, double ElapsedMs) RunRandomLookups(USequence sequence, int lookupCount, int maxKeyInclusive, int seed)
    {
        var random = new Random(seed);
        long hits = 0;
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < lookupCount; i++)
        {
            var key = random.Next(1, maxKeyInclusive + 1);
            var row = sequence.GetByKey(key);
            if (TryReadId(row, out var id) && id == key)
            {
                hits++;
            }
        }
        watch.Stop();
        return (hits, watch.Elapsed.TotalMilliseconds);
    }

    private static bool TryReadId(object? row, out int id)
    {
        if (row is object[] array && array.Length > 0 && array[0] is int value)
        {
            id = value;
            return true;
        }

        id = 0;
        return false;
    }

    private static void Close(USequence? sequence)
    {
        try
        {
            sequence?.Close();
        }
        catch
        {
            // Best-effort close in benchmark cleanup path.
        }
    }

    private static void AddCommonMetrics(
        List<RunMetric> metrics,
        long recordCount,
        double loadMs,
        double buildMs,
        double reopenMs,
        double directLookupMs,
        long directLookupKey,
        bool directLookupHit,
        double randomLookupMs,
        long lookupCount,
        long lookupHits)
    {
        metrics.Add(new RunMetric { MetricKey = "recordCount", Value = recordCount });
        metrics.Add(new RunMetric { MetricKey = "loadMs", Value = loadMs });
        metrics.Add(new RunMetric { MetricKey = "buildMs", Value = buildMs });
        metrics.Add(new RunMetric { MetricKey = "reopenRefreshMs", Value = reopenMs });
        metrics.Add(new RunMetric { MetricKey = "directPointLookupMs", Value = directLookupMs });
        metrics.Add(new RunMetric { MetricKey = "directPointLookupKey", Value = directLookupKey });
        metrics.Add(new RunMetric { MetricKey = "directPointLookupHit", Value = directLookupHit ? 1 : 0 });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupMs", Value = randomLookupMs });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupCount", Value = lookupCount });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupHits", Value = lookupHits });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupMisses", Value = Math.Max(0, lookupCount - lookupHits) });
    }

    private static IEnumerable<ArtifactDescriptor> CollectArtifacts(string workDirectory)
    {
        if (!Directory.Exists(workDirectory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(workDirectory, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            yield return new ArtifactDescriptor(
                GuessRole(info.Name),
                Path.GetRelativePath(workDirectory, info.FullName),
                info.Length);
        }
    }

    private static ArtifactRole GuessRole(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        if (lower.Contains("state")) return ArtifactRole.State;
        if (lower.Contains("index")) return ArtifactRole.SecondaryIndex;
        if (lower.EndsWith(".db", StringComparison.Ordinal) || lower.Contains("polar")) return ArtifactRole.PrimaryData;
        return ArtifactRole.Unknown;
    }

    private static ExperimentManifest ReadManifest(string path)
    {
        var full = Path.GetFullPath(path);
        var manifest = JsonSerializer.Deserialize<ExperimentManifest>(File.ReadAllText(full), JsonOptions);
        return manifest ?? throw new InvalidDataException("Experiment JSON is empty or invalid: " + full);
    }

    private static ExperimentSpec BuildSpec(ExperimentManifest manifest, string engineKey)
    {
        manifest.Targets.TryGetValue(engineKey, out var target);
        return new ExperimentSpec
        {
            ExperimentKey = manifest.ExperimentKey,
            ResearchQuestionId = manifest.ResearchQuestionId,
            HypothesisId = manifest.HypothesisId,
            Description = manifest.Description,
            TargetKey = engineKey,
            Engine = target?.Engine ?? "polar-db",
            Nuget = target?.Nuget ?? PackageVersion,
            Dataset = manifest.Dataset,
            Workload = manifest.Workload,
            FaultProfile = manifest.FaultProfile,
            FairnessProfile = manifest.FairnessProfile,
            RequiredCapabilities = manifest.RequiredCapabilities
        };
    }

    private static bool ShouldLoadReverse(ExperimentSpec spec)
    {
        if (spec.Dataset.ProfileKey.Contains("reverse", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return spec.Workload.Parameters?.TryGetValue("loadOrder", out var loadOrder) == true &&
               loadOrder.Contains("reverse", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFullCoverage(string workloadKey)
    {
        return workloadKey.Contains("full-adapter-coverage", StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveInt(BenchWorkloadSpec workload, string key, int fallback, int min)
    {
        if (workload.Parameters?.TryGetValue(key, out var text) == true && int.TryParse(text, out var value))
        {
            return Math.Max(min, value);
        }

        return Math.Max(min, fallback);
    }

    private static bool ResolveBool(BenchWorkloadSpec workload, string key, bool fallback)
    {
        if (workload.Parameters?.TryGetValue(key, out var text) == true && bool.TryParse(text, out var value))
        {
            return value;
        }

        return fallback;
    }

    private static EnvironmentManifest CollectEnvironment(string environmentClass)
    {
        return new EnvironmentManifest
        {
            EnvironmentClass = environmentClass,
            MachineName = Environment.MachineName,
            OsDescription = RuntimeInformation.OSDescription,
            OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            Is64BitProcess = Environment.Is64BitProcess,
            ProcessorCount = Environment.ProcessorCount,
            CurrentDirectory = Environment.CurrentDirectory,
            UserName = Environment.UserName,
            Git = null
        };
    }

    private static RunResult BuildFailureResult(RunnerOptions options, DateTimeOffset started, string error)
    {
        return new RunResult
        {
            RunId = CreateRunId("failed", options.EngineKey),
            TimestampUtc = started,
            EngineKey = options.EngineKey,
            ExperimentKey = "unknown",
            DatasetProfileKey = "unknown",
            FairnessProfileKey = "unknown",
            ComparisonSetId = options.ComparisonSetId,
            RunSeriesSequenceNumber = options.SequenceNumber,
            RunRole = options.RunRole,
            Environment = CollectEnvironment(options.EnvironmentClass),
            Runtime = new EngineRuntimeDescriptor { Source = "nuget-pinned", Nuget = PackageVersion },
            TechnicalSuccess = false,
            TechnicalFailureReason = error,
            SemanticSuccess = null,
            SemanticFailureReason = null,
            Metrics = Array.Empty<RunMetric>(),
            Artifacts = Array.Empty<ArtifactDescriptor>(),
            EngineDiagnostics = new Dictionary<string, string> { ["technicalFailureReason"] = error },
            Tags = null,
            Notes = new List<string> { "Failed before experiment execution." }
        };
    }

    private static string CreateRunId(string experimentKey, string engineKey)
    {
        return DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "." + Sanitize(experimentKey) + "." + Sanitize(engineKey) + "." + Guid.NewGuid().ToString("N");
    }

    private static string Sanitize(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-').ToArray();
        return new string(chars).Trim('-');
    }

    private static void WriteResult(string outputPath, RunResult result)
    {
        var full = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full) ?? ".");
        File.WriteAllText(full, JsonSerializer.Serialize(result, JsonOptions));
        Console.WriteLine(full);
    }

    private static void PrepareWorkDirectory(string workDirectory)
    {
        var full = Path.GetFullPath(workDirectory);

        if (Directory.Exists(full))
        {
            Directory.Delete(full, recursive: true);
        }

        Directory.CreateDirectory(full);
    }

    private sealed record ArtifactLayout(string Root, string DataPath, string StatePath)
    {
        public static ArtifactLayout Create(string workDirectory)
        {
            var root = Path.Combine(workDirectory, "artifacts");
            return new ArtifactLayout(
                root,
                Path.Combine(root, "f0.bin"),
                Path.Combine(root, "state.bin"));
        }
    }

    private sealed class RunnerOptions
    {
        public const string Usage = "Usage: --engine-key <key> --experiment <experiment.json> --work-dir <dir> --output <raw.json> [--env local] [--comparison-set <id>] [--run-role warmup|measured] [--sequence-number <n>]";

        public required string EngineKey { get; init; }
        public required string ExperimentPath { get; init; }
        public required string WorkDirectory { get; init; }
        public required string OutputPath { get; init; }
        public string EnvironmentClass { get; init; } = "local";
        public string? ComparisonSetId { get; init; }
        public string? RunRole { get; init; }
        public int? SequenceNumber { get; init; }
        public int? WarmupCount { get; init; }
        public int? MeasuredCount { get; init; }

        public static RunnerOptions Parse(string[] args, string defaultEngineKey)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < args.Length; i++)
            {
                var key = args[i];
                if (!key.StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Unexpected positional argument: " + key);
                }

                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Missing value for option: " + key);
                }

                map[key] = args[++i];
            }

            return new RunnerOptions
            {
                EngineKey = Get(map, "--engine-key", defaultEngineKey),
                ExperimentPath = Required(map, "--experiment"),
                WorkDirectory = Required(map, "--work-dir"),
                OutputPath = Required(map, "--output"),
                EnvironmentClass = Get(map, "--env", "local"),
                ComparisonSetId = GetNullable(map, "--comparison-set"),
                RunRole = GetNullable(map, "--run-role"),
                SequenceNumber = GetNullableInt(map, "--sequence-number"),
                WarmupCount = GetNullableInt(map, "--warmup-count"),
                MeasuredCount = GetNullableInt(map, "--measured-count")
            };
        }

        private static string Required(Dictionary<string, string> map, string key)
        {
            return map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException("Missing required option: " + key);
        }

        private static string Get(Dictionary<string, string> map, string key, string fallback)
        {
            return map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;
        }

        private static string? GetNullable(Dictionary<string, string> map, string key)
        {
            return map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
        }

        private static int? GetNullableInt(Dictionary<string, string> map, string key)
        {
            return map.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : null;
        }
    }
}
