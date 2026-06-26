using Common;
using Polar.DB.Typed;

namespace GetStarted.TypedDbSet;

internal static partial class Program
{
    private sealed record PersonWithTypedGuidKey(Guid Code, string Name, string City);

    private static void RunKeyTypeGuard()
    {
        string rootPath = DbPath.Create();
        using IDbSet<Person> people = OpenPeople(rootPath);

        people.Append(new Person(1, "Алексей Романов", 34, "Москва"));
        Check.Equal("Алексей Романов", people.GetByKey(1).Name, "int key must still work");

        ExpectKeyTypeError(
            () => people.ContainsKey("1"),
            "Id",
            typeof(int),
            typeof(string));

        ExpectKeyTypeError(
            () => people.GetByKey(1L),
            "Id",
            typeof(int),
            typeof(long));

        Guid code = Guid.Parse("3fbb403f-bc6f-4478-b62b-5cb04fd3b211");
        using IDbSet<PersonWithTypedGuidKey> guidPeople = new DbSet<PersonWithTypedGuidKey>(
            DbPath.Create(),
            options => options
                .Key(x => x.Code)
                .ExternalKey(x => x.City));

        guidPeople.Append(new PersonWithTypedGuidKey(code, "Мария Лебедева", "Тверь"));
        Check.Equal("Мария Лебедева", guidPeople.GetByKey(code).Name, "Guid key must still work");

        ExpectKeyTypeError(
            () => guidPeople.ContainsKey(code.ToString("D")),
            "Code",
            typeof(Guid),
            typeof(string));

        Console.WriteLine("Wrong key CLR type fails early with a clear ArgumentException.");
    }

    private static void ExpectKeyTypeError(
        Action action,
        string fieldName,
        Type expectedType,
        Type actualType)
    {
        try
        {
            action();
            throw new InvalidOperationException("Expected ArgumentException was not thrown.");
        }
        catch (ArgumentException ex)
        {
            Check.Equal(
                true,
                ex.Message.Contains(fieldName, StringComparison.Ordinal),
                "Key type error must mention field name");
            Check.Equal(
                true,
                ex.Message.Contains(expectedType.FullName!, StringComparison.Ordinal),
                "Key type error must mention expected CLR type");
            Check.Equal(
                true,
                ex.Message.Contains(actualType.FullName!, StringComparison.Ordinal),
                "Key type error must mention actual CLR type");
        }
    }
}
