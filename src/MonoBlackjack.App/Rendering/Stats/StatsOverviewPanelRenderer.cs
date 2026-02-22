using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Core.Ports;

namespace MonoBlackjack.Rendering.Stats;

internal sealed class StatsOverviewPanelRenderer
{
    private readonly SpriteFont _font;
    private readonly Texture2D _pixelTexture;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Func<float, float> _getResponsiveScale;

    public StatsOverviewPanelRenderer(
        SpriteFont font,
        Texture2D pixelTexture,
        GraphicsDevice graphicsDevice,
        Func<float, float> getResponsiveScale)
    {
        _font = font;
        _pixelTexture = pixelTexture;
        _graphicsDevice = graphicsDevice;
        _getResponsiveScale = getResponsiveScale;
    }

    public void Draw(SpriteBatch sb, OverviewStats overview, IReadOnlyList<BankrollPoint> bankrollHistory)
    {
        var vp = _graphicsDevice.Viewport;
        float leftX = vp.Width * 0.06f;
        float contentTop = vp.Height * 0.13f;
        float contentWidth = vp.Width * 0.88f;
        float contentBottom = vp.Height * 0.86f;

        if (overview.TotalRounds == 0)
        {
            var emptyScale = _getResponsiveScale(0.8f);
            var emptySize = _font.MeasureString("No rounds played yet.") * emptyScale;
            sb.DrawString(_font, "No rounds played yet.",
                new Vector2(vp.Width / 2f - emptySize.X / 2f, vp.Height * 0.4f),
                StatsStyle.SecondaryText, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
            return;
        }

        sb.Draw(_pixelTexture,
            new Rectangle((int)leftX, (int)(contentTop - 6f), (int)contentWidth, (int)(contentBottom - contentTop)),
            StatsStyle.DashboardSurface);

        var bankrollSummary = StatsFormatting.ComputeBankrollSummary(bankrollHistory);

        string profitStr = StatsFormatting.FormatSignedCurrency(overview.NetProfit);
        Color profitColor = overview.NetProfit >= 0 ? Color.LightGreen : Color.Salmon;
        float heroLabelScale = _getResponsiveScale(0.55f);
        float heroScale = _getResponsiveScale(1.65f);
        const string profitLabel = "Net Profit";
        var heroLabelSize = _font.MeasureString(profitLabel) * heroLabelScale;
        float heroLabelY = contentTop + 8f;
        sb.DrawString(_font, profitLabel,
            new Vector2(vp.Width / 2f - heroLabelSize.X / 2f, heroLabelY),
            StatsStyle.SecondaryText, 0f, Vector2.Zero, heroLabelScale, SpriteEffects.None, 0f);

        var heroSize = _font.MeasureString(profitStr) * heroScale;
        float heroY = heroLabelY + heroLabelSize.Y + 4f;
        sb.DrawString(_font, profitStr,
            new Vector2(vp.Width / 2f - heroSize.X / 2f, heroY),
            profitColor, 0f, Vector2.Zero, heroScale, SpriteEffects.None, 0f);

        const string bankrollLabel = "Bankroll Delta";
        string bankrollStr = StatsFormatting.FormatSignedCurrency(bankrollSummary.Latest);
        float bankrollLabelScale = _getResponsiveScale(0.5f);
        float bankrollScale = _getResponsiveScale(1.2f);
        var bankrollLabelSize = _font.MeasureString(bankrollLabel) * bankrollLabelScale;
        float bankrollLabelY = heroY + heroSize.Y + 6f;
        sb.DrawString(_font, bankrollLabel,
            new Vector2(vp.Width / 2f - bankrollLabelSize.X / 2f, bankrollLabelY),
            StatsStyle.SecondaryText, 0f, Vector2.Zero, bankrollLabelScale, SpriteEffects.None, 0f);

        var bankrollSize = _font.MeasureString(bankrollStr) * bankrollScale;
        float bankrollY = bankrollLabelY + bankrollLabelSize.Y + 2f;
        Color bankrollColor = bankrollSummary.Latest >= 0 ? Color.LightGreen : Color.Salmon;
        sb.DrawString(_font, bankrollStr,
            new Vector2(vp.Width / 2f - bankrollSize.X / 2f, bankrollY),
            bankrollColor, 0f, Vector2.Zero, bankrollScale, SpriteEffects.None, 0f);

        string subLine = $"{overview.TotalRounds} rounds   {overview.TotalSessions} sessions   Peak {StatsFormatting.FormatSignedCurrency(bankrollSummary.Peak)}";
        float subScale = _getResponsiveScale(0.6f);
        var subSize = _font.MeasureString(subLine) * subScale;
        float subY = bankrollY + bankrollSize.Y + 6f;
        sb.DrawString(_font, subLine,
            new Vector2(vp.Width / 2f - subSize.X / 2f, subY),
            StatsStyle.SecondaryText, 0f, Vector2.Zero, subScale, SpriteEffects.None, 0f);

        float firstDividerY = subY + subSize.Y + 10f;
        DrawHorizontalDivider(sb, leftX + 10f, firstDividerY, contentWidth - 20f);

        float chartTop = firstDividerY + 12f;
        float chartHeight = Math.Clamp(vp.Height * 0.24f, 130f, 320f);
        float chartLeft = leftX + contentWidth * 0.02f;
        float chartWidth = contentWidth * 0.96f;
        DrawBankrollChart(sb, chartLeft, chartTop, chartWidth, chartHeight, bankrollHistory);

        float secondDividerY = chartTop + chartHeight + 12f;
        DrawHorizontalDivider(sb, leftX + 10f, secondDividerY, contentWidth - 20f);

        float gridTop = secondDividerY + 14f;
        float colWidth = contentWidth / 3f;
        float statScale = _getResponsiveScale(0.58f);
        float lineHeight = _font.MeasureString("A").Y * statScale + Math.Clamp(vp.Height * 0.012f, 8f, 16f);

        int totalHands = overview.Wins + overview.Losses + overview.Pushes + overview.Blackjacks + overview.Surrenders;

        float col1 = leftX + 8f;
        DrawStatLine(sb, col1, gridTop, statScale, "Win Rate",
            totalHands > 0 ? $"{(float)(overview.Wins + overview.Blackjacks) / totalHands:P1}" : "N/A", Color.LightGreen);
        DrawStatLine(sb, col1, gridTop + lineHeight, statScale, "Loss Rate",
            totalHands > 0 ? $"{(float)overview.Losses / totalHands:P1}" : "N/A", Color.Salmon);
        DrawStatLine(sb, col1, gridTop + lineHeight * 2, statScale, "Push Rate",
            totalHands > 0 ? $"{(float)overview.Pushes / totalHands:P1}" : "N/A", StatsStyle.SecondaryText);

        float col2 = leftX + colWidth;
        DrawStatLine(sb, col2, gridTop, statScale, "Blackjack",
            totalHands > 0 ? $"{(float)overview.Blackjacks / totalHands:P1}" : "N/A", Color.Gold);
        DrawStatLine(sb, col2, gridTop + lineHeight, statScale, "Bust Rate",
            totalHands > 0 ? $"{(float)overview.Busts / totalHands:P1}" : "N/A", Color.Salmon);
        DrawStatLine(sb, col2, gridTop + lineHeight * 2, statScale, "Avg Bet",
            $"${overview.AverageBet:F0}", Color.White);

        float col3 = leftX + colWidth * 2;
        string streakStr = overview.CurrentStreak > 0
            ? $"W{overview.CurrentStreak}"
            : overview.CurrentStreak < 0
                ? $"L{Math.Abs(overview.CurrentStreak)}"
                : "-";
        Color streakColor = overview.CurrentStreak > 0 ? Color.LightGreen
            : overview.CurrentStreak < 0 ? Color.Salmon : StatsStyle.SecondaryText;
        DrawStatLine(sb, col3, gridTop, statScale, "Streak", streakStr, streakColor);
        DrawStatLine(sb, col3, gridTop + lineHeight, statScale, "Best Round",
            StatsFormatting.FormatSignedCurrency(overview.BiggestWin), Color.LightGreen);
        DrawStatLine(sb, col3, gridTop + lineHeight * 2, statScale, "Worst Round",
            StatsFormatting.FormatSignedCurrency(overview.WorstLoss), Color.Salmon);

        int dividerTop = (int)(gridTop - 4f);
        int dividerHeight = (int)(lineHeight * 3f + 8f);
        sb.Draw(_pixelTexture,
            new Rectangle((int)(leftX + colWidth - 4f), dividerTop, 1, dividerHeight),
            StatsStyle.DividerColor);
        sb.Draw(_pixelTexture,
            new Rectangle((int)(leftX + colWidth * 2f - 4f), dividerTop, 1, dividerHeight),
            StatsStyle.DividerColor);
    }

    private void DrawStatLine(SpriteBatch sb, float x, float y, float scale, string label, string value, Color valueColor)
    {
        sb.DrawString(_font, label, new Vector2(x, y),
            StatsStyle.SecondaryText, 0f, Vector2.Zero, scale * 0.85f, SpriteEffects.None, 0f);
        float labelWidth = _font.MeasureString(label).X * scale * 0.85f;
        sb.DrawString(_font, $"  {value}", new Vector2(x + labelWidth, y),
            valueColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawBankrollChart(SpriteBatch sb, float x, float y, float width, float height, IReadOnlyList<BankrollPoint> bankrollHistory)
    {
        if (bankrollHistory.Count < 2)
        {
            var emptyScale = _getResponsiveScale(0.5f);
            sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height), StatsStyle.ChartSurface);
            sb.DrawString(_font, "Need 2+ rounds for chart",
                new Vector2(x + width * 0.3f, y + height * 0.4f),
                StatsStyle.SecondaryText, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
            return;
        }

        sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, (int)height), StatsStyle.ChartSurface);

        decimal minVal = 0;
        decimal maxVal = 0;
        foreach (var pt in bankrollHistory)
        {
            if (pt.CumulativeProfit < minVal)
                minVal = pt.CumulativeProfit;
            if (pt.CumulativeProfit > maxVal)
                maxVal = pt.CumulativeProfit;
        }

        decimal range = maxVal - minVal;
        if (range == 0)
            range = 1;

        float padding = Math.Clamp(width * 0.015f, 6f, 14f);
        float chartInnerWidth = width - padding * 2;
        float chartInnerHeight = height - padding * 2;

        float zeroY = y + padding + chartInnerHeight * (float)(1.0 - (double)(0 - minVal) / (double)range);
        sb.Draw(_pixelTexture, new Rectangle((int)x, (int)zeroY, (int)width, 2), StatsStyle.DividerColor);

        float prevPx = 0f;
        float prevPy = 0f;
        for (int i = 0; i < bankrollHistory.Count; i++)
        {
            float px = x + padding + chartInnerWidth * ((float)i / (bankrollHistory.Count - 1));
            float normalized = (float)((double)(bankrollHistory[i].CumulativeProfit - minVal) / (double)range);
            float py = y + padding + chartInnerHeight * (1f - normalized);

            if (i > 0)
            {
                DrawLine(sb, prevPx, prevPy, px, py,
                    bankrollHistory[i].CumulativeProfit >= 0 ? Color.LightGreen : Color.Salmon,
                    thickness: 3f);
            }

            prevPx = px;
            prevPy = py;
        }

        sb.Draw(_pixelTexture, new Rectangle((int)(prevPx - 2f), (int)(prevPy - 2f), 4, 4), Color.White);

        float labelScale = _getResponsiveScale(0.42f);
        sb.DrawString(_font, StatsFormatting.FormatSignedCurrency(maxVal),
            new Vector2(x + 2, y + 2),
            StatsStyle.SecondaryText, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
        sb.DrawString(_font, StatsFormatting.FormatSignedCurrency(minVal),
            new Vector2(x + 2, y + height - _font.MeasureString("A").Y * labelScale - 2),
            StatsStyle.SecondaryText, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
        sb.DrawString(_font, $"Now {StatsFormatting.FormatSignedCurrency(bankrollHistory[^1].CumulativeProfit)}",
            new Vector2(x + width - _font.MeasureString("Now +$9999").X * labelScale - 6f, y + 2f),
            StatsStyle.PrimaryText, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
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
        sb.Draw(_pixelTexture, new Rectangle((int)x, (int)y, (int)width, 1), StatsStyle.DividerColor);
    }
}
