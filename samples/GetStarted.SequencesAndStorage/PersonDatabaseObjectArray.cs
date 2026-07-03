using Common;
using Polar.Universal;

namespace GetStarted.SequencesAndStorage;

public static class PersonDatabaseObjectArray
{
    public static void Run()
    {
        string dbPath = DbPath.Create();

        using (var db = PersonSequence.Open(dbPath))
        {
            LoadInitialData(db);
            PrintRows("Initial live records", db.ElementValues());

            object bob = db.GetByKey(2);
            Check.Equal("Борис", ReadName(bob), "Lookup by primary key must find Борис");

            db.AppendElement(PersonSchema.Create(4, 42, "Дарья"));
            db.AppendElement(PersonSchema.Tombstone(2));
            db.AppendElement(PersonSchema.Create(2,  54, "Роман"));
            db.Flush();

            PrintRows("After append, delete and update", db.ElementValues());
            CheckCurrentState(db);
        }

        using (var reopened = PersonSequence.OpenAndRestore(dbPath))
        {
            PrintRows("After reopen and dynamic restore", reopened.ElementValues());
            CheckCurrentState(reopened);
        }
    }

    private static void LoadInitialData(USequence db)
    {
        db.Load(new[]
        {
            PersonSchema.Create(1,  31, "Анна"),
            PersonSchema.Create(2,  52, "Борис"),
            PersonSchema.Create(3,  27, "Клара")
        });

        db.Build();
    }

    private static void CheckCurrentState(USequence db)
    {
        var ids = db.ElementValues()
            .Select(ReadId)
            .OrderBy(id => id)
            .ToArray();

        Check.SequenceEqual(new[] { 1, 2, 3, 4 }, ids, "Live ids must match");

        object person = db.GetByKey(2);
        Check.Equal("Роман", ReadName(person), "Latest id=2 record must be Роман");
        Check.Equal(54, ReadAge(person), "Updated Роман age must match");
    }

    private static int ReadId(object record) =>
        (int)Fields(record)[PersonSchema.IdIndex];

    private static int ReadAge(object record) =>
        (int)Fields(record)[PersonSchema.AgeIndex];

    private static string ReadName(object record) =>
        (string)Fields(record)[PersonSchema.NameIndex];

    private static bool ReadDeleted(object record) =>
        (bool)Fields(record)[PersonSchema.DeletedIndex];

    private static object[] Fields(object record) =>
        (object[])record;

    private static void PrintRows(string title, IEnumerable<object> rows)
    {
        Console.WriteLine(title + ":");
        foreach (object row in rows)
        {
            Console.WriteLine(
                $"  id={ReadId(row)}, " +
                $"age={ReadAge(row)}, " +
                $"name={ReadName(row)}, " +
                $"deleted={ReadDeleted(row)}");
        }
    }
}
