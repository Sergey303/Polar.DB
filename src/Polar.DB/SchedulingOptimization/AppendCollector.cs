namespace Polar.DB.SchedulingOptimization;

/// <summary>
/// Пока простой список для хранения небольшого числа изменений.
/// Нужен во время построения новой эпохи.
/// В теории может быть переделан под большое число изменений, складируя в простой новый файл через USequence. 
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
