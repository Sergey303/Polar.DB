namespace PolarDbBenchmarks;

internal static class PolarRows
{
    public static object ToPolar(Row row, bool deleted = false) => new object[]
    {
        row.Id,
        row.LongKey,
        row.GuidKey,
        row.SKey,
        row.ExternalId,
        row.ExternalKey,
        row.Payload,
        deleted
    };

    public static object Tombstone(long id) => new object[]
    {
        id,
        0L,
        string.Empty,
        string.Empty,
        -1,
        string.Empty,
        string.Empty,
        true
    };

    public static Row FromPolar(object value)
    {
        var row = (object[])value;
        return new Row((long)row[0], (long)row[1], (string)row[2], (string)row[3],
            (int)row[4], (string)row[5], (string)row[6]);
    }
}
