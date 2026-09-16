using System.Globalization;
using DiscordUsernameChecker.Models;
using Microsoft.Data.Sqlite;

namespace DiscordUsernameChecker.Services;

public sealed class UsernameCheckLogStore : IUsernameCheckLogStore
{
    private const string DatabaseFileName = "UsernameChecks.db";

    private readonly string _connectionString;

    public UsernameCheckLogStore()
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = ResolveDatabasePath(),
        }.ConnectionString;

        CreateSchema();
    }

    public void AppendCheck(UsernameCheckResult checkResult)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CheckLog (Username, Status, CheckedAt)
            VALUES ($username, $status, $checkedAt)
            """;
        command.Parameters.AddWithValue("$username", checkResult.Username);
        command.Parameters.AddWithValue("$status", checkResult.Status.ToString());
        command.Parameters.AddWithValue(
            "$checkedAt",
            (checkResult.LastCheckedAt ?? DateTimeOffset.UtcNow).ToUniversalTime().ToString("o")
        );
        command.ExecuteNonQuery();
    }

    public UsernameCheckResult? FindLatestCheck(string username)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Status, CheckedAt
            FROM CheckLog
            WHERE Username = $username COLLATE NOCASE
            ORDER BY Id DESC
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$username", username);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var status = Enum.TryParse<UsernameStatus>(reader.GetString(0), out var parsedStatus)
            ? parsedStatus
            : UsernameStatus.Pending;
        var checkedAt = DateTimeOffset.Parse(
            reader.GetString(1),
            null,
            DateTimeStyles.RoundtripKind
        );

        return new UsernameCheckResult(username, status, checkedAt);
    }

    public string? ReadState(string stateKey)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM AppState WHERE Key = $key";
        command.Parameters.AddWithValue("$key", stateKey);
        return command.ExecuteScalar() as string;
    }

    public void WriteState(string stateKey, string stateValue)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AppState (Key, Value)
            VALUES ($key, $value)
            ON CONFLICT (Key) DO UPDATE SET Value = excluded.Value
            """;
        command.Parameters.AddWithValue("$key", stateKey);
        command.Parameters.AddWithValue("$value", stateValue);
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void CreateSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS CheckLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL,
                Status TEXT NOT NULL,
                CheckedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_CheckLog_Username ON CheckLog (Username, Id DESC);

            CREATE TABLE IF NOT EXISTS AppState (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private static string ResolveDatabasePath()
    {
        var homeDirectory = Environment.GetEnvironmentVariable("HOME");
        var isAzureAppService =
            Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") is not null
            && !string.IsNullOrEmpty(homeDirectory);

        // Wwwroot Is Wiped On Every Deploy
        var dataDirectory = isAzureAppService
            ? Path.Combine(homeDirectory!, "data")
            : AppContext.BaseDirectory;

        Directory.CreateDirectory(dataDirectory);
        return Path.Combine(dataDirectory, DatabaseFileName);
    }
}
