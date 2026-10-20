using Polar.DB.Typed;
using Xunit;

namespace Polar.DB.Tests;

public sealed class GuidTypedLifecycleMatrixTests
{
    private static readonly Guid First = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
    private static readonly Guid Second = Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100");
    private static readonly Guid Payload = Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00");

    [Fact]
    public void Plain_And_External_Guid_Fields_Survive_Duplicate_Rejection_And_Reopening()
    {
        string root = Path.Combine(Path.GetTempPath(), "polar-guid-typed-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using (var set = Open(root))
            {
                set.Append(new Row(1, Payload, First, "original"));
                set.Append(new Row(2, Guid.Empty, Second, "other"));

                Assert.Equal(Payload, set.GetByKey(1).PayloadGuid);
                Assert.Equal("original", Assert.Single(set.Find(row => row.ExternalGuid, First)).Name);
                Assert.Equal(Guid.Empty, set.GetByKey(2).PayloadGuid);

                Assert.Throws<InvalidOperationException>(() =>
                    set.Append(new Row(1, Payload, Second, "replacement")));
                Assert.Equal("original", Assert.Single(set.Find(row => row.ExternalGuid, First)).Name);
                Assert.Equal(2, Assert.Single(set.Find(row => row.ExternalGuid, Second)).Id);
            }

            using (var reopened = Open(root))
            {
                Assert.Equal(new Row(1, Payload, First, "original"), reopened.GetByKey(1));
                Assert.Equal(Guid.Empty, reopened.GetByKey(2).PayloadGuid);
                Assert.Equal(1, Assert.Single(reopened.Find(row => row.ExternalGuid, First)).Id);
                Assert.Equal(2, Assert.Single(reopened.Find(row => row.ExternalGuid, Second)).Id);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static DbSet<Row> Open(string root) => new(root, options =>
        options.Name("guid-typed-lifecycle")
            .UseKey(row => row.Id)
            .UseExternalKey(row => row.ExternalGuid));

    public sealed record Row(int Id, Guid PayloadGuid, Guid ExternalGuid, string Name);
}
