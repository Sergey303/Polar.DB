using System.Globalization;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Analysis.Runtime;

/// <summary>
/// Reads comparison metrics from one raw run.
/// The methods keep current fallback rules:
/// prefer explicit metrics, then infer values from artifacts or engine diagnostics when possible.
/// </summary>
internal static class ComparisonMetricReader
{
    public static double? ReadMetric(RunResult run, params string[] metricKeys)
    {
        foreach (var metricKey in metricKeys)
        {
            var metric = run.Metrics.FirstOrDefault(x => x.MetricKey.Equals(metricKey, StringComparison.OrdinalIgnoreCase));
            if (metric is not null)
            {
                return metric.Value;
            }
        }

        return null;
    }

    public static double? ReadTotalArtifactBytes(RunResult run)
    {
        var metric = ReadMetric(run, "totalArtifactBytes");
        if (metric.HasValue)
        {
            return metric.Value;
        }

        if (run.Artifacts.Count > 0)
        {
            return run.Artifacts.Sum(artifact => (double)artifact.Bytes);
        }

        return null;
    }

    public static double? ReadPrimaryArtifactBytes(RunResult run)
    {
        var metric = ReadMetric(run, "primaryDataBytes", "primaryDatabaseBytes");
        if (metric.HasValue)
        {
            return metric.Value;
        }

        if (run.Artifacts.Count > 0)
        {
            var primary = run.Artifacts
                .Where(artifact => artifact.Role is ArtifactRole.PrimaryData or ArtifactRole.PrimaryDatabase)
                .Sum(artifact => (double)artifact.Bytes);

            if (primary > 0.0)
            {
                return primary;
            }
        }

        if (run.EngineDiagnostics is not null)
        {
            if (TryReadDiagnostic(run.EngineDiagnostics, "primaryDataFileBytes", out var polarPrimary))
            {
                return polarPrimary;
            }

            if (TryReadDiagnostic(run.EngineDiagnostics, "dbBytes", out var sqlitePrimary))
            {
                return sqlitePrimary;
            }
        }

        return null;
    }

    public static double? ReadSideArtifactBytes(RunResult run)
    {
        var metric = ReadMetric(run, "sideArtifactBytes");
        if (metric.HasValue)
        {
            return metric.Value;
        }

        var total = ReadTotalArtifactBytes(run);
        var primary = ReadPrimaryArtifactBytes(run);
        if (total.HasValue && primary.HasValue)
        {
            return Math.Max(0.0, total.Value - primary.Value);
        }

        return null;
    }

    private static bool TryReadDiagnostic(IReadOnlyDictionary<string, string> diagnostics, string key, out double value)
    {
        if (diagnostics.TryGetValue(key, out var text) &&
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = 0.0;
        return false;
    }
}
