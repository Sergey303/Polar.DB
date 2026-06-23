using Polar.DB.SchedulingOptimization;
using Polar.Universal;

namespace GetStarted.SequencesAndStorage.Scenarios;

internal sealed class SchedulingOptimizationScenario : ISampleScenario
{
    public string Id => "epoch-rotate";
    public string Title => "Ротация эпох с ready marker и append-хвостом";
    public string SourcePath => "samples/GetStarted.SequencesAndStorage/SchedulingOptimizationExample.cs";
    public bool IsExtractedFragment => false;

    public void Run()
    {
        AssertReadyMarkerChoosesOnlyCompletedEpochs();
        AssertAppendTailMovesToNewActiveEpoch();
        Console.WriteLine("Epoch rotation checks passed.");
    }

    private static void AssertReadyMarkerChoosesOnlyCompletedEpochs()
    {
        var root = NewRoot();
        var manager = new EpochFolderManager(root);
        var first = manager.CreateBuilding(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        using (CreateDb(first, new[] { PersonSchema.Create(1, 10, "Alice") })) { }

        manager.MarkReady(first);
        var brokenNewer = manager.CreateBuilding(new DateTimeOffset(2026, 1, 1, 0, 1, 0, TimeSpan.Zero));
        using (CreateDb(brokenNewer, new[] { PersonSchema.Create(2, 20, "Bob") })) { }

        AssertEqual(first, manager.GetLatestReady(), "Broken newer epoch must be ignored.");
        AssertTrue(File.Exists(Path.Combine(first, EpochFolderManager.ReadyMarkerFileName)), "Ready marker expected.");
    }

    private static void AssertAppendTailMovesToNewActiveEpoch()
    {
        var root = NewRoot();
        var manager = new EpochFolderManager(root);
        var oldPath = manager.CreateBuilding(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        using (CreateDb(oldPath, new[] { PersonSchema.Create(1, 10, "Alice") })) { }
        manager.MarkReady(oldPath);

        using var owner = new ActiveSequenceOwner(PersonSchema.Open(oldPath));
        var rotation = owner.BeginRotation();
        var activeRows = owner.ReadForRotation(rotation, source => source.ElementValues().ToArray());

        owner.AppendElement(PersonSchema.Create(2, 20, "Bob"));

        var newPath = manager.CreateBuilding(new DateTimeOffset(2026, 1, 1, 0, 1, 0, TimeSpan.Zero));
        USequence? newSequence = null;

        try
        {
            newSequence = CreateDb(newPath, activeRows);
            var oldSequence = owner.CompleteRotation(rotation, newSequence, () => manager.MarkReady(newPath));
            newSequence = null;
            oldSequence.Dispose();

            var ids = owner.Active.ElementValues()
                .Select(PersonSchema.GetId)
                .OrderBy(id => id)
                .ToArray();

            AssertTrue(ids.SequenceEqual(new[] { 1, 2 }), "Tail append must be visible in new active epoch.");
            AssertEqual(newPath, manager.GetLatestReady(), "New ready epoch must become latest.");
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
        var sequence = PersonSchema.Create(dbPath);
        sequence.Load(rows);
        sequence.Flush();
        sequence.Build();
        return sequence;
    }

    private static string NewRoot()
    {
        return Path.Combine(Path.GetTempPath(), "polar-epoch-checks", Guid.NewGuid().ToString("N"));
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertEqual(string? expected, string? actual, string message)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidOperationException(message + $" Expected='{expected}', actual='{actual}'.");
    }
}
