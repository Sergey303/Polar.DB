namespace Polar.DB.Typed.Runtime;

internal sealed class PrimaryKeyMap
{
    private readonly Dictionary<IComparable, object> _records = new();

    public int Count => _records.Count;

    public void Rebuild(
        IEnumerable<object> records,
        Func<object, IComparable> getKey)
    {
        if (records == null) throw new ArgumentNullException(nameof(records));
        if (getKey == null) throw new ArgumentNullException(nameof(getKey));

        _records.Clear();
        foreach (object? record in records)
        {
            if (record == null) continue;
            Add(getKey(record), record);
        }
    }

    public void Add(IComparable key, object record)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (record == null) throw new ArgumentNullException(nameof(record));

        if (_records.ContainsKey(key))
            throw new InvalidOperationException($"Duplicate primary key '{key}'.");

        _records.Add(key, record);
    }

    public bool TryGet(IComparable key, out object record)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        return _records.TryGetValue(key, out record!);
    }
}
