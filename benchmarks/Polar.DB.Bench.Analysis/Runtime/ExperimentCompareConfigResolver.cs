using System.Text.Json;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Analysis.Runtime;

internal readonly record struct ResolvedCompareConfig(
    bool HistoryEnabled,
    bool OtherExperimentsEnabled,
    IReadOnlyList<string> OtherExperiments);

internal static class ExperimentCompareConfigResolver
{
    public static ResolvedCompareConfig Resolve(ExperimentManifest manifest)
    {
        var historyEnabled = ResolveHistoryEnabled(manifest.Compare.History);
        var (otherEnabled, otherExperiments) = ResolveOtherExperiments(manifest.Compare.OtherExperiments);
        return new ResolvedCompareConfig(historyEnabled, otherEnabled, otherExperiments);
    }

    private static bool ResolveHistoryEnabled(JsonElement historyElement)
    {
        return historyElement.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => historyElement.GetArrayLength() > 0,
            JsonValueKind.Object => ResolveEnabledFlag(historyElement, defaultEnabled: true),
            _ => false
        };
    }

    private static (bool Enabled, IReadOnlyList<string> Experiments) ResolveOtherExperiments(JsonElement otherElement)
    {
        if (otherElement.ValueKind == JsonValueKind.Array)
        {
            var experiments = ReadStringArray(otherElement);
            return (experiments.Count > 0, experiments);
        }

        if (otherElement.ValueKind == JsonValueKind.Object)
        {
            var experiments = ReadStringArrayProperty(otherElement, "experiments");
            var enabled = ResolveEnabledFlag(otherElement, defaultEnabled: experiments.Count > 0);
            return (enabled, experiments);
        }

        if (otherElement.ValueKind == JsonValueKind.True)
        {
            return (true, Array.Empty<string>());
        }

        return (false, Array.Empty<string>());
    }

    private static bool ResolveEnabledFlag(JsonElement element, bool defaultEnabled)
    {
        if (element.TryGetProperty("enabled", out var enabledElement))
        {
            if (enabledElement.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (enabledElement.ValueKind == JsonValueKind.False)
            {
                return false;
            }
        }

        return defaultEnabled;
    }

    private static IReadOnlyList<string> ReadStringArrayProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var propertyElement))
        {
            return Array.Empty<string>();
        }

        return ReadStringArray(propertyElement);
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return element.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
