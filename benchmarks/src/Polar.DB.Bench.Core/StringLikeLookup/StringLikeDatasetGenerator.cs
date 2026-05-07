using System;
using System.Collections.Generic;
using System.Text;

namespace Polar.DB.Bench.Core.StringLikeLookup;

public static class StringLikeDatasetGenerator
{
    public static IReadOnlyList<StringLikeRecord> Generate(StringLikeDatasetOptions options)
    {
        options.Validate();
        var records = new List<StringLikeRecord>(options.RecordCount);
        var payload = CreatePayload(options.PayloadBytes, options.Seed);

        for (var id = 0; id < options.RecordCount; id++)
        {
            var group = id % options.GroupCount;
            var sub = (id / options.GroupCount) % options.SubGroupCount;
            var name = $"grp{group:D4}/sub{sub:D4}/item{id:D8}";
            records.Add(new StringLikeRecord(id, name, payload));
        }

        records.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
        return records;
    }

    private static string CreatePayload(int bytes, int seed)
    {
        if (bytes == 0) return string.Empty;

        var random = new Random(seed);
        var sb = new StringBuilder(bytes);

        for (var i = 0; i < bytes; i++)
        {
            sb.Append((char)('a' + random.Next(0, 26)));
        }

        return sb.ToString();
    }
}
