using Common;
using Polar.DB;
using Polar.Universal;

namespace GetStarted.SequencesAndStorage;

public static class PersonDatabaseRecordAccessor
{
    private static readonly RecordAccessor Accessor = new((PTypeRecord)PersonSchema.Type);

    public static void Run()
    {
        string dbPath = DbPath.Create();

        using (var db = PersonSequence.Open(dbPath))
        {
            LoadInitialData(db);
            PrintRows("Initial live records", db.ElementValues());

            object bob = db.GetByKey(2);
            Check.Equal("Bob", ReadName(bob), "Lookup by primary key must find Bob");

            db.AppendElement(CreatePerson(4, 42, "Dora"));
            db.AppendElement(CreateTombstone(2));
            db.AppendElement(CreatePerson(2, 54, "Robert"));
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
            CreatePerson(1, 31, "Alice"),
            CreatePerson(2, 52, "Bob"),
            CreatePerson(3, 27, "Clara")
        });

        db.Build();
    }

    private static object CreatePerson(int id, int age, string name, bool deleted = false)
    {
        object record = Accessor.CreateRecord();
        Accessor.Set(record, PersonSchema.Id, id);
        Accessor.Set(record, PersonSchema.Age, age);
        Accessor.Set(record, PersonSchema.Name, name);
        Accessor.Set(record, PersonSchema.Deleted, deleted);
        return record;
    }

    private static object CreateTombstone(int id) =>
        CreatePerson(id, 0, string.Empty, deleted: true);

    private static void CheckCurrentState(USequence db)
    {
        var ids = db.ElementValues()
            .Select(ReadId)
            .OrderBy(id => id)
            .ToArray();

        Check.SequenceEqual(new[] { 1, 2, 3, 4 }, ids, "Live ids must match");

        object person = db.GetByKey(2);
        Check.Equal("Robert", ReadName(person), "Latest id=2 record must be Robert");
        Check.Equal(54, ReadAge(person), "Updated Robert age must match");
    }

    private static int ReadId(object record) =>
        Accessor.Get<int>(record, PersonSchema.Id);

    private static int ReadAge(object record) =>
        Accessor.Get<int>(record, PersonSchema.Age);

    private static string ReadName(object record) =>
        Accessor.Get<string>(record, PersonSchema.Name);

    private static bool ReadDeleted(object record) =>
        Accessor.Get<bool>(record, PersonSchema.Deleted);

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
