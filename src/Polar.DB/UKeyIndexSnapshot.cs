namespace Polar.Universal;

internal sealed class UKeyIndexSnapshot
{
    internal static readonly UKeyIndexSnapshot Empty =
        new(Array.Empty<int>(), Array.Empty<long>());

    internal UKeyIndexSnapshot(int[] hashes, long[] offsets)
    {
        Hashes = hashes;
        Offsets = offsets;
    }

    internal int[] Hashes { get; }
    internal long[] Offsets { get; }
}
