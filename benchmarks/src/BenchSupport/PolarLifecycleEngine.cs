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
        var samples = new List<double>();
        var artifactDir = dir;
        for (var i = -options.WarmupOps; i < options.MeasuredOps; i++)
        {
            var runDir = Path.Combine(dir, "run-" + i);
            Directory.CreateDirectory(runDir);
            var store = PolarStoreFactory.Open(runDir, ExperimentKind.BuildPrimaryIntOnly);
            store.Sequence.Load(data.Select(row => PolarRows.ToPolar(row)));

            var stopwatch = Stopwatch.StartNew();
            store.Sequence.Build();
            store.Sequence.Flush();
            stopwatch.Stop();
            store.Sequence.Close();

            if (i >= 0)
            {
                samples.Add(stopwatch.Elapsed.TotalMilliseconds);
                artifactDir = runDir;
            }
        }

        var rows = PolarMaterializer.ReadAll(artifactDir, ExperimentKind.BuildPrimaryIntOnly);
        return Result("polar-db-current", samples, rows, artifactDir, before);
    }

    private static EngineResult ReopenOnly(ExperimentOptions options, Row[] data, string dir)
    {
        var before = BenchmarkResources.Capture();
        var prepared = PrepareBuiltStore(dir, data, ExperimentKind.ReopenOnly);
        prepared.Sequence.Close();
        var samples = new List<double>();

        for (var i = 0; i < options.MeasuredOps + options.WarmupOps; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var store = PolarStoreFactory.Open(dir, ExperimentKind.ReopenOnly);
            store.Sequence.Refresh();
            store.Sequence.Close();
            stopwatch.Stop();

            if (i >= options.WarmupOps) samples.Add(stopwatch.Elapsed.TotalMilliseconds);
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
        {
            var stopwatch = Stopwatch.StartNew();
            store.Sequence.AppendElement(PolarRows.ToPolar(row));
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

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
        {
            var stopwatch = Stopwatch.StartNew();
            store.Sequence.AppendElement(PolarRows.Tombstone(key));
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

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

    private static EngineResult Result(string engine, IReadOnlyList<double> samples, Row[] actualRows, string dir, ResourceSnapshot before) =>
        new(engine, "Measured", samples, actualRows.Length, BenchmarkChecksum.HashRows(actualRows),
            BenchmarkPaths.DirBytes(dir), before, BenchmarkResources.Capture());
}
