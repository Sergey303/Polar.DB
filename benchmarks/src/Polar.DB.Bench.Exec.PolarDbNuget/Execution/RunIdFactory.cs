namespace Polar.DB.Bench.Exec.PolarDbNuget.Execution;

internal static class RunIdFactory
{
    public static string Create(string engineKey, string mode, DateTimeOffset timestampUtc)
    {
        var stamp = timestampUtc.UtcDateTime.ToString("yyyyMMddTHHmmssfffZ");
        var safeEngine = Sanitize(engineKey);
        var safeMode = Sanitize(mode);
        return $"{stamp}.{safeEngine}.{safeMode}.{Guid.NewGuid():N}";
    }

    private static string Sanitize(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-').ToArray();
        return new string(chars).Trim('-');
    }
}
