using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Engine.Sqlite;

public sealed class SqliteStorageEngineAdapter : IStorageEngineAdapter
{
    public string EngineKey => "sqlite";

    public IReadOnlyCollection<EngineCapability> Capabilities { get; } = new[]
    {
        EngineCapability.BulkLoad,
        EngineCapability.PointLookup,
        EngineCapability.RangeLookup,
        EngineCapability.ReopenRecovery,
        EngineCapability.PhysicalArtifactInspection,
        EngineCapability.AppendCycles
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
            throw new NotImplementedException(
                "TODO: implement the real SQLite adapter. Map common workloads to SQLite schema creation, bulk insert, indexing, reopen, query paths, and capture db/wal/shm artifact topology.");
        }
    }
}
