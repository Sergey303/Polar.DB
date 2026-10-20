using System.Buffers.Binary;
using Polar.DB.ExternalKey;
using Polar.Universal;
using Xunit;
namespace Polar.DB.Tests;
public sealed class GuidInterruptedIndexRecoveryTests
{
    private static readonly Guid First = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Second = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Tag = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    [Theory]
    [InlineData("key-tail")]
    [InlineData("key-short-header")]
    [InlineData("offset-short-header")]
    [InlineData("key-stale-header")]
    [InlineData("offset-tail")]
    [InlineData("offset-stale-header")]
    [InlineData("interrupted-build")]
    [InlineData("interrupted-clear")]
    public void Interrupted_Guid_Index_Is_Rebuilt_Without_Rewriting_Source(string failure)
    {
        using var store = new Store();
        store.Seed();
        byte[] sourceBefore = File.ReadAllBytes(store.DataPath);
        string keyPath = store.ExternalPath(0);
        string offsetPath = store.ExternalPath(1);
        switch (failure)
        {
            case "key-short-header":
                File.WriteAllBytes(keyPath, new byte[] { 0x12 });
                break;
            case "offset-short-header":
                File.WriteAllBytes(offsetPath, new byte[] { 0x12 });
                break;
            case "key-tail":
                using (var file = File.Open(keyPath, FileMode.Append))
                    file.Write(new byte[] { 0x12, 0x34, 0x56 });
                break;
            case "key-stale-header":
                WriteCount(keyPath, 1);
                break;
            case "offset-tail":
                using (var file = File.Open(offsetPath, FileMode.Append))
                    file.Write(new byte[] { 0x12, 0x34, 0x56 });
                break;
            case "offset-stale-header":
                WriteCount(offsetPath, 1);
                break;
            case "interrupted-build":
                using (var file = File.Open(keyPath, FileMode.Open, FileAccess.Write))
                {
                    file.Position = 8;
                    file.Write(Second.ToByteArray());
                }
                File.WriteAllText(keyPath + ".rebuild-required", "rebuild-index-from-primary");
                break;
            case "interrupted-clear":
                File.WriteAllBytes(keyPath, new byte[8]);
                File.WriteAllBytes(offsetPath, new byte[8]);
                File.WriteAllText(keyPath + ".rebuild-required", "rebuild-index-from-primary");
                break;
        }
        var opened = store.Open();
        using (opened.Sequence)
        {
            opened.Sequence.Refresh();
            Assert.Equal(new[] { First, Second }, Ids(opened.Index.GetManyByValue(Tag)));
            Assert.Equal(First, ((object[])opened.Sequence.GetByKey(First)!)[0]);
        }
        Assert.Equal(sourceBefore, File.ReadAllBytes(store.DataPath));
        Assert.False(File.Exists(keyPath + ".rebuild-required"));
        Assert.Equal(40, new FileInfo(keyPath).Length);
        Assert.Equal(24, new FileInfo(offsetPath).Length);
    }
    [Fact]
    public void Guid_Key_Update_Delete_And_Reopen_Preserve_External_Lookups()
    {
        using var store = new Store();
        store.Seed();
        var opened = store.Open();
        using (opened.Sequence)
        {
            opened.Sequence.Refresh();
            opened.Sequence.AppendElement(Row(First, Second, false));
            Assert.Equal(new[] { Second }, Ids(opened.Index.GetManyByValue(Tag)));
            opened.Sequence.AppendElement(Row(Second, Tag, true));
            Assert.Empty(opened.Index.GetManyByValue(Tag));
            opened.Sequence.Build();
        }
        var reopened = store.Open();
        using (reopened.Sequence)
        {
            reopened.Sequence.Refresh();
            Assert.Empty(reopened.Index.GetManyByValue(Tag));
            Assert.Equal(new[] { First }, Ids(reopened.Index.GetManyByValue(Second)));
        }
    }
    private static object[] Row(Guid id, Guid tag, bool deleted) =>
        new object[] { id, tag, deleted };
    private static Guid[] Ids(IEnumerable<object> rows) =>
        rows.Cast<object[]>().Select(row => (Guid)row[0]).OrderBy(id => id).ToArray();
    private static void WriteCount(string path, long count)
    {
        using var file = File.Open(path, FileMode.Open, FileAccess.Write);
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, count);
        file.Write(bytes);
    }
    private sealed class Store : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(),
            "polar-guid-crash-" + Guid.NewGuid().ToString("N"));
        internal Store() => Directory.CreateDirectory(_folder);
        internal string DataPath => Path.Combine(_folder, "data-00.bin");
        internal string ExternalPath(int index) =>
            Path.Combine(_folder, $"external-{index:00}.bin");
        internal (USequence Sequence, ExternalKeyIndex<Guid> Index) Open()
        {
            int data = 0;
            Stream DataStream() => new FileStream(
                Path.Combine(_folder, $"data-{data++:00}.bin"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            int external = 0;
            Stream ExternalStream() => new FileStream(ExternalPath(external++),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            var type = new PTypeRecord(
                new NamedType("id", new PType(PTypeEnumeration.guid)),
                new NamedType("tag", new PType(PTypeEnumeration.guid)),
                new NamedType("deleted", new PType(PTypeEnumeration.boolean)));
            var sequence = new USequence(type, Path.Combine(_folder, "state.bin"),
                DataStream, row => (bool)((object[])row)[2], optimise: false);
            sequence.SetPrimaryKey<Guid>(row => (Guid)((object[])row)[0]);
            var index = new ExternalKeyIndex<Guid>(ExternalStream, sequence,
                row => new[] { (Guid)((object[])row)[1] });
            sequence.uindexes = new IUIndex[] { index };
            return (sequence, index);
        }
        internal void Seed()
        {
            var (sequence, _) = Open();
            using (sequence)
            {
                sequence.Load(new object[] { Row(First, Tag, false), Row(Second, Tag, false) });
                sequence.Build();
            }
        }
        public void Dispose() => Directory.Delete(_folder, recursive: true);
    }
}
