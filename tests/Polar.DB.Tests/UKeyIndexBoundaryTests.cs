using Xunit;

namespace Polar.DB.Tests;

public class UKeyIndexBoundaryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetByKey_EmptyIndex_ReturnsNull(bool keysInMemory)
    {
        using var scope = new UKeyIndexTestHelpers.SequenceScope();
        Func<object, IComparable> keyFunc = record => (string)((object[])record)[1];
        Func<IComparable, int> hashFunc = _ => 1;
        var index = new UKeyIndex(scope.StreamGen, scope.Sequence, keyFunc, hashFunc, keysInMemory);

        index.Build();
        var result = index.GetByKey("ALICE");

        Assert.Null(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetByKey_NoMatch_ReturnsNull(bool keysInMemory)
    {
        using var scope = new UKeyIndexTestHelpers.SequenceScope();
        Func<object, IComparable> keyFunc = record => (string)((object[])record)[1];
        Func<IComparable, int> hashFunc = _ => 1;
        var index = new UKeyIndex(scope.StreamGen, scope.Sequence, keyFunc, hashFunc, keysInMemory);

        UKeyIndexTestHelpers.LoadAndBuild(
            scope,
            index,
            UKeyIndexTestHelpers.Row(1, "ALICE"),
            UKeyIndexTestHelpers.Row(2, "BOB"),
            UKeyIndexTestHelpers.Row(3, "CLARA"));

        var result = index.GetByKey("DAVID");

        Assert.Null(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetByKey_SingleMatch_ReturnsExpectedRecord(bool keysInMemory)
    {
        using var scope = new UKeyIndexTestHelpers.SequenceScope();
        Func<object, IComparable> keyFunc = record => (string)((object[])record)[1];
        Func<IComparable, int> hashFunc = _ => 1;
        var index = new UKeyIndex(scope.StreamGen, scope.Sequence, keyFunc, hashFunc, keysInMemory);

        UKeyIndexTestHelpers.LoadAndBuild(
            scope,
            index,
            UKeyIndexTestHelpers.Row(1, "ALICE"));

        var result = Assert.IsType<object[]>(index.GetByKey("ALICE"));

        Assert.Equal(1, (int)result[0]);
        Assert.Equal("ALICE", (string)result[1]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetByKey_AllEqualHashBlock_FindsFirstElement(bool keysInMemory)
    {
        using var scope = new UKeyIndexTestHelpers.SequenceScope();
        Func<object, IComparable> keyFunc = record => (string)((object[])record)[1];
        Func<IComparable, int> hashFunc = _ => 1;
        var index = new UKeyIndex(scope.StreamGen, scope.Sequence, keyFunc, hashFunc, keysInMemory);

        UKeyIndexTestHelpers.LoadAndBuild(
            scope,
            index,
            UKeyIndexTestHelpers.Row(1, "ALICE"),
            UKeyIndexTestHelpers.Row(2, "BOB"),
            UKeyIndexTestHelpers.Row(3, "CLARA"));

        var result = Assert.IsType<object[]>(index.GetByKey("ALICE"));

        Assert.Equal(1, (int)result[0]);
        Assert.Equal("ALICE", (string)result[1]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetByKey_AllEqualHashBlock_FindsLastElement(bool keysInMemory)
    {
        using var scope = new UKeyIndexTestHelpers.SequenceScope();
        Func<object, IComparable> keyFunc = record => (string)((object[])record)[1];
        Func<IComparable, int> hashFunc = _ => 1;
        var index = new UKeyIndex(scope.StreamGen, scope.Sequence, keyFunc, hashFunc, keysInMemory);

        UKeyIndexTestHelpers.LoadAndBuild(
            scope,
            index,
            UKeyIndexTestHelpers.Row(1, "ALICE"),
            UKeyIndexTestHelpers.Row(2, "BOB"),
            UKeyIndexTestHelpers.Row(3, "CLARA"));

        var result = Assert.IsType<object[]>(index.GetByKey("CLARA"));

        Assert.Equal(3, (int)result[0]);
        Assert.Equal("CLARA", (string)result[1]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetByKey_AllEqualHashBlock_NoMatch_ReturnsNull(bool keysInMemory)
    {
        using var scope = new UKeyIndexTestHelpers.SequenceScope();
        Func<object, IComparable> keyFunc = record => (string)((object[])record)[1];
        Func<IComparable, int> hashFunc = _ => 1;
        var index = new UKeyIndex(scope.StreamGen, scope.Sequence, keyFunc, hashFunc, keysInMemory);

        UKeyIndexTestHelpers.LoadAndBuild(
            scope,
            index,
            UKeyIndexTestHelpers.Row(1, "ALICE"),
            UKeyIndexTestHelpers.Row(2, "BOB"),
            UKeyIndexTestHelpers.Row(3, "CLARA"));

        var result = index.GetByKey("ZZZ");

        Assert.Null(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetByKey_DuplicateKeys_DoesNotMissRequestedKey(bool keysInMemory)
    {
        using var scope = new UKeyIndexTestHelpers.SequenceScope();
        Func<object, IComparable> keyFunc = record => (string)((object[])record)[1];
        Func<IComparable, int> hashFunc = _ => 1;
        var index = new UKeyIndex(scope.StreamGen, scope.Sequence, keyFunc, hashFunc, keysInMemory);

        UKeyIndexTestHelpers.LoadAndBuild(
            scope,
            index,
            UKeyIndexTestHelpers.Row(1, "ALICE"),
            UKeyIndexTestHelpers.Row(2, "BOB"),
            UKeyIndexTestHelpers.Row(3, "BOB"),
            UKeyIndexTestHelpers.Row(4, "CLARA"));

        var result = Assert.IsType<object[]>(index.GetByKey("BOB"));

        Assert.Equal("BOB", (string)result[1]);
        Assert.Contains((int)result[0], new[] { 2, 3 });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetByKey_FindsFirstAndLastElements_InRepeatedHashRange(bool keysInMemory)
    {
        using var scope = new UKeyIndexTestHelpers.SequenceScope();
        Func<object, IComparable> keyFunc = record => (string)((object[])record)[1];
        Func<IComparable, int> hashFunc = _ => 1;
        var index = new UKeyIndex(scope.StreamGen, scope.Sequence, keyFunc, hashFunc, keysInMemory);

        UKeyIndexTestHelpers.LoadAndBuild(
            scope,
            index,
            UKeyIndexTestHelpers.Row(10, "ALPHA"),
            UKeyIndexTestHelpers.Row(20, "BETA"),
            UKeyIndexTestHelpers.Row(30, "GAMMA"),
            UKeyIndexTestHelpers.Row(40, "OMEGA"));

        var first = Assert.IsType<object[]>(index.GetByKey("ALPHA"));
        var last = Assert.IsType<object[]>(index.GetByKey("OMEGA"));

        Assert.Equal(10, (int)first[0]);
        Assert.Equal("ALPHA", (string)first[1]);
        Assert.Equal(40, (int)last[0]);
        Assert.Equal("OMEGA", (string)last[1]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Refresh_AfterBuild_PreservesLookupAcrossPersistedIndex(bool keysInMemory)
    {
        using var scope = new UKeyIndexTestHelpers.SequenceScope();
        Func<object, IComparable> keyFunc = record => (string)((object[])record)[1];
        Func<IComparable, int> hashFunc = _ => 1;
        var index = new UKeyIndex(scope.StreamGen, scope.Sequence, keyFunc, hashFunc, keysInMemory);

        UKeyIndexTestHelpers.LoadAndBuild(
            scope,
            index,
            UKeyIndexTestHelpers.Row(1, "ALICE"),
            UKeyIndexTestHelpers.Row(2, "BOB"),
            UKeyIndexTestHelpers.Row(3, "CLARA"));

        index.Refresh();

        var first = Assert.IsType<object[]>(index.GetByKey("ALICE"));
        var last = Assert.IsType<object[]>(index.GetByKey("CLARA"));
        var missing = index.GetByKey("DAVID");

        Assert.Equal(1, (int)first[0]);
        Assert.Equal(3, (int)last[0]);
        Assert.Null(missing);
    }
}
