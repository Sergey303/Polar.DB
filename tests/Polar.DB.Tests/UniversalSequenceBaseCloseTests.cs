using Xunit;

namespace Polar.DB.Tests;

public class UniversalSequenceBaseCloseTests
{
    [Fact]
    public void Close_Flushes_Header_And_Allows_Reopen_With_Consistent_State()
    {
        using var scope = new UniversalSequenceBaseTestHelpers.TempFileScope("close.bin");
        long firstOffset;
        long secondOffset;

        using (var writerStream = scope.Open())
        {
            var sequence = UniversalSequenceBaseTestHelpers.CreateVariableSequence(writerStream);
            sequence.Clear();
            firstOffset = sequence.AppendElement(new object[] { 1, "A" });
            secondOffset = sequence.AppendElement(new object[] { 2, "BB" });
            sequence.Close();
        }

        using (var headerStream = File.OpenRead(scope.FilePath))
        {
            var headerBytes = new byte[8];
            Assert.Equal(8, headerStream.Read(headerBytes, 0, headerBytes.Length));
            Assert.Equal(2L, BitConverter.ToInt64(headerBytes, 0));
        }

        using (var readerStream = scope.Open(FileMode.Open))
        {
            var reopened = UniversalSequenceBaseTestHelpers.CreateVariableSequence(readerStream);

            Assert.Equal(2L, reopened.Count());
            var first = Assert.IsType<object[]>(reopened.GetElement(firstOffset));
            var second = Assert.IsType<object[]>(reopened.GetElement(secondOffset));

            Assert.Equal(1, (int)first[0]);
            Assert.Equal("A", (string)first[1]);
            Assert.Equal(2, (int)second[0]);
            Assert.Equal("BB", (string)second[1]);
        }
    }
}
