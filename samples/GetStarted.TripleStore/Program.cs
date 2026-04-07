namespace GetStarted
{
    public partial class Program
    {

        private static readonly IReadOnlyDictionary<string, Action> Scenarios =
            new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase)
            {
                ["main10"] = Main10,
                ["main11"] = Main11,
                ["main13"] = Main13,
                ["main14"] = Main14,
                ["main16"] = Main16,
                ["main19"] = Main19,
                ["main20"] = Main20,
            };

        public static void Main(string[] args)
        {
            if (args.Length == 0 || args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                PrintScenarios();
                return;
            }

            if (args[0].Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var scenario in Scenarios)
                {
                    Console.WriteLine($"=== {scenario.Key} ===");
                    scenario.Value();
                    Console.WriteLine();
                }
                return;
            }

            if (Scenarios.TryGetValue(args[0], out var run))
            {
                run();
                return;
            }

            Console.WriteLine($"Unknown scenario: {args[0]}");
            PrintScenarios();
        }

        private static void PrintScenarios()
        {
            Console.WriteLine("GetStarted.TripleStore scenarios:");
            Console.WriteLine("  list  - show scenarios");
            Console.WriteLine("  all   - run all scenarios in sequence");
            Console.WriteLine("  main10 - Mag_Triple_Store load/build/look");
            Console.WriteLine("  main11 - TripleStore_mag load/build/tests");
            Console.WriteLine("  main13 - TripleStore32 load/build/build-test");
            Console.WriteLine("  main14 - TripleStoreInt32_ build/query");
            Console.WriteLine("  main16 - TripleStoreInt32 RDF-like person dataset");
            Console.WriteLine("  main19 - Polar.TripleStore TripleStoreInt32 experiment");
            Console.WriteLine("  main20 - Polar.TripleStore TripleRecordStore experiment");
        }

        internal static string ScenarioRoot(string scenarioName)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "data", scenarioName);
            Directory.CreateDirectory(path);
            return path + Path.DirectorySeparatorChar;
        }

        internal static string ScenarioDatabases(string scenarioName)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "data", scenarioName, "Databases");
            Directory.CreateDirectory(path);
            return path + Path.DirectorySeparatorChar;
        }
    }
}
