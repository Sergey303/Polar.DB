using Polar.DB.SchedulingOptimization;
using Polar.Universal;

namespace GetStarted.SequencesAndStorage;

public static class SchedulingOptimizationExample
{
    public static void Run()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "polar-db-scheduling-example",
            Guid.NewGuid().ToString("N"));

        var epochs = new EpochFolderManager(rootPath);
        using var owner = new ActiveSequenceOwner(OpenLastOrCreate(epochs));

        Console.WriteLine("Initial active epoch:");
        PrintRows(owner.Read(active => active.ElementValues().ToArray()));

        owner.AppendElement(PersonSchema.Create(4, 42, "Dora"));
        Console.WriteLine("After append to active epoch:");
        PrintRows(owner.Read(active => active.ElementValues().ToArray()));

        RotateOnce(epochs, owner);
        Console.WriteLine("After rotation:");
        PrintRows(owner.Read(active => active.ElementValues().ToArray()));

        Console.WriteLine("Ready epochs:");
        foreach (var path in epochs.ListReady())
            Console.WriteLine("  " + path);
    }

    private static USequence OpenLastOrCreate(EpochFolderManager epochs)
    {
        var latestReadyPath = epochs.GetLatestReady();
        if (!string.IsNullOrWhiteSpace(latestReadyPath) && Directory.Exists(latestReadyPath))
            return PersonSchema.Open(latestReadyPath);

        var epochPath = epochs.CreateBuilding(DateTimeOffset.UtcNow);
        var sequence = CreateDb(epochPath, new[]
        {
            PersonSchema.Create(1, 1123, "Alice"),
            PersonSchema.Create(2, 5423, "Bob"),
            PersonSchema.Create(3, 5123, "Clara")
        });

        epochs.MarkReady(epochPath);
        return sequence;
    }

    private static void RotateOnce(EpochFolderManager epochs, ActiveSequenceOwner owner)
    {
        var rotation = owner.BeginRotation();
        var epochPath = epochs.CreateBuilding(DateTimeOffset.UtcNow);
        USequence? newSequence = null;

        try
        {
            var activeRows = owner.ReadForRotation(rotation, source =>
                source.ElementValues()
                    .Where(record => !PersonSchema.Deleted(record))
                    .ToArray());

            owner.AppendElement(PersonSchema.Create(5, 35, "Eve"));
            newSequence = CreateDb(epochPath, activeRows);

            var oldSequence = owner.CompleteRotation(
                rotation,
                newSequence,
                () => epochs.MarkReady(epochPath));

            newSequence = null;
            oldSequence.Dispose();
        }
        catch
        {
            owner.CancelRotation(rotation);
            newSequence?.Dispose();
            throw;
        }
    }

    private static USequence CreateDb(string dbPath, IEnumerable<object> rows)
    {
        var sequence = PersonSchema.Open(dbPath);
        sequence.Load(rows);
        sequence.Flush();
        sequence.Build();
        return sequence;
    }

    private static void PrintRows(IEnumerable<object> rows)
    {
        foreach (var row in rows)
        {
            Console.WriteLine(
                $"  id={PersonSchema.GetId(row)}, " +
                $"age={PersonSchema.GetAge(row)}, " +
                $"name={PersonSchema.GetName(row)}, " +
                $"deleted={PersonSchema.Deleted(row)}");
        }
    }
}
