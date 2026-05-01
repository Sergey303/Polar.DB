using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.LookupSeries;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Engine.PolarDb;

/// <summary>
/// Polar.DB current-source adapter for lookup-series experiments.
///
/// This file intentionally routes lookup-series workloads before any legacy stage4 validation.
/// It is meant for the current source adapter only; pinned NuGet runners remain separate.
/// </summary>
public sealed class PolarDbStorageEngineAdapter : IStorageEngineAdapter
{
    public string EngineKey => "polar-db";

    public IReadOnlyCollection<EngineCapability> Capabilities { get; } = new[]
    {
        EngineCapability.BulkLoad,
        EngineCapability.PointLookup,
        EngineCapability.ReopenRecovery,
        EngineCapability.PhysicalArtifactInspection
    };

    public IEngineRun CreateRun(ExperimentSpec spec, RunWorkspace workspace)
    {
        return new PolarDbEngineRun(spec, workspace);
    }

    private sealed class PolarDbEngineRun : IEngineRun
    {
        private readonly ExperimentSpec _spec;
        private readonly RunWorkspace _workspace;

        public PolarDbEngineRun(ExperimentSpec spec, RunWorkspace workspace)
        {
            _spec = spec;
            _workspace = workspace;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<RunResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (LookupSeriesWorkload.IsLookupSeries(_spec.Workload.WorkloadKey))
            {
                return PolarDbLookupSeriesExecutor.ExecuteAsync(_spec, _workspace, cancellationToken);
            }

            throw new NotSupportedException(
                $"Experiment/workload '{_spec.ExperimentKey}'/'{_spec.Workload.WorkloadKey}' is not implemented in this lookup-series Polar.DB adapter file. " +
                "Use the previous full adapter file for older non-lookup benchmark workloads.");
        }
    }
}
