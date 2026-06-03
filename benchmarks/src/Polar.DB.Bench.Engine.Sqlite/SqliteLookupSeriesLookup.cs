using System;
using System.Threading;
using Polar.DB.Bench.Core.LookupSeries;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Engine.Sqlite;

internal static partial class SqliteLookupSeriesExecutor
{
    private static void RunDirectProbe(
        ExperimentSpec spec,
        LookupSeriesOptions options,
        SqliteLookupCommands lookup,
        SqliteLookupRunState state)
    {
        var probe = LookupSeriesWorkload.CreateFirstProbe(
            options.KeyKind,
            options.Mode,
            spec.Dataset.Seed ?? 1,
            spec.Dataset.RecordCount,
            options.DuplicateGroupSize);

        var result = ExecuteLookupProbe(lookup, probe);
        state.DirectLookupMs = result.TotalMs;
        state.DirectLookupIndexSearchMs = result.IndexSearchMs;
        state.DirectLookupMaterializationMs = result.MaterializationMs;
        state.DirectExpectedRows = probe.ExpectedCount;
        state.DirectReturnedRows = result.ReturnedRows;
        state.DirectHit = result.Matched;

        if (!result.Matched && !string.IsNullOrWhiteSpace(result.MismatchReason))
        {
            state.MismatchSamples.Add("direct lookup " + result.MismatchReason);
        }
    }

    private static void RunProbeSeries(
        ExperimentSpec spec,
        LookupSeriesOptions options,
        SqliteLookupCommands lookup,
        SqliteLookupRunState state,
        CancellationToken cancellationToken)
    {
        var probes = CreateProbes(spec, options);
        for (var i = 0; i < probes.Length; i++)
        {
            if ((i & 0x3FF) == 0) cancellationToken.ThrowIfCancellationRequested();
            var probe = probes[i];
            var result = ExecuteLookupProbe(lookup, probe);
            state.LookupMs += result.TotalMs;
            state.LookupIndexSearchMs += result.IndexSearchMs;
            state.LookupMaterializationMs += result.MaterializationMs;
            state.LookupReturnedRows += result.ReturnedRows;
            state.LookupExpectedRows += probe.ExpectedCount;
            state.LookupReturnedOffsets += result.ReturnedOffsets;
            state.LookupExpectedOffsets += probe.ExpectedCount;

            if (result.Matched) state.LookupProbeHits++;
            else AddLookupMiss(state, result);
        }
    }

    private static void AddLookupMiss(SqliteLookupRunState state, LookupProbeResult result)
    {
        state.LookupProbeMisses++;
        if (state.MismatchSamples.Count < 10 && !string.IsNullOrWhiteSpace(result.MismatchReason))
        {
            state.MismatchSamples.Add("lookup " + result.MismatchReason);
        }
    }

    private static LookupProbe[] CreateProbes(ExperimentSpec spec, LookupSeriesOptions options)
    {
        var random = new Random((spec.Dataset.Seed ?? 1) ^ LookupSeriesWorkload.CommonLookupSeedSalt);
        var probes = new LookupProbe[options.LookupCount];
        for (var i = 0; i < probes.Length; i++)
        {
            probes[i] = LookupSeriesWorkload.CreateRandomProbe(
                options.KeyKind, options.Mode, spec.Dataset.Seed ?? 1,
                spec.Dataset.RecordCount, options.DuplicateGroupSize, random);
        }

        return probes;
    }
}
