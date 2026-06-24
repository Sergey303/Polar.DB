using Polar.DB;

namespace GetStarted.SequencesAndStorage;

public static class PersonSchema
{
    public const string Id = nameof(Id);
    public const string Age = nameof(Age);
    public const string Name = nameof(Name);
    public const string Deleted = nameof(Deleted);

    public const int IdIndex = 0;
    public const int AgeIndex = 1;
    public const int NameIndex = 2;
    public const int DeletedIndex = 3;

    public static readonly PType Type = new PTypeRecord(
        new NamedType(Id, new PType(PTypeEnumeration.integer)),
        new NamedType(Age, new PType(PTypeEnumeration.integer)),
        new NamedType(Name, new PType(PTypeEnumeration.sstring)),
        new NamedType(Deleted, new PType(PTypeEnumeration.boolean)));

    public static object Create(int id, int age, string name, bool deleted = false) =>
        new object[] { id, age, name, deleted };

    public static object Tombstone(int id) =>
        Create(id, 0, string.Empty, deleted: true);

    public static int GetId(object record) =>
        (int)GetArray(record)[IdIndex];

    public static int GetAge(object record) =>
        (int)GetArray(record)[AgeIndex];

    public static string GetName(object record) =>
        (string)GetArray(record)[NameIndex];

    public static bool IsDeleted(object record) =>
        (bool)GetArray(record)[DeletedIndex];

    public static bool IsEmpty(object record) =>
        IsDeleted(record);

    public static IComparable PrimaryKey(object record) =>
        GetId(record);

    public static int HashKey(IComparable key) =>
        Convert.ToInt32(key);

    public static IEnumerable<object> Live(IEnumerable<object> records) =>
        records.Where(record => !IsDeleted(record));

    public static string Format(object record) =>
        $"id={GetId(record)}, age={GetAge(record)}, name={GetName(record)}, deleted={IsDeleted(record)}";

    private static object[] GetArray(object record) =>
        (object[])record;
}
