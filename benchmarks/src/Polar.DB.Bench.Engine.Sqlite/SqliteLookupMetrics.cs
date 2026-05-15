using System.Collections.Generic;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Engine.Sqlite;

internal static partial class SqliteLookupSeriesExecutor
{
    private static List<RunMetric> BuildMetrics(
        SqliteLookupRunState state,
        long managedAfter,
        System.GCMemoryInfo gcInfo,
        long processPeakWorkingSet,
        SqliteLookupArtifactInventory artifacts)
    {
        var metrics = new List<RunMetric>();
        AddTimingMetrics(metrics, state);
        AddLookupMetrics(metrics, state);
        AddCompatibilityMetrics(metrics, state);
        AddStorageAndRuntimeMetrics(metrics, state, managedAfter, gcInfo, processPeakWorkingSet, artifacts);
        return metrics;
    }

    private static void AddTimingMetrics(List<RunMetric> metrics, SqliteLookupRunState state)
    {
        metrics.Add(new RunMetric { MetricKey = "elapsedMsTotal", Value = state.ElapsedMs });
        metrics.Add(new RunMetric { MetricKey = "elapsedMsSingleRun", Value = state.ElapsedMs });
        metrics.Add(new RunMetric { MetricKey = "loadMs", Value = state.LoadMs });
        metrics.Add(new RunMetric { MetricKey = "buildMs", Value = state.BuildMs });
        metrics.Add(new RunMetric { MetricKey = "reopenRefreshMs", Value = state.ReopenRefreshMs });
        metrics.Add(new RunMetric { MetricKey = "directLookupMs", Value = state.DirectLookupMs });
        metrics.Add(new RunMetric { MetricKey = "directLookupIndexSearchMs", Value = state.DirectLookupIndexSearchMs });
        metrics.Add(new RunMetric { MetricKey = "directLookupMaterializationMs", Value = state.DirectLookupMaterializationMs });
    }

    private static void AddLookupMetrics(List<RunMetric> metrics, SqliteLookupRunState state)
    {
        var count = state.Spec.Workload.LookupCount ?? 0;
        metrics.Add(new RunMetric { MetricKey = "lookupMs", Value = state.LookupMs });
        metrics.Add(new RunMetric { MetricKey = "lookupIndexSearchMs", Value = state.LookupIndexSearchMs });
        metrics.Add(new RunMetric { MetricKey = "lookupMaterializationMs", Value = state.LookupMaterializationMs });
        metrics.Add(new RunMetric { MetricKey = "lookupProbeCount", Value = count });
        metrics.Add(new RunMetric { MetricKey = "lookupProbeHits", Value = state.LookupProbeHits });
        metrics.Add(new RunMetric { MetricKey = "lookupProbeMisses", Value = state.LookupProbeMisses });
        metrics.Add(new RunMetric { MetricKey = "lookupReturnedRows", Value = state.LookupReturnedRows });
        metrics.Add(new RunMetric { MetricKey = "lookupExpectedRows", Value = state.LookupExpectedRows });
        metrics.Add(new RunMetric { MetricKey = "lookupReturnedOffsets", Value = state.LookupReturnedOffsets });
        metrics.Add(new RunMetric { MetricKey = "lookupExpectedOffsets", Value = state.LookupExpectedOffsets });
        metrics.Add(new RunMetric { MetricKey = "lookupMsPerQuery", Value = count > 0 ? state.LookupMs / count : 0 });
        metrics.Add(new RunMetric { MetricKey = "lookupQueriesPerSecond", Value = state.LookupMs > 0 ? count / (state.LookupMs / 1000.0) : 0 });
    }
}
