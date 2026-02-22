using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Core;
using MonoBlackjack.Layout;

namespace MonoBlackjack.Rendering.Game;

internal sealed class GameTableRenderer
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Texture2D _tableTexture;
    private readonly SpriteFont _font;
    private readonly GameRules _rules;
    private readonly Func<float, float> _getResponsiveScale;

    public GameTableRenderer(
        GraphicsDevice graphicsDevice,
        Texture2D tableTexture,
        SpriteFont font,
        GameRules rules,
        Func<float, float> getResponsiveScale)
    {
        _graphicsDevice = graphicsDevice;
        _tableTexture = tableTexture;
        _font = font;
        _rules = rules;
        _getResponsiveScale = getResponsiveScale;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var vp = _graphicsDevice.Viewport;
        var tableLayout = GameLayoutCalculator.CalculateTableLayout(vp.Width, vp.Height);
        var tableRect = new Rectangle(
            (int)MathF.Round(tableLayout.Left),
            (int)MathF.Round(tableLayout.Top),
            Math.Max(1, (int)MathF.Round(tableLayout.Width)),
            Math.Max(1, (int)MathF.Round(tableLayout.Height)));

        spriteBatch.Draw(_tableTexture, tableRect, Color.White);

        var payoutArc    = MapArcToViewport(GameLayoutCalculator.PayoutArc,     tableLayout);
        var dealerArc    = MapArcToViewport(GameLayoutCalculator.DealerRuleArc, tableLayout);
        var insuranceArc = MapArcToViewport(GameLayoutCalculator.InsuranceArc,  tableLayout);

        DrawTextOnArc(
            spriteBatch,
            BuildPayoutText(_rules.BlackjackPayout),
            payoutArc,
            new Color(210, 35, 35),
            _getResponsiveScale(0.68f));

        DrawTextOnArc(
            spriteBatch,
            BuildDealerRuleText(_rules.DealerHitsSoft17),
            dealerArc,
            new Color(255, 212, 64),
            _getResponsiveScale(0.46f));

        DrawInsuranceArcText(spriteBatch, insuranceArc, _getResponsiveScale(0.62f));
    }

    private ArcRenderInfo MapArcToViewport(ArcLayoutInfo sourceArc, TableLayoutInfo tableLayout)
    {
        var center = new Vector2(
            tableLayout.Left + (sourceArc.CenterSource.X * tableLayout.Scale),
            tableLayout.Top + (sourceArc.CenterSource.Y * tableLayout.Scale));

        return new ArcRenderInfo(
            center,
            sourceArc.RadiusSource * tableLayout.Scale,
            MathHelper.ToRadians(sourceArc.StartAngleDeg),
            MathHelper.ToRadians(sourceArc.EndAngleDeg));
    }

    private void DrawTextOnArc(
        SpriteBatch spriteBatch,
        string text,
        ArcRenderInfo arc,
        Color color,
        float baseScale)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        if (arc.Radius <= 1f)
            return;

        float delta = NormalizeSignedRadians(arc.EndAngleRad - arc.StartAngleRad);
        if (MathF.Abs(delta) < 0.01f)
            return;

        float direction = MathF.Sign(delta);
        float availableArcLength = MathF.Abs(delta) * arc.Radius;
        float letterSpacing = Math.Max(1f, 1.8f * baseScale);

        var glyphWidths = new float[text.Length];
        float textLength = 0f;
        for (int i = 0; i < text.Length; i++)
        {
            string glyph = text[i].ToString();
            float glyphWidth = _font.MeasureString(glyph).X * baseScale;
            glyphWidths[i] = glyphWidth;
            textLength += glyphWidth;

            if (i < text.Length - 1)
                textLength += letterSpacing;
        }

        float scale = baseScale;
        float maxLength = availableArcLength * 0.95f;
        if (textLength > maxLength && textLength > 0f)
        {
            float fit = maxLength / textLength;
            scale *= fit;
            letterSpacing *= fit;
            textLength = 0f;

            for (int i = 0; i < glyphWidths.Length; i++)
            {
                glyphWidths[i] *= fit;
                textLength += glyphWidths[i];
                if (i < glyphWidths.Length - 1)
                    textLength += letterSpacing;
            }
        }

        float midAngle = arc.StartAngleRad + (delta * 0.5f);
        float cursor = -textLength * 0.5f;
        for (int i = 0; i < text.Length; i++)
        {
            float glyphWidth = glyphWidths[i];
            float glyphMidOffset = cursor + (glyphWidth * 0.5f);
            float glyphAngle = midAngle + (direction * (glyphMidOffset / arc.Radius));
            Vector2 glyphPosition = arc.Center + new Vector2(MathF.Cos(glyphAngle), MathF.Sin(glyphAngle)) * arc.Radius;

            char character = text[i];
            if (!char.IsWhiteSpace(character))
            {
                string glyph = character.ToString();
                Vector2 origin = _font.MeasureString(glyph) * 0.5f;
                float rotation = glyphAngle - MathF.PI / 2f;
                if (direction > 0f)
                    rotation += MathF.PI;

                spriteBatch.DrawString(
                    _font,
                    glyph,
                    glyphPosition,
                    color,
                    rotation,
                    origin,
                    scale,
                    SpriteEffects.None,
                    0f);
            }

            cursor += glyphWidth + letterSpacing;
        }
    }

    private static float NormalizeSignedRadians(float radians)
    {
        const float tau = MathF.PI * 2f;
        while (radians <= -MathF.PI)
            radians += tau;
        while (radians > MathF.PI)
            radians -= tau;
        return radians;
    }

    private static string BuildPayoutText(decimal blackjackPayout)
    {
        string ratio = blackjackPayout switch
        {
            1.5m => "3 TO 2",
            1.2m => "6 TO 5",
            _ => $"{blackjackPayout.ToString("0.##", CultureInfo.InvariantCulture)} TO 1"
        };

        return $"BLACKJACK PAYS {ratio}";
    }

    private static string BuildDealerRuleText(bool dealerHitsSoft17)
    {
        return dealerHitsSoft17
            ? "Dealer must draw to 16, hit soft 17"
            : "Dealer must draw to 16, stand on 17";
    }

    private void DrawInsuranceArcText(SpriteBatch spriteBatch, ArcRenderInfo arc, float baseScale)
    {
        float delta = NormalizeSignedRadians(arc.EndAngleRad - arc.StartAngleRad);
        float oneThird = arc.StartAngleRad + delta / 3f;
        float twoThird = arc.StartAngleRad + 2f * delta / 3f;

        var leftArc   = new ArcRenderInfo(arc.Center, arc.Radius, arc.StartAngleRad, oneThird);
        var centerArc = new ArcRenderInfo(arc.Center, arc.Radius, oneThird, twoThird);
        var rightArc  = new ArcRenderInfo(arc.Center, arc.Radius, twoThird, arc.EndAngleRad);

        var color = new Color(210, 35, 35);
        DrawTextOnArc(spriteBatch, "PAYS 2 TO 1", leftArc,   color, baseScale);
        DrawTextOnArc(spriteBatch, "INSURANCE",   centerArc, color, baseScale);
        DrawTextOnArc(spriteBatch, "PAYS 2 TO 1", rightArc,  color, baseScale);
    }

    private readonly record struct ArcRenderInfo(
        Vector2 Center,
        float Radius,
        float StartAngleRad,
        float EndAngleRad);
}
