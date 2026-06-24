using Polar.DB;

namespace GetStarted.StructuresAndSerialization;

internal static class PersonSchema
{
    public const string Id = nameof(Id);
    public const string Name = nameof(Name);
    public const string Age = nameof(Age);

    public const int IdIndex = 0;
    public const int NameIndex = 1;
    public const int AgeIndex = 2;

    public static readonly PTypeRecord Type = new(
        new NamedType(Id, new PType(PTypeEnumeration.integer)),
        new NamedType(Name, new PType(PTypeEnumeration.sstring)),
        new NamedType(Age, new PType(PTypeEnumeration.integer)));

    public static object Create(int id, string name, int age) =>
        new object[] { id, name, age };

    public static int GetId(object record) =>
        (int)GetArray(record)[IdIndex];

    public static string GetName(object record) =>
        (string)GetArray(record)[NameIndex];

    public static int GetAge(object record) =>
        (int)GetArray(record)[AgeIndex];

    public static string Format(object record) =>
        $"#{GetId(record)} {GetName(record)} | age={GetAge(record)}";

    private static object[] GetArray(object record) =>
        (object[])record;
}