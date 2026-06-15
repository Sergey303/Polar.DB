namespace Polar.Universal;

internal readonly struct UKeyIndexDynamicEntry
{
    internal UKeyIndexDynamicEntry(long offset, long revision)
    {
        Offset = offset;
        Revision = revision;
    }

    internal long Offset { get; }
    internal long Revision { get; }
}
