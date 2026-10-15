using System.Diagnostics;
using Polar.DB;
using Polar.DB.ExternalKey;
using Polar.Universal;

internal static class IndexedExperiment
{
    private static readonly PTypeRecord RecordType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("age", new PType(PTypeEnumeration.integer)),
        new NamedType("tags", new PTypeSequence(new PType(PTypeEnumeration.sstring))));

    private static object[] Row(int id, string name, int age, params string[] tags) =>
        new object[] { id, name, age, tags.Cast<object>().ToArray() };

    private static object[] Initial(int id) =>
        Row(id, $"NAME{id:D6}", 20 + id % 7, id % 2 == 0 ? "even" : "odd", $"topic{id % 5}");

    internal static void Run(string root, int n, string phase, bool optimise)
    {
        string dir = Path.Combine(root, optimise ? "indexed-optimised" : "indexed-standard");
        string state = Path.Combine(dir, "state.bin");
        Directory.CreateDirectory(dir);
        if (phase == "reopen" && (!File.Exists(state) || !File.Exists(Path.Combine(dir, "f0.bin"))))
            throw new FileNotFoundException("Run indexed load first.", state);
        if (phase == "load")
        {
            foreach (string file in Directory.EnumerateFiles(dir, "f*.bin")) File.Delete(file);
            if (File.Exists(state)) File.Delete(state);
            foreach (string file in Directory.EnumerateFiles(dir, "f*.state")) File.Delete(file);
        }

        int fileNo = 0;
        Stream NextStream() => new FileStream(Path.Combine(dir, $"f{fileNo++}.bin"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite);

        var timer = Stopwatch.StartNew();
        using var sequence = new USequence(RecordType, state, NextStream,
            isEmpty: ob => string.IsNullOrEmpty((string)((object[])ob)[1]), optimise: optimise);
        sequence.SetPrimaryKey<int>(ob => (int)((object[])ob)[0]);
        Console.WriteLine($"INDEXED {phase} constructor_ms={timer.Elapsed.TotalMilliseconds:F1}");

        // Index positions: 0 external sortable age, 1 vector of tags, 2 names/Like,
        // 3 comparator-based UIndex, 4 historic external sorted int key.
        var external = new ExternalKeyIndex<int>(NextStream, sequence,
            ob => new[] { (int)((object[])ob)[2] });
        var tags = new SVectorIndex(NextStream, sequence,
            ob => ((object[])((object[])ob)[3]).Cast<string>());
        var names = new SVectorIndex(NextStream, sequence,
            ob => new[] { (string)((object[])ob)[1] });
        var comparerIndex = new UIndex(NextStream, sequence, _ => true,
            ob => Hashfunctions.HashRot13((string)((object[])ob)[1]),
            Comparer<object>.Create((a, b) => string.Compare(
                (string)((object[])a)[1], (string)((object[])b)[1], StringComparison.Ordinal)));
        var sortIndex = new ESortIndex32(NextStream, sequence,
            ob => new[] { (int)((object[])ob)[2] }, Comparer<int>.Default);
        sequence.uindexes = new IUIndex[] { external, tags, names, comparerIndex, sortIndex };

        if (phase == "load")
        {
            timer.Restart();
            sequence.Load(Enumerable.Range(0, n).Select(i => (object)Initial(i)));
            Console.WriteLine($"INDEXED load_ms={timer.Elapsed.TotalMilliseconds:F1}");
            timer.Restart();
            sequence.Build();
            Console.WriteLine($"INDEXED build_ms={timer.Elapsed.TotalMilliseconds:F1}");
            sequence.AppendElement(Row(7, "EDITED", 55, "edited", "topic5"));
            sequence.AppendElement(Row(9, "", 0)); // logical deletion (tombstone)
            sequence.AppendElement(Row(n, "NEW", 55, "new"));
            ValidateQueries(sequence, sortIndex, n, "dynamic");
            timer.Restart();
            sequence.Build();
            Console.WriteLine($"INDEXED rebuild_after_edits_ms={timer.Elapsed.TotalMilliseconds:F1}");
        }
        else
        {
            timer.Restart();
            sequence.Refresh();
            Console.WriteLine($"INDEXED refresh_ms={timer.Elapsed.TotalMilliseconds:F1}");
        }

        ValidateQueries(sequence, sortIndex, n, phase);
        timer.Restart();
        long seen = 0, sum = 0;
        sequence.Scan((_, row) => { seen++; sum += (int)((object[])row)[0]; return true; });
        Check.Equal(n, seen, "indexed active count");
        Check.Equal((long)n * (n + 1) / 2 - 9, sum, "indexed active id checksum");
        Check.Rate("INDEXED logical Scan", n, timer.Elapsed);

        timer.Restart();
        for (int i = 0; i < 10000; i++)
            Check.That(sequence.GetByKey(i % n) != null || i % n == 9, "indexed lookup");
        Check.Rate("INDEXED key lookups", 10000, timer.Elapsed);
        Console.WriteLine($"INDEXED PASS active={seen} physical={sequence.Count()} state={state}");
    }

    private static void ValidateQueries(USequence sequence, ESortIndex32 sorted, int n, string stage)
    {
        static int[] Ids(IEnumerable<object> rows) => rows.Cast<object[]>()
            .Select(row => (int)row[0]).OrderBy(id => id).ToArray();
        static void Single(IEnumerable<object> rows, int expected, string label)
        {
            int[] ids = Ids(rows);
            Check.Equal(1, ids.Length, label + " count");
            Check.Equal(expected, ids[0], label + " id");
        }

        Check.Equal(n + 3L, sequence.Count(), stage + " physical count");
        Check.Equal("EDITED", (string)((object[])sequence.GetByKey(7)!)[1], stage + " updated key");
        Check.That(sequence.GetByKey(9) == null, stage + " tombstone");
        Check.Equal("NEW", (string)((object[])sequence.GetByKey(n)!)[1], stage + " inserted key");
        Single(sequence.GetAllByValue(1, "edited", _ => Array.Empty<IComparable>()), 7, stage + " vector tag");
        Single(sequence.GetAllByLike(2, "EDIT"), 7, stage + " prefix Like");
        Single(sequence.GetAllBySample(3, Row(0, "EDITED", 0)), 7, stage + " comparator");
        Single(sequence.GetAllByValue(2, "edited", _ => Array.Empty<IComparable>()), 7, stage + " exact name");
        Check.Equal(0, Ids(sequence.GetAllByValue(2, "NAME000007", _ => Array.Empty<IComparable>())).Length,
            stage + " no stale name");
        Check.Equal(0, Ids(sequence.GetAllByValue(0, 20, _ => Array.Empty<IComparable>())).Count(id => id == 7),
            stage + " no stale external key");
        Check.That(Ids(sequence.GetAllByValue(0, 55, _ => Array.Empty<IComparable>())).SequenceEqual(new[] { 7, n }),
            stage + " external sorted age");
        Check.That(Ids(sorted.GetManyByValue(55)).SequenceEqual(new[] { 7, n }),
            stage + " legacy external sorted key");
        Console.WriteLine($"INDEXED {stage} primary/external/vector/Like/comparator/sorted PASS");
    }
}
