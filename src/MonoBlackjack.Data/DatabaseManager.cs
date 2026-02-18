using Microsoft.Data.Sqlite;

namespace MonoBlackjack.Data;

public sealed class DatabaseManager
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public string DatabasePath => _databasePath;

    public DatabaseManager(string? databasePath = null)
    {
        _databasePath = databasePath ?? ResolveDefaultDatabasePath();
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath
        }.ToString();

        EnsureSchema();
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    private static string ResolveDefaultDatabasePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "MonoBlackjack", "monoblackjack.db");
    }

    private void EnsureSchema()
    {
        using var connection = OpenConnection();
        EnsureBaseSchema(connection);
        EnsureMoneyColumnsUseMinorUnits(connection);
        EnsureHandResultPlayerBustedColumn(connection);
    }

    private static void EnsureBaseSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Profile (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT NOT NULL UNIQUE,
                IsActive    INTEGER NOT NULL DEFAULT 0,
                CreatedUtc  TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ProfileSetting (
                ProfileId     INTEGER NOT NULL REFERENCES Profile(Id),
                SettingKey    TEXT NOT NULL,
                SettingValue  TEXT NOT NULL,
                UpdatedUtc    TEXT NOT NULL,
                PRIMARY KEY (ProfileId, SettingKey)
            );

            CREATE TABLE IF NOT EXISTS Session (
                Id                INTEGER PRIMARY KEY AUTOINCREMENT,
                ProfileId         INTEGER NOT NULL REFERENCES Profile(Id),
                StartedUtc        TEXT NOT NULL,
                EndedUtc          TEXT,
                BlackjackPayout   TEXT NOT NULL,
                DealerHitsS17     INTEGER NOT NULL,
                DeckCount         INTEGER NOT NULL,
                SurrenderRule     TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Round (
                Id                INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId         INTEGER NOT NULL REFERENCES Session(Id),
                ProfileId         INTEGER NOT NULL REFERENCES Profile(Id),
                PlayedUtc         TEXT NOT NULL,
                BetAmount         INTEGER NOT NULL,
                NetPayout         INTEGER NOT NULL,
                DealerBusted      INTEGER NOT NULL DEFAULT 0,
                BlackjackPayout   TEXT NOT NULL,
                DealerHitsS17     INTEGER NOT NULL,
                DeckCount         INTEGER NOT NULL,
                SurrenderRule     TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS HandResult (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                RoundId     INTEGER NOT NULL REFERENCES Round(Id),
                HandIndex   INTEGER NOT NULL,
                Outcome     TEXT NOT NULL,
                Payout      INTEGER NOT NULL,
                PlayerBusted INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS CardSeen (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                RoundId     INTEGER NOT NULL REFERENCES Round(Id),
                Recipient   TEXT NOT NULL,
                HandIndex   INTEGER NOT NULL,
                Rank        TEXT NOT NULL,
                Suit        TEXT NOT NULL,
                FaceDown    INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Decision (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                RoundId         INTEGER NOT NULL REFERENCES Round(Id),
                HandIndex       INTEGER NOT NULL,
                PlayerValue     INTEGER NOT NULL,
                IsSoft          INTEGER NOT NULL,
                DealerUpcard    TEXT NOT NULL,
                Action          TEXT NOT NULL,
                ResultOutcome   TEXT,
                ResultPayout    INTEGER
            );

            CREATE INDEX IF NOT EXISTS IX_Round_ProfileId ON Round(ProfileId);
            CREATE INDEX IF NOT EXISTS IX_Round_SessionId ON Round(SessionId);
            CREATE INDEX IF NOT EXISTS IX_Session_ProfileId ON Session(ProfileId);
            CREATE INDEX IF NOT EXISTS IX_ProfileSetting_ProfileId ON ProfileSetting(ProfileId);
            CREATE INDEX IF NOT EXISTS IX_HandResult_RoundId ON HandResult(RoundId);
            CREATE INDEX IF NOT EXISTS IX_CardSeen_RoundId ON CardSeen(RoundId);
            CREATE INDEX IF NOT EXISTS IX_Decision_RoundId ON Decision(RoundId);
            CREATE INDEX IF NOT EXISTS IX_Decision_Action ON Decision(Action);
            CREATE INDEX IF NOT EXISTS IX_Decision_PlayerValue ON Decision(PlayerValue);
            CREATE INDEX IF NOT EXISTS IX_Decision_DealerUpcard ON Decision(DealerUpcard);
            """;
        command.ExecuteNonQuery();
    }

    private static void EnsureMoneyColumnsUseMinorUnits(SqliteConnection connection)
    {
        bool migrateRound = !ColumnUsesIntegerAffinity(connection, "Round", "BetAmount")
            || !ColumnUsesIntegerAffinity(connection, "Round", "NetPayout");
        bool migrateHandResult = !ColumnUsesIntegerAffinity(connection, "HandResult", "Payout");
        bool migrateDecision = !ColumnUsesIntegerAffinity(connection, "Decision", "ResultPayout");

        if (!migrateRound && !migrateHandResult && !migrateDecision)
            return;

        bool handResultHasPlayerBusted = ColumnExists(connection, "HandResult", "PlayerBusted");

        using (var disableForeignKeys = connection.CreateCommand())
        {
            disableForeignKeys.CommandText = "PRAGMA foreign_keys = OFF;";
            disableForeignKeys.ExecuteNonQuery();
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            if (migrateRound)
                MigrateRoundMoneyColumns(connection, transaction);

            if (migrateHandResult)
                MigrateHandResultMoneyColumn(connection, transaction, handResultHasPlayerBusted);

            if (migrateDecision)
                MigrateDecisionMoneyColumn(connection, transaction);

            EnsureIndexes(connection, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
        finally
        {
            using var enableForeignKeys = connection.CreateCommand();
            enableForeignKeys.CommandText = "PRAGMA foreign_keys = ON;";
            enableForeignKeys.ExecuteNonQuery();
        }
    }

    private static void MigrateRoundMoneyColumns(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            ALTER TABLE Round RENAME TO Round_legacy_money;

            CREATE TABLE Round (
                Id                INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId         INTEGER NOT NULL REFERENCES Session(Id),
                ProfileId         INTEGER NOT NULL REFERENCES Profile(Id),
                PlayedUtc         TEXT NOT NULL,
                BetAmount         INTEGER NOT NULL,
                NetPayout         INTEGER NOT NULL,
                DealerBusted      INTEGER NOT NULL DEFAULT 0,
                BlackjackPayout   TEXT NOT NULL,
                DealerHitsS17     INTEGER NOT NULL,
                DeckCount         INTEGER NOT NULL,
                SurrenderRule     TEXT NOT NULL
            );

            INSERT INTO Round (
                Id,
                SessionId,
                ProfileId,
                PlayedUtc,
                BetAmount,
                NetPayout,
                DealerBusted,
                BlackjackPayout,
                DealerHitsS17,
                DeckCount,
                SurrenderRule
            )
            SELECT
                Id,
                SessionId,
                ProfileId,
                PlayedUtc,
                CAST(ROUND(BetAmount * 100.0) AS INTEGER),
                CAST(ROUND(NetPayout * 100.0) AS INTEGER),
                DealerBusted,
                BlackjackPayout,
                DealerHitsS17,
                DeckCount,
                SurrenderRule
            FROM Round_legacy_money;

            DROP TABLE Round_legacy_money;
            """;
        command.ExecuteNonQuery();
    }

    private static void MigrateHandResultMoneyColumn(SqliteConnection connection, SqliteTransaction transaction, bool hasPlayerBusted)
    {
        string playerBustedSelect = hasPlayerBusted ? "COALESCE(PlayerBusted, 0)" : "0";

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            ALTER TABLE HandResult RENAME TO HandResult_legacy_money;

            CREATE TABLE HandResult (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                RoundId      INTEGER NOT NULL REFERENCES Round(Id),
                HandIndex    INTEGER NOT NULL,
                Outcome      TEXT NOT NULL,
                Payout       INTEGER NOT NULL,
                PlayerBusted INTEGER NOT NULL DEFAULT 0
            );

            INSERT INTO HandResult (
                Id,
                RoundId,
                HandIndex,
                Outcome,
                Payout,
                PlayerBusted
            )
            SELECT
                Id,
                RoundId,
                HandIndex,
                Outcome,
                CAST(ROUND(Payout * 100.0) AS INTEGER),
                {playerBustedSelect}
            FROM HandResult_legacy_money;

            DROP TABLE HandResult_legacy_money;
            """;
        command.ExecuteNonQuery();
    }

    private static void MigrateDecisionMoneyColumn(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            ALTER TABLE Decision RENAME TO Decision_legacy_money;

            CREATE TABLE Decision (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                RoundId         INTEGER NOT NULL REFERENCES Round(Id),
                HandIndex       INTEGER NOT NULL,
                PlayerValue     INTEGER NOT NULL,
                IsSoft          INTEGER NOT NULL,
                DealerUpcard    TEXT NOT NULL,
                Action          TEXT NOT NULL,
                ResultOutcome   TEXT,
                ResultPayout    INTEGER
            );

            INSERT INTO Decision (
                Id,
                RoundId,
                HandIndex,
                PlayerValue,
                IsSoft,
                DealerUpcard,
                Action,
                ResultOutcome,
                ResultPayout
            )
            SELECT
                Id,
                RoundId,
                HandIndex,
                PlayerValue,
                IsSoft,
                DealerUpcard,
                Action,
                ResultOutcome,
                CASE
                    WHEN ResultPayout IS NULL THEN NULL
                    ELSE CAST(ROUND(ResultPayout * 100.0) AS INTEGER)
                END
            FROM Decision_legacy_money;

            DROP TABLE Decision_legacy_money;
            """;
        command.ExecuteNonQuery();
    }

    private static void EnsureHandResultPlayerBustedColumn(SqliteConnection connection)
    {
        if (ColumnExists(connection, "HandResult", "PlayerBusted"))
            return;

        using var command = connection.CreateCommand();
        command.CommandText = "ALTER TABLE HandResult ADD COLUMN PlayerBusted INTEGER NOT NULL DEFAULT 0;";
        command.ExecuteNonQuery();
    }

    private static bool ColumnUsesIntegerAffinity(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (!string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                continue;

            var columnType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            return columnType.Contains("INT", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void EnsureIndexes(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE INDEX IF NOT EXISTS IX_Round_ProfileId ON Round(ProfileId);
            CREATE INDEX IF NOT EXISTS IX_Round_SessionId ON Round(SessionId);
            CREATE INDEX IF NOT EXISTS IX_Session_ProfileId ON Session(ProfileId);
            CREATE INDEX IF NOT EXISTS IX_ProfileSetting_ProfileId ON ProfileSetting(ProfileId);
            CREATE INDEX IF NOT EXISTS IX_HandResult_RoundId ON HandResult(RoundId);
            CREATE INDEX IF NOT EXISTS IX_CardSeen_RoundId ON CardSeen(RoundId);
            CREATE INDEX IF NOT EXISTS IX_Decision_RoundId ON Decision(RoundId);
            CREATE INDEX IF NOT EXISTS IX_Decision_Action ON Decision(Action);
            CREATE INDEX IF NOT EXISTS IX_Decision_PlayerValue ON Decision(PlayerValue);
            CREATE INDEX IF NOT EXISTS IX_Decision_DealerUpcard ON Decision(DealerUpcard);
            """;
        command.ExecuteNonQuery();
    }
}
