namespace PolarDbBenchmarks;

internal static class BenchmarkChecksum
{
    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong Hash(Row row)
    {
        unchecked
        {
            var hash = Offset;
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
        unchecked
        {
            var hash = Offset;
            foreach (var row in rows)
                hash = Combine(hash, Hash(row));
            return hash;
        }
    }

    public static ulong Combine(ulong current, ulong value)
    {
        unchecked
        {
            current ^= value;
            current *= Prime;
            current ^= current >> 32;
            current *= Prime;
            return current;
        }
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
        hash *= Prime;
    }

    private static void Add(ref ulong hash, string value)
    {
        foreach (var symbol in value)
        {
            hash ^= symbol;
            hash *= Prime;
        }
    }
}
