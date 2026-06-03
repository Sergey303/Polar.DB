namespace PolarDbBenchmarks;

internal static class BenchmarkChecksum
{
    public static ulong Hash(Row row)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            Add(ref hash, row.Id);
            Add(ref hash, row.ExternalId);
            Add(ref hash, row.SKey);
            Add(ref hash, row.ExternalKey);
            Add(ref hash, row.Payload);
            return hash;
        }
    }

    public static ulong HashRows(IEnumerable<Row> rows)
    {
        ulong result = 0;
        foreach (var row in rows)
            result ^= Hash(row);
        return result;
    }

    public static int StableHash(IComparable key)
    {
        unchecked
        {
            return key switch
            {
                int value => value,
                long value => (int)(value ^ (value >> 32)),
                string value => StableStringHash(value),
                _ => key.GetHashCode()
            };
        }
    }

    private static int StableStringHash(string value)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var symbol in value)
            {
                hash ^= symbol;
                hash *= 16777619;
            }

            return hash;
        }
    }

    private static void Add(ref ulong hash, long value)
    {
        hash ^= (ulong)value;
        hash *= 1099511628211UL;
    }

    private static void Add(ref ulong hash, string value)
    {
        foreach (var symbol in value)
        {
            hash ^= symbol;
            hash *= 1099511628211UL;
        }
    }
}
