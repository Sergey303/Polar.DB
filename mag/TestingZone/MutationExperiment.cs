using System.Diagnostics;
using Polar.DB;

internal static class MutationExperiment
{
    internal static void Run(string root, string phase)
    {
        string dir = Path.Combine(root, "mutation");
        string file = Path.Combine(dir, "fixed-int.bin");
        Directory.CreateDirectory(dir);
        if (phase == "reopen" && !File.Exists(file))
            throw new FileNotFoundException("Run mutation load first.", file);
        if (phase == "load" && File.Exists(file)) File.Delete(file);

        var timer = Stopwatch.StartNew();
        using var stream = new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        using var sequence = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), stream);
        Console.WriteLine($"MUTATION {phase} constructor_ms={timer.Elapsed.TotalMilliseconds:F1}");
        if (phase == "load")
        {
            sequence.AppendElement(10);
            long replacedOffset = sequence.AppendElement(20);
            sequence.AppendElement(30);
            Check.Equal(12L, replacedOffset, "fixed-size second offset");
            sequence.SetElement(222, replacedOffset);
            sequence.Flush();
        }
        else sequence.Refresh();

        Check.Equal(3L, sequence.Count(), "fixed-size count");
        Check.Equal(20L, sequence.AppendOffset, "fixed-size logical end");
        Check.Equal(10, (int)sequence.GetByIndex(0), "fixed first");
        Check.Equal(222, (int)sequence.GetByIndex(1), "fixed in-place updated element");
        Check.Equal(30, (int)sequence.GetByIndex(2), "fixed last");
        Check.That(sequence.ElementValues().Cast<int>().SequenceEqual(new[] { 10, 222, 30 }),
            "fixed persistence after reopen");
        Console.WriteLine("MUTATION PASS fixed-size in-place edit + reopen");
    }
}
