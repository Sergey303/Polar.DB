using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Analysis.Runtime;

/// <summary>
/// Resolved compare config from experiment manifest.
/// Both flags are simple bools with no manual experiment lists.
/// </summary>
internal readonly record struct ResolvedCompareConfig(
    bool HistoryEnabled,
    bool OtherExperimentsEnabled);

/// <summary>
/// Resolves compare config from the simplified ExperimentCompareSpec.
/// No complex JSON parsing, no manual experiment lists.
/// Cross-experiment context is auto-discovered from experiment folders.
/// </summary>
internal static class ExperimentCompareConfigResolver
{
    /// <summary>
    /// Reads the simplified bool-only compare config from the manifest.
    /// When compare section is absent, defaults apply (both true).
    /// </summary>
    public static ResolvedCompareConfig Resolve(ExperimentManifest manifest)
    {
        return new ResolvedCompareConfig(
            HistoryEnabled: manifest.Compare.History,
            OtherExperimentsEnabled: manifest.Compare.OtherExperiments);
    }
}
