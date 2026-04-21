using Xunit;

namespace Polar.DB.Tests;

public class USequenceStateRecoveryTests
{
    [Fact]
    public void Refresh_Replays_Primary_Dynamic_Tail_From_State_File()
    {
        string tempDir = StorageCorruptionHelpers.CreateTempDirectory();
        string statePath = Path.Combine(tempDir, "state.bin");

        try
        {
            var writer = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            writer.AppendElement(1);
            writer.AppendElement(2);
            writer.Build();
            writer.AppendElement(3);
            writer.Close();

            var reopened = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            reopened.Refresh();

            Assert.Equal(3, reopened.GetByKey(3));
            Assert.Equal(new object[] { 1, 2, 3 }, reopened.ElementValues().ToArray());

            reopened.Close();
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(tempDir);
        }
    }

    [Fact]
    public void RestoreDynamic_Replays_Tail_And_Updates_State_File()
    {
        string tempDir = StorageCorruptionHelpers.CreateTempDirectory();
        string statePath = Path.Combine(tempDir, "state.bin");

        try
        {
            var writer = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            writer.AppendElement(10);
            writer.AppendElement(20);
            writer.Build();
            writer.AppendElement(30);
            writer.Close();

            var reopened = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            reopened.RestoreDynamic();

            Assert.Equal(30, reopened.GetByKey(30));

            var state = StorageCorruptionHelpers.ReadState(statePath);
            Assert.Equal(3L, state.Count);
            Assert.True(state.AppendOffset >= 20L);

            reopened.Close();
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(tempDir);
        }
    }

    [Fact]
    public void Refresh_Treats_TooShort_State_File_As_Default_And_Does_Not_Lose_Data()
    {
        string tempDir = StorageCorruptionHelpers.CreateTempDirectory();
        string statePath = Path.Combine(tempDir, "state.bin");

        try
        {
            var writer = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            writer.AppendElement(5);
            writer.AppendElement(6);
            writer.Build();
            writer.AppendElement(7);
            writer.Close();

            StorageCorruptionHelpers.WriteTooShortState(statePath);

            var reopened = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            reopened.Refresh();

            Assert.Equal(5, reopened.GetByKey(5));
            Assert.Equal(6, reopened.GetByKey(6));
            Assert.Equal(7, reopened.GetByKey(7));

            reopened.Close();
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(tempDir);
        }
    }

    [Fact]
    public void Build_Saves_Current_Count_And_AppendOffset_To_State_File()
    {
        string tempDir = StorageCorruptionHelpers.CreateTempDirectory();
        string statePath = Path.Combine(tempDir, "state.bin");

        try
        {
            var sequence = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            sequence.AppendElement(100);
            sequence.AppendElement(200);
            sequence.AppendElement(300);
            sequence.Build();

            var state = StorageCorruptionHelpers.ReadState(statePath);
            Assert.Equal(3L, state.Count);
            Assert.True(state.AppendOffset > 8L);

            sequence.Close();
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(tempDir);
        }
    }

    [Fact]
    public void Repeated_Restart_Cycles_With_RestoreDynamic_Preserve_All_Appended_Keys()
    {
        string tempDir = StorageCorruptionHelpers.CreateTempDirectory();
        string statePath = Path.Combine(tempDir, "state.bin");

        try
        {
            var sequence = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            sequence.AppendElement(1);
            sequence.Build();
            sequence.Close();

            for (int value = 2; value <= 6; value++)
            {
                var reopened = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
                reopened.RestoreDynamic();
                reopened.AppendElement(value);
                reopened.Close();
            }

            var final = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            final.RestoreDynamic();

            for (int value = 1; value <= 6; value++)
            {
                Assert.Equal(value, final.GetByKey(value));
            }

            Assert.Equal(new object[] { 1, 2, 3, 4, 5, 6 }, final.ElementValues().Cast<object>().ToArray());
            final.Close();
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(tempDir);
        }
    }
}
