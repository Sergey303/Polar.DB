using Polar.DB;
using Polar.Universal;

namespace PolarDbBenchmarks;

internal sealed record PolarStore(USequence Sequence, EKeyIndex? IntIndex, EKeyIndex? StringIndex);

internal static class PolarStoreFactory
{
    public static PolarStore Open(string dir, ExperimentKind kind)
    {
        var counter = 0;
        Stream StreamGen()
        {
            var path = Path.Combine(dir, "f" + counter++ + ".bin");
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        var sequence = new USequence(
            ElementType(), Path.Combine(dir, "state.bin"), StreamGen,
            IsDeleted, PrimaryKey(kind), BenchmarkChecksum.StableHash);

        var intIndex = NeedsIntIndex(kind) ? CreateIntIndex(StreamGen, sequence) : null;
        var stringIndex = NeedsStringIndex(kind) ? CreateStringIndex(StreamGen, sequence) : null;
        sequence.uindexes = new IUIndex[] { intIndex, stringIndex }
            .Where(index => index != null).Cast<IUIndex>().ToArray();

        return new PolarStore(sequence, intIndex, stringIndex);
    }

    private static PType ElementType() => new PTypeRecord(
        new NamedType("id", new PType(PTypeEnumeration.longinteger)),
        new NamedType("long_key", new PType(PTypeEnumeration.longinteger)),
        new NamedType("guid_key", new PType(PTypeEnumeration.sstring)),
        new NamedType("skey", new PType(PTypeEnumeration.sstring)),
        new NamedType("external_id", new PType(PTypeEnumeration.integer)),
        new NamedType("external_key", new PType(PTypeEnumeration.sstring)),
        new NamedType("payload", new PType(PTypeEnumeration.sstring)),
        new NamedType("deleted", new PType(PTypeEnumeration.boolean)));

    private static bool IsDeleted(object value) => (bool)((object[])value)[7];

    private static Func<object, IComparable> PrimaryKey(ExperimentKind kind) => kind switch
    {
        ExperimentKind.PkLongLookup => value => (long)((object[])value)[1],
        ExperimentKind.PkGuidLookup => value => (string)((object[])value)[2],
        ExperimentKind.PkStringLookup => value => (string)((object[])value)[3],
        _ => value => (long)((object[])value)[0]
    };

    private static bool NeedsIntIndex(ExperimentKind kind) =>
        kind is ExperimentKind.ExternalIntLookup or ExperimentKind.BuildOnly;

    private static bool NeedsStringIndex(ExperimentKind kind) =>
        kind is ExperimentKind.ExternalStringLookup
            or ExperimentKind.ExternalFamousStringLookup
            or ExperimentKind.BuildOnly;

    private static EKeyIndex CreateIntIndex(Func<Stream> streamGen, USequence sequence) =>
        new(streamGen, sequence, value => new IComparable[] { (int)((object[])value)[4] },
            BenchmarkChecksum.StableHash);

    private static EKeyIndex CreateStringIndex(Func<Stream> streamGen, USequence sequence) =>
        new(streamGen, sequence, value => new IComparable[] { (string)((object[])value)[5] },
            BenchmarkChecksum.StableHash);
}
