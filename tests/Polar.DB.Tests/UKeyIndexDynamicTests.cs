using Xunit;

namespace Polar.DB.Tests;

public class UKeyIndexDynamicTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OnAppendElement_DuplicateDynamicKey_ReplacesOriginalOffset_ForIsOriginal(bool keysInMemory)
    {
        using var scope = new UKeyIndexTestHelpers.SequenceScope();
        Func<object, IComparable> keyFunc = record => (string)((object[])record)[1];
        Func<IComparable, int> hashFunc = _ => 1;
        var index = new UKeyIndex(scope.StreamGen, scope.Sequence, keyFunc, hashFunc, keysInMemory);

        index.Build();

        var first = UKeyIndexTestHelpers.Row(1, "BOB");
        var second = UKeyIndexTestHelpers.Row(2, "BOB");

        index.OnAppendElement(first, 100L);
        Assert.True(index.IsOriginal("BOB", 100L));

        index.OnAppendElement(second, 200L);

        Assert.False(index.IsOriginal("BOB", 100L));
        Assert.True(index.IsOriginal("BOB", 200L));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OnAppendElement_NewDynamicKey_IsFindable_WithoutRebuild(bool keysInMemory)
    {
        using var scope = new UKeyIndexTestHelpers.SequenceScope();
        Func<object, IComparable> keyFunc = record => (string)((object[])record)[1];
        Func<IComparable, int> hashFunc = _ => 1;
        var index = new UKeyIndex(scope.StreamGen, scope.Sequence, keyFunc, hashFunc, keysInMemory);

        UKeyIndexTestHelpers.LoadAndBuild(
            scope,
            index,
            UKeyIndexTestHelpers.Row(1, "ALICE"));

        long bobOffset = scope.Sequence.AppendElement(UKeyIndexTestHelpers.Row(2, "BOB"));
        index.OnAppendElement(UKeyIndexTestHelpers.Row(2, "BOB"), bobOffset);

        var result = Assert.IsType<object[]>(index.GetByKey("BOB"));

        Assert.Equal(2, (int)result[0]);
        Assert.Equal("BOB", (string)result[1]);
    }
}
