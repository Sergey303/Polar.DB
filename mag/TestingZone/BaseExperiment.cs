using System.Diagnostics;
using Polar.DB;

internal static class BaseExperiment
{
    private static readonly PTypeRecord RecordType = new(
        new NamedType("code", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("age", new PType(PTypeEnumeration.integer)));

    internal static void Run(string root, int n, string phase)
    {
        string dir = Path.Combine(root, "base");
        string data = Path.Combine(dir, "f0.bin");
        Directory.CreateDirectory(dir);
        if (phase == "reopen" && !File.Exists(data))
            throw new FileNotFoundException("Run the load phase first.", data);
        if (phase == "load")
        {
            if (File.Exists(data)) File.Delete(data);
            string checkpoint = Path.ChangeExtension(data, ".state");
            if (File.Exists(checkpoint)) File.Delete(checkpoint);
        }

        var timer = Stopwatch.StartNew();
        using var stream = new FileStream(data, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        using var sequence = new UniversalSequenceBase(RecordType, stream);
        Console.WriteLine($"BASE {phase} ctor_ms={timer.Elapsed.TotalMilliseconds:F1} (recovery is in constructor)");
        if (phase == "load")
        {
            timer.Restart();
            for (int i = 0; i < n; i++)
                sequence.AppendElement(new object[] { n - i - 1, i.ToString(), 22 });
            sequence.Flush();
            Console.WriteLine($"BASE append_ms={timer.Elapsed.TotalMilliseconds:F1}");
        }
        // Reopening already restores the sequence. Refresh would repeat a full validation scan.

        Check.Equal(n, sequence.Count(), "base persisted record count");
        timer.Restart();
        long sum = 0, seen = 0;
        foreach (object element in sequence.ElementValues())
        {
            sum += (int)((object[])element)[2];
            seen++;
        }
        Check.Equal(n, seen, "base ElementValues count");
        Check.Equal(22L * n, sum, "base ElementValues checksum");
        Check.Rate("BASE ElementValues", n, timer.Elapsed);

        timer.Restart();
        sum = seen = 0;
        sequence.Scan((_, record) =>
        {
            sum += (int)((object[])record)[2];
            seen++;
            return true;
        });
        Check.Equal(n, seen, "base Scan count");
        Check.Equal(22L * n, sum, "base Scan checksum");
        Check.Rate("BASE Scan", n, timer.Elapsed);
        Check.Equal(n - 1, (int)((object[])sequence.GetElement(8L))[0], "base first value");
        Console.WriteLine($"BASE PASS count={n} bytes={stream.Length} append_offset={sequence.AppendOffset}");
    }
}
