using Microsoft.Data.Sqlite;
using FluentAssertions;
using MonoBlackjack.Core;
using MonoBlackjack.Core.Events;
using MonoBlackjack.Core.Ports;
using MonoBlackjack.Data;
using MonoBlackjack.Data.Repositories;

namespace MonoBlackjack.Data.Tests;

public sealed class SqliteRepositoriesTests
{
    [Fact]
    public void ProfileRepository_CreatesProfiles_AndTracksActiveProfile()
    {
        var (database, path) = CreateDatabase();
        try
        {
            var profiles = new SqliteProfileRepository(database);

            var first = profiles.GetOrCreateProfile("Default");
            var second = profiles.GetOrCreateProfile("CardCounter");

            first.IsActive.Should().BeTrue();
            second.IsActive.Should().BeFalse();

            profiles.SetActiveProfile(second.Id);

            var active = profiles.GetActiveProfile();
            active.Should().NotBeNull();
            active!.Id.Should().Be(second.Id);

            var all = profiles.GetProfiles();
            all.Should().HaveCount(2);
            all.Single(x => x.Id == first.Id).IsActive.Should().BeFalse();
            all.Single(x => x.Id == second.Id).IsActive.Should().BeTrue();
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void StatsRepository_AutoSplitsSession_WhenRuleFingerprintChanges()
    {
        var (database, path) = CreateDatabase();
        try
        {
            var profiles = new SqliteProfileRepository(database);
            var stats = new SqliteStatsRepository(database);

            var profile = profiles.GetOrCreateProfile("Default");

            var rulesA = new RuleFingerprint("3:2", false, 6, "none");
            var rulesB = new RuleFingerprint("6:5", false, 6, "none");

            stats.RecordRound(profile.Id, CreateRoundRecord(rulesA, 10m, 10m, "Stand"));
            stats.RecordRound(profile.Id, CreateRoundRecord(rulesA, 10m, -10m, "Hit"));
            stats.RecordRound(profile.Id, CreateRoundRecord(rulesB, 10m, 15m, "Double"));

            using var connection = database.OpenConnection();

            long sessionCount = ExecuteScalarLong(connection, "SELECT COUNT(*) FROM Session;");
            long roundCount = ExecuteScalarLong(connection, "SELECT COUNT(*) FROM Round;");
            long handResultCount = ExecuteScalarLong(connection, "SELECT COUNT(*) FROM HandResult;");
            long decisionCount = ExecuteScalarLong(connection, "SELECT COUNT(*) FROM Decision;");
            long cardSeenCount = ExecuteScalarLong(connection, "SELECT COUNT(*) FROM CardSeen;");

            sessionCount.Should().Be(2);
            roundCount.Should().Be(3);
            handResultCount.Should().Be(3);
            decisionCount.Should().Be(3);
            cardSeenCount.Should().Be(12);

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, EndedUtc
                FROM Session
                ORDER BY Id;
                """;
            using var reader = command.ExecuteReader();
            reader.Read().Should().BeTrue();
            int firstSessionId = reader.GetInt32(0);
            reader.IsDBNull(1).Should().BeFalse();
            reader.Read().Should().BeTrue();
            int secondSessionId = reader.GetInt32(0);
            reader.IsDBNull(1).Should().BeTrue();

            using var roundSessionQuery = connection.CreateCommand();
            roundSessionQuery.CommandText = """
                SELECT SessionId
                FROM Round
                ORDER BY Id;
                """;
            using var roundReader = roundSessionQuery.ExecuteReader();
            roundReader.Read().Should().BeTrue();
            roundReader.GetInt32(0).Should().Be(firstSessionId);
            roundReader.Read().Should().BeTrue();
            roundReader.GetInt32(0).Should().Be(firstSessionId);
            roundReader.Read().Should().BeTrue();
            roundReader.GetInt32(0).Should().Be(secondSessionId);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void SettingsRepository_SaveAndLoad_RoundTripsValues()
    {
        var (database, path) = CreateDatabase();
        try
        {
            var profiles = new SqliteProfileRepository(database);
            var settingsRepo = new SqliteSettingsRepository(database);
            var profile = profiles.GetOrCreateProfile("Default");

            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [GameConfig.SettingDealerHitsSoft17] = "True",
                [GameConfig.SettingBlackjackPayout] = "3:2",
                [GameConfig.SettingNumberOfDecks] = "6",
                [GameConfig.SettingSurrenderRule] = "none",
                [GameConfig.SettingDoubleAfterSplit] = "True",
                [GameConfig.SettingResplitAces] = "False",
                [GameConfig.SettingMaxSplits] = "3",
                [GameConfig.SettingDoubleDownRestriction] = DoubleDownRestriction.NineToEleven.ToString(),
                [GameConfig.SettingPenetrationPercent] = "75"
            };

            settingsRepo.SaveSettings(profile.Id, settings);
            var loaded = settingsRepo.LoadSettings(profile.Id);

            loaded.Should().BeEquivalentTo(settings);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void SettingsRepository_SaveAndLoad_EnforcesCurrentSettingsContract()
    {
        var (database, path) = CreateDatabase();
        try
        {
            var profiles = new SqliteProfileRepository(database);
            var settingsRepo = new SqliteSettingsRepository(database);
            var profile = profiles.GetOrCreateProfile("Default");

            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [GameConfig.SettingShowHandValues] = "false",
                [GameConfig.SettingKeybindPause] = "escape",
                [GameConfig.SettingKeybindBack] = "Back",
                [GameConfig.SettingNumberOfDecks] = "999",
                ["LegacyCustomSetting"] = "on"
            };

            settingsRepo.SaveSettings(profile.Id, settings);
            var loaded = settingsRepo.LoadSettings(profile.Id);

            loaded.Should().ContainKey(GameConfig.SettingShowHandValues);
            loaded[GameConfig.SettingShowHandValues].Should().Be("False");
            loaded.Should().ContainKey(GameConfig.SettingKeybindPause);
            loaded[GameConfig.SettingKeybindPause].Should().Be("Escape");
            loaded.Should().ContainKey(GameConfig.SettingKeybindBack);
            loaded[GameConfig.SettingKeybindBack].Should().Be("Back");

            loaded.Should().NotContainKey(GameConfig.SettingNumberOfDecks);
            loaded.Should().NotContainKey("LegacyCustomSetting");
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void StatsRepository_IgnoresNonCanonicalDealerUpcards_InDashboardAggregations()
    {
        var (database, path) = CreateDatabase();
        try
        {
            var profiles = new SqliteProfileRepository(database);
            var stats = new SqliteStatsRepository(database);
            var profile = profiles.GetOrCreateProfile("Default");
            var rules = new RuleFingerprint("3:2", false, 6, "none");

            stats.RecordRound(profile.Id, CreateRoundRecord(rules, 10m, -10m, "Hit", dealerUpcard: "10", dealerBusted: false));
            stats.RecordRound(profile.Id, CreateRoundRecord(rules, 10m, 10m, "Stand", dealerUpcard: "K", dealerBusted: true));
            stats.RecordRound(profile.Id, CreateRoundRecord(rules, 10m, -10m, "Hit", dealerUpcard: "T", dealerBusted: true));

            var dealerBusts = stats.GetDealerBustByUpcard(profile.Id);
            dealerBusts.Should().ContainSingle(x => x.Upcard == "T");

            var canonicalBust = dealerBusts.Single(x => x.Upcard == "T");
            canonicalBust.TotalHands.Should().Be(1);
            canonicalBust.BustedHands.Should().Be(1);

            var hardMatrix = stats.GetStrategyMatrix(profile.Id, "Hard");
            hardMatrix.Should().ContainSingle();
            hardMatrix[0].DealerUpcard.Should().Be("T");
            hardMatrix[0].Total.Should().Be(1);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void StatsRepository_OverviewBusts_UseExplicitPlayerBustSignal()
    {
        var (database, path) = CreateDatabase();
        try
        {
            var profiles = new SqliteProfileRepository(database);
            var stats = new SqliteStatsRepository(database);
            var profile = profiles.GetOrCreateProfile("Default");
            var rules = new RuleFingerprint("3:2", false, 6, "none");

            stats.RecordRound(profile.Id, CreateRoundRecord(
                rules,
                bet: 10m,
                payout: -10m,
                action: "Double",
                playerBusted: false));
            stats.RecordRound(profile.Id, CreateRoundRecord(
                rules,
                bet: 10m,
                payout: -10m,
                action: "Hit",
                playerBusted: true));
            stats.RecordRound(profile.Id, CreateRoundRecord(
                rules,
                bet: 10m,
                payout: 10m,
                action: "Stand",
                playerBusted: false));

            var overview = stats.GetOverviewStats(profile.Id);
            overview.Losses.Should().Be(2);
            overview.Busts.Should().Be(1);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void StatsRepository_CardDistribution_CountsRevealedDealerHoleCardOnce()
    {
        var (database, path) = CreateDatabase();
        try
        {
            var profiles = new SqliteProfileRepository(database);
            var stats = new SqliteStatsRepository(database);
            var profile = profiles.GetOrCreateProfile("Default");
            var rules = new RuleFingerprint("3:2", false, 6, "none");

            stats.RecordRound(profile.Id, CreateRoundRecord(
                rules,
                bet: 10m,
                payout: -10m,
                action: "Stand",
                cardsSeen:
                [
                    new CardSeenRecord("Player", 0, Rank.Ten.ToString(), Suit.Hearts.ToString(), false),
                    new CardSeenRecord("Player", 0, Rank.Seven.ToString(), Suit.Clubs.ToString(), false),
                    new CardSeenRecord("Dealer", 0, Rank.Six.ToString(), Suit.Spades.ToString(), false),
                    new CardSeenRecord("Dealer", 0, Rank.King.ToString(), Suit.Diamonds.ToString(), true),
                    new CardSeenRecord("Dealer", 0, Rank.King.ToString(), Suit.Diamonds.ToString(), false)
                ]));

            var distribution = stats.GetCardDistribution(profile.Id);
            distribution.Single(x => x.Rank == "K").Count.Should().Be(1);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void StatsRepository_RecordRound_PersistsMoneyAsMinorUnitsWithoutDrift()
    {
        var (database, path) = CreateDatabase();
        try
        {
            var profiles = new SqliteProfileRepository(database);
            var stats = new SqliteStatsRepository(database);
            var profile = profiles.GetOrCreateProfile("Default");
            var rules = new RuleFingerprint("3:2", false, 6, "none");

            stats.RecordRound(profile.Id, CreateRoundRecord(
                rules,
                bet: 10.10m,
                payout: -3.35m,
                action: "Hit",
                playerBusted: true));

            using var connection = database.OpenConnection();
            ExecuteScalarLong(connection, "SELECT BetAmount FROM Round;").Should().Be(1010);
            ExecuteScalarLong(connection, "SELECT NetPayout FROM Round;").Should().Be(-335);
            ExecuteScalarLong(connection, "SELECT Payout FROM HandResult;").Should().Be(-335);
            ExecuteScalarLong(connection, "SELECT ResultPayout FROM Decision;").Should().Be(-335);

            var overview = stats.GetOverviewStats(profile.Id);
            overview.NetProfit.Should().Be(-3.35m);
            overview.AverageBet.Should().Be(10.10m);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void StatsRepository_RecordRound_PersistsNullableDecisionResultFields()
    {
        var (database, path) = CreateDatabase();
        try
        {
            var profiles = new SqliteProfileRepository(database);
            var stats = new SqliteStatsRepository(database);
            var profile = profiles.GetOrCreateProfile("Default");
            var rules = new RuleFingerprint("3:2", false, 6, "none");

            var round = new RoundRecord(
                PlayedAtUtc: DateTime.UtcNow,
                BetAmount: 10m,
                NetPayout: 0m,
                DealerBusted: false,
                Rules: rules,
                HandResults:
                [
                    new HandResultRecord(0, HandOutcome.Push, 0m, false)
                ],
                CardsSeen:
                [
                    new CardSeenRecord("Player", 0, Rank.Ten.ToString(), Suit.Hearts.ToString(), false),
                    new CardSeenRecord("Player", 0, Rank.Seven.ToString(), Suit.Clubs.ToString(), false),
                    new CardSeenRecord("Dealer", 0, Rank.Six.ToString(), Suit.Spades.ToString(), false),
                    new CardSeenRecord("Dealer", 0, Rank.King.ToString(), Suit.Diamonds.ToString(), true)
                ],
                Decisions:
                [
                    new DecisionRecord(0, 17, false, "6", "Stand", null, null)
                ]);

            stats.RecordRound(profile.Id, round);

            using var connection = database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT ResultOutcome, ResultPayout FROM Decision LIMIT 1;";
            using var reader = command.ExecuteReader();
            reader.Read().Should().BeTrue();
            reader.IsDBNull(0).Should().BeTrue();
            reader.IsDBNull(1).Should().BeTrue();
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void DatabaseManager_MigratesLegacyRealMoneyColumns_ToIntegerMinorUnits()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mbj-legacy-{Guid.NewGuid():N}.db");

        try
        {
            SeedLegacyRealMoneySchema(path);

            var database = new DatabaseManager(path);
            using var connection = database.OpenConnection();

            GetColumnType(connection, "Round", "BetAmount").Should().Contain("INT");
            GetColumnType(connection, "Round", "NetPayout").Should().Contain("INT");
            GetColumnType(connection, "HandResult", "Payout").Should().Contain("INT");
            GetColumnType(connection, "Decision", "ResultPayout").Should().Contain("INT");

            ExecuteScalarLong(connection, "SELECT BetAmount FROM Round WHERE Id = 1;").Should().Be(1010);
            ExecuteScalarLong(connection, "SELECT NetPayout FROM Round WHERE Id = 1;").Should().Be(-335);
            ExecuteScalarLong(connection, "SELECT Payout FROM HandResult WHERE Id = 1;").Should().Be(-335);
            ExecuteScalarLong(connection, "SELECT ResultPayout FROM Decision WHERE Id = 1;").Should().Be(-335);
            ExecuteScalarLong(connection, "SELECT PlayerBusted FROM HandResult WHERE Id = 1;").Should().Be(0);

            GetForeignKeyTargetTable(connection, "HandResult").Should().Be("Round");
            GetForeignKeyTargetTable(connection, "CardSeen").Should().Be("Round");
            GetForeignKeyTargetTable(connection, "Decision").Should().Be("Round");

            var stats = new SqliteStatsRepository(database);
            var rules = new RuleFingerprint("3:2", false, 6, "none");

            stats.RecordRound(
                profileId: 1,
                CreateRoundRecord(
                    rules,
                    bet: 5m,
                    payout: 5m,
                    action: "Stand",
                    dealerUpcard: "6",
                    dealerBusted: false,
                    playerBusted: false));

            ExecuteScalarLong(connection, "SELECT COUNT(*) FROM Round;").Should().Be(2);
            ExecuteScalarLong(connection, "SELECT COUNT(*) FROM CardSeen;").Should().BeGreaterThan(1);
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static RoundRecord CreateRoundRecord(
        RuleFingerprint rules,
        decimal bet,
        decimal payout,
        string action,
        string dealerUpcard = "6",
        bool dealerBusted = false,
        bool playerBusted = false,
        IReadOnlyList<CardSeenRecord>? cardsSeen = null)
    {
        return new RoundRecord(
            PlayedAtUtc: DateTime.UtcNow,
            BetAmount: bet,
            NetPayout: payout,
            DealerBusted: dealerBusted,
            Rules: rules,
            HandResults:
            [
                new HandResultRecord(0, payout > 0 ? HandOutcome.Win : HandOutcome.Lose, payout, playerBusted)
            ],
            CardsSeen: cardsSeen ??
            [
                new CardSeenRecord("Player", 0, Rank.Ten.ToString(), Suit.Hearts.ToString(), false),
                new CardSeenRecord("Player", 0, Rank.Seven.ToString(), Suit.Clubs.ToString(), false),
                new CardSeenRecord("Dealer", 0, Rank.Six.ToString(), Suit.Spades.ToString(), false),
                new CardSeenRecord("Dealer", 0, Rank.King.ToString(), Suit.Diamonds.ToString(), true)
            ],
            Decisions:
            [
                new DecisionRecord(
                    0,
                    17,
                    false,
                    dealerUpcard,
                    action,
                    payout > 0 ? HandOutcome.Win : HandOutcome.Lose,
                    payout)
            ]);
    }

    private static (DatabaseManager Database, string Path) CreateDatabase()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mbj-test-{Guid.NewGuid():N}.db");
        return (new DatabaseManager(path), path);
    }

    private static long ExecuteScalarLong(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    private static string GetColumnType(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                return reader.GetString(2);
        }

        throw new InvalidOperationException($"Column '{columnName}' was not found in table '{tableName}'.");
    }

    private static string GetForeignKeyTargetTable(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_key_list({tableName});";
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException($"Table '{tableName}' has no foreign key definitions.");
        return reader.GetString(2);
    }

    private static void SeedLegacyRealMoneySchema(string path)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Profile (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT NOT NULL UNIQUE,
                IsActive    INTEGER NOT NULL DEFAULT 0,
                CreatedUtc  TEXT NOT NULL
            );

            CREATE TABLE Session (
                Id                INTEGER PRIMARY KEY AUTOINCREMENT,
                ProfileId         INTEGER NOT NULL REFERENCES Profile(Id),
                StartedUtc        TEXT NOT NULL,
                EndedUtc          TEXT,
                BlackjackPayout   TEXT NOT NULL,
                DealerHitsS17     INTEGER NOT NULL,
                DeckCount         INTEGER NOT NULL,
                SurrenderRule     TEXT NOT NULL
            );

            CREATE TABLE Round (
                Id                INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId         INTEGER NOT NULL REFERENCES Session(Id),
                ProfileId         INTEGER NOT NULL REFERENCES Profile(Id),
                PlayedUtc         TEXT NOT NULL,
                BetAmount         REAL NOT NULL,
                NetPayout         REAL NOT NULL,
                DealerBusted      INTEGER NOT NULL DEFAULT 0,
                BlackjackPayout   TEXT NOT NULL,
                DealerHitsS17     INTEGER NOT NULL,
                DeckCount         INTEGER NOT NULL,
                SurrenderRule     TEXT NOT NULL
            );

            CREATE TABLE HandResult (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                RoundId     INTEGER NOT NULL REFERENCES Round(Id),
                HandIndex   INTEGER NOT NULL,
                Outcome     TEXT NOT NULL,
                Payout      REAL NOT NULL
            );

            CREATE TABLE CardSeen (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                RoundId     INTEGER NOT NULL REFERENCES Round(Id),
                Recipient   TEXT NOT NULL,
                HandIndex   INTEGER NOT NULL,
                Rank        TEXT NOT NULL,
                Suit        TEXT NOT NULL,
                FaceDown    INTEGER NOT NULL
            );

            CREATE TABLE Decision (
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

            INSERT INTO Profile (Id, Name, IsActive, CreatedUtc)
            VALUES (1, 'Default', 1, '2026-02-18T00:00:00.0000000Z');

            INSERT INTO Session (
                Id,
                ProfileId,
                StartedUtc,
                EndedUtc,
                BlackjackPayout,
                DealerHitsS17,
                DeckCount,
                SurrenderRule
            )
            VALUES (
                1,
                1,
                '2026-02-18T00:00:00.0000000Z',
                NULL,
                '3:2',
                0,
                6,
                'none'
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
            VALUES (
                1,
                1,
                1,
                '2026-02-18T00:00:00.0000000Z',
                10.10,
                -3.35,
                0,
                '3:2',
                0,
                6,
                'none'
            );

            INSERT INTO HandResult (Id, RoundId, HandIndex, Outcome, Payout)
            VALUES (1, 1, 0, 'Lose', -3.35);

            INSERT INTO CardSeen (Id, RoundId, Recipient, HandIndex, Rank, Suit, FaceDown)
            VALUES (1, 1, 'Player', 0, 'Ten', 'Hearts', 0);

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
            VALUES (
                1,
                1,
                0,
                17,
                0,
                '6',
                'Hit',
                'Lose',
                -3.35
            );
            """;
        command.ExecuteNonQuery();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Ignore cleanup errors in tests.
        }
    }
}
