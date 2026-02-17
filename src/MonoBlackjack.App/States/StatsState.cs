using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoBlackjack.Core.Ports;

namespace MonoBlackjack;

internal sealed class StatsState : State
{
    private enum Tab { Overview, Analysis }
    private enum MatrixMode { Hard, Soft, Pairs }

    private readonly IStatsRepository _statsRepository;
    private readonly int _profileId;
    private readonly Texture2D _buttonTexture;
    private readonly Texture2D _pixelTexture;
    private readonly SpriteFont _font;
    private readonly List<Button> _buttons = [];
    private readonly RasterizerState _scissorRasterizer = new() { ScissorTestEnable = true };

    private Button _overviewTab = null!;
    private Button _analysisTab = null!;
    private Button _backButton = null!;
    private Button _matrixHardButton = null!;
    private Button _matrixSoftButton = null!;
    private Button _matrixPairsButton = null!;

    private Tab _activeTab = Tab.Overview;
    private MatrixMode _matrixMode = MatrixMode.Hard;
    private KeybindMap _keybinds = null!;

    // Scroll state for Analysis tab
    private float _scrollOffset;
    private float _maxScroll;
    private int _previousScrollValue;

    // Cached data
    private OverviewStats _overview = null!;
    private IReadOnlyList<BankrollPoint> _bankrollHistory = [];
    private IReadOnlyList<DealerBustStat> _dealerBusts = [];
    private IReadOnlyList<HandValueOutcome> _handValueOutcomes = [];
    private IReadOnlyList<StrategyCell> _strategyMatrix = [];
    private IReadOnlyList<CardFrequency> _cardDistribution = [];

    private static readonly string[] UpcardOrder = ["A", "2", "3", "4", "5", "6", "7", "8", "9", "T"];
    private static readonly Color DashboardSurface = new(8, 12, 18, 216);
    private static readonly Color ChartSurface = new(14, 22, 31, 230);
    private static readonly Color PrimaryText = new(236, 242, 248);
    private static readonly Color SecondaryText = new(183, 197, 214);
    private static readonly Color DividerColor = new(104, 126, 148, 118);
    private static readonly Color MatrixLowSampleColor = new(68, 78, 92, 208);

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

        _previousScrollValue = Mouse.GetState().ScrollWheelValue;

        // Create buttons once — handlers bound here, never re-created
        _overviewTab = new Button(_buttonTexture, _font) { Text = "Overview", PenColor = Color.Black };
        _overviewTab.Click += (_, _) => { _activeTab = Tab.Overview; _scrollOffset = 0; };

        _analysisTab = new Button(_buttonTexture, _font) { Text = "Analysis", PenColor = Color.Black };
        _analysisTab.Click += (_, _) => { _activeTab = Tab.Analysis; _scrollOffset = 0; };

        _matrixHardButton = new Button(_buttonTexture, _font) { Text = "Hard", PenColor = Color.Black };
        _matrixHardButton.Click += (_, _) => { _matrixMode = MatrixMode.Hard; LoadStrategyMatrix(); };

        _matrixSoftButton = new Button(_buttonTexture, _font) { Text = "Soft", PenColor = Color.Black };
        _matrixSoftButton.Click += (_, _) => { _matrixMode = MatrixMode.Soft; LoadStrategyMatrix(); };

        _matrixPairsButton = new Button(_buttonTexture, _font) { Text = "Pairs", PenColor = Color.Black };
        _matrixPairsButton.Click += (_, _) => { _matrixMode = MatrixMode.Pairs; LoadStrategyMatrix(); };

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

        // Scrollable content area — clipped to avoid drawing over tabs/title/back button
        int clipTop = (int)(vp.Height * 0.12f);
        int clipBottom = (int)(vp.Height * 0.90f);
        var savedScissor = _graphicsDevice.ScissorRectangle;
        _graphicsDevice.ScissorRectangle = new Rectangle(0, clipTop, vp.Width, clipBottom - clipTop);

        spriteBatch.Begin(rasterizerState: _scissorRasterizer);

        if (_activeTab == Tab.Overview)
            DrawOverview(spriteBatch);
        else
            DrawAnalysis(gameTime, spriteBatch);

        spriteBatch.End();
        _graphicsDevice.ScissorRectangle = savedScissor;

        // Back button drawn outside clipped region
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

