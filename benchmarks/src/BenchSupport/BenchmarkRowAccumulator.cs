namespace PolarDbBenchmarks;

internal struct BenchmarkRowAccumulator
{
    private ulong _count;
    private ulong _sum;
    private ulong _xor;
    private ulong _mixedSum;

    public void Add(Row row)
    {
        var hash = BenchmarkChecksum.Hash(row);
        var mixed = Mix(hash);
        _count++;
        _sum += hash;
        _mixedSum += mixed;
        _xor ^= RotateLeft(mixed, (int)(hash & 63));
    }

    public ulong Finish()
    {
        ulong result = 14695981039346656037UL;
        result = BenchmarkChecksum.Combine(result, _count);
        result = BenchmarkChecksum.Combine(result, _sum);
        result = BenchmarkChecksum.Combine(result, _xor);
        result = BenchmarkChecksum.Combine(result, _mixedSum);
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
}
