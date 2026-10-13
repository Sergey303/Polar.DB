using Polar.DB.ExternalKey;
using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public class ExternalKeyGuidStorageTests
{
    private static readonly Guid First = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Second = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void New_Guid_External_Index_Uses_Native_16_Byte_Keys()
    {
        using var env = new SequenceEnvironment();
        var keyStream = new MemoryStream();
        var offsetStream = new MemoryStream();

        using (var index = new ExternalKeyIndex<Guid>(
            StreamGen(keyStream, offsetStream),
            env.Sequence,
            row => new[] { Guid.Parse((string)((object[])row)[1]) }))
        {
            index.Build();
            index.Refresh();

            Assert.Equal(40L, keyStream.Length);
            Assert.Equal(new[] { 1 }, QueryIds(index, First));
            Assert.Equal(new[] { 2 }, QueryIds(index, Second));
        }
    }

    [Fact]
    public void Legacy_String_Backed_Guid_Index_Remains_Readable()
    {
        using var env = new SequenceEnvironment();
        long[] offsets = env.GetOffsets();

        var keyStream = new MemoryStream();
        var offsetStream = new MemoryStream();
        SeedLegacyGuidIndex(keyStream, offsetStream, offsets[0], offsets[1]);

        using (var index = new ExternalKeyIndex<Guid>(
            StreamGen(keyStream, offsetStream),
            env.Sequence,
            row => new[] { Guid.Parse((string)((object[])row)[1]) }))
        {
            index.Build();
            index.Refresh();

            Assert.Equal(new[] { 1 }, QueryIds(index, First));
            Assert.Equal(new[] { 2 }, QueryIds(index, Second));
            Assert.Equal(74L, keyStream.Length);
        }
    }

    private static void SeedLegacyGuidIndex(
        MemoryStream keyStream,
        MemoryStream offsetStream,
        long firstOffset,
        long secondOffset)
    {
        using (var writer = new BinaryWriter(keyStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(2L);
            writer.Write(First.ToString("N"));
            writer.Write(Second.ToString("N"));
            writer.Flush();
        }

        using (var writer = new BinaryWriter(offsetStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(2L);
            writer.Write(firstOffset);
            writer.Write(secondOffset);
            writer.Flush();
        }

        keyStream.Position = 0L;
        offsetStream.Position = 0L;
    }

    private static Func<Stream> StreamGen(Stream first, Stream second)
    {
        int index = 0;
        return () => index++ switch
        {
            0 => first,
            1 => second,
            _ => throw new InvalidOperationException("Unexpected stream request.")
        };
    }

    private static int[] QueryIds(ExternalKeyIndex<Guid> index, Guid key) =>
        index.GetManyByValue(key)
            .Cast<object[]>()
            .Select(row => (int)row[0])
            .ToArray();

    private sealed class SequenceEnvironment : IDisposable
    {
        private readonly List<Stream> _streams = new();

        public SequenceEnvironment()
        {
            Sequence = new USequence(
                new PTypeRecord(
                    new NamedType("id", new PType(PTypeEnumeration.integer)),
                    new NamedType("guid", new PType(PTypeEnumeration.sstring))),
                null,
                CreateStream,
                _ => false,
                optimise: false);
            Sequence.SetPrimaryKey<int>(row => (int)((object[])row)[0]);
            Sequence.AppendElement(new object[] { 1, First.ToString("D") });
            Sequence.AppendElement(new object[] { 2, Second.ToString("D") });
            Sequence.Build();
        }

        public USequence Sequence { get; }

        public long[] GetOffsets()
        {
            var offsets = new List<long>();
            Sequence.Scan((offset, _) =>
            {
                offsets.Add(offset);
                return true;
            });
            return offsets.ToArray();
        }

        private Stream CreateStream()
        {
            var stream = new MemoryStream();
            _streams.Add(stream);
            return stream;
        }

        public void Dispose()
        {
            Sequence.Dispose();
            foreach (Stream stream in _streams)
                stream.Dispose();
        }
    }
}
