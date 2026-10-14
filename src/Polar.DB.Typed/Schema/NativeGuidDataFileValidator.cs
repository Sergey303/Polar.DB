namespace Polar.DB.Typed.Schema;

internal static class NativeGuidDataFileValidator
{
    // A fixed-size native Guid record cannot share the physical layout of a
    // legacy string-backed Guid record. Check before USequence's recovery
    // can reinterpret (and potentially rewrite) a mismatched data stream.
    internal static void Validate(PType recordType, IReadOnlyList<FieldScheme> fields,
        string tablePath, string schemaPath)
    {
        if (!recordType.HasNoTail ||
            !fields.Any(field => field.PolarType.Vid == PTypeEnumeration.guid))
            return;

        string dataPath = Path.Combine(tablePath, "data-00.bin");
        if (!File.Exists(dataPath))
            return;

        long size = new FileInfo(dataPath).Length;
        if (size == 0L)
            return;

        int recordBytes = recordType.HeadSize;
        const int HeaderBytes = sizeof(long);
        if (recordBytes <= 0 || size < HeaderBytes ||
            (size - HeaderBytes) % recordBytes != 0)
        {
            throw new SchemeCompatibilityException(tablePath, schemaPath,
                $"Native fixed-size Guid records require a {HeaderBytes}-byte header " +
                $"and whole {recordBytes}-byte records, but {dataPath} is {size} bytes. " +
                "The data may use the legacy string-backed Guid layout.");
        }

        // Do not compare header count here: after an interrupted append, the
        // sequence recovery path may legitimately repair a stale count.
    }
}
