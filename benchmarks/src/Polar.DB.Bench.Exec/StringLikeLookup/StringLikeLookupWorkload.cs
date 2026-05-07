using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Polar.DB.Bench.Core.LookupSeries;
using Polar.DB.Bench.Core.StringLikeLookup;

namespace Polar.DB.Bench.Exec.StringLikeLookup;

public sealed class StringLikeLookupWorkload
{
    public async ValueTask<StringLikeRunResult> RunAsync(
        ILikeLookupEngine engine,
        StringLikeExperimentOptions options,
        CancellationToken cancellationToken = default)
    {
        options.Validate();
        var started = DateTimeOffset.UtcNow;
        var environment = EnvironmentSnapshot.Capture(options.GitCommit, options.GitBranch);
        var records = StringLikeDatasetGenerator.Generate(options.Dataset);

        var loadElapsed = await MeasureAsync(
            ct => engine.LoadAsync(records, ct), cancellationToken);
        var buildElapsed = await MeasureAsync(engine.BuildAsync, cancellationToken);
        var reopenElapsed = await MeasureAsync(engine.ReopenAsync, cancellationToken);

        var caseResults = new List<StringLikeCaseResult>(options.Queries.Count);
        foreach (var query in options.Queries)
        {
            caseResults.Add(await RunCaseAsync(engine, query, options, cancellationToken));
        }

        var artifacts = await engine.CollectArtifactsAsync(cancellationToken);
        return new StringLikeRunResult(
            engine.EngineKey,
            started,
            environment,
            loadElapsed,
            buildElapsed,
            reopenElapsed,
            caseResults,
            artifacts);
    }

    private static async ValueTask<TimeSpan> MeasureAsync(
        Func<CancellationToken, ValueTask> action,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        await action(cancellationToken);
        sw.Stop();
        return sw.Elapsed;
    }

    private static async ValueTask<StringLikeCaseResult> RunCaseAsync(
        ILikeLookupEngine engine,
        StringLikeQueryCase query,
        StringLikeExperimentOptions options,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < options.WarmupIterations; i++)
        {
            _ = await engine.LookupAsync(query, cancellationToken);
        }

        var samples = new List<double>(options.MeasuredIterations);
        EngineLookupResult? last = null;

        for (var i = 0; i < options.MeasuredIterations; i++)
        {
            var sw = Stopwatch.StartNew();
            last = await engine.LookupAsync(query, cancellationToken);
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        last ??= EngineLookupResult.FromCount(0);
        return new StringLikeCaseResult(
            query.Key,
            query.Kind,
            query.Pattern,
            last.MatchedCount,
            Stats.TrimmedMean(samples),
            Stats.Percentile(samples, 95),
            samples.Min(),
            samples.Max(),
            last.Diagnostics);
    }
}
