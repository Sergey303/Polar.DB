using System;

namespace Polar.DB.Bench.Core.Services;

public static class RunIdFactory
{
    public static string Create(string experimentKey, string datasetProfileKey, string engineKey, string environmentClass)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ");
        return $"{timestamp}__{experimentKey}__{datasetProfileKey}__{engineKey}__{environmentClass}";
    }
}
