namespace Polar.Universal;

internal sealed class BuildEntryComparer : IComparer<BuildEntry>
{
    public static readonly BuildEntryComparer Instance = new();

    public int Compare(BuildEntry left, BuildEntry right)
    {
        var hashComparison = left.HashKey.CompareTo(right.HashKey);
        return hashComparison != 0
            ? hashComparison
            : left.Offset.CompareTo(right.Offset);
    }
}
