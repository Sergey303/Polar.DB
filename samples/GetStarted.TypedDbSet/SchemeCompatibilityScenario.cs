using Common;
using Polar.DB.Typed;

namespace GetStarted.TypedDbSet;

internal static partial class Program
{
    private static void RunSchemeCompatibilityGuard()
    {
        string rootPath = DbPath.Create();

        using (IDbSet<Person> people = OpenPeople(rootPath))
        {
            people.Append(new Person(200, "Раиса Захарова", 63, "Москва"));
        }

        SchemeCompatibilityException missingField = ExpectSchemeError(() =>
        {
            using var _ = new DbSet<PersonWithoutCity>(rootPath, options =>
                options.Name(PersonStorageName).Key(x => x.Id));
        });

        Check.Equal(true, missingField.Detail.Contains("Field count changed"),
            "Opening the same table with a missing field must fail clearly");

        SchemeCompatibilityException reorderedFields = ExpectSchemeError(() =>
        {
            using var _ = new DbSet<PersonWithChangedOrder>(rootPath, options =>
                options.Name(PersonStorageName).Key(x => x.Id));
        });

        Check.Equal(true, reorderedFields.Detail.Contains("Field #2 changed"),
            "Opening the same table with reordered fields must fail clearly");

        Console.WriteLine("Scheme compatibility guard rejects unsafe record changes:");
        Console.WriteLine($"  missing field: {missingField.Detail}");
        Console.WriteLine($"  changed order: {reorderedFields.Detail}");
    }

    private static SchemeCompatibilityException ExpectSchemeError(Action action)
    {
        try
        {
            action();
        }
        catch (SchemeCompatibilityException ex)
        {
            return ex;
        }

        throw new InvalidOperationException("Expected SchemeCompatibilityException was not thrown.");
    }

    private sealed record PersonWithoutCity(int Id, string Name, int Age);

    private sealed record PersonWithChangedOrder(int Id, string Name, string City, int Age);
}
