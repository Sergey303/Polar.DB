using System;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Polar.DB.Bench.Core.LookupSeries;

namespace Polar.DB.Bench.Engine.Sqlite;

internal static partial class SqliteLookupSeriesExecutor
{
    internal static object ConvertKeyForSqlite(IComparable key, LookupKeyKind keyKind)
    {
        return keyKind switch
        {
            LookupKeyKind.Int32 => Convert.ToInt32(key, CultureInfo.InvariantCulture),
            LookupKeyKind.Int64 => Convert.ToInt64(key, CultureInfo.InvariantCulture),
            LookupKeyKind.Guid => key switch
            {
                Guid guid => guid.ToString("D"),
                string text => Guid.Parse(text).ToString("D"),
                _ => Guid.Parse(Convert.ToString(key, CultureInfo.InvariantCulture)!).ToString("D")
            },
            _ => throw new ArgumentOutOfRangeException(nameof(keyKind))
        };
    }

    internal static IComparable ReadKey(SqliteDataReader reader, LookupKeyKind keyKind)
    {
        return keyKind switch
        {
            LookupKeyKind.Int32 => checked((int)reader.GetInt64(0)),
            LookupKeyKind.Int64 => reader.GetInt64(0),
            LookupKeyKind.Guid => Guid.Parse(reader.GetString(0)),
            _ => throw new ArgumentOutOfRangeException(nameof(keyKind))
        };
    }

    private static string ResolveLookupKeySqlType(LookupKeyKind keyKind)
    {
        return keyKind == LookupKeyKind.Guid ? "TEXT" : "INTEGER";
    }
}
