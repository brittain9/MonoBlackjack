using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Core.Ports;

namespace MonoBlackjack;

internal sealed class StatsMatrixRenderer
{
    private readonly SpriteFont _font;
    private readonly Texture2D _pixelTexture;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Func<float, float> _getResponsiveScale;

    public StatsMatrixRenderer(
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

    public float Draw(SpriteBatch sb, IReadOnlyList<StrategyCell> strategyMatrix, float x, float y, float width)
    {
        if (strategyMatrix.Count == 0)
        {
            var emptyScale = _getResponsiveScale(0.5f);
            sb.DrawString(_font, "No data for this hand type",
                new Vector2(x, y),
                StatsStyle.SecondaryText, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
            return _font.MeasureString("A").Y * emptyScale + 8f;
        }

        var lookup = new Dictionary<int, Dictionary<string, StrategyCell>>();
        foreach (var cell in strategyMatrix)
        {
            string upcard = cell.DealerUpcard;
            if (Array.IndexOf(StatsStyle.UpcardOrder, upcard) < 0)
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
            var emptyScale = _getResponsiveScale(0.5f);
            sb.DrawString(_font, "No data for this hand type",
                new Vector2(x, y),
                StatsStyle.SecondaryText, 0f, Vector2.Zero, emptyScale, SpriteEffects.None, 0f);
            return _font.MeasureString("A").Y * emptyScale + 8f;
        }

        int cols = StatsStyle.UpcardOrder.Length + 1;
        int rows = playerValues.Count + 1;

        float cellW = ResolveMatrixCellWidth(width, cols);
        float cellH = ResolveMatrixCellHeight(_graphicsDevice.Viewport.Height);
        float tableWidth = cellW * cols;
        float tableStartX = x + Math.Max(0f, (width - tableWidth) / 2f);

        float legendScale = _getResponsiveScale(0.34f);
        const string legendText = "Green profitable   Red losing   Gray low sample";
        sb.DrawString(_font, legendText, new Vector2(x, y), StatsStyle.SecondaryText, 0f, Vector2.Zero, legendScale, SpriteEffects.None, 0f);

        float legendHeight = _font.MeasureString("A").Y * legendScale + 8f;
        float gridY = y + legendHeight;
        float labelScale = _getResponsiveScale(0.38f);

        sb.Draw(_pixelTexture,
            new Rectangle((int)tableStartX, (int)gridY, (int)tableWidth, (int)(cellH * rows)),
            StatsStyle.ChartSurface);

        sb.Draw(_pixelTexture,
            new Rectangle((int)tableStartX, (int)gridY, (int)tableWidth, (int)cellH),
            new Color(24, 36, 49, 255));

        const string headerAxis = "P/D";
        var axisSize = _font.MeasureString(headerAxis) * (labelScale * 0.8f);
        sb.DrawString(_font, headerAxis,
            new Vector2(tableStartX + cellW / 2f - axisSize.X / 2f, gridY + cellH / 2f - axisSize.Y / 2f),
            StatsStyle.SecondaryText, 0f, Vector2.Zero, labelScale * 0.8f, SpriteEffects.None, 0f);

        for (int c = 0; c < StatsStyle.UpcardOrder.Length; c++)
        {
            float cx = tableStartX + cellW * (c + 1);
            var headerSize = _font.MeasureString(StatsStyle.UpcardOrder[c]) * labelScale;
            sb.DrawString(_font, StatsStyle.UpcardOrder[c],
                new Vector2(cx + cellW / 2f - headerSize.X / 2f, gridY + cellH / 2f - headerSize.Y / 2f),
                StatsStyle.SecondaryText, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
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
                StatsStyle.SecondaryText, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);

            var byUpcard = lookup[pv];

            for (int c = 0; c < StatsStyle.UpcardOrder.Length; c++)
            {
                float cx = tableStartX + cellW * (c + 1);

                if (!byUpcard.TryGetValue(StatsStyle.UpcardOrder[c], out var cell) || cell.Total == 0)
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
                StatsStyle.DividerColor);
        }

        for (int r = 0; r <= rows; r++)
        {
            float ry = gridY + r * cellH;
            sb.Draw(_pixelTexture,
                new Rectangle((int)tableStartX, (int)ry, (int)tableWidth, 1),
                StatsStyle.DividerColor);
        }

        return legendHeight + cellH * rows + 8f;
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
            return StatsStyle.MatrixLowSampleColor;

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
}
