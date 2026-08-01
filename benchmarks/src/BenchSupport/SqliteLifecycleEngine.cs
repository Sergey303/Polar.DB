using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace PolarDbBenchmarks;

internal static class SqliteLifecycleEngine
{
    public static EngineResult Run(ExperimentOptions options, Row[] data, string dir)
    {
        if (options.Kind == ExperimentKind.BuildPrimaryIntOnly) return BuildPrimaryIntOnly(options, data, dir);
        if (options.Kind == ExperimentKind.ReopenOnly) return ReopenOnly(options, data, dir);
        if (options.Kind == ExperimentKind.AppendOnly) return Mutation(options, data, dir, append: true);
        return Mutation(options, data, dir, append: false);
    }

    private static EngineResult BuildPrimaryIntOnly(ExperimentOptions options, Row[] data, string dir)
    {
        var before = BenchmarkResources.Capture();
        var totalSamples = new List<double>();
        var loadSamples = new List<double>();
        var buildSamples = new List<double>();
        var flushSamples = new List<double>();
        var artifactDir = dir;

        for (var i = -options.WarmupOps; i < options.MeasuredOps; i++)
        {
            var runDir = Path.Combine(dir, "run-" + i);
            Directory.CreateDirectory(runDir);
            var db = Path.Combine(runDir, "data.sqlite");
            var loadMs = Measure(() => CreateTinyPrimaryStore(db, data));
            using var connection = new SqliteConnection($"Data Source={db}");
            connection.Open();

            var total = Stopwatch.StartNew();
            var buildMs = Measure(() => SqliteStore.CreatePrimaryIntIndex(connection));
            var flushMs = Measure(() => SqliteStore.Flush(connection));
            total.Stop();

            if (i >= 0)
            {
                totalSamples.Add(total.Elapsed.TotalMilliseconds);
                loadSamples.Add(loadMs);
                buildSamples.Add(buildMs);
                flushSamples.Add(flushMs);
                artifactDir = runDir;
            }
        }

        return Result(
            "sqlite",
            "build + flush",
            totalSamples,
            data,
            artifactDir,
            before,
            buildSamples,
            flushSamples,
            load: loadSamples);
    }

    private static void CreateTinyPrimaryStore(string db, IEnumerable<Row> rows)
    {
        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();
        SqliteStore.Exec(connection, "PRAGMA journal_mode=WAL;");
        SqliteStore.Exec(connection, "CREATE TABLE rows(id INTEGER NOT NULL);");

        using var tx = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO rows(id) VALUES($id)";
        var id = command.Parameters.Add("$id", SqliteType.Integer);

        foreach (var row in rows)
        {
            id.Value = row.Id;
            command.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private static EngineResult ReopenOnly(ExperimentOptions options, Row[] data, string dir)
    {
        var before = BenchmarkResources.Capture();
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "data.sqlite");
        SqliteStore.Create(db, data, withIndexes: true);

        var openOnly = MeasureRepeated(options.WarmupOps, options.MeasuredOps, () =>
        {
            using var connection = new SqliteConnection($"Data Source={db}");
            connection.Open();
        });

        var expectedLookup = BenchmarkChecksum.HashRows(new[] { data[0] });
        var queryReady = MeasureRepeated(options.WarmupOps, options.MeasuredOps, () =>
        {
            using var connection = new SqliteConnection($"Data Source={db}");
            connection.Open();
            using var session = SqliteLookupSession.Create(connection, ExperimentKind.PkIntLookup);
            var query = session.Query(data[0].Id);
            if (query.Rows != 1 || query.Checksum != expectedLookup)
                throw new InvalidDataException("SQLite reopen lookup returned an unexpected row.");
        });

        return Result(
            "sqlite",
            "query-ready reopen",
            queryReady,
            SqliteRows.ReadAll(db),
            dir,
            before,
            open: openOnly);
    }

    private static EngineResult Mutation(
        ExperimentOptions options,
        Row[] data,
        string dir,
        bool append)
    {
        var before = BenchmarkResources.Capture();
        var warmupDir = Path.Combine(dir, "warmup");
        var volatileDir = Path.Combine(dir, "volatile");
        var durableDir = Path.Combine(dir, "durable");

        WarmMutation(options, data, warmupDir, append);

        Directory.CreateDirectory(volatileDir);
        var volatileDb = Path.Combine(volatileDir, "data.sqlite");
        SqliteStore.Create(volatileDb, data, withIndexes: true);
        Row[] actualRows;
        var volatileSamples = new List<double>();
        using (var connection = new SqliteConnection($"Data Source={volatileDb}"))
        {
            connection.Open();
            using var transaction = connection.BeginTransaction();
            if (append)
            {
                foreach (var row in BenchmarkData.Dataset(options.MeasuredOps, options.Kind, data.Length + 1))
                    volatileSamples.Add(Measure(() => InsertOne(connection, row)));
            }
            else
            {
                foreach (var key in BenchmarkData.PrimaryKeys(data, options.MeasuredOps))
                    volatileSamples.Add(Measure(() => DeleteOne(connection, key)));
            }

            transaction.Commit();
            actualRows = SqliteRows.ReadAll(connection);
        }

        var durableSamples = MeasureDurableBatches(options, data, durableDir, append);
        var result = Result(
            "sqlite",
            "volatile mutation",
            volatileSamples,
            actualRows,
            volatileDir,
            before,
            durable: durableSamples,
            durableBatchSize: BenchmarkDefaults.MutationDurableBatchSize);

        BenchmarkPaths.TryDeleteDirectory(warmupDir);
        BenchmarkPaths.TryDeleteDirectory(durableDir);
        return result;
    }

    private static void WarmMutation(
        ExperimentOptions options,
        Row[] data,
        string dir,
        bool append)
    {
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "data.sqlite");
        var warmRows = data.Take(Math.Min(data.Length, 50_000)).ToArray();
        SqliteStore.Create(db, warmRows, withIndexes: true);
        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();
        using var transaction = connection.BeginTransaction();
        if (append)
        {
            foreach (var row in BenchmarkData.Dataset(options.WarmupOps, options.Kind, warmRows.Length + 1))
                InsertOne(connection, row);
        }
        else
        {
            foreach (var key in BenchmarkData.PrimaryKeys(warmRows, Math.Min(options.WarmupOps, warmRows.Length)))
                DeleteOne(connection, key);
        }

        transaction.Rollback();
    }

