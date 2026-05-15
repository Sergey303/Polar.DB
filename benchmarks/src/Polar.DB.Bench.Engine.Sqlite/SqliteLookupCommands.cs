using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Polar.DB.Bench.Core.LookupSeries;

namespace Polar.DB.Bench.Engine.Sqlite;

internal sealed class SqliteLookupCommands : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly LookupSeriesOptions options;
    private readonly SqliteCommand rowIdCommand;
    private readonly SqliteParameter rowIdKeyParameter;
    private readonly Dictionary<int, SqliteCommand> materializeCommands = new();

    public SqliteLookupCommands(SqliteConnection connection, LookupSeriesOptions options)
    {
        this.connection = connection;
        this.options = options;
        rowIdCommand = connection.CreateCommand();
        rowIdCommand.CommandText = "SELECT rowid FROM records WHERE lookup_key = $key ORDER BY rowid;";
        rowIdKeyParameter = rowIdCommand.Parameters.Add("$key", ResolveSqliteType(options.KeyKind));
        rowIdCommand.Prepare();
    }

    public List<long> SelectRowIdsByKey(IComparable key)
    {
        rowIdKeyParameter.Value = SqliteLookupSeriesExecutor.ConvertKeyForSqlite(key, options.KeyKind);
        using var reader = rowIdCommand.ExecuteReader();
        var rowIds = new List<long>();
        while (reader.Read()) rowIds.Add(reader.GetInt64(0));
        return rowIds;
    }

    public MaterializedRowsResult MaterializeRows(IReadOnlyList<long> rowIds)
    {
        if (rowIds.Count == 0) return new MaterializedRowsResult(Array.Empty<SqliteMaterializedRow>());

        var command = GetMaterializeCommand(rowIds.Count);
        for (var i = 0; i < rowIds.Count; i++) command.Parameters[i].Value = rowIds[i];

        using var reader = command.ExecuteReader();
        var rows = new List<SqliteMaterializedRow>(rowIds.Count);
        while (reader.Read())
        {
            rows.Add(new SqliteMaterializedRow(
                SqliteLookupSeriesExecutor.ReadKey(reader, options.KeyKind),
                reader.GetInt64(1),
                reader.GetString(2)));
        }

        return new MaterializedRowsResult(rows);
    }

    public void Dispose()
    {
        rowIdCommand.Dispose();
        foreach (var command in materializeCommands.Values) command.Dispose();
    }

    private SqliteCommand GetMaterializeCommand(int rowIdCount)
    {
        if (materializeCommands.TryGetValue(rowIdCount, out var cached)) return cached;

        var command = connection.CreateCommand();
        var names = new string[rowIdCount];
        for (var i = 0; i < rowIdCount; i++)
        {
            names[i] = "$rowid" + i;
            command.Parameters.Add(names[i], SqliteType.Integer);
        }

        command.CommandText = "SELECT lookup_key, ordinal, payload FROM records WHERE rowid IN (" +
                              string.Join(", ", names) + ") ORDER BY rowid;";
        command.Prepare();
        materializeCommands[rowIdCount] = command;
        return command;
    }

    private static SqliteType ResolveSqliteType(LookupKeyKind keyKind)
    {
        return keyKind == LookupKeyKind.Guid ? SqliteType.Text : SqliteType.Integer;
    }
}
