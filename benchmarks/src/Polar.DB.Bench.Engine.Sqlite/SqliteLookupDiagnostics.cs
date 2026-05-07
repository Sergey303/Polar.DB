using System.Collections.Generic;
using System.Globalization;

namespace Polar.DB.Bench.Engine.Sqlite;

internal static partial class SqliteLookupSeriesExecutor
{
    private static Dictionary<string, string> BuildDiagnostics(
        SqliteLookupRunState state,
        SqliteLookupArtifactInventory artifacts)
    {
        var diagnostics = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["lookupMs"] = S(state.LookupMs),
            ["lookupIndexSearchMs"] = S(state.LookupIndexSearchMs),
            ["lookupMaterializationMs"] = S(state.LookupMaterializationMs),
            ["lookupProbeHits"] = state.LookupProbeHits.ToString(CultureInfo.InvariantCulture),
            ["lookupProbeMisses"] = state.LookupProbeMisses.ToString(CultureInfo.InvariantCulture),
            ["lookupReturnedRows"] = state.LookupReturnedRows.ToString(CultureInfo.InvariantCulture),
            ["lookupExpectedRows"] = state.LookupExpectedRows.ToString(CultureInfo.InvariantCulture),
            ["rowCountAfterReopen"] = state.RowCountAfterReopen.ToString(CultureInfo.InvariantCulture),
            ["dbBytes"] = artifacts.PrimaryDatabaseBytes.ToString(CultureInfo.InvariantCulture),
            ["walBytes"] = artifacts.WalBytes.ToString(CultureInfo.InvariantCulture),
            ["shmBytes"] = artifacts.SharedMemoryBytes.ToString(CultureInfo.InvariantCulture),
            ["totalArtifactBytes"] = artifacts.TotalBytes.ToString(CultureInfo.InvariantCulture),
            ["semanticSuccess"] = state.SemanticSuccess?.ToString() ?? "not-evaluated",
            ["sqliteLookupImplementation"] = "prepared-rowid-search-plus-batched-rowid-materialization"
        };

        if (state.MismatchSamples.Count > 0)
        {
            diagnostics["lookupMismatchSamples"] = string.Join(" | ", state.MismatchSamples);
        }

        if (!string.IsNullOrWhiteSpace(state.SemanticFailureReason))
        {
            diagnostics["semanticFailureReason"] = state.SemanticFailureReason;
        }

        if (!string.IsNullOrWhiteSpace(state.TechnicalFailureReason))
        {
            diagnostics["technicalFailureReason"] = state.TechnicalFailureReason;
        }

        return diagnostics;
    }
}
