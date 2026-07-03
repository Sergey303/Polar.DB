using Polar.DB.Typed;

namespace GetStarted.TypedDbSet;

public sealed record Person(int Id, string Name, int Age, string City);

internal static partial class Program
{
    private static string PersonStorageName => typeof(Person).FullName!.Replace('+', '.');

    private static IDbSet<Person> OpenPeople(string rootPath) => new DbSet<Person>(
        rootPath,
        options => options
            .UseKey(x => x.Id)
            .UseExternalKey(x => x.Age)
            .UseExternalKey(x => x.City));

    private static string Format(Person person) =>
        $"#{person.Id} {person.Name} | age={person.Age} | city={person.City}";
}
