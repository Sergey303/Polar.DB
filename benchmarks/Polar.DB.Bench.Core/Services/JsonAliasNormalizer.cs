using System.Text.Json;
using System.Text.Json.Nodes;

namespace Polar.DB.Bench.Core.Services;

public static class JsonAliasNormalizer
{
    private static readonly HashSet<string> FreeFormScopeKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "diagnostics",
            "engineDiagnostics",
            "derived",
            "derivedMetrics",
            "metrics",
            "options",
            "parameters",
            "tags"
        };

    private static readonly IReadOnlyDictionary<string, string> LegacyToPreferred =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["analysisTimestampUtc"] = "analyzedAt",
            ["average"] = "avg",
            ["appliesTo"] = "applies",
            ["baselineId"] = "baseline",
            ["batchCount"] = "batches",
            ["comparisonId"] = "comparison",
            ["comparisonSetId"] = "set",
            ["currentDirectory"] = "cwd",
            ["datasetProfileKey"] = "dataset",
            ["derivedMetrics"] = "derived",
            ["elapsedMsSingleRun"] = "elapsed",
            ["engineDiagnostics"] = "diagnostics",
            ["engineKey"] = "engine",
            ["engineSeries"] = "series",
            ["environment"] = "env",
            ["environmentClass"] = "env",
            ["experimentKey"] = "experiment",
            ["fairnessProfile"] = "fairness",
            ["faultProfile"] = "fault",
            ["faultProfileKey"] = "type",
            ["fixedSlack"] = "slack",
            ["frameworkDescription"] = "framework",
            ["hypothesisId"] = "hypothesis",
            ["is64BitProcess"] = "is64Bit",
            ["loadMs"] = "load",
            ["buildMs"] = "build",
            ["lookupMs"] = "lookup",
            ["lookupBatchCount"] = "lookupBatch",
            ["machineName"] = "machine",
            ["maxRegressionPercent"] = "maxRegression",
            ["measuredRunCount"] = "measured",
            ["metricKey"] = "metric",
            ["missingCount"] = "missing",
            ["osArchitecture"] = "osArch",
            ["osDescription"] = "os",
            ["overallStatus"] = "status",
            ["primaryArtifactBytes"] = "primaryBytes",
            ["processArchitecture"] = "processArch",
            ["processorCount"] = "cpuCount",
            ["profileKey"] = "profile",
            ["policyId"] = "policy",
            ["rawResultPath"] = "raw",
            ["rawResultPaths"] = "raw",
            ["recordCount"] = "count",
            ["relativePath"] = "path",
            ["reopenMs"] = "reopen",
            ["requiredCapabilities"] = "requires",
            ["researchQuestionId"] = "research",
            ["runId"] = "run",
            ["runRole"] = "role",
            ["runSeriesSequenceNumber"] = "seq",
            ["runTimestampUtc"] = "at",
            ["semanticEvaluatedCount"] = "semanticChecked",
            ["semanticFailureReason"] = "semanticError",
            ["semanticSuccess"] = "semantic",
            ["semanticSuccessCount"] = "semanticOk",
            ["sideArtifactBytes"] = "sideBytes",
            ["technicalFailureReason"] = "technicalError",
            ["technicalSuccess"] = "technical",
            ["technicalSuccessCount"] = "technicalOk",
            ["timestampUtc"] = "at",
            ["totalArtifactBytes"] = "totalBytes",
            ["userName"] = "user",
            ["warmupRunCount"] = "warmup",
            ["workloadKey"] = "type",
            ["parameters"] = "options",
            ["lookupCount"] = "lookup",
            ["absoluteMax"] = "max"
        };

    public static async Task<T?> DeserializeAsync<T>(
        Stream stream,
        JsonSerializerOptions options,
        CancellationToken cancellationToken = default)
    {
        var node = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
        if (node is null)
        {
            return default;
        }

        Normalize(node, parentKey: null);
        return node.Deserialize<T>(options);
    }

    private static void Normalize(JsonNode node, string? parentKey)
    {
        switch (node)
        {
            case JsonObject obj:
                NormalizeObject(obj, parentKey);
                break;
            case JsonArray arr:
                foreach (var child in arr)
                {
                    if (child is not null)
                    {
                        Normalize(child, parentKey: null);
                    }
                }

                break;
        }
    }

    private static void NormalizeObject(JsonObject obj, string? parentKey)
    {
        var entries = obj.ToArray();
        foreach (var (key, value) in entries)
        {
            if (value is not null)
            {
                Normalize(value, key);
            }
        }

        if (IsFreeFormScope(parentKey))
        {
            return;
        }

        foreach (var (key, value) in entries)
        {
            var preferred = ResolvePreferredName(key, parentKey);
            if (preferred is null)
            {
                continue;
            }

            if (obj.ContainsKey(preferred))
            {
                continue;
            }

            obj.Remove(key);
            obj[preferred] = value;
        }
    }

    private static bool IsFreeFormScope(string? parentKey)
    {
        return !string.IsNullOrWhiteSpace(parentKey) && FreeFormScopeKeys.Contains(parentKey);
    }

    private static string? ResolvePreferredName(string key, string? parentKey)
    {
        if (key.Equals("fairnessProfileKey", StringComparison.OrdinalIgnoreCase))
        {
            return parentKey is "fairnessProfile" or "fairness"
                ? "type"
                : "fairness";
        }

        return LegacyToPreferred.TryGetValue(key, out var preferred)
            ? preferred
            : null;
    }
}
