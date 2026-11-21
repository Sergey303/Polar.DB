using Polar.DB.SchedulingOptimization;
using Xunit;

namespace Polar.DB.Tests;

public sealed class AtomicGenerationStoreTests
{
    [Fact]
    public void Prepare_Validate_Commit_And_Recover_Switch_Whole_Generation()
    {
        WithTemp(root =>
        {
            var store = new AtomicGenerationStore(root);
            Assert.Null(store.Recover());

            using (var first = store.Prepare(p => File.WriteAllText(System.IO.Path.Combine(p, "data.txt"), "old")))
            {
                Assert.Throws<InvalidOperationException>(() => first.Commit());
                first.Validate(p => File.ReadAllText(System.IO.Path.Combine(p, "data.txt")) == "old");
                first.Commit();
            }
            string original = store.Recover()!;
            using (var second = store.Prepare(p => File.WriteAllText(System.IO.Path.Combine(p, "data.txt"), "new")))
            {
                second.Validate(p => File.ReadAllText(System.IO.Path.Combine(p, "data.txt")) == "new");
                Assert.Equal(original, store.Recover());
                second.Commit();
            }
            string current = store.Recover()!;
            Assert.NotEqual(original, current);
            Assert.Equal("old", File.ReadAllText(System.IO.Path.Combine(original, "data.txt")));
            Assert.Equal("new", File.ReadAllText(System.IO.Path.Combine(current, "data.txt")));
        });
    }

    [Fact]
    public void Interrupted_And_Rejected_Preparations_Never_Become_Current()
    {
        WithTemp(root =>
        {
            var store = new AtomicGenerationStore(root);
            using (var generation = store.Prepare(p => File.WriteAllText(System.IO.Path.Combine(p, "data.txt"), "stable")))
            {
                generation.Validate(_ => true);
                generation.Commit();
            }
            string stable = store.Recover()!;
            using (var pending = store.Prepare(p => File.WriteAllText(System.IO.Path.Combine(p, "data.txt"), "partial")))
            {
                Assert.Throws<InvalidDataException>(() => pending.Validate(_ => false));
                Assert.Equal(stable, store.Recover());
            }
            Assert.Equal(stable, new AtomicGenerationStore(root).Recover());

            // Simulates a process dying after creating the READY marker but
            // before publishing CURRENT. The ready folder is never committed.
            var epochs = new EpochFolderManager(root);
            string uncommitted = epochs.CreateBuilding();
            File.WriteAllText(System.IO.Path.Combine(uncommitted, "data.txt"), "not committed");
            epochs.MarkReady(uncommitted);
            Assert.Equal(stable, new AtomicGenerationStore(root).Recover());
        });
    }

    [Fact]
    public void Writer_Lease_Excludes_Second_Writer_And_Corrupt_Pointer_Fails_Closed()
    {
        WithTemp(root =>
        {
            var store = new AtomicGenerationStore(root);
            using (var pending = store.Prepare(_ => { }))
            {
                var second = new AtomicGenerationStore(root);
                Assert.Throws<IOException>(() => second.Prepare(_ => { }));
            }

            File.WriteAllText(System.IO.Path.Combine(root, AtomicGenerationStore.CurrentFileName), "../outside");
            Assert.Throws<InvalidDataException>(() => store.Recover());
            Assert.Throws<InvalidDataException>(() => store.Prepare(_ => { }));
        });
    }

    private static void WithTemp(Action<string> action)
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "polar-atomic-generation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { action(root); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}