namespace Polar.Universal;

internal readonly struct PrimaryBuildEntry<TKey>
    where TKey : struct
{
    public PrimaryBuildEntry(int hashKey, TKey key, long offset, bool isEmpty)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (isEmpty)
            throw new ArgumentException(
                "Loaded typed primary build entries must already exclude empty elements.", nameof(isEmpty));

        HashKey = hashKey;
        Key = key;
        Offset = offset;
    }

    public int HashKey { get; }
    public TKey Key { get; }
    public long Offset { get; }
}

internal sealed class PrimaryBuildEntryComparer<TKey> : IComparer<PrimaryBuildEntry<TKey>>
    where TKey : struct, IComparable<TKey>
{
    public static readonly PrimaryBuildEntryComparer<TKey> Instance = new();

    public int Compare(PrimaryBuildEntry<TKey> left, PrimaryBuildEntry<TKey> right)
    {
        var hashComparison = left.HashKey.CompareTo(right.HashKey);
        if (hashComparison != 0) return hashComparison;

        var keyComparison = left.Key.CompareTo(right.Key);
        if (keyComparison != 0) return keyComparison;

        return left.Offset.CompareTo(right.Offset);
    }
}

internal interface ILoadedTypedPrimaryBuild
{
    void Build(UKeyIndex index);
}

internal sealed class LoadedTypedPrimaryBuild<TKey> : ILoadedTypedPrimaryBuild
    where TKey : struct, IComparable<TKey>, IEquatable<TKey>
{
    private PrimaryBuildEntry<TKey>[] entries;

    public LoadedTypedPrimaryBuild(PrimaryBuildEntry<TKey>[] entries)
    {
        this.entries = entries ?? throw new ArgumentNullException(nameof(entries));
    }

    public void Build(UKeyIndex index)
    {
        index.BuildFromLoadedTypedEntries(entries);
        entries = Array.Empty<PrimaryBuildEntry<TKey>>();
    }
}
