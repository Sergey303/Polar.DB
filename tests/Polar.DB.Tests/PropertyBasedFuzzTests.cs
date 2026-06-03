using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Runs deterministic pseudo-random operation sequences against storage and compares the result with a simple model.
/// </summary>
/// <remarks>
/// This is a lightweight property-style test without additional dependencies. It repeatedly combines append, reopen,
/// stale-tail corruption, and truncation to catch state-machine bugs that are hard to see in one-off examples.
/// </remarks>
public class PropertyBasedFuzzTests
{
    /// <summary>
    /// Verifies that random append/reopen/corruption cycles expose the same logical items as the in-memory model.
    /// </summary>
    /// <param name="seed">The deterministic random seed used to reproduce the generated operation sequence.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(20260414)]
    public void Random_Append_Reopen_And_Tail_Corruption_Matches_InMemory_Model(int seed)
    {
        var random = new Random(seed);
        var model = new List<int>();
        using var stream = new MemoryStream();

        for (int step = 0; step < 200; step++)
        {
            stream.Position = 0L;
            var sequence = StorageCorruptionHelpers.CreateInt32Sequence(stream);

            if (step == 0)
                sequence.Clear();

            int operation = random.Next(0, 10);
            if (operation <= 6)
            {
                int value = random.Next(-10_000, 10_000);
                sequence.AppendElement(value);
                sequence.Flush();
                model.Add(value);
            }
            else if (operation == 7 && model.Count > 0)
            {
                // Simulate stale tail after a stable state. Recovery must not expose it as a logical item.
                long stableLength = sequence.AppendOffset;
                StorageCorruptionHelpers.AppendInt32Tail(stream, random.Next());
                stream.Position = 0L;
                var recovered = StorageCorruptionHelpers.CreateInt32Sequence(stream);
                Assert.Equal(model.Count, recovered.Count());
                Assert.Equal(stableLength, recovered.AppendOffset);
            }
            else if (operation == 8 && stream.Length > 8)
            {
                // Simulate truncation; model is conservatively shortened to the readable fixed-size capacity.
                long newLength = Math.Max(8L, stream.Length - random.Next(1, 4));
                stream.SetLength(newLength);
                long readable = Math.Max(0L, (newLength - 8L) / 4L);
                while (model.Count > readable)
                    model.RemoveAt(model.Count - 1);

                stream.Position = 0L;
                _ = StorageCorruptionHelpers.CreateInt32Sequence(stream);
            }
            else
            {
                sequence.Flush();
            }

            stream.Position = 0L;
            var check = StorageCorruptionHelpers.CreateInt32Sequence(stream);
            Assert.Equal(model.Count, check.Count());
            for (int i = 0; i < model.Count; i++)
                Assert.Equal(model[i], check.GetByIndex(i));
        }
    }
}
