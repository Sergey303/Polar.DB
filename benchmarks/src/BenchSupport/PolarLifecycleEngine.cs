using System.Diagnostics;
using Polar.DB.ExternalKey;
using Polar.Universal;

namespace PolarDbBenchmarks;

internal static class PolarLifecycleEngine
{
    public static EngineResult Run(ExperimentOptions options, Row[] data, string dir)
    {
        return options.Kind switch
        {
            ExperimentKind.BuildPrimaryIntOnly => BuildPrimaryIntOnly(options, data, dir),
            ExperimentKind.BuildExternalIndexesOnly => BuildExternalIndexesOnly(options, data, dir),
            ExperimentKind.TraversalOnly => TraversalOnly(options, data, dir),
            ExperimentKind.ReopenWithTail => ReopenWithTail(options, data, dir),
            ExperimentKind.ReopenOnly => ReopenOnly(options, data, dir),
            ExperimentKind.AppendOnly => Mutation(options, data, dir, append: true),
            _ => Mutation(options, data, dir, append: false)
        };
    }

    private static EngineResult BuildPrimaryIntOnly(ExperimentOptions options, Row[] data, string dir)
    {
        return BuildPrimaryIntOnly(
            options,
            data,
            dir,
            store => store.Sequence.Load(data.Select(row => (object)checked((int)row.Id))),
            "polar-db-current");
    }

    private static EngineResult BuildPrimaryIntOnly(
        ExperimentOptions options,
        Row[] data,
        string dir,
        Action<PolarStore> load,
        string engineName)
    {
        var before = BenchmarkResources.Capture();
        var totalSamples = new List<double>();
        var loadSamples = new List<double>();
        var buildSamples = new List<double>();
        var flushSamples = new List<double>();
        var loadAllocationGc = new MutableAllocationGcSamples();
        var buildAllocationGc = new MutableAllocationGcSamples();
        var stages = new MutablePrimaryBuildStages();
        var artifactDir = dir;
        ResourceSnapshot? liveResources = null;

        for (var i = -options.WarmupOps; i < options.MeasuredOps; i++)
        {
            var runDir = Path.Combine(dir, "run-" + i);
            Directory.CreateDirectory(runDir);
            var store = PolarStoreFactory.Open(runDir, ExperimentKind.BuildPrimaryIntOnly);
            var loadMeasurement = BenchmarkMeasure.Profile(() => load(store));

            var total = Stopwatch.StartNew();
            var buildMeasurement = BenchmarkMeasure.Profile(() => store.Sequence.Build());
            var profile = store.Sequence.LastPrimaryBuildProfile;
            var flushMs = Measure(() => store.Sequence.Flush());
            total.Stop();

            if (i >= 0)
            {
                totalSamples.Add(total.Elapsed.TotalMilliseconds);
                loadSamples.Add(loadMeasurement.ElapsedMs);
                buildSamples.Add(buildMeasurement.ElapsedMs);
                flushSamples.Add(flushMs);
                loadAllocationGc.Add(loadMeasurement);
                buildAllocationGc.Add(buildMeasurement);
                stages.Add(profile);
                artifactDir = runDir;
            }

            if (i == options.MeasuredOps - 1)
            {
                liveResources = BenchmarkResources.Capture();
                GC.KeepAlive(store);
            }

            store.Sequence.Close();
            if (i != options.MeasuredOps - 1)
                BenchmarkPaths.TryDeleteDirectory(runDir);
        }

        return Result(
            engineName,
            "build + flush",
            totalSamples,
            data,
            artifactDir,
            before,
            after: liveResources,
            build: buildSamples,
            flush: flushSamples,
            stages: stages.ToImmutable(),
            load: loadSamples,
            loadAllocationGc: loadAllocationGc.ToImmutable(),
            buildAllocationGc: buildAllocationGc.ToImmutable());
    }

