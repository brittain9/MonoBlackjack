using Microsoft.Data.Sqlite;
using MonoBlackjack.Core.Ports;

namespace MonoBlackjack.Data.Repositories;

public sealed class SqliteSettingsRepository : ISettingsRepository
{
    private readonly DatabaseManager _database;

    public SqliteSettingsRepository(DatabaseManager database)
    {
        _database = database;
    }

    public IReadOnlyDictionary<string, string> LoadSettings(int profileId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT SettingKey, SettingValue
            FROM ProfileSetting
            WHERE ProfileId = $profileId;
            """;
        command.Parameters.AddWithValue("$profileId", profileId);

        using var reader = command.ExecuteReader();
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            settings[reader.GetString(0)] = reader.GetString(1);
        }

        return settings;
    }

    public void SaveSettings(int profileId, IReadOnlyDictionary<string, string> settings)
    {
        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM ProfileSetting WHERE ProfileId = $profileId;";
            delete.Parameters.AddWithValue("$profileId", profileId);
            delete.ExecuteNonQuery();
        }

        foreach (var setting in settings)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO ProfileSetting (ProfileId, SettingKey, SettingValue, UpdatedUtc)
                VALUES ($profileId, $key, $value, $updatedUtc);
                """;
            insert.Parameters.AddWithValue("$profileId", profileId);
            insert.Parameters.AddWithValue("$key", setting.Key);
            insert.Parameters.AddWithValue("$value", setting.Value);
            insert.Parameters.AddWithValue("$updatedUtc", DateTime.UtcNow.ToString("O"));
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}
