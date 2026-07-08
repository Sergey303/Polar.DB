using System.Diagnostics;
using Polar.Universal;

namespace PolarDbBenchmarks;

internal static class PolarLifecycleEngine
{
    public static EngineResult Run(ExperimentOptions options, Row[] data, string dir)
    {
        if (options.Kind == ExperimentKind.BuildPrimaryIntOnly) return BuildPrimaryIntOnly(options, data, dir);
        if (options.Kind == ExperimentKind.ReopenOnly) return ReopenOnly(options, data, dir);
        if (options.Kind == ExperimentKind.AppendOnly) return AppendOnly(options, data, dir);
        return DeleteOnly(options, data, dir);
    }

    public static EngineResult RunPreboxedPrimaryIntOnly(ExperimentOptions options, Row[] data, string dir)
    {
        if (options.Kind != ExperimentKind.BuildPrimaryIntOnly)
            throw new ArgumentException("Preboxed primary benchmark is only valid for BuildPrimaryIntOnly.", nameof(options));

        var preboxedIds = data.Select(row => (object)row.Id).ToArray();
        return BuildPrimaryIntOnly(options, data, dir,
            store => store.Sequence.Load(preboxedIds), "polar-db-preboxed-load");
    }

    public static EngineResult RunFixedInt64BulkPrimaryIntOnly(ExperimentOptions options, Row[] data, string dir)
    {
        if (options.Kind != ExperimentKind.BuildPrimaryIntOnly)
            throw new ArgumentException("Fixed Int64 bulk benchmark is only valid for BuildPrimaryIntOnly.", nameof(options));

        var ids = data.Select(row => row.Id).ToArray();
        return BuildPrimaryIntOnly(options, data, dir,
            store => store.Sequence.LoadFixedInt64ForBenchmark(ids), "polar-db-fixed64-bulk-load");
    }

    public static EngineResult RunFixedInt64StorageOnlyPrimaryIntOnly(ExperimentOptions options, Row[] data, string dir)
    {
        if (options.Kind != ExperimentKind.BuildPrimaryIntOnly)
            throw new ArgumentException("Fixed Int64 storage-only benchmark is only valid for BuildPrimaryIntOnly.", nameof(options));

        var ids = data.Select(row => row.Id).ToArray();
        return BuildPrimaryIntOnly(options, data, dir,
            store => store.Sequence.LoadFixedInt64StorageOnlyForBenchmark(ids), "polar-db-fixed64-storage-only-load");
    }

    public static EngineResult RunFixedInt64TypedMetadataProbePrimaryIntOnly(
        ExperimentOptions options, Row[] data, string dir)
    {
        if (options.Kind != ExperimentKind.BuildPrimaryIntOnly)
            throw new ArgumentException("Fixed Int64 typed metadata probe is only valid for BuildPrimaryIntOnly.", nameof(options));

        var ids = data.Select(row => row.Id).ToArray();
        return BuildPrimaryIntOnly(options, data, dir,
            store => store.Sequence.LoadFixedInt64TypedMetadataProbeForBenchmark(ids, BenchmarkChecksum.StableHash),
            "polar-db-fixed64-typed-metadata-probe");
    }

    public static EngineResult RunFixedInt64TypedBuildProbePrimaryIntOnly(
        ExperimentOptions options, Row[] data, string dir)
    {
        if (options.Kind != ExperimentKind.BuildPrimaryIntOnly)
            throw new ArgumentException("Fixed Int64 typed build probe is only valid for BuildPrimaryIntOnly.", nameof(options));

        var ids = data.Select(row => row.Id).ToArray();
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
            Int64TypedPrimaryBuildProbeEntry[]? entries = null;

            var loadMs = Measure(() => entries = Int64TypedPrimaryBuildProbe.Load(store.Sequence, ids));
            var total = Stopwatch.StartNew();
            var profile = UIndexBuildProfile.Empty;
            var buildMs = Measure(() => profile = Int64TypedPrimaryBuildProbe.Build(store.Sequence, entries!));
            var flushMs = Measure(() => store.Sequence.Flush());
            total.Stop();

            if (i == options.MeasuredOps - 1)
                VerifyTypedBuildProbe(store.Sequence, ids);

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

        Int64TypedPrimaryBuildProbeSemantics.VerifyDuplicateKeys(Path.Combine(dir, "duplicate-key-semantics"));

        return Result("polar-db-fixed64-typed-build-probe", totalSamples, data, artifactDir, before,
            buildSamples, flushSamples, stages.ToImmutable(), loadSamples);
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
        var rows = data;
        return Result(engineName, totalSamples, rows, artifactDir, before,
            buildSamples, flushSamples, stages.ToImmutable(), loadSamples);
    }

