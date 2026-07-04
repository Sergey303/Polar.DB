namespace Polar.Universal;

internal readonly struct Int64BuildEntry
{
    public Int64BuildEntry(int hashKey, long key, long offset)
    {
        HashKey = hashKey;
        Key = key;
        Offset = offset;
    }

    public int HashKey { get; }
    public long Key { get; }
    public long Offset { get; }
}
