using Polar.Universal;
using Xunit;

namespace Polar.DB.Tests;

public class USequencePrimaryKeyConfigurationTests
{
    private static readonly PTypeRecord StringKeyRecordType = new(
        new NamedType("id", new PType(PTypeEnumeration.sstring)),
        new NamedType("value", new PType(PTypeEnumeration.sstring)));

    [Fact]
    public void SetPrimaryKey_StringKey_PreservesLookupAfterReopen()
    {
        using var scope = new FileSequenceScope();

        using (var sequence = scope.OpenStringKeySequence())
        {
            sequence.Load(new object[]
            {
                new object[] { "alpha", "A" },
                new object[] { "beta", "B" }
            });
            sequence.Build();
        }

        using var reopened = scope.OpenStringKeySequence();
        reopened.Refresh();

        var found = Assert.IsType<object[]>(reopened.GetByKey("beta"));
        Assert.Equal("B", Assert.IsType<string>(found[1]));
    }

    [Fact]
    public void SetPrimaryKey_ReopensIndexWrittenByLegacyConstructorContract()
    {
        using var scope = new FileSequenceScope();

        using (var legacy = scope.OpenLegacyIntSequence())
        {
            legacy.Load(new object[] { 10, 20, 30 });
            legacy.Build();
        }

        using var configured = scope.OpenConfiguredIntSequence();
        configured.Refresh();

        Assert.Equal(20, Assert.IsType<int>(configured.GetByKey(20)));
    }

    [Fact]
    public void SetPrimaryKey_LongScalar_PreservesLookupAfterReopen()
    {
        using var scope = new FileSequenceScope();
        long[] values = { 11L, 22L, 33L, 44L };

        using (var sequence = scope.OpenConfiguredInt64Sequence())
        {
            sequence.Load(values.Cast<object>());
            sequence.Build();
        }

        using var reopened = scope.OpenConfiguredInt64Sequence();
        reopened.Refresh();

        Assert.Equal(33L, Assert.IsType<long>(reopened.GetByKey(33L)));
    }

    private sealed class FileSequenceScope : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            "PolarDbTests",
            Guid.NewGuid().ToString("N"));

        public FileSequenceScope()
        {
            Directory.CreateDirectory(root);
        }

        public USequence OpenStringKeySequence()
        {
            var sequence = new USequence(
                StringKeyRecordType,
                Path.Combine(root, "state.bin"),
                CreateStreamGenerator(),
                _ => false,
                optimise: false);
            sequence.SetPrimaryKey<string>(record => (string)((object[])record)[0]);
            return sequence;
        }

        public USequence OpenLegacyIntSequence()
        {
#pragma warning disable CS0618 // Intentional compatibility coverage for the obsolete constructor.
            return new USequence(
                new PType(PTypeEnumeration.integer),
                Path.Combine(root, "state.bin"),
                CreateStreamGenerator(),
                _ => false,
                value => (int)value,
                key => (int)key,
                optimise: false);
#pragma warning restore CS0618
        }

        public USequence OpenConfiguredIntSequence()
        {
            var sequence = new USequence(
                new PType(PTypeEnumeration.integer),
                Path.Combine(root, "state.bin"),
                CreateStreamGenerator(),
                _ => false,
                optimise: false);
            sequence.SetPrimaryKey<int>(value => (int)value);
            return sequence;
        }

        public USequence OpenConfiguredInt64Sequence()
        {
            var sequence = new USequence(
                new PType(PTypeEnumeration.longinteger),
                Path.Combine(root, "state.bin"),
                CreateStreamGenerator(),
                _ => false,
                optimise: false);
            sequence.SetPrimaryKey<long>(value => (long)value);
            return sequence;
        }

        private Func<Stream> CreateStreamGenerator()
        {
            var counter = 0;
            return () => new FileStream(
                Path.Combine(root, $"f{counter++}.bin"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch { }
        }
    }
}
