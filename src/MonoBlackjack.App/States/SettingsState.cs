using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Core;
using MonoBlackjack.Core.Ports;

namespace MonoBlackjack;

internal sealed class SettingsState : State
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly int _profileId;
    private readonly Texture2D _buttonTexture;
    private readonly SpriteFont _font;
    private readonly List<SettingRow> _rows = [];
    private readonly List<Button> _buttons = [];
    private Button _saveButton = null!;
    private Button _backButton = null!;
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

        _buttonTexture = _content.Load<Texture2D>("Controls/Button");
        _font = _content.Load<SpriteFont>("Fonts/MyFont");

        var persisted = _settingsRepository.LoadSettings(_profileId);
        if (persisted.Count > 0)
            GameConfig.ApplySettings(persisted);

        InitializeRows();
        BuildLayout();
    }

    public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        spriteBatch.Begin();

        DrawTitle(spriteBatch);

        foreach (var row in _rows)
        {
            spriteBatch.DrawString(_font, row.Label, row.LabelPosition, Color.White, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, row.SelectedLabel, row.ValuePosition, Color.Gold, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
        }

        foreach (var button in _buttons)
            button.Draw(gameTime, spriteBatch);

        if (!string.IsNullOrEmpty(_statusMessage))
        {
            var vp = _graphicsDevice.Viewport;
            var size = _font.MeasureString(_statusMessage) * 0.8f;
            var pos = new Vector2(vp.Width / 2f - size.X / 2f, vp.Height - size.Y - 12f);
            spriteBatch.DrawString(_font, _statusMessage, pos, Color.LightGreen, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
        }

        spriteBatch.End();
    }

    public override void Update(GameTime gameTime)
    {
        foreach (var button in _buttons)
            button.Update(gameTime);

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
        BuildLayout();
    }

    private void DrawTitle(SpriteBatch spriteBatch)
    {
        const string title = "Settings";
        const string subtitle = "Casino Rule Variations";
        var vp = _graphicsDevice.Viewport;

        var titleScale = 1.1f;
        var titleSize = _font.MeasureString(title) * titleScale;
        var subtitleScale = 0.7f;
        var subtitleSize = _font.MeasureString(subtitle) * subtitleScale;

        spriteBatch.DrawString(
            _font,
            title,
            new Vector2(vp.Width / 2f - titleSize.X / 2f, vp.Height * 0.045f),
            Color.White,
            0f,
            Vector2.Zero,
            titleScale,
            SpriteEffects.None,
            0f);

        spriteBatch.DrawString(
            _font,
            subtitle,
            new Vector2(vp.Width / 2f - subtitleSize.X / 2f, vp.Height * 0.095f),
            Color.LightGray,
            0f,
            Vector2.Zero,
            subtitleScale,
            SpriteEffects.None,
            0f);
    }

    private void InitializeRows()
    {
        _rows.Clear();
        var current = GameConfig.ToSettingsDictionary();

        AddRow(
            GameConfig.SettingDealerHitsSoft17,
            "Dealer Soft 17",
            [new SettingChoice("Stand (S17)", "False"), new SettingChoice("Hit (H17)", "True")],
            current);
        AddRow(
            GameConfig.SettingBlackjackPayout,
            "Blackjack Payout",
            [new SettingChoice("3:2", "3:2"), new SettingChoice("6:5", "6:5")],
            current);
        AddRow(
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
            GameConfig.SettingSurrenderRule,
            "Surrender",
            [
                new SettingChoice("None", "none"),
                new SettingChoice("Late", "late"),
                new SettingChoice("Early", "early")
            ],
            current);
        AddRow(
            GameConfig.SettingDoubleAfterSplit,
            "Double After Split",
            [new SettingChoice("Enabled", "True"), new SettingChoice("Disabled", "False")],
            current);
        AddRow(
            GameConfig.SettingResplitAces,
            "Resplit Aces",
            [new SettingChoice("Enabled", "True"), new SettingChoice("Disabled", "False")],
            current);
        AddRow(
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
            GameConfig.SettingDoubleDownRestriction,
            "Double Restriction",
            [
                new SettingChoice("Any 2 Cards", DoubleDownRestriction.AnyTwoCards.ToString()),
                new SettingChoice("9-11 Only", DoubleDownRestriction.NineToEleven.ToString()),
                new SettingChoice("10-11 Only", DoubleDownRestriction.TenToEleven.ToString())
            ],
            current);
        AddRow(
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
    }

    private void AddRow(
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

        _rows.Add(new SettingRow(key, label, choices.ToList(), selectedIndex));
    }

    private void BuildLayout()
    {
        _buttons.Clear();

        var vp = _graphicsDevice.Viewport;
        float rowTop = vp.Height * 0.17f;
        float rowSpacing = Math.Clamp(vp.Height * 0.06f, 38f, 56f);

        float labelX = vp.Width * 0.12f;
        float valueX = vp.Width * 0.52f;
        float prevX = vp.Width * 0.70f;
        float nextX = vp.Width * 0.78f;

        var navButtonSize = new Vector2(
            Math.Clamp(vp.Width * 0.055f, 58f, 90f),
            Math.Clamp(vp.Height * 0.048f, 30f, 44f));

        foreach (var row in _rows)
        {
            int rowIndex = _rows.IndexOf(row);
            float y = rowTop + rowSpacing * rowIndex;

            row.LabelPosition = new Vector2(labelX, y);
            row.ValuePosition = new Vector2(valueX, y);

            var previous = new Button(_buttonTexture, _font)
            {
                Text = "<",
                PenColor = Color.Black,
                Size = navButtonSize,
                Position = new Vector2(prevX, y + navButtonSize.Y * 0.25f)
            };

            var next = new Button(_buttonTexture, _font)
            {
                Text = ">",
                PenColor = Color.Black,
                Size = navButtonSize,
                Position = new Vector2(nextX, y + navButtonSize.Y * 0.25f)
            };

            var capturedRow = row;
            previous.Click += (_, _) => ShiftRow(capturedRow, -1);
            next.Click += (_, _) => ShiftRow(capturedRow, 1);

            row.PreviousButton = previous;
            row.NextButton = next;

            _buttons.Add(previous);
            _buttons.Add(next);
        }

        var actionButtonSize = new Vector2(
            Math.Clamp(vp.Width * 0.16f, 150f, 260f),
            Math.Clamp(vp.Height * 0.065f, 38f, 56f));

        float actionY = vp.Height - actionButtonSize.Y * 1.4f;
        float centerX = vp.Width / 2f;
        float offset = actionButtonSize.X * 0.62f;

        _saveButton = new Button(_buttonTexture, _font)
        {
            Text = "Save",
            PenColor = Color.Black,
            Size = actionButtonSize,
            Position = new Vector2(centerX - offset, actionY)
        };
        _saveButton.Click += OnSaveClicked;

        _backButton = new Button(_buttonTexture, _font)
        {
            Text = "Back",
            PenColor = Color.Black,
            Size = actionButtonSize,
            Position = new Vector2(centerX + offset, actionY)
        };
        _backButton.Click += OnBackClicked;

        _buttons.Add(_saveButton);
        _buttons.Add(_backButton);
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
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _rows)
            settings[row.Key] = row.SelectedValue;

        GameConfig.ApplySettings(settings);
        _settingsRepository.SaveSettings(_profileId, settings);

        _statusMessage = "Settings saved";
        _statusSeconds = 2.0f;
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _game.ChangeState(new MenuState(_game, _graphicsDevice, _content));
    }

    private sealed record SettingChoice(string Label, string Value);

    private sealed class SettingRow
    {
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

        public SettingRow(string key, string label, List<SettingChoice> choices, int selectedIndex)
        {
            Key = key;
            Label = label;
            Choices = choices;
            SelectedIndex = selectedIndex;
        }
    }
}
