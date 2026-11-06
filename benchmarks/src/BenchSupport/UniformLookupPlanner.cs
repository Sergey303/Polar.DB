using System.Security.Cryptography;

namespace PolarDbBenchmarks;

// Separate opt-in plan; legacy LookupPlanner remains byte-for-byte unchanged.
internal static class UniformLookupPlanner
{
    public const string Protocol = "uniform-independent-v3";
    public const ulong Seed = 0x6A09E667F3BCC909UL;
    private const int WarmupSize = 500;
    private const int BatchSize = 10_000;
    private const int LatencySize = 2_000;

    internal sealed record Coverage(int RowCount, int UniqueKeys, int BinSize,
        int BinsTotal, int BinsTouched, int MillionRowRegionsTouched,
        string KeyStreamSha256);

    public static (LookupPlan Plan, Coverage Coverage) Create(int rowCount)
    {
        if (rowCount < WarmupSize + BatchSize + LatencySize)
            throw new ArgumentOutOfRangeException(nameof(rowCount),
                "Uniform v3 requires at least 12,500 rows for nonoverlapping keys.");
        var state = Seed;
        var seen = new HashSet<int>();
        object[] Draw(int count)
        {
            var keys = new object[count];
            for (var i = 0; i < count; i++)
            {
                int id;
                do { id = checked((int)Bounded(ref state, (ulong)rowCount) + 1); }
                while (!seen.Add(id));
                keys[i] = (long)id;
            }
            return keys;
        }

        // Three disjoint uniform-without-replacement samples from the full ID range.
        var warm = Draw(WarmupSize);
        var batch = Draw(BatchSize);
        var latency = Draw(LatencySize);
        var plan = new LookupPlan("Uniform independent full-table v3", true,
            warm, batch, latency, BatchSize / 100, 100);
        var stream = warm.Concat(batch).Concat(latency).Select(x => checked((int)(long)x)).ToArray();
        var bytes = new byte[stream.Length * sizeof(int)];
        for (var i = 0; i < stream.Length; i++)
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
                bytes.AsSpan(i * sizeof(int), sizeof(int)), stream[i]);
        var binSize = 50_000;
        var bins = stream.Select(id => (id - 1) / binSize).Distinct().Count();
        var regions = stream.Select(id => (id - 1) / 1_000_000).Distinct().Count();
        var coverage = new Coverage(rowCount, seen.Count, binSize,
            (rowCount + binSize - 1) / binSize, bins, regions,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        return (plan, coverage);
    }

    // SplitMix64, stable across .NET versions and without modulo bias.
    private static ulong Next(ref ulong state)
    {
        var z = unchecked(state += 0x9E3779B97F4A7C15UL);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        return z ^ (z >> 31);
    }

    private static ulong Bounded(ref ulong state, ulong bound)
    {
        var threshold = unchecked(0UL - bound) % bound;
        ulong r;
        do { r = Next(ref state); } while (r < threshold);
        return r % bound;
    }

    public static (QueryResult Batch, QueryResult Latency) Expected(
        LookupPlan plan, ExperimentKind kind)
    {
        if (kind != ExperimentKind.PkIntLookup)
            throw new NotSupportedException("Uniform v3 currently supports only primary int.");
        QueryResult HashKeys(object[] keys)
        {
            ulong sum = 14695981039346656037UL;
            foreach (var raw in keys)
            {
                var accumulator = new BenchmarkRowAccumulator();
                accumulator.Add(BenchmarkData.CreateRow((long)raw, kind));
                sum = BenchmarkChecksum.Combine(sum, accumulator.Finish());
            }
            return new QueryResult(keys.Length, sum);
        }
        return (HashKeys(plan.BatchKeys), HashKeys(plan.LatencyKeys));
    }
}
