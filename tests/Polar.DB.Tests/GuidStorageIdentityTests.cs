using Polar.DB.Typed;
using Xunit;

namespace Polar.DB.Tests;

public class GuidStorageIdentityTests
{
    private static readonly Guid Example = Guid.Parse("12345678-9abc-def0-1234-567890abcdef");

    [Fact]
    public void New_Mixed_Guid_Table_Has_Stable_Layout_Identity()
    {
        string root = NewRoot();
        try
        {
            using (var set = Open(root))
                set.Append(new Row(1, Example, "text"));

            string marker = Marker(root);
            Assert.True(File.Exists(marker));
            string original = File.ReadAllText(marker);
            Assert.StartsWith("PDB-GUID-LAYOUT-V1:", original);

            using (var reopened = Open(root))
                Assert.Equal(Example, reopened.GetByKey(1).ExternalId);

            Assert.Equal(original, File.ReadAllText(marker));
        }
        finally { Clean(root); }
    }

    [Fact]
    public void Early_Native_Mixed_Guid_Table_Without_Marker_Is_Checked_Once()
    {
        string root = NewRoot();
        try
        {
            using (var set = Open(root))
                set.Append(new Row(1, Example, "text"));

            File.Delete(Marker(root));
            using (var reopened = Open(root))
                Assert.Equal("text", reopened.GetByKey(1).Name);

            Assert.True(File.Exists(Marker(root)));
        }
        finally { Clean(root); }
    }

    [Fact]
    public void Mixed_Guid_Table_With_Wrong_Schema_Marker_Is_Rejected()
    {
        string root = NewRoot();
        try
        {
            using (var set = Open(root))
                set.Append(new Row(1, Example, "text"));

            File.WriteAllText(Marker(root), "PDB-GUID-LAYOUT-V1:WRONG");
            Assert.Throws<SchemeCompatibilityException>(() => Open(root));
        }
        finally { Clean(root); }
    }

    [Fact]
    public void Legacy_Guid_String_Bytes_Cannot_Be_Opened_As_Native_Mixed_Fields()
    {
        string root = NewRoot();
        try
        {
            using (var set = Open(root)) { }

            string dataPath = Path.Combine(root, "mixed-guid", "data-00.bin");
            using (var stream = new FileStream(dataPath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(1L);
                writer.Write(1);
                writer.Write(Example.ToString("D"));
                writer.Write("text");
            }

            File.Delete(Marker(root));
            byte[] before = File.ReadAllBytes(dataPath);
            Assert.Throws<SchemeCompatibilityException>(() => Open(root));
            Assert.Equal(before, File.ReadAllBytes(dataPath));
            Assert.False(File.Exists(Marker(root)));
        }
        finally { Clean(root); }
    }

    [Fact]
    public void Truncated_Mixed_Guid_Header_Cannot_Create_A_New_Marker()
    {
        string root = NewRoot();
        try
        {
            using (var set = Open(root)) { }
            string dataPath = Path.Combine(root, "mixed-guid", "data-00.bin");
            File.WriteAllBytes(dataPath, new byte[] { 1, 2, 3 });
            File.Delete(Marker(root));
            Assert.Throws<SchemeCompatibilityException>(() => Open(root));
            Assert.False(File.Exists(Marker(root)));
        }
        finally { Clean(root); }
    }

    private static DbSet<Row> Open(string root) =>
        new(root, options => options.Name("mixed-guid")
            .UseKey(item => item.Id).UseExternalKey(item => item.ExternalId));

    private static string Marker(string root) =>
        Path.Combine(root, "mixed-guid", "guid-layout.v1");

    private static string NewRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "PolarDbTests",
            "guid-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Clean(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    public sealed record Row(int Id, Guid ExternalId, string Name);
}
