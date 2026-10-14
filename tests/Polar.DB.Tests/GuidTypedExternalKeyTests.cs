using Polar.DB.Typed;
using Xunit;

namespace Polar.DB.Tests;

public class GuidTypedExternalKeyTests
{
    private static readonly Guid First = Guid.Parse("12345678-9abc-def0-1234-567890abcdef");
    private static readonly Guid Second = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void Typed_Guid_External_Key_Builds_And_Reopens_As_Native()
    {
        string root = NewRoot();
        try
        {
            using (var set = Open(root))
            {
                set.Append(new ExternalRow(1, First, "one"));
                set.Append(new ExternalRow(2, Second, "two"));
                Assert.Equal(1, Assert.Single(set.Find(row => row.ExternalId, First)).Id);
                Assert.Equal(2, Assert.Single(set.Find(row => row.ExternalId, Second)).Id);
            }

            string table = Path.Combine(root, "typed-guid-external");
            Assert.Equal(40L, new FileInfo(Path.Combine(table, "external-ExternalId-00.bin")).Length);

            using var reopened = Open(root);
            Assert.Equal(1, Assert.Single(reopened.Find(row => row.ExternalId, First)).Id);
            Assert.Equal(2, Assert.Single(reopened.Find(row => row.ExternalId, Second)).Id);
        }
        finally
        {
            Clean(root);
        }
    }

    [Fact]
    public void Explicit_Incompatible_Polar_Field_Type_Is_Rejected()
    {
        string root = NewRoot();
        try
        {
            using (var set = Open(root))
                set.Append(new ExternalRow(1, First, "one"));

            string schemaPath = Path.Combine(root, "typed-guid-external", "schema.json");
            string schema = File.ReadAllText(schemaPath);
            Assert.Contains("\"PolarType\": \"guid\"", schema);
            File.WriteAllText(schemaPath, schema.Replace(
                "\"PolarType\": \"guid\"",
                "\"PolarType\": \"integer\"",
                StringComparison.Ordinal));

            Assert.Throws<SchemeCompatibilityException>(() => Open(root));
        }
        finally
        {
            Clean(root);
        }
    }

    [Fact]
    public void Explicit_Native_Guid_Schema_Rejects_Legacy_String_Physical_Data()
    {
        string root = NewRoot();
        const string name = "explicit-native-guid";
        Guid id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        try
        {
            using (var empty = new DbSet<GuidIntRow>(root, options => options
                .Name(name).UseKey(row => row.Id))) { }

            string dataPath = Path.Combine(root, name, "data-00.bin");
            using (var stream = new FileStream(dataPath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(1L);
                writer.Write(id.ToString("D")); // Old, variable-length Guid layout.
                writer.Write(9);
            }

            Assert.Throws<SchemeCompatibilityException>(() =>
                new DbSet<GuidIntRow>(root, options => options
                    .Name(name).UseKey(row => row.Id)));
        }
        finally
        {
            Clean(root);
        }
    }

    [Fact]
    public void Explicit_Schema_Field_Order_Mismatch_Is_Rejected()
    {
        string root = NewRoot();
        try
        {
            using (var set = Open(root))
                set.Append(new ExternalRow(1, First, "one"));
            string path = Path.Combine(root, "typed-guid-external", "schema.json");
            string schema = File.ReadAllText(path);
            Assert.Contains("\"Order\": 0", schema);
            File.WriteAllText(path, schema.Replace("\"Order\": 0", "\"Order\": 99", StringComparison.Ordinal));
            Assert.Throws<SchemeCompatibilityException>(() => Open(root));
        }
        finally
        {
            Clean(root);
        }
    }

    private static DbSet<ExternalRow> Open(string root) =>
        new(root, options => options
            .Name("typed-guid-external")
            .UseKey(row => row.Id)
            .UseExternalKey(row => row.ExternalId));

    private static string NewRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "PolarDbTests",
            "typed-guid-external-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Clean(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    public sealed record ExternalRow(int Id, Guid ExternalId, string Name);
    public sealed record GuidIntRow(Guid Id, int Value);
}
