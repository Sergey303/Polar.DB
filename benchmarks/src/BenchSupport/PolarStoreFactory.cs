using Polar.DB;
using Polar.Universal;

namespace PolarDbBenchmarks;

internal sealed record PolarStore(
    USequence Sequence,
    EKeyIndex? IntIndex,
    EKeyIndex? LongIndex,
    EKeyIndex? GuidIndex,
    EKeyIndex? StringIndex);

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

        var sequence = new USequence(ElementType(), Path.Combine(dir, "state.bin"), StreamGen,
            IsDeleted, PrimaryKey(kind), BenchmarkChecksum.StableHash);

        var intIndex = NeedsIntIndex(kind) ? CreateIntIndex(StreamGen, sequence) : null;
        var longIndex = NeedsLongIndex(kind) ? CreateLongIndex(StreamGen, sequence) : null;
        var guidIndex = NeedsGuidIndex(kind) ? CreateGuidIndex(StreamGen, sequence) : null;
        var stringIndex = NeedsStringIndex(kind) ? CreateStringIndex(StreamGen, sequence) : null;
        sequence.uindexes = new IUIndex[] { intIndex, longIndex, guidIndex, stringIndex }
            .Where(index => index != null).Cast<IUIndex>().ToArray();

        return new PolarStore(sequence, intIndex, longIndex, guidIndex, stringIndex);
    }

    private static PType ElementType() => new PTypeRecord(
        new NamedType("id", new PType(PTypeEnumeration.longinteger)),
        new NamedType("long_key", new PType(PTypeEnumeration.longinteger)),
        new NamedType("guid_low", new PType(PTypeEnumeration.longinteger)),
        new NamedType("guid_high", new PType(PTypeEnumeration.longinteger)),
        new NamedType("skey", new PType(PTypeEnumeration.sstring)),
        new NamedType("external_id", new PType(PTypeEnumeration.integer)),
        new NamedType("external_long", new PType(PTypeEnumeration.longinteger)),
        new NamedType("external_guid_low", new PType(PTypeEnumeration.longinteger)),
        new NamedType("external_guid_high", new PType(PTypeEnumeration.longinteger)),
        new NamedType("external_key", new PType(PTypeEnumeration.sstring)),
        new NamedType("payload", new PType(PTypeEnumeration.sstring)),
        new NamedType("deleted", new PType(PTypeEnumeration.boolean)));

    private static bool IsDeleted(object value) => (bool)((object[])value)[11];

    private static Func<object, IComparable> PrimaryKey(ExperimentKind kind) => kind switch
    {
        ExperimentKind.PkLongLookup => value => (long)((object[])value)[1],
        ExperimentKind.PkGuidLookup => value => ReadGuid(value, 2),
        ExperimentKind.PkStringLookup => value => (string)((object[])value)[4],
        _ => value => (long)((object[])value)[0]
    };

    private static bool NeedsIntIndex(ExperimentKind kind) =>
        kind is ExperimentKind.ExternalIntLookup or ExperimentKind.ExternalFamousIntLookup
            or ExperimentKind.ReopenOnly or ExperimentKind.AppendOnly or ExperimentKind.DeleteOnly;

    private static bool NeedsLongIndex(ExperimentKind kind) =>
        kind is ExperimentKind.ExternalLongLookup or ExperimentKind.ExternalFamousLongLookup
            or ExperimentKind.ReopenOnly or ExperimentKind.AppendOnly or ExperimentKind.DeleteOnly;

    private static bool NeedsGuidIndex(ExperimentKind kind) =>
        kind is ExperimentKind.ExternalGuidLookup or ExperimentKind.ExternalFamousGuidLookup
            or ExperimentKind.ReopenOnly or ExperimentKind.AppendOnly or ExperimentKind.DeleteOnly;

    private static bool NeedsStringIndex(ExperimentKind kind) =>
        kind is ExperimentKind.ExternalStringLookup or ExperimentKind.ExternalFamousStringLookup
            or ExperimentKind.ReopenOnly or ExperimentKind.AppendOnly or ExperimentKind.DeleteOnly;

    private static Guid ReadGuid(object value, int offset)
    {
        var row = (object[])value;
        return BenchmarkGuid.Join((long)row[offset], (long)row[offset + 1]);
    }

    private static EKeyIndex CreateIntIndex(Func<Stream> streamGen, USequence sequence) =>
        new(streamGen, sequence, value => new IComparable[] { (int)((object[])value)[5] }, BenchmarkChecksum.StableHash);

    private static EKeyIndex CreateLongIndex(Func<Stream> streamGen, USequence sequence) =>
        new(streamGen, sequence, value => new IComparable[] { (long)((object[])value)[6] }, BenchmarkChecksum.StableHash);

    private static EKeyIndex CreateGuidIndex(Func<Stream> streamGen, USequence sequence) =>
        new(streamGen, sequence, value => new IComparable[] { ReadGuid(value, 7) }, BenchmarkChecksum.StableHash);

    private static EKeyIndex CreateStringIndex(Func<Stream> streamGen, USequence sequence) =>
        new(streamGen, sequence, value => new IComparable[] { (string)((object[])value)[9] }, BenchmarkChecksum.StableHash);
}
