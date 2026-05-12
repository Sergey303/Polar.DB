using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;
using Polar.DB.Bench.Core.StringLikeLookup;

namespace Polar.DB.Bench.Engine.PolarDb;

internal static partial class PolarDbStringLikeLookupExecutor
{
    private const string EngineKeyValue = "polar-db";

    public static Task<RunResult> ExecuteAsync(
        ExperimentSpec spec,
        RunWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        var options = StringLikeLookupWorkload.Resolve(spec);
        var environment = EnvironmentCollector.Collect(workspace.EnvironmentClass, workspace.RootDirectory);
        var runId = RunIdFactory.Create(spec.ExperimentKey, spec.Dataset.ProfileKey, EngineKeyValue, environment.EnvironmentClass);
        var layout = CreateLayout(workspace, runId);
        var metrics = new List<RunMetric>();
        var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var technicalSuccess = true;
        var technicalError = (string?)null;
        var semanticSuccess = false;
        var semanticError = (string?)null;
        var total = Stopwatch.StartNew();
        var loadMs = 0.0;
        var buildMs = 0.0;
        var reopenMs = 0.0;
        long rowCountAfterReopen = 0;
        long appendOffsetAfterReopen = 0;
        USequence? sequence = null;
        USequence? active = null;

        try
        {
            Directory.CreateDirectory(layout.Root);
            sequence = CreateSequence(layout, options);
            loadMs = Measure(() => sequence.Load(GenerateRows(options)));
            buildMs = Measure(sequence.Build);
            sequence.Close();
            sequence = null;

            reopenMs = Measure(() =>
            {
                active = CreateSequence(layout, options);
                active.Refresh();
                rowCountAfterReopen = active.sequence.Count();
                appendOffsetAfterReopen = active.sequence.ElementOffset();
            });

            var mismatches = new List<string>();
            foreach (var query in options.Queries)
            {
                var result = RunCase(active!, query, options, cancellationToken);
                StringLikeLookupResultMetrics.AddCase(metrics, query, result);
                if (result.MatchedCount != query.ExpectedCount)
                    mismatches.Add($"{query.Key}: expected={query.ExpectedCount}, actual={result.MatchedCount}");
            }

            active!.Close();
            active = null;
            semanticSuccess = rowCountAfterReopen == options.RecordCount && mismatches.Count == 0;
            if (!semanticSuccess)
                semanticError = "Polar.DB string-like semantic mismatch: " + string.Join(" | ", mismatches);
        }
        catch (Exception ex)
        {
            technicalSuccess = false;
            technicalError = ex.ToString();
        }
        finally
        {
            TryClose(sequence);
            TryClose(active);
            total.Stop();
        }

        var artifacts = CollectArtifacts(layout.Root, workspace.WorkingDirectory);
        StringLikeLookupResultMetrics.AddCommon(metrics, total.Elapsed.TotalMilliseconds, loadMs, buildMs, reopenMs, rowCountAfterReopen, artifacts);
        diagnostics["querySemantics"] = DescribeQuerySemantics(options);
        diagnostics["searchMode"] = options.SearchMode;
        diagnostics["useNameIndex"] = options.UseNameIndex.ToString(CultureInfo.InvariantCulture);
        diagnostics["rowCountAfterReopen"] = rowCountAfterReopen.ToString(CultureInfo.InvariantCulture);
        diagnostics["appendOffsetAfterReopen"] = appendOffsetAfterReopen.ToString(CultureInfo.InvariantCulture);

        return Task.FromResult(new RunResult
        {
            RunId = runId,
            TimestampUtc = DateTimeOffset.UtcNow,
            EngineKey = EngineKeyValue,
            ExperimentKey = spec.ExperimentKey,
            DatasetProfileKey = spec.Dataset.ProfileKey,
            FairnessProfileKey = spec.FairnessProfile?.FairnessProfileKey ?? "unspecified",
            Environment = environment,
            TechnicalSuccess = technicalSuccess,
            TechnicalFailureReason = technicalError,
            SemanticSuccess = semanticSuccess,
            SemanticFailureReason = semanticError,
            Metrics = metrics,
            Artifacts = artifacts,
            EngineDiagnostics = diagnostics,
            Tags = new Dictionary<string, string>
            {
                ["workload"] = StringLikeLookupWorkload.WorkloadKey,
                ["searchMode"] = options.SearchMode,
                ["useNameIndex"] = options.UseNameIndex.ToString(CultureInfo.InvariantCulture)
            },
            Notes = new List<string> { "Polar.DB string-like lookup benchmark with explicit scan/index mode." }
        });
    }

    private static StringLikeCaseMeasurement RunCase(
        USequence sequence,
        StringLikeQueryCase query,
        StringLikeLookupOptions options,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < options.WarmupIterations; i++) _ = Count(sequence, query, options);
        var samples = new List<double>(options.MeasuredIterations);
        long matched = 0;
        long rowsVisited = 0;
        for (var i = 0; i < options.MeasuredIterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var watch = Stopwatch.StartNew();
            (matched, rowsVisited) = Count(sequence, query, options);
            watch.Stop();
            samples.Add(watch.Elapsed.TotalMilliseconds);
        }
        return StringLikeCaseMeasurement.From(query, matched, rowsVisited, samples);
    }

    private static string DescribeQuerySemantics(StringLikeLookupOptions options)
    {
        if (string.Equals(options.SearchMode, StringLikeLookupWorkload.SearchModePrefixScan, StringComparison.OrdinalIgnoreCase))
            return "Polar.DB sequence scan with exact/prefix string predicates; name index is disabled by experiment option.";
        if (string.Equals(options.SearchMode, StringLikeLookupWorkload.SearchModeContainsScan, StringComparison.OrdinalIgnoreCase))
            return "Polar.DB sequence scan with contains predicate; contains LIKE is intentionally not treated as prefix-indexable.";
        if (options.UseNameIndex)
            return "Polar.DB SVectorIndex.GetAllByLike prefix comparator/range traversal for exact/prefix cases.";
        return "Polar.DB string-like lookup without name index; expected to use sequence scan for scan-mode cases.";
    }
}
