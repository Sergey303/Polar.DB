namespace Polar.Universal;

internal sealed class BuildEntryComparer : IComparer<BuildEntry>
{
    public static readonly BuildEntryComparer Instance = new();

    public int Compare(BuildEntry left, BuildEntry right)
    {
        var hashComparison = left.HashKey.CompareTo(right.HashKey);
        if (hashComparison != 0) return hashComparison;

        var keyComparison = left.Key.CompareTo(right.Key);
        if (keyComparison != 0) return keyComparison;

        return left.Offset.CompareTo(right.Offset);
    }
}