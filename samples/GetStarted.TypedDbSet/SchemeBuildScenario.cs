using Common;
using Polar.DB.Typed;

namespace GetStarted.TypedDbSet;

internal static partial class Program
{
    private sealed record PersonWithLongId(long Id, string Name);

    private sealed record PersonWithGuidId(Guid Code, string Name);

    private sealed record PersonWithDateTime(
        int Id,
        string Name,
        DateTime CreatedAt);

    private sealed class PersonWithoutConstructor
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    private static void RunSchemeBuildGuard()
    {
        ExpectBuildError<Person>("Primary key is required");
        ExpectLongKeySupport();
        ExpectGuidKeySupport();
        ExpectBuildError<PersonWithDateTime>(
            options => options.Key(x => x.Id),
            "CreatedAt",
            "System.DateTime");
        ExpectBuildError<PersonWithoutConstructor>(
            options => options.Key(x => x.Id),
            "public constructor with fields");

        Console.WriteLine("Scheme build guard requires explicit key and supports long/Guid keys.");
    }

    private static void ExpectLongKeySupport()
    {
        string rootPath = DbPath.Create();
        using IDbSet<PersonWithLongId> people = new DbSet<PersonWithLongId>(
            rootPath,
            options => options.Key(x => x.Id));

        long id = 9_000_000_000L;
        people.Append(new PersonWithLongId(id, "Леонид Серов"));
        Check.Equal("Леонид Серов", people.GetByKey(id).Name, "long key must work through configured Key");
    }

    private static void ExpectGuidKeySupport()
    {
        string rootPath = DbPath.Create();
        using IDbSet<PersonWithGuidId> people = new DbSet<PersonWithGuidId>(
            rootPath,
            options => options.Key(x => x.Code));

        Guid code = Guid.Parse("9f9ca22f-9136-4e91-bceb-8f8c3f4be001");
        people.Append(new PersonWithGuidId(code, "Наталья Бородина"));
        Check.Equal("Наталья Бородина", people.GetByKey(code).Name, "Guid key must work through configured Key");
    }

    private static void ExpectBuildError<T>(params string[] expectedFragments) =>
        ExpectBuildError<T>(_ => { }, expectedFragments);

    private static void ExpectBuildError<T>(
        Action<DbSetOptions<T>> configure,
        params string[] expectedFragments)
    {
        string rootPath = DbPath.Create();
        try
        {
            using var ignored = new DbSet<T>(rootPath, configure);
            throw new InvalidOperationException(
                $"Expected {nameof(SchemeBuildException)} was not thrown for {typeof(T).Name}.");
        }
        catch (SchemeBuildException ex)
        {
            foreach (string fragment in expectedFragments)
            {
                Check.Equal(
                    true,
                    ex.Message.Contains(fragment, StringComparison.Ordinal),
                    $"Scheme build error must mention '{fragment}' for {typeof(T).Name}");
            }

            Check.Equal(
                typeof(T),
                ex.RecordType,
                "Scheme build error must keep the failing record type");
        }
    }
}
