using System.Buffers.Binary;
using Polar.DB;
using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public sealed class P1StoragePerformanceTests
{
    [Fact]
    public void ByteFlowSkip_ConsumesNestedPayloadWithoutChangingBinaryFormat()
    {
        var nestedRecord = new PTypeRecord(
            new NamedType("value", new PType(PTypeEnumeration.integer)),
            new NamedType("text", new PType(PTypeEnumeration.sstring)));
        var type = new PTypeRecord(
            new NamedType("character", new PType(PTypeEnumeration.character)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)),
            new NamedType("fixed", new PTypeFString(8)),
            new NamedType("items", new PTypeSequence(nestedRecord)),
            new NamedType("choice", new PTypeUnion(
                new NamedType("none", new PType(PTypeEnumeration.none)),
                new NamedType("text", new PType(PTypeEnumeration.sstring)))));

        object value = new object[]
        {
            'Ж',
            "hello-世界",
            "fixed",
            new object[]
            {
                new object[] { 7, "seven" },
                new object[] { 8, "восемь" }
            },
            new object[] { 1, "variant-value" }
        };

        byte[] payload;
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
                ByteFlow.Serialize(writer, value, type);
            payload = stream.ToArray();
        }

        using (var stream = new MemoryStream(payload, writable: false))
        using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            ByteFlow.Skip(reader, type);
            Assert.Equal(stream.Length, stream.Position);
        }

        using var truncated = new MemoryStream(payload[..^1], writable: false);
        using var truncatedReader = new BinaryReader(truncated, System.Text.Encoding.UTF8, leaveOpen: true);
        Assert.Throws<EndOfStreamException>(() => ByteFlow.Skip(truncatedReader, type));
    }

    [Fact]
    public void FixedInt32BulkWrite_UsesBoundedChunksAndKeepsLittleEndianLayout()
    {
        var values = Enumerable.Range(0, 100_000).Select(i => unchecked(i * 104729)).ToArray();
        using var stream = new WriteLimitStream(64 * 1024);
        using var sequence = new UniversalSequenceBase(new PType(PTypeEnumeration.integer), stream);

        sequence.ReplaceWithFixedInt32Array(values);

        Assert.True(stream.MaxObservedWrite <= 64 * 1024);
        Assert.Equal(values.LongLength, sequence.Count());
        Assert.Equal(values[0], (int)sequence.GetByIndex(0));
        Assert.Equal(values[^1], (int)sequence.GetByIndex(values.Length - 1));

        byte[] raw = stream.ToArray();
        Assert.Equal(values.LongLength, BinaryPrimitives.ReadInt64LittleEndian(raw.AsSpan(0, sizeof(long))));
        Assert.Equal(values[12345], BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(sizeof(long) + 12345 * sizeof(int), sizeof(int))));
    }

    [Fact]
    public void FixedInt64BulkWrite_UsesBoundedChunksAndKeepsLittleEndianLayout()
    {
        var values = Enumerable.Range(0, 100_000)
            .Select(i => unchecked(((long)i << 33) ^ (uint)(i * 7919)))
            .ToArray();
        using var stream = new WriteLimitStream(64 * 1024);
        using var sequence = new UniversalSequenceBase(new PType(PTypeEnumeration.longinteger), stream);

        sequence.ReplaceWithFixedInt64Array(values);

        Assert.True(stream.MaxObservedWrite <= 64 * 1024);
        Assert.Equal(values.LongLength, sequence.Count());
        Assert.Equal(values[0], (long)sequence.GetByIndex(0));
        Assert.Equal(values[^1], (long)sequence.GetByIndex(values.Length - 1));

        byte[] raw = stream.ToArray();
        Assert.Equal(values.LongLength, BinaryPrimitives.ReadInt64LittleEndian(raw.AsSpan(0, sizeof(long))));
        Assert.Equal(values[54321], BinaryPrimitives.ReadInt64LittleEndian(raw.AsSpan(sizeof(long) + 54321 * sizeof(long), sizeof(long))));
    }

    [Fact]
    public void UIndex_AppendsWithoutComparerWork_AndBuildDoesNotDuplicateDynamicRows()
    {
        Func<Stream> streamGen = () => new MemoryStream();
        var type = new PTypeRecord(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("group", new PType(PTypeEnumeration.integer)),
            new NamedType("deleted", new PType(PTypeEnumeration.boolean)));

        using var sequence = new USequence(type, null, streamGen, value => (bool)((object[])value)[2]);
        sequence.SetPrimaryKey<int>(value => (int)((object[])value)[0], value => value);

        int comparerCalls = 0;
        var comparer = Comparer<object>.Create((left, right) =>
        {
            comparerCalls++;
            return ((int)((object[])left)[1]).CompareTo((int)((object[])right)[1]);
        });
        using var index = new UIndex(
            streamGen,
            sequence,
            _ => true,
            value => (int)((object[])value)[1],
            comparer);
        sequence.uindexes = new IUIndex[] { index };

        sequence.Load(new object[] { Row(0, 3) });
        sequence.Build();
        comparerCalls = 0;

        for (int i = 1; i <= 100; i++)
            sequence.AppendElement(Row(i, i % 5));

        Assert.Equal(0, comparerCalls);

        object sample = Row(-1, 3);
        Assert.Equal(21, sequence.GetAllBySample(0, sample).Count());
        Assert.True(comparerCalls > 0);

        sequence.Build();
        Assert.Equal(21, sequence.GetAllBySample(0, sample).Count());
    }

    private static object[] Row(int id, int group) => new object[] { id, group, false };

    private sealed class WriteLimitStream : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly int _maxWrite;

        internal WriteLimitStream(int maxWrite)
        {
            _maxWrite = maxWrite;
        }

        internal int MaxObservedWrite { get; private set; }
        internal byte[] ToArray() => _inner.ToArray();

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _inner.Read(buffer);
        public override int ReadByte() => _inner.ReadByte();
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            ObserveWrite(count);
            _inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ObserveWrite(buffer.Length);
            _inner.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            ObserveWrite(1);
            _inner.WriteByte(value);
        }

        private void ObserveWrite(int count)
        {
            MaxObservedWrite = Math.Max(MaxObservedWrite, count);
            if (count > _maxWrite)
                throw new InvalidOperationException($"Write of {count} bytes exceeded {_maxWrite}-byte test limit.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
