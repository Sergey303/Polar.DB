using Polar.DB.Typed;
using Xunit;

namespace Polar.DB.Tests;

public class GuidLegacyMixedStorageTests
{
    [Fact]
    public void Existing_Legacy_String_Guid_And_Text_Reopens_Without_Data_Rewrite()
    {
        Guid key = Guid.Parse("abcd1234-abcd-1234-abcd-1234abcd1234");
        string root = Path.Combine(Path.GetTempPath(), "PolarDbTests",
            "legacy-mixed-" + Guid.NewGuid().ToString("N"));
        string table = Path.Combine(root, "legacy-mixed");
        Directory.CreateDirectory(table);

        try
        {
            using (var empty = Open(root)) { }

            string schemaPath = Path.Combine(table, "schema.json");
            string schema = File.ReadAllText(schemaPath);
            Assert.Contains("\"PolarType\": \"guid\"", schema);
            File.WriteAllText(schemaPath, schema.Replace(
                "\"PolarType\": \"guid\"",
                "\"PolarType\": \"sstring\"",
                StringComparison.Ordinal));

            string markerPath = Path.Combine(table, "guid-layout.v1");
            File.Delete(markerPath);

            string dataPath = Path.Combine(table, "data-00.bin");
            using (var stream = new FileStream(dataPath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(1L);
                writer.Write(key.ToString("D"));
                writer.Write("legacy-text");
            }

            byte[] original = File.ReadAllBytes(dataPath);
            using (var reopened = Open(root))
                Assert.Equal("legacy-text", reopened.GetByKey(key).Name);

            Assert.Equal(original, File.ReadAllBytes(dataPath));
            Assert.True(File.Exists(markerPath));
            using (var reopenedAgain = Open(root))
                Assert.Equal(key, reopenedAgain.GetByKey(key).Id);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static DbSet<LegacyRow> Open(string root) =>
        new(root, options => options.Name("legacy-mixed").UseKey(row => row.Id));

    public sealed record LegacyRow(Guid Id, string Name);
}
