namespace MonoBlackjack.Core;

/// <summary>
/// Canonical settings contract for persistence and runtime loading.
/// Unsupported keys/values are rejected rather than interpreted.
/// </summary>
public static class SettingsContract
{
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    private static readonly string[] BooleanValues = ["True", "False"];
    private static readonly string[] BlackjackPayoutValues = ["3:2", "6:5"];
    private static readonly string[] NumberOfDeckValues = ["1", "2", "4", "6", "8"];
    private static readonly string[] SurrenderValues = ["none", "late", "early"];
    private static readonly string[] MaxSplitsValues = ["1", "2", "3", "4"];
    private static readonly string[] DoubleDownRestrictionValues =
    [
        nameof(DoubleDownRestriction.AnyTwoCards),
        nameof(DoubleDownRestriction.NineToEleven),
        nameof(DoubleDownRestriction.TenToEleven)
    ];
    private static readonly string[] PenetrationPercentValues = ["60", "65", "70", "75", "80", "85", "90"];

    private static readonly string[] KeybindBaseValues = BuildBaseKeybindValues();
    private static readonly string[] KeybindGameplayValues = BuildGameplayKeybindValues();

    private static readonly string[] KeybindHitValues = KeybindGameplayValues;
    private static readonly string[] KeybindStandValues = KeybindGameplayValues;
    private static readonly string[] KeybindDoubleValues = KeybindGameplayValues;
    private static readonly string[] KeybindSplitValues = KeybindGameplayValues;
    private static readonly string[] KeybindSurrenderValues = KeybindGameplayValues;
    private static readonly string[] KeybindPauseValues = KeybindBaseValues;
    private static readonly string[] KeybindBackValues = KeybindBaseValues;

    private static readonly string[] GraphicsBackgroundValues = ["Green", "Blue", "Red"];
    private static readonly string[] GraphicsFontScaleValues = ["0.9", "1.0", "1.2"];
    private static readonly string[] GraphicsCardBackValues = ["Classic", "Blue", "Red"];

    private static readonly Dictionary<string, string> DefaultValues = new(Comparer)
    {
        [GameConfig.SettingDealerHitsSoft17] = "False",
        [GameConfig.SettingBlackjackPayout] = "3:2",
        [GameConfig.SettingNumberOfDecks] = "6",
        [GameConfig.SettingSurrenderRule] = "none",
        [GameConfig.SettingDoubleAfterSplit] = "True",
        [GameConfig.SettingResplitAces] = "False",
        [GameConfig.SettingMaxSplits] = "3",
        [GameConfig.SettingDoubleDownRestriction] = nameof(DoubleDownRestriction.AnyTwoCards),
        [GameConfig.SettingPenetrationPercent] = "75",
        [GameConfig.SettingShowHandValues] = "True",
        [GameConfig.SettingKeybindHit] = "H",
        [GameConfig.SettingKeybindStand] = "S",
        [GameConfig.SettingKeybindDouble] = "D",
        [GameConfig.SettingKeybindSplit] = "P",
        [GameConfig.SettingKeybindSurrender] = "R",
        [GameConfig.SettingKeybindPause] = "Escape",
        [GameConfig.SettingKeybindBack] = "Escape",
        [GameConfig.SettingGraphicsBackgroundColor] = "Green",
        [GameConfig.SettingGraphicsFontScale] = "1.0",
        [GameConfig.SettingGraphicsCardBack] = "Classic"
    };

    private static readonly string[] KeybindKeys =
    [
        GameConfig.SettingKeybindHit,
        GameConfig.SettingKeybindStand,
        GameConfig.SettingKeybindDouble,
        GameConfig.SettingKeybindSplit,
        GameConfig.SettingKeybindSurrender,
        GameConfig.SettingKeybindPause,
        GameConfig.SettingKeybindBack
    ];

    public static IReadOnlyList<string> KeybindSettingKeys => KeybindKeys;

    public static IReadOnlyDictionary<string, string> GetDefaultSettings()
    {
        return new Dictionary<string, string>(DefaultValues, Comparer);
    }

    public static bool TryGetDefaultValue(string key, out string value)
    {
        return DefaultValues.TryGetValue(key, out value!);
    }

    public static bool IsSupportedKey(string key)
    {
        return DefaultValues.ContainsKey(key);
    }

    public static bool TryNormalize(string key, string? value, out string normalizedValue)
    {
        normalizedValue = string.Empty;

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = NormalizeValue(key, value);
        if (normalized is null)
            return false;

        normalizedValue = normalized;
        return true;
    }

