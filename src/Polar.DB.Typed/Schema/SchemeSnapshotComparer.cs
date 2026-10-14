namespace Polar.DB.Typed.Schema;

internal static class SchemeSnapshotComparer
{
    private const string LegacyOpaquePType = "Polar.DB.PType";

    public static bool AreCompatible(SchemeSnapshot stored, SchemeSnapshot current) =>
        FindDifference(stored, current) == null;

    public static string DescribeDifference(SchemeSnapshot stored, SchemeSnapshot current) =>
        FindDifference(stored, current)
        ?? "Stored schema.json differs from the requested record type.";

    public static bool IsLegacyOpaquePolarType(string value) =>
        string.Equals(value, LegacyOpaquePType, StringComparison.Ordinal);

    private static string? FindDifference(SchemeSnapshot stored, SchemeSnapshot current)
    {
        if (!Same(stored.StorageName, current.StorageName))
            return $"Storage name changed from '{stored.StorageName}' to '{current.StorageName}'.";
        if (!Same(stored.KeyName, current.KeyName))
            return $"Primary key changed from '{stored.KeyName}' to '{current.KeyName}'.";
        if (stored.Fields.Count != current.Fields.Count)
            return $"Field count changed from {stored.Fields.Count} to {current.Fields.Count}.";

        for (int index = 0; index < stored.Fields.Count; index++)
        {
            string? diff = DescribeFieldDifference(index, stored.Fields[index], current.Fields[index]);
            if (diff != null) return diff;
        }

        if (!Same(stored.TypeName, current.TypeName))
            return $"Record type changed from '{stored.TypeName}' to '{current.TypeName}'.";

        return null;
    }

    private static string? DescribeFieldDifference(
        int index,
        SchemeFieldSnapshot stored,
        SchemeFieldSnapshot current)
    {
        if (!Same(stored.Name, current.Name))
            return $"Field #{index} changed from '{stored.Name}' to '{current.Name}'.";
        if (!Same(stored.ClrType, current.ClrType))
            return $"Field '{stored.Name}' CLR type changed from '{stored.ClrType}' to '{current.ClrType}'.";
        if (stored.Order != index || current.Order != index)
            return $"Field '{stored.Name}' has an inconsistent persisted field order.";
        if (!PolarTypesCompatible(stored, current))
            return $"Field '{stored.Name}' Polar type changed from '{stored.PolarType}' to '{current.PolarType}'.";
        if (stored.Key != current.Key)
            return $"Field '{stored.Name}' primary-key flag changed.";
        return null;
    }

    private static bool PolarTypesCompatible(
        SchemeFieldSnapshot stored,
        SchemeFieldSnapshot current)
    {
        if (Same(stored.PolarType, current.PolarType))
            return true;
        if (!IsLegacyOpaquePolarType(stored.PolarType))
            return false;

        string? expectedLegacyType = stored.ClrType switch
        {
            "System.Int32" => PTypeEnumeration.integer.ToString(),
            "System.String" => PTypeEnumeration.sstring.ToString(),
            "System.Boolean" => PTypeEnumeration.boolean.ToString(),
            "System.Int64" => PTypeEnumeration.sstring.ToString(),
            "System.Guid" => PTypeEnumeration.sstring.ToString(),
            _ => null
        };

        return expectedLegacyType != null && Same(expectedLegacyType, current.PolarType);
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);
}
