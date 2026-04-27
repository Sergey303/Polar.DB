using System;
using System.Collections.Generic;
using System.Linq;

namespace Polar.DB.Bench.Charts.Runtime;

/// <summary>
/// Describes the direction of a metric for best-cell highlighting.
/// </summary>
internal enum MetricDirection
{
    /// <summary>Lower values are better (e.g. latency, size).</summary>
    LowerIsBetter,
    /// <summary>Higher values are better (e.g. throughput, hit rate).</summary>
    HigherIsBetter,
    /// <summary>Zero is the ideal value (e.g. error count, wrong rows).</summary>
    ZeroIsBest,
    /// <summary>One (100%) is the ideal value (e.g. hit rate, success rate).</summary>
    OneIsBest,
    /// <summary>No direction preference (informational only).</summary>
    Neutral
}

/// <summary>
/// Unit category for a metric, used for formatting and section grouping.
/// </summary>
internal enum MetricUnit
{
    Milliseconds,
    Bytes,
    Count,
    Ratio,
    Percent,
    PerSecond,
    RowsPerSecond,
    QueriesPerSecond,
    MillisecondsPerQuery,
    MillisecondsPerRow,
    None
}

/// <summary>
/// Which statistic to display by default for this metric.
/// </summary>
internal enum PreferredStat
{
    P50,
    P95,
    Average,
    Max,
    Min
}

/// <summary>
/// Describes one known metric for report rendering.
/// </summary>
internal sealed record MetricDescriptor
{
    /// <summary>Raw metric key as it appears in the Metrics dictionary (e.g. "search.point.ms").</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Human-readable label for table headers.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Thematic section this metric belongs to.</summary>
    public string Section { get; init; } = string.Empty;

    /// <summary>Unit category for formatting.</summary>
    public MetricUnit Unit { get; init; }

    /// <summary>Direction for best-cell highlighting.</summary>
    public MetricDirection Direction { get; init; }

    /// <summary>Which statistic to show by default.</summary>
    public PreferredStat PreferredStat { get; init; } = PreferredStat.P50;

    /// <summary>Optional description shown in tooltip or appendix.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Priority within the section (lower = higher in table).
    /// Defaults to 100 for alphabetical fallback.
    /// </summary>
    public int Priority { get; init; } = 100;
}

/// <summary>
/// Static catalog of all known benchmark metrics with their display metadata.
/// Used by HtmlSectionRenderer to render thematic metric tables.
/// </summary>
internal static class MetricReportCatalog
{
    /// <summary>
    /// All known metric descriptors, keyed by metric key (case-insensitive).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, MetricDescriptor> DescriptorsByKey;

    /// <summary>
    /// All known metric descriptors grouped by section name.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<MetricDescriptor>> DescriptorsBySection;

    /// <summary>
    /// Ordered list of section names for rendering.
    /// </summary>
    public static readonly IReadOnlyList<string> SectionOrder;

    static MetricReportCatalog()
    {
        var descriptors = BuildDescriptors();
        DescriptorsByKey = new Dictionary<string, MetricDescriptor>(
            descriptors.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase));

