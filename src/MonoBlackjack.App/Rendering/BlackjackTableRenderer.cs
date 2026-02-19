using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Core;
using MonoBlackjack.Layout;

namespace MonoBlackjack.Rendering;

/// <summary>
/// Draws a stylized blackjack table surface (felt, rails, seats, rule text).
/// Rendered as a static background under cards and UI controls.
/// </summary>
internal sealed class BlackjackTableRenderer
{
    private const int FeltBands = 72;
    private const int VignetteBands = 24;

    private static readonly Color LineGold = new(210, 188, 74);
    private static readonly Color RuleRed = new(184, 26, 26);
    private static readonly Color RuleGold = new(232, 184, 70);

    private readonly Texture2D _pixelTexture;
    private readonly SpriteFont _font;
    private readonly Func<float, float> _getResponsiveScale;

    public BlackjackTableRenderer(
        Texture2D pixelTexture,
        SpriteFont font,
        Func<float, float> getResponsiveScale)
    {
        _pixelTexture = pixelTexture;
        _font = font;
        _getResponsiveScale = getResponsiveScale;
    }

    public void Draw(
        SpriteBatch spriteBatch,
        in TableSurfaceLayout layout,
        GameRules rules,
        Color feltColor,
        bool insuranceEnabled)
    {
        var bounds = layout.ViewportBounds;
        DrawFeltBase(spriteBatch, bounds, feltColor);

        var center = layout.Center;
        float outerRadius = layout.OuterRadius;
        float middleRadius = layout.MiddleRadius;
        float innerRadius = layout.InnerRadius;

        float arcStart = layout.ArcStartRadians;
        float arcEnd = layout.ArcEndRadians;

        DrawArc(spriteBatch, center, outerRadius, arcStart, arcEnd, LineGold, thickness: 3f, segments: 80);
        DrawArc(spriteBatch, center, middleRadius, arcStart, arcEnd, LineGold, thickness: 2f, segments: 80);
        DrawArc(spriteBatch, center, innerRadius, arcStart, arcEnd, LineGold, thickness: 3f, segments: 80);

        DrawSeatBoxes(spriteBatch, center, outerRadius, layout.TopAnchorRadians, layout.ArcHalfSweepRadians, bounds);

        float headlineScale = _getResponsiveScale(0.9f);
        float dealerRuleScale = _getResponsiveScale(0.58f);
        float insuranceScale = _getResponsiveScale(0.74f);
        float surrenderScale = _getResponsiveScale(0.52f);
        float headlineHalfSweep = layout.ArcHalfSweepRadians * 0.81f;
        float dealerHalfSweep = layout.ArcHalfSweepRadians * 0.74f;
        float insuranceHalfSweep = layout.ArcHalfSweepRadians * 0.52f;

        DrawArcText(
            spriteBatch,
            ResolveBlackjackRuleText(rules.BlackjackPayout),
            center,
            middleRadius * 0.97f,
            layout.TopAnchorRadians - headlineHalfSweep,
            layout.TopAnchorRadians + headlineHalfSweep,
            RuleRed,
            headlineScale);

        DrawArcText(
            spriteBatch,
            ResolveDealerRuleText(rules.DealerHitsSoft17),
            center,
            middleRadius * 0.83f,
            layout.TopAnchorRadians - dealerHalfSweep,
            layout.TopAnchorRadians + dealerHalfSweep,
            RuleGold,
            dealerRuleScale);

        if (insuranceEnabled)
        {
            DrawArcText(
                spriteBatch,
                "INSURANCE",
                center,
                innerRadius * 1.04f,
                layout.TopAnchorRadians - insuranceHalfSweep,
                layout.TopAnchorRadians + insuranceHalfSweep,
                RuleRed,
                insuranceScale);

            DrawSideInsuranceText(spriteBatch, layout);
        }

        float surrenderY = Math.Min(
            center.Y - outerRadius * 0.04f,
            layout.PlayerCardsTopY - Math.Max(bounds.Height * 0.018f, 12f));
        DrawCenteredText(
            spriteBatch,
            ResolveSurrenderRuleText(rules),
            new Vector2(center.X, surrenderY),
            RuleGold,
            surrenderScale);
    }

