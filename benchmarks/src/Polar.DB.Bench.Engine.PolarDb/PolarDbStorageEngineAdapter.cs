using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.LookupSeries;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.StringLikeLookup;

namespace Polar.DB.Bench.Engine.PolarDb;

/// <summary>
/// Polar.DB current-source adapter for common lookup experiments.
/// Pinned NuGet runners remain separate; this adapter is for current source only.
/// </summary>
public sealed class PolarDbStorageEngineAdapter : IStorageEngineAdapter
{
    public string EngineKey => "polar-db";

    public IReadOnlyCollection<EngineCapability> Capabilities { get; } = new[]
    {
        EngineCapability.BulkLoad,
        EngineCapability.PointLookup,
        EngineCapability.RangeLookup,
        EngineCapability.ReopenRecovery,
        EngineCapability.PhysicalArtifactInspection
    };

    public IEngineRun CreateRun(ExperimentSpec spec, RunWorkspace workspace) =>
        new PolarDbEngineRun(spec, workspace);

    private sealed class PolarDbEngineRun : IEngineRun
    {
        private readonly ExperimentSpec spec;
        private readonly RunWorkspace workspace;

        public PolarDbEngineRun(ExperimentSpec spec, RunWorkspace workspace)
        {
            this.spec = spec;
            this.workspace = workspace;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<RunResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (StringLikeLookupWorkload.IsStringLike(spec.Workload.WorkloadKey))
                return PolarDbStringLikeLookupExecutor.ExecuteAsync(spec, workspace, cancellationToken);

            if (LookupSeriesWorkload.IsLookupSeries(spec.Workload.WorkloadKey))
                return PolarDbLookupSeriesExecutor.ExecuteAsync(spec, workspace, cancellationToken);

            throw new NotSupportedException(
                $"Experiment/workload '{spec.ExperimentKey}'/'{spec.Workload.WorkloadKey}' is not implemented in Polar.DB adapter.");
        }
    }
}
