namespace Polar.DB.Typed.Runtime;

internal sealed class PrimaryKeyMap<TRecord> : IPrimaryKeyMap<TRecord>
{
    private readonly IPrimaryKeyIndex<TRecord> _index;

    public PrimaryKeyMap(IPrimaryKeyIndex<TRecord> index)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
    }

    public int Count => _index.Count;

    public void Build()
    {
        _index.Build();
    }

    public bool TryGet(IComparable key, out TRecord record)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        return _index.TryGet(key, out record);
    }

    public bool Contains(IComparable key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        return _index.Contains(key);
    }

    public void EnsureMissing(IComparable key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        if (Contains(key))
            throw new InvalidOperationException($"Duplicate primary key '{key}'.");
    }
}