    private static IReadOnlyList<double> MeasureDurableBatches(
        ExperimentOptions options,
        Row[] data,
        string dir,
        bool append)
    {
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "data.sqlite");
        SqliteStore.Create(db, data, withIndexes: true);
        var warmupBatches = BenchmarkDefaults.MutationDurableWarmupBatches;
        var measuredBatches = BenchmarkDefaults.MutationDurableMeasuredBatches;
        var batchSize = BenchmarkDefaults.MutationDurableBatchSize;
        var totalOps = (warmupBatches + measuredBatches) * batchSize;
        var appendRows = append
            ? BenchmarkData.Dataset(totalOps, options.Kind, data.Length + options.MeasuredOps + 1)
            : Array.Empty<Row>();
        var deleteKeys = append
            ? Array.Empty<long>()
            : BenchmarkData.PrimaryKeys(data, Math.Min(totalOps, data.Length)).ToArray();
        if (!append && deleteKeys.Length < totalOps)
            throw new InvalidOperationException("Not enough unique rows for durable delete batches.");

        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();
        var samples = new List<double>();
        var offset = 0;
        for (var batch = 0; batch < warmupBatches + measuredBatches; batch++)
        {
            var stopwatch = Stopwatch.StartNew();
            using (var transaction = connection.BeginTransaction())
            {
                for (var i = 0; i < batchSize; i++)
                {
                    if (append)
                        InsertOne(connection, appendRows[offset++]);
                    else
                        DeleteOne(connection, deleteKeys[offset++]);
                }

                transaction.Commit();
            }

            SqliteStore.Flush(connection);
            BenchmarkDurability.SyncDirectoryFiles(dir);
            stopwatch.Stop();
            if (batch >= warmupBatches)
                samples.Add(stopwatch.Elapsed.TotalMilliseconds / batchSize);
        }

        var actualRows = SqliteRows.ReadAll(connection);
        ValidateDurableRows(data, appendRows, actualRows, append, totalOps);
        return samples;
    }

    private static void ValidateDurableRows(
        Row[] original,
        Row[] appended,
        Row[] actual,
        bool append,
        int operationCount)
    {
        var expected = (append ? original.Concat(appended) : original.Skip(operationCount)).ToArray();
        if (actual.Length != expected.Length ||
            BenchmarkChecksum.HashRows(actual) != BenchmarkChecksum.HashRows(expected))
            throw new InvalidDataException("SQLite durable mutation result failed correctness validation.");
    }

    private static void InsertOne(SqliteConnection connection, Row row)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO rows(id,long_key,guid_key,skey,external_id,external_long,external_guid,external_key,payload) VALUES($id,$long,$guid,$skey,$eid,$elong,$eguid,$ekey,$payload)";
        command.Parameters.AddWithValue("$id", row.Id);
        command.Parameters.AddWithValue("$long", row.LongKey);
        command.Parameters.AddWithValue("$guid", BenchmarkGuid.ToBytes(row.GuidKey));
        command.Parameters.AddWithValue("$skey", row.SKey);
        command.Parameters.AddWithValue("$eid", row.ExternalId);
        command.Parameters.AddWithValue("$elong", row.ExternalLong);
        command.Parameters.AddWithValue("$eguid", BenchmarkGuid.ToBytes(row.ExternalGuid));
        command.Parameters.AddWithValue("$ekey", row.ExternalKey);
        command.Parameters.AddWithValue("$payload", row.Payload);
        command.ExecuteNonQuery();
    }

    private static void DeleteOne(SqliteConnection connection, long id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM rows WHERE id=$id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private static List<double> MeasureRepeated(int warmup, int measured, Action action)
    {
        var samples = new List<double>();
        for (var i = -warmup; i < measured; i++)
        {
            var value = Measure(action);
            if (i >= 0) samples.Add(value);
        }

        return samples;
    }

    private static double Measure(Action action)
    {
        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static EngineResult Result(
        string engine,
        string metric,
        IReadOnlyList<double> samples,
        Row[] actualRows,
        string dir,
        ResourceSnapshot before,
        IReadOnlyList<double>? build = null,
        IReadOnlyList<double>? flush = null,
        IReadOnlyList<double>? load = null,
        IReadOnlyList<double>? open = null,
        IReadOnlyList<double>? durable = null,
        int durableBatchSize = 0) =>
        new(
            engine,
            "Measured",
            metric,
            samples,
            actualRows.Length,
            BenchmarkChecksum.HashRows(actualRows),
            BenchmarkPaths.DirBytes(dir),
            before,
            BenchmarkResources.Capture(),
            build,
            flush,
            null,
            load,
            open,
            durable,
            durableBatchSize);
}
