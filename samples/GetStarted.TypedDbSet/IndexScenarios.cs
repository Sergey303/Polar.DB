using Common;
using Polar.DB.Typed;

namespace GetStarted.TypedDbSet;

internal static partial class Program
{
    private static void RunExternalKeyMap()
    {
        string rootPath = DbPath.Create();
        using IDbSet<Person> people = OpenPeople(rootPath);

        people.Append(new Person(30, "Мария Васильева", 35, "Москва"));
        people.Append(new Person(31, "Николай Фёдоров", 41, "Киров"));
        people.Append(new Person(32, "Ольга Романова", 35, "Москва"));

        IReadOnlyList<Person> age35 = people.Find(x => x.Age, 35);
        Check.SequenceEqual(new[] { 30, 32 }, age35.Select(x => x.Id), "First Find must build configured external key index");

        people.Append(new Person(33, "Павел Захаров", 35, "Тверь"));

        IReadOnlyList<Person> age35AfterAppend = people.Find(x => x.Age, 35);
        Check.SequenceEqual(new[] { 30, 32, 33 }, age35AfterAppend.Select(x => x.Id),
            "Append must update already built external key index");

        IReadOnlyList<Person> fromMoscow = people.Find(x => x.City, "Москва");
        Check.SequenceEqual(new[] { 30, 32 }, fromMoscow.Select(x => x.Id),
            "Configured external key must work for string fields too");

        Console.WriteLine("External key index uses InMemoryExternalKeyMap for typed Find and append updates:");
        foreach (Person person in age35AfterAppend)
            Console.WriteLine($"  {Format(person)}");
    }

    private static void RunAppendMutationSafety()
    {
        string rootPath = DbPath.Create();
        using IDbSet<Person> people = OpenPeople(rootPath);

        people.Append(new Person(40, "Антон Макаров", 50, "Москва"));
        people.Append(new Person(41, "Валерия Никифорова", 50, "Саратов"));

        Check.SequenceEqual(new[] { 40, 41 }, people.Find(x => x.Age, 50).Select(x => x.Id),
            "Age external key index must be built before duplicate write check");
        Check.SequenceEqual(new[] { 40 }, people.Find(x => x.City, "Москва").Select(x => x.Id),
            "City external key index must be built before duplicate write check");

        bool duplicateRejected = false;
        try
        {
            people.Append(new Person(40, "Юлия Беляева", 51, "Москва"));
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }

        Check.Equal(true, duplicateRejected, "Duplicate append must fail before physical append");
        Check.SequenceEqual(new[] { 40, 41 }, people.All().Select(x => x.Id),
            "Duplicate append must not change stored records");
        Check.SequenceEqual(new[] { 40, 41 }, people.Find(x => x.Age, 50).Select(x => x.Id),
            "Duplicate append must not change built age external key index");
        Check.SequenceEqual(new[] { 40 }, people.Find(x => x.City, "Москва").Select(x => x.Id),
            "Duplicate append must not change built city external key index");

        people.Append(new Person(42, "Кирилл Егоров", 50, "Москва"));

        Check.SequenceEqual(new[] { 40, 41, 42 }, people.Find(x => x.Age, 50).Select(x => x.Id),
            "Valid append must update built age external key index");
        Check.SequenceEqual(new[] { 40, 42 }, people.Find(x => x.City, "Москва").Select(x => x.Id),
            "Valid append must update built city external key index");

        Console.WriteLine("Append mutation keeps records and built external key indexes consistent:");
        foreach (Person person in people.All())
            Console.WriteLine($"  {Format(person)}");
    }
}
