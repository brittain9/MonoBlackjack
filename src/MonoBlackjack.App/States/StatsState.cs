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
        _backButton.Click += (_, _) => _game.ChangeState(new MenuState(_game, _graphicsDevice, _content));

        _buttons.AddRange([_overviewTab, _analysisTab, _matrixHardButton, _matrixSoftButton, _matrixPairsButton, _backButton]);

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
            DrawAnalysis(spriteBatch);

        spriteBatch.End();
        _graphicsDevice.ScissorRectangle = savedScissor;

        // Back button drawn outside clipped region
        spriteBatch.Begin();
        _backButton.Draw(gameTime, spriteBatch);
        spriteBatch.End();
    }

    public override void Update(GameTime gameTime)
    {
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
    }

    public override void PostUpdate(GameTime gameTime) { }

    public override void Dispose()
    {
        _scissorRasterizer.Dispose();
    }

    public override void HandleResize(Rectangle vp)
    {
        UpdateLayout();
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
        float scale = 1.0f;
        var size = _font.MeasureString(title) * scale;
        sb.DrawString(_font, title, new Vector2(vp.Width / 2f - size.X / 2f, vp.Height * 0.015f),
            Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
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

        if (_overview.TotalRounds == 0)
        {
            sb.DrawString(_font, "No rounds played yet.",
                new Vector2(vp.Width / 2f - _font.MeasureString("No rounds played yet.").X * 0.4f, vp.Height * 0.4f),
                Color.Gray, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            return;
        }

        // Hero number: Net Profit
        string profitStr = _overview.NetProfit >= 0
            ? $"+${_overview.NetProfit:F0}"
            : $"-${Math.Abs(_overview.NetProfit):F0}";
        Color profitColor = _overview.NetProfit >= 0 ? Color.LightGreen : Color.Salmon;
        float heroScale = 1.3f;
        var heroSize = _font.MeasureString(profitStr) * heroScale;
        sb.DrawString(_font, profitStr,
            new Vector2(vp.Width / 2f - heroSize.X / 2f, contentTop),
            profitColor, 0f, Vector2.Zero, heroScale, SpriteEffects.None, 0f);

        // Sub-headline
        string subLine = $"{_overview.TotalRounds} rounds   {_overview.TotalSessions} sessions";
        float subScale = 0.6f;
        var subSize = _font.MeasureString(subLine) * subScale;
        sb.DrawString(_font, subLine,
            new Vector2(vp.Width / 2f - subSize.X / 2f, contentTop + heroSize.Y + 4f),
            Color.LightGray, 0f, Vector2.Zero, subScale, SpriteEffects.None, 0f);

        // Bankroll chart
        float chartTop = contentTop + heroSize.Y + subSize.Y + 16f;
        float chartHeight = vp.Height * 0.22f;
        float chartLeft = vp.Width * 0.08f;
        float chartRight = vp.Width * 0.92f;
        float chartWidth = chartRight - chartLeft;

        DrawBankrollChart(sb, chartLeft, chartTop, chartWidth, chartHeight);

        // Stats grid below chart
        float gridTop = chartTop + chartHeight + 20f;
        float colWidth = vp.Width * 0.3f;
        float statScale = 0.55f;
        float lineHeight = _font.MeasureString("A").Y * statScale + 6f;

        int totalHands = _overview.Wins + _overview.Losses + _overview.Pushes + _overview.Blackjacks + _overview.Surrenders;

        // Column 1: Win/Loss/Push
        float col1 = leftX;
        DrawStatLine(sb, col1, gridTop, statScale, "Win Rate",
            totalHands > 0 ? $"{(float)(_overview.Wins + _overview.Blackjacks) / totalHands:P1}" : "N/A", Color.LightGreen);
        DrawStatLine(sb, col1, gridTop + lineHeight, statScale, "Loss Rate",
            totalHands > 0 ? $"{(float)_overview.Losses / totalHands:P1}" : "N/A", Color.Salmon);
        DrawStatLine(sb, col1, gridTop + lineHeight * 2, statScale, "Push Rate",
            totalHands > 0 ? $"{(float)_overview.Pushes / totalHands:P1}" : "N/A", Color.LightGray);

        // Column 2: BJ, Bust
        float col2 = leftX + colWidth;
        DrawStatLine(sb, col2, gridTop, statScale, "Blackjack",
            totalHands > 0 ? $"{(float)_overview.Blackjacks / totalHands:P1}" : "N/A", Color.Gold);
        DrawStatLine(sb, col2, gridTop + lineHeight, statScale, "Bust Rate",
            totalHands > 0 ? $"{(float)_overview.Busts / totalHands:P1}" : "N/A", Color.Salmon);
        DrawStatLine(sb, col2, gridTop + lineHeight * 2, statScale, "Avg Bet",
            $"${_overview.AverageBet:F0}", Color.White);

        // Column 3: Streak, Best, Worst
        float col3 = leftX + colWidth * 2;
        string streakStr = _overview.CurrentStreak > 0
            ? $"W{_overview.CurrentStreak}"
            : _overview.CurrentStreak < 0
                ? $"L{Math.Abs(_overview.CurrentStreak)}"
                : "-";
        Color streakColor = _overview.CurrentStreak > 0 ? Color.LightGreen
            : _overview.CurrentStreak < 0 ? Color.Salmon : Color.Gray;
        DrawStatLine(sb, col3, gridTop, statScale, "Streak", streakStr, streakColor);
        DrawStatLine(sb, col3, gridTop + lineHeight, statScale, "Best Round",
            $"+${_overview.BiggestWin:F0}", Color.LightGreen);
        DrawStatLine(sb, col3, gridTop + lineHeight * 2, statScale, "Worst Round",
            $"${_overview.WorstLoss:F0}", Color.Salmon);
    }

    private void DrawStatLine(SpriteBatch sb, float x, float y, float scale, string label, string value, Color valueColor)
    {
        sb.DrawString(_font, label, new Vector2(x, y),
            Color.Gray, 0f, Vector2.Zero, scale * 0.85f, SpriteEffects.None, 0f);
        float labelWidth = _font.MeasureString(label).X * scale * 0.85f;
        sb.DrawString(_font, $"  {value}", new Vector2(x + labelWidth, y),
            valueColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawBankrollChart(SpriteBatch sb, float x, float y, float width, float height)
    {
        if (_bankrollHistory.Count < 2)
        {
            sb.DrawString(_font, "Need 2+ rounds for chart",
                new Vector2(x + width * 0.3f, y + height * 0.4f),
                Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            return;
        }

        // Chart background
        sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height),
            new Color(20, 20, 20, 180));

        // Find min/max for scaling
        decimal minVal = 0, maxVal = 0;
        foreach (var pt in _bankrollHistory)
        {
            if (pt.CumulativeProfit < minVal) minVal = pt.CumulativeProfit;
            if (pt.CumulativeProfit > maxVal) maxVal = pt.CumulativeProfit;
        }

        decimal range = maxVal - minVal;
        if (range == 0) range = 1;

        float padding = 4f;
        float chartInnerWidth = width - padding * 2;
        float chartInnerHeight = height - padding * 2;

        // Zero line
        float zeroY = y + padding + chartInnerHeight * (float)(1.0 - (double)(0 - minVal) / (double)range);
        sb.Draw(_pixelTexture,
            new Rectangle((int)x, (int)zeroY, (int)width, 1),
            new Color(80, 80, 80));

        // Draw line chart
        float prevPx = 0, prevPy = 0;
        for (int i = 0; i < _bankrollHistory.Count; i++)
        {
            float px = x + padding + chartInnerWidth * ((float)i / (_bankrollHistory.Count - 1));
            float normalized = (float)((double)(_bankrollHistory[i].CumulativeProfit - minVal) / (double)range);
            float py = y + padding + chartInnerHeight * (1f - normalized);

            if (i > 0)
            {
                DrawLine(sb, prevPx, prevPy, px, py,
                    _bankrollHistory[i].CumulativeProfit >= 0 ? Color.LightGreen : Color.Salmon);
            }

            prevPx = px;
            prevPy = py;
        }

        // Axis labels
        float labelScale = 0.4f;
        sb.DrawString(_font, $"${maxVal:F0}",
            new Vector2(x + 2, y + 2),
            Color.Gray, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
        sb.DrawString(_font, $"${minVal:F0}",
            new Vector2(x + 2, y + height - _font.MeasureString("A").Y * labelScale - 2),
            Color.Gray, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
    }

    private void DrawLine(SpriteBatch sb, float x1, float y1, float x2, float y2, Color color)
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
            new Vector2(length, 2f),
            SpriteEffects.None,
            0f);
    }

    private void DrawAnalysis(SpriteBatch sb)
    {
        var vp = _graphicsDevice.Viewport;
        float contentTop = vp.Height * 0.13f - _scrollOffset;
        float leftX = vp.Width * 0.06f;
        float sectionScale = 0.7f;

        if (_overview.TotalRounds == 0)
        {
            sb.DrawString(_font, "No data yet.",
                new Vector2(vp.Width / 2f - _font.MeasureString("No data yet.").X * 0.4f, vp.Height * 0.4f),
                Color.Gray, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            return;
        }

        // Section 1: Dealer Bust Rate by Upcard
        float sectionY = contentTop;
        sb.DrawString(_font, "Dealer Bust Rate by Upcard",
            new Vector2(leftX, sectionY),
            Color.White, 0f, Vector2.Zero, sectionScale, SpriteEffects.None, 0f);

        sectionY += _font.MeasureString("A").Y * sectionScale + 8f;
        DrawDealerBustChart(sb, leftX, sectionY, vp.Width * 0.88f, vp.Height * 0.15f);

        sectionY += vp.Height * 0.15f + 24f;

        // Section 2: Outcomes by Hand Value
        sb.DrawString(_font, "Your Outcomes by Hand Value",
            new Vector2(leftX, sectionY),
            Color.White, 0f, Vector2.Zero, sectionScale, SpriteEffects.None, 0f);

        sectionY += _font.MeasureString("A").Y * sectionScale + 8f;
        DrawHandValueChart(sb, leftX, sectionY, vp.Width * 0.88f, vp.Height * 0.13f);

        sectionY += vp.Height * 0.13f + 24f;

        // Section 3: Strategy Matrix
        sb.DrawString(_font, "Strategy Matrix",
            new Vector2(leftX, sectionY),
            Color.White, 0f, Vector2.Zero, sectionScale, SpriteEffects.None, 0f);

        // Reposition matrix mode buttons based on scroll
        float modeButtonY = sectionY + 2f;
        float modeStartX = leftX + _font.MeasureString("Strategy Matrix").X * sectionScale + 20f;
        _matrixHardButton.Position = new Vector2(modeStartX, modeButtonY);
        _matrixSoftButton.Position = new Vector2(modeStartX + _matrixHardButton.Size.X * 1.15f, modeButtonY);
        _matrixPairsButton.Position = new Vector2(modeStartX + _matrixHardButton.Size.X * 2.3f, modeButtonY);

        _matrixHardButton.Draw(new GameTime(), sb);
        _matrixSoftButton.Draw(new GameTime(), sb);
        _matrixPairsButton.Draw(new GameTime(), sb);

        // Underline active mode
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

        sectionY += _font.MeasureString("A").Y * sectionScale + _matrixHardButton.Size.Y * 0.5f + 12f;
        float matrixHeight = DrawStrategyMatrix(sb, leftX, sectionY, vp.Width * 0.88f);

        sectionY += matrixHeight + 24f;

        // Section 4: Card Distribution
        sb.DrawString(_font, "Card Distribution",
            new Vector2(leftX, sectionY),
            Color.White, 0f, Vector2.Zero, sectionScale, SpriteEffects.None, 0f);

        sectionY += _font.MeasureString("A").Y * sectionScale + 8f;
        DrawCardDistribution(sb, leftX, sectionY, vp.Width * 0.88f, vp.Height * 0.12f);

        sectionY += vp.Height * 0.12f + 40f;

        // Update max scroll
        float totalContentHeight = sectionY - (vp.Height * 0.13f - _scrollOffset);
        float visibleHeight = vp.Height * 0.8f;
        _maxScroll = Math.Max(0, totalContentHeight - visibleHeight);
    }

    private void DrawDealerBustChart(SpriteBatch sb, float x, float y, float width, float height)
    {
        if (_dealerBusts.Count == 0)
        {
            sb.DrawString(_font, "No data",
                new Vector2(x + width * 0.4f, y + height * 0.3f),
                Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            return;
        }

        // Background
        sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height),
            new Color(20, 20, 20, 180));

        float barWidth = width / (UpcardOrder.Length + 1);
        float labelScale = 0.4f;
        float barPadding = barWidth * 0.2f;

        for (int i = 0; i < UpcardOrder.Length; i++)
        {
            string upcard = UpcardOrder[i];
            var stat = _dealerBusts.FirstOrDefault(s => s.Upcard == upcard);

            float barX = x + barWidth * (i + 0.5f);
            float barActualWidth = barWidth - barPadding * 2;

            // Label
            var labelSize = _font.MeasureString(upcard) * labelScale;
            sb.DrawString(_font, upcard,
                new Vector2(barX + barActualWidth / 2f - labelSize.X / 2f, y + height - labelSize.Y - 2f),
                Color.Gray, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

            if (stat == null || stat.TotalHands == 0) continue;

            float bustRate = (float)stat.BustedHands / stat.TotalHands;
            float maxBarHeight = height - labelSize.Y - 20f;
            float barHeight = maxBarHeight * bustRate;

            // Bar
            Color barColor = bustRate > 0.35f ? Color.LightGreen : bustRate > 0.2f ? Color.Gold : Color.Salmon;
            sb.Draw(_pixelTexture,
                new Rectangle((int)barX, (int)(y + height - labelSize.Y - 6f - barHeight), (int)barActualWidth, (int)barHeight),
                barColor * 0.8f);

            // Percentage label
            string pctStr = $"{bustRate:P0}";
            var pctSize = _font.MeasureString(pctStr) * (labelScale * 0.9f);
            sb.DrawString(_font, pctStr,
                new Vector2(barX + barActualWidth / 2f - pctSize.X / 2f, y + height - labelSize.Y - 8f - barHeight - pctSize.Y),
                Color.White, 0f, Vector2.Zero, labelScale * 0.9f, SpriteEffects.None, 0f);
        }
    }

    private void DrawHandValueChart(SpriteBatch sb, float x, float y, float width, float height)
    {
        if (_handValueOutcomes.Count == 0)
        {
            sb.DrawString(_font, "No data",
                new Vector2(x + width * 0.4f, y + height * 0.3f),
                Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            return;
        }

        sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height),
            new Color(20, 20, 20, 180));

        // Filter to values 4-21
        var filtered = _handValueOutcomes.Where(h => h.PlayerValue >= 4 && h.PlayerValue <= 21).ToList();
        if (filtered.Count == 0) return;

        int minVal = 4, maxVal = 21;
        int valRange = maxVal - minVal + 1;
        float cellWidth = width / (valRange + 1);
        float labelScale = 0.35f;

        for (int v = minVal; v <= maxVal; v++)
        {
            var stat = filtered.FirstOrDefault(h => h.PlayerValue == v);
            float cellX = x + cellWidth * (v - minVal + 0.5f);

            // Value label
            string valStr = v.ToString(CultureInfo.InvariantCulture);
            var labelSize = _font.MeasureString(valStr) * labelScale;
            sb.DrawString(_font, valStr,
                new Vector2(cellX + cellWidth * 0.3f - labelSize.X / 2f, y + height - labelSize.Y - 2f),
                Color.Gray, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

            if (stat == null || stat.Total == 0) continue;

            float winRate = (float)stat.Wins / stat.Total;
            float barMaxH = height - labelSize.Y - 8f;
            float barH = barMaxH * Math.Min(winRate, 1f);

            Color barColor = winRate > 0.5f ? Color.LightGreen : winRate > 0.35f ? Color.Gold : Color.Salmon;
            float barW = cellWidth * 0.7f;

            sb.Draw(_pixelTexture,
                new Rectangle((int)cellX, (int)(y + height - labelSize.Y - 4f - barH), (int)barW, (int)barH),
                barColor * 0.7f);
        }
    }

    private float DrawStrategyMatrix(SpriteBatch sb, float x, float y, float width)
    {
        if (_strategyMatrix.Count == 0)
        {
            sb.DrawString(_font, "No data for this hand type",
                new Vector2(x, y),
                Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            return _font.MeasureString("A").Y * 0.5f + 8f;
        }

        // Build lookup: playerValue -> dealerUpcard -> cell
        var lookup = new Dictionary<int, Dictionary<string, StrategyCell>>();
        foreach (var cell in _strategyMatrix)
        {
            if (!lookup.TryGetValue(cell.PlayerValue, out var byUpcard))
            {
                byUpcard = new Dictionary<string, StrategyCell>(StringComparer.OrdinalIgnoreCase);
                lookup[cell.PlayerValue] = byUpcard;
            }
            byUpcard[cell.DealerUpcard] = cell;
        }

        var playerValues = lookup.Keys.Where(v => v >= 4 && v <= 21).OrderByDescending(v => v).ToList();
        if (playerValues.Count == 0)
        {
            sb.DrawString(_font, "No data for this hand type",
                new Vector2(x, y),
                Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            return _font.MeasureString("A").Y * 0.5f + 8f;
        }

        int cols = UpcardOrder.Length + 1; // +1 for row header
        int rows = playerValues.Count + 1; // +1 for header row

        float cellW = Math.Min(width / cols, 70f);
        float cellH = Math.Clamp(_graphicsDevice.Viewport.Height * 0.028f, 18f, 30f);
        float labelScale = 0.35f;

        // Header row
        for (int c = 0; c < UpcardOrder.Length; c++)
        {
            float cx = x + cellW * (c + 1);
            var headerSize = _font.MeasureString(UpcardOrder[c]) * labelScale;
            sb.DrawString(_font, UpcardOrder[c],
                new Vector2(cx + cellW / 2f - headerSize.X / 2f, y + cellH / 2f - headerSize.Y / 2f),
                Color.LightGray, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
        }

        // Data rows
        for (int r = 0; r < playerValues.Count; r++)
        {
            int pv = playerValues[r];
            float ry = y + cellH * (r + 1);

            // Row header
            string rowLabel = pv.ToString(CultureInfo.InvariantCulture);
            var rlSize = _font.MeasureString(rowLabel) * labelScale;
            sb.DrawString(_font, rowLabel,
                new Vector2(x + cellW / 2f - rlSize.X / 2f, ry + cellH / 2f - rlSize.Y / 2f),
                Color.LightGray, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

            var byUpcard = lookup[pv];

            for (int c = 0; c < UpcardOrder.Length; c++)
            {
                float cx = x + cellW * (c + 1);

                if (!byUpcard.TryGetValue(UpcardOrder[c], out var cell) || cell.Total == 0)
                {
                    // Empty cell — light border
                    sb.Draw(_pixelTexture,
                        new Rectangle((int)cx, (int)ry, (int)cellW, (int)cellH),
                        new Color(30, 30, 30, 100));
                    continue;
                }

                // Net profit rate: (wins - losses) / total
                float profitRate = (float)(cell.Wins - cell.Losses) / cell.Total;

                // Color: green for positive, red for negative
                Color cellColor;
                if (profitRate > 0.2f) cellColor = new Color(30, 120, 30);
                else if (profitRate > 0) cellColor = new Color(30, 80, 30);
                else if (profitRate > -0.2f) cellColor = new Color(100, 30, 30);
                else cellColor = new Color(140, 30, 30);

                sb.Draw(_pixelTexture,
                    new Rectangle((int)cx + 1, (int)ry + 1, (int)cellW - 2, (int)cellH - 2),
                    cellColor * 0.9f);

                // Value text
                string cellStr = $"{profitRate:+0%;-0%}";
                var csSize = _font.MeasureString(cellStr) * (labelScale * 0.85f);
                sb.DrawString(_font, cellStr,
                    new Vector2(cx + cellW / 2f - csSize.X / 2f, ry + cellH / 2f - csSize.Y / 2f),
                    Color.White, 0f, Vector2.Zero, labelScale * 0.85f, SpriteEffects.None, 0f);
            }
        }

        return cellH * rows + 4f;
    }

    private void DrawCardDistribution(SpriteBatch sb, float x, float y, float width, float height)
    {
        if (_cardDistribution.Count == 0)
        {
            sb.DrawString(_font, "No data",
                new Vector2(x + width * 0.4f, y + height * 0.3f),
                Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            return;
        }

        sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height),
            new Color(20, 20, 20, 180));

        float cellWidth = width / (_cardDistribution.Count + 1);
        float labelScale = 0.35f;

        for (int i = 0; i < _cardDistribution.Count; i++)
        {
            var card = _cardDistribution[i];
            float cellX = x + cellWidth * (i + 0.5f);

            // Rank label
            var labelSize = _font.MeasureString(card.Rank) * labelScale;
            sb.DrawString(_font, card.Rank,
                new Vector2(cellX + cellWidth * 0.3f - labelSize.X / 2f, y + height - labelSize.Y - 2f),
                Color.Gray, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

            // Bar showing deviation from expected
            float deviation = (float)(card.ActualPercent - card.ExpectedPercent);
            float maxBarH = (height - labelSize.Y - 24f) / 2f;
            float barH = Math.Min(Math.Abs(deviation) / 3f, 1f) * maxBarH;
            float midY = y + (height - labelSize.Y - 4f) / 2f;

            Color barColor = deviation >= 0 ? Color.CornflowerBlue : Color.Salmon;
            float barW = cellWidth * 0.6f;

            if (deviation >= 0)
            {
                sb.Draw(_pixelTexture,
                    new Rectangle((int)cellX, (int)(midY - barH), (int)barW, (int)barH),
                    barColor * 0.7f);
            }
            else
            {
                sb.Draw(_pixelTexture,
                    new Rectangle((int)cellX, (int)midY, (int)barW, (int)barH),
                    barColor * 0.7f);
            }

            // Percentage label
            string pctStr = $"{card.ActualPercent:F1}%";
            var pctSize = _font.MeasureString(pctStr) * (labelScale * 0.85f);
            float pctY = deviation >= 0
                ? midY - barH - pctSize.Y - 2f
                : midY + barH + 2f;
            sb.DrawString(_font, pctStr,
                new Vector2(cellX + barW / 2f - pctSize.X / 2f, pctY),
                Color.White, 0f, Vector2.Zero, labelScale * 0.85f, SpriteEffects.None, 0f);
        }

        // Draw expected line
        float expectedY = y + (height - _font.MeasureString("A").Y * labelScale - 4f) / 2f;
        sb.Draw(_pixelTexture,
            new Rectangle((int)x, (int)expectedY, (int)width, 1),
            Color.Gray * 0.5f);
    }
}