    private static EngineResult BuildExternalIndexesOnly(ExperimentOptions options, Row[] data, string dir)
    {
        var before = BenchmarkResources.Capture();
        var totalSamples = new List<double>();
        var buildSamples = new List<double>();
        var flushSamples = new List<double>();
        var buildAllocationGc = new MutableAllocationGcSamples();
        var artifactDir = dir;
        ResourceSnapshot? liveResources = null;

        for (var i = -options.WarmupOps; i < options.MeasuredOps; i++)
        {
            var runDir = Path.Combine(dir, "run-" + i);
            Directory.CreateDirectory(runDir);
            var store = PolarStoreFactory.Open(runDir, ExperimentKind.BuildExternalIndexesOnly);
            var indexes = store.Sequence.uindexes;

            store.Sequence.uindexes = Array.Empty<IUIndex>();
            store.Sequence.Load(data.Select(row => PolarRows.ToPolar(row)));
            store.Sequence.Build();
            store.Sequence.Flush();
            store.Sequence.uindexes = indexes;

            var total = Stopwatch.StartNew();
            var buildMeasurement = BenchmarkMeasure.Profile(() =>
            {
                foreach (var index in indexes) index.Build();
            });
            var flushMs = Measure(() =>
            {
                foreach (var index in indexes) index.Flush();
            });
            total.Stop();

            ValidateExternalIndexes(store, data);

            if (i >= 0)
            {
                totalSamples.Add(total.Elapsed.TotalMilliseconds);
                buildSamples.Add(buildMeasurement.ElapsedMs);
                flushSamples.Add(flushMs);
                buildAllocationGc.Add(buildMeasurement);
                artifactDir = runDir;
            }

            if (i == options.MeasuredOps - 1)
            {
                liveResources = BenchmarkResources.Capture();
                GC.KeepAlive(store);
            }

            store.Sequence.Close();
            if (i != options.MeasuredOps - 1)
                BenchmarkPaths.TryDeleteDirectory(runDir);
        }

        return Result(
            "polar-db-current",
            "external indexes build + flush",
            totalSamples,
            data,
            artifactDir,
            before,
            after: liveResources,
            build: buildSamples,
            flush: flushSamples,
            buildAllocationGc: buildAllocationGc.ToImmutable());
    }

    private static EngineResult TraversalOnly(ExperimentOptions options, Row[] data, string dir)
    {
        var before = BenchmarkResources.Capture();
        var store = PrepareBuiltStore(dir, data, ExperimentKind.TraversalOnly);
        var expected = new QueryResult(data.LongLength, BenchmarkChecksum.HashRows(data));
        var samples = new List<double>();

        for (var i = -options.WarmupOps; i < options.MeasuredOps; i++)
        {
            QueryResult actual = default!;
            var elapsed = Measure(() => actual = ScanAll(store));
            if (actual != expected)
                throw new InvalidDataException("Polar.DB traversal returned unexpected rows.");
            if (i >= 0) samples.Add(elapsed);
        }

        var liveResources = BenchmarkResources.Capture();
        GC.KeepAlive(store);
        store.Sequence.Close();

        return Result(
            "polar-db-current",
            "full logical traversal",
            samples,
            data,
            dir,
            before,
            after: liveResources);
    }

