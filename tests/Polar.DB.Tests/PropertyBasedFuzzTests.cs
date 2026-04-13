using Xunit;

namespace Polar.DB.Tests;

public class PropertyBasedFuzzTests
{
    [Fact]
    public void Randomized_String_Sequence_With_Garbage_Tails_Recovers_Readable_Prefix()
    {
        var random = new Random(12345);

        for (int iteration = 0; iteration < 25; iteration++)
        {
            int count = random.Next(1, 20);
            string[] values = Enumerable.Range(0, count)
                .Select(i => $"v{iteration}_{i}_{new string((char)('a' + (i % 26)), random.Next(1, 8))}")
                .ToArray();

            byte[] valid = StorageCorruptionHelpers.BuildVariableStringSequenceBytes(values);
            byte[] tail = Enumerable.Range(0, random.Next(1, 7))
                .Select(_ => (byte)random.Next(0, 255))
                .ToArray();
            byte[] corrupted = StorageCorruptionHelpers.ConcatBytes(valid, tail);

            using var stream = new MemoryStream(corrupted);
            var sequence = new UniversalSequenceBase(new PType(PTypeEnumeration.sstring), stream);

            Assert.Equal(values.Length, sequence.Count());
            Assert.Equal(values.Cast<object>().ToArray(), sequence.ElementValues().ToArray());
            Assert.Equal(valid.Length, sequence.AppendOffset);
            Assert.Equal(valid.Length, stream.Length);
        }
    }

    [Fact]
    public void Randomized_Fixed_Size_Header_Mismatches_Recover_To_Readable_Element_Count()
    {
        var random = new Random(54321);

        for (int iteration = 0; iteration < 40; iteration++)
        {
            int actualCount = random.Next(0, 20);
            int declaredCount = random.Next(0, 30);
            int[] values = Enumerable.Range(0, actualCount)
                .Select(i => iteration * 1000 + i)
                .ToArray();

            using var stream = new MemoryStream();
            StorageCorruptionHelpers.WriteFixedSequenceBytes(stream, values, declaredCount);

            var sequence = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), stream);

            int expectedRecoveredCount = Math.Min(actualCount, declaredCount);
            Assert.Equal(expectedRecoveredCount, sequence.Count());
            Assert.Equal(values.Take(expectedRecoveredCount).Cast<object>().ToArray(), sequence.ElementValues().ToArray());
            Assert.Equal(8L + expectedRecoveredCount * 4L, sequence.AppendOffset);
            Assert.Equal(sequence.AppendOffset, stream.Length);
        }
    }

    [Fact]
    public void Randomized_USequence_Restarts_And_Appends_Preserve_All_Keys()
    {
        string tempDir = StorageCorruptionHelpers.CreateTempDirectory();
        string statePath = Path.Combine(tempDir, "state.bin");
        var expectedKeys = new HashSet<int>();
        var random = new Random(24680);

        try
        {
            USequence? current = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);

            for (int step = 0; step < 60; step++)
            {
                int operation = random.Next(0, 4);
                switch (operation)
                {
                    case 0:
                    case 1:
                        int value = step + 1;
                        current.AppendElement(value);
                        expectedKeys.Add(value);
                        break;

                    case 2:
                        current.Build();
                        break;

                    default:
                        current.Close();
                        current = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
                        current.RestoreDynamic();
                        break;
                }
            }

            current.Close();

            var reopened = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            reopened.RestoreDynamic();

            foreach (int key in expectedKeys.OrderBy(x => x))
            {
                Assert.Equal(key, reopened.GetByKey(key));
            }

            Assert.Equal(expectedKeys.Count, reopened.ElementValues().Cast<object>().Count());
            reopened.Close();
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(tempDir);
        }
    }

    [Fact]
    public void Randomized_State_File_TooShort_Never_Prevents_Recovering_Current_Data()
    {
        string tempDir = StorageCorruptionHelpers.CreateTempDirectory();
        string statePath = Path.Combine(tempDir, "state.bin");

        try
        {
            var sequence = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
            for (int value = 1; value <= 10; value++)
                sequence.AppendElement(value);
            sequence.Build();
            for (int value = 11; value <= 15; value++)
                sequence.AppendElement(value);
            sequence.Close();

            for (int iteration = 0; iteration < 10; iteration++)
            {
                StorageCorruptionHelpers.WriteTooShortState(statePath);

                var reopened = StorageCorruptionHelpers.CreateIntegerSequence(tempDir, statePath, optimise: false);
                reopened.Refresh();

                for (int value = 1; value <= 15; value++)
                    Assert.Equal(value, reopened.GetByKey(value));

                reopened.Close();
            }
        }
        finally
        {
            StorageCorruptionHelpers.DeleteDirectoryQuietly(tempDir);
        }
    }
}
