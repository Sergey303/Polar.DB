using System.Diagnostics;
using Polar.DB;

namespace Legacy2021Replay;

// Port of GetStarted3/Main306.cs. Keep its search algorithm and Stopwatch
// boundaries: this tests sequence+position-index, NOT ExternalKeyIndex<T>.
internal static class IndexReplay
{
    internal static void Run(string dir, int nelements, int ntests, int? seed)
    {
        Stopwatch sw = new Stopwatch();
        PType tp_rec = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)),
            new NamedType("age", new PType(PTypeEnumeration.integer)));

        using Stream stream = File.Open(Path.Combine(dir, "data306.bin"), FileMode.OpenOrCreate);
        using Stream stream2 = File.Open(Path.Combine(dir, "keys306.bin"), FileMode.OpenOrCreate);
        using Stream stream3 = File.Open(Path.Combine(dir, "offsets306.bin"), FileMode.OpenOrCreate);
        var sequence = new UniversalSequenceBase(tp_rec, stream);
        var sequence2 = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), stream2);
        var sequence3 = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), stream3);
        Random rnd = seed.HasValue ? new Random(seed.Value) : new Random();
        long[] offsets = new long[nelements];
        int[] keys = new int[nelements];

        sw.Restart();
        sequence.Clear();
        for (int i = 0; i < nelements; i++)
        {
            int key = nelements - i - 1;
            offsets[i] = sequence.AppendElement(new object[] { key, "Иванов" + key, rnd.Next(1, 110) });
            keys[i] = key;
        }
        sequence.Flush();
        Array.Sort(keys, offsets);
        sequence2.Clear();
        for (int i = 0; i < nelements; i++) sequence2.AppendElement(keys[i]);
        sequence2.Flush();
        sequence3.Clear();
        for (int i = 0; i < nelements; i++) sequence3.AppendElement(offsets[i]);
        sequence3.Flush();
        sw.Stop();
        Console.WriteLine($"Load of {nelements} elements. duration={sw.ElapsedMilliseconds}");

        int k = nelements * 2 / 3;
        long ind = BinarySequenceSearchFirst(0, nelements, k, sequence2);
        long offf = (long)sequence3.GetByIndex(ind);
        object rec = sequence.GetElement(offf);
        Console.WriteLine($"k={k}, v={tp_rec.Interpret(rec)}");

        sw.Restart();
        for (int j = 0; j < ntests; j++)
        {
            int key = rnd.Next(nelements);
            long nom1 = BinarySequenceSearchFirst(0, nelements, key, sequence2);
            long off = (long)sequence3.GetByIndex(nom1);
            object[] fields = (object[])sequence.GetElement(off);
            if (key != (int)fields[0]) throw new Exception("1233eddf");
        }
        sw.Stop();
        Console.WriteLine($"duration of {ntests} tests is {sw.ElapsedMilliseconds} ms.");
        Console.WriteLine($"verified_lookup_count={ntests}");
        sequence3.Close();
        sequence2.Close();
        sequence.Close();
    }

    // Exact recursive search structure from the 2021 Main306 method.
    private static long BinarySequenceSearchFirst(long start, long number, int key, UniversalSequenceBase seq)
    {
        long half = number / 2;
        int middle_keyvalue = (int)seq.GetByIndex(start + half);
        if (half == 0)
        {
            if ((int)seq.GetByIndex(start) == key) return start;
            else if ((int)seq.GetByIndex(start + 1) == key) return start + 1;
            else return -1;
        }
        if (middle_keyvalue == key) return start + half;
        long middle = start + half;
        long rest = number - half - 1;
        var middle_depth = middle_keyvalue - key;
        if (middle_depth == 0) return middle;
        if (middle_depth < 0)
        {
            return BinarySequenceSearchFirst(middle + 1, rest, key, seq);
        }
        else
        {
            return BinarySequenceSearchFirst(start, half, key, seq);
        }
    }
}
