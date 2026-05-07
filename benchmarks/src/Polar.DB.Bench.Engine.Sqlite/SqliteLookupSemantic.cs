using System.Collections.Generic;

namespace Polar.DB.Bench.Engine.Sqlite;

internal static partial class SqliteLookupSeriesExecutor
{
    private static string BuildSemanticFailureReason(
        long expectedCount,
        long actualCount,
        int lookupCount,
        long lookupHits,
        long lookupMisses,
        long lookupExpectedRows,
        long lookupReturnedRows,
        IReadOnlyList<string> mismatchSamples)
    {
        return "SQLite lookup-series semantic check failed: " +
               $"expectedCount={expectedCount}, actualCount={actualCount}, lookupCount={lookupCount}, " +
               $"lookupHits={lookupHits}, lookupMisses={lookupMisses}, " +
               $"lookupExpectedRows={lookupExpectedRows}, lookupReturnedRows={lookupReturnedRows}. " +
               (mismatchSamples.Count == 0 ? string.Empty : "Samples: " + string.Join(" | ", mismatchSamples));
    }
}
