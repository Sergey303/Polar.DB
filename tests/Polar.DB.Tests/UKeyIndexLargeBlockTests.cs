using System.Reflection;
using Xunit;

namespace Polar.DB.Tests;

/// <summary>
/// Large-block regression tests for internal <c>UKeyIndex</c> behavior.
///
/// <para>
/// These tests focus on the cases that often hide boundary bugs:
/// long same-hash ranges, long duplicate-key ranges, refresh after build,
/// and append-after-build without full rebuild.
/// </para>
/// </summary>
public class UKeyIndexLargeBlockTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetByKey_LargeSameHashBlock_Finds_First_Middle_And_Last_Distinct_Keys(bool keysInMemory)
    {
        using var scope = new Scope();
        var index = scope.CreateIndex(
            record => (string)((object[])record)[1],
            _ => 1,
            keysInMemory);

        var rows = Enumerable.Range(0, 120)
            .Select(i => Row(i + 1, $"Name-{i:D3}"))
            .Cast<object>()
            .ToArray();

        scope.Sequence.Load(rows);
        index.Build();

        AssertRecord(index.GetByKey("Name-000"), 1, "Name-000");
        AssertRecord(index.GetByKey("Name-059"), 60, "Name-059");
        AssertRecord(index.GetByKey("Name-119"), 120, "Name-119");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetByKey_LargeSameHashBlock_Missing_Key_Returns_Null(bool keysInMemory)
    {
        using var scope = new Scope();
        var index = scope.CreateIndex(
            record => (string)((object[])record)[1],
            _ => 1,
            keysInMemory);

        var rows = Enumerable.Range(0, 100)
            .Select(i => Row(i + 1, $"Key-{i:D3}"))
            .Cast<object>()
            .ToArray();

        scope.Sequence.Load(rows);
        index.Build();

        Assert.Null(index.GetByKey("Key-999"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetByKey_LargeDuplicateKeyBlock_Returns_Matching_Record_Within_Duplicate_Range(bool keysInMemory)
    {
        using var scope = new Scope();
        var index = scope.CreateIndex(
            record => (string)((object[])record)[1],
            _ => 1,
            keysInMemory);

        var rows = new List<object>();

        for (int i = 0; i < 20; i++)
            rows.Add(Row(100 + i, $"Unique-L-{i:D2}"));

        for (int i = 0; i < 50; i++)
            rows.Add(Row(1000 + i, "DUPLICATE"));

        for (int i = 0; i < 20; i++)
            rows.Add(Row(200 + i, $"Unique-R-{i:D2}"));

        scope.Sequence.Load(rows.ToArray());
        index.Build();

        var found = Assert.IsType<object[]>(index.GetByKey("DUPLICATE"));

        Assert.Equal("DUPLICATE", (string)found[1]);
        Assert.InRange((int)found[0], 1000, 1049);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Refresh_On_LargeSameHashBlock_Preserves_Boundary_Lookups(bool keysInMemory)
    {
        using var scope = new Scope();
        var index = scope.CreateIndex(
            record => (string)((object[])record)[1],
            _ => 1,
            keysInMemory);

        var rows = Enumerable.Range(0, 150)
            .Select(i => Row(i + 1, $"Collision-{i:D3}"))
            .Cast<object>()
            .ToArray();

        scope.Sequence.Load(rows);
        index.Build();
        index.Refresh();

        AssertRecord(index.GetByKey("Collision-000"), 1, "Collision-000");
        AssertRecord(index.GetByKey("Collision-074"), 75, "Collision-074");
        AssertRecord(index.GetByKey("Collision-149"), 150, "Collision-149");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OnAppendElement_AfterBuild_OnLargeSameHashBlock_Finds_New_Key_Without_Rebuild(bool keysInMemory)
    {
        using var scope = new Scope();
        var index = scope.CreateIndex(
            record => (string)((object[])record)[1],
            _ => 1,
            keysInMemory);

        var rows = Enumerable.Range(0, 80)
            .Select(i => Row(i + 1, $"Base-{i:D3}"))
            .Cast<object>()
            .ToArray();

        scope.Sequence.Load(rows);
        index.Build();

        long appendedOffset = scope.Sequence.AppendElement(Row(999, "Appended-Key"));
        index.OnAppendElement(Row(999, "Appended-Key"), appendedOffset);

        AssertRecord(index.GetByKey("Base-000"), 1, "Base-000");
        AssertRecord(index.GetByKey("Base-079"), 80, "Base-079");
        AssertRecord(index.GetByKey("Appended-Key"), 999, "Appended-Key");
    }

    private static object[] Row(int id, string name) => new object[] { id, name };

    private static void AssertRecord(object? value, int expectedId, string expectedName)
    {
        var record = Assert.IsType<object[]>(value);
        Assert.Equal(expectedId, (int)record[0]);
        Assert.Equal(expectedName, (string)record[1]);
    }

    private sealed class Scope : IDisposable
    {
        private static readonly PTypeRecord RecordType = new(
            new NamedType("id", new PType(PTypeEnumeration.integer)),
            new NamedType("name", new PType(PTypeEnumeration.sstring)));

        private readonly string _tempDir;
        private int _fileNo;

        public Scope()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PolarDbTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            Sequence = new USequence(
                RecordType,
                Path.Combine(_tempDir, "state.bin"),
                StreamGen,
                _ => false,
                value => (int)((object[])value)[0],
                key => (int)key,
                optimise: false);
        }

        public USequence Sequence { get; }

        public UKeyIndexAdapter CreateIndex(
            Func<object, IComparable> keyFunc,
            Func<IComparable, int> hashFunc,
            bool keysInMemory)
        {
            return new UKeyIndexAdapter(StreamGen, Sequence, keyFunc, hashFunc, keysInMemory);
        }

        private Stream StreamGen()
        {
            var path = Path.Combine(_tempDir, $"f{_fileNo++}.bin");
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        public void Dispose()
        {
            try { Sequence.Close(); } catch { }
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch { }
        }
    }

    private sealed class UKeyIndexAdapter
    {
        private static readonly Type UKeyIndexType =
            typeof(USequence).Assembly.GetType("Polar.DB.UKeyIndex", throwOnError: true)!;

        private readonly object _instance;
        private readonly MethodInfo _build;
        private readonly MethodInfo _refresh;
        private readonly MethodInfo _getByKey;
        private readonly MethodInfo _onAppendElement;

        public UKeyIndexAdapter(
            Func<Stream> streamGen,
            USequence sequence,
            Func<object, IComparable> keyFunc,
            Func<IComparable, int> hashFunc,
            bool keysInMemory)
        {
            _instance = Activator.CreateInstance(
                UKeyIndexType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { streamGen, sequence, keyFunc, hashFunc, keysInMemory },
                culture: null)!;

            _build = GetMethod("Build");
            _refresh = GetMethod("Refresh");
            _getByKey = GetMethod("GetByKey");
            _onAppendElement = GetMethod("OnAppendElement");
        }

        public void Build() => _build.Invoke(_instance, Array.Empty<object?>());

        public void Refresh() => _refresh.Invoke(_instance, Array.Empty<object?>());

        public object? GetByKey(IComparable key) => _getByKey.Invoke(_instance, new object?[] { key });

        public void OnAppendElement(object element, long offset) =>
            _onAppendElement.Invoke(_instance, new object?[] { element, offset });

        private static MethodInfo GetMethod(string name) =>
            UKeyIndexType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(UKeyIndexType.FullName, name);
    }
}
