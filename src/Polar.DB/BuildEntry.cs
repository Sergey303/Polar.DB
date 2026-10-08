namespace Polar.Universal;

internal readonly struct BuildEntry
{
    public BuildEntry(int hashKey, long offset, bool isEmpty)
    {
        Offset = offset;
        HashKey = hashKey;
        IsEmpty = isEmpty;
    }

    public long Offset { get; }
    public int HashKey { get; }
    public bool IsEmpty { get; }
}
