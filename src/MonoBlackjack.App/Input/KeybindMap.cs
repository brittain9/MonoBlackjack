using Microsoft.Xna.Framework.Input;
using MonoBlackjack.Core;

namespace MonoBlackjack;

internal enum InputAction
{
    Hit,
    Stand,
    Double,
    Split,
    Surrender,
    Pause,
    Back
}

internal sealed class KeybindMap
{
    private static readonly InputAction[] ActionOrder =
    [
        InputAction.Hit,
        InputAction.Stand,
        InputAction.Double,
        InputAction.Split,
        InputAction.Surrender,
        InputAction.Pause,
        InputAction.Back
    ];

    private readonly Dictionary<InputAction, InputBinding> _bindings;
    private readonly Dictionary<InputAction, string> _labels;

    private KeybindMap(
        Dictionary<InputAction, InputBinding> bindings,
        Dictionary<InputAction, string> labels)
    {
        _bindings = bindings;
        _labels = labels;
    }

    public static KeybindMap FromSettings(IReadOnlyDictionary<string, string> settings)
    {
        var merged = SettingsContract.MergeWithDefaults(settings);
        var bindings = new Dictionary<InputAction, InputBinding>();
        var labels = new Dictionary<InputAction, string>();

        foreach (var action in ActionOrder)
        {
            var settingKey = GetSettingKey(action);
            var options = GetAllowedValues(action);

            // Start from persisted value, then fall back through allowed values if parsing fails.
            var preferred = merged.TryGetValue(settingKey, out var loadedValue)
                ? loadedValue
                : options[0];

            var chosen = ChooseBinding(preferred, options);
            bindings[action] = chosen.Binding;
            labels[action] = chosen.Label;
        }

        return new KeybindMap(bindings, labels);
    }

    public InputBinding GetBinding(InputAction action)
    {
        return _bindings[action];
    }

    public string GetLabel(InputAction action)
    {
        return _labels[action];
    }

    public bool IsJustPressed(InputAction action, KeyboardState current, KeyboardState previous)
    {
        var binding = _bindings[action];
        return IsBindingJustPressed(binding, current, previous);
    }

    private static (InputBinding Binding, string Label) ChooseBinding(
        string preferredValue,
        IReadOnlyList<string> allowedValues)
    {
        if (InputBinding.TryParse(preferredValue, out var preferredBinding))
            return (preferredBinding, preferredValue);

        for (int i = 0; i < allowedValues.Count; i++)
        {
            var label = allowedValues[i];
            if (!InputBinding.TryParse(label, out var binding))
                continue;

            return (binding, label);
        }

        return (new InputBinding(Keys.None), "None");
    }

    private static bool IsBindingJustPressed(InputBinding binding, KeyboardState current, KeyboardState previous)
    {
        if (binding.Key == Keys.None)
            return false;

        if (!current.IsKeyDown(binding.Key) || previous.IsKeyDown(binding.Key))
            return false;

        bool shiftDown = IsShiftDown(current);
        if (binding.RequiresShift)
            return shiftDown;

        return !shiftDown;
    }

    private static bool IsShiftDown(KeyboardState state)
    {
        return state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift);
    }

    private static string GetSettingKey(InputAction action)
    {
        return action switch
        {
            InputAction.Hit => GameConfig.SettingKeybindHit,
            InputAction.Stand => GameConfig.SettingKeybindStand,
            InputAction.Double => GameConfig.SettingKeybindDouble,
            InputAction.Split => GameConfig.SettingKeybindSplit,
            InputAction.Surrender => GameConfig.SettingKeybindSurrender,
            InputAction.Pause => GameConfig.SettingKeybindPause,
            InputAction.Back => GameConfig.SettingKeybindBack,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown input action.")
        };
    }

    private static IReadOnlyList<string> GetAllowedValues(InputAction action)
    {
        return action switch
        {
            InputAction.Hit => ["H", "Space"],
            InputAction.Stand => ["S", "Enter"],
            InputAction.Double => ["D", "Shift+D"],
            InputAction.Split => ["P", "Shift+S"],
            InputAction.Surrender => ["R", "Shift+R"],
            InputAction.Pause => ["Escape", "P"],
            InputAction.Back => ["Escape", "Back"],
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown input action.")
        };
    }
}
