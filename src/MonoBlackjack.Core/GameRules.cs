using System.Globalization;

namespace MonoBlackjack.Core;

/// <summary>
/// Immutable game rules configuration. Replaces mutable static GameConfig.
/// All casino rules are configurable; universal constants (bust = 21, insurance = 2:1) are in GameConfig.
/// </summary>
public sealed record GameRules
{
    /// <summary>
    /// Number of decks in the shoe. Casino standard = 6.
    /// </summary>
    public int NumberOfDecks { get; init; }

    /// <summary>
    /// Percentage of the shoe dealt before reshuffling between rounds.
    /// 75 means reshuffle when 25% of cards remain.
    /// </summary>
    public int PenetrationPercent { get; init; }

    /// <summary>
    /// Use cryptographic RNG for casino-grade shuffling (production).
    /// False = deterministic Random for testing/reproducibility (development).
    /// </summary>
    public bool UseCryptographicShuffle { get; init; }

    /// <summary>
    /// Starting bankroll for human players.
    /// </summary>
    public decimal StartingBank { get; init; }

    /// <summary>
    /// True = dealer hits on soft 17 (H17, worse for player).
    /// False = dealer stands on soft 17 (S17, standard).
    /// </summary>
    public bool DealerHitsSoft17 { get; init; }

    /// <summary>
    /// Blackjack payout multiplier. Standard = 1.5 (3:2). Bad = 1.2 (6:5).
    /// </summary>
    public decimal BlackjackPayout { get; init; }

    /// <summary>
    /// Allow doubling down after splitting a pair.
    /// </summary>
    public bool DoubleAfterSplit { get; init; }

    /// <summary>
    /// Restrict double-down to specific starting hand values.
    /// </summary>
    public DoubleDownRestriction DoubleDownRestriction { get; init; }

    /// <summary>
    /// Allow re-splitting aces (rare, player-favorable).
    /// </summary>
    public bool ResplitAces { get; init; }

    /// <summary>
    /// Maximum number of times a hand can be split (typically 3).
    /// </summary>
    public int MaxSplits { get; init; }

    /// <summary>
    /// Allow late surrender (forfeit hand for half the bet after dealer checks for blackjack).
    /// </summary>
    public bool AllowLateSurrender { get; init; }

    /// <summary>
    /// Allow early surrender (forfeit before dealer checks — very rare, very player-favorable).
    /// </summary>
    public bool AllowEarlySurrender { get; init; }

    /// <summary>
    /// Minimum bet amount.
    /// </summary>
    public decimal MinimumBet { get; init; }

    /// <summary>
    /// Maximum bet amount.
    /// </summary>
    public decimal MaximumBet { get; init; }

    /// <summary>
    /// Betting flow mode. Betting = manual bet selection, FreePlay = auto-deal with no bankroll.
    /// </summary>
    public BetFlowMode BetFlow { get; init; }

    /// <summary>
    /// Creates a GameRules instance with validation.
    /// </summary>
    public GameRules(
        int numberOfDecks,
        int penetrationPercent,
        bool useCryptographicShuffle,
        decimal startingBank,
        bool dealerHitsSoft17,
        decimal blackjackPayout,
        bool doubleAfterSplit,
        DoubleDownRestriction doubleDownRestriction,
        bool resplitAces,
        int maxSplits,
        bool allowLateSurrender,
        bool allowEarlySurrender,
        decimal minimumBet,
        decimal maximumBet,
        BetFlowMode betFlow)
    {
        if (numberOfDecks < 1 || numberOfDecks > 1000)
            throw new ArgumentOutOfRangeException(nameof(numberOfDecks), numberOfDecks, "Number of decks must be between 1 and 1000");

        if (penetrationPercent < 1 || penetrationPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(penetrationPercent), penetrationPercent, "Penetration percent must be between 1 and 100");

        if (startingBank < 0)
            throw new ArgumentOutOfRangeException(nameof(startingBank), startingBank, "Starting bank cannot be negative");

        if (blackjackPayout <= 0)
            throw new ArgumentOutOfRangeException(nameof(blackjackPayout), blackjackPayout, "Blackjack payout must be positive");

        if (maxSplits < 1 || maxSplits > 10)
            throw new ArgumentOutOfRangeException(nameof(maxSplits), maxSplits, "Max splits must be between 1 and 10");

        if (minimumBet < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumBet), minimumBet, "Minimum bet cannot be negative");

        if (maximumBet < minimumBet)
            throw new ArgumentOutOfRangeException(nameof(maximumBet), maximumBet, $"Maximum bet ({maximumBet}) must be >= minimum bet ({minimumBet})");

