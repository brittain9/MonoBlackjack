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

    private static RoundRecord CreateRoundRecord(
        RuleFingerprint rules,
        decimal bet,
        decimal payout,
        string action)
    {
        return new RoundRecord(
            PlayedAtUtc: DateTime.UtcNow,
            BetAmount: bet,
            NetPayout: payout,
            Rules: rules,
            HandResults:
            [
                new HandResultRecord(0, payout > 0 ? HandOutcome.Win : HandOutcome.Lose, payout)
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
                new DecisionRecord(
                    0,
                    17,
                    false,
                    "6",
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
