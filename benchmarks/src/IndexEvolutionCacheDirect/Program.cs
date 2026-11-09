using System.Diagnostics;
using System.Text.Json;
using System.Globalization;
using Polar.DB;

namespace IndexEvolutionDirect;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 6)
                throw new ArgumentException("Usage: <empty-data-directory> <rows> <probes> <seed> <baseline|refresh|evict|modern> <controller.py>");
            string path = Path.GetFullPath(args[0]);
            int rows = int.Parse(args[1], CultureInfo.InvariantCulture);
            int probes = int.Parse(args[2], CultureInfo.InvariantCulture);
            int seed = int.Parse(args[3], CultureInfo.InvariantCulture);
            string mode = args[4];
            string controller = Path.GetFullPath(args[5]);
            if (!File.Exists(controller)) throw new FileNotFoundException("Cache controller", controller);
            if (mode is not ("baseline" or "refresh" or "evict" or "modern"))
                throw new ArgumentException("Unsupported cache mode: " + mode);
#if LEGACY2021
            if (mode == "modern") throw new ArgumentException("Wrong engine for modern mode");
#else
            if (mode != "modern") throw new ArgumentException("Wrong engine for historical cache mode");
#endif
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

            // Manipulation after 100 correctness/warmup probes and before the timed phase.
            string cacheJson = "{}";
#if LEGACY2021
            if (mode == "refresh") ((LegacyAdapter)adapter).RefreshIndex();
            var control = new ProcessStartInfo("python3")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            control.ArgumentList.Add(controller);
            control.ArgumentList.Add(Path.Combine(path, "file-1.bin"));
            control.ArgumentList.Add(mode == "evict" ? "evict" : "inspect");
            using (var process = Process.Start(control) ?? throw new IOException("Cannot start controller"))
            {
                cacheJson = process.StandardOutput.ReadToEnd().Trim();
                string errors = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new IOException("Cache controller failed: " + errors);
                using var parsed = JsonDocument.Parse(cacheJson);
                double fraction = parsed.RootElement.GetProperty("after_resident_fraction").GetDouble();
                if (mode == "evict" && fraction > 0.10)
                    throw new InvalidDataException($"Eviction not confirmed: {fraction:F4}");
                if (mode == "refresh" && fraction < 0.90)
                    throw new InvalidDataException($"Refresh did not make index resident: {fraction:F4}");
            }
#endif
            long expectedChecksum = 0;
            foreach (int key in queries) expectedChecksum += rows - 1L - key;
            long foundChecksum = 0;
            stopwatch.Restart();
            for (int i = 0; i < Math.Min(100, probes); i++)
                foundChecksum += Verify(adapter.Lookup(queries[i]), queries[i], rows, 1);
            stopwatch.Stop();
            double first100Ms = stopwatch.Elapsed.TotalMilliseconds;
            stopwatch.Restart();
            for (int i = Math.Min(100, probes); i < probes; i++)
                foundChecksum += Verify(adapter.Lookup(queries[i]), queries[i], rows, 1);
            stopwatch.Stop();
            double tailMs = stopwatch.Elapsed.TotalMilliseconds;
            if (foundChecksum != expectedChecksum)
                throw new InvalidDataException("Incorrect lookup checksum");
            string engine = Environment.GetEnvironmentVariable("INDEX_EVOLUTION_SHA")
                ?? throw new InvalidOperationException("INDEX_EVOLUTION_SHA is required");
            Console.WriteLine($"RESULT engine={engine} rows={rows} probes={probes} seed={seed} " +
                $"load_ms={loadMs.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"prerequisite_ms={prepareMs.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"index_build_ms={buildMs.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"mode={mode} " +
                $"first100_ms={first100Ms.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"tail_ms={tailMs.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"lookup_ms={(first100Ms+tailMs).ToString("F3", CultureInfo.InvariantCulture)} " +
                $"cache_json={cacheJson} " +
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
