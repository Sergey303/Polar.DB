using Polar.Universal;
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

}
