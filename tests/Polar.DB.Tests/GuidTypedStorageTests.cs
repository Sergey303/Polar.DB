using System.Text.Json;
using Polar.DB.Typed;
using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public class GuidTypedStorageTests
{
    [Fact]
    public void New_DbSet_Guid_Field_Uses_Native_Storage_And_Reopens()
    {
        string root = CreateRoot();
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        try
        {
            using (var set = Open(root, "native-guid"))
                set.Append(new GuidIntEntity(id, 7));

            string tablePath = Path.Combine(root, "native-guid");
            string schema = File.ReadAllText(Path.Combine(tablePath, "schema.json"));

            Assert.Contains(""PolarType": "guid"", schema);
            Assert.Equal(28L, new FileInfo(Path.Combine(tablePath, "data-00.bin")).Length);

            using var reopened = Open(root, "native-guid");
            Assert.Equal(new GuidIntEntity(id, 7), reopened.GetByKey(id));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void Legacy_Guid_String_Table_Remains_Readable_And_Upgrades_Schema_Metadata()
    {
        string root = CreateRoot();
        const string name = "legacy-guid";
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        string tablePath = Path.Combine(root, name);

        try
        {
            Directory.CreateDirectory(tablePath);
            SeedLegacySchema(tablePath, name);
            SeedLegacyData(tablePath, id, 9);

            long legacyLength = new FileInfo(Path.Combine(tablePath, "data-00.bin")).Length;
            using (var set = Open(root, name))
                Assert.Equal(new GuidIntEntity(id, 9), set.GetByKey(id));

            string schema = File.ReadAllText(Path.Combine(tablePath, "schema.json"));
            Assert.Contains(""PolarType": "sstring"", schema);
            Assert.Equal(legacyLength, new FileInfo(Path.Combine(tablePath, "data-00.bin")).Length);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static DbSet<GuidIntEntity> Open(string root, string name) =>
        new(root, options => options
            .Name(name)
            .UseKey(item => item.Id));

    private static void SeedLegacySchema(string tablePath, string storageName)
    {
        string typeName = typeof(GuidIntEntity).FullName ?? typeof(GuidIntEntity).Name;
        var snapshot = new
        {
            TypeName = typeName,
            StorageName = storageName,
            KeyName = "Id",
            Fields = new object[]
            {
                new { Name = "Id", ClrType = typeof(Guid).FullName!, PolarType = "Polar.DB.PType", Order = 0, Key = true },
                new { Name = "Value", ClrType = typeof(int).FullName!, PolarType = "Polar.DB.PType", Order = 1, Key = false }
            }
        };

        File.WriteAllText(
            Path.Combine(tablePath, "schema.json"),
            JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void SeedLegacyData(string tablePath, Guid id, int value)
    {
        int fileNumber = 0;
        Stream StreamGen()
        {
            string path = Path.Combine(tablePath, $"data-{fileNumber++:00}.bin");
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        var recordType = new PTypeRecord(
            new NamedType("Id", new PType(PTypeEnumeration.sstring)),
            new NamedType("Value", new PType(PTypeEnumeration.integer)));

        using var sequence = new USequence(
            recordType,
            Path.Combine(tablePath, "state.bin"),
            StreamGen,
            _ => false);

        sequence.SetPrimaryKey<string>(
            row => (string)((object[])row)[0],
            StableStringHash);
        sequence.AppendElement(new object[] { id.ToString("D"), value });
        sequence.Build();
    }

    private static int StableStringHash(string text)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (char ch in text)
            {
                hash ^= ch;
                hash *= 16777619;
            }

            return hash;
        }
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

    public sealed record GuidIntEntity(Guid Id, int Value);
}
