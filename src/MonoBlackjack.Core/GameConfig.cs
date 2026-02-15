namespace MonoBlackjack.Core;

public enum DoubleDownRestriction
{
    AnyTwoCards,
    NineToEleven,
    TenToEleven
}

/// <summary>
/// Global game configuration. All rules are configurable for simulation/testing.
/// </summary>
public static class GameConfig
{
    /// <summary>
    /// Bust threshold. Standard blackjack = 21.
    /// </summary>
    public static int BustNumber = 21;

    /// <summary>
    /// Soft ace bonus value. Ace counts as 1 + this value when it doesn't bust.
    /// </summary>
    public static int AceExtraValue = 10;

    /// <summary>
    /// Number of decks in the shoe. Casino standard = 6.
    /// </summary>
    public static int NumberOfDecks = 6;

    /// <summary>
    /// Percentage of the shoe dealt before reshuffling between rounds.
    /// 75 means reshuffle when 25% of cards remain.
    /// </summary>
    public static int PenetrationPercent = 75;

    /// <summary>
    /// Use cryptographic RNG for casino-grade shuffling (production).
    /// False = deterministic Random for testing/reproducibility (development).
    /// </summary>
    public static bool UseCryptographicShuffle = false;

    /// <summary>
    /// Starting bankroll for human players.
    /// </summary>
    public static int StartingBank = 1000;

    /// <summary>
    /// True = dealer hits on soft 17 (H17, worse for player).
    /// False = dealer stands on soft 17 (S17, standard).
    /// </summary>
    public static bool DealerHitsSoft17 = false;

    /// <summary>
    /// Blackjack payout multiplier. Standard = 1.5 (3:2). Bad = 1.2 (6:5).
    /// </summary>
    public static decimal BlackjackPayout = 1.5m;

    /// <summary>
    /// Insurance payout. Standard = 2:1 (pays 2x the insurance bet).
    /// </summary>
    public static decimal InsurancePayout = 2.0m;

    /// <summary>
    /// Allow doubling down after splitting a pair.
    /// </summary>
    public static bool DoubleAfterSplit = true;

    /// <summary>
    /// Restrict double-down to specific starting hand values.
    /// </summary>
    public static DoubleDownRestriction DoubleDownRestriction = DoubleDownRestriction.AnyTwoCards;

    /// <summary>
    /// Allow re-splitting aces (rare, player-favorable).
    /// </summary>
    public static bool ResplitAces = false;

    /// <summary>
    /// Maximum number of times a hand can be split (typically 3).
    /// </summary>
    public static int MaxSplits = 3;

    /// <summary>
    /// Allow late surrender (forfeit hand for half the bet after dealer checks for blackjack).
    /// </summary>
    public static bool AllowLateSurrender = false;

    /// <summary>
    /// Allow early surrender (forfeit before dealer checks — very rare, very player-favorable).
    /// </summary>
    public static bool AllowEarlySurrender = false;

    /// <summary>
    /// Minimum bet amount.
    /// </summary>
    public static decimal MinimumBet = 5m;

    /// <summary>
    /// Maximum bet amount.
    /// </summary>
    public static decimal MaximumBet = 500m;
}
