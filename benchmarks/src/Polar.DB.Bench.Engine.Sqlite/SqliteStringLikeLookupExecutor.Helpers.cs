using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Polar.DB.Bench.Core.Abstractions;
using Polar.DB.Bench.Core.Models;
using Polar.DB.Bench.Core.StringLikeLookup;

namespace Polar.DB.Bench.Engine.Sqlite;

internal static partial class SqliteStringLikeLookupExecutor
{
    private static SqliteConnection Open(string databasePath)
    {
        var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        Execute(connection, "PRAGMA journal_mode = WAL;");
        Execute(connection, "PRAGMA synchronous = NORMAL;");
        Execute(connection, "PRAGMA case_sensitive_like = ON;");
        return connection;
    }

    private static void CreateSchema(SqliteConnection connection) => Execute(connection,
        "CREATE TABLE records (id INTEGER PRIMARY KEY, name TEXT NOT NULL, payload TEXT NOT NULL);");

    private static void Load(SqliteConnection connection, StringLikeLookupOptions options, System.Threading.CancellationToken ct)
    {
        using var tx = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO records(id, name, payload) VALUES ($id, $name, $payload);";
        var id = command.Parameters.Add("$id", SqliteType.Integer);
        var name = command.Parameters.Add("$name", SqliteType.Text);
        var payload = command.Parameters.Add("$payload", SqliteType.Text);
        command.Prepare();
        foreach (var record in StringLikeLookupWorkload.GenerateRecords(options))
        {
            ct.ThrowIfCancellationRequested();
            id.Value = record.Id;
            name.Value = record.Name;
            payload.Value = record.Payload;
            command.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private static SqliteCommand CreateLikeCountCommand(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM records WHERE name LIKE $pattern;";
        command.Parameters.Add("$pattern", SqliteType.Text);
        command.Prepare();
        return command;
    }

    private static long Count(SqliteCommand command, string pattern)
    {
        command.Parameters[0].Value = pattern;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static string Explain(SqliteConnection connection, string pattern)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN SELECT COUNT(*) FROM records WHERE name LIKE $pattern;";
        command.Parameters.AddWithValue("$pattern", pattern);
        using var reader = command.ExecuteReader();
        var parts = new List<string>();
        while (reader.Read()) parts.Add(reader.GetString(3));
        return string.Join(" | ", parts);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long ScalarLong(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static double Measure(Action action)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        action();
        watch.Stop();
        return watch.Elapsed.TotalMilliseconds;
    }

    private static IReadOnlyList<ArtifactDescriptor> CollectArtifacts(string root, string relativeRoot)
    {
        if (!Directory.Exists(root)) return Array.Empty<ArtifactDescriptor>();
        var result = new List<ArtifactDescriptor>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            result.Add(new ArtifactDescriptor(Role(info.Name), Path.GetRelativePath(relativeRoot, info.FullName), info.Length));
        }
        return result;
    }

    private static ArtifactRole Role(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.EndsWith("-wal")) return ArtifactRole.Wal;
        if (lower.EndsWith("-shm")) return ArtifactRole.SharedMemory;
        if (lower.EndsWith(".sqlite") || lower.EndsWith(".db")) return ArtifactRole.PrimaryDatabase;
        return ArtifactRole.Unknown;
    }
}
