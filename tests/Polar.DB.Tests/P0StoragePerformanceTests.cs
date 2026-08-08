using Polar.DB;
using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public sealed class P0StoragePerformanceTests
{
    [Fact]
    public void ExternalIndex_ReplacesBoxedPrimaryWithoutLeakingOldKey_AndDeduplicatesKeys()
    {
        using var fixture = new StoreFixture();
        using var sequence = fixture.OpenWithExternalIndex(out var external);

        sequence.Load(new object[]
        {
            Row(1, 7, false, "first"),
            Row(2, 7, false, "second")
        });
        sequence.Build();

        Assert.Equal(2, external.GetManyByKey(7).Count());

        sequence.AppendElement(Row(1, 8, false, "replacement"));

        var oldKey = external.GetManyByKey(7).Select(Name).ToArray();
        Assert.Equal(new[] { "second" }, oldKey);
        Assert.Equal(new[] { "replacement" }, external.GetManyByKey(8).Select(Name).ToArray());

        sequence.AppendElement(Row(1, 8, true, "deleted"));
        Assert.Empty(external.GetManyByKey(8));
    }

    [Fact]
    public void PrimarySnapshot_PersistsOnlyStaleOffsetsAndRestoresLatestRows()
    {
        using var fixture = new StoreFixture();
        using (var sequence = fixture.OpenPrimaryOnly())
        {
            sequence.Load(new object[]
            {
                Row(1, 10, false, "old"),
                Row(1, 10, false, "latest"),
                Row(2, 20, false, "other")
            });
            sequence.Build();
            Assert.Equal(new[] { "latest", "other" }, sequence.ElementValues().Select(Name).OrderBy(x => x).ToArray());
        }

        Assert.True(new FileInfo(fixture.StatePath).Length > sizeof(long) * 2);

        fixture.ResetStreams();
        using var reopened = fixture.OpenPrimaryOnly();
        reopened.Refresh();

        Assert.Equal(3, reopened.Count());
        Assert.Equal("latest", Name(reopened.GetByKey(1)!));
        Assert.Equal(new[] { "latest", "other" }, reopened.ElementValues().Select(Name).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void CleanSnapshot_ReopenUsesStateHintWithoutScanningVariablePayload()
    {
        using var fixture = new StoreFixture();
        using (var sequence = fixture.OpenPrimaryOnly())
        {
            sequence.Load(Enumerable.Range(0, 2000)
                .Select(i => Row(i, i % 17, false, new string((char)('a' + i % 20), 64)))
                .Cast<object>());
            sequence.Build();
        }

        fixture.ResetStreams(countPayloadReads: true);
        using var reopened = fixture.OpenPrimaryOnly();
        reopened.Refresh();

        Assert.Equal(2000, reopened.Count());
        Assert.NotNull(fixture.PayloadCounter);
        Assert.True(
            fixture.PayloadCounter!.BytesRead <= 32,
            $"Expected header-only payload reads during clean reopen, got {fixture.PayloadCounter.BytesRead} bytes.");
    }

    [Fact]
    public void InvalidStateHint_FallsBackToRecoveryWhenPayloadIsTruncated()
    {
        using var fixture = new StoreFixture();
        using (var sequence = fixture.OpenPrimaryOnly())
        {
            sequence.Load(new object[]
            {
                Row(1, 10, false, "one"),
                Row(2, 20, false, new string('x', 256))
            });
            sequence.Build();
        }

        var payloadPath = fixture.StreamPath(0);
        using (var stream = new FileStream(payloadPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            stream.SetLength(stream.Length - 1);

        fixture.ResetStreams();
        using var recovered = fixture.OpenPrimaryOnly();
        recovered.Refresh();

        Assert.Equal(1, recovered.Count());
        Assert.NotNull(recovered.GetByKey(1));
        Assert.Null(recovered.GetByKey(2));
    }

    private static object[] Row(int id, int external, bool deleted, string name) =>
        new object[] { id, external, deleted, name };

    private static string Name(object row) => (string)((object[])row)[3];

    private sealed class StoreFixture : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "polar-p0-" + Guid.NewGuid().ToString("N"));
        private int _streamNumber;
        private bool _countPayloadReads;

        internal StoreFixture()
        {
            Directory.CreateDirectory(_dir);
        }

        internal string StatePath => Path.Combine(_dir, "state.bin");
        internal CountingStream? PayloadCounter { get; private set; }

        internal string StreamPath(int number) => Path.Combine(_dir, "f" + number + ".bin");

        internal void ResetStreams(bool countPayloadReads = false)
        {
            _streamNumber = 0;
            _countPayloadReads = countPayloadReads;
            PayloadCounter = null;
        }

        internal USequence OpenPrimaryOnly()
        {
            var sequence = NewSequence();
            return sequence;
        }

        internal USequence OpenWithExternalIndex(out EKeyIndex index)
        {
            var sequence = NewSequence();
            index = new EKeyIndex(
                NextStream,
                sequence,
                value =>
                {
                    var external = (int)((object[])value)[1];
                    return new IComparable[] { external, external };
                },
                key => (int)key);
            sequence.uindexes = new IUIndex[] { index };
            return sequence;
        }

        private USequence NewSequence()
        {
            var type = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.integer)),
                new NamedType("external", new PType(PTypeEnumeration.integer)),
                new NamedType("deleted", new PType(PTypeEnumeration.boolean)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)));

            var sequence = new USequence(
                type,
                StatePath,
                NextStream,
                value => (bool)((object[])value)[2]);
            sequence.SetPrimaryKey<int>(value => (int)((object[])value)[0], value => value);
            return sequence;
        }

        private Stream NextStream()
        {
            var number = _streamNumber++;
            var inner = new FileStream(
                StreamPath(number),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite);

            if (_countPayloadReads && number == 0)
            {
                PayloadCounter = new CountingStream(inner);
                return PayloadCounter;
            }

            return inner;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_dir, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class CountingStream : Stream
    {
        private readonly Stream _inner;

        internal CountingStream(Stream inner)
        {
            _inner = inner;
        }

        internal long BytesRead { get; private set; }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = _inner.Read(buffer);
            BytesRead += read;
            return read;
        }

        public override int ReadByte()
        {
            var value = _inner.ReadByte();
            if (value >= 0) BytesRead++;
            return value;
        }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
