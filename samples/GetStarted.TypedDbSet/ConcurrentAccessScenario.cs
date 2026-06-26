using Common;
using Polar.DB.Typed;

namespace GetStarted.TypedDbSet;

internal static partial class Program
{
    private static void RunConcurrentAccess()
    {
        string rootPath = DbPath.Create();
        using IDbSet<Person> people = OpenPeople(rootPath);

        foreach (Person person in CreatePeople(100, 20))
            people.Append(person);

        Check.SequenceEqual(ExpectedAgeIds(100, 119, 60), people.Find(x => x.Age, 60).Select(x => x.Id),
            "Initial age index must be built before concurrent access");
        Check.SequenceEqual(ExpectedCityIds(100, 119, "Москва"), people.Find(x => x.City, "Москва").Select(x => x.Id),
            "Initial city index must be built before concurrent access");

        Task[] readers = Enumerable.Range(0, 4)
            .Select(readerId => Task.Run(() => RunReaderLoop(people, readerId)))
            .ToArray();

        Task writerA = Task.Run(() => AppendPeople(people, 120, 5));
        Task writerB = Task.Run(() => AppendPeople(people, 125, 5));

        Task.WaitAll(readers.Concat(new[] { writerA, writerB }).ToArray());

        int[] allIds = people.All()
            .Select(x => x.Id)
            .OrderBy(x => x)
            .ToArray();
        int[] ageIds = people.Find(x => x.Age, 60)
            .Select(x => x.Id)
            .OrderBy(x => x)
            .ToArray();
        int[] cityIds = people.Find(x => x.City, "Москва")
            .Select(x => x.Id)
            .OrderBy(x => x)
            .ToArray();

        Check.SequenceEqual(Enumerable.Range(100, 30), allIds,
            "Concurrent readers and writers must keep all records available");
        Check.SequenceEqual(ExpectedAgeIds(100, 129, 60), ageIds,
            "Concurrent append must keep built age index consistent");
        Check.SequenceEqual(ExpectedCityIds(100, 129, "Москва"), cityIds,
            "Concurrent append must keep built city index consistent");

        Console.WriteLine("Concurrent readers and writers keep typed maps consistent:");
        Console.WriteLine($"  records: {allIds.Length}");
        Console.WriteLine($"  age=60: {string.Join(", ", ageIds)}");
        Console.WriteLine($"  city=Москва: {string.Join(", ", cityIds)}");
    }

    private static void RunReaderLoop(IDbSet<Person> people, int readerId)
    {
        for (int round = 0; round < 60; round++)
        {
            Check.Equal(true, people.ContainsKey(100), $"Reader {readerId} must see stable seeded key");
            Check.Equal(true, people.TryGetByKey(101, out Person? person),
                $"Reader {readerId} must read seeded key without exception");
            Check.Equal("Человек 101", person!.Name, $"Reader {readerId} must read typed value");

            _ = people.Find(x => x.Age, 60);
            _ = people.Find(x => x.City, "Москва");
            _ = people.All();
        }
    }

    private static void AppendPeople(IDbSet<Person> people, int startId, int count)
    {
        foreach (Person person in CreatePeople(startId, count))
            people.Append(person);
    }

    private static IEnumerable<Person> CreatePeople(int startId, int count)
    {
        for (int id = startId; id < startId + count; id++)
            yield return CreatePerson(id);
    }

    private static Person CreatePerson(int id)
    {
        int age = id % 2 == 0 ? 60 : 61;
        string city = id % 3 == 0 ? "Москва" : "Казань";
        return new Person(id, $"Человек {id}", age, city);
    }

    private static IEnumerable<int> ExpectedAgeIds(int startId, int endId, int age) =>
        Enumerable.Range(startId, endId - startId + 1)
            .Where(id => CreatePerson(id).Age == age);

    private static IEnumerable<int> ExpectedCityIds(int startId, int endId, string city) =>
        Enumerable.Range(startId, endId - startId + 1)
            .Where(id => CreatePerson(id).City == city);
}
