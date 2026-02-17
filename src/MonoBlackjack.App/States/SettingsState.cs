using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Core;
using MonoBlackjack.Core.Ports;

namespace MonoBlackjack;

internal sealed class SettingsState : State
{
    private enum SettingsSection
    {
        Rules,
        Keybinds,
        Graphics,
        Assistance
    }

    private const string SettingKeybindHit = "KeybindHit";
    private const string SettingKeybindStand = "KeybindStand";
    private const string SettingKeybindDouble = "KeybindDouble";
    private const string SettingKeybindSplit = "KeybindSplit";
    private const string SettingKeybindSurrender = "KeybindSurrender";
    private const string SettingKeybindPause = "KeybindPause";
    private const string SettingGraphicsBackground = "GraphicsBackgroundColor";
    private const string SettingGraphicsFontScale = "GraphicsFontScale";
    private const string SettingGraphicsCardBack = "GraphicsCardBack";

    private readonly ISettingsRepository _settingsRepository;
    private readonly int _profileId;
    private readonly Texture2D _buttonTexture;
    private readonly Texture2D _pixelTexture;
    private readonly SpriteFont _font;
    private readonly List<SettingRow> _rows = [];
    private readonly Dictionary<string, string> _loadedSettings = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Button> _tabButtons = [];
    private readonly List<Button> _actionButtons = [];

    private readonly Button _rulesTab;
    private readonly Button _keybindsTab;
    private readonly Button _graphicsTab;
    private readonly Button _assistanceTab;
    private readonly Button _saveButton;
    private readonly Button _backButton;

    private SettingsSection _activeSection = SettingsSection.Rules;
    private string _statusMessage = string.Empty;
    private float _statusSeconds;

    public SettingsState(
        BlackjackGame game,
        GraphicsDevice graphicsDevice,
        ContentManager content,
        ISettingsRepository settingsRepository,
        int profileId)
        : base(game, graphicsDevice, content)
    {
        _settingsRepository = settingsRepository;
        _profileId = profileId;

        var persisted = _settingsRepository.LoadSettings(_profileId);
        foreach (var kvp in persisted)
            _loadedSettings[kvp.Key] = kvp.Value;

        if (persisted.Count > 0)
        {
            var rules = GameRules.FromSettings(persisted);
            _game.UpdateRules(rules);
        }

        _buttonTexture = _content.Load<Texture2D>("Controls/Button");
        _font = _content.Load<SpriteFont>("Fonts/MyFont");
        _pixelTexture = game.PixelTexture;

        _rulesTab = CreateTabButton("Rules", SettingsSection.Rules);
        _keybindsTab = CreateTabButton("Keybinds", SettingsSection.Keybinds);
        _graphicsTab = CreateTabButton("Graphics", SettingsSection.Graphics);
        _assistanceTab = CreateTabButton("Assistance", SettingsSection.Assistance);
        _tabButtons.AddRange([_rulesTab, _keybindsTab, _graphicsTab, _assistanceTab]);

        _saveButton = new Button(_buttonTexture, _font) { Text = "Save", PenColor = Color.Black };
        _saveButton.Click += OnSaveClicked;

        _backButton = new Button(_buttonTexture, _font) { Text = "Back", PenColor = Color.Black };
        _backButton.Click += OnBackClicked;
        _actionButtons.AddRange([_saveButton, _backButton]);

        InitializeRows();
        UpdateLayout();
    }

    public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        spriteBatch.Begin();

        DrawTitle(spriteBatch);
        DrawTabs(gameTime, spriteBatch);

        var rowScale = GetResponsiveScale(0.85f);
        foreach (var row in GetVisibleRows())
        {
            spriteBatch.DrawString(_font, row.Label, row.LabelPosition, Color.White, 0f, Vector2.Zero, rowScale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, row.SelectedLabel, row.ValuePosition, Color.Gold, 0f, Vector2.Zero, rowScale, SpriteEffects.None, 0f);
            row.PreviousButton.Draw(gameTime, spriteBatch);
            row.NextButton.Draw(gameTime, spriteBatch);
        }

        foreach (var button in _actionButtons)
            button.Draw(gameTime, spriteBatch);

