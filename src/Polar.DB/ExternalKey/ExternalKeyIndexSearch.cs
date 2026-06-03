namespace Polar.DB.ExternalKey;

internal static class ExternalKeyIndexSearch
{
    internal static int FindFirstEqual<T>(T[] values, T sample, IComparer<T> comparer)
        where T : IComparable<T>
    {
        int lo = 0;
        int hi = values.Length;

        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (comparer.Compare(values[mid], sample) < 0)
                lo = mid + 1;
            else
                hi = mid;
        }

        if (lo >= values.Length) return -1;
        return comparer.Compare(values[lo], sample) == 0 ? lo : -1;
    }
}
