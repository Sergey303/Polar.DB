using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.Services;
using Polar.DB.Bench.Core.StringLikeLookup;

namespace Polar.DB.Bench.Engine.Sqlite;

internal static partial class SqliteStringLikeLookupExecutor
{
    private const string EngineKeyValue = "sqlite";

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
        SqliteConnection? connection = null;

        try
        {
            Directory.CreateDirectory(layout.Root);
            connection = Open(layout.DatabasePath);
            CreateSchema(connection);
            loadMs = Measure(() => Load(connection, options, cancellationToken));
            buildMs = options.UseNameIndex
                ? Measure(() => Execute(connection, "CREATE INDEX IF NOT EXISTS ix_records_name ON records(name);"))
                : 0.0;
            connection.Dispose();
            connection = null;
            reopenMs = Measure(() =>
            {
                connection = Open(layout.DatabasePath);
                rowCountAfterReopen = ScalarLong(connection, "SELECT COUNT(*) FROM records;");
            });

            var mismatches = new List<string>();
            using var command = CreateLikeCountCommand(connection!);
            foreach (var query in options.Queries)
            {
                var result = RunCase(command, query, options, cancellationToken);
                StringLikeLookupResultMetrics.AddCase(metrics, query, result);
                diagnostics[$"sqlite.eqp.{query.Key}"] = Explain(connection!, query.Pattern);
                if (result.MatchedCount != query.ExpectedCount)
                    mismatches.Add($"{query.Key}: expected={query.ExpectedCount}, actual={result.MatchedCount}");
            }

            semanticSuccess = rowCountAfterReopen == options.RecordCount && mismatches.Count == 0;
            if (!semanticSuccess)
                semanticError = "SQLite string-like semantic mismatch: " + string.Join(" | ", mismatches);
        }
        catch (Exception ex)
        {
            technicalSuccess = false;
            technicalError = ex.ToString();
        }
        finally
        {
            connection?.Dispose();
            total.Stop();
        }

        var artifacts = CollectArtifacts(layout.Root, workspace.WorkingDirectory);
        StringLikeLookupResultMetrics.AddCommon(metrics, total.Elapsed.TotalMilliseconds, loadMs, buildMs, reopenMs, rowCountAfterReopen, artifacts);
        diagnostics["querySemantics"] = DescribeQuerySemantics(options);
        diagnostics["searchMode"] = options.SearchMode;
        diagnostics["useNameIndex"] = options.UseNameIndex ? "true" : "false";
        diagnostics["rowCountAfterReopen"] = rowCountAfterReopen.ToString(CultureInfo.InvariantCulture);

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
                ["useNameIndex"] = options.UseNameIndex ? "true" : "false"
            },
            Notes = new List<string> { "SQLite string LIKE benchmark with explicit scan/index mode." }
        });
    }

    private static StringLikeCaseMeasurement RunCase(
        SqliteCommand command,
        StringLikeQueryCase query,
        StringLikeLookupOptions options,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < options.WarmupIterations; i++) _ = Count(command, query.Pattern);
        var samples = new List<double>(options.MeasuredIterations);
        long matched = 0;
        var rowsVisited = EstimateRowsVisited(query, options);
        for (var i = 0; i < options.MeasuredIterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var watch = Stopwatch.StartNew();
            matched = Count(command, query.Pattern);
            watch.Stop();
            samples.Add(watch.Elapsed.TotalMilliseconds);
        }
        return StringLikeCaseMeasurement.From(query, matched, rowsVisited(matched), samples);
    }

    private static Func<long, long> EstimateRowsVisited(StringLikeQueryCase query, StringLikeLookupOptions options)
    {
        if (query.Kind == StringLikeQueryKind.Contains || !options.UseNameIndex)
            return _ => options.RecordCount;
        return matched => matched;
    }

    private static string DescribeQuerySemantics(StringLikeLookupOptions options)
    {
        if (!options.UseNameIndex)
            return "SQL LIKE with case_sensitive_like=ON and no ix_records_name index; expected plan is table scan.";
        if (string.Equals(options.SearchMode, StringLikeLookupWorkload.SearchModeContainsScan, StringComparison.OrdinalIgnoreCase))
            return "SQL LIKE contains pattern with case_sensitive_like=ON; leading wildcard is expected to scan even if an index exists.";
        return "SQL LIKE with case_sensitive_like=ON and ix_records_name index for prefix-compatible patterns.";
    }

    private sealed record SqliteLayout(string Root, string DatabasePath);
    private static SqliteLayout CreateLayout(RunWorkspace workspace, string runId)
    {
        var root = Path.Combine(workspace.ArtifactsDirectory ?? workspace.WorkingDirectory, runId, "sqlite-string-like");
        return new SqliteLayout(root, Path.Combine(root, "string-like.sqlite"));
    }
}
