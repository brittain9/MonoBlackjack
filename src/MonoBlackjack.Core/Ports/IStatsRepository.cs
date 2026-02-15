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
    decimal Payout);

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
    RuleFingerprint Rules,
    IReadOnlyList<HandResultRecord> HandResults,
    IReadOnlyList<CardSeenRecord> CardsSeen,
    IReadOnlyList<DecisionRecord> Decisions);

public interface IStatsRepository
{
    void RecordRound(int profileId, RoundRecord round);
}
