using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack;
using MonoBlackjack.Core;

namespace MonoBlackjack.App.Tests;

public class Phase4StateTests
{
    [Fact]
    public void MenuState_ResolveModeFromMenuLabel_ReturnsExpectedModes()
    {
        Assert.Equal(BetFlowMode.Betting, MenuState.ResolveModeFromMenuLabel(MenuState.CasinoModeLabel));
        Assert.Equal(BetFlowMode.FreePlay, MenuState.ResolveModeFromMenuLabel(MenuState.PracticeModeLabel));
    }

    [Fact]
    public void MenuState_ResolveModeFromMenuLabel_ThrowsForUnknownLabel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MenuState.ResolveModeFromMenuLabel("Arcade Mode"));
    }

    [Fact]
    public void SettingsState_BuildSavedSettings_MergesOverridesPreservesUnknownAndRemovesBetFlow()
    {
        var loaded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GameConfig.SettingBetFlow] = "Betting",
            [GameConfig.SettingShowHandValues] = "True",
            ["LegacyCustomSetting"] = "KeepMe"
        };

        var selected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GameConfig.SettingShowHandValues] = "False",
            [GameConfig.SettingShowRecommendations] = "True"
        };

        var saved = SettingsState.BuildSavedSettings(loaded, selected);

        Assert.False(saved.ContainsKey(GameConfig.SettingBetFlow));
        Assert.Equal("False", saved[GameConfig.SettingShowHandValues]);
        Assert.Equal("True", saved[GameConfig.SettingShowRecommendations]);
        Assert.Equal("KeepMe", saved["LegacyCustomSetting"]);
    }

    [Fact]
    public void GameState_ResolveShowHandValues_UsesDefaultAndParsesLegacyValues()
    {
        var missing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var explicitFalse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GameConfig.SettingShowHandValues] = "False"
        };
        var legacyNo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GameConfig.SettingShowHandValues] = "No"
        };

        Assert.True(GameState.ResolveShowHandValues(missing));
        Assert.False(GameState.ResolveShowHandValues(explicitFalse));
        Assert.False(GameState.ResolveShowHandValues(legacyNo));
    }

    [Fact]
    public void GameState_ApplySelectedMode_OverridesOnlyBetFlow()
    {
        var baseRules = GameRules.Standard with
        {
            BetFlow = BetFlowMode.Betting,
            NumberOfDecks = 8,
            PenetrationPercent = 85
        };

        var applied = GameState.ApplySelectedMode(baseRules, BetFlowMode.FreePlay);

        Assert.Equal(BetFlowMode.FreePlay, applied.BetFlow);
        Assert.Equal(baseRules.NumberOfDecks, applied.NumberOfDecks);
        Assert.Equal(baseRules.PenetrationPercent, applied.PenetrationPercent);
    }

    [Fact]
    public void BlackjackGame_StateHistoryHelpers_RespectLifoAndSizeLimit()
    {
        var first = new TestState();
        var second = new TestState();
        var third = new TestState();
        var history = new List<State>();

        BlackjackGame.PushStateHistoryEntry(history, first, maxStateHistory: 2);
        BlackjackGame.PushStateHistoryEntry(history, second, maxStateHistory: 2);
        BlackjackGame.PushStateHistoryEntry(history, third, maxStateHistory: 2);

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(2, history.Count);
        Assert.Same(second, history[0]);
        Assert.Same(third, history[1]);

        var popped = BlackjackGame.TryPopStateHistory(history, out var poppedState);
        Assert.True(popped);
        Assert.Same(third, poppedState);
        Assert.Single(history);

        popped = BlackjackGame.TryPopStateHistory(new List<State>(), out poppedState);
        Assert.False(popped);
        Assert.Null(poppedState);
    }

    private sealed class TestState : State
    {
        public int DisposeCount { get; private set; }

        public TestState()
            : base(null!, null!, null!)
        {
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch) { }

        public override void PostUpdate(GameTime gameTime) { }

        public override void Update(GameTime gameTime) { }

        public override void HandleResize(Rectangle vp) { }

        public override void Dispose()
        {
            DisposeCount++;
        }
    }
}
