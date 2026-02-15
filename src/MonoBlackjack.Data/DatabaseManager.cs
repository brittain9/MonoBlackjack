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
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Profile (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT NOT NULL UNIQUE,
                IsActive    INTEGER NOT NULL DEFAULT 0,
                CreatedUtc  TEXT NOT NULL
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
                BetAmount         REAL NOT NULL,
                NetPayout         REAL NOT NULL,
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
                Payout      REAL NOT NULL
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
                ResultPayout    REAL
            );

            CREATE INDEX IF NOT EXISTS IX_Round_ProfileId ON Round(ProfileId);
            CREATE INDEX IF NOT EXISTS IX_Round_SessionId ON Round(SessionId);
            CREATE INDEX IF NOT EXISTS IX_Session_ProfileId ON Session(ProfileId);
            CREATE INDEX IF NOT EXISTS IX_Decision_Action ON Decision(Action);
            CREATE INDEX IF NOT EXISTS IX_Decision_PlayerValue ON Decision(PlayerValue);
            CREATE INDEX IF NOT EXISTS IX_Decision_DealerUpcard ON Decision(DealerUpcard);
            """;
        command.ExecuteNonQuery();
    }
}
