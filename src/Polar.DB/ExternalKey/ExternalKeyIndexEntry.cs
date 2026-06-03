namespace Polar.DB.ExternalKey;

internal readonly struct ExternalKeyIndexEntry<T>
    where T : IComparable<T>
{
    internal ExternalKeyIndexEntry(IComparable primary, T key, long offset)
    {
        Primary = primary;
        Key = key;
        Offset = offset;
    }

    internal IComparable Primary { get; }
    internal T Key { get; }
    internal long Offset { get; }
}
