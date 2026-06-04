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
            Add(ref hash, row.Id); Add(ref hash, row.LongKey); Add(ref hash, row.GuidKey);
            Add(ref hash, row.SKey); Add(ref hash, row.ExternalId); Add(ref hash, row.ExternalLong);
            Add(ref hash, row.ExternalGuid); Add(ref hash, row.ExternalKey); Add(ref hash, row.Payload);
            return hash;
        }
    }

    public static ulong HashRows(IEnumerable<Row> rows)
    {
        var accumulator = new BenchmarkRowAccumulator();
        foreach (var row in rows)
            accumulator.Add(row);
        return accumulator.Finish();
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
                Guid value => StableGuidHash(value),
                string value => StableStringHash(value),
                _ => key.GetHashCode()
            };
        }
    }

    private static int StableGuidHash(Guid value)
    {
        var split = BenchmarkGuid.Split(value);
        return StableHash(split.Low) ^ (int)RotateLeft((uint)StableHash(split.High), 17);
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

    private static uint RotateLeft(uint value, int shift) =>
        (value << shift) | (value >> (32 - shift));

    private static void Add(ref ulong hash, long value)
    {
        hash ^= (ulong)value;
        hash *= Prime;
    }

    private static void Add(ref ulong hash, Guid value)
    {
        var split = BenchmarkGuid.Split(value);
        Add(ref hash, split.Low);
        Add(ref hash, split.High);
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