    private void DrawFeltBase(SpriteBatch spriteBatch, Rectangle bounds, Color feltColor)
    {
        float bandWidth = bounds.Width / (float)FeltBands;
        for (int i = 0; i < FeltBands; i++)
        {
            float t = i / (float)(FeltBands - 1);
            float centerWeight = 1f - MathF.Abs((t * 2f) - 1f);
            float brightness = MathHelper.Lerp(0.72f, 1f, centerWeight);
            var bandColor = ScaleColor(feltColor, brightness);
            var x = bounds.Left + (int)(i * bandWidth);
            var width = (int)MathF.Ceiling(bandWidth) + 1;
            spriteBatch.Draw(_pixelTexture, new Rectangle(x, bounds.Top, width, bounds.Height), bandColor);
        }

        float rowHeight = bounds.Height / (float)VignetteBands;
        for (int i = 0; i < VignetteBands; i++)
        {
            float t = i / (float)(VignetteBands - 1);
            byte alpha = (byte)(MathHelper.Lerp(62f, 0f, t));
            var overlay = new Color((byte)0, (byte)0, (byte)0, alpha);
            int yTop = bounds.Top + (int)(i * rowHeight);
            int yBottom = bounds.Bottom - (int)((i + 1) * rowHeight);
            int height = (int)MathF.Ceiling(rowHeight) + 1;
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.Left, yTop, bounds.Width, height), overlay);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.Left, yBottom, bounds.Width, height), overlay);
        }
    }

    private static Color ScaleColor(Color color, float brightness)
    {
        int r = (int)Math.Clamp(color.R * brightness, 0f, 255f);
        int g = (int)Math.Clamp(color.G * brightness, 0f, 255f);
        int b = (int)Math.Clamp(color.B * brightness, 0f, 255f);
        return new Color(r, g, b);
    }

    private void DrawSeatBoxes(
        SpriteBatch spriteBatch,
        Vector2 center,
        float outerRadius,
        float topAnchor,
        float arcHalfSweep,
        Rectangle bounds)
    {
        float seatRadius = outerRadius * 1.06f;
        var seatSize = new Vector2(bounds.Width * 0.051f, bounds.Height * 0.085f);

        ReadOnlySpan<float> seatOffsetDegrees = stackalloc float[] { -57f, -40f, -24f, 24f, 40f, 57f };
        for (int i = 0; i < seatOffsetDegrees.Length; i++)
        {
            float offsetRadians = MathHelper.ToRadians(seatOffsetDegrees[i]);
            float maxSweep = arcHalfSweep - MathHelper.ToRadians(2f);
            float clampedOffset = Math.Clamp(offsetRadians, -maxSweep, maxSweep);
            float angle = topAnchor + clampedOffset;
            var radial = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var seatCenter = center + radial * seatRadius;
            float rotation = angle + MathF.PI / 2f;
            DrawRectOutline(spriteBatch, seatCenter, seatSize, rotation, LineGold, thickness: 2.5f);
        }

        float sideOffset = MathHelper.ToRadians(10f);
        float arcStart = topAnchor - arcHalfSweep;
        float arcEnd = topAnchor + arcHalfSweep;
        DrawSideSeatBox(spriteBatch, center, seatRadius * 1.01f, arcStart - sideOffset, seatSize);
        DrawSideSeatBox(spriteBatch, center, seatRadius * 1.01f, arcEnd + sideOffset, seatSize);
    }

    private void DrawSideSeatBox(SpriteBatch spriteBatch, Vector2 center, float radius, float angle, Vector2 size)
    {
        var radial = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var seatCenter = center + radial * radius;
        float rotation = angle + MathF.PI / 2f;
        DrawRectOutline(spriteBatch, seatCenter, size, rotation, LineGold, thickness: 2.5f);
    }

    private void DrawArc(
        SpriteBatch spriteBatch,
        Vector2 center,
        float radius,
        float startAngle,
        float endAngle,
        Color color,
        float thickness,
        int segments)
    {
        var previous = center + new Vector2(MathF.Cos(startAngle), MathF.Sin(startAngle)) * radius;

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = MathHelper.Lerp(startAngle, endAngle, t);
            var current = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            DrawLine(spriteBatch, previous, current, color, thickness);
            previous = current;
        }
    }

    private void DrawRectOutline(
        SpriteBatch spriteBatch,
        Vector2 center,
        Vector2 size,
        float rotation,
        Color color,
        float thickness)
    {
        float halfW = size.X / 2f;
        float halfH = size.Y / 2f;
        Span<Vector2> corners = stackalloc Vector2[4]
        {
            new Vector2(-halfW, -halfH),
            new Vector2(halfW, -halfH),
            new Vector2(halfW, halfH),
            new Vector2(-halfW, halfH)
        };

        float cos = MathF.Cos(rotation);
        float sin = MathF.Sin(rotation);

        Span<Vector2> transformed = stackalloc Vector2[4];
        for (int i = 0; i < corners.Length; i++)
        {
            var p = corners[i];
            transformed[i] = new Vector2(
                center.X + p.X * cos - p.Y * sin,
                center.Y + p.X * sin + p.Y * cos);
        }

        DrawLine(spriteBatch, transformed[0], transformed[1], color, thickness);
        DrawLine(spriteBatch, transformed[1], transformed[2], color, thickness);
        DrawLine(spriteBatch, transformed[2], transformed[3], color, thickness);
        DrawLine(spriteBatch, transformed[3], transformed[0], color, thickness);
    }

    private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness)
    {
        var delta = end - start;
        float length = delta.Length();
        if (length <= 0.01f)
            return;

        float angle = MathF.Atan2(delta.Y, delta.X);
        spriteBatch.Draw(
            _pixelTexture,
            start,
            null,
            color,
            angle,
            Vector2.Zero,
            new Vector2(length, thickness),
            SpriteEffects.None,
            0f);
    }

    private void DrawArcText(
        SpriteBatch spriteBatch,
        string text,
        Vector2 center,
        float radius,
        float startAngle,
        float endAngle,
        Color color,
        float scale)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        float direction = endAngle >= startAngle ? 1f : -1f;
        float sweep = MathF.Abs(endAngle - startAngle);
        float arcLength = radius * sweep;

        var glyphWidths = new float[text.Length];
        float totalWidth = 0f;
        for (int i = 0; i < text.Length; i++)
        {
            string glyph = text[i].ToString();
            float width = _font.MeasureString(glyph).X * scale;
            glyphWidths[i] = width;
            totalWidth += width;
        }

        float offset = Math.Max(0f, (arcLength - totalWidth) / 2f);
        for (int i = 0; i < text.Length; i++)
        {
            float glyphWidth = glyphWidths[i];
            float glyphMid = offset + glyphWidth / 2f;
            float angle = startAngle + direction * (glyphMid / radius);

            var glyphPosition = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            float rotation = angle + (direction > 0 ? MathF.PI / 2f : -MathF.PI / 2f);

            string glyph = text[i].ToString();
            var glyphSize = _font.MeasureString(glyph);
            spriteBatch.DrawString(
                _font,
                glyph,
                glyphPosition,
                color,
                rotation,
                glyphSize / 2f,
                scale,
                SpriteEffects.None,
                0f);

            offset += glyphWidth;
        }
    }

    private void DrawSideInsuranceText(SpriteBatch spriteBatch, in TableSurfaceLayout layout)
    {
        var center = layout.Center;
        float outerRadius = layout.OuterRadius;
        float scale = _getResponsiveScale(0.7f);
        string text = "PAYS 2 TO 1";
        var textSize = _font.MeasureString(text) * scale;
        float horizontalInset = Math.Max(layout.ViewportBounds.Width * 0.01f, 8f);

        var leftPos = center + new Vector2(-outerRadius * 0.9f, -outerRadius * 0.34f);
        var rightPos = center + new Vector2(outerRadius * 0.9f, -outerRadius * 0.34f);

        float minLeft = layout.ViewportBounds.Left + horizontalInset + textSize.X / 2f;
        leftPos.X = Math.Max(leftPos.X, minLeft);

        if (layout.ReservedUiBounds.Width > 0)
        {
            float maxRight = layout.ReservedUiBounds.Left - horizontalInset - textSize.X / 2f;
            rightPos.X = Math.Min(rightPos.X, maxRight);
        }
        else
        {
            float maxRight = layout.ViewportBounds.Right - horizontalInset - textSize.X / 2f;
            rightPos.X = Math.Min(rightPos.X, maxRight);
        }

        DrawCenteredText(spriteBatch, text, leftPos, RuleRed, scale, MathHelper.ToRadians(-57f));
        DrawCenteredText(spriteBatch, text, rightPos, RuleRed, scale, MathHelper.ToRadians(57f));
    }

    private void DrawCenteredText(
        SpriteBatch spriteBatch,
        string text,
        Vector2 position,
        Color color,
        float scale,
        float rotation = 0f)
    {
        var size = _font.MeasureString(text);
        spriteBatch.DrawString(
            _font,
            text,
            position,
            color,
            rotation,
            size / 2f,
            scale,
            SpriteEffects.None,
            0f);
    }

    private static string ResolveBlackjackRuleText(decimal blackjackPayout)
    {
        if (blackjackPayout == 1.5m)
            return "BLACKJACK PAYS 3 TO 2";
        if (blackjackPayout == 1.2m)
            return "BLACKJACK PAYS 6 TO 5";

        return $"BLACKJACK PAYS {blackjackPayout:0.##} TO 1";
    }

    private static string ResolveDealerRuleText(bool dealerHitsSoft17)
    {
        return dealerHitsSoft17
            ? "Dealer must draw to 16, hit soft 17"
            : "Dealer must draw to 16, stand on 17";
    }

    private static string ResolveSurrenderRuleText(GameRules rules)
    {
        if (rules.AllowEarlySurrender)
            return "SURRENDER: EARLY";
        if (rules.AllowLateSurrender)
            return "SURRENDER: LATE";
        return "SURRENDER: NONE";
    }
}
