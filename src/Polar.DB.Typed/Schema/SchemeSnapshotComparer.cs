namespace Polar.DB.Typed.Schema;

internal static class SchemeSnapshotComparer
{
    public static string DescribeDifference(SchemeSnapshot stored, SchemeSnapshot current)
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

        return "Stored schema.json differs from the requested record type.";
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
        if (!Same(stored.PolarType, current.PolarType))
            return $"Field '{stored.Name}' Polar type changed from '{stored.PolarType}' to '{current.PolarType}'.";
        if (stored.Key != current.Key)
            return $"Field '{stored.Name}' primary-key flag changed.";
        return null;
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);
}
