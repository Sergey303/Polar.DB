namespace PolarDbBenchmarks;

internal static class PolarRows
{
    public static object ToPolar(Row row, bool deleted = false)
    {
        var guid = BenchmarkGuid.Split(row.GuidKey);
        var externalGuid = BenchmarkGuid.Split(row.ExternalGuid);
        return new object[]
        {
            row.Id, row.LongKey, guid.Low, guid.High, row.SKey, row.ExternalId,
            row.ExternalLong, externalGuid.Low, externalGuid.High, row.ExternalKey,
            row.Payload, deleted
        };
    }

    public static object Tombstone(long id) => new object[]
    {
        id,
        0L,
        0L,
        0L,
        string.Empty,
        -1,
        0L,
        0L,
        0L,
        string.Empty,
        string.Empty,
        true
    };

    public static Row FromPolar(object value)
    {
        var row = (object[])value;
        return new Row((long)row[0], (long)row[1], BenchmarkGuid.Join((long)row[2], (long)row[3]),
            (string)row[4], (int)row[5], (long)row[6], BenchmarkGuid.Join((long)row[7], (long)row[8]),
            (string)row[9], (string)row[10]);
    }
}
