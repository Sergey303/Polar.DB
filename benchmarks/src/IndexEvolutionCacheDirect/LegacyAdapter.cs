#if LEGACY2021
using Polar.DB;

namespace IndexEvolutionDirect;

internal static class AdapterFactory
{
    internal static IIndexAdapter Create(PType recordType, SequentialStreams factory) =>
        new LegacyAdapter(recordType, factory);
}

internal sealed class LegacyAdapter : IIndexAdapter
{
    private readonly BearingPure _bearing;
    private readonly SequentialStreams _streams;
    private IndexKey32Comp? _index;

    internal LegacyAdapter(PType recordType, SequentialStreams streams)
    {
        _streams = streams;
        _bearing = new BearingPure(recordType, streams.OpenNext);
        // BearingPure.Load invokes Build(); keep that phase free of the external index.
        _bearing.Indexes = Array.Empty<IIndex>();
    }

    public void Load(IEnumerable<object> records) => _bearing.Load(records);

    public void PreparePrerequisites()
    {
        // Historical scale is part of this index, not an external adapter optimization.
        _index = new IndexKey32Comp(_streams.OpenNext, _bearing, _ => true,
            element => (int)((object[])element)[1], comp: null);
        _bearing.Indexes = new IIndex[] { _index };
    }

    public void BuildAndFlush()
    {
        if (_index == null) throw new InvalidOperationException("Index not prepared");
        _index.Build();
        _index.Flush();
    }

    public void RefreshIndex() => (_index ?? throw new InvalidOperationException("No index")).Refresh();

    public IEnumerable<object> Lookup(int key) =>
        (_index ?? throw new InvalidOperationException("Index not built")).GetAllByKey(key);

    public void Dispose()
    {
        _index?.Close();
        _streams.Dispose();
    }
}
#endif
