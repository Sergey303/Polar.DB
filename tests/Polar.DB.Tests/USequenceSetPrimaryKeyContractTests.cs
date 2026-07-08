using System.Linq.Expressions;
using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public class USequenceSetPrimaryKeyContractTests
{
    private static readonly PTypeRecord PersonType = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)));

    [Fact]
    public void SetPrimaryKey_CalledTwice_ThrowsInvalidOperationException()
    {
        using var scope = new SequenceScope(new PType(PTypeEnumeration.integer));

        scope.Sequence.SetPrimaryKey<int>(value => (int)value);

        var error = Assert.Throws<InvalidOperationException>(
            () => scope.Sequence.SetPrimaryKey<int>(value => (int)value));

        Assert.Contains("already configured", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PrimaryKeyOperations_BeforeSetPrimaryKey_ThrowInvalidOperationException()
    {
        using var scope = new SequenceScope(new PType(PTypeEnumeration.integer));

        Assert.Throws<InvalidOperationException>(() => scope.Sequence.Load(new object[] { 1 }));
        Assert.Throws<InvalidOperationException>(() => scope.Sequence.Build());
        Assert.Throws<InvalidOperationException>(() => scope.Sequence.AppendElement(1));
        Assert.Throws<InvalidOperationException>(() => scope.Sequence.GetByKey(1));
    }

    [Fact]
    public void SetPrimaryKey_NullExpression_ThrowsArgumentNullException()
    {
        using var scope = new SequenceScope(new PType(PTypeEnumeration.integer));
        Expression<Func<object, int>> expression = null!;

        var error = Assert.Throws<ArgumentNullException>(
            () => scope.Sequence.SetPrimaryKey(expression));

        Assert.Equal("keyExpression", error.ParamName);
    }

    [Fact]
    public void SetPrimaryKey_UnsupportedKeyTypeWithoutHasher_ThrowsAndAllowsRetry()
    {
        using var scope = new SequenceScope(new PType(PTypeEnumeration.integer));

        var error = Assert.Throws<NotSupportedException>(
            () => scope.Sequence.SetPrimaryKey<decimal>(value => (decimal)(int)value));

        Assert.Contains("explicit hasher", error.Message, StringComparison.OrdinalIgnoreCase);

        scope.Sequence.SetPrimaryKey<int>(value => (int)value);
        scope.Sequence.Load(new object[] { 7 });
        scope.Sequence.Build();

        Assert.Equal(7, Assert.IsType<int>(scope.Sequence.GetByKey(7)));
    }

    [Fact]
    public void SetPrimaryKey_UnsupportedKeyTypeWithCustomHasher_Works()
    {
        using var scope = new SequenceScope(new PType(PTypeEnumeration.real));
        scope.Sequence.SetPrimaryKey<decimal>(
            value => (decimal)(double)value,
            key => decimal.ToInt32(key));

        scope.Sequence.Load(new object[] { 1.0, 2.0, 3.0 });
        scope.Sequence.Build();

        Assert.Equal(2.0, Assert.IsType<double>(scope.Sequence.GetByKey(2m)));
    }

    [Fact]
    public void SetPrimaryKey_CustomHasherCollisions_StillMatchExactKey()
    {
        using var scope = new SequenceScope(PersonType);
        scope.Sequence.SetPrimaryKey<int>(
            record => (int)((object[])record)[0],
            _ => 1);

        scope.Sequence.Load(new object[]
        {
            Person(1, "Alice"),
            Person(2, "Bob"),
            Person(3, "Carol")
        });
        scope.Sequence.Build();

        var found = Assert.IsType<object[]>(scope.Sequence.GetByKey(2));
        Assert.Equal("Bob", Assert.IsType<string>(found[1]));
        Assert.Null(scope.Sequence.GetByKey(4));
    }

    [Fact]
    public void SetPrimaryKey_DuplicateKeys_KeepLatestPhysicalRecord()
    {
        using var scope = new SequenceScope(PersonType);
        scope.Sequence.SetPrimaryKey<int>(record => (int)((object[])record)[0]);

        scope.Sequence.Load(new object[]
        {
            Person(1, "Old"),
            Person(2, "Bob"),
            Person(1, "New")
        });
        scope.Sequence.Build();

        var found = Assert.IsType<object[]>(scope.Sequence.GetByKey(1));
        Assert.Equal("New", Assert.IsType<string>(found[1]));
    }

    private static object[] Person(int id, string name) => new object[] { id, name };

    private sealed class SequenceScope : IDisposable
    {
        private readonly List<Stream> streams = new();

        public SequenceScope(PType elementType)
        {
            Sequence = new USequence(
                elementType,
                stateFileName: null,
                streamGen: CreateStream,
                isEmpty: _ => false,
                optimise: false);
        }

        public USequence Sequence { get; }

        private Stream CreateStream()
        {
            var stream = new MemoryStream();
            streams.Add(stream);
            return stream;
        }

        public void Dispose()
        {
            Sequence.Close();
            foreach (var stream in streams)
                stream.Dispose();
        }
    }
}
