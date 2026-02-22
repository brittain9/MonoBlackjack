using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Core.Ports;

namespace MonoBlackjack.Rendering.Stats;

internal sealed class StatsAnalysisPanelRenderer
{
    private readonly SpriteFont _font;
    private readonly Texture2D _pixelTexture;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Func<float, float> _getResponsiveScale;
    private readonly StatsMatrixRenderer _matrixRenderer;

    public StatsAnalysisPanelRenderer(
        SpriteFont font,
        Texture2D pixelTexture,
        GraphicsDevice graphicsDevice,
        Func<float, float> getResponsiveScale,
        StatsMatrixRenderer matrixRenderer)
    {
        _font = font;
        _pixelTexture = pixelTexture;
        _graphicsDevice = graphicsDevice;
        _getResponsiveScale = getResponsiveScale;
        _matrixRenderer = matrixRenderer;
    }

    public float Draw(
        GameTime gameTime,
        SpriteBatch sb,
        OverviewStats overview,
        IReadOnlyList<DealerBustStat> dealerBusts,
        IReadOnlyList<HandValueOutcome> handValueOutcomes,
        IReadOnlyList<StrategyCell> strategyMatrix,
        IReadOnlyList<CardFrequency> cardDistribution,
        Button matrixHardButton,
        Button matrixSoftButton,
        Button matrixPairsButton,
        StatsMatrixMode matrixMode,
        float scrollOffset)
    {
        var vp = _graphicsDevice.Viewport;
        float contentTop = vp.Height * 0.13f - scrollOffset;
        float leftX = vp.Width * 0.06f;
        float contentWidth = vp.Width * 0.88f;
        float sectionScale = _getResponsiveScale(0.75f);
        float sectionTitleHeight = _font.MeasureString("A").Y * sectionScale;

        if (overview.TotalRounds == 0)
        {
            var emptyScale = _getResponsiveScale(0.8f);
            var emptySize = _font.MeasureString("No data yet.") * emptyScale;
            sb.DrawString(_font, "No data yet.",
                new Vector2(vp.Width / 2f - emptySize.X / 2f, vp.Height * 0.4f),
                StatsStyle.SecondaryText, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
            return 0f;
        }

        float sectionY = contentTop;

        DrawSectionTitle(sb, leftX, sectionY, "Dealer Bust Rate by Upcard", sectionScale);
        sectionY += sectionTitleHeight + 10f;
        float dealerBustHeight = Math.Clamp(vp.Height * 0.17f, 96f, 190f);
        DrawDealerBustChart(sb, leftX, sectionY, contentWidth, dealerBustHeight, dealerBusts);

        sectionY += dealerBustHeight + 18f;
        DrawHorizontalDivider(sb, leftX, sectionY, contentWidth);
        sectionY += 14f;

        DrawSectionTitle(sb, leftX, sectionY, "Your Outcomes by Hand Value", sectionScale);
        sectionY += sectionTitleHeight + 10f;
        float handValueHeight = Math.Clamp(vp.Height * 0.15f, 88f, 170f);
        DrawHandValueChart(sb, leftX, sectionY, contentWidth, handValueHeight, handValueOutcomes);

        sectionY += handValueHeight + 18f;
        DrawHorizontalDivider(sb, leftX, sectionY, contentWidth);
        sectionY += 14f;

        DrawSectionTitle(sb, leftX, sectionY, "Strategy Matrix", sectionScale);

        float modeButtonY = sectionY + 2f;
        float modeStartX = leftX + _font.MeasureString("Strategy Matrix").X * sectionScale + 20f;
        float modeButtonGap = matrixHardButton.Size.X * 0.15f;
        float modeButtonsWidth = matrixHardButton.Size.X * 3f + modeButtonGap * 2f;
        float contentRight = leftX + contentWidth;
        if (modeStartX + modeButtonsWidth > contentRight)
        {
            modeStartX = leftX;
            modeButtonY += sectionTitleHeight + 6f;
        }

        matrixHardButton.Position = new Vector2(modeStartX, modeButtonY);
        matrixSoftButton.Position = new Vector2(modeStartX + matrixHardButton.Size.X + modeButtonGap, modeButtonY);
        matrixPairsButton.Position = new Vector2(modeStartX + (matrixHardButton.Size.X + modeButtonGap) * 2f, modeButtonY);

        matrixHardButton.Draw(gameTime, sb);
        matrixSoftButton.Draw(gameTime, sb);
        matrixPairsButton.Draw(gameTime, sb);

        var activeMode = matrixMode switch
        {
            StatsMatrixMode.Hard => matrixHardButton,
            StatsMatrixMode.Soft => matrixSoftButton,
            StatsMatrixMode.Pairs => matrixPairsButton,
            _ => matrixHardButton
        };
        var modeRect = activeMode.DestRect;
        sb.Draw(_pixelTexture,
            new Rectangle(modeRect.X, modeRect.Bottom + 1, modeRect.Width, 2),
            Color.Gold);

        float matrixStartY = Math.Max(sectionY + sectionTitleHeight + 10f, modeButtonY + matrixHardButton.Size.Y + 10f);
        float matrixHeight = _matrixRenderer.Draw(sb, strategyMatrix, leftX, matrixStartY, contentWidth);

        sectionY = matrixStartY + matrixHeight + 18f;
        DrawHorizontalDivider(sb, leftX, sectionY, contentWidth);
        sectionY += 14f;

        DrawSectionTitle(sb, leftX, sectionY, "Card Distribution", sectionScale);
        sectionY += sectionTitleHeight + 10f;
        float cardDistributionHeight = Math.Clamp(vp.Height * 0.14f, 84f, 170f);
        DrawCardDistribution(sb, leftX, sectionY, contentWidth, cardDistributionHeight, cardDistribution);
        sectionY += cardDistributionHeight + 36f;

        float totalContentHeight = sectionY - contentTop;
        float visibleHeight = vp.Height * 0.78f;
        return Math.Max(0f, totalContentHeight - visibleHeight);
    }

    private void DrawSectionTitle(SpriteBatch sb, float x, float y, string title, float scale)
    {
        sb.DrawString(_font, title, new Vector2(x, y), StatsStyle.PrimaryText, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawDealerBustChart(SpriteBatch sb, float x, float y, float width, float height, IReadOnlyList<DealerBustStat> dealerBusts)
    {
        if (dealerBusts.Count == 0)
        {
            var emptyScale = _getResponsiveScale(0.5f);
            sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height), StatsStyle.ChartSurface);
            sb.DrawString(_font, "No data",
                new Vector2(x + width * 0.4f, y + height * 0.3f),
                StatsStyle.SecondaryText, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
            return;
        }

        sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height), StatsStyle.ChartSurface);

        var totalsByUpcard = new Dictionary<string, (int Total, int Bust)>(StringComparer.OrdinalIgnoreCase);
        foreach (var stat in dealerBusts)
        {
            string upcard = stat.Upcard;
            if (Array.IndexOf(StatsStyle.UpcardOrder, upcard) < 0)
                continue;

            if (!totalsByUpcard.TryGetValue(upcard, out var running))
                running = (0, 0);
            totalsByUpcard[upcard] = (running.Total + stat.TotalHands, running.Bust + stat.BustedHands);
        }

        float barWidth = width / StatsStyle.UpcardOrder.Length;
        float labelScale = _getResponsiveScale(0.4f);
        float barPadding = barWidth * 0.2f;

        for (int i = 0; i < StatsStyle.UpcardOrder.Length; i++)
        {
            string upcard = StatsStyle.UpcardOrder[i];
            totalsByUpcard.TryGetValue(upcard, out var stat);

            float barX = x + barWidth * i + barPadding;
            float barActualWidth = barWidth - barPadding * 2;

            var labelSize = _font.MeasureString(upcard) * labelScale;
            sb.DrawString(_font, upcard,
                new Vector2(barX + barActualWidth / 2f - labelSize.X / 2f, y + height - labelSize.Y - 2f),
                StatsStyle.SecondaryText, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

            if (stat.Total == 0)
                continue;

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
                StatsStyle.PrimaryText, 0f, Vector2.Zero, labelScale * 0.9f, SpriteEffects.None, 0f);
        }
    }

