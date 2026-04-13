using System.Diagnostics;
using Xunit;

namespace Polar.DB.Tests;

public class PerformanceSmokeTests
{
    [Fact]
    public void Append_Throughput_Smoke_For_Fixed_Size_Sequence()
    {
        using var stream = new MemoryStream();
        var sequence = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), stream);
        long budgetMs = StorageCorruptionHelpers.ReadBudgetMs("POLARDB_APPEND_BUDGET_MS", 10000);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 20_000; i++)
            sequence.AppendElement(i);
        sequence.Flush();
        sw.Stop();

        Assert.Equal(20_000, sequence.Count());
        StorageCorruptionHelpers.AssertDurationWithin(sw, budgetMs, "fixed-size append throughput smoke");
    }

    [Fact]
    public void Refresh_Smoke_For_Built_USequence()
    {
        string tempDir = StorageCorruptionHelpers.CreateTempDirectory();
        string statePath = Path.Combine(tempDir, "state.bin");
        long budgetMs = StorageCorruptionHelpers.ReadBudgetMs("POLARDB_REFRESH_BUDGET_MS", 10000);

        try
        {
            var writer = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            for (int i = 0; i < 10_000; i++)
                writer.AppendElement(i);
            writer.Build();
            writer.Close();

            var reopened = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);

            var sw = Stopwatch.StartNew();
            reopened.Refresh();
            sw.Stop();

            Assert.Equal(9999, reopened.GetByKey(9999));
            StorageCorruptionHelpers.AssertDurationWithin(sw, budgetMs, "USequence refresh smoke");
            reopened.Close();
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(tempDir);
        }
    }

    [Fact]
    public void Recovery_Smoke_For_Variable_Size_Sequence_With_Garbage_Tail()
    {
        long budgetMs = StorageCorruptionHelpers.ReadBudgetMs("POLARDB_RECOVERY_BUDGET_MS", 10000);

        string[] values = Enumerable.Range(0, 5000)
            .Select(i => $"value-{i:D5}-payload")
            .ToArray();

        byte[] valid = StorageCorruptionHelpers.BuildVariableStringSequenceBytes(values);
        byte[] corrupted = StorageCorruptionHelpers.ConcatBytes(valid, Enumerable.Repeat((byte)0xAB, 128).ToArray());

        using var stream = new MemoryStream(corrupted);
        var sw = Stopwatch.StartNew();
        var sequence = new UniversalSequenceBase(new PType(PTypeEnumeration.sstring), stream);
        sw.Stop();

        Assert.Equal(values.Length, sequence.Count());
        Assert.Equal(values[0], (string)sequence.GetElement(8L));
        Assert.Equal(valid.Length, sequence.AppendOffset);
        StorageCorruptionHelpers.AssertDurationWithin(sw, budgetMs, "variable-size recovery smoke");
    }
}
