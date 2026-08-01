using System.Diagnostics;
using System.Reflection;

namespace PolarDbBenchmarks;

internal static class BenchmarkExecution
{
    public static void Run(ExperimentOptions options)
    {
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        options = ApplyOverrides(options, args);
        var engine = BenchmarkArgs.Engine(args);
        if (engine != null)
        {
            RunWorker(options, args, engine.Value);
            return;
        }

        RunCoordinator(options, args);
    }

    private static void RunWorker(ExperimentOptions options, string[] args, BenchmarkEngine engine)
    {
        var runId = BenchmarkArgs.RunId(args)
            ?? throw new ArgumentException("--benchmark-run-id is required for a benchmark worker.");
        BenchmarkPaths.EnsureResultDirectories(options.ExperimentId, runId);
        var manifest = BenchmarkEnvironment.Capture(
            runId, options.ExperimentId, "engine-worker", engine);
        var result = options.Kind.IsLookup()
            ? LookupBench.RunWorker(options, runId, engine, manifest)
            : LifecycleBench.RunWorker(options, runId, engine, manifest);
        BenchmarkRawArtifacts.WriteWorker(result);
    }

    private static void RunCoordinator(ExperimentOptions options, string[] args)
    {
        var runId = BenchmarkEnvironment.NewRunId(options.ExperimentId);
        var started = DateTimeOffset.UtcNow;
        BenchmarkPaths.EnsureResultDirectories(options.ExperimentId, runId);
        var coordinator = BenchmarkEnvironment.Capture(runId, options.ExperimentId, "coordinator", null);
        if (!coordinator.PublicationReady)
        {
            Console.Error.WriteLine(
                "[bench] NON-PUBLICATION RUN: build configuration is " + coordinator.BuildConfiguration +
                "; optimizations disabled=" + coordinator.OptimizationsDisabled + ".");
        }

        try
        {
            var engineOrder = EngineOrder(options, runId);
            foreach (var engine in engineOrder)
                RunChild(runId, engine, args);

            var sqlite = BenchmarkRawArtifacts.ReadWorker(options.ExperimentId, runId, BenchmarkEngine.Sqlite);
            var polar = BenchmarkRawArtifacts.ReadWorker(options.ExperimentId, runId, BenchmarkEngine.PolarDb);
            ValidateEnvironment(coordinator, sqlite.Manifest, polar.Manifest);
            var manifest = new BenchmarkRunManifest(
                RunId: runId,
                ExperimentId: options.ExperimentId,
                StartedUtc: started,
                Coordinator: coordinator,
                EngineProcesses: engineOrder.Select(engine =>
                    engine == BenchmarkEngine.Sqlite ? sqlite.Manifest : polar.Manifest).ToArray(),
                EngineOrder: engineOrder.Select(BenchmarkPaths.EngineToken).ToArray(),
                ReopenDefinition: "Open-only measures opening and closing storage handles. Query-ready measures opening, metadata/index readiness, and one indexed primary-key lookup.",
                VolatileMutationDefinition: "Per-operation time before a persistence boundary; final flush/commit is excluded.",
                DurableMutationDefinition: "Per-operation average inside a batch that includes SQLite transaction commit plus WAL checkpoint and file sync, or Polar.DB Flush plus file sync.");

            if (options.Kind.IsLookup())
            {
                var runs = LookupBench.Merge(sqlite, polar);
                BenchmarkRawArtifacts.WriteCombined(options, manifest, null, runs);
                File.WriteAllText(BenchmarkPaths.ResultPath(options.ExperimentId),
                    BenchmarkReport.RenderLookup(options, runs, manifest));
            }
            else
            {
                var runs = LifecycleBench.Merge(sqlite, polar);
                BenchmarkRawArtifacts.WriteCombined(options, manifest, runs, null);
                File.WriteAllText(BenchmarkPaths.ResultPath(options.ExperimentId),
                    BenchmarkReport.Render(options, runs, manifest));
            }

            Console.WriteLine("[bench] html: " + BenchmarkPaths.ResultPath(options.ExperimentId));
            Console.WriteLine("[bench] manifest: " + BenchmarkPaths.LatestManifestPath(options.ExperimentId));
            Console.WriteLine("[bench] raw json: " + BenchmarkPaths.LatestRawJsonPath(options.ExperimentId));
            Console.WriteLine("[bench] raw csv: " + BenchmarkPaths.LatestRawCsvPath(options.ExperimentId));
            BenchmarkPaths.TryCleanupRunWork(options.ExperimentId, runId);
        }
        catch
        {
            Console.Error.WriteLine("[bench] failed run kept under benchmarks/work and benchmarks/results/raw: " + runId);
            throw;
        }
    }