    private void DrawHandValueChart(SpriteBatch sb, float x, float y, float width, float height, IReadOnlyList<HandValueOutcome> handValueOutcomes)
    {
        if (handValueOutcomes.Count == 0)
        {
            var emptyScale = _getResponsiveScale(0.5f);
            sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height), StatsStyle.ChartSurface);
            sb.DrawString(_font, "No data",
                new Vector2(x + width * 0.4f, y + height * 0.3f),
                StatsStyle.SecondaryText, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
            return;
        }

        sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height), StatsStyle.ChartSurface);

        var filtered = handValueOutcomes.Where(h => h.PlayerValue >= 4 && h.PlayerValue <= 21).ToList();
        if (filtered.Count == 0)
            return;

        var byValue = filtered.ToDictionary(h => h.PlayerValue);

        const int minVal = 4;
        const int maxVal = 21;
        int valRange = maxVal - minVal + 1;
        float cellWidth = width / valRange;
        float labelScale = _getResponsiveScale(0.35f);
        float pctScale = labelScale * 0.8f;

        for (int v = minVal; v <= maxVal; v++)
        {
            byValue.TryGetValue(v, out var stat);
            float cellX = x + cellWidth * (v - minVal);

            string valStr = v.ToString(CultureInfo.InvariantCulture);
            var labelSize = _font.MeasureString(valStr) * labelScale;
            sb.DrawString(_font, valStr,
                new Vector2(cellX + cellWidth * 0.5f - labelSize.X / 2f, y + height - labelSize.Y - 2f),
                StatsStyle.SecondaryText, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

            if (stat == null || stat.Total == 0)
                continue;

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
                    StatsStyle.PrimaryText, 0f, Vector2.Zero, pctScale, SpriteEffects.None, 0f);
            }
        }
    }

    private void DrawCardDistribution(SpriteBatch sb, float x, float y, float width, float height, IReadOnlyList<CardFrequency> cardDistribution)
    {
        if (cardDistribution.Count == 0)
        {
            var emptyScale = _getResponsiveScale(0.5f);
            sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height), StatsStyle.ChartSurface);
            sb.DrawString(_font, "No data",
                new Vector2(x + width * 0.4f, y + height * 0.3f),
                StatsStyle.SecondaryText, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
            return;
        }

        sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height), StatsStyle.ChartSurface);

        float cellWidth = width / cardDistribution.Count;
        float labelScale = _getResponsiveScale(0.36f);

        for (int i = 0; i < cardDistribution.Count; i++)
        {
            var card = cardDistribution[i];
            float cellX = x + cellWidth * i;

            var labelSize = _font.MeasureString(card.Rank) * labelScale;
            sb.DrawString(_font, card.Rank,
                new Vector2(cellX + cellWidth * 0.5f - labelSize.X / 2f, y + height - labelSize.Y - 2f),
                StatsStyle.SecondaryText, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

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
                StatsStyle.PrimaryText, 0f, Vector2.Zero, labelScale * 0.85f, SpriteEffects.None, 0f);
        }

        float expectedY = y + (height - _font.MeasureString("A").Y * labelScale - 4f) / 2f;
        sb.Draw(_pixelTexture,
            new Rectangle((int)x, (int)expectedY, (int)width, 2),
            StatsStyle.DividerColor);
    }

    private void DrawHorizontalDivider(SpriteBatch sb, float x, float y, float width)
    {
        sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, 1), StatsStyle.DividerColor);
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
