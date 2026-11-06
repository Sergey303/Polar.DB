using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace PolarDbBenchmarks;

// An opt-in experiment. The existing full build+lookup benchmark remains unchanged.
// Each prepare/query/verify invocation is a separate process; query never calls Load/Build.
internal static partial class PreparedLookupRunner
{
    private const int SchemaVersion = 2;
    private const long GiB = 1024L * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private sealed record FileSeal(string Name, long Bytes, string Sha256);
    private sealed record ExpectedPhase(string Name, long BatchRows, ulong BatchChecksum,
        long LatencyRows, ulong LatencyChecksum);
    private sealed record DatasetSeal(int Schema, string Experiment, string Kind, int Rows,
        string Engine, string SourceSha, DateTimeOffset CreatedUtc, FileSeal[] Files,
        int LookupsPerSample, ExpectedPhase[] ExpectedPhases);

    public static bool TryRun(ExperimentOptions options, string[] args)
    {
        if (TryUniform(options, args)) return true;
        var stage = Value(args, "prepared-stage");
        if (stage is null) return false;
        if (!options.Kind.IsLookup()) throw new ArgumentException("Prepared stages require a lookup benchmark.");
        if (stage is not ("prepare" or "verify" or "query"))
            throw new ArgumentException("--prepared-stage must be prepare, verify, or query.");

        var engine = BenchmarkArgs.Engine(args)
            ?? throw new ArgumentException("Specify exactly one --benchmark-engine=sqlite|polar-db.");
        var rows = BenchmarkArgs.Rows(args, options.RowCounts.ToArray());
        if (rows.Count != 1) throw new ArgumentException("Prepared mode requires a single --rows=N.");
        var root = RequiredDirectory(args, "prepared-root");
        if (IsWithin(root, BenchmarkPaths.RepoRoot))
            throw new ArgumentException("Prepared databases must be stored outside the repository.");
        var engineName = BenchmarkPaths.EngineToken(engine);
        var datasetDir = Path.Combine(root, options.ExperimentId, "rows-" + rows[0], engineName);
        var sealPath = Path.Combine(root, options.ExperimentId, "rows-" + rows[0], engineName + ".ready.json");
        var sha = CurrentCommit();

        if (stage == "prepare")
            Prepare(options, engine, rows[0], datasetDir, sealPath, sha);
        else
        {
            var seal = ReadSeal(sealPath, options, engineName, rows[0], sha);
            CheckFilesExist(datasetDir, seal); // stat only; no full file read before query
            if (stage == "verify") VerifyFiles(datasetDir, seal);
            else
            {
                var runId = BenchmarkArgs.RunId(args)
                    ?? throw new ArgumentException("query requires --benchmark-run-id=... for immutable results.");
                if (runId.Length > 100 || runId.Length == 0 || runId.Any(c =>
                        !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
                    throw new ArgumentException("Run ID must contain only ASCII letters, digits, - and _.");
                var resultRoot = RequiredDirectory(args, "prepared-results-root");
                if (IsWithin(resultRoot, BenchmarkPaths.RepoRoot))
                    throw new ArgumentException("Prepared results must be stored outside the repository.");
                if (IsWithin(resultRoot, root) || IsWithin(root, resultRoot))
                    throw new ArgumentException("Results and prepared data must not share a directory tree.");
                Query(options, engine, rows[0], datasetDir, sealPath, seal, runId,
                    resultRoot, Value(args, "prepared-phase") ?? "hot");
            }
        }
        return true;
    }

    private static void Prepare(ExperimentOptions options, BenchmarkEngine engine,
        int rows, string dir, string path, string sha)
    {
        if (Directory.Exists(dir) || File.Exists(path))
            throw new IOException("Prepared dataset already exists; refusing to overwrite: " + dir);
        CheckDiskReserve(dir);
        Directory.CreateDirectory(dir);
        Console.WriteLine($"[prepared] build {options.ExperimentId}: {rows} rows / {BenchmarkPaths.EngineToken(engine)}");
        var data = BenchmarkData.Dataset(rows, options.Kind);
        if (engine == BenchmarkEngine.Sqlite)
        {
            var db = Path.Combine(dir, "data.sqlite");
            SqliteStore.CreateForLookup(db, data, options.Kind);
            // The usual benchmark uses pooled SQLite connections. Here the seal must
            // describe the durable, closed database, not temporary WAL/SHM files.
            SqliteConnection.ClearAllPools();
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder {
                DataSource = db, Pooling = false
            }.ToString()))
            {
                connection.Open();
                SqliteStore.Flush(connection);
            }
            SqliteConnection.ClearAllPools();
        }
        else
        {
            var store = PolarStoreFactory.Open(dir, options.Kind);
            try
            {
                store.Sequence.Load(data.Select(row => PolarRows.ToPolar(row)));
                store.Sequence.Build();
                store.Sequence.Flush();
            }
            finally { store.Sequence.Close(); }
        }
        var files = SealedFiles(dir, engine == BenchmarkEngine.Sqlite);
        if (files.Length == 0) throw new InvalidDataException("Prepared database contains no files.");
        // Compute the oracle and lookups-per-sample ONCE during preparation.
        // Query-only workers generate just lookup keys, never all source rows.
        var plans = LookupPlanner.Plans(options.Kind, data);
        var oracle = BenchmarkExpected.CreateLookupOracle(options.Kind, data, plans);
        var phases = plans.Select(plan => {
            var batch = oracle.ForKeys(plan.BatchKeys);
            var latency = oracle.ForKeys(plan.LatencyKeys);
            return new ExpectedPhase(plan.Name, batch.Rows, batch.Checksum,
                latency.Rows, latency.Checksum);
        }).ToArray();
        var seal = new DatasetSeal(SchemaVersion, options.ExperimentId, options.Kind.ToString(),
            rows, BenchmarkPaths.EngineToken(engine), sha, DateTimeOffset.UtcNow, files,
            plans[0].LookupsPerBatchSample, phases);
        WriteNewJson(path, seal);
        Console.WriteLine($"[prepared] sealed {files.Length} files, {files.Sum(f => f.Bytes)} bytes: {path}");
    }

}
