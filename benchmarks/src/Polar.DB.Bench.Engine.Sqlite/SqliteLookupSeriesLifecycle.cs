using System;
using System.Globalization;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;
using Polar.DB.Bench.Core.LookupSeries;
using Polar.DB.Bench.Core.Models;

namespace Polar.DB.Bench.Engine.Sqlite;

internal static partial class SqliteLookupSeriesExecutor
{
    private static SqliteConnection OpenConnection(string databasePath, string fairnessProfileKey)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? ".");
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        ApplyFairnessPragmas(connection, fairnessProfileKey);
        return connection;
    }

    private static void ApplyFairnessPragmas(SqliteConnection connection, string fairnessProfileKey)
    {
        ExecuteNonQuery(connection, "PRAGMA journal_mode = WAL;");

        if (fairnessProfileKey.Equals("durability-balanced", StringComparison.OrdinalIgnoreCase))
        {
            ExecuteNonQuery(connection, "PRAGMA synchronous = FULL;");
            ExecuteNonQuery(connection, "PRAGMA temp_store = FILE;");
            return;
        }

        ExecuteNonQuery(connection, "PRAGMA synchronous = NORMAL;");
        ExecuteNonQuery(connection, "PRAGMA temp_store = MEMORY;");
    }

    private static void CreateSchema(SqliteConnection connection, LookupSeriesOptions options)
    {
        ExecuteNonQuery(connection, "DROP TABLE IF EXISTS records;");
        ExecuteNonQuery(connection,
            "CREATE TABLE records (" +
            "lookup_key " + ResolveLookupKeySqlType(options.KeyKind) + " NOT NULL, " +
            "ordinal INTEGER NOT NULL, " +
            "payload TEXT NOT NULL" +
            ");");
    }

    private static void BulkLoad(
        SqliteConnection connection,
        ExperimentSpec spec,
        LookupSeriesOptions options,
        CancellationToken cancellationToken)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO records (lookup_key, ordinal, payload) VALUES ($key, $ordinal, $payload);";

        var keyParameter = command.Parameters.Add("$key", ResolveKeySqliteType(options.KeyKind));
        var ordinalParameter = command.Parameters.Add("$ordinal", SqliteType.Integer);
        var payloadParameter = command.Parameters.Add("$payload", SqliteType.Text);
        command.Prepare();

        var seed = spec.Dataset.Seed ?? 1;
        var recordCount = checked((int)spec.Dataset.RecordCount);
        for (var ordinal = 1; ordinal <= recordCount; ordinal++)
        {
            if ((ordinal & 0x3FFF) == 0) cancellationToken.ThrowIfCancellationRequested();
            var key = LookupSeriesWorkload.CreateKey(options.KeyKind, options.Mode, seed, ordinal, options.DuplicateGroupSize);
            keyParameter.Value = ConvertKeyForSqlite(key, options.KeyKind);
            ordinalParameter.Value = ordinal;
            payloadParameter.Value = "payload-" + ordinal.ToString(CultureInfo.InvariantCulture);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static SqliteType ResolveKeySqliteType(LookupKeyKind keyKind)
    {
        return keyKind == LookupKeyKind.Guid ? SqliteType.Text : SqliteType.Integer;
    }

    private static long CountRows(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM records;";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
