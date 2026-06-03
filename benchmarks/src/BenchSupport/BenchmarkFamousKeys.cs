namespace PolarDbBenchmarks;

internal static class BenchmarkFamousKeys
{
    public const int HitInt = 42;
    public const long HitLong = 42_000_000_000L;
    public static readonly Guid HitGuid = BenchmarkGuid.For(42);
    public const string HitString = "to-be-or-not-to-be";

    private static readonly string[] Strings =
    {
        HitString,
        "veni-vidi-vici",
        "eureka",
        "stay-hungry-stay-foolish",
        "elementary-my-dear-watson",
        "may-the-force-be-with-you",
        "i-think-therefore-i-am",
        "knowledge-is-power"
    };

    public static bool IsHit(long id) => id % 5 is 0 or 1;

    public static int ExternalInt(long id) => IsHit(id) ? HitInt : 1000 + (int)(id % 997);

    public static long ExternalLong(long id) => IsHit(id) ? HitLong : 90_000_000_000L + (id % 997);

    public static Guid ExternalGuid(long id) => IsHit(id) ? HitGuid : BenchmarkGuid.For(7_000_000L + id % 997);

    public static string ExternalString(long id) => IsHit(id) ? HitString : Strings[2 + (int)(id % (Strings.Length - 2))];

    public static string Payload(long id) => Strings[(int)(id % Strings.Length)] + "-payload-" + id.ToString("000000000");
}
