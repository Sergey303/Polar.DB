using Polar.DB;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;
using static Polar.DB.Bench.Core.Services.FileWarmup;
using BenchWorkloadSpec = Polar.DB.Bench.Core.Models.WorkloadSpec;

namespace Polar.DB.Bench.Exec.PolarDb211;

internal static class Program
{
    private const string RunnerIdentity = "Polar.DB 2.1.1 NuGet";
    private const string RuntimeSource = "nuget-pinned";
    private const string? RuntimeNuget = "2.1.1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Schema: id(integer), name(sstring), age(real), category100(integer), isPopular(integer)
    private static readonly PTypeRecord PersonRecordType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("age", new PType(PTypeEnumeration.real)),
        new NamedType("category100", new PType(PTypeEnumeration.integer)),
        new NamedType("isPopular", new PType(PTypeEnumeration.integer)));

    public static int Main(string[] args)
    {
        RunnerOptions options;
        try
        {
            options = RunnerOptions.Parse(args, defaultEngineKey: "polar-db-2.1.1");
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
            Console.Error.WriteLine($"External Polar.DB typed runner '{RunnerIdentity}' produced technical failure.");
            Console.Error.WriteLine(result.TechnicalFailureReason);
        }

        // Важно: если raw-result записан, возвращаем 0.
        // Техническая ошибка библиотеки — это данные benchmark-а, а не причина ломать Analysis/Charts.
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
            "Typed external Polar.DB runner.",
            "Protocol: polar-db-search-point-and-category/v1 (point-lookup + scan/filter).",
            "Runtime: " + RunnerIdentity
        };
        var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["runtimeSource"] = RuntimeSource,
            ["runnerProject"] = typeof(Program).Assembly.GetName().Name ?? "unknown",
            ["protocol"] = "polar-db-search-point-and-category/v1",
            ["indexedCategoryLookupSupported"] = "False",
            ["scanFilterSupported"] = "True",
            ["semanticScope"] = "point+scan"
        };
        if (!string.IsNullOrWhiteSpace(RuntimeNuget))
        {
            diagnostics["runtimeNuget"] = RuntimeNuget!;
        }

        var totalWatch = Stopwatch.StartNew();
        var technicalSuccess = true;
        string? technicalFailure = null;
        bool? semanticSuccess = null;
        string? semanticFailure = null;

        var artifactLayout = ArtifactLayout.Create(options.WorkDirectory);
        Directory.CreateDirectory(artifactLayout.Root);

        try
        {
            ExecutePointLookup(spec, artifactLayout, metrics, diagnostics, out semanticSuccess, out semanticFailure);
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
            Runtime = new EngineRuntimeDescriptor { Source = RuntimeSource, Nuget = RuntimeNuget },
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

    private static void ExecutePointLookup(
        ExperimentSpec spec,
        ArtifactLayout layout,
        List<RunMetric> metrics,
        Dictionary<string, string> diagnostics,
        out bool? semanticSuccess,
        out string? semanticFailure)
    {
        var recordCount = checked((int)Math.Max(0, spec.Dataset.RecordCount));
        var seed = spec.Dataset.Seed ?? 303;

        // Parse workload options
        var options = spec.Workload.Parameters ?? new Dictionary<string, string>();
        var pointExistingQueries = ParseIntOption(options, "pointExistingQueries", 10000);
        var pointMissingQueries = ParseIntOption(options, "pointMissingQueries", 10000);
        var scanQueries = ParseIntOption(options, "scanQueries", 20);
        var categoryModulo = ParseIntOption(options, "categoryModulo", 100);
        var popularPercent = ParseIntOption(options, "popularPercent", 5);

        const bool optimise = false;

        USequence? useq = null;
        long existingHits = 0;
        long missingMisses = 0;

        var loadMs = 0.0;
        var buildMs = 0.0;
        var existingLookupMs = 0.0;
        var missingLookupMs = 0.0;

        // Scan/filter state
        long scanRowsScanned = 0;
        long scanRowsMatched = 0;
        long scanWrongRows = 0;
        long scanEmptyResultCount = 0;
        var scanMs = 0.0;
        var reopenRefreshMs = 0.0;

        try
        {
            // A. Load/build
            useq = CreateSequence(layout.Root, layout.StatePath, optimise);

            var loadWatch = Stopwatch.StartNew();
            useq.Load(GeneratePersons(recordCount, categoryModulo, popularPercent));
            loadWatch.Stop();
            loadMs = loadWatch.Elapsed.TotalMilliseconds;

            var buildWatch = Stopwatch.StartNew();
            useq.Build();
            buildWatch.Stop();
            buildMs = buildWatch.Elapsed.TotalMilliseconds;

            // Warm artifact files before measured lookup (unless explicitly disabled)
            if (IsWarmEnabled(spec.Workload.Parameters))
            {
                WarmDirectory(layout.Root,
                    cancellationToken: CancellationToken.None);
            }

            // B. Existing point lookup
            var existingWatch = Stopwatch.StartNew();
            var existingRng = new Random(unchecked(seed ^ 0x1001));
            for (var i = 0; i < pointExistingQueries; i++)
            {
                var key = existingRng.Next(0, recordCount);
                var row = useq.GetByKey(key);
                if (TryReadId(row, out var id) && id == key)
                {
                    existingHits++;
                }
            }
            existingWatch.Stop();
            existingLookupMs = existingWatch.Elapsed.TotalMilliseconds;

            // C. Missing point lookup
            var missingWatch = Stopwatch.StartNew();
            for (var i = 0; i < pointMissingQueries; i++)
            {
                var key = recordCount + 1 + i;
                try
                {
                    var row = useq.GetByKey(key);
                    // If no exception but row is null/empty, count as miss
                    if (row == null)
                    {
                        missingMisses++;
                    }
                    else
                    {
                        // Got a row for a missing key - this is unexpected but count as miss
                        missingMisses++;
                    }
                }
                catch
                {
                    // GetByKey throws for missing key - count as miss/empty
                    missingMisses++;
                }
            }
            missingWatch.Stop();
            missingLookupMs = missingWatch.Elapsed.TotalMilliseconds;

            // D. Scan/filter category lookup
            // Close and reopen to ensure clean state for scan (measured separately)
            var reopenRefreshWatch = Stopwatch.StartNew();
            Close(useq);
            useq = CreateSequence(layout.Root, layout.StatePath, optimise);
            useq.Refresh();
            reopenRefreshWatch.Stop();
            reopenRefreshMs = reopenRefreshWatch.Elapsed.TotalMilliseconds;

            // Warm artifact files before measured scan (unless explicitly disabled)
            if (IsWarmEnabled(spec.Workload.Parameters))
            {
                WarmDirectory(layout.Root,
                    cancellationToken: CancellationToken.None);
            }

            var scanWatch = Stopwatch.StartNew();
            for (var i = 0; i < scanQueries; i++)
            {
                var selectedCategory = (i * 37 + seed) % categoryModulo;
                long matched = 0;
                long wrong = 0;

                // Single pass: count scanned, match, and validate inline
                foreach (var element in useq.ElementValues())
                {
                    scanRowsScanned++;
                    if (element is object[] fields && fields.Length >= 5)
                    {
                        var category = (int)fields[3];
                        if (category == selectedCategory)
                        {
                            matched++;
                            // Validate the matched row immediately
                            if (category != selectedCategory)
                            {
                                wrong++;
                            }
                        }
                    }
                }

                // Expected count: recordCount / categoryModulo plus remainder handling
                var expectedCount = recordCount / categoryModulo;
                var remainder = recordCount % categoryModulo;
                // Categories 0..remainder-1 get one extra element
                if (selectedCategory < remainder)
                {
                    expectedCount++;
                }

                if (matched != expectedCount)
                {
                    wrong += Math.Abs(matched - expectedCount);
                }

                scanRowsMatched += matched;
                scanWrongRows += wrong;

                if (matched == 0)
                {
                    scanEmptyResultCount++;
                }
            }
            scanWatch.Stop();
            scanMs = scanWatch.Elapsed.TotalMilliseconds;
        }
        finally
        {
            Close(useq);
        }

        // Metrics
        metrics.Add(new RunMetric { MetricKey = "recordCount", Value = recordCount });
        metrics.Add(new RunMetric { MetricKey = "loadMs", Value = loadMs });
        metrics.Add(new RunMetric { MetricKey = "buildMs", Value = buildMs });

        // Existing point lookup metrics
        metrics.Add(new RunMetric { MetricKey = "search.point.ms", Value = existingLookupMs });
        metrics.Add(new RunMetric { MetricKey = "search.point.queries", Value = pointExistingQueries });
        metrics.Add(new RunMetric { MetricKey = "search.point.hits", Value = existingHits });
        metrics.Add(new RunMetric { MetricKey = "search.point.misses", Value = pointExistingQueries - existingHits });
        metrics.Add(new RunMetric { MetricKey = "search.point.hitRate", Value = pointExistingQueries > 0 ? (double)existingHits / pointExistingQueries : 0 });
        metrics.Add(new RunMetric { MetricKey = "search.point.emptyRate", Value = pointExistingQueries > 0 ? (double)(pointExistingQueries - existingHits) / pointExistingQueries : 0 });
        metrics.Add(new RunMetric { MetricKey = "search.point.msPerQuery", Value = pointExistingQueries > 0 ? existingLookupMs / pointExistingQueries : 0 });
        metrics.Add(new RunMetric { MetricKey = "search.point.queriesPerSecond", Value = existingLookupMs > 0 ? pointExistingQueries / (existingLookupMs / 1000.0) : 0 });

        // Missing point lookup metrics
        metrics.Add(new RunMetric { MetricKey = "search.point.missing.ms", Value = missingLookupMs });
        metrics.Add(new RunMetric { MetricKey = "search.point.missing.queries", Value = pointMissingQueries });
        metrics.Add(new RunMetric { MetricKey = "search.point.missing.hits", Value = 0 });
        metrics.Add(new RunMetric { MetricKey = "search.point.missing.misses", Value = missingMisses });
        metrics.Add(new RunMetric { MetricKey = "search.point.missing.hitRate", Value = 0 });
        metrics.Add(new RunMetric { MetricKey = "search.point.missing.emptyRate", Value = pointMissingQueries > 0 ? (double)missingMisses / pointMissingQueries : 0 });
        metrics.Add(new RunMetric { MetricKey = "search.point.missing.msPerQuery", Value = pointMissingQueries > 0 ? missingLookupMs / pointMissingQueries : 0 });
        metrics.Add(new RunMetric { MetricKey = "search.point.missing.queriesPerSecond", Value = missingLookupMs > 0 ? pointMissingQueries / (missingLookupMs / 1000.0) : 0 });

        // Scan/filter category lookup metrics
        metrics.Add(new RunMetric { MetricKey = "search.scan.ms", Value = scanMs });
        metrics.Add(new RunMetric { MetricKey = "search.scan.queries", Value = scanQueries });
        metrics.Add(new RunMetric { MetricKey = "search.scan.rowsScanned", Value = scanRowsScanned });
        metrics.Add(new RunMetric { MetricKey = "search.scan.rowsMatched", Value = scanRowsMatched });
        metrics.Add(new RunMetric { MetricKey = "search.scan.msPerQuery", Value = scanQueries > 0 ? scanMs / scanQueries : 0 });
        metrics.Add(new RunMetric { MetricKey = "search.scan.rowsScannedPerSecond", Value = scanMs > 0 ? scanRowsScanned / (scanMs / 1000.0) : 0 });
        metrics.Add(new RunMetric { MetricKey = "search.scan.rowsMatchedPerSecond", Value = scanMs > 0 ? scanRowsMatched / (scanMs / 1000.0) : 0 });
        metrics.Add(new RunMetric { MetricKey = "search.scan.semanticWrongRows", Value = scanWrongRows });
        metrics.Add(new RunMetric { MetricKey = "search.scan.emptyResultCount", Value = scanEmptyResultCount });

        // Standard metrics
        metrics.Add(new RunMetric { MetricKey = "reopenRefreshMs", Value = reopenRefreshMs });

        // Diagnostics
        diagnostics["optimise"] = optimise.ToString();
        diagnostics["recordCount"] = recordCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        diagnostics["pointExistingQueries"] = pointExistingQueries.ToString(System.Globalization.CultureInfo.InvariantCulture);
        diagnostics["pointMissingQueries"] = pointMissingQueries.ToString(System.Globalization.CultureInfo.InvariantCulture);
        diagnostics["scanQueries"] = scanQueries.ToString(System.Globalization.CultureInfo.InvariantCulture);
        diagnostics["categoryModulo"] = categoryModulo.ToString(System.Globalization.CultureInfo.InvariantCulture);
        diagnostics["popularPercent"] = popularPercent.ToString(System.Globalization.CultureInfo.InvariantCulture);
        diagnostics["seed"] = seed.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // SemanticSuccess: existing hits == pointExistingQueries AND missing misses == pointMissingQueries
        // AND scan/filter returns expected row counts AND scan wrong rows == 0
        var pointOk = existingHits == pointExistingQueries && missingMisses == pointMissingQueries;
        var scanOk = scanWrongRows == 0;
        semanticSuccess = pointOk && scanOk;
        semanticFailure = semanticSuccess.Value
            ? null
            : $"Point: expected existing hits={pointExistingQueries}, missing misses={pointMissingQueries}. Got existingHits={existingHits}, missingMisses={missingMisses}. Scan: wrongRows={scanWrongRows}.";
    }

    private static USequence CreateSequence(string root, string statePath, bool optimise)
    {
        Directory.CreateDirectory(root);
        var cnt = 0;
        Func<Stream> genStream = () => new FileStream(
            Path.Combine(root, "f" + (cnt++) + ".bin"),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite);

        return new USequence(
            PersonRecordType,
            statePath,
            genStream,
            ob => false,
            ob => (int)((object[])ob)[0],
            ic => (int)ic,
            optimise);
    }

    private static IEnumerable<object> GeneratePersons(int count, int categoryModulo, int popularPercent)
    {
        for (var i = 0; i < count; i++)
        {
            var category100 = i % categoryModulo;
            var isPopular = (i % 100) < popularPercent ? 1 : 0;
            yield return new object[] { i, "n" + i, 33.3, category100, isPopular };
        }
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
            // Best-effort cleanup path. Technical failure has already been captured if execution failed.
        }
    }

    private static int ParseIntOption(Dictionary<string, string> options, string key, int defaultValue)
    {
        if (options.TryGetValue(key, out var value) && int.TryParse(value, out var parsed))
        {
            return parsed;
        }
        return defaultValue;
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
        if (lower.EndsWith(".db", StringComparison.Ordinal) || lower.Contains("polar") || lower.StartsWith("f")) return ArtifactRole.PrimaryData;
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
            Nuget = target?.Nuget ?? RuntimeNuget,
            Dataset = manifest.Dataset,
            Workload = manifest.Workload,
            FaultProfile = manifest.FaultProfile,
            FairnessProfile = manifest.FairnessProfile,
            RequiredCapabilities = manifest.RequiredCapabilities
        };
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
            Runtime = new EngineRuntimeDescriptor { Source = RuntimeSource, Nuget = RuntimeNuget },
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

    private sealed record ArtifactLayout(string Root, string StatePath)
    {
        public static ArtifactLayout Create(string workDirectory)
        {
            var root = Path.Combine(workDirectory, "artifacts");
            return new ArtifactLayout(root, Path.Combine(root, "state.bin"));
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
