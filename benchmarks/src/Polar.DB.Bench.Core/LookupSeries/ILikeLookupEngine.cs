using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Polar.DB.Bench.Core.StringLikeLookup;

namespace Polar.DB.Bench.Core.LookupSeries;

public interface ILikeLookupEngine : IAsyncDisposable
{
    string EngineKey { get; }

    ValueTask LoadAsync(
        IReadOnlyList<StringLikeRecord> records,
        CancellationToken cancellationToken);

    ValueTask BuildAsync(CancellationToken cancellationToken);

    ValueTask ReopenAsync(CancellationToken cancellationToken);

    ValueTask<EngineLookupResult> LookupAsync(
        StringLikeQueryCase query,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ArtifactInfo>> CollectArtifactsAsync(
        CancellationToken cancellationToken);
}