    private static EngineResult ReopenWithTail(ExperimentOptions options, Row[] data, string dir)
    {
        var before = BenchmarkResources.Capture();
        Directory.CreateDirectory(dir);
        var prepared = PolarStoreFactory.Open(dir, ExperimentKind.BuildPrimaryIntOnly);
        prepared.Sequence.Load(data.Select(row => (object)checked((int)row.Id)));
        prepared.Sequence.Build();
        prepared.Sequence.Flush();

        var tail = BenchmarkData.Dataset(
            BenchmarkDefaults.ReopenTailRows,
            ExperimentKind.ReopenWithTail,
            data.LongLength + 1L);
        foreach (var row in tail)
            prepared.Sequence.AppendElement(checked((int)row.Id));
        prepared.Sequence.Flush();
        prepared.Sequence.Close();

        var lastKey = checked((int)tail[^1].Id);
        var samples = MeasureRepeated(options.WarmupOps, options.MeasuredOps, () =>
        {
            var store = PolarStoreFactory.Open(dir, ExperimentKind.BuildPrimaryIntOnly);
            store.Sequence.Refresh();
            ValidatePrimaryIntLookup(store, lastKey, "tail replay");
            store.Sequence.Close();
        });

        var liveStore = PolarStoreFactory.Open(dir, ExperimentKind.BuildPrimaryIntOnly);
        liveStore.Sequence.Refresh();
        ValidatePrimaryIntLookup(liveStore, lastKey, "tail replay live snapshot");
        var liveResources = BenchmarkResources.Capture();
        GC.KeepAlive(liveStore);
        liveStore.Sequence.Close();

        var rows = checked(data.LongLength + tail.LongLength);
        var checksum = BenchmarkChecksum.HashInt32Values(
            data.Select(row => checked((int)row.Id))
                .Concat(tail.Select(row => checked((int)row.Id))));
        return Result(
            "polar-db-current",
            "fixed-int query-ready reopen with dynamic tail",
            samples,
            rows,
            checksum,
            dir,
            before,
            after: liveResources);
    }

