using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Polar.DB.Bench.Core.StringLikeLookup;

namespace Polar.DB.Bench.Engine.PolarDb.StringLikeLookup;

public interface IPolarStringLikeStore : IAsyncDisposable
{
    ValueTask LoadAsync(
        IReadOnlyList<StringLikeRecord> records,
        CancellationToken cancellationToken);

    ValueTask BuildAsync(CancellationToken cancellationToken);

    ValueTask ReopenAsync(CancellationToken cancellationToken);

    ValueTask<EngineLookupResult> CountByPrefixAsync(
        string prefix,
        CancellationToken cancellationToken);

    ValueTask<EngineLookupResult> CountByContainsScanAsync(
        string text,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ArtifactInfo>> CollectArtifactsAsync(
        CancellationToken cancellationToken);
}
