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
        command.CommandText = SelectAllSql() + " ORDER BY id";

        using var reader = command.ExecuteReader();
        var rows = new List<Row>();
        while (reader.Read())
            rows.Add(Read(reader));

        return rows.ToArray();
    }

    public static string SelectAllSql() =>
        "SELECT id,long_key,guid_key,skey,external_id,external_long,external_guid,external_key,payload FROM rows";

    public static Row Read(SqliteDataReader reader) =>
        new(reader.GetInt64(0), reader.GetInt64(1), BenchmarkGuid.FromBytes((byte[])reader["guid_key"]),
            reader.GetString(3), reader.GetInt32(4), reader.GetInt64(5),
            BenchmarkGuid.FromBytes((byte[])reader["external_guid"]), reader.GetString(7), reader.GetString(8));
}
