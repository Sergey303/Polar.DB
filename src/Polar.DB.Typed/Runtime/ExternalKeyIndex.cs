using Polar.DB.Typed.Schema;

namespace Polar.DB.Typed.Runtime;

internal sealed class ExternalKeyIndex
{
    private readonly Dictionary<object, List<object>> _records = new();

    public ExternalKeyIndex(FieldScheme field)
    {
        Field = field ?? throw new ArgumentNullException(nameof(field));
    }

    public FieldScheme Field { get; }
    public string Name => Field.Name;

    public object ReadKey(object record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));

        object? rawValue = ((object[])record)[Field.Index];
        return Normalize(rawValue);
    }

    public void Add(object record)
    {
        object key = ReadKey(record);
        if (!_records.TryGetValue(key, out List<object>? bucket))
        {
            bucket = new List<object>();
            _records.Add(key, bucket);
        }

        bucket.Add(record);
    }

    public IReadOnlyList<object> Find(object? value)
    {
        object key = Normalize(value);
        return _records.TryGetValue(key, out List<object>? bucket)
            ? bucket.ToArray()
            : Array.Empty<object>();
    }

    internal static object Normalize(object? value) => value ?? new();
}