        NumberOfDecks = numberOfDecks;
        PenetrationPercent = penetrationPercent;
        UseCryptographicShuffle = useCryptographicShuffle;
        StartingBank = startingBank;
        DealerHitsSoft17 = dealerHitsSoft17;
        BlackjackPayout = blackjackPayout;
        DoubleAfterSplit = doubleAfterSplit;
        DoubleDownRestriction = doubleDownRestriction;
        ResplitAces = resplitAces;
        MaxSplits = maxSplits;
        AllowLateSurrender = allowLateSurrender;
        AllowEarlySurrender = allowEarlySurrender;
        MinimumBet = minimumBet;
        MaximumBet = maximumBet;
        BetFlow = betFlow;
    }

    /// <summary>
    /// Standard casino rules (6 decks, 3:2 payout, S17, etc.).
    /// </summary>
    public static GameRules Standard => new(
        numberOfDecks: 6,
        penetrationPercent: 75,
        useCryptographicShuffle: false,
        startingBank: 1000m,
        dealerHitsSoft17: false,  // S17 is standard
        blackjackPayout: 1.5m,    // 3:2
        doubleAfterSplit: true,
        doubleDownRestriction: DoubleDownRestriction.AnyTwoCards,
        resplitAces: false,
        maxSplits: 3,
        allowLateSurrender: false,
        allowEarlySurrender: false,
        minimumBet: 5m,
        maximumBet: 500m,
        betFlow: BetFlowMode.Betting
    );

    /// <summary>
    /// Converts rules to settings dictionary for persistence.
    /// </summary>
    public IReadOnlyDictionary<string, string> ToSettingsDictionary()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GameConfig.SettingDealerHitsSoft17] = DealerHitsSoft17.ToString(),
            [GameConfig.SettingBlackjackPayout] = FormatBlackjackPayout(BlackjackPayout),
            [GameConfig.SettingNumberOfDecks] = NumberOfDecks.ToString(CultureInfo.InvariantCulture),
            [GameConfig.SettingSurrenderRule] = GetSurrenderRule(),
            [GameConfig.SettingDoubleAfterSplit] = DoubleAfterSplit.ToString(),
            [GameConfig.SettingResplitAces] = ResplitAces.ToString(),
            [GameConfig.SettingMaxSplits] = MaxSplits.ToString(CultureInfo.InvariantCulture),
            [GameConfig.SettingDoubleDownRestriction] = DoubleDownRestriction.ToString(),
            [GameConfig.SettingPenetrationPercent] = PenetrationPercent.ToString(CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Creates GameRules from settings dictionary.
    /// </summary>
    public static GameRules FromSettings(IReadOnlyDictionary<string, string> settings)
    {
        var rules = Standard; // Start with defaults

        if (settings.TryGetValue(GameConfig.SettingDealerHitsSoft17, out var dealerHitsSoft17)
            && bool.TryParse(dealerHitsSoft17, out var parsedDealerHitsSoft17))
        {
            rules = rules with { DealerHitsSoft17 = parsedDealerHitsSoft17 };
        }

        if (settings.TryGetValue(GameConfig.SettingBlackjackPayout, out var blackjackPayout)
            && TryParseBlackjackPayout(blackjackPayout, out var parsedBlackjackPayout))
        {
            rules = rules with { BlackjackPayout = parsedBlackjackPayout };
        }

        if (settings.TryGetValue(GameConfig.SettingNumberOfDecks, out var numberOfDecks)
            && int.TryParse(numberOfDecks, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDecks))
        {
            rules = rules with { NumberOfDecks = Math.Clamp(parsedDecks, 1, 1000) };
        }

        if (settings.TryGetValue(GameConfig.SettingSurrenderRule, out var surrenderRule))
        {
            var (early, late) = ParseSurrenderRule(surrenderRule);
            rules = rules with { AllowEarlySurrender = early, AllowLateSurrender = late };
        }

        if (settings.TryGetValue(GameConfig.SettingDoubleAfterSplit, out var doubleAfterSplit)
            && bool.TryParse(doubleAfterSplit, out var parsedDoubleAfterSplit))
        {
            rules = rules with { DoubleAfterSplit = parsedDoubleAfterSplit };
        }

        if (settings.TryGetValue(GameConfig.SettingResplitAces, out var resplitAces)
            && bool.TryParse(resplitAces, out var parsedResplitAces))
        {
            rules = rules with { ResplitAces = parsedResplitAces };
        }

        if (settings.TryGetValue(GameConfig.SettingMaxSplits, out var maxSplits)
            && int.TryParse(maxSplits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxSplits))
        {
            rules = rules with { MaxSplits = Math.Clamp(parsedMaxSplits, 1, 10) };
        }

        if (settings.TryGetValue(GameConfig.SettingDoubleDownRestriction, out var restriction)
            && Enum.TryParse<DoubleDownRestriction>(restriction, ignoreCase: true, out var parsedRestriction))
        {
            rules = rules with { DoubleDownRestriction = parsedRestriction };
        }

        if (settings.TryGetValue(GameConfig.SettingPenetrationPercent, out var penetrationPercent)
            && int.TryParse(penetrationPercent, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPenetration))
        {
            rules = rules with { PenetrationPercent = Math.Clamp(parsedPenetration, 1, 100) };
        }

        return rules;
    }

    private static (bool early, bool late) ParseSurrenderRule(string surrenderRule)
    {
        return surrenderRule.Trim().ToLowerInvariant() switch
        {
            "early" => (true, false),
            "late" => (false, true),
            _ => (false, false)
        };
    }

    private string GetSurrenderRule()
    {
        if (AllowEarlySurrender)
            return "early";
        if (AllowLateSurrender)
            return "late";
        return "none";
    }

    private static string FormatBlackjackPayout(decimal payout)
    {
        if (payout == 1.5m)
            return "3:2";
        if (payout == 1.2m)
            return "6:5";
        return payout.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryParseBlackjackPayout(string value, out decimal payout)
    {
        if (value.Equals("3:2", StringComparison.OrdinalIgnoreCase))
        {
            payout = 1.5m;
            return true;
        }

        if (value.Equals("6:5", StringComparison.OrdinalIgnoreCase))
        {
            payout = 1.2m;
            return true;
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out payout);
    }
}
