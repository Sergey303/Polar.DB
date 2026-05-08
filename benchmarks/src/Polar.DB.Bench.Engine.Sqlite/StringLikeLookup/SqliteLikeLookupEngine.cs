using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Polar.DB.Bench.Core.LookupSeries;
using Polar.DB.Bench.Core.StringLikeLookup;

namespace Polar.DB.Bench.Engine.Sqlite.StringLikeLookup;

public sealed class SqliteLikeLookupEngine : ILikeLookupEngine
{
    private readonly SqliteLikeLookupEngineOptions _options;
    private SqliteConnection? _connection;

    public SqliteLikeLookupEngine(SqliteLikeLookupEngineOptions options) => _options = options;

    public string EngineKey => "sqlite";

    public async ValueTask LoadAsync(
        IReadOnlyList<StringLikeRecord> records,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.WorkDirectory);
        DeleteExistingArtifacts();
        _connection = await OpenConnectionAsync(cancellationToken);
        await ExecuteAsync("CREATE TABLE items(id INTEGER PRIMARY KEY, name TEXT NOT NULL, payload TEXT NOT NULL);", cancellationToken);

        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);
        await using var command = _connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = "INSERT INTO items(id, name, payload) VALUES ($id, $name, $payload);";
        var id = command.Parameters.Add("$id", SqliteType.Integer);
        var name = command.Parameters.Add("$name", SqliteType.Text);
        var payload = command.Parameters.Add("$payload", SqliteType.Text);

        foreach (var record in records)
        {
            id.Value = record.Id;
            name.Value = record.Name;
            payload.Value = record.Payload;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async ValueTask BuildAsync(CancellationToken cancellationToken)
    {
        await ExecuteAsync("CREATE INDEX ix_items_name ON items(name);", cancellationToken);
        await ExecuteAsync("ANALYZE;", cancellationToken);
    }

    public async ValueTask ReopenAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null) await _connection.DisposeAsync();
        _connection = await OpenConnectionAsync(cancellationToken);
    }

    public async ValueTask<EngineLookupResult> LookupAsync(
        StringLikeQueryCase query,
        CancellationToken cancellationToken)
    {
        var plan = await ExplainAsync(query.Pattern, cancellationToken);
        await using var command = RequireConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM items WHERE name LIKE $pattern ESCAPE '\\';";
        command.Parameters.AddWithValue("$pattern", query.Pattern);
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return new EngineLookupResult(count, null, new Dictionary<string, string> { ["queryPlan"] = plan });
    }

    public ValueTask<IReadOnlyList<ArtifactInfo>> CollectArtifactsAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(ArtifactSizer.Collect(_options.WorkDirectory, "sqlite"));

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={_options.DatabasePath}");
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "PRAGMA case_sensitive_like = ON;", cancellationToken);
        if (_options.UseWal) await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken);
        if (_options.SynchronousNormal) await ExecuteAsync(connection, "PRAGMA synchronous = NORMAL;", cancellationToken);
        return connection;
    }

    private async Task<string> ExplainAsync(string pattern, CancellationToken cancellationToken)
    {
        await using var command = RequireConnection().CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN SELECT COUNT(*) FROM items WHERE name LIKE $pattern ESCAPE '\\';";
        command.Parameters.AddWithValue("$pattern", pattern);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var lines = new List<string>();
        while (await reader.ReadAsync(cancellationToken)) lines.Add(reader.GetString(3));
        return string.Join(" | ", lines);
    }

    private async Task ExecuteAsync(string sql, CancellationToken cancellationToken) =>
        await ExecuteAsync(RequireConnection(), sql, cancellationToken);

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private SqliteConnection RequireConnection() =>
        _connection ?? throw new InvalidOperationException("SQLite connection is not prepared.");

    private void DeleteExistingArtifacts()
    {
        foreach (var path in Directory.EnumerateFiles(_options.WorkDirectory, _options.DatabaseFileName + "*"))
        {
            File.Delete(path);
        }
    }
}