        // Scroll handling for Analysis tab
        if (_activeTab == Tab.Analysis)
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
            MatrixMode.Hard => "Hard",
            MatrixMode.Soft => "Soft",
            MatrixMode.Pairs => "Pairs",
            _ => "Hard"
        };
        _strategyMatrix = _statsRepository.GetStrategyMatrix(_profileId, handType);
    }

    /// <summary>
    /// Recomputes positions and sizes for all buttons. Does NOT create new buttons.
    /// </summary>
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

        // Matrix mode buttons get repositioned dynamically in DrawAnalysis,
        // but set initial size here
        _matrixHardButton.Size = modeSize;
        _matrixSoftButton.Size = modeSize;
        _matrixPairsButton.Size = modeSize;

        var backSize = new Vector2(
            Math.Clamp(vp.Width * 0.12f, 100f, 180f),
            Math.Clamp(vp.Height * 0.055f, 32f, 48f));

        _backButton.Size = backSize;
        _backButton.Position = new Vector2(vp.Width - backSize.X * 0.7f, vp.Height - backSize.Y * 0.8f);
    }

    // --- Drawing ---

    private void DrawTitle(SpriteBatch sb)
    {
        var vp = _graphicsDevice.Viewport;
        const string title = "Statistics";
        float scale = GetResponsiveScale(1.0f);
        var size = _font.MeasureString(title) * scale;
        sb.DrawString(_font, title, new Vector2(vp.Width / 2f - size.X / 2f, vp.Height * 0.015f),
            PrimaryText, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawTabButtons(GameTime gameTime, SpriteBatch sb)
    {
        _overviewTab.Draw(gameTime, sb);
        _analysisTab.Draw(gameTime, sb);

        // Draw underline on active tab
        var activeBtn = _activeTab == Tab.Overview ? _overviewTab : _analysisTab;
        var rect = activeBtn.DestRect;
        sb.Draw(_pixelTexture,
            new Rectangle(rect.X, rect.Bottom + 2, rect.Width, 3),
            Color.Gold);
    }

    private void DrawOverview(SpriteBatch sb)
    {
        var vp = _graphicsDevice.Viewport;
        float leftX = vp.Width * 0.06f;
        float contentTop = vp.Height * 0.13f;
        float contentWidth = vp.Width * 0.88f;
        float contentBottom = vp.Height * 0.86f;

        if (_overview.TotalRounds == 0)
        {
            var emptyScale = GetResponsiveScale(0.8f);
            var emptySize = _font.MeasureString("No rounds played yet.") * emptyScale;
            sb.DrawString(_font, "No rounds played yet.",
                new Vector2(vp.Width / 2f - emptySize.X / 2f, vp.Height * 0.4f),
                SecondaryText, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
            return;
        }

        sb.Draw(_pixelTexture,
            new Rectangle((int)leftX, (int)(contentTop - 6f), (int)contentWidth, (int)(contentBottom - contentTop)),
            DashboardSurface);

        var bankrollSummary = ComputeBankrollSummary(_bankrollHistory);

        string profitStr = FormatSignedCurrency(_overview.NetProfit);
        Color profitColor = _overview.NetProfit >= 0 ? Color.LightGreen : Color.Salmon;
        float heroLabelScale = GetResponsiveScale(0.55f);
        float heroScale = GetResponsiveScale(1.65f);
        const string profitLabel = "Net Profit";
        var heroLabelSize = _font.MeasureString(profitLabel) * heroLabelScale;
        float heroLabelY = contentTop + 8f;
        sb.DrawString(_font, profitLabel,
            new Vector2(vp.Width / 2f - heroLabelSize.X / 2f, heroLabelY),
            SecondaryText, 0f, Vector2.Zero, heroLabelScale, SpriteEffects.None, 0f);

        var heroSize = _font.MeasureString(profitStr) * heroScale;
        float heroY = heroLabelY + heroLabelSize.Y + 4f;
        sb.DrawString(_font, profitStr,
            new Vector2(vp.Width / 2f - heroSize.X / 2f, heroY),
            profitColor, 0f, Vector2.Zero, heroScale, SpriteEffects.None, 0f);

        const string bankrollLabel = "Bankroll Delta";
        string bankrollStr = FormatSignedCurrency(bankrollSummary.Latest);
        float bankrollLabelScale = GetResponsiveScale(0.5f);
        float bankrollScale = GetResponsiveScale(1.2f);
        var bankrollLabelSize = _font.MeasureString(bankrollLabel) * bankrollLabelScale;
        float bankrollLabelY = heroY + heroSize.Y + 6f;
        sb.DrawString(_font, bankrollLabel,
            new Vector2(vp.Width / 2f - bankrollLabelSize.X / 2f, bankrollLabelY),
            SecondaryText, 0f, Vector2.Zero, bankrollLabelScale, SpriteEffects.None, 0f);

        var bankrollSize = _font.MeasureString(bankrollStr) * bankrollScale;
        float bankrollY = bankrollLabelY + bankrollLabelSize.Y + 2f;
        Color bankrollColor = bankrollSummary.Latest >= 0 ? Color.LightGreen : Color.Salmon;
        sb.DrawString(_font, bankrollStr,
            new Vector2(vp.Width / 2f - bankrollSize.X / 2f, bankrollY),
            bankrollColor, 0f, Vector2.Zero, bankrollScale, SpriteEffects.None, 0f);

        string subLine = $"{_overview.TotalRounds} rounds   {_overview.TotalSessions} sessions   Peak {FormatSignedCurrency(bankrollSummary.Peak)}";
        float subScale = GetResponsiveScale(0.6f);
        var subSize = _font.MeasureString(subLine) * subScale;
        float subY = bankrollY + bankrollSize.Y + 6f;
        sb.DrawString(_font, subLine,
            new Vector2(vp.Width / 2f - subSize.X / 2f, subY),
            SecondaryText, 0f, Vector2.Zero, subScale, SpriteEffects.None, 0f);

        float firstDividerY = subY + subSize.Y + 10f;
        DrawHorizontalDivider(sb, leftX + 10f, firstDividerY, contentWidth - 20f);

        float chartTop = firstDividerY + 12f;
        float chartHeight = Math.Clamp(vp.Height * 0.24f, 130f, 320f);
        float chartLeft = leftX + contentWidth * 0.02f;
        float chartWidth = contentWidth * 0.96f;

        DrawBankrollChart(sb, chartLeft, chartTop, chartWidth, chartHeight);

        float secondDividerY = chartTop + chartHeight + 12f;
        DrawHorizontalDivider(sb, leftX + 10f, secondDividerY, contentWidth - 20f);

        float gridTop = secondDividerY + 14f;
        float colWidth = contentWidth / 3f;
        float statScale = GetResponsiveScale(0.58f);
        float lineHeight = _font.MeasureString("A").Y * statScale + Math.Clamp(vp.Height * 0.012f, 8f, 16f);

        int totalHands = _overview.Wins + _overview.Losses + _overview.Pushes + _overview.Blackjacks + _overview.Surrenders;

        float col1 = leftX + 8f;
        DrawStatLine(sb, col1, gridTop, statScale, "Win Rate",
            totalHands > 0 ? $"{(float)(_overview.Wins + _overview.Blackjacks) / totalHands:P1}" : "N/A", Color.LightGreen);
        DrawStatLine(sb, col1, gridTop + lineHeight, statScale, "Loss Rate",
            totalHands > 0 ? $"{(float)_overview.Losses / totalHands:P1}" : "N/A", Color.Salmon);
        DrawStatLine(sb, col1, gridTop + lineHeight * 2, statScale, "Push Rate",
            totalHands > 0 ? $"{(float)_overview.Pushes / totalHands:P1}" : "N/A", SecondaryText);

        float col2 = leftX + colWidth;
        DrawStatLine(sb, col2, gridTop, statScale, "Blackjack",
            totalHands > 0 ? $"{(float)_overview.Blackjacks / totalHands:P1}" : "N/A", Color.Gold);
        DrawStatLine(sb, col2, gridTop + lineHeight, statScale, "Bust Rate",
            totalHands > 0 ? $"{(float)_overview.Busts / totalHands:P1}" : "N/A", Color.Salmon);
        DrawStatLine(sb, col2, gridTop + lineHeight * 2, statScale, "Avg Bet",
            $"${_overview.AverageBet:F0}", Color.White);

        float col3 = leftX + colWidth * 2;
        string streakStr = _overview.CurrentStreak > 0
            ? $"W{_overview.CurrentStreak}"
            : _overview.CurrentStreak < 0
                ? $"L{Math.Abs(_overview.CurrentStreak)}"
                : "-";
        Color streakColor = _overview.CurrentStreak > 0 ? Color.LightGreen
            : _overview.CurrentStreak < 0 ? Color.Salmon : SecondaryText;
        DrawStatLine(sb, col3, gridTop, statScale, "Streak", streakStr, streakColor);
        DrawStatLine(sb, col3, gridTop + lineHeight, statScale, "Best Round",
            FormatSignedCurrency(_overview.BiggestWin), Color.LightGreen);
        DrawStatLine(sb, col3, gridTop + lineHeight * 2, statScale, "Worst Round",
            FormatSignedCurrency(_overview.WorstLoss), Color.Salmon);

        int dividerTop = (int)(gridTop - 4f);
        int dividerHeight = (int)(lineHeight * 3f + 8f);
        sb.Draw(_pixelTexture,
            new Rectangle((int)(leftX + colWidth - 4f), dividerTop, 1, dividerHeight),
            DividerColor);
        sb.Draw(_pixelTexture,
            new Rectangle((int)(leftX + colWidth * 2f - 4f), dividerTop, 1, dividerHeight),
            DividerColor);
    }

    private void DrawStatLine(SpriteBatch sb, float x, float y, float scale, string label, string value, Color valueColor)
    {
        sb.DrawString(_font, label, new Vector2(x, y),
            SecondaryText, 0f, Vector2.Zero, scale * 0.85f, SpriteEffects.None, 0f);
        float labelWidth = _font.MeasureString(label).X * scale * 0.85f;
        sb.DrawString(_font, $"  {value}", new Vector2(x + labelWidth, y),
            valueColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawBankrollChart(SpriteBatch sb, float x, float y, float width, float height)
    {
        if (_bankrollHistory.Count < 2)
        {
            var emptyScale = GetResponsiveScale(0.5f);
            sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height), ChartSurface);
            sb.DrawString(_font, "Need 2+ rounds for chart",
                new Vector2(x + width * 0.3f, y + height * 0.4f),
                SecondaryText, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
            return;
        }

        sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height),
            ChartSurface);

        decimal minVal = 0, maxVal = 0;
        foreach (var pt in _bankrollHistory)
        {
            if (pt.CumulativeProfit < minVal) minVal = pt.CumulativeProfit;
            if (pt.CumulativeProfit > maxVal) maxVal = pt.CumulativeProfit;
        }

        decimal range = maxVal - minVal;
        if (range == 0) range = 1;

        float padding = Math.Clamp(width * 0.015f, 6f, 14f);
        float chartInnerWidth = width - padding * 2;
        float chartInnerHeight = height - padding * 2;

        float zeroY = y + padding + chartInnerHeight * (float)(1.0 - (double)(0 - minVal) / (double)range);
        sb.Draw(_pixelTexture,
            new Rectangle((int)x, (int)zeroY, (int)width, 2),
            DividerColor);

        float prevPx = 0, prevPy = 0;
        for (int i = 0; i < _bankrollHistory.Count; i++)
        {
            float px = x + padding + chartInnerWidth * ((float)i / (_bankrollHistory.Count - 1));
            float normalized = (float)((double)(_bankrollHistory[i].CumulativeProfit - minVal) / (double)range);
            float py = y + padding + chartInnerHeight * (1f - normalized);

            if (i > 0)
            {
                DrawLine(sb, prevPx, prevPy, px, py,
                    _bankrollHistory[i].CumulativeProfit >= 0 ? Color.LightGreen : Color.Salmon,
                    thickness: 3f);
            }

            prevPx = px;
            prevPy = py;
        }

        sb.Draw(_pixelTexture, new Rectangle((int)(prevPx - 2f), (int)(prevPy - 2f), 4, 4), Color.White);

        float labelScale = GetResponsiveScale(0.42f);
        sb.DrawString(_font, FormatSignedCurrency(maxVal),
            new Vector2(x + 2, y + 2),
            SecondaryText, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
        sb.DrawString(_font, FormatSignedCurrency(minVal),
            new Vector2(x + 2, y + height - _font.MeasureString("A").Y * labelScale - 2),
            SecondaryText, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
        sb.DrawString(_font, $"Now {FormatSignedCurrency(_bankrollHistory[^1].CumulativeProfit)}",
            new Vector2(x + width - _font.MeasureString("Now +$9999").X * labelScale - 6f, y + 2f),
            PrimaryText, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
    }

    private void DrawLine(SpriteBatch sb, float x1, float y1, float x2, float y2, Color color, float thickness = 2f)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        float angle = MathF.Atan2(dy, dx);

        sb.Draw(_pixelTexture,
            new Vector2(x1, y1),
            null,
            color,
            angle,
            Vector2.Zero,
            new Vector2(length, thickness),
            SpriteEffects.None,
            0f);
    }

    private void DrawHorizontalDivider(SpriteBatch sb, float x, float y, float width)
    {
        sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, 1), DividerColor);
    }

    private void DrawAnalysis(GameTime gameTime, SpriteBatch sb)
    {
        var vp = _graphicsDevice.Viewport;
        float contentTop = vp.Height * 0.13f - _scrollOffset;
        float leftX = vp.Width * 0.06f;
        float contentWidth = vp.Width * 0.88f;
        float sectionScale = GetResponsiveScale(0.75f);
        float sectionTitleHeight = _font.MeasureString("A").Y * sectionScale;

        if (_overview.TotalRounds == 0)
        {
            var emptyScale = GetResponsiveScale(0.8f);
            var emptySize = _font.MeasureString("No data yet.") * emptyScale;
            sb.DrawString(_font, "No data yet.",
                new Vector2(vp.Width / 2f - emptySize.X / 2f, vp.Height * 0.4f),
                SecondaryText, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
            return;
        }

        float sectionY = contentTop;

        DrawSectionTitle(sb, leftX, sectionY, "Dealer Bust Rate by Upcard", sectionScale);
        sectionY += sectionTitleHeight + 10f;
        float dealerBustHeight = Math.Clamp(vp.Height * 0.17f, 96f, 190f);
        DrawDealerBustChart(sb, leftX, sectionY, contentWidth, dealerBustHeight);

        sectionY += dealerBustHeight + 18f;
        DrawHorizontalDivider(sb, leftX, sectionY, contentWidth);
        sectionY += 14f;

        DrawSectionTitle(sb, leftX, sectionY, "Your Outcomes by Hand Value", sectionScale);
        sectionY += sectionTitleHeight + 10f;
        float handValueHeight = Math.Clamp(vp.Height * 0.15f, 88f, 170f);
        DrawHandValueChart(sb, leftX, sectionY, contentWidth, handValueHeight);

        sectionY += handValueHeight + 18f;
        DrawHorizontalDivider(sb, leftX, sectionY, contentWidth);
        sectionY += 14f;

        DrawSectionTitle(sb, leftX, sectionY, "Strategy Matrix", sectionScale);

        float modeButtonY = sectionY + 2f;
        float modeStartX = leftX + _font.MeasureString("Strategy Matrix").X * sectionScale + 20f;
        float modeButtonGap = _matrixHardButton.Size.X * 0.15f;
        float modeButtonsWidth = _matrixHardButton.Size.X * 3f + modeButtonGap * 2f;
        float contentRight = leftX + contentWidth;
        if (modeStartX + modeButtonsWidth > contentRight)
        {
            modeStartX = leftX;
            modeButtonY += sectionTitleHeight + 6f;
        }

        _matrixHardButton.Position = new Vector2(modeStartX, modeButtonY);
        _matrixSoftButton.Position = new Vector2(modeStartX + _matrixHardButton.Size.X + modeButtonGap, modeButtonY);
        _matrixPairsButton.Position = new Vector2(modeStartX + (_matrixHardButton.Size.X + modeButtonGap) * 2f, modeButtonY);

        _matrixHardButton.Draw(gameTime, sb);
        _matrixSoftButton.Draw(gameTime, sb);
        _matrixPairsButton.Draw(gameTime, sb);

        var activeMode = _matrixMode switch
        {
            MatrixMode.Hard => _matrixHardButton,
            MatrixMode.Soft => _matrixSoftButton,
            MatrixMode.Pairs => _matrixPairsButton,
            _ => _matrixHardButton
        };
        var modeRect = activeMode.DestRect;
        sb.Draw(_pixelTexture,
            new Rectangle(modeRect.X, modeRect.Bottom + 1, modeRect.Width, 2),
            Color.Gold);

        float matrixStartY = Math.Max(sectionY + sectionTitleHeight + 10f, modeButtonY + _matrixHardButton.Size.Y + 10f);
        float matrixHeight = DrawStrategyMatrix(sb, leftX, matrixStartY, contentWidth);

        sectionY = matrixStartY + matrixHeight + 18f;
        DrawHorizontalDivider(sb, leftX, sectionY, contentWidth);
        sectionY += 14f;

        DrawSectionTitle(sb, leftX, sectionY, "Card Distribution", sectionScale);
        sectionY += sectionTitleHeight + 10f;
        float cardDistributionHeight = Math.Clamp(vp.Height * 0.14f, 84f, 170f);
        DrawCardDistribution(sb, leftX, sectionY, contentWidth, cardDistributionHeight);
        sectionY += cardDistributionHeight + 36f;

        float totalContentHeight = sectionY - (vp.Height * 0.13f - _scrollOffset);
        float visibleHeight = vp.Height * 0.78f;
        _maxScroll = Math.Max(0, totalContentHeight - visibleHeight);
    }

    private void DrawSectionTitle(SpriteBatch sb, float x, float y, string title, float scale)
    {
        sb.DrawString(_font, title, new Vector2(x, y), PrimaryText, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawDealerBustChart(SpriteBatch sb, float x, float y, float width, float height)
    {
        if (_dealerBusts.Count == 0)
        {
            var emptyScale = GetResponsiveScale(0.5f);
            sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height), ChartSurface);
            sb.DrawString(_font, "No data",
                new Vector2(x + width * 0.4f, y + height * 0.3f),
                SecondaryText, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
            return;
        }

        sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height),
            ChartSurface);

        var totalsByUpcard = new Dictionary<string, (int Total, int Bust)>(StringComparer.OrdinalIgnoreCase);
        foreach (var stat in _dealerBusts)
        {
            string upcard = stat.Upcard;
            if (Array.IndexOf(UpcardOrder, upcard) < 0)
                continue;

            if (!totalsByUpcard.TryGetValue(upcard, out var running))
                running = (0, 0);
            totalsByUpcard[upcard] = (running.Total + stat.TotalHands, running.Bust + stat.BustedHands);
        }

        float barWidth = width / UpcardOrder.Length;
        float labelScale = GetResponsiveScale(0.4f);
        float barPadding = barWidth * 0.2f;

        for (int i = 0; i < UpcardOrder.Length; i++)
        {
            string upcard = UpcardOrder[i];
            totalsByUpcard.TryGetValue(upcard, out var stat);

            float barX = x + barWidth * i + barPadding;
            float barActualWidth = barWidth - barPadding * 2;

            var labelSize = _font.MeasureString(upcard) * labelScale;
            sb.DrawString(_font, upcard,
                new Vector2(barX + barActualWidth / 2f - labelSize.X / 2f, y + height - labelSize.Y - 2f),
                SecondaryText, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

            if (stat.Total == 0) continue;

            float bustRate = (float)stat.Bust / stat.Total;
            float maxBarHeight = height - labelSize.Y - 20f;
            float barHeight = maxBarHeight * bustRate;

            Color barColor = ResolveDealerBustBarColor(bustRate);
            sb.Draw(_pixelTexture,
                new Rectangle((int)barX, (int)(y + height - labelSize.Y - 6f - barHeight), (int)barActualWidth, (int)barHeight),
                barColor * 0.85f);

            string pctStr = $"{bustRate:P0}";
            var pctSize = _font.MeasureString(pctStr) * (labelScale * 0.9f);
            sb.DrawString(_font, pctStr,
                new Vector2(barX + barActualWidth / 2f - pctSize.X / 2f, y + height - labelSize.Y - 8f - barHeight - pctSize.Y),
                PrimaryText, 0f, Vector2.Zero, labelScale * 0.9f, SpriteEffects.None, 0f);
        }
    }

    private void DrawHandValueChart(SpriteBatch sb, float x, float y, float width, float height)
    {
        if (_handValueOutcomes.Count == 0)
        {
            var emptyScale = GetResponsiveScale(0.5f);
            sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height), ChartSurface);
            sb.DrawString(_font, "No data",
                new Vector2(x + width * 0.4f, y + height * 0.3f),
                SecondaryText, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
            return;
        }

        sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height),
            ChartSurface);

        var filtered = _handValueOutcomes.Where(h => h.PlayerValue >= 4 && h.PlayerValue <= 21).ToList();
        if (filtered.Count == 0) return;

        var byValue = filtered.ToDictionary(h => h.PlayerValue);

        int minVal = 4, maxVal = 21;
        int valRange = maxVal - minVal + 1;
        float cellWidth = width / valRange;
        float labelScale = GetResponsiveScale(0.35f);
        float pctScale = labelScale * 0.8f;

        for (int v = minVal; v <= maxVal; v++)
        {
            byValue.TryGetValue(v, out var stat);
            float cellX = x + cellWidth * (v - minVal);

            string valStr = v.ToString(CultureInfo.InvariantCulture);
            var labelSize = _font.MeasureString(valStr) * labelScale;
            sb.DrawString(_font, valStr,
                new Vector2(cellX + cellWidth * 0.5f - labelSize.X / 2f, y + height - labelSize.Y - 2f),
                SecondaryText, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

            if (stat == null || stat.Total == 0) continue;

            float winRate = (float)stat.Wins / stat.Total;
            float barMaxH = height - labelSize.Y - 12f;
            float barH = barMaxH * Math.Min(winRate, 1f);
            float barW = Math.Max(cellWidth * 0.62f, 4f);
            float barX = cellX + (cellWidth - barW) * 0.5f;

            Color barColor = ResolveOutcomeRateColor(winRate);

            sb.Draw(_pixelTexture,
                new Rectangle((int)barX, (int)(y + height - labelSize.Y - 4f - barH), (int)barW, (int)barH),
                barColor * 0.75f);

            if (cellWidth > 30f)
            {
                string pctStr = $"{winRate:P0}";
                var pctSize = _font.MeasureString(pctStr) * pctScale;
                sb.DrawString(_font, pctStr,
                    new Vector2(barX + barW * 0.5f - pctSize.X / 2f, y + height - labelSize.Y - 6f - barH - pctSize.Y),
                    PrimaryText, 0f, Vector2.Zero, pctScale, SpriteEffects.None, 0f);
            }
        }
    }

    private float DrawStrategyMatrix(SpriteBatch sb, float x, float y, float width)
    {
        if (_strategyMatrix.Count == 0)
        {
            var emptyScale = GetResponsiveScale(0.5f);
            sb.DrawString(_font, "No data for this hand type",
                new Vector2(x, y),
                SecondaryText, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
            return _font.MeasureString("A").Y * emptyScale + 8f;
        }

        var lookup = new Dictionary<int, Dictionary<string, StrategyCell>>();
        foreach (var cell in _strategyMatrix)
        {
            string upcard = cell.DealerUpcard;
            if (Array.IndexOf(UpcardOrder, upcard) < 0)
                continue;

            if (!lookup.TryGetValue(cell.PlayerValue, out var byUpcard))
            {
                byUpcard = new Dictionary<string, StrategyCell>(StringComparer.OrdinalIgnoreCase);
                lookup[cell.PlayerValue] = byUpcard;
            }

            if (!byUpcard.TryGetValue(upcard, out var existing))
            {
                byUpcard[upcard] = cell with { DealerUpcard = upcard };
                continue;
            }

            byUpcard[upcard] = existing with
            {
                Wins = existing.Wins + cell.Wins,
                Losses = existing.Losses + cell.Losses,
                Pushes = existing.Pushes + cell.Pushes,
                Total = existing.Total + cell.Total,
                NetPayout = existing.NetPayout + cell.NetPayout
            };
        }

        var playerValues = lookup.Keys.Where(v => v >= 4 && v <= 21).OrderByDescending(v => v).ToList();
        if (playerValues.Count == 0)
        {
            var emptyScale = GetResponsiveScale(0.5f);
            sb.DrawString(_font, "No data for this hand type",
                new Vector2(x, y),
                SecondaryText, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
            return _font.MeasureString("A").Y * emptyScale + 8f;
        }

        int cols = UpcardOrder.Length + 1;
        int rows = playerValues.Count + 1;

        float cellW = ResolveMatrixCellWidth(width, cols);
        float cellH = ResolveMatrixCellHeight(_graphicsDevice.Viewport.Height);
        float tableWidth = cellW * cols;
        float tableStartX = x + Math.Max(0f, (width - tableWidth) / 2f);

        float legendScale = GetResponsiveScale(0.34f);
        string legendText = "Green profitable   Red losing   Gray low sample";
        sb.DrawString(_font, legendText, new Vector2(x, y), SecondaryText, 0f, Vector2.Zero, legendScale, SpriteEffects.None, 0f);

        float legendHeight = _font.MeasureString("A").Y * legendScale + 8f;
        float gridY = y + legendHeight;
        float labelScale = GetResponsiveScale(0.38f);

        sb.Draw(_pixelTexture,
            new Rectangle((int)tableStartX, (int)gridY, (int)tableWidth, (int)(cellH * rows)),
            ChartSurface);

        sb.Draw(_pixelTexture,
            new Rectangle((int)tableStartX, (int)gridY, (int)tableWidth, (int)cellH),
            new Color(24, 36, 49, 255));

        string headerAxis = "P/D";
        var axisSize = _font.MeasureString(headerAxis) * (labelScale * 0.8f);
        sb.DrawString(_font, headerAxis,
            new Vector2(tableStartX + cellW / 2f - axisSize.X / 2f, gridY + cellH / 2f - axisSize.Y / 2f),
            SecondaryText, 0f, Vector2.Zero, labelScale * 0.8f, SpriteEffects.None, 0f);

        for (int c = 0; c < UpcardOrder.Length; c++)
        {
            float cx = tableStartX + cellW * (c + 1);
            var headerSize = _font.MeasureString(UpcardOrder[c]) * labelScale;
            sb.DrawString(_font, UpcardOrder[c],
                new Vector2(cx + cellW / 2f - headerSize.X / 2f, gridY + cellH / 2f - headerSize.Y / 2f),
                SecondaryText, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
        }

        for (int r = 0; r < playerValues.Count; r++)
        {
            int pv = playerValues[r];
            float ry = gridY + cellH * (r + 1);

            sb.Draw(_pixelTexture,
                new Rectangle((int)tableStartX, (int)ry, (int)cellW, (int)cellH),
                new Color(24, 36, 49, 255));

            string rowLabel = pv.ToString(CultureInfo.InvariantCulture);
            var rlSize = _font.MeasureString(rowLabel) * labelScale;
            sb.DrawString(_font, rowLabel,
                new Vector2(tableStartX + cellW / 2f - rlSize.X / 2f, ry + cellH / 2f - rlSize.Y / 2f),
                SecondaryText, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

            var byUpcard = lookup[pv];

            for (int c = 0; c < UpcardOrder.Length; c++)
            {
                float cx = tableStartX + cellW * (c + 1);

                if (!byUpcard.TryGetValue(UpcardOrder[c], out var cell) || cell.Total == 0)
                {
                    sb.Draw(_pixelTexture,
                        new Rectangle((int)cx, (int)ry, (int)cellW, (int)cellH),
                        new Color(31, 42, 54, 185));
                    continue;
                }

                float profitRate = (float)(cell.Wins - cell.Losses) / cell.Total;
                Color cellColor = ResolveMatrixCellColor(profitRate, cell.Total);

                sb.Draw(_pixelTexture,
                    new Rectangle((int)cx + 1, (int)ry + 1, (int)cellW - 2, (int)cellH - 2),
                    cellColor);

                string cellStr = $"{profitRate:+0%;-0%}";
                float valueScale = labelScale * 0.82f;
                var csSize = _font.MeasureString(cellStr) * valueScale;
                sb.DrawString(_font, cellStr,
                    new Vector2(cx + cellW / 2f - csSize.X / 2f, ry + cellH / 2f - csSize.Y / 2f),
                    ResolveMatrixTextColor(cellColor), 0f, Vector2.Zero, valueScale, SpriteEffects.None, 0f);
            }
        }

        for (int c = 0; c <= cols; c++)
        {
            float cx = tableStartX + c * cellW;
            sb.Draw(_pixelTexture,
                new Rectangle((int)cx, (int)gridY, 1, (int)(cellH * rows)),
                DividerColor);
        }

        for (int r = 0; r <= rows; r++)
        {
            float ry = gridY + r * cellH;
            sb.Draw(_pixelTexture,
                new Rectangle((int)tableStartX, (int)ry, (int)tableWidth, 1),
                DividerColor);
        }

        return legendHeight + cellH * rows + 8f;
    }

    private void DrawCardDistribution(SpriteBatch sb, float x, float y, float width, float height)
    {
        if (_cardDistribution.Count == 0)
        {
            var emptyScale = GetResponsiveScale(0.5f);
            sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height), ChartSurface);
            sb.DrawString(_font, "No data",
                new Vector2(x + width * 0.4f, y + height * 0.3f),
                SecondaryText, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
            return;
        }

        sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height),
            ChartSurface);

        float cellWidth = width / _cardDistribution.Count;
        float labelScale = GetResponsiveScale(0.36f);

        for (int i = 0; i < _cardDistribution.Count; i++)
        {
            var card = _cardDistribution[i];
            float cellX = x + cellWidth * i;

            var labelSize = _font.MeasureString(card.Rank) * labelScale;
            sb.DrawString(_font, card.Rank,
                new Vector2(cellX + cellWidth * 0.5f - labelSize.X / 2f, y + height - labelSize.Y - 2f),
                SecondaryText, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

            float deviation = (float)(card.ActualPercent - card.ExpectedPercent);
            float maxBarH = (height - labelSize.Y - 24f) / 2f;
            float barH = Math.Min(Math.Abs(deviation) / 3f, 1f) * maxBarH;
            float midY = y + (height - labelSize.Y - 4f) / 2f;

            Color barColor = deviation >= 0 ? new Color(81, 160, 232) : new Color(229, 119, 96);
            float barW = Math.Max(cellWidth * 0.62f, 4f);
            float barX = cellX + (cellWidth - barW) * 0.5f;

            if (deviation >= 0)
            {
                sb.Draw(_pixelTexture,
                    new Rectangle((int)barX, (int)(midY - barH), (int)barW, (int)barH),
                    barColor * 0.78f);
            }
            else
            {
                sb.Draw(_pixelTexture,
                    new Rectangle((int)barX, (int)midY, (int)barW, (int)barH),
                    barColor * 0.78f);
            }

            string pctStr = $"{card.ActualPercent:F1}%";
            var pctSize = _font.MeasureString(pctStr) * (labelScale * 0.85f);
            float pctY = deviation >= 0
                ? midY - barH - pctSize.Y - 2f
                : midY + barH + 2f;
            sb.DrawString(_font, pctStr,
                new Vector2(barX + barW / 2f - pctSize.X / 2f, pctY),
                PrimaryText, 0f, Vector2.Zero, labelScale * 0.85f, SpriteEffects.None, 0f);
        }

        float expectedY = y + (height - _font.MeasureString("A").Y * labelScale - 4f) / 2f;
        sb.Draw(_pixelTexture,
            new Rectangle((int)x, (int)expectedY, (int)width, 2),
            DividerColor);
    }

    internal static (decimal Latest, decimal Peak, decimal Trough) ComputeBankrollSummary(IReadOnlyList<BankrollPoint> history)
    {
        if (history.Count == 0)
            return (0m, 0m, 0m);

        decimal latest = history[^1].CumulativeProfit;
        decimal peak = history.Max(x => x.CumulativeProfit);
        decimal trough = history.Min(x => x.CumulativeProfit);
        return (latest, peak, trough);
    }

    internal static string FormatSignedCurrency(decimal amount)
    {
        if (amount > 0)
            return $"+${amount:F0}";
        if (amount < 0)
            return $"-${Math.Abs(amount):F0}";
        return "$0";
    }

    internal static float ResolveMatrixCellWidth(float availableWidth, int columnCount)
    {
        if (columnCount <= 0)
            return 52f;

        return Math.Clamp(availableWidth / columnCount, 52f, 100f);
    }

    internal static float ResolveMatrixCellHeight(int viewportHeight)
    {
        return Math.Clamp(viewportHeight * 0.042f, 24f, 46f);
    }

    internal static Color ResolveMatrixCellColor(float profitRate, int sampleSize)
    {
        if (sampleSize < 5)
            return MatrixLowSampleColor;

        float clampedRate = Math.Clamp(profitRate, -1f, 1f);
        Color baseColor = clampedRate switch
        {
            >= 0.4f => new Color(54, 162, 104),
            >= 0.18f => new Color(65, 132, 92),
            > -0.18f => new Color(104, 118, 136),
            > -0.4f => new Color(154, 93, 93),
            _ => new Color(192, 74, 74)
        };

        byte alpha = (byte)Math.Clamp(130 + sampleSize * 8, 130, 255);
        return new Color(baseColor.R, baseColor.G, baseColor.B, alpha);
    }

    internal static Color ResolveMatrixTextColor(Color background)
    {
        float luminance = 0.2126f * background.R + 0.7152f * background.G + 0.0722f * background.B;
        return luminance >= 145f ? Color.Black : Color.White;
    }

    private static Color ResolveDealerBustBarColor(float bustRate)
    {
        if (bustRate >= 0.4f)
            return new Color(76, 179, 120);
        if (bustRate >= 0.25f)
            return new Color(215, 178, 89);
        return new Color(223, 108, 94);
    }

    private static Color ResolveOutcomeRateColor(float winRate)
    {
        if (winRate >= 0.55f)
            return new Color(76, 179, 120);
        if (winRate >= 0.4f)
            return new Color(215, 178, 89);
        return new Color(223, 108, 94);
    }
}
