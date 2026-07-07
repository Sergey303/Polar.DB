using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public class UKeyIndexConfiguredTests
{
    private static USequence CreateIntegerSequence(bool optimise)
    {
        var sequence = new USequence(
            new PType(PTypeEnumeration.integer),
            null,
            () => new MemoryStream(),
            _ => false,
            optimise);
        sequence.SetPrimaryKey<int>(value => (int)value);
        return sequence;
    }

    [Fact]
    public void GetByKey_ReturnsValue_WhenIndexStoredOnDisk()
    {
        var sequence = CreateIntegerSequence(false);
        sequence.AppendElement(42);
        sequence.Build();
        object result = sequence.GetByKey(42);
        Assert.IsType<int>(result);
        Assert.Equal(42, (int)result);
    }

    [Fact]
    public void GetByKey_ReturnsValue_WhenOnlyOneElementExists_AndIndexStoredOnDisk()
    {
        using var scope = CreateIntegerSequenceScope(false);
        var sequence = scope.Sequence;
        sequence.AppendElement(42);
        sequence.Build();
        object result = sequence.GetByKey(42);
        Assert.NotNull(result);
        Assert.IsType<int>(result);
        Assert.Equal(42, (int)result);
    }

    [Fact]
    public void GetByKey_ReturnsLeftBoundaryValue_WhenMatchIsFirstElement_AndIndexStoredOnDisk()
    {
        using var scope = CreateIntegerSequenceScope(false);
        var sequence = scope.Sequence;
        sequence.AppendElement(10);
        sequence.AppendElement(20);
        sequence.AppendElement(30);
        sequence.Build();
        object result = sequence.GetByKey(10);
        Assert.NotNull(result);
        Assert.IsType<int>(result);
        Assert.Equal(10, (int)result);
    }

    [Fact]
    public void GetByKey_ReturnsRightBoundaryValue_WhenMatchIsLastElement_AndIndexStoredOnDisk()
    {
        using var scope = CreateIntegerSequenceScope(false);
        var sequence = scope.Sequence;
        sequence.AppendElement(10);
        sequence.AppendElement(20);
        sequence.AppendElement(30);
        sequence.Build();
        object result = sequence.GetByKey(30);
        Assert.NotNull(result);
        Assert.IsType<int>(result);
        Assert.Equal(30, (int)result);
    }

    [Fact]
    public void GetByKey_ReturnsSecondElement_WhenTwoElementsExist_AndIndexStoredOnDisk()
    {
        using var scope = CreateIntegerSequenceScope(false);
        var sequence = scope.Sequence;
        sequence.AppendElement(10);
        sequence.AppendElement(20);
        sequence.Build();
        object result = sequence.GetByKey(20);
        Assert.NotNull(result);
        Assert.IsType<int>(result);
        Assert.Equal(20, (int)result);
    }

    [Fact]
    public void GetByKey_ReturnsNull_WhenKeyIsSmallerThanAllIndexedValues_AndIndexStoredOnDisk()
    {
        using var scope = CreateIntegerSequenceScope(false);
        var sequence = scope.Sequence;
        sequence.AppendElement(10);
        sequence.AppendElement(20);
        sequence.AppendElement(30);
        sequence.Build();
        object result = sequence.GetByKey(5);
        Assert.Null(result);
    }

    [Fact]
    public void GetByKey_ReturnsNull_WhenKeyIsGreaterThanAllIndexedValues_AndIndexStoredOnDisk()
    {
        using var scope = CreateIntegerSequenceScope(false);
        var sequence = scope.Sequence;
        sequence.AppendElement(10);
        sequence.AppendElement(20);
        sequence.AppendElement(30);
        sequence.Build();
        object result = sequence.GetByKey(100);
        Assert.Null(result);
    }

    [Fact]
    public void GetByKey_ReturnsExactValue_WhenSeveralKeysShareSameHash_AndIndexStoredOnDisk()
    {
        using var scope = CreateIntegerSequenceScope(false, key => key % 2);
        var sequence = scope.Sequence;
        sequence.AppendElement(2);
        sequence.AppendElement(4);
        sequence.AppendElement(6);
        sequence.AppendElement(8);
        sequence.Build();
        object result = sequence.GetByKey(6);
        Assert.NotNull(result);
        Assert.IsType<int>(result);
        Assert.Equal(6, (int)result);
    }

    [Fact]
    public void GetByKey_ReturnsNull_WhenHashMatchesButExactKeyDoesNotExist_AndIndexStoredOnDisk()
    {
        using var scope = CreateIntegerSequenceScope(false, key => key % 2);
        var sequence = scope.Sequence;
        sequence.AppendElement(2);
        sequence.AppendElement(4);
        sequence.AppendElement(6);
        sequence.Build();
        object result = sequence.GetByKey(8);
        Assert.Null(result);
    }

    [Fact]
    public void GetByKey_ReturnsValue_AfterRefresh_WhenIndexStoredOnDisk()
    {
        using var scope = CreateIntegerSequenceScope(false);
        var sequence = scope.Sequence;
        sequence.AppendElement(10);
        sequence.AppendElement(20);
        sequence.AppendElement(30);
        sequence.Build();
        sequence.Refresh();
        object result = sequence.GetByKey(20);
        Assert.NotNull(result);
        Assert.IsType<int>(result);
        Assert.Equal(20, (int)result);
    }

    private static IntegerSequenceScope CreateIntegerSequenceScope(bool optimise, Func<int, int>? hashOfKey = null)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "PolarDbTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        int fileNo = 0;
        Func<Stream> streamGen = () => new FileStream(
            Path.Combine(tempDir, $"f{fileNo++}.bin"),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite);

        var sequence = new USequence(
            new PType(PTypeEnumeration.integer),
            Path.Combine(tempDir, "state.bin"),
            streamGen,
            _ => false,
            optimise);
        sequence.SetPrimaryKey<int>(obj => (int)obj, hashOfKey ?? IdentityHash);
        return new IntegerSequenceScope(sequence, tempDir);
    }

    private static int IdentityHash(int key) => key;

    private sealed class IntegerSequenceScope : IDisposable
    {
        public IntegerSequenceScope(USequence sequence, string tempDir)
        {
            Sequence = sequence;
            TempDir = tempDir;
        }

        public USequence Sequence { get; }
        public string TempDir { get; }

        public void Dispose()
        {
            try { Sequence.Close(); } catch { }
            try { if (Directory.Exists(TempDir)) Directory.Delete(TempDir, true); } catch { }
        }
    }
}
