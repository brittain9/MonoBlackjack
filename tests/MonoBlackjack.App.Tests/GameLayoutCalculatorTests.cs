using MonoBlackjack.Layout;
using Microsoft.Xna.Framework;

namespace MonoBlackjack.App.Tests;

public class GameLayoutCalculatorTests
{
    [Theory]
    [InlineData(600)]
    [InlineData(720)]
    [InlineData(1080)]
    [InlineData(2160)]
    public void CalculateCardSize_UsesAspectRatioAndClamp(int viewportHeight)
    {
        var size = GameLayoutCalculator.CalculateCardSize(viewportHeight);

        Assert.InRange(size.Y, 120f, 220f);
        var ratio = size.X / size.Y;
        Assert.InRange(ratio, GameLayoutCalculator.CardAspectRatio - 0.0001f, GameLayoutCalculator.CardAspectRatio + 0.0001f);
    }

    [Theory]
    [InlineData(800, 600, 2)]
    [InlineData(800, 600, 5)]
    [InlineData(1280, 720, 3)]
    [InlineData(1920, 1080, 5)]
    public void LayoutCenteredRowX_ProducesEvenSpacingWithoutOverflow(int viewportWidth, int viewportHeight, int buttonCount)
    {
        var cardSize = GameLayoutCalculator.CalculateCardSize(viewportHeight);
        var padding = GameLayoutCalculator.CalculateActionButtonPadding(viewportWidth);
        var buttonSize = GameLayoutCalculator.CalculateActionButtonSize(viewportWidth, cardSize, padding);

        var centers = GameLayoutCalculator.LayoutCenteredRowX(
            viewportWidth / 2f,
            buttonSize.X,
            padding,
            buttonCount);

        Assert.Equal(buttonCount, centers.Count);

        for (int i = 1; i < centers.Count; i++)
        {
            var expectedDelta = buttonSize.X + padding;
            var delta = centers[i] - centers[i - 1];
            Assert.InRange(delta, expectedDelta - 0.001f, expectedDelta + 0.001f);
        }

        var left = centers[0] - buttonSize.X / 2f;
        var right = centers[^1] + buttonSize.X / 2f;
        Assert.True(left >= -0.001f);
        Assert.True(right <= viewportWidth + 0.001f);
    }

    [Theory]
    [InlineData(800, 600, 14f)]
    [InlineData(1280, 720, 16f)]
    [InlineData(1920, 1080, 18f)]
    public void CalculateActionButtonY_StaysBelowHandValueLabel(int viewportWidth, int viewportHeight, float handValueTextHeight)
    {
        var cardSize = GameLayoutCalculator.CalculateCardSize(viewportHeight);
        var playerCardsY = GameLayoutCalculator.CalculatePlayerCardsY(viewportHeight);
        var padding = GameLayoutCalculator.CalculateActionButtonPadding(viewportWidth);
        var buttonSize = GameLayoutCalculator.CalculateActionButtonSize(viewportWidth, cardSize, padding);

        var y = GameLayoutCalculator.CalculateActionButtonY(
            viewportHeight,
            playerCardsY,
            cardSize,
            buttonSize,
            handValueTextHeight);

        var buttonTop = y - buttonSize.Y / 2f;
        var valueBottom = playerCardsY + cardSize.Y + 8f + handValueTextHeight;
        Assert.True(buttonTop > valueBottom);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void DealerAndPlayerRows_DoNotOverlapVertically(int viewportWidth, int viewportHeight)
    {
        var cardSize = GameLayoutCalculator.CalculateCardSize(viewportHeight);
        var dealerY = GameLayoutCalculator.CalculateDealerCardsY(viewportHeight);
        var playerY = GameLayoutCalculator.CalculatePlayerCardsY(viewportHeight);

        var dealerBottom = dealerY + cardSize.Y;
        Assert.True(playerY > dealerBottom, $"Expected player row top ({playerY}) > dealer row bottom ({dealerBottom}) for viewport {viewportWidth}x{viewportHeight}");
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void ComputeRowCardCenter_ForLargeSingleHand_StaysWithinViewportBand(int viewportWidth, int viewportHeight)
    {
        var cardSize = GameLayoutCalculator.CalculateCardSize(viewportHeight);
        const int cardCount = 10;
        var centerY = GameLayoutCalculator.CalculatePlayerCardsY(viewportHeight) + cardSize.Y / 2f;

        for (int i = 0; i < cardCount; i++)
        {
            var center = GameLayoutCalculator.ComputeRowCardCenter(
                viewportWidth,
                cardSize,
                cardCount,
                i,
                centerY);

            var left = center.X - cardSize.X / 2f;
            var right = center.X + cardSize.X / 2f;
            Assert.True(left >= viewportWidth * 0.05f - 0.001f);
            Assert.True(right <= viewportWidth * 0.95f + 0.001f);
        }
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void ComputeMultiHandCardCenter_ForManyHands_StaysWithinViewportBand(int viewportWidth, int viewportHeight)
    {
        var cardSize = GameLayoutCalculator.CalculateCardSize(viewportHeight);
        var counts = new[] { 5, 6, 6, 5 };
        var centerY = GameLayoutCalculator.CalculatePlayerCardsY(viewportHeight) + cardSize.Y / 2f;

        float minLeft = float.MaxValue;
        float maxRight = float.MinValue;
        var allX = new List<float>();

        for (int hand = 0; hand < counts.Length; hand++)
        {
            for (int card = 0; card < counts[hand]; card++)
            {
                var center = GameLayoutCalculator.ComputeMultiHandCardCenter(
                    viewportWidth,
                    cardSize,
                    counts,
                    hand,
                    card,
                    centerY);

                allX.Add(center.X);
                minLeft = Math.Min(minLeft, center.X - cardSize.X / 2f);
                maxRight = Math.Max(maxRight, center.X + cardSize.X / 2f);
            }
        }

        Assert.True(minLeft >= viewportWidth * 0.05f - 0.001f);
        Assert.True(maxRight <= viewportWidth * 0.95f + 0.001f);
        Assert.True(allX.SequenceEqual(allX.OrderBy(x => x)));
    }
}
