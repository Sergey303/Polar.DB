namespace Polar.Universal;

internal readonly struct BuildEntry
{
    public BuildEntry(int hashKey, long offset, bool isEmpty)
    {
        HashKey = hashKey;
        Offset = offset;
        IsEmpty = isEmpty;
    }

    public int HashKey { get; }
    public long Offset { get; }
    public bool IsEmpty { get; }
}
