using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.LookupSeries;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.StringLikeLookup;

namespace Polar.DB.Bench.Engine.Sqlite;

/// <summary>
/// SQLite adapter for common lookup experiments.
/// </summary>
public sealed class SqliteStorageEngineAdapter : IStorageEngineAdapter
{
    public string EngineKey => "sqlite";

    public IReadOnlyCollection<EngineCapability> Capabilities { get; } = new[]
    {
        EngineCapability.BulkLoad,
        EngineCapability.PointLookup,
        EngineCapability.RangeLookup,
        EngineCapability.ReopenRecovery,
        EngineCapability.PhysicalArtifactInspection
    };

    public IEngineRun CreateRun(ExperimentSpec spec, RunWorkspace workspace) =>
        new SqliteEngineRun(spec, workspace);

    private sealed class SqliteEngineRun : IEngineRun
    {
        private readonly ExperimentSpec spec;
        private readonly RunWorkspace workspace;

        public SqliteEngineRun(ExperimentSpec spec, RunWorkspace workspace)
        {
            this.spec = spec;
            this.workspace = workspace;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<RunResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (StringLikeLookupWorkload.IsStringLike(spec.Workload.WorkloadKey))
                return SqliteStringLikeLookupExecutor.ExecuteAsync(spec, workspace, cancellationToken);

            if (LookupSeriesWorkload.IsLookupSeries(spec.Workload.WorkloadKey))
                return SqliteLookupSeriesExecutor.ExecuteAsync(spec, workspace, cancellationToken);

            throw new NotSupportedException(
                $"Experiment/workload '{spec.ExperimentKey}'/'{spec.Workload.WorkloadKey}' is not implemented in SQLite adapter.");
        }
    }
}
