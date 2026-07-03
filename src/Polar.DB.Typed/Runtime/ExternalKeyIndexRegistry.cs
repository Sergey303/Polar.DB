using Polar.Universal;

namespace Polar.DB.Typed.Runtime;

internal sealed class ExternalKeyIndexRegistry<TRecord> : IExternalKeyIndexRegistry<TRecord>
{
    private readonly Dictionary<string, IExternalKeyIndexTyped> _indexes = new(StringComparer.Ordinal);

    public int Count => _indexes.Count;

    public IReadOnlyList<string> BuiltFieldNames => _indexes.Keys
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<IUIndex> StorageIndexes => _indexes.Values
        .OrderBy(index => index.Name, StringComparer.Ordinal)
        .Select(index => index.StorageIndex)
        .ToArray();

    public bool Has(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name is required.", nameof(fieldName));

        return _indexes.ContainsKey(fieldName);
    }

    public void Add(IExternalKeyIndexTyped index)
    {
        if (index == null) throw new ArgumentNullException(nameof(index));
        _indexes[index.Name] = index;
    }

    public bool TryGet<TExternalKey>(
        string fieldName,
        out IExternalKeyIndexTyped<TRecord, TExternalKey>? index)
        where TExternalKey : IComparable<TExternalKey>
    {
        if (!_indexes.TryGetValue(fieldName, out IExternalKeyIndexTyped? existing))
        {
            index = null;
            return false;
        }

        if (existing is IExternalKeyIndexTyped<TRecord, TExternalKey> typed)
        {
            index = typed;
            return true;
        }

        throw new ArgumentException(
            $"External key field '{fieldName}' expects key type '{existing.KeyType.FullName}', " +
            $"but got '{typeof(TExternalKey).FullName}'.",
            nameof(fieldName));
    }

    public void ValidateAddToBuiltIndexes(object storageRecord)
    {
        if (storageRecord == null) throw new ArgumentNullException(nameof(storageRecord));

        foreach (IExternalKeyIndexTyped index in _indexes.Values)
            index.ValidateStorageRecord(storageRecord);
    }

    public void RebuildExisting()
    {
        foreach (IExternalKeyIndexTyped index in _indexes.Values)
            index.Build();
    }
}
