using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Polar.DB.Bench.Core.LookupSeries;
using Polar.DB.Bench.Core.StringLikeLookup;

namespace Polar.DB.Bench.Engine.PolarDb.StringLikeLookup;

public sealed class PolarDbLikeLookupEngine : ILikeLookupEngine
{
    private readonly PolarDbLikeLookupEngineOptions _options;
    private readonly Func<PolarDbLikeLookupEngineOptions, IPolarStringLikeStore> _storeFactory;
    private IPolarStringLikeStore? _store;

    public PolarDbLikeLookupEngine(
        PolarDbLikeLookupEngineOptions options,
        Func<PolarDbLikeLookupEngineOptions, IPolarStringLikeStore> storeFactory)
    {
        _options = options;
        _storeFactory = storeFactory;
    }

    public string EngineKey => _options.EngineKey;

    public async ValueTask LoadAsync(
        IReadOnlyList<StringLikeRecord> records,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.WorkDirectory);
        _store = _storeFactory(_options);
        await _store.LoadAsync(records, cancellationToken);
    }

    public ValueTask BuildAsync(CancellationToken cancellationToken)
    {
        var store = RequireStore();
        return store.BuildAsync(cancellationToken);
    }

    public ValueTask ReopenAsync(CancellationToken cancellationToken)
    {
        var store = RequireStore();
        return store.ReopenAsync(cancellationToken);
    }

    public ValueTask<EngineLookupResult> LookupAsync(
        StringLikeQueryCase query,
        CancellationToken cancellationToken)
    {
        var store = RequireStore();
        return query.Kind switch
        {
            StringLikeQueryKind.Exact => store.CountByPrefixAsync(query.Prefix, cancellationToken),
            StringLikeQueryKind.Prefix => store.CountByPrefixAsync(query.Prefix, cancellationToken),
            StringLikeQueryKind.Contains => store.CountByContainsScanAsync(query.Prefix, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(query.Kind))
        };
    }

    public ValueTask<IReadOnlyList<ArtifactInfo>> CollectArtifactsAsync(CancellationToken cancellationToken) =>
        RequireStore().CollectArtifactsAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_store is not null) await _store.DisposeAsync();
    }

    private IPolarStringLikeStore RequireStore() =>
        _store ?? throw new InvalidOperationException("PolarDB store is not loaded.");
}
