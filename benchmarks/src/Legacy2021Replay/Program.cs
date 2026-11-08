using System.Globalization;

namespace Legacy2021Replay;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length is < 2 or > 5)
            {
                Console.Error.WriteLine("Usage: Legacy2021Replay <binary|binary-reuse|index> <absolute-output-directory> [rows<=100000] [probes] [seed]");
                return 2;
            }

            string scenario = args[0];
            string dir = Path.GetFullPath(args[1]);
            int rows = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 10_000;
            int probes = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : scenario.StartsWith("binary", StringComparison.Ordinal) ? 100_000 : 1000;
            int? seed = args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : null;
            if (rows < 2 || rows > 100_000 || probes < 1 || probes > 100_000)
                throw new ArgumentOutOfRangeException(nameof(args), "Safety bound: 2..100000 rows and 1..100000 probes.");
            if (scenario == "binary-reuse")
            {
                string file = Path.Combine(dir, "data.bin");
                if (!File.Exists(file) || new FileInfo(file).Length != rows * sizeof(long))
                    throw new InvalidOperationException("Reuse requires an existing complete data.bin file.");
            }
            else
            {
                if (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any())
                    throw new InvalidOperationException("Output directory must be empty: " + dir);
                Directory.CreateDirectory(dir);
            }
            Console.WriteLine($"legacy_2021_method={scenario} rows={rows} probes={probes} seed={(seed?.ToString() ?? "historical-random")}");
            Console.WriteLine("historic-source=agmarchuk/PolarDB@39cefe13db8697cf5f8e15edaa4677ba52a2cdea");
            Console.WriteLine("engine-source=" +
                (Environment.GetEnvironmentVariable("REPLAY_ENGINE_SHA") ?? "UNVERIFIED; set REPLAY_ENGINE_SHA"));
            if (scenario is "binary" or "binary-reuse")
                BinaryReplay.Run(dir, rows, probes, seed, scenario == "binary");
            else if (scenario == "index") IndexReplay.Run(dir, rows, probes, seed);
            else throw new ArgumentException("Unknown scenario " + scenario);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
