using System.Collections.Generic;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Engine.Sqlite;

internal static partial class SqliteLookupSeriesExecutor
{
    private static void AddCompatibilityMetrics(List<RunMetric> metrics, SqliteLookupRunState state)
    {
        var count = state.Spec.Workload.LookupCount ?? 0;
        metrics.Add(new RunMetric { MetricKey = "directPointLookupMs", Value = state.DirectLookupMs });
        metrics.Add(new RunMetric { MetricKey = "directPointLookupHit", Value = state.DirectHit ? 1 : 0 });
        metrics.Add(new RunMetric { MetricKey = "directLookupExpectedRows", Value = state.DirectExpectedRows });
        metrics.Add(new RunMetric { MetricKey = "directLookupReturnedRows", Value = state.DirectReturnedRows });
        metrics.Add(new RunMetric { MetricKey = "lookupSeriesMs", Value = state.LookupMs });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupMs", Value = state.LookupMs });
        metrics.Add(new RunMetric { MetricKey = "lookupCount", Value = count });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupCount", Value = count });
        metrics.Add(new RunMetric { MetricKey = "lookupHitCount", Value = state.LookupProbeHits });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupHits", Value = state.LookupProbeHits });
        metrics.Add(new RunMetric { MetricKey = "randomPointLookupMisses", Value = state.LookupProbeMisses });
        metrics.Add(new RunMetric { MetricKey = "lookupReturnedRowCount", Value = state.LookupReturnedRows });

        metrics.Add(new RunMetric { MetricKey = "indexOnlyLookupMs", Value = state.LookupIndexSearchMs });
        metrics.Add(new RunMetric { MetricKey = "indexOnlyProbeCount", Value = count });
        metrics.Add(new RunMetric { MetricKey = "indexOnlyProbeHits", Value = state.LookupProbeHits });
        metrics.Add(new RunMetric { MetricKey = "indexOnlyProbeMisses", Value = state.LookupProbeMisses });
        metrics.Add(new RunMetric { MetricKey = "indexOnlyReturnedOffsets", Value = state.LookupReturnedOffsets });
        metrics.Add(new RunMetric { MetricKey = "indexOnlyExpectedOffsets", Value = state.LookupExpectedOffsets });

        metrics.Add(new RunMetric { MetricKey = "materializedLookupMs", Value = state.LookupMaterializationMs });
        metrics.Add(new RunMetric { MetricKey = "materializedTotalLookupMs", Value = state.LookupMs });
        metrics.Add(new RunMetric { MetricKey = "materializedProbeCount", Value = count });
        metrics.Add(new RunMetric { MetricKey = "materializedProbeHits", Value = state.LookupProbeHits });
        metrics.Add(new RunMetric { MetricKey = "materializedProbeMisses", Value = state.LookupProbeMisses });
        metrics.Add(new RunMetric { MetricKey = "materializedReturnedRows", Value = state.LookupReturnedRows });
        metrics.Add(new RunMetric { MetricKey = "materializedExpectedRows", Value = state.LookupExpectedRows });
    }
}
