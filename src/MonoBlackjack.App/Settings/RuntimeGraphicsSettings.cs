using System.Globalization;
using Microsoft.Xna.Framework;
using MonoBlackjack.Core;

namespace MonoBlackjack;

internal readonly record struct RuntimeGraphicsSettings(
    Color BackgroundColor,
    float FontScaleMultiplier,
    string CardBackTheme)
{
    private static readonly Color TableGreen = new(14, 88, 28);
    private static readonly Color TableBlue = new(10, 42, 88);
    private static readonly Color TableRed = new(88, 24, 24);

    public static RuntimeGraphicsSettings Default => FromSettings(SettingsContract.GetDefaultSettings());

    public static RuntimeGraphicsSettings FromSettings(IReadOnlyDictionary<string, string> settings)
    {
        var merged = SettingsContract.MergeWithDefaults(settings);
        var background = ResolveBackgroundColor(merged[GameConfig.SettingGraphicsBackgroundColor]);
        var fontScale = ResolveFontScaleMultiplier(merged[GameConfig.SettingGraphicsFontScale]);
        var cardBackTheme = ResolveCardBackTheme(merged[GameConfig.SettingGraphicsCardBack]);
        return new RuntimeGraphicsSettings(background, fontScale, cardBackTheme);
    }

    internal static Color ResolveBackgroundColor(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "green" => TableGreen,
            "blue" => TableBlue,
            "red" => TableRed,
            _ => TableGreen
        };
    }

    internal static float ResolveFontScaleMultiplier(string value)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return 1.0f;

        return Math.Clamp(parsed, 0.75f, 1.35f);
    }

    internal static string ResolveCardBackTheme(string value)
    {
        if (string.Equals(value, "Blue", StringComparison.OrdinalIgnoreCase))
            return "Blue";

        if (string.Equals(value, "Red", StringComparison.OrdinalIgnoreCase))
            return "Red";

        return "Classic";
    }
}
