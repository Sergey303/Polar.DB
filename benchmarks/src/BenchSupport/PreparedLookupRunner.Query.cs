using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace PolarDbBenchmarks;

internal static partial class PreparedLookupRunner
{
    private static void Query(ExperimentOptions options, BenchmarkEngine engine, int rows,
        string dir, string sealPath, DatasetSeal seal, string runId, string resultRoot,
        string phaseName, UniformSeal? uniform = null,
        string? cacheTreatment = null, bool pinnedCache = false)
    {
        if (phaseName is not ("hot" or "cold"))
            throw new ArgumentException("--prepared-phase must be hot or cold.");
        var outpath = Path.Combine(resultRoot, options.ExperimentId, runId + ".json");
        if (File.Exists(outpath)) throw new IOException("Query result exists; refusing overwrite: " + outpath);
        var plan = uniform is null
            ? LookupPlanner.Plans(options.Kind,
                BenchmarkData.Dataset(rows, options.Kind), seal.LookupsPerSample)
                .Single(p => p.FileWarmup == (phaseName == "hot"))
            : UniformLookupPlanner.Create(rows).Plan;
        // Pin before opening: no gap between preparation and the engine warmup.
        using var pageLock = pinnedCache
            ? new PreparedPageLock(dir, seal.Files.Select(f => f.Name)) : null;
        var beforeFileWarmup = Capture();
        if (plan.FileWarmup)
            WarmupSealedFiles(dir, seal.Files);
        var afterFileWarmup = Capture();

        Func<object, QueryResult> lookup;
        Func<LookupPlan, string, (IReadOnlyList<double> Samples, long Rows, ulong Checksum)> batch;
        Func<object[], string, (IReadOnlyList<double> Samples, long Rows, ulong Checksum)> latency;
        Action close;
        var opening = Stopwatch.StartNew();
        if (engine == BenchmarkEngine.Sqlite)
        {
            // SQLite must not mutate the prepared DB or create a new one if missing.
            var builder = new SqliteConnectionStringBuilder {
                DataSource = Path.Combine(dir, "data.sqlite"),
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };
            var connection = new SqliteConnection(builder.ToString());
            connection.Open();
            var session = SqliteLookupSession.Create(connection, options.Kind);
            lookup = session.Query;
            batch = session.MeasureBatch;
            latency = session.MeasureLatency;
            close = () => { session.Dispose(); connection.Dispose(); };
        }
        else
        {
            // Polar.DB's current factory requires read/write streams; hashes are checked
            // by a separate verify invocation AFTER query (never before timed querying).
            var store = PolarStoreFactory.Open(dir, options.Kind);
            store.Sequence.Refresh();
            var session = new PolarLookupSession(store, options.Kind);
            lookup = session.Query;
            batch = session.MeasureBatch;
            latency = session.MeasureLatency;
            close = store.Sequence.Close;
        }
        opening.Stop();
        var afterOpen = Capture();
        (IReadOnlyList<double> Samples, long Rows, ulong Checksum) batchResult;
        (IReadOnlyList<double> Samples, long Rows, ulong Checksum) latencyResult;
        var warmup = Stopwatch.StartNew();
        var batchDuration = TimeSpan.Zero;
        var latencyDuration = TimeSpan.Zero;
        MetricSnapshot afterWarmup, afterBatch, afterLatency;
        PreparedFileResidency.Snapshot? residencyBeforeBatch = null;
        PreparedFileResidency.Snapshot? residencyAfterBatch = null;
        PreparedFileResidency.Snapshot? residencyBeforeTreatment = null;
        MetricSnapshot? afterCacheTreatment = null;
        try
        {
            foreach (var key in plan.WarmupKeys) lookup(key);
            warmup.Stop();
            afterWarmup = Capture();
            if (cacheTreatment is not null)
                (residencyBeforeTreatment, residencyBeforeBatch) =
                    PreparedCacheTreatment.ApplyAndVerify(dir,
                        BenchmarkPaths.EngineToken(engine), cacheTreatment,
                        seal.Files.Select(f => f.Name),
                        pageLock is null ? null : () => pageLock.ReleaseTarget(
                            BenchmarkPaths.EngineToken(engine), cacheTreatment));
            else if (uniform is not null)
                residencyBeforeBatch = PreparedFileResidency.Capture(dir,
                    seal.Files.Select(f => f.Name));
            afterCacheTreatment = Capture();
            var timer = Stopwatch.StartNew();
            batchResult = batch(plan, "prepared " + phaseName + " batch");
            timer.Stop();
            batchDuration = timer.Elapsed;
            afterBatch = Capture();
            if (uniform is not null)
                residencyAfterBatch = PreparedFileResidency.Capture(dir,
                    seal.Files.Select(f => f.Name));
            timer.Restart();
            latencyResult = latency(plan.LatencyKeys, "prepared " + phaseName + " latency");
            timer.Stop();
            latencyDuration = timer.Elapsed;
            afterLatency = Capture();
        }
        finally { close(); }
        var afterClose = Capture();

        // The expected checksums are sealed during prepare. Query-only workers
        // do not enumerate all source rows before OR after measured queries.
        var reference = uniform is null
            ? seal.ExpectedPhases.Single(p => p.Name == plan.Name)
            : new ExpectedPhase(plan.Name, uniform.BatchRows, uniform.BatchChecksum,
                uniform.LatencyRows, uniform.LatencyChecksum);
        var expectedBatch = new QueryResult(reference.BatchRows, reference.BatchChecksum);
        var expectedLatency = new QueryResult(reference.LatencyRows, reference.LatencyChecksum);
        if (batchResult.Rows != expectedBatch.Rows || batchResult.Checksum != expectedBatch.Checksum ||
            latencyResult.Rows != expectedLatency.Rows || latencyResult.Checksum != expectedLatency.Checksum)
            throw new InvalidDataException("Prepared lookup returned incorrect rows or checksums.");

        var result = new {
            Schema = SchemaVersion, RunId = runId, SourceSha = seal.SourceSha,
            QueryRunnerSha = CurrentCommit(),
            PlanProtocol = uniform?.Protocol ?? "legacy-prepared-v2",
            CacheTreatment = cacheTreatment,
            PinnedCacheProtocol = pageLock is null ? null : "mlock-v1",
            PinnedInitiallyBytes = pageLock?.InitiallyLockedBytes,
            PinnedDuringBatchBytes = pageLock?.LockedBytes,
            ResidencyBeforeTreatment = residencyBeforeTreatment,
            PlanKeyStreamSha256 = uniform?.KeyStreamSha256,
            PlanCoverage = uniform?.Coverage,
            ResidencyBeforeBatch = residencyBeforeBatch,
            ResidencyAfterBatch = residencyAfterBatch,
            Environment = BenchmarkEnvironment.Capture(runId, options.ExperimentId,
                "prepared-query-only", engine),
            PreparedManifest = sealPath, Engine = BenchmarkPaths.EngineToken(engine),
            Experiment = options.ExperimentId, Kind = options.Kind.ToString(), Rows = rows,
            Phase = phaseName, PhaseDefinition = plan.ToManifest(),
            FileWarmupElapsedMs = (afterFileWarmup.StopwatchTicks - beforeFileWarmup.StopwatchTicks) * 1000d / Stopwatch.Frequency,
            OpenRefreshMs = opening.Elapsed.TotalMilliseconds,
            KeyWarmupMs = warmup.Elapsed.TotalMilliseconds,
            BatchElapsedMs = batchDuration.TotalMilliseconds,
            LatencyElapsedMs = latencyDuration.TotalMilliseconds,
            BatchAverageMs = batchResult.Samples,
            LatencySamplesMs = latencyResult.Samples,
            BatchRows = batchResult.Rows, BatchChecksum = batchResult.Checksum,
            LatencyRows = latencyResult.Rows, LatencyChecksum = latencyResult.Checksum,
            ExpectedBatch = expectedBatch, ExpectedLatency = expectedLatency,
            Snapshots = new { BeforeFileWarmup = beforeFileWarmup,
                AfterFileWarmup = afterFileWarmup, AfterOpen = afterOpen,
                AfterKeyWarmup = afterWarmup,
                AfterCacheTreatment = afterCacheTreatment, AfterBatch = afterBatch,
                AfterLatency = afterLatency, AfterClose = afterClose },
            Interpretation = "Metrics include independent process IO and cgroup counters per stage; cgroup includes file cache and other container tasks. Cold means no explicit file warmup, not an OS cache reset. Query never rebuilds the DB. Hashes must be verified separately after querying."
        };
        WriteNewJson(outpath, result);
        Console.WriteLine("[prepared] query verified: " + outpath);
    }
}
