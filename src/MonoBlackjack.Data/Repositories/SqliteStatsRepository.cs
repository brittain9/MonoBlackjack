using Microsoft.Data.Sqlite;
using MonoBlackjack.Core.Ports;

namespace MonoBlackjack.Data.Repositories;

public sealed class SqliteStatsRepository : IStatsRepository
{
    private readonly DatabaseManager _database;

    public SqliteStatsRepository(DatabaseManager database)
    {
        _database = database;
    }

    public void RecordRound(int profileId, RoundRecord round)
    {
        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        int sessionId = GetOrCreateSessionId(connection, transaction, profileId, round.Rules, round.PlayedAtUtc);
        int roundId = InsertRound(connection, transaction, sessionId, profileId, round);

        InsertHandResults(connection, transaction, roundId, round.HandResults);
        InsertCardsSeen(connection, transaction, roundId, round.CardsSeen);
        InsertDecisions(connection, transaction, roundId, round.Decisions);

        transaction.Commit();
    }

    private static int InsertRound(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int sessionId,
        int profileId,
        RoundRecord round)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Round (
                SessionId,
                ProfileId,
                PlayedUtc,
                BetAmount,
                NetPayout,
                BlackjackPayout,
                DealerHitsS17,
                DeckCount,
                SurrenderRule
            )
            VALUES (
                $sessionId,
                $profileId,
                $playedUtc,
                $betAmount,
                $netPayout,
                $blackjackPayout,
                $dealerHitsS17,
                $deckCount,
                $surrenderRule
            );
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$profileId", profileId);
        command.Parameters.AddWithValue("$playedUtc", round.PlayedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$betAmount", (double)round.BetAmount);
        command.Parameters.AddWithValue("$netPayout", (double)round.NetPayout);
        command.Parameters.AddWithValue("$blackjackPayout", round.Rules.BlackjackPayout);
        command.Parameters.AddWithValue("$dealerHitsS17", round.Rules.DealerHitsSoft17 ? 1 : 0);
        command.Parameters.AddWithValue("$deckCount", round.Rules.DeckCount);
        command.Parameters.AddWithValue("$surrenderRule", round.Rules.SurrenderRule);

        return Convert.ToInt32((long)command.ExecuteScalar()!);
    }

