using Common;
using Polar.DB.Typed;

namespace GetStarted.TypedDbSet;

internal static partial class Program
{
    private static void RunDbSetLifecycleGuard()
    {
        string rootPath = DbPath.Create();
        var people = (DbSet<Person>)OpenPeople(rootPath);

        people.Append(new Person(400, "Зоя Андреева", 54, "Москва"));
        people.Dispose();
        people.Dispose();

        ExpectDisposed(() => people.Append(new Person(401, "Ярослав Данилов", 55, "Калуга")), "Append");
        ExpectDisposed(() => people.GetByKey(400), "GetByKey");
        ExpectDisposed(() => people.ContainsKey(400), "ContainsKey");
        ExpectDisposed(() => people.TryGetByKey(400, out _), "TryGetByKey");
        ExpectDisposed(() => people.All(), "All");
        ExpectDisposed(() => people.Find(x => x.Age, 54), "Find");
        ExpectDisposed(() => people.Diagnostics(), "Diagnostics");

        Console.WriteLine("Disposed DbSet rejects later operations and allows repeated Dispose().");
    }

    private static void ExpectDisposed(Action action, string operation)
    {
        try
        {
            action();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        throw new InvalidOperationException($"{operation} must throw ObjectDisposedException after Dispose().");
    }
}
