using System.Diagnostics;
using System.Reflection;
using Polar.DB.Bench.Exec.PolarDbNuget.Contracts;

namespace Polar.DB.Bench.Exec.PolarDbNuget.Workloads;

internal sealed class PolarDbWorkloadHost
{
    private readonly Assembly _assembly;

    public PolarDbWorkloadHost(Assembly assembly)
    {
        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
    }

    public WorkloadResult Execute(ExperimentDocument experiment, string workDirectory)
    {
        if (!string.Equals(experiment.ScenarioKey, "load-build-refresh-lookup", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Unsupported scenarioKey: {experiment.ScenarioKey}. Supported: load-build-refresh-lookup.");
        }

        var result = new WorkloadResult();
        var api = new ReflectivePolarDbApiShape(_assembly);

        var recordCount = Math.Max(0, experiment.Dataset.RecordCount);
        var lookupCount = Math.Max(0, experiment.Workload.LookupCount);
        var keys = GenerateKeys(experiment.Dataset, recordCount);
        var lookupKeys = GenerateLookupKeys(experiment, keys, lookupCount);

        var databasePath = Path.Combine(workDirectory, "polar-db-nuget.data.db");
        var statePath = Path.Combine(workDirectory, "polar-db-nuget.state");

        object? sequence = null;
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);

        try
        {
            var recordType = api.CreateRecordType();
            sequence = api.CreateSequence(recordType, databasePath, statePath);

            var loadWatch = Stopwatch.StartNew();
            for (var i = 0; i < recordCount; i++)
            {
                api.AppendRecord(sequence, CreateRecord(keys[i], i));
            }
            loadWatch.Stop();

            result.Metrics["recordCount"] = recordCount;
            result.Metrics["loadElapsedMs"] = loadWatch.Elapsed.TotalMilliseconds;
            result.Metrics["loadThroughputRecordsPerSecond"] = loadWatch.Elapsed.TotalSeconds > 0
                ? recordCount / loadWatch.Elapsed.TotalSeconds
                : 0;

            if (experiment.Workload.BuildBeforeLookup)
            {
                var buildWatch = Stopwatch.StartNew();
                api.Build(sequence);
                buildWatch.Stop();
                result.Metrics["buildElapsedMs"] = buildWatch.Elapsed.TotalMilliseconds;
            }

            if (experiment.Workload.RefreshBeforeLookup)
            {
                var refreshWatch = Stopwatch.StartNew();
                api.Refresh(sequence);
                refreshWatch.Stop();
                result.Metrics["refreshElapsedMs"] = refreshWatch.Elapsed.TotalMilliseconds;
            }

            var lookupWatch = Stopwatch.StartNew();
            var found = 0;
            for (var i = 0; i < lookupKeys.Length; i++)
            {
                var value = api.Lookup(sequence, lookupKeys[i]);
                if (value != null)
                {
                    found++;
                }
            }
            lookupWatch.Stop();

            result.Metrics["lookupCount"] = lookupKeys.Length;
            result.Metrics["lookupFoundCount"] = found;
            result.Metrics["lookupElapsedMs"] = lookupWatch.Elapsed.TotalMilliseconds;
            result.Metrics["lookupThroughputOperationsPerSecond"] = lookupWatch.Elapsed.TotalSeconds > 0
                ? lookupKeys.Length / lookupWatch.Elapsed.TotalSeconds
                : 0;
        }
        finally
        {
            if (sequence != null)
            {
                api.DisposeIfSupported(sequence);
            }
        }

        var process = Process.GetCurrentProcess();
        result.Metrics["allocatedBytes"] = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        result.Metrics["gen0Collections"] = GC.CollectionCount(0) - gen0Before;
        result.Metrics["gen1Collections"] = GC.CollectionCount(1) - gen1Before;
        result.Metrics["gen2Collections"] = GC.CollectionCount(2) - gen2Before;
        result.Metrics["workingSetBytes"] = process.WorkingSet64;
        result.Metrics["peakWorkingSetBytes"] = process.PeakWorkingSet64;

        return result;
    }

    private static object[] CreateRecord(int key, int ordinal)
    {
        return new object[]
        {
            key,
            $"name-{ordinal:D8}"
        };
    }

    private static int[] GenerateKeys(DatasetSpec dataset, int recordCount)
    {
        var keys = new int[recordCount];
        var duplicateModulo = Math.Max(0, dataset.DuplicateModulo);

        for (var i = 0; i < recordCount; i++)
        {
            keys[i] = duplicateModulo > 0 ? i % duplicateModulo : i;
        }

        if (string.Equals(dataset.KeyPattern, "reverse", StringComparison.OrdinalIgnoreCase))
        {
            Array.Reverse(keys);
        }
        else if (string.Equals(dataset.KeyPattern, "random", StringComparison.OrdinalIgnoreCase))
        {
            Shuffle(keys, new Random(dataset.Seed));
        }

        return keys;
    }

    private static int[] GenerateLookupKeys(ExperimentDocument experiment, int[] existingKeys, int lookupCount)
    {
        if (lookupCount <= 0 || existingKeys.Length == 0)
        {
            return Array.Empty<int>();
        }

        var random = new Random(experiment.Dataset.Seed ^ 0x5A17_2026);
        var result = new int[lookupCount];

        if (string.Equals(experiment.Workload.LookupPattern, "sequential-existing", StringComparison.OrdinalIgnoreCase))
        {
            for (var i = 0; i < lookupCount; i++)
            {
                result[i] = existingKeys[i % existingKeys.Length];
            }

            return result;
        }

        for (var i = 0; i < lookupCount; i++)
        {
            result[i] = existingKeys[random.Next(existingKeys.Length)];
        }

        return result;
    }

    private static void Shuffle<T>(T[] values, Random random)
    {
        for (var i = values.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
