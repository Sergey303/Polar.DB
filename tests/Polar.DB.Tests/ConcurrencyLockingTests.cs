using Xunit;

namespace Polar.DB.Tests;

public class ConcurrencyLockingTests
{
    [Fact]
    public void Build_Throws_When_State_File_Is_Locked_Exclusively()
    {
        string tempDir = StorageCorruptionHelpers.CreateTempDirectory();
        string statePath = Path.Combine(tempDir, "state.bin");

        try
        {
            var sequence = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            sequence.AppendElement(1);

            using var stateLock = new FileStream(
                statePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            Assert.ThrowsAny<IOException>(() => sequence.Build());
            sequence.Close();
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(tempDir);
        }
    }

    [Fact]
    public void Opening_Second_Sequence_Fails_When_Main_Data_File_Is_Locked_Exclusively()
    {
        string tempDir = StorageCorruptionHelpers.CreateTempDirectory();
        string statePath = Path.Combine(tempDir, "state.bin");
        string sequencePath = Path.Combine(tempDir, "sequence.bin");

        try
        {
            using var lockStream = new FileStream(
                sequencePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            Assert.ThrowsAny<IOException>(() =>
            {
                var _ = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false, fileShare: FileShare.ReadWrite);
            });
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(tempDir);
        }
    }

    [Fact]
    public void Two_Reopened_Sequences_Can_Read_Same_Built_Data_When_Files_Are_Shared()
    {
        string tempDir = StorageCorruptionHelpers.CreateTempDirectory();
        string statePath = Path.Combine(tempDir, "state.bin");

        try
        {
            var writer = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            writer.AppendElement(10);
            writer.AppendElement(20);
            writer.Build();
            writer.Close();

            var left = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            var right = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);

            left.Refresh();
            right.Refresh();

            Assert.Equal(10, left.GetByKey(10));
            Assert.Equal(20, right.GetByKey(20));

            left.Close();
            right.Close();
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(tempDir);
        }
    }

    [Fact]
    public void RestoreDynamic_After_Unsaved_Appends_From_Previous_Instance_Rebuilds_Primary_Lookup()
    {
        string tempDir = StorageCorruptionHelpers.CreateTempDirectory();
        string statePath = Path.Combine(tempDir, "state.bin");

        try
        {
            var first = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            first.AppendElement(1);
            first.Build();
            first.AppendElement(2);
            first.AppendElement(3);
            first.Close();

            var second = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            second.RestoreDynamic();

            Assert.Equal(2, second.GetByKey(2));
            Assert.Equal(3, second.GetByKey(3));

            second.Close();
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(tempDir);
        }
    }
}
