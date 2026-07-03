using Polar.Universal;

namespace Polar.DB.Typed.Runtime;

internal interface IExternalKeyIndexRegistry<TRecord>
{
    int Count { get; }

    IReadOnlyList<string> BuiltFieldNames { get; }

    IReadOnlyList<IUIndex> StorageIndexes { get; }

    bool Has(string fieldName);

    void Add(IExternalKeyIndexTyped index);

    bool TryGet<TExternalKey>(
        string fieldName,
        out IExternalKeyIndexTyped<TRecord, TExternalKey>? index)
        where TExternalKey : IComparable<TExternalKey>;

    void ValidateAddToBuiltIndexes(object storageRecord);

    void RebuildExisting();
}
