using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.StringLikeLookup;

namespace Polar.DB.Bench.Engine.PolarDb;

internal static partial class PolarDbStringLikeLookupExecutor
{
    private static USequence CreateSequence(PolarDbStringLayout layout)
    {
        var streamIndex = 0;
        var streamPaths = new[]
        {
            layout.DataPath,
            layout.PrimaryHashIndexPath,
            layout.PrimaryOffsetIndexPath,
            layout.NameValueIndexPath,
            layout.NameOffsetIndexPath
        };

        Stream StreamGen()
        {
            if (streamIndex >= streamPaths.Length)
                throw new InvalidOperationException("PolarDB string-like stream generator was called too many times.");
            return new FileStream(streamPaths[streamIndex++], FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        }

        var sequence = new USequence(
            CreateRecordType(),
            layout.StatePath,
            StreamGen,
            IsEmpty,
            ReadId,
            key => (int)key,
            optimise: true);

        sequence.uindexes = new IUIndex[]
        {
            new SVectorIndex(StreamGen, sequence, row => new[] { ReadName(row) }, ignorecase: false)
        };
        return sequence;
    }

    private static IEnumerable<object> GenerateRows(StringLikeLookupOptions options)
    {
        foreach (var record in StringLikeLookupWorkload.GenerateRecords(options))
            yield return new object[] { record.Id, record.Name, record.Payload };
    }

    private static long Count(USequence sequence, StringLikeQueryCase query)
    {
        if (query.Kind == StringLikeQueryKind.Contains)
        {
            var count = 0L;
            sequence.Scan((_, row) =>
            {
                if (ReadName(row).Contains(query.Prefix, StringComparison.Ordinal)) count++;
                return true;
            });
            return count;
        }

        var rows = sequence.GetAllByLike(0, query.Prefix);
        return query.Kind == StringLikeQueryKind.Exact
            ? rows.LongCount(row => string.Equals(ReadName(row), query.Prefix, StringComparison.Ordinal))
            : rows.LongCount();
    }

    private static PTypeRecord CreateRecordType() => new(
        new NamedType("id", new PType(PTypeEnumeration.integer)),
        new NamedType("name", new PType(PTypeEnumeration.sstring)),
        new NamedType("payload", new PType(PTypeEnumeration.sstring)));

    private static bool IsEmpty(object row) => row is not object[] values || values.Length < 2;

    private static IComparable ReadId(object row)
    {
        if (row is object[] values && values.Length > 0)
            return Convert.ToInt32(values[0], CultureInfo.InvariantCulture);
        throw new InvalidOperationException("PolarDB string-like row must have id at index 0.");
    }

    private static string ReadName(object row)
    {
        if (row is object[] values && values.Length > 1 && values[1] is string name)
            return name;
        throw new InvalidOperationException("PolarDB string-like row must have name at index 1.");
    }

    private static double Measure(Action action)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        action();
        watch.Stop();
        return watch.Elapsed.TotalMilliseconds;
    }

    private static void TryClose(USequence? sequence)
    {
        try { sequence?.Close(); }
        catch { }
    }
}
