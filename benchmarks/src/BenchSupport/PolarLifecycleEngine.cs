using System.Diagnostics;
using Polar.Universal;

namespace PolarDbBenchmarks;

internal static class PolarLifecycleEngine
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
        return BuildPrimaryIntOnly(options, data, dir,
            store => store.Sequence.Load(data.Select(row => (object)row.Id)), "polar-db-current");
    }

    private static EngineResult BuildPrimaryIntOnly(
        ExperimentOptions options, Row[] data, string dir, Action<PolarStore> load, string engineName)
    {
        var before = BenchmarkResources.Capture();
        var totalSamples = new List<double>();
        var loadSamples = new List<double>();
        var buildSamples = new List<double>();
        var flushSamples = new List<double>();
        var stages = new MutablePrimaryBuildStages();
        var artifactDir = dir;

        for (var i = -options.WarmupOps; i < options.MeasuredOps; i++)
        {
            var runDir = Path.Combine(dir, "run-" + i);
            Directory.CreateDirectory(runDir);
            var store = PolarStoreFactory.Open(runDir, ExperimentKind.BuildPrimaryIntOnly);
            var loadMs = Measure(() => load(store));
            var total = Stopwatch.StartNew();
            var buildMs = Measure(() => store.Sequence.Build());
            var profile = store.Sequence.LastPrimaryBuildProfile;
            var flushMs = Measure(() => store.Sequence.Flush());
            total.Stop();
            store.Sequence.Close();

            if (i >= 0)
            {
                totalSamples.Add(total.Elapsed.TotalMilliseconds);
                loadSamples.Add(loadMs);
                buildSamples.Add(buildMs);
                flushSamples.Add(flushMs);
                stages.Add(profile);
                artifactDir = runDir;
            }
        }

        return Result(
            engineName,
            "build + flush",
            totalSamples,
            data,
            artifactDir,
            before,
            buildSamples,
            flushSamples,
            stages.ToImmutable(),
            loadSamples);
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
            var value = store.Sequence.GetByKey(data[0].Id);
            if (value == null) throw new InvalidDataException("Polar.DB reopen lookup returned no row.");
            var row = PolarRows.FromPolar(value);
            var checksum = BenchmarkChecksum.HashRows(new[] { row });
            if (checksum != expectedLookup)
                throw new InvalidDataException("Polar.DB reopen lookup returned an unexpected row.");
            store.Sequence.Close();
        });

        return Result(
            "polar-db-current",
            "query-ready reopen",
            queryReady,
            PolarMaterializer.ReadAll(dir, ExperimentKind.ReopenOnly),
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
        var expected = (append ? original.Concat(appended) : original.Skip(operationCount)).ToArray();
        if (actual.Length != expected.Length ||
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
        PrimaryBuildStageSamples? stages = null,
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
            stages,
            load,
            open,
            durable,
            durableBatchSize);

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