    private static EngineResult ReopenOnly(ExperimentOptions options, Row[] data, string dir)
    {
        var before = BenchmarkResources.Capture();
        var prepared = PrepareBuiltStore(dir, data, ExperimentKind.ReopenOnly);
        prepared.Sequence.Close();

        var openOnly = MeasureRepeated(options.WarmupOps, options.MeasuredOps, () =>
        {
            var store = PolarStoreFactory.Open(dir, ExperimentKind.ReopenOnly);
            store.Sequence.Close();
        });

        var expectedLookup = BenchmarkChecksum.HashRows(new[] { data[0] });
        var queryReady = MeasureRepeated(options.WarmupOps, options.MeasuredOps, () =>
        {
            var store = PolarStoreFactory.Open(dir, ExperimentKind.ReopenOnly);
            store.Sequence.Refresh();
            ValidatePrimaryLookup(store, data[0].Id, expectedLookup, "reopen");
            store.Sequence.Close();
        });

        var liveStore = PolarStoreFactory.Open(dir, ExperimentKind.ReopenOnly);
        liveStore.Sequence.Refresh();
        ValidatePrimaryLookup(liveStore, data[0].Id, expectedLookup, "reopen live snapshot");
        var liveResources = BenchmarkResources.Capture();
        GC.KeepAlive(liveStore);
        liveStore.Sequence.Close();

        return Result(
            "polar-db-current",
            "query-ready reopen",
            queryReady,
            PolarMaterializer.ReadAll(dir, ExperimentKind.ReopenOnly),
            dir,
            before,
            after: liveResources,
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

        var volatileStore = PrepareBuiltStore(volatileDir, data, options.Kind);
        var volatileSamples = new List<double>();
        if (append)
        {
            var rows = BenchmarkData.Dataset(options.MeasuredOps, options.Kind, data.Length + 1);
            foreach (var row in rows)
                volatileSamples.Add(Measure(() => volatileStore.Sequence.AppendElement(PolarRows.ToPolar(row))));
        }
        else
        {
            foreach (var key in BenchmarkData.PrimaryKeys(data, options.MeasuredOps))
                volatileSamples.Add(Measure(() => volatileStore.Sequence.AppendElement(PolarRows.Tombstone(key))));
        }

        var actualRows = PolarMaterializer.ReadAll(volatileStore);
        volatileStore.Sequence.Flush();
        volatileStore.Sequence.Close();

        var durableSamples = MeasureDurableBatches(options, data, durableDir, append);

        var result = Result(
            "polar-db-current",
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
        var warmRows = data.Take(Math.Min(data.Length, 50_000)).ToArray();
        var store = PrepareBuiltStore(dir, warmRows, options.Kind);
        if (append)
        {
            foreach (var row in BenchmarkData.Dataset(options.WarmupOps, options.Kind, warmRows.Length + 1))
                store.Sequence.AppendElement(PolarRows.ToPolar(row));
        }
        else
        {
            foreach (var key in BenchmarkData.PrimaryKeys(warmRows, Math.Min(options.WarmupOps, warmRows.Length)))
                store.Sequence.AppendElement(PolarRows.Tombstone(key));
        }

        store.Sequence.Flush();
        store.Sequence.Close();
    }

    private static IReadOnlyList<double> MeasureDurableBatches(
        ExperimentOptions options,
        Row[] data,
        string dir,
        bool append)
    {
        var store = PrepareBuiltStore(dir, data, options.Kind);
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

        var samples = new List<double>();
        var offset = 0;
        for (var batch = 0; batch < warmupBatches + measuredBatches; batch++)
        {
            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < batchSize; i++)
            {
                if (append)
                    store.Sequence.AppendElement(PolarRows.ToPolar(appendRows[offset++]));
                else
                    store.Sequence.AppendElement(PolarRows.Tombstone(deleteKeys[offset++]));
            }

            store.Sequence.Flush();
            BenchmarkDurability.SyncDirectoryFiles(dir);
            stopwatch.Stop();
            if (batch >= warmupBatches)
                samples.Add(stopwatch.Elapsed.TotalMilliseconds / batchSize);
        }

        var actualRows = PolarMaterializer.ReadAll(store);
        ValidateDurableRows(data, appendRows, actualRows, append, totalOps);
        store.Sequence.Close();
        return samples;
    }

    private static void ValidateDurableRows(
        Row[] original,
        Row[] appended,
        Row[] actual,
        bool append,
        int operationCount)
    {
        var expected = append ? original.Concat(appended) : original.Skip(operationCount);
        if (actual.LongLength != expected.LongCount() ||
            BenchmarkChecksum.HashRows(actual) != BenchmarkChecksum.HashRows(expected))
            throw new InvalidDataException("Polar.DB durable mutation result failed correctness validation.");
    }

    private static PolarStore PrepareBuiltStore(string dir, Row[] data, ExperimentKind kind)
    {
        Directory.CreateDirectory(dir);
        var store = PolarStoreFactory.Open(dir, kind);
        store.Sequence.Load(data.Select(row => PolarRows.ToPolar(row)));
        store.Sequence.Build();
        store.Sequence.Flush();
        return store;
    }

    private static QueryResult ScanAll(PolarStore store)
    {
        var accumulator = new BenchmarkRowAccumulator();
        long rows = 0;
        foreach (var value in store.Sequence.ElementValues())
        {
            accumulator.Add(PolarRows.FromPolar(value));
            rows++;
        }

        return new QueryResult(rows, accumulator.Finish());
    }

    private static void ValidatePrimaryIntLookup(
        PolarStore store,
        int key,
        string scenario)
    {
        var value = store.Sequence.GetByKey(key);
        if (value is not int actual || actual != key)
            throw new InvalidDataException("Polar.DB " + scenario + " lookup returned an unexpected Int32 key.");
    }

    private static void ValidatePrimaryLookup(
        PolarStore store,
        long key,
        ulong expectedChecksum,
        string scenario)
    {
        var value = store.Sequence.GetByKey(key);
        if (value == null)
            throw new InvalidDataException("Polar.DB " + scenario + " lookup returned no row.");

        var checksum = BenchmarkChecksum.HashRows(new[] { PolarRows.FromPolar(value) });
        if (checksum != expectedChecksum)
            throw new InvalidDataException("Polar.DB " + scenario + " lookup returned an unexpected row.");
    }

    private static void ValidateExternalIndexes(PolarStore store, Row[] data)
    {
        ValidateExternalIndex(store.IntIndex!, data[0].ExternalId,
            data.Where(row => row.ExternalId == data[0].ExternalId));
        ValidateExternalIndex(store.LongIndex!, data[0].ExternalLong,
            data.Where(row => row.ExternalLong == data[0].ExternalLong));
        ValidateExternalIndex(store.GuidIndex!, data[0].ExternalGuid,
            data.Where(row => row.ExternalGuid == data[0].ExternalGuid));
        ValidateExternalIndex(store.StringIndex!, data[0].ExternalKey,
            data.Where(row => row.ExternalKey == data[0].ExternalKey));
    }

    private static void ValidateExternalIndex(
        IExternalKeyIndex index,
        IComparable key,
        IEnumerable<Row> expectedRows)
    {
        var actual = index.GetManyByValue(key).Select(PolarRows.FromPolar).ToArray();
        var expected = expectedRows.ToArray();
        if (actual.LongLength != expected.LongLength ||
            BenchmarkChecksum.HashRows(actual) != BenchmarkChecksum.HashRows(expected))
            throw new InvalidDataException("Polar.DB external-index build failed correctness validation.");
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
        ResourceSnapshot? after = null,
        IReadOnlyList<double>? build = null,
        IReadOnlyList<double>? flush = null,
        PrimaryBuildStageSamples? stages = null,
        IReadOnlyList<double>? load = null,
        IReadOnlyList<double>? open = null,
        IReadOnlyList<double>? durable = null,
        int durableBatchSize = 0,
        AllocationGcSamples? loadAllocationGc = null,
        AllocationGcSamples? buildAllocationGc = null) =>
        Result(
            engine,
            metric,
            samples,
            actualRows.LongLength,
            BenchmarkChecksum.HashRows(actualRows),
            dir,
            before,
            after,
            build,
            flush,
            stages,
            load,
            open,
            durable,
            durableBatchSize,
            loadAllocationGc,
            buildAllocationGc);

    private static EngineResult Result(
        string engine,
        string metric,
        IReadOnlyList<double> samples,
        long rows,
        ulong checksum,
        string dir,
        ResourceSnapshot before,
        ResourceSnapshot? after = null,
        IReadOnlyList<double>? build = null,
        IReadOnlyList<double>? flush = null,
        PrimaryBuildStageSamples? stages = null,
        IReadOnlyList<double>? load = null,
        IReadOnlyList<double>? open = null,
        IReadOnlyList<double>? durable = null,
        int durableBatchSize = 0,
        AllocationGcSamples? loadAllocationGc = null,
        AllocationGcSamples? buildAllocationGc = null) =>
        new(
            engine,
            "Measured",
            metric,
            samples,
            rows,
            checksum,
            BenchmarkPaths.DirBytes(dir),
            before,
            after ?? BenchmarkResources.Capture(),
            build,
            flush,
            stages,
            load,
            open,
            durable,
            durableBatchSize,
            loadAllocationGc,
            buildAllocationGc);

    private sealed class MutablePrimaryBuildStages
    {
        private readonly List<double> _scan = new();
        private readonly List<double> _toArray = new();
        private readonly List<double> _sort = new();
        private readonly List<double> _writeHashKeys = new();
        private readonly List<double> _writeOffsets = new();
        private readonly List<double> _gc = new();
        private readonly List<double> _total = new();

        public void Add(UIndexBuildProfile profile)
        {
            _scan.Add(profile.ScanMs);
            _toArray.Add(profile.ToArrayMs);
            _sort.Add(profile.SortMs);
            _writeHashKeys.Add(profile.WriteHashKeysMs);
            _writeOffsets.Add(profile.WriteOffsetsMs);
            _gc.Add(profile.GcMs);
            _total.Add(profile.TotalMs);
        }

        public PrimaryBuildStageSamples ToImmutable() =>
            new(_scan, _toArray, _sort, _writeHashKeys, _writeOffsets, _gc, _total);
    }
}
