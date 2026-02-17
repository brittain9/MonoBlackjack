using Microsoft.Xna.Framework.Input;

namespace MonoBlackjack;

internal readonly record struct InputBinding(Keys Key, bool RequiresShift = false)
{
    public static bool TryParse(string? rawValue, out InputBinding binding)
    {
        binding = default;

        if (string.IsNullOrWhiteSpace(rawValue))
            return false;

        var token = rawValue.Trim();
        bool requiresShift = false;

        const string shiftPrefix = "Shift+";
        if (token.StartsWith(shiftPrefix, StringComparison.OrdinalIgnoreCase))
        {
            requiresShift = true;
            token = token[shiftPrefix.Length..].Trim();
            if (token.Length == 0)
                return false;
        }

        if (!TryParseKey(token, out var key))
            return false;

        binding = new InputBinding(key, requiresShift);
        return true;
    }

    private static bool TryParseKey(string token, out Keys key)
    {
        key = default;

        if (token.Equals("Backspace", StringComparison.OrdinalIgnoreCase))
        {
            key = Keys.Back;
            return true;
        }

        return Enum.TryParse(token, ignoreCase: true, out key);
    }
}
