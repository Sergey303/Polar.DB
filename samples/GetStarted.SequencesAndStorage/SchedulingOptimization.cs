using Common;
using Polar.DB.SchedulingOptimization;
using Polar.Universal;

namespace GetStarted.SequencesAndStorage;

public static class SchedulingOptimization
{
    public static void Run()
    {
        var epochs = new EpochFolderManager(DbPath.Create());
        using var owner = new ActiveSequenceOwner(OpenLastOrCreate(epochs));

        CheckIds("Initial active epoch", owner, 1, 2, 3);
        CheckReadyEpochs(epochs, 1);

        owner.AppendElement(PersonSchema.Create(4, 42, "Дарья"));
        CheckIds("After append to active epoch", owner, 1, 2, 3, 4);

        RotateOnce(epochs, owner);
        CheckIds("After rotation", owner, 1, 2, 3, 4, 5);
        CheckReadyEpochs(epochs, 2);
        CheckLatestReadyCanReopen(epochs, 1, 2, 3, 4, 5);
        CheckBuildingEpochIsIgnored(epochs);
    }

    private static USequence OpenLastOrCreate(EpochFolderManager epochs)
    {
        var latestReadyPath = epochs.GetLatestReady();
        if (!string.IsNullOrWhiteSpace(latestReadyPath) && Directory.Exists(latestReadyPath))
            return PersonSequence.Open(latestReadyPath);

        var epochPath = epochs.CreateBuilding(DateTimeOffset.UtcNow);
        var sequence = CreateDb(epochPath, new[]
        {
            PersonSchema.Create(1, 1123,"Анна"),
            PersonSchema.Create(2, 5423, "Борис" ),
            PersonSchema.Create(3, 5123, "Клара")
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

            owner.AppendElement(PersonSchema.Create(5, 35, "Евгения"));
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
        var sequence = PersonSequence.Open(dbPath);
        sequence.Load(rows);
        sequence.Flush();
        sequence.Build();
        return sequence;
    }

    private static void CheckIds(string title, ActiveSequenceOwner owner, params int[] expected)
    {
        var rows = owner.Read(active => active.ElementValues().ToArray());
        PrintRows(title, rows);

        var actual = rows.Select(PersonSchema.GetId).OrderBy(id => id).ToArray();
        Check.SequenceEqual(expected, actual, title + " ids must match");
    }

    private static void CheckReadyEpochs(EpochFolderManager epochs, int expectedCount)
    {
        var ready = epochs.ListReady().ToArray();
        Check.Equal(expectedCount, ready.Length, "Ready epoch count must match");
        if (!ready.All(path => File.Exists(Path.Combine(path, "_epoch.ready")))) throw new InvalidOperationException("Евгенияry ready epoch must have _epoch.ready marker");
    }

    private static void CheckLatestReadyCanReopen(EpochFolderManager epochs, params int[] expectedIds)
    {
        var latestReady = epochs.GetLatestReady();
        if (string.IsNullOrWhiteSpace(latestReady)) throw new InvalidOperationException("Latest ready epoch must exist");
        using var reopened = PersonSequence.Open(latestReady);
        var actual = reopened.ElementValues().Select(PersonSchema.GetId).OrderBy(id => id).ToArray();
        Check.SequenceEqual(expectedIds, actual, "Latest ready epoch must contain rotated data");
    }

    private static void CheckBuildingEpochIsIgnored(EpochFolderManager epochs)
    {
        var latestBefore = epochs.GetLatestReady();
        _ = epochs.CreateBuilding(DateTimeOffset.UtcNow.AddMinutes(10));
        var latestAfter = epochs.GetLatestReady();
        Check.Equal(latestBefore, latestAfter, "Building epoch without marker must be ignored");
    }

    private static void PrintRows(string title, IEnumerable<object> rows)
    {
        Console.WriteLine(title + ":");
        foreach (var row in rows)
            Console.WriteLine("  " + PersonSchema.Format(row));
    }
}
