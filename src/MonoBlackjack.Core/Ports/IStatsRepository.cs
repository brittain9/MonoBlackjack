using MonoBlackjack.Core.Events;

namespace MonoBlackjack.Core.Ports;

public sealed record RuleFingerprint(
    string BlackjackPayout,
    bool DealerHitsSoft17,
    int DeckCount,
    string SurrenderRule);

public sealed record HandResultRecord(
    int HandIndex,
    HandOutcome Outcome,
    decimal Payout,
    bool PlayerBusted);

public sealed record CardSeenRecord(
    string Recipient,
    int HandIndex,
    string Rank,
    string Suit,
    bool FaceDown);

public sealed record DecisionRecord(
    int HandIndex,
    int PlayerValue,
    bool IsSoft,
    string DealerUpcard,
    string Action,
    HandOutcome? ResultOutcome,
    decimal? ResultPayout);

public sealed record RoundRecord(
    DateTime PlayedAtUtc,
    decimal BetAmount,
    decimal NetPayout,
    bool DealerBusted,
    RuleFingerprint Rules,
    IReadOnlyList<HandResultRecord> HandResults,
    IReadOnlyList<CardSeenRecord> CardsSeen,
    IReadOnlyList<DecisionRecord> Decisions);

// Dashboard query DTOs

public sealed record OverviewStats(
    int TotalRounds,
    int TotalSessions,
    decimal NetProfit,
    int Wins,
    int Losses,
    int Pushes,
    int Blackjacks,
    int Surrenders,
    int Busts,
    decimal AverageBet,
    decimal BiggestWin,
    decimal WorstLoss,
    int CurrentStreak);

public sealed record BankrollPoint(int RoundNumber, decimal CumulativeProfit);

public sealed record DealerBustStat(string Upcard, int TotalHands, int BustedHands);

public sealed record HandValueOutcome(int PlayerValue, int Wins, int Losses, int Pushes, int Total);

public sealed record StrategyCell(
    int PlayerValue,
    string DealerUpcard,
    int Wins,
    int Losses,
    int Pushes,
    int Total,
    decimal NetPayout);

public sealed record CardFrequency(string Rank, int Count, double ExpectedPercent, double ActualPercent);

public interface IStatsRepository
{
    void RecordRound(int profileId, RoundRecord round);

    OverviewStats GetOverviewStats(int profileId);
    IReadOnlyList<BankrollPoint> GetBankrollHistory(int profileId);
    IReadOnlyList<DealerBustStat> GetDealerBustByUpcard(int profileId);
    IReadOnlyList<HandValueOutcome> GetOutcomesByHandValue(int profileId);
    IReadOnlyList<StrategyCell> GetStrategyMatrix(int profileId, string handType);
    IReadOnlyList<CardFrequency> GetCardDistribution(int profileId);
    IReadOnlyList<RuleFingerprint> GetDistinctRuleSets(int profileId);
}
