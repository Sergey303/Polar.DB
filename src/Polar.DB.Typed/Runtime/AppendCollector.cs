namespace Polar.DB.Typed.Runtime;

internal sealed class AppendCollector
{
    private readonly List<object> _records = new();

    public int Count => _records.Count;

    public void Append(object record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        _records.Add(record);
    }

    public IReadOnlyList<object> Snapshot()
    {
        return _records.ToArray();
    }

    public void Clear()
    {
        _records.Clear();
    }
}
