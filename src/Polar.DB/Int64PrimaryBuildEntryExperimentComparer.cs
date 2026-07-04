namespace Polar.Universal;

internal sealed class Int64PrimaryBuildEntryExperimentComparer : IComparer<Int64PrimaryBuildEntryExperiment>
{
    public static readonly Int64PrimaryBuildEntryExperimentComparer Instance = new();

    public int Compare(Int64PrimaryBuildEntryExperiment left, Int64PrimaryBuildEntryExperiment right)
    {
        var hashComparison = left.HashKey.CompareTo(right.HashKey);
        if (hashComparison != 0) return hashComparison;

        var keyComparison = left.Key.CompareTo(right.Key);
        if (keyComparison != 0) return keyComparison;

        return left.Offset.CompareTo(right.Offset);
    }
}
