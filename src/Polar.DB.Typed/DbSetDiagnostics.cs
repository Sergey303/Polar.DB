namespace Polar.DB.Typed;

public sealed record DbSetDiagnostics(
    string StorageName,
    string StoragePath,
    string KeyName,
    string KeyClrType,
    IReadOnlyList<string> FieldNames,
    IReadOnlyList<string> ExternalKeyNames,
    IReadOnlyList<string> BuiltExternalKeyNames,
    int Count,
    int CollectedAppendCount);
