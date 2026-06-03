using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Engine.Sqlite;

internal static partial class SqliteLookupSeriesExecutor
{
    private static RunResult BuildRunResult(SqliteLookupRunState state)
    {
        var managedAfter = System.GC.GetTotalMemory(forceFullCollection: false);
        var gcInfo = System.GC.GetGCMemoryInfo();
        var processPeakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64;
        var artifacts = SqliteLookupArtifacts.Collect(state.ArtifactLayout, state.Workspace.WorkingDirectory);
        var metrics = BuildMetrics(state, managedAfter, gcInfo, processPeakWorkingSet, artifacts);
        var diagnostics = BuildDiagnostics(state, artifacts);

        return new RunResult
        {
            RunId = state.RunId,
            TimestampUtc = state.TimestampUtc,
            EngineKey = EngineKeyValue,
            ExperimentKey = state.Spec.ExperimentKey,
            DatasetProfileKey = state.Spec.Dataset.ProfileKey,
            FairnessProfileKey = state.FairnessProfileKey,
            Environment = state.Environment,
            TechnicalSuccess = state.TechnicalSuccess,
            TechnicalFailureReason = state.TechnicalFailureReason,
            SemanticSuccess = state.SemanticSuccess,
            SemanticFailureReason = state.SemanticFailureReason,
            Metrics = metrics,
            Artifacts = artifacts.Descriptors,
            EngineDiagnostics = diagnostics,
            Tags = BuildTags(state),
            Notes = BuildNotes(state)
        };
    }

    private static Dictionary<string, string> BuildTags(SqliteLookupRunState state)
    {
        return new Dictionary<string, string>
        {
            ["research"] = state.Spec.ResearchQuestionId ?? string.Empty,
            ["hypothesis"] = state.Spec.HypothesisId ?? string.Empty,
            ["lookupMode"] = state.Spec.Workload.WorkloadKey,
            ["lookupMeasurement"] = "prepared-rowid-search-plus-batched-rowid-materialization"
        };
    }

    private static List<string> BuildNotes(SqliteLookupRunState state)
    {
        return new List<string>
        {
            "Lookup-series run for SQLite adapter.",
            "Prepared commands are reused across probes.",
            "Materialization uses one batched rowid query per probe, not one SQL query per returned row.",
            "materializedLookupMs is the payload materialization phase; lookupMs is total lookup time."
        };
    }

    private static string S(double value) => value.ToString(CultureInfo.InvariantCulture);
}
