using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Core.Abstractions;

public interface IEngineRun : IAsyncDisposable
{
    Task<RunResult> ExecuteAsync(CancellationToken cancellationToken = default);
}
