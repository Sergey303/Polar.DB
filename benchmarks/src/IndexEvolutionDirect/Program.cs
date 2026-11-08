using System.Diagnostics;
using System.Globalization;
using Polar.DB;

namespace IndexEvolutionDirect;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 4)
                throw new ArgumentException("Usage: <empty-data-directory> <rows> <probes> <seed>");
            string path = Path.GetFullPath(args[0]);
            int rows = int.Parse(args[1], CultureInfo.InvariantCulture);
            int probes = int.Parse(args[2], CultureInfo.InvariantCulture);
            int seed = int.Parse(args[3], CultureInfo.InvariantCulture);
            if (rows is < 2 or > 1_000_000 || probes is < 1 or > 100_000)
                throw new ArgumentOutOfRangeException(nameof(args), "Safety limits: rows<=1m probes<=100k");
            if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
                throw new InvalidOperationException("The data directory must be empty: " + path);
            Directory.CreateDirectory(path);
            var factory = new SequentialStreams(path);
            var recordType = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.integer)),
                new NamedType("external_key", new PType(PTypeEnumeration.integer)));
            using IIndexAdapter adapter = AdapterFactory.Create(recordType, factory);
            int[] queries = new int[probes];
            var random = new Random(seed);
            for (int i = 0; i < probes; i++) queries[i] = random.Next(rows);

            var stopwatch = Stopwatch.StartNew();
            adapter.Load(Records(rows));
            stopwatch.Stop();
            double loadMs = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            adapter.PreparePrerequisites();
            stopwatch.Stop();
            double prepareMs = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            adapter.BuildAndFlush();
            stopwatch.Stop();
            double buildMs = stopwatch.Elapsed.TotalMilliseconds;

            // Do not put warmup or correctness checks inside the 1000-search timer.
            Verify(adapter.Lookup(-1), -1, rows, 0);
            Verify(adapter.Lookup(0), 0, rows, 1);
            Verify(adapter.Lookup(rows - 1), rows - 1, rows, 1);
            Verify(adapter.Lookup(rows / 2), rows / 2, rows, 1);
            for (int i = 0; i < Math.Min(100, probes); i++)
                Verify(adapter.Lookup(queries[i]), queries[i], rows, 1);

            long expectedChecksum = 0;
            foreach (int key in queries) expectedChecksum += rows - 1L - key;
            long foundChecksum = 0;
            stopwatch.Restart();
            foreach (int key in queries)
                foundChecksum += Verify(adapter.Lookup(key), key, rows, 1);
            stopwatch.Stop();
            if (foundChecksum != expectedChecksum)
                throw new InvalidDataException("Incorrect lookup checksum");
            string engine = Environment.GetEnvironmentVariable("INDEX_EVOLUTION_SHA")
                ?? throw new InvalidOperationException("INDEX_EVOLUTION_SHA is required");
            Console.WriteLine($"RESULT engine={engine} rows={rows} probes={probes} seed={seed} " +
                $"load_ms={loadMs.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"prerequisite_ms={prepareMs.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"index_build_ms={buildMs.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"lookup_ms={stopwatch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"checksum={foundChecksum} verified=true");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static IEnumerable<object> Records(int rows)
    {
        for (int id = 0; id < rows; id++) yield return new object[] { id, rows - 1 - id };
    }

    private static long Verify(IEnumerable<object> values, int externalKey, int rows, int expectedCount)
    {
        long sum = 0;
        int count = 0;
        foreach (object value in values)
        {
            object[] fields = (object[])value;
            int id = (int)fields[0];
            int key = (int)fields[1];
            if (id != rows - 1 - externalKey || key != externalKey)
                throw new InvalidDataException($"Wrong result for {externalKey}: {id}, {key}");
            count++;
            sum += id;
        }
        if (count != expectedCount)
            throw new InvalidDataException($"Key {externalKey}: expected {expectedCount}, got {count}");
        return sum;
    }
}
