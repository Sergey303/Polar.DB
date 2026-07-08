using Polar.DB.Typed;
using Xunit;

namespace Polar.DB.Tests;

public class DbSetPrimaryKeyConfigurationTests
{
    [Fact]
    public void DbSet_PrimaryKeys_Reopen_For_Int_String_Long_And_Guid()
    {
        string root = Path.Combine(Path.GetTempPath(), "PolarDbTests", Guid.NewGuid().ToString("N"));
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
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch { }
        }
    }

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

    private sealed record IntEntity(int Id, string Value);
    private sealed record StringEntity(string Id, string Value);
    private sealed record LongEntity(long Id, string Value);
    private sealed record GuidEntity(Guid Id, string Value);
}
