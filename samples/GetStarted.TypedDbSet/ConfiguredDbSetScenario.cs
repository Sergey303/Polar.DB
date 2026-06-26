using Common;
using Polar.DB.Typed;

namespace GetStarted.TypedDbSet;

internal static partial class Program
{
    private sealed record PersonByCode(string Code, string Name, int Age, string City);

    private sealed record PersonByLongCode(long Code, string Name, string City);

    private sealed record PersonByGuidCode(Guid Code, string Name, string City);

    private static void RunConstructorConfig()
    {
        string rootPath = DbPath.Create();

        using (IDbSet<PersonByCode> people = new DbSet<PersonByCode>(
            rootPath,
            options => options
                .Name("people")
                .UseKey(x => x.Code)
                .UseExternalKey(x => x.Age)
                .UseExternalKey(x => x.City)))
        {
            people.AddRange(new[]
            {
                new PersonByCode("P-100", "Лидия Королёва", 33, "Москва"),
                new PersonByCode("P-101", "Михаил Тихонов", 46, "Киров")
            });

            Check.Equal(2, people.Count, "Configured DbSet must count records");
            Check.Equal(true, people.ContainsKey("P-100"), "Configured string key must support ContainsKey");
            Check.Equal("Михаил Тихонов", people.GetByKey("P-101").Name,
                "Configured key selector must be used by GetByKey");
            Check.SequenceEqual(new[] { "P-100" }, people.Find(x => x.City, "Москва").Select(x => x.Code),
                "Configured external key must be used by Find");
        }

        string schemaPath = Path.Combine(rootPath, "people", "schema.json");
        Check.Equal(true, File.Exists(schemaPath), "Configured storage name must choose table folder");

        using IDbSet<PersonByCode> reopened = new DbSet<PersonByCode>(
            rootPath,
            options => options
                .Name("people")
                .UseKey(x => x.Code)
                .UseExternalKey(x => x.Age)
                .UseExternalKey(x => x.City));

        Check.Equal(2, reopened.Count, "Configured DbSet must rebuild Count after reopen");
        Check.Equal("Лидия Королёва", reopened.GetByKey("P-100").Name,
            "Configured key selector must survive reopen");

        ExpectLongConfiguredKey();
        ExpectGuidConfiguredKey();
        ExpectUnconfiguredExternalKeyError();

        Console.WriteLine("Constructor config uses explicit name, key and external keys:");
        foreach (PersonByCode person in reopened.All())
            Console.WriteLine($"  {person.Code} {person.Name} | age={person.Age} | city={person.City}");
    }

    private static void ExpectLongConfiguredKey()
    {
        string rootPath = DbPath.Create();
        using IDbSet<PersonByLongCode> people = new DbSet<PersonByLongCode>(
            rootPath,
            options => options
                .UseKey(x => x.Code)
                .UseExternalKey(x => x.City));

        long code = 9_000_000_001L;
        people.Append(new PersonByLongCode(code, "Олег Зимин", "Псков"));
        Check.Equal("Олег Зимин", people.GetByKey(code).Name, "long configured key must work");
        Check.SequenceEqual(new[] { code }, people.Find(x => x.City, "Псков").Select(x => x.Code),
            "external key must work with long primary key records");
    }

    private static void ExpectGuidConfiguredKey()
    {
        string rootPath = DbPath.Create();
        using IDbSet<PersonByGuidCode> people = new DbSet<PersonByGuidCode>(
            rootPath,
            options => options
                .UseKey(x => x.Code)
                .UseExternalKey(x => x.City));

        Guid code = Guid.Parse("4d937aea-0f43-47a1-9ce2-945729c7f002");
        people.Append(new PersonByGuidCode(code, "Полина Рябова", "Тверь"));
        Check.Equal("Полина Рябова", people.GetByKey(code).Name, "Guid configured key must work");
        Check.SequenceEqual(new[] { code }, people.Find(x => x.City, "Тверь").Select(x => x.Code),
            "external key must work with Guid primary key records");
    }

    private static void ExpectUnconfiguredExternalKeyError()
    {
        using IDbSet<PersonByCode> people = new DbSet<PersonByCode>(
            DbPath.Create(),
            options => options
                .Name("people-by-code-no-ext")
                .UseKey(x => x.Code));

        people.Append(new PersonByCode("P-200", "Роман Нестеров", 37, "Самара"));

        bool failed = false;
        try
        {
            _ = people.Find(x => x.City, "Самара");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ExternalKey", StringComparison.Ordinal))
        {
            failed = true;
        }

        Check.Equal(true, failed, "Find must require options.ExternalKey for non-primary field lookup");
    }
}
