using System.Globalization;

namespace MonoBlackjack.Core;

public enum DoubleDownRestriction
{
    AnyTwoCards,
    NineToEleven,
    TenToEleven
}

public enum BetFlowMode
{
    Betting,
    FreePlay
}

/// <summary>
/// Global game configuration. All rules are configurable for simulation/testing.
/// </summary>
public static class GameConfig
{
    public const string SettingDealerHitsSoft17 = "DealerHitsSoft17";
    public const string SettingBlackjackPayout = "BlackjackPayout";
    public const string SettingNumberOfDecks = "NumberOfDecks";
    public const string SettingSurrenderRule = "SurrenderRule";
    public const string SettingDoubleAfterSplit = "DoubleAfterSplit";
    public const string SettingResplitAces = "ResplitAces";
    public const string SettingMaxSplits = "MaxSplits";
    public const string SettingDoubleDownRestriction = "DoubleDownRestriction";
    public const string SettingPenetrationPercent = "PenetrationPercent";
    public const string SettingBetFlow = "BetFlow";

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
    public static decimal StartingBank = 1000m;

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

    /// <summary>
    /// Betting flow mode. Betting = manual bet selection, FreePlay = auto-deal with no bankroll.
    /// </summary>
    public static BetFlowMode BetFlow = BetFlowMode.Betting;

    public static IReadOnlyDictionary<string, string> ToSettingsDictionary()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SettingDealerHitsSoft17] = DealerHitsSoft17.ToString(),
            [SettingBlackjackPayout] = FormatBlackjackPayout(BlackjackPayout),
            [SettingNumberOfDecks] = NumberOfDecks.ToString(CultureInfo.InvariantCulture),
            [SettingSurrenderRule] = GetSurrenderRule(),
            [SettingDoubleAfterSplit] = DoubleAfterSplit.ToString(),
            [SettingResplitAces] = ResplitAces.ToString(),
            [SettingMaxSplits] = MaxSplits.ToString(CultureInfo.InvariantCulture),
            [SettingDoubleDownRestriction] = DoubleDownRestriction.ToString(),
            [SettingPenetrationPercent] = PenetrationPercent.ToString(CultureInfo.InvariantCulture),
            [SettingBetFlow] = BetFlow.ToString()
        };
    }

    public static void ApplySettings(IReadOnlyDictionary<string, string> settings)
    {
        if (settings.TryGetValue(SettingDealerHitsSoft17, out var dealerHitsSoft17)
            && bool.TryParse(dealerHitsSoft17, out var parsedDealerHitsSoft17))
        {
            DealerHitsSoft17 = parsedDealerHitsSoft17;
        }

        if (settings.TryGetValue(SettingBlackjackPayout, out var blackjackPayout)
            && TryParseBlackjackPayout(blackjackPayout, out var parsedBlackjackPayout))
        {
            BlackjackPayout = parsedBlackjackPayout;
        }

        if (settings.TryGetValue(SettingNumberOfDecks, out var numberOfDecks)
            && int.TryParse(numberOfDecks, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDecks))
        {
            NumberOfDecks = Math.Clamp(parsedDecks, 1, 1000);
        }

        if (settings.TryGetValue(SettingSurrenderRule, out var surrenderRule))
        {
            ApplySurrenderRule(surrenderRule);
        }

        if (settings.TryGetValue(SettingDoubleAfterSplit, out var doubleAfterSplit)
            && bool.TryParse(doubleAfterSplit, out var parsedDoubleAfterSplit))
        {
            DoubleAfterSplit = parsedDoubleAfterSplit;
        }

        if (settings.TryGetValue(SettingResplitAces, out var resplitAces)
            && bool.TryParse(resplitAces, out var parsedResplitAces))
        {
            ResplitAces = parsedResplitAces;
        }

        if (settings.TryGetValue(SettingMaxSplits, out var maxSplits)
            && int.TryParse(maxSplits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxSplits))
        {
            MaxSplits = Math.Clamp(parsedMaxSplits, 1, 10);
        }

        if (settings.TryGetValue(SettingDoubleDownRestriction, out var restriction)
            && Enum.TryParse<DoubleDownRestriction>(restriction, ignoreCase: true, out var parsedRestriction))
        {
            DoubleDownRestriction = parsedRestriction;
        }

        if (settings.TryGetValue(SettingPenetrationPercent, out var penetrationPercent)
            && int.TryParse(penetrationPercent, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPenetration))
        {
            PenetrationPercent = Math.Clamp(parsedPenetration, 1, 100);
        }

        if (settings.TryGetValue(SettingBetFlow, out var betFlow)
            && Enum.TryParse<BetFlowMode>(betFlow, ignoreCase: true, out var parsedBetFlow))
        {
            BetFlow = parsedBetFlow;
        }
    }

    private static void ApplySurrenderRule(string surrenderRule)
    {
        switch (surrenderRule.Trim().ToLowerInvariant())
        {
            case "early":
                AllowEarlySurrender = true;
                AllowLateSurrender = false;
                break;
            case "late":
                AllowEarlySurrender = false;
                AllowLateSurrender = true;
                break;
            default:
                AllowEarlySurrender = false;
                AllowLateSurrender = false;
                break;
        }
    }

    private static string GetSurrenderRule()
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
