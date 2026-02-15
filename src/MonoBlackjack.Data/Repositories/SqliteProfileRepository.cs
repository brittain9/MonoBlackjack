using Microsoft.Data.Sqlite;
using MonoBlackjack.Core.Ports;

namespace MonoBlackjack.Data.Repositories;

public sealed class SqliteProfileRepository : IProfileRepository
{
    private readonly DatabaseManager _database;

    public SqliteProfileRepository(DatabaseManager database)
    {
        _database = database;
    }

    public PlayerProfile GetOrCreateProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name must not be empty.", nameof(name));

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        var existing = FindByName(connection, transaction, name);
        if (existing is not null)
        {
            if (!HasActiveProfile(connection, transaction))
                SetActiveProfileInternal(connection, transaction, existing.Id);

            transaction.Commit();
            return existing with { IsActive = GetActiveProfileId(connection, transaction) == existing.Id };
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO Profile (Name, IsActive, CreatedUtc)
            VALUES ($name, 0, $createdUtc);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$name", name.Trim());
        insert.Parameters.AddWithValue("$createdUtc", DateTime.UtcNow.ToString("O"));
        int id = Convert.ToInt32((long)insert.ExecuteScalar()!);

        if (!HasActiveProfile(connection, transaction))
            SetActiveProfileInternal(connection, transaction, id);

        int? activeId = GetActiveProfileId(connection, transaction);
        transaction.Commit();

        return new PlayerProfile(id, name.Trim(), activeId == id);
    }

    public IReadOnlyList<PlayerProfile> GetProfiles()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, IsActive
            FROM Profile
            ORDER BY Id;
            """;

        using var reader = command.ExecuteReader();
        var profiles = new List<PlayerProfile>();
        while (reader.Read())
        {
            profiles.Add(new PlayerProfile(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetInt32(2) == 1));
        }

        return profiles;
    }

    public PlayerProfile? GetActiveProfile()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, IsActive
            FROM Profile
            WHERE IsActive = 1
            ORDER BY Id
            LIMIT 1;
            """;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        return new PlayerProfile(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetInt32(2) == 1);
    }

    public void SetActiveProfile(int profileId)
    {
        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        SetActiveProfileInternal(connection, transaction, profileId);
        transaction.Commit();
    }

    private static PlayerProfile? FindByName(SqliteConnection connection, SqliteTransaction transaction, string name)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id, Name, IsActive
            FROM Profile
            WHERE Name = $name
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$name", name.Trim());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        return new PlayerProfile(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetInt32(2) == 1);
    }

    private static bool HasActiveProfile(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM Profile WHERE IsActive = 1;";
        long count = (long)command.ExecuteScalar()!;
        return count > 0;
    }

    private static int? GetActiveProfileId(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id
            FROM Profile
            WHERE IsActive = 1
            ORDER BY Id
            LIMIT 1;
            """;

        var result = command.ExecuteScalar();
        return result is null ? null : Convert.ToInt32((long)result);
    }

    private static void SetActiveProfileInternal(SqliteConnection connection, SqliteTransaction transaction, int profileId)
    {
        using (var clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "UPDATE Profile SET IsActive = 0;";
            clear.ExecuteNonQuery();
        }

        using var set = connection.CreateCommand();
        set.Transaction = transaction;
        set.CommandText = """
            UPDATE Profile
            SET IsActive = 1
            WHERE Id = $id;
            """;
        set.Parameters.AddWithValue("$id", profileId);
        int affected = set.ExecuteNonQuery();
        if (affected == 0)
            throw new InvalidOperationException($"Profile {profileId} does not exist.");
    }
}
