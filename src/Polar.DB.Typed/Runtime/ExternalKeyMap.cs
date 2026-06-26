using Polar.DB.Typed.Schema;

namespace Polar.DB.Typed.Runtime;

internal sealed class ExternalKeyMap<TRecord>
{
    private readonly Dictionary<string, ExternalKeyIndexTyped> _indexes = new(StringComparer.Ordinal);

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

        var index = new ExternalKeyIndexTyped(field);
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

        ExternalKeyIndexTyped[] existing = _indexes.Values
            .Select(index => new ExternalKeyIndexTyped(index.Field))
            .ToArray();

        _indexes.Clear();
        foreach (ExternalKeyIndexTyped index in existing)
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

        foreach (ExternalKeyIndexTyped index in _indexes.Values)
            index.ReadKey(record);
    }

    public void AddToBuiltIndexes(object record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));

        foreach (ExternalKeyIndexTyped index in _indexes.Values)
            index.Add(record);
    }

    public IReadOnlyList<object> Find<T>(string fieldName, T? value)
    {
        if (!_indexes.TryGetValue(fieldName, out ExternalKeyIndexTyped? index))
            throw new InvalidOperationException($"External key index for field '{fieldName}' was not built.");

        return index.Find(value);
    }
}