    public static IReadOnlyDictionary<string, string> Sanitize(IReadOnlyDictionary<string, string> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var sanitized = new Dictionary<string, string>(Comparer);
        foreach (var entry in settings)
        {
            if (TryNormalize(entry.Key, entry.Value, out var normalized))
                sanitized[ResolveCanonicalKey(entry.Key)] = normalized;
        }

        return sanitized;
    }

    public static IReadOnlyDictionary<string, string> MergeWithDefaults(IReadOnlyDictionary<string, string> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var merged = new Dictionary<string, string>(DefaultValues, Comparer);
        foreach (var entry in Sanitize(settings))
            merged[entry.Key] = entry.Value;

        return merged;
    }

    private static string ResolveCanonicalKey(string key)
    {
        foreach (var canonicalKey in DefaultValues.Keys)
        {
            if (string.Equals(key, canonicalKey, StringComparison.OrdinalIgnoreCase))
                return canonicalKey;
        }

        return key;
    }

    private static string? NormalizeValue(string key, string value)
    {
        return key switch
        {
            GameConfig.SettingDealerHitsSoft17 => NormalizeBoolean(value),
            GameConfig.SettingDoubleAfterSplit => NormalizeBoolean(value),
            GameConfig.SettingResplitAces => NormalizeBoolean(value),
            GameConfig.SettingShowHandValues => NormalizeBoolean(value),
            GameConfig.SettingBlackjackPayout => NormalizeChoice(value, BlackjackPayoutValues),
            GameConfig.SettingNumberOfDecks => NormalizeChoice(value, NumberOfDeckValues),
            GameConfig.SettingSurrenderRule => NormalizeChoice(value, SurrenderValues),
            GameConfig.SettingMaxSplits => NormalizeChoice(value, MaxSplitsValues),
            GameConfig.SettingDoubleDownRestriction => NormalizeChoice(value, DoubleDownRestrictionValues),
            GameConfig.SettingPenetrationPercent => NormalizeChoice(value, PenetrationPercentValues),
            GameConfig.SettingKeybindHit => NormalizeChoice(value, KeybindHitValues),
            GameConfig.SettingKeybindStand => NormalizeChoice(value, KeybindStandValues),
            GameConfig.SettingKeybindDouble => NormalizeChoice(value, KeybindDoubleValues),
            GameConfig.SettingKeybindSplit => NormalizeChoice(value, KeybindSplitValues),
            GameConfig.SettingKeybindSurrender => NormalizeChoice(value, KeybindSurrenderValues),
            GameConfig.SettingKeybindPause => NormalizeChoice(value, KeybindPauseValues),
            GameConfig.SettingKeybindBack => NormalizeChoice(value, KeybindBackValues),
            GameConfig.SettingGraphicsBackgroundColor => NormalizeChoice(value, GraphicsBackgroundValues),
            GameConfig.SettingGraphicsFontScale => NormalizeChoice(value, GraphicsFontScaleValues),
            GameConfig.SettingGraphicsCardBack => NormalizeChoice(value, GraphicsCardBackValues),
            _ => null
        };
    }

    private static string? NormalizeBoolean(string value)
    {
        var canonical = NormalizeChoice(value, BooleanValues);
        if (canonical is not null)
            return canonical;

        if (bool.TryParse(value.Trim(), out var parsed))
            return parsed.ToString();

        return null;
    }

    private static string[] BuildBaseKeybindValues()
    {
        var values = new List<string>
        {
            "Space",
            "Enter",
            "Escape",
            "Back",
            "Tab",
            "Up",
            "Down",
            "Left",
            "Right",
            "Home",
            "End",
            "PageUp",
            "PageDown",
            "Insert",
            "Delete"
        };

        for (char c = 'A'; c <= 'Z'; c++)
            values.Add(c.ToString());

        for (int i = 0; i <= 9; i++)
            values.Add($"D{i}");

        for (int i = 0; i <= 9; i++)
            values.Add($"NumPad{i}");

        return [.. values];
    }

    private static string[] BuildGameplayKeybindValues()
    {
        var values = new List<string>(KeybindBaseValues);
        for (char c = 'A'; c <= 'Z'; c++)
            values.Add($"Shift+{c}");
        return [.. values];
    }

    private static string? NormalizeChoice(string value, IReadOnlyList<string> allowedValues)
    {
        var trimmed = value.Trim();
        for (int i = 0; i < allowedValues.Count; i++)
        {
            var allowed = allowedValues[i];
            if (string.Equals(trimmed, allowed, StringComparison.OrdinalIgnoreCase))
                return allowed;
        }

        return null;
    }
}