    private static void InsertHandResults(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int roundId,
        IReadOnlyList<HandResultRecord> handResults)
    {
        foreach (var result in handResults)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO HandResult (RoundId, HandIndex, Outcome, Payout)
                VALUES ($roundId, $handIndex, $outcome, $payout);
                """;
            command.Parameters.AddWithValue("$roundId", roundId);
            command.Parameters.AddWithValue("$handIndex", result.HandIndex);
            command.Parameters.AddWithValue("$outcome", result.Outcome.ToString());
            command.Parameters.AddWithValue("$payout", (double)result.Payout);
            command.ExecuteNonQuery();
        }
    }

    private static void InsertCardsSeen(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int roundId,
        IReadOnlyList<CardSeenRecord> cardsSeen)
    {
        foreach (var card in cardsSeen)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO CardSeen (RoundId, Recipient, HandIndex, Rank, Suit, FaceDown)
                VALUES ($roundId, $recipient, $handIndex, $rank, $suit, $faceDown);
                """;
            command.Parameters.AddWithValue("$roundId", roundId);
            command.Parameters.AddWithValue("$recipient", card.Recipient);
            command.Parameters.AddWithValue("$handIndex", card.HandIndex);
            command.Parameters.AddWithValue("$rank", card.Rank);
            command.Parameters.AddWithValue("$suit", card.Suit);
            command.Parameters.AddWithValue("$faceDown", card.FaceDown ? 1 : 0);
            command.ExecuteNonQuery();
        }
    }

    private static void InsertDecisions(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int roundId,
        IReadOnlyList<DecisionRecord> decisions)
    {
        foreach (var decision in decisions)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Decision (
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
                    $roundId,
                    $handIndex,
                    $playerValue,
                    $isSoft,
                    $dealerUpcard,
                    $action,
                    $resultOutcome,
                    $resultPayout
                );
                """;
            command.Parameters.AddWithValue("$roundId", roundId);
            command.Parameters.AddWithValue("$handIndex", decision.HandIndex);
            command.Parameters.AddWithValue("$playerValue", decision.PlayerValue);
            command.Parameters.AddWithValue("$isSoft", decision.IsSoft ? 1 : 0);
            command.Parameters.AddWithValue("$dealerUpcard", decision.DealerUpcard);
            command.Parameters.AddWithValue("$action", decision.Action);
            command.Parameters.AddWithValue(
                "$resultOutcome",
                decision.ResultOutcome?.ToString() is { } outcome ? outcome : DBNull.Value);
            command.Parameters.AddWithValue(
                "$resultPayout",
                decision.ResultPayout.HasValue ? (double)decision.ResultPayout.Value : DBNull.Value);
            command.ExecuteNonQuery();
        }
    }

    private static int GetOrCreateSessionId(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int profileId,
        RuleFingerprint currentRules,
        DateTime nowUtc)
    {
        using var query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = """
            SELECT Id, BlackjackPayout, DealerHitsS17, DeckCount, SurrenderRule, EndedUtc
            FROM Session
            WHERE ProfileId = $profileId
            ORDER BY Id DESC
            LIMIT 1;
            """;
        query.Parameters.AddWithValue("$profileId", profileId);

        using var reader = query.ExecuteReader();
        if (!reader.Read())
        {
            reader.Close();
            return InsertSession(connection, transaction, profileId, currentRules, nowUtc);
        }

        int sessionId = reader.GetInt32(0);
        var existingRules = new RuleFingerprint(
            reader.GetString(1),
            reader.GetInt32(2) == 1,
            reader.GetInt32(3),
            reader.GetString(4));
        bool isEnded = !reader.IsDBNull(5);
        reader.Close();

        if (RulesEqual(existingRules, currentRules))
        {
            if (isEnded)
                return InsertSession(connection, transaction, profileId, currentRules, nowUtc);
            return sessionId;
        }

        using (var close = connection.CreateCommand())
        {
            close.Transaction = transaction;
            close.CommandText = """
                UPDATE Session
                SET EndedUtc = $endedUtc
                WHERE Id = $id AND EndedUtc IS NULL;
                """;
            close.Parameters.AddWithValue("$endedUtc", nowUtc.ToString("O"));
            close.Parameters.AddWithValue("$id", sessionId);
            close.ExecuteNonQuery();
        }

        return InsertSession(connection, transaction, profileId, currentRules, nowUtc);
    }

    private static int InsertSession(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int profileId,
        RuleFingerprint rules,
        DateTime nowUtc)
    {
        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO Session (
                ProfileId,
                StartedUtc,
                BlackjackPayout,
                DealerHitsS17,
                DeckCount,
                SurrenderRule
            )
            VALUES (
                $profileId,
                $startedUtc,
                $blackjackPayout,
                $dealerHitsS17,
                $deckCount,
                $surrenderRule
            );
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$profileId", profileId);
        insert.Parameters.AddWithValue("$startedUtc", nowUtc.ToString("O"));
        insert.Parameters.AddWithValue("$blackjackPayout", rules.BlackjackPayout);
        insert.Parameters.AddWithValue("$dealerHitsS17", rules.DealerHitsSoft17 ? 1 : 0);
        insert.Parameters.AddWithValue("$deckCount", rules.DeckCount);
        insert.Parameters.AddWithValue("$surrenderRule", rules.SurrenderRule);

        return Convert.ToInt32((long)insert.ExecuteScalar()!);
    }

    private static bool RulesEqual(RuleFingerprint left, RuleFingerprint right)
    {
        return left.BlackjackPayout == right.BlackjackPayout
            && left.DealerHitsSoft17 == right.DealerHitsSoft17
            && left.DeckCount == right.DeckCount
            && left.SurrenderRule == right.SurrenderRule;
    }
}
