using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoBlackjack.Core.Ports;

namespace MonoBlackjack;

internal sealed class StatsState : State
{
    private readonly IStatsRepository _statsRepository;
    private readonly int _profileId;
    private readonly Texture2D _buttonTexture;
    private readonly Texture2D _pixelTexture;
    private readonly SpriteFont _font;
    private readonly List<Button> _buttons = [];
    private readonly RasterizerState _scissorRasterizer = new() { ScissorTestEnable = true };
    private readonly StatsOverviewPanelRenderer _overviewRenderer;
    private readonly StatsAnalysisPanelRenderer _analysisRenderer;

    private Button _overviewTab = null!;
    private Button _analysisTab = null!;
    private Button _backButton = null!;
    private Button _matrixHardButton = null!;
    private Button _matrixSoftButton = null!;
    private Button _matrixPairsButton = null!;

    private StatsTab _activeTab = StatsTab.Overview;
    private StatsMatrixMode _matrixMode = StatsMatrixMode.Hard;
    private KeybindMap _keybinds = null!;

    private float _scrollOffset;
    private float _maxScroll;
    private int _previousScrollValue;

    private OverviewStats _overview = null!;
    private IReadOnlyList<BankrollPoint> _bankrollHistory = [];
    private IReadOnlyList<DealerBustStat> _dealerBusts = [];
    private IReadOnlyList<HandValueOutcome> _handValueOutcomes = [];
    private IReadOnlyList<StrategyCell> _strategyMatrix = [];
    private IReadOnlyList<CardFrequency> _cardDistribution = [];

    public StatsState(
        BlackjackGame game,
        GraphicsDevice graphicsDevice,
        ContentManager content,
        IStatsRepository statsRepository,
        int profileId)
        : base(game, graphicsDevice, content)
    {
        _statsRepository = statsRepository;
        _profileId = profileId;

        _buttonTexture = _content.Load<Texture2D>("Controls/Button");
        _font = _content.Load<SpriteFont>("Fonts/MyFont");
        _pixelTexture = game.PixelTexture;

        _overviewRenderer = new StatsOverviewPanelRenderer(_font, _pixelTexture, _graphicsDevice, GetResponsiveScale);
        var matrixRenderer = new StatsMatrixRenderer(_font, _pixelTexture, _graphicsDevice, GetResponsiveScale);
        _analysisRenderer = new StatsAnalysisPanelRenderer(_font, _pixelTexture, _graphicsDevice, GetResponsiveScale, matrixRenderer);

        _previousScrollValue = Mouse.GetState().ScrollWheelValue;

        _overviewTab = new Button(_buttonTexture, _font) { Text = "Overview", PenColor = Color.Black };
        _overviewTab.Click += (_, _) => { _activeTab = StatsTab.Overview; _scrollOffset = 0; };

        _analysisTab = new Button(_buttonTexture, _font) { Text = "Analysis", PenColor = Color.Black };
        _analysisTab.Click += (_, _) => { _activeTab = StatsTab.Analysis; _scrollOffset = 0; };

        _matrixHardButton = new Button(_buttonTexture, _font) { Text = "Hard", PenColor = Color.Black };
        _matrixHardButton.Click += (_, _) => { _matrixMode = StatsMatrixMode.Hard; LoadStrategyMatrix(); };

        _matrixSoftButton = new Button(_buttonTexture, _font) { Text = "Soft", PenColor = Color.Black };
        _matrixSoftButton.Click += (_, _) => { _matrixMode = StatsMatrixMode.Soft; LoadStrategyMatrix(); };

        _matrixPairsButton = new Button(_buttonTexture, _font) { Text = "Pairs", PenColor = Color.Black };
        _matrixPairsButton.Click += (_, _) => { _matrixMode = StatsMatrixMode.Pairs; LoadStrategyMatrix(); };

        _backButton = new Button(_buttonTexture, _font) { Text = "Back", PenColor = Color.Black };
        _backButton.Click += (_, _) => _game.GoBack();

        _buttons.AddRange([_overviewTab, _analysisTab, _matrixHardButton, _matrixSoftButton, _matrixPairsButton, _backButton]);

        ReloadKeybinds();
        LoadData();
        UpdateLayout();
    }

    public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        var vp = _graphicsDevice.Viewport;

        spriteBatch.Begin();
        DrawTitle(spriteBatch);
        DrawTabButtons(gameTime, spriteBatch);
        spriteBatch.End();

        int clipTop = (int)(vp.Height * 0.12f);
        int clipBottom = (int)(vp.Height * 0.90f);
        var savedScissor = _graphicsDevice.ScissorRectangle;
        _graphicsDevice.ScissorRectangle = new Rectangle(0, clipTop, vp.Width, clipBottom - clipTop);

        spriteBatch.Begin(rasterizerState: _scissorRasterizer);
        if (_activeTab == StatsTab.Overview)
        {
            _maxScroll = 0f;
            _overviewRenderer.Draw(spriteBatch, _overview, _bankrollHistory);
        }
        else
        {
            _maxScroll = _analysisRenderer.Draw(
                gameTime,
                spriteBatch,
                _overview,
                _dealerBusts,
                _handValueOutcomes,
                _strategyMatrix,
                _cardDistribution,
                _matrixHardButton,
                _matrixSoftButton,
                _matrixPairsButton,
                _matrixMode,
                _scrollOffset);
        }