        if (!string.IsNullOrEmpty(_statusMessage))
        {
            var vp = _graphicsDevice.Viewport;
            var statusScale = GetResponsiveScale(0.8f);
            var size = _font.MeasureString(_statusMessage) * statusScale;
            var pos = new Vector2(vp.Width / 2f - size.X / 2f, vp.Height - size.Y - 12f);
            spriteBatch.DrawString(_font, _statusMessage, pos, Color.LightGreen, 0f, Vector2.Zero, statusScale, SpriteEffects.None, 0f);
        }

        spriteBatch.End();
    }

    public override void Update(GameTime gameTime)
    {
        foreach (var button in _tabButtons)
            button.Update(gameTime);

        foreach (var button in _actionButtons)
            button.Update(gameTime);

        foreach (var row in GetVisibleRows())
        {
            row.PreviousButton.Update(gameTime);
            row.NextButton.Update(gameTime);
        }

        if (_statusSeconds > 0f)
        {
            _statusSeconds -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_statusSeconds <= 0f)
                _statusMessage = string.Empty;
        }
    }

    public override void PostUpdate(GameTime gameTime) { }

    public override void HandleResize(Rectangle vp)
    {
        UpdateLayout();
    }

    private Button CreateTabButton(string text, SettingsSection section)
    {
        var button = new Button(_buttonTexture, _font) { Text = text, PenColor = Color.Black };
        button.Click += (_, _) =>
        {
            _activeSection = section;
            _statusMessage = string.Empty;
        };
        return button;
    }

    private void DrawTitle(SpriteBatch spriteBatch)
    {
        const string title = "Settings";
        var subtitle = _activeSection switch
        {
            SettingsSection.Rules => "Casino Rules",
            SettingsSection.Keybinds => "Keyboard Controls (Planned)",
            SettingsSection.Graphics => "Visual Options (Planned)",
            SettingsSection.Assistance => "Gameplay Assistance",
            _ => string.Empty
        };

        var vp = _graphicsDevice.Viewport;
        var titleScale = GetResponsiveScale(1.1f);
        var titleSize = _font.MeasureString(title) * titleScale;
        var subtitleScale = GetResponsiveScale(0.7f);
        var subtitleSize = _font.MeasureString(subtitle) * subtitleScale;

        spriteBatch.DrawString(
            _font,
            title,
            new Vector2(vp.Width / 2f - titleSize.X / 2f, vp.Height * 0.04f),
            Color.White,
            0f,
            Vector2.Zero,
            titleScale,
            SpriteEffects.None,
            0f);

        spriteBatch.DrawString(
            _font,
            subtitle,
            new Vector2(vp.Width / 2f - subtitleSize.X / 2f, vp.Height * 0.09f),
            Color.LightGray,
            0f,
            Vector2.Zero,
            subtitleScale,
            SpriteEffects.None,
            0f);
    }

    private void DrawTabs(GameTime gameTime, SpriteBatch spriteBatch)
    {
        foreach (var button in _tabButtons)
            button.Draw(gameTime, spriteBatch);

        var active = _activeSection switch
        {
            SettingsSection.Rules => _rulesTab,
            SettingsSection.Keybinds => _keybindsTab,
            SettingsSection.Graphics => _graphicsTab,
            SettingsSection.Assistance => _assistanceTab,
            _ => _rulesTab
        };

        var rect = active.DestRect;
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Bottom + 2, rect.Width, 3), Color.Gold);
    }

    private void InitializeRows()
    {
        _rows.Clear();
        var current = BuildCurrentSettings();

        AddRow(
            SettingsSection.Rules,
            GameConfig.SettingDealerHitsSoft17,
            "Dealer Soft 17",
            [new SettingChoice("Stand (S17)", "False"), new SettingChoice("Hit (H17)", "True")],
            current);
        AddRow(
            SettingsSection.Rules,
            GameConfig.SettingBlackjackPayout,
            "Blackjack Payout",
            [new SettingChoice("3:2", "3:2"), new SettingChoice("6:5", "6:5")],
            current);
        AddRow(
            SettingsSection.Rules,
            GameConfig.SettingNumberOfDecks,
            "Deck Count",
            [
                new SettingChoice("1", "1"),
                new SettingChoice("2", "2"),
                new SettingChoice("4", "4"),
                new SettingChoice("6", "6"),
                new SettingChoice("8", "8")
            ],
            current);
        AddRow(
            SettingsSection.Rules,
            GameConfig.SettingSurrenderRule,
            "Surrender",
            [
                new SettingChoice("None", "none"),
                new SettingChoice("Late", "late"),
                new SettingChoice("Early", "early")
            ],
            current);
        AddRow(
            SettingsSection.Rules,
            GameConfig.SettingDoubleAfterSplit,
            "Double After Split",
            [new SettingChoice("Enabled", "True"), new SettingChoice("Disabled", "False")],
            current);
        AddRow(
            SettingsSection.Rules,
            GameConfig.SettingResplitAces,
            "Resplit Aces",
            [new SettingChoice("Enabled", "True"), new SettingChoice("Disabled", "False")],
            current);
        AddRow(
            SettingsSection.Rules,
            GameConfig.SettingMaxSplits,
            "Max Splits",
            [
                new SettingChoice("1", "1"),
                new SettingChoice("2", "2"),
                new SettingChoice("3", "3"),
                new SettingChoice("4", "4")
            ],
            current);
        AddRow(
            SettingsSection.Rules,
            GameConfig.SettingDoubleDownRestriction,
            "Double Restriction",
            [
                new SettingChoice("Any 2 Cards", DoubleDownRestriction.AnyTwoCards.ToString()),
                new SettingChoice("9-11 Only", DoubleDownRestriction.NineToEleven.ToString()),
                new SettingChoice("10-11 Only", DoubleDownRestriction.TenToEleven.ToString())
            ],
            current);
        AddRow(
            SettingsSection.Rules,
            GameConfig.SettingPenetrationPercent,
            "Shoe Penetration",
            [
                new SettingChoice("60%", "60"),
                new SettingChoice("65%", "65"),
                new SettingChoice("70%", "70"),
                new SettingChoice("75%", "75"),
                new SettingChoice("80%", "80"),
                new SettingChoice("85%", "85"),
                new SettingChoice("90%", "90")
            ],
            current);

        AddRow(
            SettingsSection.Keybinds,
            SettingKeybindHit,
            "Hit Action",
            [new SettingChoice("H", "H"), new SettingChoice("Space", "Space")],
            current);
        AddRow(
            SettingsSection.Keybinds,
            SettingKeybindStand,
            "Stand Action",
            [new SettingChoice("S", "S"), new SettingChoice("Enter", "Enter")],
            current);
        AddRow(
            SettingsSection.Keybinds,
            SettingKeybindDouble,
            "Double Action",
            [new SettingChoice("D", "D"), new SettingChoice("Shift+D", "Shift+D")],
            current);
        AddRow(
            SettingsSection.Keybinds,
            SettingKeybindSplit,
            "Split Action",
            [new SettingChoice("P", "P"), new SettingChoice("Shift+S", "Shift+S")],
            current);
        AddRow(
            SettingsSection.Keybinds,
            SettingKeybindSurrender,
            "Surrender Action",
            [new SettingChoice("R", "R"), new SettingChoice("Shift+R", "Shift+R")],
            current);
        AddRow(
            SettingsSection.Keybinds,
            SettingKeybindPause,
            "Pause Action",
            [new SettingChoice("Escape", "Escape"), new SettingChoice("P", "P")],
            current);

        AddRow(
            SettingsSection.Graphics,
            SettingGraphicsBackground,
            "Background Color",
            [new SettingChoice("Green", "Green"), new SettingChoice("Blue", "Blue"), new SettingChoice("Red", "Red")],
            current);
        AddRow(
            SettingsSection.Graphics,
            SettingGraphicsFontScale,
            "Font Scale",
            [new SettingChoice("0.9x", "0.9"), new SettingChoice("1.0x", "1.0"), new SettingChoice("1.2x", "1.2")],
            current);
        AddRow(
            SettingsSection.Graphics,
            SettingGraphicsCardBack,
            "Card Back",
            [new SettingChoice("Classic", "Classic"), new SettingChoice("Blue", "Blue"), new SettingChoice("Red", "Red")],
            current);

        AddRow(
            SettingsSection.Assistance,
            GameConfig.SettingShowHandValues,
            "Show Hand Values",
            [new SettingChoice("Yes", "True"), new SettingChoice("No", "False")],
            current);
        AddRow(
            SettingsSection.Assistance,
            GameConfig.SettingShowRecommendations,
            "Show Recommendations",
            [new SettingChoice("No", "False"), new SettingChoice("Yes", "True")],
            current);
    }

    private Dictionary<string, string> BuildCurrentSettings()
    {
        var current = new Dictionary<string, string>(_game.CurrentRules.ToSettingsDictionary(), StringComparer.OrdinalIgnoreCase);
        SetLoadedOrDefault(current, SettingKeybindHit, "H");
        SetLoadedOrDefault(current, SettingKeybindStand, "S");
        SetLoadedOrDefault(current, SettingKeybindDouble, "D");
        SetLoadedOrDefault(current, SettingKeybindSplit, "P");
        SetLoadedOrDefault(current, SettingKeybindSurrender, "R");
        SetLoadedOrDefault(current, SettingKeybindPause, "Escape");

        SetLoadedOrDefault(current, SettingGraphicsBackground, "Green");
        SetLoadedOrDefault(current, SettingGraphicsFontScale, "1.0");
        SetLoadedOrDefault(current, SettingGraphicsCardBack, "Classic");

        SetLoadedOrDefault(current, GameConfig.SettingShowHandValues, "True");
        SetLoadedOrDefault(current, GameConfig.SettingShowRecommendations, "False");

        return current;
    }

    private void SetLoadedOrDefault(Dictionary<string, string> settings, string key, string defaultValue)
    {
        if (_loadedSettings.TryGetValue(key, out var value))
        {
            settings[key] = value;
            return;
        }

        settings[key] = defaultValue;
    }

    private void AddRow(
        SettingsSection section,
        string key,
        string label,
        IReadOnlyList<SettingChoice> choices,
        IReadOnlyDictionary<string, string> current)
    {
        int selectedIndex = 0;
        if (current.TryGetValue(key, out var value))
        {
            for (int i = 0; i < choices.Count; i++)
            {
                if (string.Equals(choices[i].Value, value, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        var row = new SettingRow(section, key, label, choices.ToList(), selectedIndex);
        row.PreviousButton = new Button(_buttonTexture, _font) { Text = "<", PenColor = Color.Black };
        row.NextButton = new Button(_buttonTexture, _font) { Text = ">", PenColor = Color.Black };
        row.PreviousButton.Click += (_, _) => ShiftRow(row, -1);
        row.NextButton.Click += (_, _) => ShiftRow(row, 1);
        _rows.Add(row);
    }

    private IEnumerable<SettingRow> GetVisibleRows()
    {
        foreach (var row in _rows)
        {
            if (row.Section == _activeSection)
                yield return row;
        }
    }

    private void UpdateLayout()
    {
        var vp = _graphicsDevice.Viewport;

        var tabSize = new Vector2(
            Math.Clamp(vp.Width * 0.14f, 120f, 200f),
            Math.Clamp(vp.Height * 0.052f, 32f, 46f));
        float tabGap = tabSize.X * 0.10f;
        float totalTabWidth = tabSize.X * 4 + tabGap * 3;
        float firstTabCenterX = vp.Width / 2f - totalTabWidth / 2f + tabSize.X / 2f;
        float tabY = vp.Height * 0.165f;

        _rulesTab.Size = tabSize;
        _rulesTab.Position = new Vector2(firstTabCenterX, tabY);

        _keybindsTab.Size = tabSize;
        _keybindsTab.Position = new Vector2(firstTabCenterX + tabSize.X + tabGap, tabY);

        _graphicsTab.Size = tabSize;
        _graphicsTab.Position = new Vector2(firstTabCenterX + (tabSize.X + tabGap) * 2f, tabY);

        _assistanceTab.Size = tabSize;
        _assistanceTab.Position = new Vector2(firstTabCenterX + (tabSize.X + tabGap) * 3f, tabY);

        float rowTop = vp.Height * 0.255f;
        float rowSpacing = Math.Clamp(vp.Height * 0.06f, 36f, 56f);
        float labelX = vp.Width * 0.10f;
        float valueX = vp.Width * 0.50f;
        float prevX = vp.Width * 0.72f;
        float nextX = vp.Width * 0.80f;
        var navButtonSize = new Vector2(
            Math.Clamp(vp.Width * 0.055f, 58f, 90f),
            Math.Clamp(vp.Height * 0.048f, 30f, 44f));

        var sectionRowIndices = new Dictionary<SettingsSection, int>
        {
            [SettingsSection.Rules] = 0,
            [SettingsSection.Keybinds] = 0,
            [SettingsSection.Graphics] = 0,
            [SettingsSection.Assistance] = 0
        };

        foreach (var row in _rows)
        {
            int sectionIndex = sectionRowIndices[row.Section];
            sectionRowIndices[row.Section] = sectionIndex + 1;
            float y = rowTop + rowSpacing * sectionIndex;

            row.LabelPosition = new Vector2(labelX, y);
            row.ValuePosition = new Vector2(valueX, y);
            row.PreviousButton.Size = navButtonSize;
            row.PreviousButton.Position = new Vector2(prevX, y + navButtonSize.Y * 0.25f);
            row.NextButton.Size = navButtonSize;
            row.NextButton.Position = new Vector2(nextX, y + navButtonSize.Y * 0.25f);
        }

        var actionButtonSize = new Vector2(
            Math.Clamp(vp.Width * 0.16f, 150f, 260f),
            Math.Clamp(vp.Height * 0.065f, 38f, 56f));
        float actionY = vp.Height - actionButtonSize.Y * 1.4f;
        float centerX = vp.Width / 2f;
        float offset = actionButtonSize.X * 0.62f;

        _saveButton.Size = actionButtonSize;
        _saveButton.Position = new Vector2(centerX - offset, actionY);

        _backButton.Size = actionButtonSize;
        _backButton.Position = new Vector2(centerX + offset, actionY);
    }

    private void ShiftRow(SettingRow row, int direction)
    {
        int count = row.Choices.Count;
        int next = row.SelectedIndex + direction;
        if (next < 0)
            next = count - 1;
        else if (next >= count)
            next = 0;

        row.SelectedIndex = next;
        _statusMessage = string.Empty;
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var settings = BuildSavedSettings(GetSelectedSettings());

        var newRules = GameRules.FromSettings(settings);
        _game.UpdateRules(newRules);
        _settingsRepository.SaveSettings(_profileId, settings);

        _loadedSettings.Clear();
        foreach (var kvp in settings)
            _loadedSettings[kvp.Key] = kvp.Value;

        _statusMessage = "Settings saved";
        _statusSeconds = 2.0f;
    }

    internal static Dictionary<string, string> BuildSavedSettings(IReadOnlyDictionary<string, string> selectedSettings)
    {
        return new Dictionary<string, string>(selectedSettings, StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, string> GetSelectedSettings()
    {
        var selected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _rows)
            selected[row.Key] = row.SelectedValue;
        return selected;
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _game.GoBack();
    }

    private sealed record SettingChoice(string Label, string Value);

    private sealed class SettingRow
    {
        public SettingsSection Section { get; }
        public string Key { get; }
        public string Label { get; }
        public List<SettingChoice> Choices { get; }
        public int SelectedIndex { get; set; }
        public Vector2 LabelPosition { get; set; }
        public Vector2 ValuePosition { get; set; }
        public Button PreviousButton { get; set; } = null!;
        public Button NextButton { get; set; } = null!;
        public string SelectedValue => Choices[SelectedIndex].Value;
        public string SelectedLabel => Choices[SelectedIndex].Label;

        public SettingRow(SettingsSection section, string key, string label, List<SettingChoice> choices, int selectedIndex)
        {
            Section = section;
            Key = key;
            Label = label;
            Choices = choices;
            SelectedIndex = selectedIndex;
        }
    }
}
