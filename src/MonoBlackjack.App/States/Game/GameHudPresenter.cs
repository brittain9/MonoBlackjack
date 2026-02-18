using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Core;
using MonoBlackjack.Core.Players;

namespace MonoBlackjack;

internal sealed class GameHudPresenter
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteFont _font;
    private readonly Texture2D _pixelTexture;
    private readonly Func<float, float> _getResponsiveScale;

    public GameHudPresenter(
        GraphicsDevice graphicsDevice,
        SpriteFont font,
        Texture2D pixelTexture,
        Func<float, float> getResponsiveScale)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _pixelTexture = pixelTexture;
        _getResponsiveScale = getResponsiveScale;
    }

    public void DrawAlignmentGuides(SpriteBatch spriteBatch, GameAnimationCoordinator animation, string dealerName, string playerName)
    {
        var vp = _graphicsDevice.Viewport;
        var centerX = vp.Width / 2f;
        var dealerTop = animation.GetDealerCardsY();
        var dealerBottom = dealerTop + animation.CardSize.Y;
        var playerTop = animation.GetPlayerCardsY();
        var playerBottom = playerTop + animation.CardSize.Y;

        var axisColor = new Color(120, 255, 255, 170);
        var boundsColor = new Color(255, 235, 120, 120);
        var handCenterColor = new Color(255, 120, 220, 200);

        spriteBatch.Draw(_pixelTexture, new Rectangle((int)centerX, 0, 1, vp.Height), axisColor);
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, (int)dealerTop, vp.Width, 1), boundsColor);
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, (int)dealerBottom, vp.Width, 1), boundsColor);
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, (int)playerTop, vp.Width, 1), boundsColor);
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, (int)playerBottom, vp.Width, 1), boundsColor);

        int dealerCount = Math.Max(animation.DealerCardCount, 2);
        var dealerFirst = animation.GetCardTargetPosition(dealerName, 0, 0);
        var dealerLast = animation.GetCardTargetPosition(dealerName, 0, dealerCount - 1);
        var dealerCenter = (dealerFirst.X + dealerLast.X) / 2f;
        spriteBatch.Draw(_pixelTexture, new Rectangle((int)dealerCenter, (int)dealerTop - 8, 1, 16), handCenterColor);

        int handCount = animation.GetPlayerHandCount();
        for (int h = 0; h < handCount; h++)
        {
            int cardCount = animation.GetPlayerCardCount(h);
            var first = animation.GetCardTargetPosition(playerName, h, 0);
            var last = animation.GetCardTargetPosition(playerName, h, cardCount - 1);
            var handCenter = (first.X + last.X) / 2f;
            spriteBatch.Draw(_pixelTexture, new Rectangle((int)handCenter, (int)playerTop - 8, 1, 16), handCenterColor);
        }

        var debugTextScale = _getResponsiveScale(0.55f);
        const string debugText = "Alignment Guides (F3)";
        spriteBatch.DrawString(
            _font,
            debugText,
            new Vector2(8f, vp.Height - _font.MeasureString(debugText).Y * debugTextScale - 8f),
            new Color(220, 255, 220),
            0f,
            Vector2.Zero,
            debugTextScale,
            SpriteEffects.None,
            0f);
    }

    public void DrawHud(SpriteBatch spriteBatch, decimal bank, GamePhase gamePhase, decimal lastBet)
    {
        var vp = _graphicsDevice.Viewport;
        var hudScale = _getResponsiveScale(0.7f);
        var hudPaddingX = Math.Max(vp.Width * 0.01f, 8f);
        var hudPaddingY = Math.Max(vp.Height * 0.011f, 6f);

        var bankText = $"Bank: ${bank}";
        spriteBatch.DrawString(
            _font,
            bankText,
            new Vector2(hudPaddingX, hudPaddingY),
            Color.White,
            0f,
            Vector2.Zero,
            hudScale,
            SpriteEffects.None,
            0f);

        if (gamePhase == GamePhase.Playing)
        {
            var betText = $"Bet: ${lastBet}";
            var betSize = _font.MeasureString(betText) * hudScale;
            spriteBatch.DrawString(
                _font,
                betText,
                new Vector2(vp.Width - betSize.X - hudPaddingX, hudPaddingY),
                Color.Gold,
                0f,
                Vector2.Zero,
                hudScale,
                SpriteEffects.None,
                0f);
        }
    }

    public void DrawWarningBanner(SpriteBatch spriteBatch, string warningMessage, float warningSecondsRemaining)
    {
        if (warningSecondsRemaining <= 0f || string.IsNullOrWhiteSpace(warningMessage))
            return;

        var vp = _graphicsDevice.Viewport;
        float scale = _getResponsiveScale(0.56f);
        var size = _font.MeasureString(warningMessage) * scale;
        var position = new Vector2(vp.Width / 2f - size.X / 2f, vp.Height - size.Y - 14f);

        spriteBatch.DrawString(
            _font,
            warningMessage,
            position,
            Color.OrangeRed,
            0f,
            Vector2.Zero,
            scale,
            SpriteEffects.None,
            0f);
    }

    public void DrawHandValues(
        SpriteBatch spriteBatch,
        bool showHandValues,
        GameRound round,
        GamePhase gamePhase,
        Dealer dealer,
        Human player,
        GameAnimationCoordinator animation)
    {
        if (!showHandValues || round == null! || gamePhase != GamePhase.Playing)
            return;

        var vp = _graphicsDevice.Viewport;
        var scale = _getResponsiveScale(0.9f);
        var labelPadding = Math.Max(vp.Height * 0.01f, 6f);

        if (animation.DealerCardCount > 0)
        {
            string dealerValueText;
            if (round.Phase == RoundPhase.DealerTurn || round.Phase == RoundPhase.Resolution || round.Phase == RoundPhase.Complete)
            {
                int dealerValue = dealer.Hand.Value;
                bool dealerSoft = dealer.Hand.IsSoft;
                dealerValueText = dealerSoft ? $"{dealerValue} (soft)" : $"{dealerValue}";
            }
            else
            {
                dealerValueText = "?";
            }

            var dealerTextSize = _font.MeasureString(dealerValueText) * scale;
            var dealerY = animation.GetDealerCardsY() - dealerTextSize.Y - labelPadding;
            var dealerX = vp.Width / 2f - dealerTextSize.X / 2f;
            spriteBatch.DrawString(_font, dealerValueText, new Vector2(dealerX, dealerY), Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        int handCount = animation.GetPlayerHandCount();
        for (int h = 0; h < handCount; h++)
        {
            if (h >= player.Hands.Count)
                continue;

            var hand = player.Hands[h];
            int handValue = hand.Value;
            bool isSoft = hand.IsSoft;
            bool isBusted = animation.IsBustedHand(h);

            string valueText = isBusted ? "BUST" : (isSoft ? $"{handValue} (soft)" : $"{handValue}");
            Color valueColor = isBusted ? Color.Red : (h == animation.ActivePlayerHandIndex ? Color.Gold : Color.LightGray);

            var textSize = _font.MeasureString(valueText) * scale;

            int cardCount = animation.GetPlayerCardCount(h);
            Vector2 firstCardPos = animation.GetCardTargetPosition(player.Name, h, 0);
            Vector2 lastCardPos = animation.GetCardTargetPosition(player.Name, h, cardCount - 1);
            float handCenterX = (firstCardPos.X + lastCardPos.X) / 2f;
            float handBottom = animation.GetPlayerCardsY() + animation.CardSize.Y;

            var textX = handCenterX - textSize.X / 2f;
            var textY = handBottom + labelPadding;
            spriteBatch.DrawString(_font, valueText, new Vector2(textX, textY), valueColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    public void DrawActiveHandIndicator(SpriteBatch spriteBatch, GameAnimationCoordinator animation, string playerName)
    {
        int cardCount = animation.GetPlayerCardCount(animation.ActivePlayerHandIndex);

        var firstCardCenter = animation.GetCardTargetPosition(playerName, animation.ActivePlayerHandIndex, 0);
        var lastCardCenter = animation.GetCardTargetPosition(playerName, animation.ActivePlayerHandIndex, cardCount - 1);
        float handCenterX = (firstCardCenter.X + lastCardCenter.X) / 2f;

        float triangleHeight = 10f;
        float triangleHalfWidth = 8f;
        float indicatorY = animation.GetPlayerCardsY() + animation.CardSize.Y + 8f;

        int rows = (int)triangleHeight;
        for (int row = 0; row < rows; row++)
        {
            float t = row / triangleHeight;
            float rowWidth = triangleHalfWidth * 2f * (1f - t);
            float x = handCenterX - rowWidth / 2f;
            float y = indicatorY + triangleHeight - 1 - row;

            var rect = new Rectangle((int)x, (int)y, (int)rowWidth, 1);
            spriteBatch.Draw(_pixelTexture, rect, Color.Gold);
        }
    }
}
