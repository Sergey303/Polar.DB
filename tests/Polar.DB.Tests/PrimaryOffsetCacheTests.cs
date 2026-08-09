using Polar.DB;
using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public sealed class PrimaryOffsetCacheTests
{
    [Fact]
    public void SnapshotCache_KeepsLatestDuplicateAndServesOffsetsFromMemory()
    {
        using var fixture = new StoreFixture();
        using (var sequence = fixture.OpenPrimaryOnly())
        {
            sequence.Load(new object[]
            {
                Row(1, false, "old"),
                Row(1, false, "snapshot-latest"),
                Row(2, false, "other")
            });
            sequence.Build();

            Assert.Equal("snapshot-latest", Name(sequence.GetByKey(1)!));
            Assert.Equal("other", Name(sequence.GetByKey(2)!));
        }

        fixture.ResetStreams(countOffsetReads: true);
        using var reopened = fixture.OpenPrimaryOnly();
        reopened.Refresh();

        Assert.Equal("snapshot-latest", Name(reopened.GetByKey(1)!));
        Assert.NotNull(fixture.OffsetCounter);
        fixture.OffsetCounter!.Reset();

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal("snapshot-latest", Name(reopened.GetByKey(1)!));
            Assert.Equal("other", Name(reopened.GetByKey(2)!));
        }

        Assert.Equal(0L, fixture.OffsetCounter.BytesRead);
    }

    [Fact]
    public void DynamicAndReplayedTail_OverrideCachedSnapshot_LastWriteWins()
    {
        using var fixture = new StoreFixture();
        using (var sequence = fixture.OpenPrimaryOnly())
        {
            sequence.Load(new object[]
            {
                Row(1, false, "snapshot"),
                Row(2, false, "other")
            });
            sequence.Build();

            sequence.AppendElement(Row(1, false, "dynamic-latest"));
            sequence.Flush();
            Assert.Equal("dynamic-latest", Name(sequence.GetByKey(1)!));
        }

        fixture.ResetStreams();
        using (var replayed = fixture.OpenPrimaryOnly())
        {
            replayed.Refresh();
            Assert.Equal("dynamic-latest", Name(replayed.GetByKey(1)!));
            Assert.Equal("other", Name(replayed.GetByKey(2)!));

            replayed.AppendElement(Row(1, true, "deleted"));
            replayed.Flush();
            Assert.Null(replayed.GetByKey(1));
        }

        fixture.ResetStreams();
        using var deletedAfterReplay = fixture.OpenPrimaryOnly();
        deletedAfterReplay.Refresh();
        Assert.Null(deletedAfterReplay.GetByKey(1));
        Assert.Equal("other", Name(deletedAfterReplay.GetByKey(2)!));
    }

    private static object[] Row(int id, bool deleted, string name) =>
        new object[] { id, deleted, name };

    private static string Name(object row) => (string)((object[])row)[2];

    private sealed class StoreFixture : IDisposable
    {
        private readonly string _dir = Path.Combine(
            Path.GetTempPath(),
            "polar-primary-offset-cache-" + Guid.NewGuid().ToString("N"));
        private int _streamNumber;
        private bool _countOffsetReads;

        internal StoreFixture()
        {
            Directory.CreateDirectory(_dir);
        }

        internal string StatePath => Path.Combine(_dir, "state.bin");
        internal CountingStream? OffsetCounter { get; private set; }

        internal string StreamPath(int number) => Path.Combine(_dir, "f" + number + ".bin");

        internal void ResetStreams(bool countOffsetReads = false)
        {
            _streamNumber = 0;
            _countOffsetReads = countOffsetReads;
            OffsetCounter = null;
        }

        internal USequence OpenPrimaryOnly()
        {
            var type = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.integer)),
                new NamedType("deleted", new PType(PTypeEnumeration.boolean)),
                new NamedType("name", new PType(PTypeEnumeration.sstring)));

            var sequence = new USequence(
                type,
                StatePath,
                NextStream,
                value => (bool)((object[])value)[1]);
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

            // USequence opens payload first, then primary hashes, then primary offsets.
            if (_countOffsetReads && number == 2)
            {
                OffsetCounter = new CountingStream(inner);
                return OffsetCounter;
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

        internal void Reset() => BytesRead = 0L;

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
