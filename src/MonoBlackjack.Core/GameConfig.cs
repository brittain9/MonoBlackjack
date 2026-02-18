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
/// Shared constants and setting keys.
/// Mutable gameplay rules now live in immutable GameRules instances.
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
    public const string SettingShowHandValues = "ShowHandValues";
    public const string SettingKeybindHit = "KeybindHit";
    public const string SettingKeybindStand = "KeybindStand";
    public const string SettingKeybindDouble = "KeybindDouble";
    public const string SettingKeybindSplit = "KeybindSplit";
    public const string SettingKeybindSurrender = "KeybindSurrender";
    public const string SettingKeybindPause = "KeybindPause";
    public const string SettingKeybindBack = "KeybindBack";
    public const string SettingGraphicsBackgroundColor = "GraphicsBackgroundColor";
    public const string SettingGraphicsFontScale = "GraphicsFontScale";
    public const string SettingGraphicsCardBack = "GraphicsCardBack";

    /// <summary>
    /// Bust threshold. Standard blackjack = 21.
    /// </summary>
    public const int BustNumber = 21;

    /// <summary>
    /// Soft ace bonus value. Ace counts as 1 + this value when it doesn't bust.
    /// </summary>
    public const int AceExtraValue = 10;

    /// <summary>
    /// Insurance payout. Standard = 2:1 (pays 2x the insurance bet).
    /// This is a universal constant in blackjack.
    /// </summary>
    public const decimal InsurancePayout = 2.0m;
}