    private static EngineResult ReopenOnly(ExperimentOptions options, Row[] data, string dir)
    {
        var before = BenchmarkResources.Capture();
        var prepared = PrepareBuiltStore(dir, data, ExperimentKind.ReopenOnly);
        prepared.Sequence.Close();
        var samples = new List<double>();

        for (var i = 0; i < options.MeasuredOps + options.WarmupOps; i++)
        {
            var ms = Measure(() =>
            {
                var store = PolarStoreFactory.Open(dir, ExperimentKind.ReopenOnly);
                store.Sequence.Refresh();
                store.Sequence.Close();
            });
            if (i >= options.WarmupOps) samples.Add(ms);
        }

        return Result("polar-db-current", samples,
            PolarMaterializer.ReadAll(dir, ExperimentKind.ReopenOnly), dir, before);
    }

    private static EngineResult AppendOnly(ExperimentOptions options, Row[] data, string dir)
    {
        var before = BenchmarkResources.Capture();
        var store = PrepareBuiltStore(dir, data, ExperimentKind.AppendOnly);
        var appendRows = BenchmarkData.Dataset(options.MeasuredOps, options.Kind, data.Length + 1);
        var samples = new List<double>();
        foreach (var row in appendRows)
            samples.Add(Measure(() => store.Sequence.AppendElement(PolarRows.ToPolar(row))));

        var rows = PolarMaterializer.ReadAll(store);
        store.Sequence.Flush();
        store.Sequence.Close();
        return Result("polar-db-current", samples, rows, dir, before);
    }

    private static EngineResult DeleteOnly(ExperimentOptions options, Row[] data, string dir)
    {
        var before = BenchmarkResources.Capture();
        var store = PrepareBuiltStore(dir, data, ExperimentKind.DeleteOnly);
        var samples = new List<double>();
        foreach (var key in BenchmarkData.PrimaryKeys(data, options.MeasuredOps))
            samples.Add(Measure(() => store.Sequence.AppendElement(PolarRows.Tombstone(key))));

        var rows = PolarMaterializer.ReadAll(store);
        store.Sequence.Flush();
        store.Sequence.Close();
        return Result("polar-db-current", samples, rows, dir, before);
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

    private static void VerifyTypedBuildProbe(USequence sequence, long[] ids)
    {
        if (sequence.Count() != ids.LongLength)
            throw new InvalidDataException(
                $"Typed Int64 primary build probe count mismatch. Expected {ids.LongLength}, actual {sequence.Count()}.");

        var index = 0;
        foreach (var value in sequence.ElementValues())
        {
            if (index >= ids.Length)
                throw new InvalidDataException("Typed Int64 primary build probe materialized more values than expected.");
            if (value is not long actual || actual != ids[index])
                throw new InvalidDataException(
                    $"Typed Int64 primary build probe materialization mismatch at index {index}.");
            index++;
        }

        if (index != ids.Length)
            throw new InvalidDataException(
                $"Typed Int64 primary build probe materialized {index} values, expected {ids.Length}.");

        var sampleCount = Math.Min(257, ids.Length);
        for (var sample = 0; sample < sampleCount; sample++)
        {
            var position = sampleCount == 1
                ? 0
                : (int)((long)sample * (ids.Length - 1) / (sampleCount - 1));
            var key = ids[position];
            var value = sequence.GetByKey(key);
            if (value is not long actual || actual != key)
                throw new InvalidDataException($"Typed Int64 primary build probe lookup failed for key {key}.");
        }
    }

    private static double Measure(Action action)
    {
        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static EngineResult Result(
        string engine, IReadOnlyList<double> samples, Row[] actualRows, string dir,
        ResourceSnapshot before, IReadOnlyList<double>? build = null,
        IReadOnlyList<double>? flush = null, PrimaryBuildStageSamples? stages = null,
        IReadOnlyList<double>? load = null) =>
        new(engine, "Measured", samples, actualRows.Length, BenchmarkChecksum.HashRows(actualRows),
            BenchmarkPaths.DirBytes(dir), before, BenchmarkResources.Capture(), build, flush, stages, load);

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
