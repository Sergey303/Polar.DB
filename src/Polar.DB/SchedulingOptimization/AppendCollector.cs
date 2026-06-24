namespace Polar.DB.SchedulingOptimization;

/// <summary>
/// Простой in-memory список append-изменений, накопленных во время сборки новой эпохи.
/// Синхронизация находится выше, в ActiveSequenceOwner.
/// </summary>
public sealed class AppendCollector
{
    private readonly List<object> _items = new();

    public int Count => _items.Count;

    public void Add(object element)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));
        _items.Add(element);
    }

    public object[] TakeSnapshot()
    {
        return _items.ToArray();
    }
}