    private static void ValidateEnvironment(
        BenchmarkEnvironmentManifest coordinator,
        params BenchmarkEnvironmentManifest[] workers)
    {
        foreach (var worker in workers)
        {
            if (!string.Equals(worker.CommitSha, coordinator.CommitSha, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Benchmark worker commit {worker.CommitSha} differs from coordinator commit {coordinator.CommitSha}.");
            if (!string.Equals(worker.BuildConfiguration, coordinator.BuildConfiguration, StringComparison.OrdinalIgnoreCase) ||
                worker.OptimizationsDisabled != coordinator.OptimizationsDisabled)
                throw new InvalidDataException(
                    $"Benchmark worker {worker.Engine} build settings differ from coordinator settings.");
        }
    }

    private static ExperimentOptions ApplyOverrides(ExperimentOptions options, string[] args)
    {
        var rows = BenchmarkArgs.Rows(args, options.RowCounts.ToArray());
        var warmup = BenchmarkArgs.Int(args, "warmup", options.WarmupOps);
        var samples = BenchmarkArgs.Int(args, "samples", options.MeasuredOps);
        return options with { RowCounts = rows, WarmupOps = warmup, MeasuredOps = samples };
    }

    private static IReadOnlyList<BenchmarkEngine> EngineOrder(ExperimentOptions options, string runId)
    {
        var requested = Environment.GetEnvironmentVariable("POLAR_BENCH_ENGINE_ORDER");
        if (string.Equals(requested, "polar-first", StringComparison.OrdinalIgnoreCase))
            return new[] { BenchmarkEngine.PolarDb, BenchmarkEngine.Sqlite };
        if (string.Equals(requested, "sqlite-first", StringComparison.OrdinalIgnoreCase))
            return new[] { BenchmarkEngine.Sqlite, BenchmarkEngine.PolarDb };

        var parity = (options.ExperimentId + runId).Aggregate(0, (value, ch) => unchecked(value * 31 + ch));
        return (parity & 1) == 0
            ? new[] { BenchmarkEngine.Sqlite, BenchmarkEngine.PolarDb }
            : new[] { BenchmarkEngine.PolarDb, BenchmarkEngine.Sqlite };
    }

    private static void RunChild(string runId, BenchmarkEngine engine, string[] args)
    {
        var entry = Assembly.GetEntryAssembly()?.Location
            ?? throw new InvalidOperationException("Cannot locate benchmark entry assembly.");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = BenchmarkPaths.RepoRoot,
            UseShellExecute = false
        };
        start.ArgumentList.Add(entry);
        foreach (var argument in BenchmarkArgs.WithoutWorkerArguments(args))
            start.ArgumentList.Add(argument);
        start.ArgumentList.Add("--benchmark-engine=" + BenchmarkPaths.EngineToken(engine));
        start.ArgumentList.Add("--benchmark-run-id=" + runId);

        Console.WriteLine("[bench] start isolated engine: " + BenchmarkPaths.EngineToken(engine));
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Cannot start benchmark worker.");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                "Benchmark worker failed: " + BenchmarkPaths.EngineToken(engine) + ", exit code " + process.ExitCode);
    }
}
