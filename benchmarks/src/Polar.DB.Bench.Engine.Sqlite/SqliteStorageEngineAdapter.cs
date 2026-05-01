using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.LookupSeries;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Engine.Sqlite;

/// <summary>
/// SQLite adapter for lookup-series experiments.
///
/// This file intentionally routes lookup-series workloads before any legacy stage4 validation.
/// </summary>
public sealed class SqliteStorageEngineAdapter : IStorageEngineAdapter
{
    public string EngineKey => "sqlite";

    public IReadOnlyCollection<EngineCapability> Capabilities { get; } = new[]
    {
        EngineCapability.BulkLoad,
        EngineCapability.PointLookup,
        EngineCapability.ReopenRecovery,
        EngineCapability.PhysicalArtifactInspection
    };

    public IEngineRun CreateRun(ExperimentSpec spec, RunWorkspace workspace)
    {
        return new SqliteEngineRun(spec, workspace);
    }

    private sealed class SqliteEngineRun : IEngineRun
    {
        private readonly ExperimentSpec _spec;
        private readonly RunWorkspace _workspace;

        public SqliteEngineRun(ExperimentSpec spec, RunWorkspace workspace)
        {
            _spec = spec;
            _workspace = workspace;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<RunResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (LookupSeriesWorkload.IsLookupSeries(_spec.Workload.WorkloadKey))
            {
                return SqliteLookupSeriesExecutor.ExecuteAsync(_spec, _workspace, cancellationToken);
            }

            throw new NotSupportedException(
                $"Experiment/workload '{_spec.ExperimentKey}'/'{_spec.Workload.WorkloadKey}' is not implemented in this lookup-series SQLite adapter file. " +
                "Use the previous full adapter file for older non-lookup benchmark workloads.");
        }
    }
}
