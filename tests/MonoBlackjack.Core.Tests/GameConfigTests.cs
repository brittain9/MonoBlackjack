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

    [Fact]
    public void SettingsContract_Sanitize_DropsUnsupportedAndInvalidValues()
    {
        var input = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GameConfig.SettingShowHandValues] = "false",
            [GameConfig.SettingNumberOfDecks] = "999",
            [GameConfig.SettingKeybindStand] = "h",
            [GameConfig.SettingKeybindPause] = "escape",
            ["LegacySetting"] = "on"
        };

        var sanitized = SettingsContract.Sanitize(input);

        sanitized.Should().ContainKey(GameConfig.SettingShowHandValues);
        sanitized[GameConfig.SettingShowHandValues].Should().Be("False");
        sanitized.Should().ContainKey(GameConfig.SettingKeybindStand);
        sanitized[GameConfig.SettingKeybindStand].Should().Be("H");
        sanitized.Should().ContainKey(GameConfig.SettingKeybindPause);
        sanitized[GameConfig.SettingKeybindPause].Should().Be("Escape");
        sanitized.Should().NotContainKey(GameConfig.SettingNumberOfDecks);
        sanitized.Should().NotContainKey("LegacySetting");
    }

    [Fact]
    public void SettingsContract_MergeWithDefaults_UsesDefaultsForMissingValues()
    {
        var merged = SettingsContract.MergeWithDefaults(new Dictionary<string, string>());

        merged.Should().ContainKey(GameConfig.SettingKeybindHit);
        merged[GameConfig.SettingKeybindHit].Should().Be("H");
        merged.Should().ContainKey(GameConfig.SettingKeybindPause);
        merged[GameConfig.SettingKeybindPause].Should().Be("Escape");
        merged.Should().ContainKey(GameConfig.SettingKeybindBack);
        merged[GameConfig.SettingKeybindBack].Should().Be("Escape");
        merged.Should().ContainKey(GameConfig.SettingShowHandValues);
        merged[GameConfig.SettingShowHandValues].Should().Be("True");
        merged.Should().ContainKey(GameConfig.SettingNumberOfDecks);
        merged[GameConfig.SettingNumberOfDecks].Should().Be("6");
    }
}