        spriteBatch.End();
        _graphicsDevice.ScissorRectangle = savedScissor;

        spriteBatch.Begin();
        _backButton.Draw(gameTime, spriteBatch);
        spriteBatch.End();
    }

    public override void Update(GameTime gameTime)
    {
        CaptureKeyboardState();

        if (_keybinds.IsJustPressed(InputAction.Back, _currentKeyboardState, _previousKeyboardState))
        {
            _game.GoBack();
            CommitKeyboardState();
            return;
        }

        foreach (var button in _buttons)
            button.Update(gameTime);

        if (_activeTab == StatsTab.Analysis)
        {
            var mouseState = Mouse.GetState();
            int scrollDelta = mouseState.ScrollWheelValue - _previousScrollValue;
            _previousScrollValue = mouseState.ScrollWheelValue;

            if (scrollDelta != 0)
            {
                _scrollOffset -= scrollDelta * 0.3f;
                _scrollOffset = MathHelper.Clamp(_scrollOffset, 0f, _maxScroll);
            }
        }
        else
        {
            _previousScrollValue = Mouse.GetState().ScrollWheelValue;
        }

        CommitKeyboardState();
    }

    public override void PostUpdate(GameTime gameTime) { }

    public override void Dispose()
    {
        _scissorRasterizer.Dispose();
    }

    public override void HandleResize(Rectangle vp)
    {
        ReloadKeybinds();
        UpdateLayout();
    }

    private void ReloadKeybinds()
    {
        var settings = _game.SettingsRepository.LoadSettings(_profileId);
        _keybinds = KeybindMap.FromSettings(settings);
    }

    private void LoadData()
    {
        _overview = _statsRepository.GetOverviewStats(_profileId);
        _bankrollHistory = _statsRepository.GetBankrollHistory(_profileId);
        _dealerBusts = _statsRepository.GetDealerBustByUpcard(_profileId);
        _handValueOutcomes = _statsRepository.GetOutcomesByHandValue(_profileId);
        _cardDistribution = _statsRepository.GetCardDistribution(_profileId);
        LoadStrategyMatrix();
    }

    private void LoadStrategyMatrix()
    {
        string handType = _matrixMode switch
        {
            StatsMatrixMode.Hard => "Hard",
            StatsMatrixMode.Soft => "Soft",
            StatsMatrixMode.Pairs => "Pairs",
            _ => "Hard"
        };
        _strategyMatrix = _statsRepository.GetStrategyMatrix(_profileId, handType);
    }

    private void UpdateLayout()
    {
        var vp = _graphicsDevice.Viewport;

        var tabSize = new Vector2(
            Math.Clamp(vp.Width * 0.14f, 120f, 200f),
            Math.Clamp(vp.Height * 0.05f, 30f, 44f));

        float tabY = vp.Height * 0.06f;
        float tabGap = tabSize.X * 0.15f;
        float tabStartX = vp.Width * 0.15f;

        _overviewTab.Size = tabSize;
        _overviewTab.Position = new Vector2(tabStartX, tabY);

        _analysisTab.Size = tabSize;
        _analysisTab.Position = new Vector2(tabStartX + tabSize.X + tabGap, tabY);

        var modeSize = new Vector2(
            Math.Clamp(vp.Width * 0.09f, 70f, 130f),
            Math.Clamp(vp.Height * 0.04f, 24f, 36f));

        _matrixHardButton.Size = modeSize;
        _matrixSoftButton.Size = modeSize;
        _matrixPairsButton.Size = modeSize;

        var backSize = new Vector2(
            Math.Clamp(vp.Width * 0.12f, 100f, 180f),
            Math.Clamp(vp.Height * 0.055f, 32f, 48f));

        _backButton.Size = backSize;
        _backButton.Position = new Vector2(vp.Width - backSize.X * 0.7f, vp.Height - backSize.Y * 0.8f);
    }

    private void DrawTitle(SpriteBatch sb)
    {
        var vp = _graphicsDevice.Viewport;
        const string title = "Statistics";
        float scale = GetResponsiveScale(1.0f);
        var size = _font.MeasureString(title) * scale;
        sb.DrawString(_font, title, new Vector2(vp.Width / 2f - size.X / 2f, vp.Height * 0.015f),
            StatsStyle.PrimaryText, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawTabButtons(GameTime gameTime, SpriteBatch sb)
    {
        _overviewTab.Draw(gameTime, sb);
        _analysisTab.Draw(gameTime, sb);

        var activeBtn = _activeTab == StatsTab.Overview ? _overviewTab : _analysisTab;
        var rect = activeBtn.DestRect;
        sb.Draw(_pixelTexture,
            new Rectangle(rect.X, rect.Bottom + 2, rect.Width, 3),
            Color.Gold);
    }
}
