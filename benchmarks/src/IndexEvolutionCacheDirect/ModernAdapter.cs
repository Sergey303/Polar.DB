#if !LEGACY2021
using Polar.DB;
using Polar.DB.ExternalKey;
using Polar.Universal;

namespace IndexEvolutionDirect;

internal static class AdapterFactory
{
    internal static IIndexAdapter Create(PType recordType, SequentialStreams factory) =>
        new ModernAdapter(recordType, factory);
}

internal sealed class ModernAdapter : IIndexAdapter
{
    private readonly SequentialStreams _streams;
    private readonly USequence _sequence;
    private ExternalKeyIndex<int>? _index;

    internal ModernAdapter(PType recordType, SequentialStreams streams)
    {
        _streams = streams;
        _sequence = new USequence(recordType, stateFileName: null, streams.OpenNext,
            isEmpty: _ => false);
        _sequence.SetPrimaryKey<int>(element => (int)((object[])element)[0]);
    }

    public void Load(IEnumerable<object> records) => _sequence.Load(records);

    public void PreparePrerequisites()
    {
        // Required for USequence.Scan and current-version visibility.
        // This is timed separately and NEVER silently included in external Build.
        _sequence.Build();
        _index = new ExternalKeyIndex<int>(_streams.OpenNext, _sequence,
            element => new[] { (int)((object[])element)[1] });
        _sequence.uindexes = new IUIndex[] { _index };
    }

    public void BuildAndFlush()
    {
        if (_index == null) throw new InvalidOperationException("Index not prepared");
        _index.Build();
        _index.Flush();
    }

    public IEnumerable<object> Lookup(int key) =>
        (_index ?? throw new InvalidOperationException("Index not built")).GetManyByValue(key);

    public void Dispose()
    {
        _sequence.Dispose();
        _streams.Dispose();
    }
}
#endif
