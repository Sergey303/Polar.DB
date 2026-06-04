namespace PolarDbBenchmarks;

internal static class BenchmarkFamousStrings
{
    public const string HitKey = "to-be-or-not-to-be";

    private static readonly string[] Keys =
    {
        HitKey,
        "veni-vidi-vici",
        "eureka",
        "stay-hungry-stay-foolish",
        "elementary-my-dear-watson",
        "may-the-force-be-with-you",
        "i-think-therefore-i-am",
        "knowledge-is-power"
    };

    public static string ExternalKey(long id)
    {
        if (id % 5 is 0 or 1) return HitKey;
        return Keys[2 + (int)(id % (Keys.Length - 2))];
    }

    public static string Payload(long id) => Keys[(int)(id % Keys.Length)] + "-payload-" + id.ToString("000000000");
}
