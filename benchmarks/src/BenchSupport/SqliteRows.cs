using Microsoft.Data.Sqlite;

namespace PolarDbBenchmarks;

internal static class SqliteRows
{
    public static Row[] ReadAll(string db)
    {
        using var connection = new SqliteConnection($"Data Source={db}");
        connection.Open();
        return ReadAll(connection);
    }

    public static Row[] ReadAll(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,skey,external_id,external_key,payload FROM rows ORDER BY id";

        using var reader = command.ExecuteReader();
        var rows = new List<Row>();
        while (reader.Read())
        {
            rows.Add(new Row(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4)));
        }

        return rows.ToArray();
    }
}
