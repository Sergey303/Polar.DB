namespace PolarDbBenchmarks;

internal static class PolarRows
{
    public static object ToPolar(Row row, bool deleted = false)
    {
        return new object[]
        {
            row.Id,
            row.LongKey,
            row.GuidKey,
            row.SKey,
            row.ExternalId,
            row.ExternalLong,
            row.ExternalGuid,
            row.ExternalKey,
            row.Payload,
            deleted
        };
    }

    public static object Tombstone(long id) => new object[]
    {
        id,
        0L,
        Guid.Empty,
        string.Empty,
        -1,
        0L,
        Guid.Empty,
        string.Empty,
        string.Empty,
        true
    };

    public static Row FromPolar(object value)
    {
        var row = (object[])value;
        return new Row(
            (long)row[0],
            (long)row[1],
            (Guid)row[2],
            (string)row[3],
            (int)row[4],
            (long)row[5],
            (Guid)row[6],
            (string)row[7],
            (string)row[8]);
    }
}
