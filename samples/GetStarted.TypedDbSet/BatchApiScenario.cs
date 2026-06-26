using Common;
using Polar.DB.Typed;

namespace GetStarted.TypedDbSet;

internal static partial class Program
{
    private static void RunBatchApi()
    {
        string rootPath = DbPath.Create();
        using IDbSet<Person> people = OpenPeople(rootPath);

        Check.Equal(0, people.Count, "New DbSet must have zero Count");

        people.AddRange(new[]
        {
            new Person(70, "Алексей Громов", 27, "Москва"),
            new Person(71, "Виктория Лебедева", 34, "Томск"),
            new Person(72, "Георгий Сафонов", 27, "Москва")
        });

        Check.Equal(3, people.Count, "AddRange must update Count");
        Check.SequenceEqual(new[] { 70, 71, 72 }, people.All().Select(x => x.Id),
            "AddRange must append all records");
        Check.SequenceEqual(new[] { 70, 72 }, people.Find(x => x.Age, 27).Select(x => x.Id),
            "Find must work after AddRange");

        people.AddRange(Array.Empty<Person>());
        Check.Equal(3, people.Count, "Empty AddRange must be a no-op");

        bool duplicateRejected = false;
        try
        {
            people.AddRange(new[]
            {
                new Person(73, "Зоя Ермакова", 41, "Казань"),
                new Person(70, "Инна Андреева", 42, "Пермь")
            });
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }

        Check.Equal(true, duplicateRejected, "AddRange must reject duplicate key before physical append");
        Check.Equal(3, people.Count, "Rejected AddRange must not change Count");
        Check.SequenceEqual(new[] { 70, 71, 72 }, people.All().Select(x => x.Id),
            "Rejected AddRange must not add partial records");

        bool duplicateInsideBatchRejected = false;
        try
        {
            people.AddRange(new[]
            {
                new Person(74, "Ксения Гусева", 29, "Омск"),
                new Person(74, "Лев Комаров", 30, "Тула")
            });
        }
        catch (InvalidOperationException)
        {
            duplicateInsideBatchRejected = true;
        }

        Check.Equal(true, duplicateInsideBatchRejected,
            "AddRange must reject duplicate keys inside the same batch");
        Check.Equal(3, people.Count, "Rejected same-batch duplicate must not change Count");

        Console.WriteLine("AddRange appends a batch and Count follows PrimaryKeyMap:");
        foreach (Person person in people.All())
            Console.WriteLine($"  {Format(person)}");
    }
}
