using System.Diagnostics;
using Polar.Universal;

namespace PolarDbBenchmarks;

internal static class SetPrimaryKeyLifecycleEngine
{
    public static EngineResult Run(ExperimentOptions options, Row[] data, string dir)
    {
        if (options.Kind != ExperimentKind.BuildPrimaryIntOnly)
            throw new ArgumentException("SetPrimaryKey benchmark is only valid for BuildPrimaryIntOnly.", nameof(options));

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
            var store = PolarStoreFactory.OpenWithSetPrimaryKey(runDir, ExperimentKind.BuildPrimaryIntOnly);

            var loadMs = Measure(() => store.Sequence.LoadFixedInt64ForBenchmark(ids));
            var total = Stopwatch.StartNew();
            var buildMs = Measure(() => store.Sequence.Build());
            var profile = store.Sequence.LastPrimaryBuildProfile;
            var flushMs = Measure(() => store.Sequence.Flush());
            total.Stop();

            if (i == options.MeasuredOps - 1)
                VerifyLookups(store.Sequence, ids);

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

        VerifyDuplicateKeys(Path.Combine(dir, "set-primary-key-duplicate-semantics"));

        return new EngineResult(
            "polar-db-set-primary-key-fixed64-load",
            "Measured",
            totalSamples,
            data.LongLength,
            BenchmarkChecksum.HashRows(data),
            BenchmarkPaths.DirBytes(artifactDir),
            before,
            BenchmarkResources.Capture(),
            buildSamples,
            flushSamples,
            stages.ToImmutable(),
            loadSamples);
    }

    private static void VerifyLookups(USequence sequence, long[] ids)
    {
        if (ids.Length == 0) return;

        var sampleCount = Math.Min(257, ids.Length);
        for (var sample = 0; sample < sampleCount; sample++)
        {
            var position = sampleCount == 1
                ? 0
                : (int)((long)sample * (ids.Length - 1) / (sampleCount - 1));
            var key = ids[position];
            var value = sequence.GetByKey(key);
            if (value is not long actual || actual != key)
                throw new InvalidDataException($"SetPrimaryKey typed build lookup failed for key {key}.");
        }
    }

    private static void VerifyDuplicateKeys(string dir)
    {
        Directory.CreateDirectory(dir);
        var store = PolarStoreFactory.OpenWithSetPrimaryKey(dir, ExperimentKind.BuildPrimaryIntOnly);
        try
        {
            long[] values = { 11, 22, 11, 33, 22 };
            store.Sequence.LoadFixedInt64ForBenchmark(values);
            store.Sequence.Build();

            var materialized = store.Sequence.ElementValues().Cast<long>().ToArray();
            long[] expected = { 11, 33, 22 };
            if (!materialized.SequenceEqual(expected))
                throw new InvalidDataException(
                    "SetPrimaryKey typed build did not preserve latest physical duplicate-key entries.");

            foreach (var key in expected)
            {
                var value = store.Sequence.GetByKey(key);
                if (value is not long actual || actual != key)
                    throw new InvalidDataException($"SetPrimaryKey duplicate-key lookup failed for key {key}.");
            }
        }
        finally
        {
            store.Sequence.Close();
        }
    }

    private static double Measure(Action action)
    {
        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private sealed class MutablePrimaryBuildStages
    {
        private readonly List<double> scan = new();
        private readonly List<double> toArray = new();
        private readonly List<double> sort = new();
        private readonly List<double> writeHashKeys = new();
        private readonly List<double> writeOffsets = new();
        private readonly List<double> gc = new();
        private readonly List<double> total = new();

        public void Add(UIndexBuildProfile profile)
        {
            scan.Add(profile.ScanMs);
            toArray.Add(profile.ToArrayMs);
            sort.Add(profile.SortMs);
            writeHashKeys.Add(profile.WriteHashKeysMs);
            writeOffsets.Add(profile.WriteOffsetsMs);
            gc.Add(profile.GcMs);
            total.Add(profile.TotalMs);
        }

        public PrimaryBuildStageSamples ToImmutable() =>
            new(scan, toArray, sort, writeHashKeys, writeOffsets, gc, total);
    }
}
