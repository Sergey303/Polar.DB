using System.Diagnostics;
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
    private static EngineResult BuildPrimaryIntOnly(ExperimentOptions options, Row[] data, string dir)
    {
        var before = BenchmarkResources.Capture();
        var totalSamples = new List<double>();
        var buildSamples = new List<double>();
        var flushSamples = new List<double>();
        var stages = new MutablePrimaryBuildStages();
        var artifactDir = dir;
        for (var i = -options.WarmupOps; i < options.MeasuredOps; i++)
        {
            var runDir = Path.Combine(dir, "run-" + i);
            Directory.CreateDirectory(runDir);
            var store = PolarStoreFactory.Open(runDir, ExperimentKind.BuildPrimaryIntOnly);
            store.Sequence.Load(data.Select(row => PolarRows.ToPolar(row)));
            var total = Stopwatch.StartNew();
            var buildMs = Measure(() => store.Sequence.Build());
            var profile = store.Sequence.LastPrimaryBuildProfile;
            var flushMs = Measure(() => store.Sequence.Flush());
            total.Stop();
            store.Sequence.Close();
            if (i >= 0)
            {
                totalSamples.Add(total.Elapsed.TotalMilliseconds);
                buildSamples.Add(buildMs);
                flushSamples.Add(flushMs);
                stages.Add(profile);
                artifactDir = runDir;
            }
        }
        var rows = PolarMaterializer.ReadAll(artifactDir, ExperimentKind.BuildPrimaryIntOnly);
        return Result("polar-db-current", totalSamples, rows, artifactDir, before,
            buildSamples, flushSamples, stages.ToImmutable());
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
        IReadOnlyList<double>? flush = null, PrimaryBuildStageSamples? stages = null) =>
        new(engine, "Measured", samples, actualRows.Length, BenchmarkChecksum.HashRows(actualRows),
            BenchmarkPaths.DirBytes(dir), before, BenchmarkResources.Capture(), build, flush, stages);

    private sealed class MutablePrimaryBuildStages
    {
        private readonly List<double> _scan = new();
        private readonly List<double> _toArray = new();
        private readonly List<double> _sort = new();
        private readonly List<double> _writeHashKeys = new();
        private readonly List<double> _writeOffsets = new();
        private readonly List<double> _gc = new();
        private readonly List<double> _total = new();

        public void Add(Polar.Universal.UIndexBuildProfile profile)
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
