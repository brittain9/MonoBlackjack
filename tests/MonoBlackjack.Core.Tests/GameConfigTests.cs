using FluentAssertions;
using MonoBlackjack.Core;

namespace MonoBlackjack.Core.Tests;

public class GameConfigTests
{
    [Fact]
    public void GameRules_FromSettings_ParsesAllFields()
    {
        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GameConfig.SettingDealerHitsSoft17] = "true",
            [GameConfig.SettingBlackjackPayout] = "6:5",
            [GameConfig.SettingNumberOfDecks] = "8",
            [GameConfig.SettingSurrenderRule] = "late",
            [GameConfig.SettingDoubleAfterSplit] = "false",
            [GameConfig.SettingResplitAces] = "true",
            [GameConfig.SettingMaxSplits] = "4",
            [GameConfig.SettingDoubleDownRestriction] = DoubleDownRestriction.TenToEleven.ToString(),
            [GameConfig.SettingPenetrationPercent] = "85"
        };

        var rules = GameRules.FromSettings(updates);

        rules.DealerHitsSoft17.Should().BeTrue();
        rules.BlackjackPayout.Should().Be(1.2m);
        rules.NumberOfDecks.Should().Be(8);
        rules.AllowLateSurrender.Should().BeTrue();
        rules.AllowEarlySurrender.Should().BeFalse();
        rules.DoubleAfterSplit.Should().BeFalse();
        rules.ResplitAces.Should().BeTrue();
        rules.MaxSplits.Should().Be(4);
        rules.DoubleDownRestriction.Should().Be(DoubleDownRestriction.TenToEleven);
        rules.PenetrationPercent.Should().Be(85);
    }

    [Fact]
    public void GameRules_FromSettings_IgnoresUnsupportedBetFlowKey()
    {
        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BetFlow"] = BetFlowMode.FreePlay.ToString()
        };

        var rules = GameRules.FromSettings(updates);

        rules.BetFlow.Should().Be(BetFlowMode.Betting);
    }

    [Fact]
    public void GameRules_ToSettingsDictionary_ContainsAllKeys()
    {
        var rules = GameRules.Standard;
        var settings = rules.ToSettingsDictionary();

        settings.Keys.Should().Contain(GameConfig.SettingDealerHitsSoft17);
        settings.Keys.Should().Contain(GameConfig.SettingBlackjackPayout);
        settings.Keys.Should().Contain(GameConfig.SettingNumberOfDecks);
        settings.Keys.Should().Contain(GameConfig.SettingSurrenderRule);
        settings.Keys.Should().Contain(GameConfig.SettingDoubleAfterSplit);
        settings.Keys.Should().Contain(GameConfig.SettingResplitAces);
        settings.Keys.Should().Contain(GameConfig.SettingMaxSplits);
        settings.Keys.Should().Contain(GameConfig.SettingDoubleDownRestriction);
        settings.Keys.Should().Contain(GameConfig.SettingPenetrationPercent);
        settings.Keys.Should().NotContain("BetFlow");
    }
}
