using Xunit;

namespace Polar.DB.Tests;

public class USequenceExplicitIndexOperationsTests
{
    [Fact]
    public void ReloadThenTrySynchronize_ReplaysTailWithoutFullRebuild()
    {
        string dir = StorageCorruptionHelpers.CreateTempDirectory();
        string state = Path.Combine(dir, "state.bin");
        try
        {
            var writer = ConfiguredUSequenceTestFactory.CreateIntegerSequence(dir, state);
            writer.AppendElement(1);
            writer.AppendElement(2);
            writer.RebuildIndexesFromSequence();
            writer.AppendElement(3);
            writer.Close();

            var reopened = ConfiguredUSequenceTestFactory.CreateIntegerSequence(dir, state);
            reopened.ReloadIndexesFromStorage();
            Assert.True(reopened.TrySynchronizeIndexesWithSequence());
            Assert.False(reopened.TrySynchronizeIndexesWithSequence());
            Assert.Equal(3, reopened.GetByKey(3));
            Assert.Equal(new object[] { 1, 2, 3 }, reopened.ElementValues().ToArray());
            reopened.Close();
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(dir);
        }
    }

    [Fact]
    public void RefreshDiagnostics_PreservesRecoveryAndReportsPairedStages()
    {
        string dir = StorageCorruptionHelpers.CreateTempDirectory();
        string state = Path.Combine(dir, "state.bin");
        try
        {
            var writer = ConfiguredUSequenceTestFactory.CreateIntegerSequence(dir, state);
            writer.AppendElement(10);
            writer.RebuildIndexesFromSequence();
            writer.AppendElement(20);
            writer.Close();

            var reopened = ConfiguredUSequenceTestFactory.CreateIntegerSequence(dir, state);
            var stages = new Dictionary<string, double>(StringComparer.Ordinal);
            reopened.Refresh((name, elapsedMs) =>
            {
                Assert.True(stages.TryAdd(name, elapsedMs));
                Assert.True(double.IsFinite(elapsedMs) && elapsedMs >= 0);
            });

            Assert.Equal(20, reopened.GetByKey(20));
            Assert.Equal(new[] { "refresh-flush", "refresh-metadata",
                "refresh-sequence-checkpoint", "refresh-primary",
                "refresh-try-replay" }, stages.Keys.ToArray());
            reopened.Close();
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(dir);
        }
    }

    [Fact]
    public void RefreshDiagnostics_ReportsFallbackWhenCheckpointIsMissing()
    {
        string dir = StorageCorruptionHelpers.CreateTempDirectory();
        string state = Path.Combine(dir, "state.bin");
        try
        {
            var writer = ConfiguredUSequenceTestFactory.CreateIntegerSequence(dir, state);
            writer.AppendElement(17);
            writer.RebuildIndexesFromSequence();
            writer.Close();
            File.Delete(state);

            var reopened = ConfiguredUSequenceTestFactory.CreateIntegerSequence(dir, state);
            var stages = new List<string>();
            reopened.Refresh((stage, _) => stages.Add(stage));

            Assert.Contains("refresh-fallback-rebuild", stages);
            Assert.Equal(17, reopened.GetByKey(17));
            reopened.Close();
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(dir);
        }
    }

    [Fact]
    public void MissingCheckpoint_RequiresExplicitRebuild()
    {
        string dir = StorageCorruptionHelpers.CreateTempDirectory();
        string state = Path.Combine(dir, "state.bin");
        try
        {
            var writer = ConfiguredUSequenceTestFactory.CreateIntegerSequence(dir, state);
            writer.AppendElement(1);
            writer.RebuildIndexesFromSequence();
            writer.Close();

            File.Delete(state);
            var reopened = ConfiguredUSequenceTestFactory.CreateIntegerSequence(dir, state);
            Assert.False(reopened.TrySynchronizeIndexesWithSequence());
            reopened.ReloadIndexesFromStorage();
            Assert.False(reopened.TrySynchronizeIndexesWithSequence());
            reopened.RebuildIndexesFromSequence();

            Assert.Equal(1, reopened.GetByKey(1));
            Assert.True(File.Exists(state));
            reopened.Close();
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(dir);
        }
    }
}
