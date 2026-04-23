using System;
using System.Collections.Generic;
using System.Linq;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Analysis.Runtime;

internal static class PolicyEvaluator
{
    public static IReadOnlyList<MetricCheckResult> Evaluate(
        RunResult raw,
        PolicyContract policy,
        BaselineDescriptor baseline)
    {
        var values = raw.Metrics.ToDictionary(x => x.MetricKey, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var checks = new List<MetricCheckResult>();

        foreach (var guard in policy.Guards)
        {
            values.TryGetValue(guard.MetricKey, out var actual);
            baseline.Metrics.TryGetValue(guard.MetricKey, out var baselineValue);

            var status = "Passed";
            double? expectedMax = null;
            string? message = null;

            if (guard.Mode.Equals("absolute_max", StringComparison.OrdinalIgnoreCase))
            {
                expectedMax = guard.AbsoluteMax;
                if (expectedMax.HasValue && actual > expectedMax.Value)
                {
                    status = guard.Severity;
                    message = $"Metric '{guard.MetricKey}' exceeded absolute max.";
                }
            }
            else if (guard.Mode.Equals("relative_to_baseline", StringComparison.OrdinalIgnoreCase))
            {
                var percent = guard.MaxRegressionPercent ?? 0.0;
                var slack = guard.FixedSlack ?? 0.0;
                expectedMax = baselineValue * (1.0 + percent / 100.0) + slack;
                if (actual > expectedMax.Value)
                {
                    status = guard.Severity;
                    message = $"Metric '{guard.MetricKey}' exceeded allowed regression budget.";
                }
            }
            else
            {
                status = "AnalysisFailed";
                message = $"Unknown guard mode '{guard.Mode}'.";
            }

            checks.Add(new MetricCheckResult
            {
                MetricKey = guard.MetricKey,
                Status = status,
                Actual = actual,
                ExpectedMax = expectedMax,
                BaselineValue = baselineValue,
                Message = message
            });
        }

        return checks;
    }
}
