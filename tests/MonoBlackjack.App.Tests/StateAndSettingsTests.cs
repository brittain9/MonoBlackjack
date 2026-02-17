using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack;
using MonoBlackjack.Core;

namespace MonoBlackjack.App.Tests;

public class StateAndSettingsTests
{
    [Fact]
    public void MenuState_ResolveModeFromMenuLabel_ReturnsExpectedModes()
    {
        Assert.Equal(BetFlowMode.Betting, MenuState.ResolveModeFromMenuLabel(MenuState.CasinoModeLabel));
        Assert.Equal(BetFlowMode.FreePlay, MenuState.ResolveModeFromMenuLabel(MenuState.FreeplayModeLabel));
    }

    [Fact]
    public void MenuState_ResolveModeFromMenuLabel_ThrowsForUnknownLabel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MenuState.ResolveModeFromMenuLabel("Arcade Mode"));
    }

    [Fact]
    public void SettingsState_BuildSavedSettings_FiltersToSupportedContractValues()
    {
        var selected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GameConfig.SettingShowHandValues] = "false",
            [GameConfig.SettingShowRecommendations] = "True",
            [GameConfig.SettingKeybindHit] = "h",
            [GameConfig.SettingGraphicsCardBack] = "Classic",
            [GameConfig.SettingNumberOfDecks] = "999",
            ["LegacyCustomSetting"] = "on"
        };

        var saved = SettingsState.BuildSavedSettings(selected);

        Assert.Equal(4, saved.Count);
        Assert.Equal("False", saved[GameConfig.SettingShowHandValues]);
        Assert.Equal("True", saved[GameConfig.SettingShowRecommendations]);
        Assert.Equal("H", saved[GameConfig.SettingKeybindHit]);
        Assert.Equal("Classic", saved[GameConfig.SettingGraphicsCardBack]);
        Assert.False(saved.ContainsKey("LegacyCustomSetting"));
        Assert.False(saved.ContainsKey(GameConfig.SettingNumberOfDecks));
    }

    [Fact]
    public void SettingsState_FindKeybindConflicts_AllowsPauseAndBackToShareEscape()
    {
        var selected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GameConfig.SettingKeybindHit] = "H",
            [GameConfig.SettingKeybindStand] = "S",
            [GameConfig.SettingKeybindDouble] = "D",
            [GameConfig.SettingKeybindSplit] = "P",
            [GameConfig.SettingKeybindSurrender] = "R",
            [GameConfig.SettingKeybindPause] = "Escape",
            [GameConfig.SettingKeybindBack] = "Escape"
        };

        var conflicts = SettingsState.FindKeybindConflicts(selected);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void SettingsState_FindKeybindConflicts_RejectsDuplicateBindingsAcrossActions()
    {
        var selected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GameConfig.SettingKeybindHit] = "H",
            [GameConfig.SettingKeybindStand] = "H",
            [GameConfig.SettingKeybindDouble] = "D",
            [GameConfig.SettingKeybindSplit] = "P",
            [GameConfig.SettingKeybindSurrender] = "R",
            [GameConfig.SettingKeybindPause] = "Escape",
            [GameConfig.SettingKeybindBack] = "Escape"
        };

        var conflicts = SettingsState.FindKeybindConflicts(selected);

        Assert.Single(conflicts);
        Assert.Equal(GameConfig.SettingKeybindHit, conflicts[0].ExistingKey);
        Assert.Equal(GameConfig.SettingKeybindStand, conflicts[0].DuplicateKey);
        Assert.Equal("H", conflicts[0].Binding);
    }

    [Fact]
    public void GameState_ResolveShowHandValues_UsesDefaultAndStrictBooleanParsing()
    {
        var missing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var explicitFalse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GameConfig.SettingShowHandValues] = "False"
        };
        var invalidNo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GameConfig.SettingShowHandValues] = "No"
        };
        var invalid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GameConfig.SettingShowHandValues] = "Invalid"
        };

        Assert.True(GameState.ResolveShowHandValues(missing));
        Assert.False(GameState.ResolveShowHandValues(explicitFalse));
        Assert.True(GameState.ResolveShowHandValues(invalidNo));
        Assert.True(GameState.ResolveShowHandValues(invalid));
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

    [Fact]
    public void GameState_ResolveVisibleActionButtonKeys_AlwaysIncludesCoreActions()
    {
        var keys = GameState.ResolveVisibleActionButtonKeys(
            canSplit: false,
            canDoubleDown: false,
            canSurrender: false);

        Assert.Equal(["Hit", "Stand"], keys);
    }

    [Fact]
    public void GameState_ResolveVisibleActionButtonKeys_OrdersOptionalActionsConsistently()
    {
        var keys = GameState.ResolveVisibleActionButtonKeys(
            canSplit: true,
            canDoubleDown: true,
            canSurrender: true);

        Assert.Equal(["Hit", "Stand", "Split", "Double", "Surrender"], keys);
    }

    [Fact]
    public void GameState_ResolveVisibleActionButtonKeys_IncludesOnlyEnabledOptionalActions()
    {
        var keys = GameState.ResolveVisibleActionButtonKeys(
            canSplit: true,
            canDoubleDown: false,
            canSurrender: true);

        Assert.Equal(["Hit", "Stand", "Split", "Surrender"], keys);
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
