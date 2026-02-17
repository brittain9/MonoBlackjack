using Microsoft.Xna.Framework.Input;
using MonoBlackjack.Core;

namespace MonoBlackjack.App.Tests;

public class KeybindMapTests
{
    [Theory]
    [InlineData("H", Keys.H, false)]
    [InlineData("Escape", Keys.Escape, false)]
    [InlineData("Backspace", Keys.Back, false)]
    [InlineData("Shift+D", Keys.D, true)]
    public void InputBinding_TryParse_ParsesSupportedBindings(string raw, Keys expectedKey, bool expectedShift)
    {
        var parsed = InputBinding.TryParse(raw, out var binding);

        Assert.True(parsed);
        Assert.Equal(expectedKey, binding.Key);
        Assert.Equal(expectedShift, binding.RequiresShift);
    }

    [Fact]
    public void KeybindMap_FromSettings_UsesDefaultsWhenValuesMissing()
    {
        var map = KeybindMap.FromSettings(new Dictionary<string, string>());

        Assert.Equal(Keys.H, map.GetBinding(InputAction.Hit).Key);
        Assert.Equal(Keys.S, map.GetBinding(InputAction.Stand).Key);
        Assert.Equal(Keys.Escape, map.GetBinding(InputAction.Pause).Key);
        Assert.Equal(Keys.Escape, map.GetBinding(InputAction.Back).Key);
    }

    [Fact]
    public void KeybindMap_FromSettings_AllowsPauseAndBackToShareBinding()
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GameConfig.SettingKeybindPause] = "Escape",
            [GameConfig.SettingKeybindBack] = "Escape",
            [GameConfig.SettingKeybindSplit] = "P"
        };

        var map = KeybindMap.FromSettings(settings);

        Assert.Equal("Escape", map.GetLabel(InputAction.Pause));
        Assert.Equal("Escape", map.GetLabel(InputAction.Back));
        Assert.Equal(map.GetBinding(InputAction.Pause), map.GetBinding(InputAction.Back));
    }

    [Fact]
    public void KeybindMap_IsJustPressed_RequiresShiftForShiftBindings()
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GameConfig.SettingKeybindDouble] = "Shift+D"
        };
        var map = KeybindMap.FromSettings(settings);

        var previous = new KeyboardState(Keys.LeftShift);
        var withShift = new KeyboardState(Keys.LeftShift, Keys.D);
        var withoutShift = new KeyboardState(Keys.D);

        Assert.True(map.IsJustPressed(InputAction.Double, withShift, previous));
        Assert.False(map.IsJustPressed(InputAction.Double, withoutShift, new KeyboardState()));
    }
}
