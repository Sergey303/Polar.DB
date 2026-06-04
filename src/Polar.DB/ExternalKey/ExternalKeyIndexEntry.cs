namespace Polar.DB.ExternalKey;

internal readonly struct ExternalKeyIndexEntry<T>
    where T : IComparable<T>
{
    internal ExternalKeyIndexEntry(IComparable primary, T key, long offset, long revision)
    {
        Primary = primary;
        Key = key;
        Offset = offset;
        Revision = revision;
    }

    internal IComparable Primary { get; }
    internal T Key { get; }
    internal long Offset { get; }
    internal long Revision { get; }
}
