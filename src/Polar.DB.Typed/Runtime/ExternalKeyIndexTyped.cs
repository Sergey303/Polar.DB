using Polar.DB.ExternalKey;
using Polar.Universal;

namespace Polar.DB.Typed.Runtime;

internal sealed class ExternalKeyIndexTyped<TRecord, TExternalKey> : IExternalKeyIndexTyped<TRecord, TExternalKey>
    where TExternalKey : IComparable<TExternalKey>
{
    private readonly IExternalKeyIndex<TExternalKey> _storageIndex;
    private readonly Func<object, TRecord> _fromStorageRecord;
    private readonly Func<object, TExternalKey> _readKey;

    public ExternalKeyIndexTyped(
        string name,
        IExternalKeyIndex<TExternalKey> storageIndex,
        Func<object, TRecord> fromStorageRecord,
        Func<object, TExternalKey> readKey)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("External key name is required.", nameof(name));

        Name = name;
        _storageIndex = storageIndex ?? throw new ArgumentNullException(nameof(storageIndex));
        _fromStorageRecord = fromStorageRecord ?? throw new ArgumentNullException(nameof(fromStorageRecord));
        _readKey = readKey ?? throw new ArgumentNullException(nameof(readKey));
    }

    public string Name { get; }

    public Type KeyType => typeof(TExternalKey);

    public IUIndex StorageIndex => _storageIndex;

    public void Build()
    {
        _storageIndex.Build();
    }

    public void ValidateStorageRecord(object storageRecord)
    {
        _ = _readKey(storageRecord);
    }

    public IReadOnlyList<TRecord> Find(TExternalKey value)
    {
        return _storageIndex
            .GetManyByValue(value)
            .Select(_fromStorageRecord)
            .ToArray();
    }
}
