using System;
using System.Collections.Generic;
using System.Globalization;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Core.StringLikeLookup;

public static class StringLikeLookupWorkload
{
    public const string WorkloadKey = "string-like-prefix-lookup";
    public const string GroupCountOption = "groupCount";
    public const string SubGroupCountOption = "subGroupCount";
    public const string PayloadBytesOption = "payloadBytes";
    public const string WarmupIterationsOption = "warmupIterations";
    public const string MeasuredIterationsOption = "measuredIterations";
    public const string IncludeContainsScanOption = "includeContainsScan";
    public const string PivotIdOption = "pivotId";

    public static bool IsStringLike(string? workloadKey) =>
        string.Equals(workloadKey, WorkloadKey, StringComparison.OrdinalIgnoreCase);

    public static StringLikeLookupOptions Resolve(ExperimentSpec spec)
    {
        if (!IsStringLike(spec.Workload.WorkloadKey))
            throw new NotSupportedException($"Workload '{spec.Workload.WorkloadKey}' is not '{WorkloadKey}'.");
        if (spec.Dataset.RecordCount <= 0)
            throw new InvalidOperationException("dataset.count must be >= 1 for string-like-prefix-lookup.");
        if (spec.Dataset.RecordCount > int.MaxValue)
            throw new InvalidOperationException("string-like-prefix-lookup currently supports dataset.count <= int.MaxValue.");

        var groupCount = Positive(spec.Workload, GroupCountOption, 100);
        var subGroupCount = Positive(spec.Workload, SubGroupCountOption, 100);
        var payloadBytes = NonNegative(spec.Workload, PayloadBytesOption, 64);
        var warmup = NonNegative(spec.Workload, WarmupIterationsOption, 5);
        var measured = Positive(spec.Workload, MeasuredIterationsOption, spec.Workload.LookupCount ?? 30);
        var includeContains = Boolean(spec.Workload, IncludeContainsScanOption, true);
        var pivot = Math.Clamp(NonNegative(spec.Workload, PivotIdOption, 742), 0, checked((int)spec.Dataset.RecordCount - 1));
        var seed = spec.Dataset.Seed ?? 20260507;

        var options = new StringLikeLookupOptions(
            checked((int)spec.Dataset.RecordCount), groupCount, subGroupCount,
            payloadBytes, seed, warmup, measured, includeContains, pivot);

        return options with { Queries = CreateCases(options) };
    }

    public static IEnumerable<StringLikeRecord> GenerateRecords(StringLikeLookupOptions options)
    {
        var payload = new string('x', options.PayloadBytes);
        for (var id = 0; id < options.RecordCount; id++)
        {
            var name = CreateName(id, options.GroupCount, options.SubGroupCount);
            yield return new StringLikeRecord(id, name, payload);
        }
    }

    private static IReadOnlyList<StringLikeQueryCase> CreateCases(StringLikeLookupOptions options)
    {
        var full = CreateName(options.PivotId, options.GroupCount, options.SubGroupCount);
        var group = options.PivotId % options.GroupCount;
        var sub = (options.PivotId / options.GroupCount) % options.SubGroupCount;
        var groupPrefix = $"grp{group:D4}/";
        var subPrefix = $"grp{group:D4}/sub{sub:D4}/";
        var subToken = $"sub{sub:D4}";
        var result = new List<StringLikeQueryCase>
        {
            new("exact1", StringLikeQueryKind.Exact, full, full, 1),
            new("prefix1", StringLikeQueryKind.Prefix, full + "%", full, 1),
            new("prefixSmall", StringLikeQueryKind.Prefix, subPrefix + "%", subPrefix, CountGroupSub(options, group, sub)),
            new("prefixMedium", StringLikeQueryKind.Prefix, groupPrefix + "%", groupPrefix, CountGroup(options, group))
        };
        if (options.IncludeContainsScan)
            result.Add(new StringLikeQueryCase("containsScan", StringLikeQueryKind.Contains, "%" + subToken + "%", subToken, CountSub(options, sub)));
        return result;
    }

    private static string CreateName(int id, int groupCount, int subGroupCount) =>
        $"grp{id % groupCount:D4}/sub{(id / groupCount) % subGroupCount:D4}/item{id:D8}";

    private static long CountGroup(StringLikeLookupOptions o, int group) =>
        group >= o.RecordCount ? 0 : 1L + ((o.RecordCount - 1L - group) / o.GroupCount);

    private static long CountGroupSub(StringLikeLookupOptions o, int group, int sub)
    {
        var first = sub * o.GroupCount + group;
        var cycle = o.GroupCount * o.SubGroupCount;
        return first >= o.RecordCount ? 0 : 1L + ((o.RecordCount - 1L - first) / cycle);
    }

    private static long CountSub(StringLikeLookupOptions o, int sub)
    {
        var total = 0L;
        for (var group = 0; group < o.GroupCount; group++) total += CountGroupSub(o, group, sub);
        return total;
    }

    private static int Positive(WorkloadSpec w, string key, int fallback) => Math.Max(1, Int(w, key, fallback));
    private static int NonNegative(WorkloadSpec w, string key, int fallback) => Math.Max(0, Int(w, key, fallback));
    private static int Int(WorkloadSpec w, string key, int fallback) =>
        w.Parameters is not null && w.Parameters.TryGetValue(key, out var value) &&
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    private static bool Boolean(WorkloadSpec w, string key, bool fallback) =>
        w.Parameters is not null && w.Parameters.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;
}

public sealed record StringLikeLookupOptions(
    int RecordCount, int GroupCount, int SubGroupCount, int PayloadBytes,
    int Seed, int WarmupIterations, int MeasuredIterations, bool IncludeContainsScan, int PivotId)
{
    public IReadOnlyList<StringLikeQueryCase> Queries { get; init; } = Array.Empty<StringLikeQueryCase>();
}
