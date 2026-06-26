namespace Polar.DB.SchedulingOptimization;

/// <summary>
/// Пока простой список для хранения небольшого числа изменений.
/// Нужен во время построения новой эпохи.
/// В теории может быть переделан под большое число изменений, складируя в простой новый файл через USequence. 
/// </summary>
public sealed class AppendCollector
{
    private readonly List<object> _records = new();

    public int Count => _records.Count;

    public void Append(object record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        _records.Add(record);
    }

    public IReadOnlyList<object> TakeSnapshot()
    {
        return _records.ToArray();
    }

    public void Clear()
    {
        _records.Clear();
    }
}
