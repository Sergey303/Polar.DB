using Common;
using Polar.DB.Typed;

namespace GetStarted.TypedDbSet;

internal static partial class Program
{
    private static void RunBasics()
    {
        string rootPath = DbPath.Create();
        using IDbSet<Person> people = OpenPeople(rootPath);

        people.Append(new Person(1, "Анна Иванова", 30, "Москва"));
        people.Append(new Person(2, "Борис Петров", 42, "Новосибирск"));
        people.Append(new Person(3, "Вера Соколова", 30, "Томск"));

        Person byKey = people.GetByKey(2);
        Check.Equal("Борис Петров", byKey.Name, "GetByKey must return typed record");

        IReadOnlyList<Person> age30 = people.Find(x => x.Age, 30);
        Check.SequenceEqual(new[] { 1, 3 }, age30.Select(x => x.Id), "Find by configured external key must return records");

        IReadOnlyList<Person> all = people.All();
        Check.SequenceEqual(new[] { 1, 2, 3 }, all.Select(x => x.Id), "All must return typed records");

        string schemaPath = Path.Combine(rootPath, PersonStorageName, "schema.json");
        Check.Equal(true, File.Exists(schemaPath), "DbSet must persist inferred schema.json");

        Console.WriteLine("Records:");
        foreach (Person person in all)
            Console.WriteLine($"  {Format(person)}");
    }

    private static void RunReopen()
    {
        string rootPath = DbPath.Create();

        using (IDbSet<Person> people = OpenPeople(rootPath))
        {
            people.Append(new Person(10, "Галина Орлова", 37, "Омск"));
            people.Append(new Person(11, "Дмитрий Смирнов", 44, "Пермь"));
        }

        using IDbSet<Person> reopened = OpenPeople(rootPath);
        IReadOnlyList<Person> all = reopened.All();
        Check.SequenceEqual(new[] { 10, 11 }, all.Select(x => x.Id), "Reopened DbSet must read persisted records");

        Person byKey = reopened.GetByKey(11);
        Check.Equal("Дмитрий Смирнов", byKey.Name, "Reopened DbSet must rebuild primary-key map");

        Console.WriteLine("Records after reopening the same root path:");
        foreach (Person person in all)
            Console.WriteLine($"  {Format(person)}");
    }

    private static void RunPrimaryKeyMap()
    {
        string rootPath = DbPath.Create();
        using IDbSet<Person> people = OpenPeople(rootPath);

        people.Append(new Person(20, "Елена Морозова", 28, "Казань"));
        people.Append(new Person(21, "Игорь Кузнецов", 31, "Тула"));

        Person first = people.GetByKey(20);
        Check.Equal("Елена Морозова", first.Name, "GetByKey must use typed primary-key map");

        bool duplicateRejected = false;
        try
        {
            people.Append(new Person(20, "Лариса Белова", 29, "Воронеж"));
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }

        Check.Equal(true, duplicateRejected, "Primary-key map must reject duplicate keys before append");
        Check.SequenceEqual(new[] { 20, 21 }, people.All().Select(x => x.Id), "Duplicate append must not add a record");

        Console.WriteLine("Primary-key map rejects duplicate keys and keeps records stable:");
        foreach (Person person in people.All())
            Console.WriteLine($"  {Format(person)}");
    }

    private static void RunLookupApi()
    {
        string rootPath = DbPath.Create();
        using IDbSet<Person> people = OpenPeople(rootPath);

        people.Append(new Person(24, "Сергей Волков", 39, "Рязань"));
        people.Append(new Person(25, "Татьяна Крылова", 45, "Самара"));

        Check.Equal(true, people.ContainsKey(24), "ContainsKey must find existing key");
        Check.Equal(false, people.ContainsKey(404), "ContainsKey must return false for missing key");

        bool found = people.TryGetByKey(25, out Person? person);
        Check.Equal(true, found, "TryGetByKey must report existing key");
        Check.Equal("Татьяна Крылова", person!.Name, "TryGetByKey must return typed value");

        bool missing = people.TryGetByKey(404, out Person? missingPerson);
        Check.Equal(false, missing, "TryGetByKey must not throw for missing key");
        Check.Equal(true, missingPerson == null, "TryGetByKey must return null for missing reference record type");

        Console.WriteLine("Lookup API supports ContainsKey and TryGetByKey:");
        Console.WriteLine($"  found: {Format(person)}");
    }
}
