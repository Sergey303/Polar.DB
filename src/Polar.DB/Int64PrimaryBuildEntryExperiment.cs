namespace Polar.Universal;

internal readonly struct Int64PrimaryBuildEntryExperiment
{
    public Int64PrimaryBuildEntryExperiment(int hashKey, long key, long offset)
    {
        HashKey = hashKey;
        Key = key;
        Offset = offset;
    }

    public int HashKey { get; }
    public long Key { get; }
    public long Offset { get; }
}
