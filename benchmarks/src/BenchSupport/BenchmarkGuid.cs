namespace PolarDbBenchmarks;

internal static class BenchmarkGuid
{
    public static Guid For(long id) =>
        new((int)id, (short)(id >> 32), (short)(id >> 48),
            new byte[] { 1, 2, 3, 4, 5, 6, (byte)(id & 255), (byte)((id >> 8) & 255) });

    public static byte[] ToBytes(Guid value) => value.ToByteArray();

    public static Guid FromBytes(byte[] value) => new(value);

    public static (long Low, long High) Split(Guid value)
    {
        var bytes = value.ToByteArray();
        return (BitConverter.ToInt64(bytes, 0), BitConverter.ToInt64(bytes, 8));
    }

    public static Guid Join(long low, long high)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(low).CopyTo(bytes, 0);
        BitConverter.GetBytes(high).CopyTo(bytes, 8);
        return new Guid(bytes);
    }
}
