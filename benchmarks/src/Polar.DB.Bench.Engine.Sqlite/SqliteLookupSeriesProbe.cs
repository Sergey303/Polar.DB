using System.Diagnostics;
using Polar.DB.Bench.Core.LookupSeries;

namespace Polar.DB.Bench.Engine.Sqlite;

internal static partial class SqliteLookupSeriesExecutor
{
    private static LookupProbeResult ExecuteLookupProbe(SqliteLookupCommands lookup, LookupProbe probe)
    {
        var totalWatch = Stopwatch.StartNew();

        var indexWatch = Stopwatch.StartNew();
        var rowIds = lookup.SelectRowIdsByKey(probe.Key);
        indexWatch.Stop();

        var materializationWatch = Stopwatch.StartNew();
        var materialized = lookup.MaterializeAndValidateRows(probe.Key, rowIds);
        materializationWatch.Stop();

        totalWatch.Stop();

        if (rowIds.Count != probe.ExpectedCount)
        {
            return LookupProbeResult.Failed(
                totalWatch, indexWatch, materializationWatch, rowIds.Count,
                materialized.ReturnedRows,
                $"rowid count mismatch for key={probe.Key}: returned={rowIds.Count}, expected={probe.ExpectedCount}");
        }

        if (materialized.ReturnedRows != probe.ExpectedCount)
        {
            return LookupProbeResult.Failed(
                totalWatch, indexWatch, materializationWatch, rowIds.Count,
                materialized.ReturnedRows,
                $"materialized row count mismatch for key={probe.Key}: returned={materialized.ReturnedRows}, expected={probe.ExpectedCount}");
        }

        if (materialized.WrongRows != 0)
        {
            return LookupProbeResult.Failed(
                totalWatch, indexWatch, materializationWatch, rowIds.Count,
                materialized.ReturnedRows,
                $"materialized rows with wrong key for key={probe.Key}: wrongRows={materialized.WrongRows}");
        }

        return LookupProbeResult.Passed(totalWatch, indexWatch, materializationWatch, rowIds.Count, materialized.ReturnedRows);
    }
}
