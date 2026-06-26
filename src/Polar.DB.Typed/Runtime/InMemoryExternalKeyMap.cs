using Polar.DB.ExternalKey;
using Polar.DB.Typed.Schema;

namespace Polar.DB.Typed.Runtime;

internal sealed class InMemoryExternalKeyMap
{
    private readonly Dictionary<string, InMemoryExternalKeyIndex> _indexes = new(StringComparer.Ordinal);

    public int Count => _indexes.Count;

    public IReadOnlyList<string> BuiltFieldNames => _indexes.Keys
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    public bool Has(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name is required.", nameof(fieldName));

        return _indexes.ContainsKey(fieldName);
    }

    public void Rebuild(
        FieldScheme field,
        IEnumerable<object> records)
    {
        if (field == null) throw new ArgumentNullException(nameof(field));
        if (records == null) throw new ArgumentNullException(nameof(records));

        var index = new InMemoryExternalKeyIndex(field.Name, field.Index);
        foreach (object? record in records)
        {
            if (record == null) continue;
            index.Add(record);
        }

        _indexes[field.Name] = index;
    }

    public void RebuildExisting(IEnumerable<object> records)
    {
        if (records == null) throw new ArgumentNullException(nameof(records));

        InMemoryExternalKeyIndex[] existing = _indexes.Values
            .Select(index => new InMemoryExternalKeyIndex(index.Name, index.RecordIndex))
            .ToArray();

        _indexes.Clear();
        foreach (InMemoryExternalKeyIndex index in existing)
        {
            foreach (object? record in records)
            {
                if (record == null) continue;
                index.Add(record);
            }

            _indexes[index.Name] = index;
        }
    }

    public void ValidateAddToBuiltIndexes(object record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));

        foreach (InMemoryExternalKeyIndex index in _indexes.Values)
            index.ReadKey(record);
    }

    public void AddToBuiltIndexes(object record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));

        foreach (InMemoryExternalKeyIndex index in _indexes.Values)
            index.Add(record);
    }

    public IReadOnlyList<object> Find(string fieldName, object? value)
    {
        if (!_indexes.TryGetValue(fieldName, out InMemoryExternalKeyIndex? index))
            throw new InvalidOperationException($"External key index for field '{fieldName}' was not built.");

        return index.Find(value);
    }
}
