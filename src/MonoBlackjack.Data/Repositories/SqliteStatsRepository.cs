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
                DealerBusted,
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
                $dealerBusted,
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
        command.Parameters.AddWithValue("$dealerBusted", round.DealerBusted ? 1 : 0);
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

        if (existingRules == currentRules)
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

    public OverviewStats GetOverviewStats(int profileId)
    {
        using var connection = _database.OpenConnection();

        int totalRounds = 0;
        int totalSessions = 0;
        decimal netProfit = 0;
        decimal avgBet = 0;
        decimal biggestWin = 0;
        decimal worstLoss = 0;

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT
                    COUNT(*),
                    COALESCE(SUM(NetPayout), 0),
                    COALESCE(AVG(CASE WHEN BetAmount > 0 THEN BetAmount END), 0),
                    COALESCE(MAX(NetPayout), 0),
                    COALESCE(MIN(NetPayout), 0)
                FROM Round WHERE ProfileId = $profileId;
                """;
            cmd.Parameters.AddWithValue("$profileId", profileId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                totalRounds = reader.GetInt32(0);
                netProfit = (decimal)reader.GetDouble(1);
                avgBet = (decimal)reader.GetDouble(2);
                biggestWin = (decimal)reader.GetDouble(3);
                worstLoss = (decimal)reader.GetDouble(4);
            }
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM Session WHERE ProfileId = $profileId;";
            cmd.Parameters.AddWithValue("$profileId", profileId);
            totalSessions = Convert.ToInt32((long)cmd.ExecuteScalar()!);
        }

        int wins = 0, losses = 0, pushes = 0, blackjacks = 0, surrenders = 0;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT Outcome, COUNT(*)
                FROM HandResult hr
                JOIN Round r ON r.Id = hr.RoundId
                WHERE r.ProfileId = $profileId
                GROUP BY Outcome;
                """;
            cmd.Parameters.AddWithValue("$profileId", profileId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string outcome = reader.GetString(0);
                int count = reader.GetInt32(1);
                switch (outcome)
                {
                    case "Win": wins = count; break;
                    case "Lose": losses = count; break;
                    case "Push": pushes = count; break;
                    case "Blackjack": blackjacks = count; break;
                    case "Surrender": surrenders = count; break;
                }
            }
        }

        // Bust count: hands that lost where the player busted.
        // A busted hand has a Hit or Double decision with Lose outcome and no Stand decision.
        int busts = 0;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COUNT(DISTINCT d.RoundId || '-' || d.HandIndex)
                FROM Decision d
                JOIN Round r ON r.Id = d.RoundId
                WHERE r.ProfileId = $profileId
                  AND d.ResultOutcome = 'Lose'
                  AND d.Action IN ('Hit', 'Double')
                  AND NOT EXISTS (
                      SELECT 1 FROM Decision d2
                      WHERE d2.RoundId = d.RoundId
                        AND d2.HandIndex = d.HandIndex
                        AND d2.Action = 'Stand'
                  );
                """;
            cmd.Parameters.AddWithValue("$profileId", profileId);
            busts = Convert.ToInt32((long)cmd.ExecuteScalar()!);
        }

        // Current streak: consecutive wins or losses from most recent rounds
        int currentStreak = 0;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT NetPayout FROM Round
                WHERE ProfileId = $profileId
                ORDER BY Id DESC
                LIMIT 100;
                """;
            cmd.Parameters.AddWithValue("$profileId", profileId);
            using var reader = cmd.ExecuteReader();
            bool? streakPositive = null;
            while (reader.Read())
            {
                decimal payout = (decimal)reader.GetDouble(0);
                if (payout == 0) continue;

                bool isWin = payout > 0;
                if (streakPositive == null)
                {
                    streakPositive = isWin;
                    currentStreak = isWin ? 1 : -1;
                }
                else if (isWin == streakPositive)
                {
                    currentStreak += isWin ? 1 : -1;
                }
                else
                {
                    break;
                }
            }
        }

        return new OverviewStats(
            totalRounds, totalSessions, netProfit,
            wins, losses, pushes, blackjacks, surrenders, busts,
            avgBet, biggestWin, worstLoss, currentStreak);
    }

    public IReadOnlyList<BankrollPoint> GetBankrollHistory(int profileId)
    {
        var points = new List<BankrollPoint>();
        using var connection = _database.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT NetPayout FROM Round
            WHERE ProfileId = $profileId
            ORDER BY Id;
            """;
        cmd.Parameters.AddWithValue("$profileId", profileId);

        using var reader = cmd.ExecuteReader();
        decimal cumulative = 0;
        int roundNum = 0;
        while (reader.Read())
        {
            cumulative += (decimal)reader.GetDouble(0);
            roundNum++;
            points.Add(new BankrollPoint(roundNum, cumulative));
        }

        return points;
    }

    public IReadOnlyList<DealerBustStat> GetDealerBustByUpcard(int profileId)
    {
        var stats = new List<DealerBustStat>();
        using var connection = _database.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                d.DealerUpcard,
                COUNT(DISTINCT d.RoundId) as TotalRounds,
                COUNT(DISTINCT CASE WHEN r.DealerBusted = 1 THEN d.RoundId END) as BustRounds
            FROM Decision d
            JOIN Round r ON r.Id = d.RoundId
            WHERE r.ProfileId = $profileId
              AND d.DealerUpcard IN ('A', '2', '3', '4', '5', '6', '7', '8', '9', 'T')
            GROUP BY d.DealerUpcard
            ORDER BY CASE d.DealerUpcard
                WHEN 'A' THEN 1
                WHEN '2' THEN 2 WHEN '3' THEN 3 WHEN '4' THEN 4
                WHEN '5' THEN 5 WHEN '6' THEN 6 WHEN '7' THEN 7
                WHEN '8' THEN 8 WHEN '9' THEN 9 WHEN 'T' THEN 10
            END;
            """;
        cmd.Parameters.AddWithValue("$profileId", profileId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            stats.Add(new DealerBustStat(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetInt32(2)));
        }

        return stats;
    }

    public IReadOnlyList<HandValueOutcome> GetOutcomesByHandValue(int profileId)
    {
        var results = new List<HandValueOutcome>();
        using var connection = _database.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                d.PlayerValue,
                SUM(CASE WHEN d.ResultOutcome IN ('Win', 'Blackjack') THEN 1 ELSE 0 END),
                SUM(CASE WHEN d.ResultOutcome = 'Lose' THEN 1 ELSE 0 END),
                SUM(CASE WHEN d.ResultOutcome IN ('Push', 'Surrender') THEN 1 ELSE 0 END),
                COUNT(*)
            FROM Decision d
            JOIN Round r ON r.Id = d.RoundId
            WHERE r.ProfileId = $profileId
              AND d.ResultOutcome IS NOT NULL
            GROUP BY d.PlayerValue
            ORDER BY d.PlayerValue;
            """;
        cmd.Parameters.AddWithValue("$profileId", profileId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new HandValueOutcome(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4)));
        }

        return results;
    }

    public IReadOnlyList<StrategyCell> GetStrategyMatrix(int profileId, string handType)
    {
        var cells = new List<StrategyCell>();
        using var connection = _database.OpenConnection();
        using var cmd = connection.CreateCommand();

        string softFilter = handType.Equals("Soft", StringComparison.OrdinalIgnoreCase)
            ? "AND d.IsSoft = 1"
            : handType.Equals("Hard", StringComparison.OrdinalIgnoreCase)
                ? "AND d.IsSoft = 0"
                : "";

        if (handType.Equals("Pairs", StringComparison.OrdinalIgnoreCase))
        {
            cmd.CommandText = $"""
                SELECT
                    d.PlayerValue,
                    d.DealerUpcard,
                    SUM(CASE WHEN d.ResultOutcome IN ('Win', 'Blackjack') THEN 1 ELSE 0 END),
                    SUM(CASE WHEN d.ResultOutcome = 'Lose' THEN 1 ELSE 0 END),
                    SUM(CASE WHEN d.ResultOutcome IN ('Push', 'Surrender') THEN 1 ELSE 0 END),
                    COUNT(*),
                    COALESCE(SUM(d.ResultPayout), 0)
                FROM Decision d
                JOIN Round r ON r.Id = d.RoundId
                WHERE r.ProfileId = $profileId
                  AND d.ResultOutcome IS NOT NULL
                  AND d.DealerUpcard IN ('A', '2', '3', '4', '5', '6', '7', '8', '9', 'T')
                  AND EXISTS (
                      SELECT 1 FROM Decision d2
                      WHERE d2.RoundId = d.RoundId AND d2.HandIndex = d.HandIndex AND d2.Action = 'Split'
                  )
                GROUP BY d.PlayerValue, d.DealerUpcard
                ORDER BY d.PlayerValue,
                    CASE d.DealerUpcard
                        WHEN 'A' THEN 1
                        WHEN '2' THEN 2 WHEN '3' THEN 3 WHEN '4' THEN 4
                        WHEN '5' THEN 5 WHEN '6' THEN 6 WHEN '7' THEN 7
                        WHEN '8' THEN 8 WHEN '9' THEN 9 WHEN 'T' THEN 10
                    END;
                """;
        }
        else
        {
            cmd.CommandText = $"""
                SELECT
                    d.PlayerValue,
                    d.DealerUpcard,
                    SUM(CASE WHEN d.ResultOutcome IN ('Win', 'Blackjack') THEN 1 ELSE 0 END),
                    SUM(CASE WHEN d.ResultOutcome = 'Lose' THEN 1 ELSE 0 END),
                    SUM(CASE WHEN d.ResultOutcome IN ('Push', 'Surrender') THEN 1 ELSE 0 END),
                    COUNT(*),
                    COALESCE(SUM(d.ResultPayout), 0)
                FROM Decision d
                JOIN Round r ON r.Id = d.RoundId
                WHERE r.ProfileId = $profileId
                  AND d.ResultOutcome IS NOT NULL
                  AND d.DealerUpcard IN ('A', '2', '3', '4', '5', '6', '7', '8', '9', 'T')
                  {softFilter}
                GROUP BY d.PlayerValue, d.DealerUpcard
                ORDER BY d.PlayerValue,
                    CASE d.DealerUpcard
                        WHEN 'A' THEN 1
                        WHEN '2' THEN 2 WHEN '3' THEN 3 WHEN '4' THEN 4
                        WHEN '5' THEN 5 WHEN '6' THEN 6 WHEN '7' THEN 7
                        WHEN '8' THEN 8 WHEN '9' THEN 9 WHEN 'T' THEN 10
                    END;
                """;
        }

        cmd.Parameters.AddWithValue("$profileId", profileId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            cells.Add(new StrategyCell(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                (decimal)reader.GetDouble(6)));
        }

        return cells;
    }

    public IReadOnlyList<CardFrequency> GetCardDistribution(int profileId)
    {
        var frequencies = new List<CardFrequency>();
        using var connection = _database.OpenConnection();

        int totalCards = 0;
        var rankCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT Rank, COUNT(*)
                FROM CardSeen cs
                JOIN Round r ON r.Id = cs.RoundId
                WHERE r.ProfileId = $profileId AND cs.FaceDown = 0
                GROUP BY Rank;
                """;
            cmd.Parameters.AddWithValue("$profileId", profileId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string rank = reader.GetString(0);
                int count = reader.GetInt32(1);
                rankCounts[rank] = count;
                totalCards += count;
            }
        }

        if (totalCards == 0)
            return frequencies;

        // Standard rank order and expected frequencies
        // In a standard deck: A,2-9 each appear 4 times (7.69%), 10/J/Q/K each 4 times
        // But 10-value cards combined = 4/13 = 30.77%
        string[] ranks = ["Ace", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Jack", "Queen", "King"];
        string[] labels = ["A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K"];
        double expectedEach = 1.0 / 13.0;

        for (int i = 0; i < ranks.Length; i++)
        {
            int count = rankCounts.GetValueOrDefault(ranks[i], 0);
            double actual = totalCards > 0 ? (double)count / totalCards : 0;
            frequencies.Add(new CardFrequency(labels[i], count, expectedEach * 100, actual * 100));
        }

        return frequencies;
    }

    public IReadOnlyList<RuleFingerprint> GetDistinctRuleSets(int profileId)
    {
        var ruleSets = new List<RuleFingerprint>();
        using var connection = _database.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT BlackjackPayout, DealerHitsS17, DeckCount, SurrenderRule
            FROM Session
            WHERE ProfileId = $profileId;
            """;
        cmd.Parameters.AddWithValue("$profileId", profileId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            ruleSets.Add(new RuleFingerprint(
                reader.GetString(0),
                reader.GetInt32(1) == 1,
                reader.GetInt32(2),
                reader.GetString(3)));
        }

        return ruleSets;
    }

}
