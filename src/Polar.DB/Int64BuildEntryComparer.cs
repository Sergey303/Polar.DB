namespace Polar.Universal;

internal sealed class Int64BuildEntryComparer : IComparer<Int64BuildEntry>
{
    public static readonly Int64BuildEntryComparer Instance = new();

    public int Compare(Int64BuildEntry left, Int64BuildEntry right)
    {
        var hashComparison = left.HashKey.CompareTo(right.HashKey);
        if (hashComparison != 0) return hashComparison;

        var keyComparison = left.Key.CompareTo(right.Key);
        if (keyComparison != 0) return keyComparison;

        return left.Offset.CompareTo(right.Offset);
    }
}
