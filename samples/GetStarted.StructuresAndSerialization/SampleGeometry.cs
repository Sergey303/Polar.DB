using Polar.DB;

namespace GetStarted.StructuresAndSerialization;

internal static class SampleGeometry
{
    public static PTypeRecord PointType { get; } = new(
        new NamedType("x", new PType(PTypeEnumeration.real)),
        new NamedType("y", new PType(PTypeEnumeration.real)));

    public static PTypeUnion GeometryValueType { get; } = new(
        new NamedType("point", PointType),
        new NamedType("label", new PType(PTypeEnumeration.sstring)),
        new NamedType("weight", new PType(PTypeEnumeration.integer)));

    public static PTypeRecord GeometrySceneType { get; } = new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("items", new PTypeSequence(GeometryValueType)));

    public static object[] SampleScene()
    {
        return new object[]
        {
            101,
            new object[]
            {
                new object[] { 0, new object[] { 1.5, 2.5 } },
                new object[] { 1, "origin" },
                new object[] { 2, 9 }
            }
        };
    }
}
