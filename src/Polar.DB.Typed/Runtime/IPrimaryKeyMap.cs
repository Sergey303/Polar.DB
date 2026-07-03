namespace Polar.DB.Typed.Runtime;

internal interface IPrimaryKeyMap<TRecord>
{
    int Count { get; }

    void Build();

    bool TryGet(IComparable key, out TRecord record);

    bool Contains(IComparable key);

    void EnsureMissing(IComparable key);
}
