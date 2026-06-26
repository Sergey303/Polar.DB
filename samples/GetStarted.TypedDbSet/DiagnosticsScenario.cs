using Common;
using Polar.DB.Typed;

namespace GetStarted.TypedDbSet;

internal static partial class Program
{
    private static void RunDiagnostics()
    {
        string rootPath = DbPath.Create();
        using IDbSet<Person> people = new DbSet<Person>(
            rootPath,
            options => options
                .UseKey(x => x.Id)
                .UseExternalKey(x => x.Age)
                .UseExternalKey(x => x.City));

        people.AddRange(new[]
        {
            new Person(90, "Артём Фёдоров", 32, "Москва"),
            new Person(91, "Валерия Котова", 36, "Калуга")
        });

        DbSetDiagnostics before = people.Diagnostics();
        Check.Equal(PersonStorageName, before.StorageName, "Diagnostics must show storage name");
        Check.Equal(true, Directory.Exists(before.StoragePath), "Diagnostics must show existing storage path");
        Check.Equal("Id", before.KeyName, "Diagnostics must show configured primary key name");
        Check.Equal(typeof(int).FullName!, before.KeyClrType, "Diagnostics must show configured primary key CLR type");
        Check.SequenceEqual(new[] { "Id", "Name", "Age", "City" }, before.FieldNames,
            "Diagnostics must show fields in record order");
        Check.SequenceEqual(new[] { "Age", "City" }, before.ExternalKeyNames,
            "Diagnostics must show configured external keys in record order");
        Check.Equal(2, before.Count, "Diagnostics Count must follow PrimaryKeyMap");
        Check.Equal(2, before.CollectedAppendCount, "Diagnostics must show append collector size");
        Check.Equal(0, before.BuiltExternalKeyNames.Count,
            "Diagnostics must show that no secondary map is built before first Find");

        _ = people.Find(x => x.City, "Москва");
        DbSetDiagnostics after = people.Diagnostics();
        Check.SequenceEqual(new[] { "City" }, after.BuiltExternalKeyNames,
            "Diagnostics must show built external-key maps after Find");

        Console.WriteLine("Diagnostics show explicit key, configured external keys and built maps:");
        Console.WriteLine($"  storage: {after.StorageName}");
        Console.WriteLine($"  key: {after.KeyName} ({after.KeyClrType})");
        Console.WriteLine($"  external keys: {string.Join(", ", after.ExternalKeyNames)}");
        Console.WriteLine($"  built maps: {string.Join(", ", after.BuiltExternalKeyNames)}");
        Console.WriteLine($"  count: {after.Count}");
    }
}
