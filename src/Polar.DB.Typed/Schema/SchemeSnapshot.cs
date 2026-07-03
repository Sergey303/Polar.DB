namespace Polar.DB.Typed.Schema;

internal sealed record SchemeSnapshot(
    string TypeName,
    string StorageName,
    string KeyName,
    IReadOnlyList<SchemeFieldSnapshot> Fields);

internal sealed record SchemeFieldSnapshot(
    string Name,
    string ClrType,
    string PolarType,
    int Order,
    bool Key);