        DescriptorsBySection = descriptors
            .GroupBy(d => d.Section)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<MetricDescriptor>)g.OrderBy(d => d.Priority).ThenBy(d => d.Label).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        SectionOrder = new[]
        {
            "Executive Summary",
            "Correctness / Status",
            "Timing & Stability",
            "Search: Point Lookup",
            "Search: Missing Point Lookup",
            "Search: Scan / Filter",
            "Storage Economics",
            "Memory / Runtime",
            "All Metrics Appendix"
        };
    }

    /// <summary>
    /// Looks up a descriptor by metric key. Returns null if the key is unknown.
    /// </summary>
    public static MetricDescriptor? TryGetDescriptor(string metricKey)
    {
        return DescriptorsByKey.TryGetValue(metricKey, out var descriptor) ? descriptor : null;
    }

    /// <summary>
    /// Returns descriptors for a given section, or empty list if section not found.
    /// </summary>
    public static IReadOnlyList<MetricDescriptor> GetDescriptorsForSection(string section)
    {
        return DescriptorsBySection.TryGetValue(section, out var descriptors) ? descriptors : Array.Empty<MetricDescriptor>();
    }

    /// <summary>
    /// Returns all metric keys that are not assigned to any known section.
    /// These should be rendered in the "All Metrics Appendix".
    /// </summary>
    public static IReadOnlyList<string> GetUnknownMetricKeys(IEnumerable<string> availableKeys)
    {
        return availableKeys
            .Where(key => !DescriptorsByKey.ContainsKey(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Determines the best value direction for a metric key.
    /// Returns LowerIsBetter for unknown metrics.
    /// </summary>
    public static MetricDirection GetDirection(string metricKey)
    {
        return TryGetDescriptor(metricKey)?.Direction ?? MetricDirection.LowerIsBetter;
    }

    /// <summary>
    /// Determines the preferred stat for a metric key.
    /// Returns P50 for unknown metrics.
    /// </summary>
    public static PreferredStat GetPreferredStat(string metricKey)
    {
        return TryGetDescriptor(metricKey)?.PreferredStat ?? PreferredStat.P50;
    }

    /// <summary>
    /// Determines the unit category for a metric key.
    /// Returns None for unknown metrics.
    /// </summary>
    public static MetricUnit GetUnit(string metricKey)
    {
        return TryGetDescriptor(metricKey)?.Unit ?? MetricUnit.None;
    }

    /// <summary>
    /// Returns the human-readable label for a metric key.
    /// Returns the key itself for unknown metrics.
    /// </summary>
    public static string GetLabel(string metricKey)
    {
        return TryGetDescriptor(metricKey)?.Label ?? metricKey;
    }

    private static List<MetricDescriptor> BuildDescriptors()
    {
        return new List<MetricDescriptor>
        {
            // ===== Executive Summary =====
            new()
            {
                Key = "ElapsedMs",
                Label = "Total elapsed time",
                Section = "Executive Summary",
                Unit = MetricUnit.Milliseconds,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 10,
                Description = "Total wall-clock time for the entire benchmark run (load + build + reopen + lookup)."
            },
            new()
            {
                Key = "TotalArtifactBytes",
                Label = "Total storage footprint",
                Section = "Executive Summary",
                Unit = MetricUnit.Bytes,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 20,
                Description = "Sum of primary and side artifact bytes on disk."
            },
            new()
            {
                Key = "LookupMs",
                Label = "Lookup phase time",
                Section = "Executive Summary",
                Unit = MetricUnit.Milliseconds,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 30,
                Description = "Time spent executing lookup queries."
            },

            // ===== Correctness / Status =====
            new()
            {
                Key = "technicalSuccessRate",
                Label = "Technical success rate",
                Section = "Correctness / Status",
                Unit = MetricUnit.Percent,
                Direction = MetricDirection.OneIsBest,
                PreferredStat = PreferredStat.Average,
                Priority = 10,
                Description = "Fraction of runs where infrastructure completed without error."
            },
            new()
            {
                Key = "semanticSuccessRate",
                Label = "Semantic success rate",
                Section = "Correctness / Status",
                Unit = MetricUnit.Percent,
                Direction = MetricDirection.OneIsBest,
                PreferredStat = PreferredStat.Average,
                Priority = 20,
                Description = "Fraction of runs where workload-level checks passed."
            },
            new()
            {
                Key = "search.point.semanticWrongRows",
                Label = "Point lookup wrong rows",
                Section = "Correctness / Status",
                Unit = MetricUnit.Count,
                Direction = MetricDirection.ZeroIsBest,
                PreferredStat = PreferredStat.Max,
                Priority = 30,
                Description = "Number of point lookups that returned incorrect results."
            },
            new()
            {
                Key = "search.scan.semanticWrongRows",
                Label = "Scan wrong rows",
                Section = "Correctness / Status",
                Unit = MetricUnit.Count,
                Direction = MetricDirection.ZeroIsBest,
                PreferredStat = PreferredStat.Max,
                Priority = 40,
                Description = "Number of scan queries that returned semantically incorrect rows."
            },
            new()
            {
                Key = "search.multi.semanticWrongRows",
                Label = "Multi-result wrong rows",
                Section = "Correctness / Status",
                Unit = MetricUnit.Count,
                Direction = MetricDirection.ZeroIsBest,
                PreferredStat = PreferredStat.Max,
                Priority = 50,
                Description = "Number of multi-result queries with incorrect result sets."
            },
            new()
            {
                Key = "search.multi.duplicateRows",
                Label = "Duplicate rows in results",
                Section = "Correctness / Status",
                Unit = MetricUnit.Count,
                Direction = MetricDirection.ZeroIsBest,
                PreferredStat = PreferredStat.Max,
                Priority = 60,
                Description = "Number of duplicate rows detected in multi-result query outputs."
            },

            // ===== Timing & Stability =====
            new()
            {
                Key = "ElapsedMs",
                Label = "Total elapsed time",
                Section = "Timing & Stability",
                Unit = MetricUnit.Milliseconds,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 10
            },
            new()
            {
                Key = "LoadMs",
                Label = "Load phase",
                Section = "Timing & Stability",
                Unit = MetricUnit.Milliseconds,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 20,
                Description = "Time to load data into the engine."
            },
            new()
            {
                Key = "BuildMs",
                Label = "Build phase",
                Section = "Timing & Stability",
                Unit = MetricUnit.Milliseconds,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 30,
                Description = "Time to build indexes or internal structures."
            },
            new()
            {
                Key = "ReopenMs",
                Label = "Reopen phase",
                Section = "Timing & Stability",
                Unit = MetricUnit.Milliseconds,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 40,
                Description = "Time to reopen or refresh the database."
            },
            new()
            {
                Key = "LookupMs",
                Label = "Lookup phase",
                Section = "Timing & Stability",
                Unit = MetricUnit.Milliseconds,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 50
            },

            // ===== Search: Point Lookup =====
            new()
            {
                Key = "search.point.ms",
                Label = "Total point lookup time",
                Section = "Search: Point Lookup",
                Unit = MetricUnit.Milliseconds,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 10,
                Description = "Total elapsed milliseconds for all point queries."
            },
            new()
            {
                Key = "search.point.queries",
                Label = "Point queries executed",
                Section = "Search: Point Lookup",
                Unit = MetricUnit.Count,
                Direction = MetricDirection.Neutral,
                PreferredStat = PreferredStat.Average,
                Priority = 20,
                Description = "Number of point queries executed."
            },
            new()
            {
                Key = "search.point.hits",
                Label = "Point lookup hits",
                Section = "Search: Point Lookup",
                Unit = MetricUnit.Count,
                Direction = MetricDirection.HigherIsBetter,
                PreferredStat = PreferredStat.Average,
                Priority = 30,
                Description = "Number of queries that found a matching record."
            },
            new()
            {
                Key = "search.point.misses",
                Label = "Point lookup misses",
                Section = "Search: Point Lookup",
                Unit = MetricUnit.Count,
                Direction = MetricDirection.ZeroIsBest,
                PreferredStat = PreferredStat.Average,
                Priority = 40,
                Description = "Number of queries that found no matching record."
            },
            new()
            {
                Key = "search.point.hitRate",
                Label = "Point lookup hit rate",
                Section = "Search: Point Lookup",
                Unit = MetricUnit.Ratio,
                Direction = MetricDirection.OneIsBest,
                PreferredStat = PreferredStat.Average,
                Priority = 50,
                Description = "Fraction of point queries that found a match (hits / queries)."
            },
            new()
            {
                Key = "search.point.emptyRate",
                Label = "Point lookup empty rate",
                Section = "Search: Point Lookup",
                Unit = MetricUnit.Ratio,
                Direction = MetricDirection.ZeroIsBest,
                PreferredStat = PreferredStat.Average,
                Priority = 60,
                Description = "Fraction of point queries that found no match (misses / queries)."
            },
            new()
            {
                Key = "search.point.msPerQuery",
                Label = "Ms per point query",
                Section = "Search: Point Lookup",
                Unit = MetricUnit.MillisecondsPerQuery,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 70,
                Description = "Average milliseconds per point query (totalMs / queryCount)."
            },
            new()
            {
                Key = "search.point.queriesPerSecond",
                Label = "Point queries per second",
                Section = "Search: Point Lookup",
                Unit = MetricUnit.QueriesPerSecond,
                Direction = MetricDirection.HigherIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 80,
                Description = "Point query throughput (queryCount / (totalMs / 1000))."
            },

            // ===== Search: Missing Point Lookup =====
            new()
            {
                Key = "search.point.misses",
                Label = "Missing key lookups",
                Section = "Search: Missing Point Lookup",
                Unit = MetricUnit.Count,
                Direction = MetricDirection.ZeroIsBest,
                PreferredStat = PreferredStat.Average,
                Priority = 10,
                Description = "Number of point lookups for non-existent keys."
            },
            new()
            {
                Key = "search.point.emptyRate",
                Label = "Missing key empty rate",
                Section = "Search: Missing Point Lookup",
                Unit = MetricUnit.Ratio,
                Direction = MetricDirection.ZeroIsBest,
                PreferredStat = PreferredStat.Average,
                Priority = 20,
                Description = "Fraction of missing-key lookups that correctly returned empty."
            },

            // ===== Search: Scan / Filter =====
            new()
            {
                Key = "search.scan.ms",
                Label = "Total scan time",
                Section = "Search: Scan / Filter",
                Unit = MetricUnit.Milliseconds,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 10,
                Description = "Total elapsed milliseconds for all scan queries."
            },
            new()
            {
                Key = "search.scan.queries",
                Label = "Scan queries executed",
                Section = "Search: Scan / Filter",
                Unit = MetricUnit.Count,
                Direction = MetricDirection.Neutral,
                PreferredStat = PreferredStat.Average,
                Priority = 20,
                Description = "Number of scan queries executed."
            },
            new()
            {
                Key = "search.scan.rowsScanned",
                Label = "Rows scanned",
                Section = "Search: Scan / Filter",
                Unit = MetricUnit.Count,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.Average,
                Priority = 30,
                Description = "Total rows examined during scan queries."
            },
            new()
            {
                Key = "search.scan.rowsMatched",
                Label = "Rows matched",
                Section = "Search: Scan / Filter",
                Unit = MetricUnit.Count,
                Direction = MetricDirection.Neutral,
                PreferredStat = PreferredStat.Average,
                Priority = 40,
                Description = "Total rows that passed the filter predicate."
            },
            new()
            {
                Key = "search.scan.msPerQuery",
                Label = "Ms per scan query",
                Section = "Search: Scan / Filter",
                Unit = MetricUnit.MillisecondsPerQuery,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 50,
                Description = "Average milliseconds per scan query (totalMs / queryCount)."
            },
            new()
            {
                Key = "search.scan.rowsScannedPerSecond",
                Label = "Rows scanned per second",
                Section = "Search: Scan / Filter",
                Unit = MetricUnit.RowsPerSecond,
                Direction = MetricDirection.HigherIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 60,
                Description = "Scan throughput (rowsScanned / (totalMs / 1000))."
            },
            new()
            {
                Key = "search.scan.rowsMatchedPerSecond",
                Label = "Rows matched per second",
                Section = "Search: Scan / Filter",
                Unit = MetricUnit.RowsPerSecond,
                Direction = MetricDirection.HigherIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 70,
                Description = "Filter throughput (rowsMatched / (totalMs / 1000))."
            },
            new()
            {
                Key = "search.scan.emptyResultCount",
                Label = "Scan empty results",
                Section = "Search: Scan / Filter",
                Unit = MetricUnit.Count,
                Direction = MetricDirection.ZeroIsBest,
                PreferredStat = PreferredStat.Max,
                Priority = 80,
                Description = "Number of scan queries that returned zero matching rows."
            },

            // ===== Storage Economics =====
            new()
            {
                Key = "TotalArtifactBytes",
                Label = "Total storage footprint",
                Section = "Storage Economics",
                Unit = MetricUnit.Bytes,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 10
            },
            new()
            {
                Key = "PrimaryArtifactBytes",
                Label = "Primary data bytes",
                Section = "Storage Economics",
                Unit = MetricUnit.Bytes,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 20,
                Description = "Bytes of primary data file(s)."
            },
            new()
            {
                Key = "SideArtifactBytes",
                Label = "Side artifact bytes",
                Section = "Storage Economics",
                Unit = MetricUnit.Bytes,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 30,
                Description = "Bytes of side artifacts (WAL, state, index, etc.)."
            },
            new()
            {
                Key = "bytesPerRecord",
                Label = "Bytes per record",
                Section = "Storage Economics",
                Unit = MetricUnit.Bytes,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 40,
                Description = "Total bytes divided by record count."
            },
            new()
            {
                Key = "indexBytesPerRecord",
                Label = "Index bytes per record",
                Section = "Storage Economics",
                Unit = MetricUnit.Bytes,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 50,
                Description = "Index/overhead bytes per record."
            },

            // ===== Memory / Runtime =====
            new()
            {
                Key = "memory.peakBytes",
                Label = "Peak memory usage",
                Section = "Memory / Runtime",
                Unit = MetricUnit.Bytes,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.Max,
                Priority = 10,
                Description = "Peak memory consumption during the benchmark run."
            },
            new()
            {
                Key = "memory.workingSetBytes",
                Label = "Working set size",
                Section = "Memory / Runtime",
                Unit = MetricUnit.Bytes,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.Max,
                Priority = 20,
                Description = "Working set memory size."
            },
            new()
            {
                Key = "memory.gcTotalBytes",
                Label = "GC total allocated",
                Section = "Memory / Runtime",
                Unit = MetricUnit.Bytes,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.Max,
                Priority = 30,
                Description = "Total bytes allocated by the GC."
            },
            new()
            {
                Key = "memory.gcCollections",
                Label = "GC collections",
                Section = "Memory / Runtime",
                Unit = MetricUnit.Count,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.Max,
                Priority = 40,
                Description = "Number of garbage collections triggered."
            },
            new()
            {
                Key = "runtime.cpuMs",
                Label = "CPU time",
                Section = "Memory / Runtime",
                Unit = MetricUnit.Milliseconds,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.P50,
                Priority = 50,
                Description = "Total CPU time consumed."
            },
            new()
            {
                Key = "runtime.threadCount",
                Label = "Thread count",
                Section = "Memory / Runtime",
                Unit = MetricUnit.Count,
                Direction = MetricDirection.LowerIsBetter,
                PreferredStat = PreferredStat.Max,
                Priority = 60,
                Description = "Number of threads used during the run."
            },
        };
    }
}
