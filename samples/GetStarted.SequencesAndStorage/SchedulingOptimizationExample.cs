using Polar.DB.SchedulingOptimization;
using Polar.Universal;

namespace GetStarted.SequencesAndStorage;

public sealed class SchedulingOptimization
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "data", Guid.NewGuid().ToString("N"));
        var epochFolderManager = new EpochFolderManager(rootPath);

        using var owner = new ActiveSequenceOwner(OpenLastOrCreate(epochFolderManager));

        owner.AppendElement(PersonSchema.Create(4, 42, "Dora"));

        foreach (var person in owner.Active.ElementValues())
        {
            Console.WriteLine($"{PersonSchema.GetId(person)}: {PersonSchema.GetName(person)}: {PersonSchema.Deleted(person)}");
        }

        await Scheduler.RunAsync(
            token => RotateAsync(epochFolderManager, owner, token),
            TimeSpan.FromMinutes(5),
            false,
            cancellationToken);
    }

    private USequence OpenLastOrCreate(EpochFolderManager epochFolderManager)
    {
        var latestReadyPath = epochFolderManager.GetLatestReady();
        if (!string.IsNullOrWhiteSpace(latestReadyPath) && Directory.Exists(latestReadyPath))
            return PersonSchema.Open(latestReadyPath);

        var epochPath = epochFolderManager.CreateBuilding(DateTimeOffset.UtcNow);
        var sequence = CreateDb(epochPath, new[]
        {
            PersonSchema.Create(1, 1123, "Alice"),
            PersonSchema.Create(2, 5423, "Bob"),
            PersonSchema.Create(3, 5123, "Clara")
        });

        sequence.Flush();
        epochFolderManager.MarkReady(epochPath);
        return sequence;
    }

    private Task RotateAsync(
        EpochFolderManager epochFolderManager,
        ActiveSequenceOwner owner,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rotation = owner.BeginRotation();
        var epochPath = epochFolderManager.CreateBuilding(DateTimeOffset.UtcNow);
        USequence? newSequence = null;

        try
        {
            var activeRows = rotation.Source.ElementValues()
                .Where(record => !PersonSchema.Deleted(record))
                .ToArray();

            newSequence = CreateDb(epochPath, activeRows);
            var oldSequence = owner.CompleteRotation(
                rotation,
                newSequence,
                () => epochFolderManager.MarkReady(epochPath));

            newSequence = null;
            oldSequence.Dispose();
            return Task.CompletedTask;
        }
        catch
        {
            owner.CancelRotation(rotation);
            newSequence?.Dispose();
            throw;
        }
    }

    public USequence CreateDb(string dbPath, IEnumerable<object> rows)
    {
        var sequence = PersonSchema.Create(dbPath);
        sequence.Load(rows);
        sequence.Flush();
        sequence.Build();
        return sequence;
    }
}
