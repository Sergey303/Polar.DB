using Polar.Universal;

namespace Polar.DB.Typed.Runtime;

internal interface IExternalKeyIndexTyped
{
    string Name { get; }

    Type KeyType { get; }

    IUIndex StorageIndex { get; }

    void Build();

    void ValidateStorageRecord(object storageRecord);
}

internal interface IExternalKeyIndexTyped<TRecord, TExternalKey> : IExternalKeyIndexTyped
    where TExternalKey : IComparable<TExternalKey>
{
    IReadOnlyList<TRecord> Find(TExternalKey value);
}
