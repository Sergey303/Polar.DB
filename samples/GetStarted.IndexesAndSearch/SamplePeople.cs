using System.Text;
using Polar.DB;

namespace GetStarted.IndexesAndSearch;

internal static class SamplePeople
{
    public static readonly PTypeRecord RecordType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("city", new PType(PTypeEnumeration.sstring)),
        new NamedType("age", new PType(PTypeEnumeration.integer)),
        new NamedType("tags", new PTypeSequence(new PType(PTypeEnumeration.sstring))),
        new NamedType("skills", new PTypeSequence(new PType(PTypeEnumeration.sstring))),
        new NamedType("notes", new PType(PTypeEnumeration.sstring)));

    public static readonly RecordAccessor Accessor = new(RecordType);

    public static object[] Person(
        int id,
        string name,
        string city,
        int age,
        IEnumerable<string> tags,
        IEnumerable<string> skills,
        string notes)
    {
        return new object[]
        {
            id,
            name,
            city,
            age,
            ToObjectArray(tags),
            ToObjectArray(skills),
            notes
        };
    }

    public static object[] AgeSample(int age)
    {
        return new object[]
        {
            0,
            string.Empty,
            string.Empty,
            age,
            Array.Empty<object>(),
            Array.Empty<object>(),
            string.Empty
        };
    }

    public static IReadOnlyList<object> BaseDataset() =>
        new object[]
        {
            Person(1, "Alice Baker", "Berlin", 30, new[] { "db", "ml" }, new[] { "csharp", "sql" }, "Builds search and indexing demos."),
            Person(2, "Bob Chen", "Helsinki", 34, new[] { "ops", "storage" }, new[] { "go", "linux" }, "Operates storage pipelines."),
            Person(3, "Carla Diaz", "Berlin", 30, new[] { "ux", "graph" }, new[] { "figma", "analytics" }, "Designs graph exploration flows."),
            Person(4, "Dmitry Ivanov", "Riga", 28, new[] { "db", "graph" }, new[] { "csharp", "dotnet" }, "Writes query planner notes."),
            Person(5, "Elena Smirnova", "Tallinn", 41, new[] { "archive", "storage" }, new[] { "sql", "etl" }, "Maintains archival search processes.")
        };

    public static object[] AppendedForPrimaryKey() =>
        Person(6, "Farah Noor", "Berlin", 33, new[] { "graph", "ml" }, new[] { "python", "analytics" }, "Adds semantic search experiments.");

    public static object[] AppendedForAge() =>
        Person(7, "George Mills", "Oslo", 30, new[] { "ops", "backup" }, new[] { "powershell", "sql" }, "Handles 30-day retention tasks.");

    public static object[] AppendedForTextSearch() =>
        Person(8, "Hanna Petrov", "Vilnius", 29, new[] { "analysis", "graph" }, new[] { "python", "search" }, "Documents analytics recipes for graph search.");

    public static object[] AppendedForTagSearch() =>
        Person(9, "Ivan Orlov", "Warsaw", 36, new[] { "storage", "api" }, new[] { "java", "kafka" }, "Extends storage ingestion services.");

    public static object[] AppendedForSkillSearch() =>
        Person(10, "Julia Novak", "Prague", 31, new[] { "db", "ui" }, new[] { "CSharp", "Blazor" }, "Bridges backend indexing and UI demos.");

    public static int Id(object record) => Accessor.Get<int>(record, "id");
    public static string Name(object record) => Accessor.Get<string>(record, "name");
    public static string City(object record) => Accessor.Get<string>(record, "city");
    public static int Age(object record) => Accessor.Get<int>(record, "age");
    public static string Notes(object record) => Accessor.Get<string>(record, "notes");

    public static string[] Tags(object record) => ((object[])Accessor.Get(record, "tags")).Cast<string>().ToArray();
    public static string[] Skills(object record) => ((object[])Accessor.Get(record, "skills")).Cast<string>().ToArray();

    public static IEnumerable<IComparable> TagsAsComparables(object record) => Tags(record);
    public static IEnumerable<IComparable> SkillsAsComparables(object record) => Skills(record);

    public static IEnumerable<string> SearchTokens(object record)
    {
        foreach (var token in Tokenize(Name(record)))
            yield return token;

        foreach (var token in Tokenize(City(record)))
            yield return token;

        foreach (var token in Tokenize(Notes(record)))
            yield return token;

        foreach (var tag in Tags(record))
            foreach (var token in Tokenize(tag))
                yield return token;

        foreach (var skill in Skills(record))
            foreach (var token in Tokenize(skill))
                yield return token;
    }

    public static string Describe(object record)
    {
        return $"#{Id(record)} {Name(record)} | city={City(record)} | age={Age(record)} | tags=[{string.Join(", ", Tags(record))}] | skills=[{string.Join(", ", Skills(record))}]";
    }

    public static IEnumerable<object> DistinctById(IEnumerable<object> records)
    {
        return records.GroupBy(Id).Select(group => group.First());
    }

    private static object[] ToObjectArray(IEnumerable<string> values)
    {
        return values.Cast<object>().ToArray();
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var buffer = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer.Append(ch);
            }
            else if (buffer.Length > 0)
            {
                yield return buffer.ToString();
                buffer.Clear();
            }
        }

        if (buffer.Length > 0)
        {
            yield return buffer.ToString();
        }
    }
}
