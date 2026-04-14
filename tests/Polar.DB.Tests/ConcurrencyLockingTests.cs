using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Contains smoke tests for file locking and restart behavior around file-backed sequence storage.
/// </summary>
/// <remarks>
/// These are intentionally small because cross-platform filesystem locking semantics vary. The tests still provide a
/// useful baseline for the operational problem where data/state files can remain locked by a previous process.
/// </remarks>
public class ConcurrencyLockingTests
{
    /// <summary>
    /// Verifies that an exclusive file handle prevents a second exclusive writer from opening the same file.
    /// </summary>
    [Fact]
    public void File_Open_With_Exclusive_Lock_Prevents_Second_Writer_When_FileShare_None_Is_Used()
    {
        using var temp = new StorageCorruptionHelpers.TempDirectory();
        string path = temp.File("locked.bin");

        using var first = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        Assert.Throws<IOException>(() =>
        {
            using var second = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        });
    }

    /// <summary>
    /// Verifies that a file-backed sequence can be reopened normally after the previous stream is disposed.
    /// </summary>
    [Fact]
    public void FileBacked_Sequence_Can_Be_Reopened_After_Previous_Stream_Is_Disposed()
    {
        using var temp = new StorageCorruptionHelpers.TempDirectory();

        using (var stream = temp.Open("restart.bin", FileMode.OpenOrCreate))
        {
            var sequence = StorageCorruptionHelpers.CreateInt32Sequence(stream);
            sequence.Clear();
            sequence.AppendElement(1);
            sequence.Flush();
        }

        using (var stream = temp.Open("restart.bin", FileMode.Open))
        {
            var sequence = StorageCorruptionHelpers.CreateInt32Sequence(stream);
            Assert.Equal(1L, sequence.Count());
            Assert.Equal(1, sequence.GetByIndex(0));
        }
    }
}
