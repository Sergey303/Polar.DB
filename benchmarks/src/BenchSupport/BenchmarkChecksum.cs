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
            Add(ref hash, row.LongKey);
            Add(ref hash, row.GuidKey);
            Add(ref hash, row.SKey);
            Add(ref hash, row.ExternalId);
            Add(ref hash, row.ExternalLong);
            Add(ref hash, row.ExternalGuid);
            Add(ref hash, row.ExternalKey);
            Add(ref hash, row.Payload);
            return hash;
        }
    }

    public static ulong HashRows(IEnumerable<Row> rows)
    {
        unchecked
        {
            ulong count = 0;
            ulong sum = 0;
            ulong xor = 0;
            ulong mixedSum = 0;
            foreach (var row in rows)
            {
                var hash = Hash(row);
                var mixed = Mix(hash);
                count++;
                sum += hash;
                mixedSum += mixed;
                xor ^= RotateLeft(mixed, (int)(hash & 63));
            }
            return FinalizeRows(count, sum, xor, mixedSum);
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
                Guid value => StableGuidHash(value),
                string value => StableStringHash(value),
                _ => key.GetHashCode()
            };
        }
    }

    private static int StableGuidHash(Guid value)
    {
        var split = BenchmarkGuid.Split(value);
        return StableHash(split.Low) ^ RotateLeft((ulong)StableHash(split.High), 17).GetHashCode();
    }

    private static ulong FinalizeRows(ulong count, ulong sum, ulong xor, ulong mixedSum)
    {
        var result = Offset;
        result = Combine(result, count);
        result = Combine(result, sum);
        result = Combine(result, xor);
        result = Combine(result, mixedSum);
        return result;
    }

    private static ulong Mix(ulong value)
    {
        value ^= value >> 33;
        value *= 0xff51afd7ed558ccdUL;
        value ^= value >> 33;
        value *= 0xc4ceb9fe1a85ec53UL;
        value ^= value >> 33;
        return value;
    }

    private static ulong RotateLeft(ulong value, int shift)
    {
        shift &= 63;
        return shift == 0 ? value : (value << shift) | (value >> (64 - shift));
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
