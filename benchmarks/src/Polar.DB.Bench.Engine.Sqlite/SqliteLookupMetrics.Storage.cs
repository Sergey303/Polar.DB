using System.Collections.Generic;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Engine.Sqlite;

internal static partial class SqliteLookupSeriesExecutor
{
    private static void AddStorageAndRuntimeMetrics(
        List<RunMetric> metrics,
        SqliteLookupRunState state,
        long managedAfter,
        System.GCMemoryInfo gcInfo,
        long processPeakWorkingSet,
        SqliteLookupArtifactInventory artifacts)
    {
        metrics.Add(new RunMetric { MetricKey = "totalArtifactBytes", Value = artifacts.TotalBytes });
        metrics.Add(new RunMetric { MetricKey = "primaryDatabaseBytes", Value = artifacts.PrimaryDatabaseBytes });
        metrics.Add(new RunMetric { MetricKey = "walBytes", Value = artifacts.WalBytes });
        metrics.Add(new RunMetric { MetricKey = "sharedMemoryBytes", Value = artifacts.SharedMemoryBytes });
        metrics.Add(new RunMetric { MetricKey = "managedBytesBefore", Value = state.ManagedBefore });
        metrics.Add(new RunMetric { MetricKey = "managedBytesAfter", Value = managedAfter });
        metrics.Add(new RunMetric { MetricKey = "managedBytesDelta", Value = managedAfter - state.ManagedBefore });
        metrics.Add(new RunMetric { MetricKey = "heapSizeBytes", Value = gcInfo.HeapSizeBytes });
        metrics.Add(new RunMetric { MetricKey = "fragmentedBytes", Value = gcInfo.FragmentedBytes });
        metrics.Add(new RunMetric { MetricKey = "processPeakWorkingSetBytes", Value = processPeakWorkingSet });
    }
}
