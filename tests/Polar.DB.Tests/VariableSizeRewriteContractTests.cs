using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Defines the expected safety envelope for in-place rewrite operations over fixed-size and variable-size items.
/// </summary>
/// <remarks>
/// The tests do not force one implementation strategy. A variable-size rewrite may either be explicitly rejected or
/// proven safe by preserving following items and keeping the logical append boundary consistent after reopen.
/// </remarks>
public class VariableSizeRewriteContractTests
{
    /// <summary>
    /// Verifies that rewriting a middle variable-size item with a larger payload does not silently corrupt following data.
    /// </summary>
    [Fact]
    public void VariableSize_Rewrite_With_Larger_Value_Must_Not_Silently_Corrupt_Following_Items()
    {
        using var stream = new MemoryStream();
        var sequence = StorageCorruptionHelpers.CreateVariableRecordSequence(stream);

        sequence.Clear();
        long firstOffset = sequence.AppendElement(new object[] { 1, "A" });
        long secondOffset = sequence.AppendElement(new object[] { 2, "B" });
        sequence.Flush();

        try
        {
            sequence.SetElement(firstOffset, new object[] { 1, "A much longer name" });
            sequence.Flush();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException)
        {
            // Acceptable policy: explicitly reject unsafe variable-size in-place rewrite.
            return;
        }

        var second = Assert.IsType<object[]>(sequence.GetElement(secondOffset));
        Assert.Equal(2, (int)second[0]);
        Assert.Equal("B", (string)second[1]);
    }

    /// <summary>
    /// Verifies that rewriting the last variable-size item with a larger payload keeps durable logical end consistent.
    /// </summary>
    [Fact]
    public void VariableSize_Rewrite_Last_Item_With_Larger_Value_Keeps_Logical_End_Consistent()
    {
        using var stream = new MemoryStream();
        var sequence = StorageCorruptionHelpers.CreateVariableRecordSequence(stream);

        sequence.Clear();
        sequence.AppendElement(new object[] { 1, "A" });
        long lastOffset = sequence.AppendElement(new object[] { 2, "B" });
        sequence.Flush();

        try
        {
            sequence.SetElement(lastOffset, new object[] { 2, "B-long" });
            sequence.Flush();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException)
        {
            return;
        }

        stream.Position = 0L;
        var reopened = StorageCorruptionHelpers.CreateVariableRecordSequence(stream);
        Assert.Equal(2L, reopened.Count());
        Assert.Equal(reopened.AppendOffset, stream.Length);

        var last = Assert.IsType<object[]>(reopened.GetByIndex(1));
        Assert.Equal(2, (int)last[0]);
        Assert.Equal("B-long", (string)last[1]);
    }

    /// <summary>
    /// Verifies the positive baseline: fixed-size in-place rewrite is supported and remains durable after reopen.
    /// </summary>
    [Fact]
    public void FixedSize_Rewrite_At_Known_Offset_Is_Supported_And_Durable()
    {
        using var stream = new MemoryStream();
        var sequence = StorageCorruptionHelpers.CreateInt32Sequence(stream);

        sequence.Clear();
        long firstOffset = sequence.AppendElement(10);
        sequence.AppendElement(20);
        sequence.Flush();

        sequence.SetElement(firstOffset, 11);
        sequence.Flush();

        stream.Position = 0L;
        var reopened = StorageCorruptionHelpers.CreateInt32Sequence(stream);
        Assert.Equal(2L, reopened.Count());
        Assert.Equal(11, reopened.GetByIndex(0));
        Assert.Equal(20, reopened.GetByIndex(1));
    }
}
