using System.Collections.Concurrent;
using Polar.DB.Typed;
using Xunit;

namespace Polar.DB.Tests;

public class DbSetPrimaryKeyConfigurationTests
{
    [Fact]
    public void DbSet_PrimaryKeys_Reopen_For_Int_String_Long_And_Guid()
    {
        string root = CreateRoot();
        var guid = Guid.Parse("11111111-2222-3333-4444-555555555555");

        try
        {
            using (var ints = Open<IntEntity, int>(root, "ints", item => item.Id))
                ints.Append(new IntEntity(7, "int"));

            using (var strings = Open<StringEntity, string>(root, "strings", item => item.Id))
                strings.Append(new StringEntity("alpha", "string"));

            using (var longs = Open<LongEntity, long>(root, "longs", item => item.Id))
                longs.Append(new LongEntity(9_000_000_000L, "long"));

            using (var guids = Open<GuidEntity, Guid>(root, "guids", item => item.Id))
                guids.Append(new GuidEntity(guid, "guid"));

            using (var ints = Open<IntEntity, int>(root, "ints", item => item.Id))
                Assert.Equal("int", ints.GetByKey(7).Value);

            using (var strings = Open<StringEntity, string>(root, "strings", item => item.Id))
                Assert.Equal("string", strings.GetByKey("alpha").Value);

            using (var longs = Open<LongEntity, long>(root, "longs", item => item.Id))
                Assert.Equal("long", longs.GetByKey(9_000_000_000L).Value);

            using (var guids = Open<GuidEntity, Guid>(root, "guids", item => item.Id))
                Assert.Equal("guid", guids.GetByKey(guid).Value);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void DbSet_GuidPrimaryKey_ExternalStringIndexes_ReturnCorrectRecords()
    {
        string root = CreateRoot();
        var expected = new TripleLikeRow(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            "resource:target",
            "predicate:target",
            1,
            "object:target",
            "ru",
            "http://www.w3.org/2001/XMLSchema#string",
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "cassette-target",
            "fog/target.xml",
            638895210000000000L,
            "15:resource:target16:predicate:target",
            "16:predicate:target1:113:object:target");

        TripleLikeRow[] rows =
        {
            new TripleLikeRow(
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                "short",
                "p",
                0,
                "o",
                string.Empty,
                string.Empty,
                Guid.Parse("10000000-0000-0000-0000-000000000001"),
                "cassette-a",
                "a.xml",
                1L,
                "5:short1:p",
                "1:p1:01:o"),
            expected,
            new TripleLikeRow(
                Guid.Parse("ffffffff-eeee-dddd-cccc-bbbbbbbbbbbb"),
                new string('s', 257),
                new string('p', 129),
                2,
                new string('o', 513),
                "en",
                "urn:very-long-data-type",
                Guid.Parse("99999999-8888-7777-6666-555555555555"),
                "cassette-with-a-longer-name",
                "nested/path/with/a/long/file-name.xml",
                long.MaxValue - 1,
                new string('x', 401),
                new string('y', 403))
        };

        try
        {
            using (var triples = OpenTriples(root))
            {
                triples.AddRange(rows);
                BuildAllExternalIndexes(triples);
                AssertExternalLookups(triples, expected);
            }

            using (var triples = OpenTriples(root))
                AssertExternalLookups(triples, expected);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void DbSet_ConcurrentSequenceBackedReads_DoNotShareTheStreamCursor()
    {
        string root = CreateRoot();
        TripleLikeRow[] rows = CreateConcurrentRows(48);

        try
        {
            using var triples = OpenTriples(root);
            triples.AddRange(rows);
            BuildAllExternalIndexes(triples);

            var errors = new ConcurrentQueue<Exception>();
            int workers = Math.Max(8, Environment.ProcessorCount * 2);

            Parallel.For(0, workers, worker =>
            {
                try
                {
                    for (int iteration = 0; iteration < 500; iteration++)
                    {
                        TripleLikeRow expected = rows[(worker * 37 + iteration) % rows.Length];
                        TripleLikeRow bySubject = triples.Find(row => row.Subject, expected.Subject).Single();
                        TripleLikeRow byComposite = triples
                            .Find(row => row.PredicateObjectKey, expected.PredicateObjectKey)
                            .Single();
                        TripleLikeRow byPrimaryKey = triples.GetByKey(expected.TripleId);

                        if (bySubject != expected || byComposite != expected || byPrimaryKey != expected)
                        {
                            throw new InvalidDataException(
                                $"Concurrent lookup returned a different record for '{expected.TripleId}'.");
                        }

                        if (!triples.ContainsKey(expected.TripleId))
                        {
                            throw new InvalidDataException(
                                $"Concurrent ContainsKey missed '{expected.TripleId}'.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            });

            Assert.True(
                errors.IsEmpty,
                string.Join(Environment.NewLine + Environment.NewLine, errors.Select(error => error.ToString())));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static TripleLikeRow[] CreateConcurrentRows(int count)
    {
        return Enumerable.Range(1, count)
            .Select(index => new TripleLikeRow(
                Guid.Parse($"{index:x8}-0000-0000-0000-000000000000"),
                $"subject:{index}:" + new string((char)('a' + index % 26), 700 + index),
                $"predicate:{index}",
                index % 3,
                $"object:{index}:" + new string((char)('A' + index % 26), 900 + index),
                index % 2 == 0 ? "ru" : "en",
                "http://www.w3.org/2001/XMLSchema#string",
                Guid.Parse($"{index:x8}-1111-2222-3333-444444444444"),
                $"cassette-{index % 7}",
                $"fog/{index}.xml",
                638895210000000000L + index,
                $"subject-predicate:{index}",
                $"predicate-object:{index}"))
            .ToArray();
    }

    private static void BuildAllExternalIndexes(DbSet<TripleLikeRow> triples)
    {
        const string sentinel = "__build_external_index__";
        Assert.Empty(triples.Find(row => row.Subject, sentinel));
        Assert.Empty(triples.Find(row => row.Predicate, sentinel));
        Assert.Empty(triples.Find(row => row.ObjectValue, sentinel));
        Assert.Empty(triples.Find(row => row.SourceCassetteId, sentinel));
        Assert.Empty(triples.Find(row => row.SubjectPredicateKey, sentinel));
        Assert.Empty(triples.Find(row => row.PredicateObjectKey, sentinel));
    }

    private static void AssertExternalLookups(DbSet<TripleLikeRow> triples, TripleLikeRow expected)
    {
        Assert.Equal(expected, Assert.Single(triples.Find(row => row.Subject, expected.Subject)));
        Assert.Equal(expected, Assert.Single(triples.Find(row => row.Predicate, expected.Predicate)));
        Assert.Equal(expected, Assert.Single(triples.Find(row => row.ObjectValue, expected.ObjectValue)));
        Assert.Equal(expected, Assert.Single(triples.Find(row => row.SourceCassetteId, expected.SourceCassetteId)));
        Assert.Equal(expected, Assert.Single(triples.Find(row => row.SubjectPredicateKey, expected.SubjectPredicateKey)));
        Assert.Equal(expected, Assert.Single(triples.Find(row => row.PredicateObjectKey, expected.PredicateObjectKey)));
    }

    private static DbSet<TripleLikeRow> OpenTriples(string root) =>
        new(root, options => options
            .Name("triples")
            .UseKey(row => row.TripleId)
            .UseExternalKey(row => row.Subject)
            .UseExternalKey(row => row.Predicate)
            .UseExternalKey(row => row.ObjectValue)
            .UseExternalKey(row => row.SourceCassetteId)
            .UseExternalKey(row => row.SubjectPredicateKey)
            .UseExternalKey(row => row.PredicateObjectKey));

    private static DbSet<T> Open<T, TKey>(
        string root,
        string name,
        System.Linq.Expressions.Expression<Func<T, TKey>> keySelector)
        where TKey : IComparable<TKey>
    {
        return new DbSet<T>(root, options => options
            .Name(name)
            .UseKey(keySelector));
    }

    private static string CreateRoot() =>
        Path.Combine(Path.GetTempPath(), "PolarDbTests", Guid.NewGuid().ToString("N"));

    private static void DeleteRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
        }
    }

    private sealed record IntEntity(int Id, string Value);
    private sealed record StringEntity(string Id, string Value);
    private sealed record LongEntity(long Id, string Value);
    private sealed record GuidEntity(Guid Id, string Value);

    private sealed record TripleLikeRow(
        Guid TripleId,
        string Subject,
        string Predicate,
        int ObjectKind,
        string ObjectValue,
        string Language,
        string DataType,
        Guid SourceRecordId,
        string SourceCassetteId,
        string SourceFogPath,
        long ModifiedAtUtcTicks,
        string SubjectPredicateKey,
        string PredicateObjectKey);
}
