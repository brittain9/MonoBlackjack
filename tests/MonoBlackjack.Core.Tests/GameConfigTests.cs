using FluentAssertions;
using MonoBlackjack.Core;

namespace MonoBlackjack.Core.Tests;

[Collection("GameConfig")]
public class GameConfigTests
{
    [Fact]
    public void ApplySettings_UpdatesAllPhase6RuleFields()
    {
        var original = new Dictionary<string, string>(GameConfig.ToSettingsDictionary(), StringComparer.OrdinalIgnoreCase);
        try
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

            GameConfig.ApplySettings(updates);

            GameConfig.DealerHitsSoft17.Should().BeTrue();
            GameConfig.BlackjackPayout.Should().Be(1.2m);
            GameConfig.NumberOfDecks.Should().Be(8);
            GameConfig.AllowLateSurrender.Should().BeTrue();
            GameConfig.AllowEarlySurrender.Should().BeFalse();
            GameConfig.DoubleAfterSplit.Should().BeFalse();
            GameConfig.ResplitAces.Should().BeTrue();
            GameConfig.MaxSplits.Should().Be(4);
            GameConfig.DoubleDownRestriction.Should().Be(DoubleDownRestriction.TenToEleven);
            GameConfig.PenetrationPercent.Should().Be(85);
        }
        finally
        {
            GameConfig.ApplySettings(original);
        }
    }

    [Fact]
    public void ToSettingsDictionary_ContainsPhase6Keys()
    {
        var settings = GameConfig.ToSettingsDictionary();

        settings.Keys.Should().Contain(GameConfig.SettingDealerHitsSoft17);
        settings.Keys.Should().Contain(GameConfig.SettingBlackjackPayout);
        settings.Keys.Should().Contain(GameConfig.SettingNumberOfDecks);
        settings.Keys.Should().Contain(GameConfig.SettingSurrenderRule);
        settings.Keys.Should().Contain(GameConfig.SettingDoubleAfterSplit);
        settings.Keys.Should().Contain(GameConfig.SettingResplitAces);
        settings.Keys.Should().Contain(GameConfig.SettingMaxSplits);
        settings.Keys.Should().Contain(GameConfig.SettingDoubleDownRestriction);
        settings.Keys.Should().Contain(GameConfig.SettingPenetrationPercent);
    }
}
