using Common;
using Polar.DB.Typed;

namespace GetStarted.TypedDbSet;

internal static partial class Program
{
    private static void RunSchemaFileSafety()
    {
        string rootPath = DbPath.Create();
        using (IDbSet<Person> people = OpenPeople(rootPath))
        {
            people.Append(new Person(300, "Алла Миронова", 52, "Москва"));
        }

        string tablePath = Path.Combine(rootPath, PersonStorageName);
        string schemaPath = Path.Combine(tablePath, "schema.json");
        Check.Equal(true, File.Exists(schemaPath), "DbSet must create schema.json atomically");
        Check.Equal(0, Directory.GetFiles(tablePath, "*.tmp").Length,
            "Atomic schema write must not leave temporary files after success");

        File.WriteAllText(schemaPath, "{ invalid json", System.Text.Encoding.UTF8);

        SchemeCompatibilityException corruptedSchema = ExpectSchemeError(() =>
        {
            using var _ = OpenPeople(rootPath);
        });

        Check.Equal(true, corruptedSchema.Detail.Contains("not valid JSON"),
            "Corrupted schema.json must fail with a clear compatibility error");

        Console.WriteLine("Schema file safety rejects corrupted schema.json:");
        Console.WriteLine($"  {corruptedSchema.Detail}");
    }
}
