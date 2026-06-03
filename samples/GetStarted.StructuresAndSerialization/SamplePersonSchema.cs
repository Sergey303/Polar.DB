using Polar.DB;

namespace GetStarted.StructuresAndSerialization;

internal static class SamplePersonSchema
{
    public static PTypeRecord PersonType { get; } = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("age", new PType(PTypeEnumeration.integer)));
}
