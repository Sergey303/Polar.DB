namespace Polar.Universal;

internal readonly struct BuildEntry
{
    public BuildEntry(int hashKey, IComparable key, long offset, bool isEmpty)
    {
        HashKey = hashKey;
        Key = key;
        Offset = offset;
        IsEmpty = isEmpty;
    }

    public int HashKey { get; }
    public IComparable Key { get; }
    public long Offset { get; }
    public bool IsEmpty { get; }
}