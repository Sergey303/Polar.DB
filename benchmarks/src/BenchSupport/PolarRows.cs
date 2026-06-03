namespace PolarDbBenchmarks;

internal static class PolarRows
{
    public static object ToPolar(Row row, bool deleted = false) => new object[]
    {
        row.Id,
        row.SKey,
        row.ExternalId,
        row.ExternalKey,
        row.Payload,
        deleted
    };

    public static object Tombstone(long id) => new object[]
    {
        id,
        string.Empty,
        -1,
        string.Empty,
        string.Empty,
        true
    };

    public static Row FromPolar(object value)
    {
        var row = (object[])value;
        return new Row(
            (long)row[0],
            (string)row[1],
            (int)row[2],
            (string)row[3],
            (string)row[4]);
    }
}
