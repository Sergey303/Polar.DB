using System.Buffers.Binary;
using Polar.DB.ExternalKey;
using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public class GuidExternalIndexFormatValidationTests
{
    [Theory]
    [InlineData(1, 0L)]                 // Partial header
    [InlineData(7, 0L)]                 // Partial header
    [InlineData(8, -1L)]                // Invalid count
    [InlineData(74, long.MaxValue)]     // Avoid length arithmetic overflow
    public void Malformed_Guid_Index_Is_Rejected(int length, long count)
    {
        using var sequence = EmptySequence();
        using var keyStream = new MemoryStream(new byte[length], writable: true);
        if (length >= sizeof(long))
        {
            Span<byte> bytes = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64LittleEndian(bytes, count);
            keyStream.Write(bytes);
            keyStream.Position = 0;
        }

        Assert.Throws<InvalidDataException>(() =>
            new ExternalKeyIndex<Guid>(
                StreamGen(keyStream),
                sequence,
                _ => Array.Empty<Guid>()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Uninitialized_Or_Empty_Existing_Guid_Index_Builds_Natively(bool hasHeader)
    {
        using var sequence = EmptySequence();
        using var keyStream = new MemoryStream();
        if (hasHeader)
        {
            using var writer = new BinaryWriter(keyStream, System.Text.Encoding.UTF8, leaveOpen: true);
            writer.Write(0L);
            writer.Flush();
            keyStream.Position = 0;
        }

        using (var index = new ExternalKeyIndex<Guid>(
            StreamGen(keyStream), sequence, _ => Array.Empty<Guid>()))
        {
            index.Build();
            index.Refresh();
            Assert.Empty(index.GetManyByValue(Guid.Empty));
            Assert.Equal(sizeof(long), keyStream.Length);
        }
    }

    private static USequence EmptySequence()
    {
        var sequence = new USequence(
            new PType(PTypeEnumeration.integer), null,
            () => new MemoryStream(), _ => false, optimise: false);
        sequence.SetPrimaryKey<int>(value => (int)value);
        sequence.Build();
        return sequence;
    }

    private static Func<Stream> StreamGen(Stream keyStream)
    {
        var calls = 0;
        return () => calls++ == 0 ? keyStream : new MemoryStream();
    }
}
