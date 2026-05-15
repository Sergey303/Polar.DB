using System;
using System.Collections.Generic;
using Polar.DB.Bench.Core.LookupSeries;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;

namespace Polar.DB.Bench.Engine.Sqlite;

internal sealed class SqliteLookupRunState
{
    public ExperimentSpec Spec { get; init; } = null!;
    public RunWorkspace Workspace { get; init; } = null!;
    public EnvironmentManifest Environment { get; init; } = null!;
    public string RunId { get; init; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; init; }
    public string FairnessProfileKey { get; init; } = string.Empty;
    public SqliteLookupArtifactLayout ArtifactLayout { get; init; } = null!;

    public bool TechnicalSuccess { get; set; } = true;
    public string? TechnicalFailureReason { get; set; }
    public bool? SemanticSuccess { get; set; }
    public string? SemanticFailureReason { get; set; }

    public double ElapsedMs { get; set; }
    public double LoadMs { get; set; }
    public double BuildMs { get; set; }
    public double ReopenRefreshMs { get; set; }
    public long RowCountAfterReopen { get; set; }

    public double DirectLookupMs { get; set; }
    public double DirectLookupIndexSearchMs { get; set; }
    public double DirectLookupMaterializationMs { get; set; }
    public int DirectExpectedRows { get; set; }
    public int DirectReturnedRows { get; set; }
    public bool DirectHit { get; set; }

    public double LookupMs { get; set; }
    public double LookupIndexSearchMs { get; set; }
    public double LookupMaterializationMs { get; set; }
    public long LookupProbeHits { get; set; }
    public long LookupProbeMisses { get; set; }
    public long LookupReturnedRows { get; set; }
    public long LookupExpectedRows { get; set; }
    public long LookupReturnedOffsets { get; set; }
    public long LookupExpectedOffsets { get; set; }

    public long ManagedBefore { get; init; }
    public List<string> MismatchSamples { get; } = new();

    public static SqliteLookupRunState Create(
        ExperimentSpec spec,
        RunWorkspace workspace,
        LookupSeriesOptions options)
    {
        var environment = EnvironmentCollector.Collect(workspace.EnvironmentClass, workspace.RootDirectory);
        var runId = RunIdFactory.Create(spec.ExperimentKey, spec.Dataset.ProfileKey, "sqlite", environment.EnvironmentClass);
        var layout = SqliteLookupArtifacts.CreateLayout(workspace, runId);

        return new SqliteLookupRunState
        {
            Spec = spec,
            Workspace = workspace,
            Environment = environment,
            RunId = runId,
            TimestampUtc = DateTimeOffset.UtcNow,
            FairnessProfileKey = spec.FairnessProfile?.FairnessProfileKey ?? "unspecified",
            ArtifactLayout = layout,
            ManagedBefore = GC.GetTotalMemory(forceFullCollection: false)
        };
    }
}
