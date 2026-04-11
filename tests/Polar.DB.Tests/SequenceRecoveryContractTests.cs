using System.Text;
using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Defines the most valuable damaged-file and recovery tests for the sequence storage model.
/// </summary>
/// <remarks>
/// These tests are deliberately hostile. Their job is to prove that recovery logic distinguishes between header intent,
/// readable data, garbage tail, and logical end of valid content.
/// </remarks>
public abstract class SequenceRecoveryContractTests
{
    /// <summary>
    /// Creates a repository-specific harness bound to a concrete sequence implementation.
    /// </summary>
    protected abstract ISequenceContractHarness CreateHarness();

    /// <summary>
    /// Verifies that recovery does not invent logical items when the header claims more than the file can actually provide.
    /// </summary>
    [Fact]
    public void Recovery_Uses_Only_Actually_Readable_Items_When_Header_Count_Is_Too_Large()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateValue("first"));
        harness.Append(harness.CreateValue("second"));
        harness.Flush();
        harness.Build();

        harness.CorruptDeclaredCount(100);
        harness.Reopen();
        harness.Refresh();

        var snapshot = harness.Snapshot();
        Assert.Equal(2, snapshot.Count);
        Assert.Equal(new[] { "first", "second" }, snapshot.Items.Select(harness.ReadPayload).ToArray());
        Assert.True(snapshot.StreamLength >= snapshot.AppendOffset);
    }

    /// <summary>
    /// Verifies that trailing garbage bytes are not reinterpreted as valid logical elements.
    /// </summary>
    [Fact]
    public void Refresh_Does_Not_Treat_Garbage_Tail_As_Valid_Data()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateValue("first"));
        harness.Append(harness.CreateValue("second"));
        harness.Flush();
        harness.Build();

        harness.AppendGarbageTail(Encoding.UTF8.GetBytes("__garbage_tail__"));
        harness.Reopen();
        harness.Refresh();

        var snapshot = harness.Snapshot();
        Assert.Equal(2, snapshot.Count);
        Assert.Equal(new[] { "first", "second" }, snapshot.Items.Select(harness.ReadPayload).ToArray());
        Assert.True(snapshot.StreamLength >= snapshot.AppendOffset);
    }

    /// <summary>
    /// Verifies that a partially written header fails explicitly instead of being normalized into a silent data-loss state.
    /// </summary>
    [Fact]
    public void Recovery_Rejects_Partial_Header_Explicitly()
    {
        using var harness = CreateHarness();

        harness.ReplaceWithRawHeader(new byte[] { 0x01, 0x02, 0x03 });
        harness.Reopen();

        Assert.ThrowsAny<Exception>(() => harness.Refresh());
    }

    /// <summary>
    /// Verifies that a truncated tail reduces logical readability instead of leaving the sequence in an internally inconsistent state.
    /// </summary>
    [Fact]
    public void Recovery_Normalizes_State_After_Truncation_In_The_Last_Item()
    {
        using var harness = CreateHarness();

        harness.Append(harness.CreateValue("first"));
        harness.Append(harness.CreateValue("second"));
        harness.Append(harness.CreateValue("third"));
        harness.Flush();
        harness.Build();

        var beforeTruncate = harness.Snapshot();
        Assert.Equal(3, beforeTruncate.Count);
        Assert.True(beforeTruncate.StreamLength > 8);

        harness.TruncateDataFile(beforeTruncate.StreamLength - Math.Min(8, (int)Math.Max(1, beforeTruncate.StreamLength / 4)));
        harness.Reopen();
        harness.Refresh();

        var afterRecovery = harness.Snapshot();
        Assert.InRange(afterRecovery.Count, 0, 2);
        Assert.True(afterRecovery.StreamLength >= afterRecovery.AppendOffset);
        Assert.DoesNotContain("third", afterRecovery.Items.Select(harness.ReadPayload));
    }
}
