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

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void ComputeRowCardCenter_ForDealerAndSinglePlayer_CentersRowsAtViewportCenter(int viewportWidth, int viewportHeight)
    {
        var cardSize = GameLayoutCalculator.CalculateCardSize(viewportHeight);
        var centerY = GameLayoutCalculator.CalculateDealerCardsY(viewportHeight) + cardSize.Y / 2f;
        var expectedCenterX = viewportWidth / 2f;

        for (int cardCount = 2; cardCount <= 10; cardCount++)
        {
            var first = GameLayoutCalculator.ComputeRowCardCenter(viewportWidth, cardSize, cardCount, 0, centerY);
            var last = GameLayoutCalculator.ComputeRowCardCenter(viewportWidth, cardSize, cardCount, cardCount - 1, centerY);
            var rowCenter = (first.X + last.X) / 2f;

            Assert.InRange(rowCenter, expectedCenterX - 0.001f, expectedCenterX + 0.001f);
        }
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void ComputeMultiHandCardCenter_ForOneToFourHands_AndTwoToTenCards_PerHand_StaysCenteredAndInBounds(int viewportWidth, int viewportHeight)
    {
        var cardSize = GameLayoutCalculator.CalculateCardSize(viewportHeight);
        var centerY = GameLayoutCalculator.CalculatePlayerCardsY(viewportHeight) + cardSize.Y / 2f;
        var expectedCenterX = viewportWidth / 2f;

        for (int handCount = 1; handCount <= 4; handCount++)
        {
            for (int cardsPerHand = 2; cardsPerHand <= 10; cardsPerHand++)
            {
                var counts = Enumerable.Repeat(cardsPerHand, handCount).ToArray();

                float minLeft = float.MaxValue;
                float maxRight = float.MinValue;

                for (int hand = 0; hand < handCount; hand++)
                {
                    for (int card = 0; card < cardsPerHand; card++)
                    {
                        var center = GameLayoutCalculator.ComputeMultiHandCardCenter(
                            viewportWidth,
                            cardSize,
                            counts,
                            hand,
                            card,
                            centerY);

                        minLeft = Math.Min(minLeft, center.X - cardSize.X / 2f);
                        maxRight = Math.Max(maxRight, center.X + cardSize.X / 2f);
                    }
                }

                var rowCenter = (minLeft + maxRight) / 2f;
                Assert.InRange(rowCenter, expectedCenterX - 0.001f, expectedCenterX + 0.001f);
                Assert.True(minLeft >= viewportWidth * 0.05f - 0.001f);
                Assert.True(maxRight <= viewportWidth * 0.95f + 0.001f);
            }
        }
    }

    [Theory]
    [InlineData(800, 600, 5, 5)]
    [InlineData(1024, 768, 6, 5)]
    [InlineData(1280, 720, 7, 6)]
    [InlineData(1920, 1080, 8, 8)]
    public void ComputeMultiHandCardCenter_ForTwoHands_WithManyCards_DoesNotOverlapBetweenHands(
        int viewportWidth,
        int viewportHeight,
        int firstHandCount,
        int secondHandCount)
    {
        var cardSize = GameLayoutCalculator.CalculateCardSize(viewportHeight);
        var centerY = GameLayoutCalculator.CalculatePlayerCardsY(viewportHeight) + cardSize.Y / 2f;
        var counts = new[] { firstHandCount, secondHandCount };

        float firstHandRight = float.MinValue;
        for (int card = 0; card < firstHandCount; card++)
        {
            var center = GameLayoutCalculator.ComputeMultiHandCardCenter(
                viewportWidth,
                cardSize,
                counts,
                handIndex: 0,
                cardIndexInHand: card,
                centerY);
            firstHandRight = Math.Max(firstHandRight, center.X + cardSize.X / 2f);
        }

        float secondHandLeft = float.MaxValue;
        for (int card = 0; card < secondHandCount; card++)
        {
            var center = GameLayoutCalculator.ComputeMultiHandCardCenter(
                viewportWidth,
                cardSize,
                counts,
                handIndex: 1,
                cardIndexInHand: card,
                centerY);
            secondHandLeft = Math.Min(secondHandLeft, center.X - cardSize.X / 2f);
        }

        Assert.True(
            secondHandLeft >= firstHandRight,
            $"Expected split hands not to overlap for {viewportWidth}x{viewportHeight}: firstRight={firstHandRight}, secondLeft={secondHandLeft}");
    }

    [Theory]
    [InlineData(800, 600, 5, 5)]
    [InlineData(1024, 768, 6, 6)]
    [InlineData(1280, 720, 7, 5)]
    [InlineData(1920, 1080, 8, 7)]
    public void ComputeMultiHandCardCenter_ForTwoHands_WithManyCards_KeepsMinimumVisualGap(
        int viewportWidth,
        int viewportHeight,
        int firstHandCount,
        int secondHandCount)
    {
        var cardSize = GameLayoutCalculator.CalculateCardSize(viewportHeight);
        var centerY = GameLayoutCalculator.CalculatePlayerCardsY(viewportHeight) + cardSize.Y / 2f;
        var counts = new[] { firstHandCount, secondHandCount };

        var lastOfFirst = GameLayoutCalculator.ComputeMultiHandCardCenter(
            viewportWidth,
            cardSize,
            counts,
            handIndex: 0,
            cardIndexInHand: firstHandCount - 1,
            centerY);
        var firstOfSecond = GameLayoutCalculator.ComputeMultiHandCardCenter(
            viewportWidth,
            cardSize,
            counts,
            handIndex: 1,
            cardIndexInHand: 0,
            centerY);

        var boundaryGap = (firstOfSecond.X - cardSize.X / 2f) - (lastOfFirst.X + cardSize.X / 2f);
        var minimumGap = cardSize.X * GameLayoutCalculator.TwoHandMinGapCardRatio;
        Assert.True(
            boundaryGap >= minimumGap - 0.001f,
            $"Expected split-hand gap >= {minimumGap} for {viewportWidth}x{viewportHeight}, but got {boundaryGap}");
    }

    [Theory]
    [InlineData(800, 600, 1920, 1080)]
    [InlineData(1024, 768, 1600, 900)]
    public void ComputeMultiHandCardCenter_ForResizeScenarios_PreservesOrderAndCentering(
        int fromWidth,
        int fromHeight,
        int toWidth,
        int toHeight)
    {
        var counts = new[] { 5, 6 };

        var fromCardSize = GameLayoutCalculator.CalculateCardSize(fromHeight);
        var fromCenterY = GameLayoutCalculator.CalculatePlayerCardsY(fromHeight) + fromCardSize.Y / 2f;
        var fromCenters = new List<float>();
        for (int hand = 0; hand < counts.Length; hand++)
        {
            for (int card = 0; card < counts[hand]; card++)
            {
                var center = GameLayoutCalculator.ComputeMultiHandCardCenter(
                    fromWidth,
                    fromCardSize,
                    counts,
                    hand,
                    card,
                    fromCenterY);
                fromCenters.Add(center.X);
            }
        }

        var toCardSize = GameLayoutCalculator.CalculateCardSize(toHeight);
        var toCenterY = GameLayoutCalculator.CalculatePlayerCardsY(toHeight) + toCardSize.Y / 2f;
        var toCenters = new List<float>();
        for (int hand = 0; hand < counts.Length; hand++)
        {
            for (int card = 0; card < counts[hand]; card++)
            {
                var center = GameLayoutCalculator.ComputeMultiHandCardCenter(
                    toWidth,
                    toCardSize,
                    counts,
                    hand,
                    card,
                    toCenterY);
                toCenters.Add(center.X);
            }
        }

        Assert.True(fromCenters.SequenceEqual(fromCenters.OrderBy(x => x)));
        Assert.True(toCenters.SequenceEqual(toCenters.OrderBy(x => x)));

        var fromRowCenter = (fromCenters.Min() + fromCenters.Max()) / 2f;
        var toRowCenter = (toCenters.Min() + toCenters.Max()) / 2f;
        Assert.InRange(fromRowCenter, fromWidth / 2f - 0.001f, fromWidth / 2f + 0.001f);
        Assert.InRange(toRowCenter, toWidth / 2f - 0.001f, toWidth / 2f + 0.001f);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void ComputeMultiHandCardCenter_ForTwoSplitHands_TwoCardsEach_DoesNotOverlapWithinEachHand(
        int viewportWidth,
        int viewportHeight)
    {
        var cardSize = GameLayoutCalculator.CalculateCardSize(viewportHeight);
        var centerY = GameLayoutCalculator.CalculatePlayerCardsY(viewportHeight) + cardSize.Y / 2f;
        var counts = new[] { 2, 2 };

        for (int handIndex = 0; handIndex < counts.Length; handIndex++)
        {
            var first = GameLayoutCalculator.ComputeMultiHandCardCenter(
                viewportWidth,
                cardSize,
                counts,
                handIndex,
                0,
                centerY);
            var second = GameLayoutCalculator.ComputeMultiHandCardCenter(
                viewportWidth,
                cardSize,
                counts,
                handIndex,
                1,
                centerY);

            var firstRight = first.X + cardSize.X / 2f;
            var secondLeft = second.X - cardSize.X / 2f;
            Assert.True(
                secondLeft >= firstRight - 0.001f,
                $"Expected non-overlap in hand {handIndex} for {viewportWidth}x{viewportHeight}: firstRight={firstRight}, secondLeft={secondLeft}");
        }
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void ComputeMultiHandCardCenter_ForThreeSplitHands_TwoCardsEach_UsesDealerLikeCardSpacing(
        int viewportWidth,
        int viewportHeight)
    {
        var cardSize = GameLayoutCalculator.CalculateCardSize(viewportHeight);
        var centerY = GameLayoutCalculator.CalculatePlayerCardsY(viewportHeight) + cardSize.Y / 2f;
        var counts = new[] { 2, 2, 2 };

        var dealerFirst = GameLayoutCalculator.ComputeRowCardCenter(viewportWidth, cardSize, 2, 0, centerY);
        var dealerSecond = GameLayoutCalculator.ComputeRowCardCenter(viewportWidth, cardSize, 2, 1, centerY);
        var dealerSpacing = dealerSecond.X - dealerFirst.X;

        for (int handIndex = 0; handIndex < counts.Length; handIndex++)
        {
            var first = GameLayoutCalculator.ComputeMultiHandCardCenter(
                viewportWidth,
                cardSize,
                counts,
                handIndex,
                0,
                centerY);
            var second = GameLayoutCalculator.ComputeMultiHandCardCenter(
                viewportWidth,
                cardSize,
                counts,
                handIndex,
                1,
                centerY);

            var handSpacing = second.X - first.X;
            Assert.InRange(handSpacing, dealerSpacing - 0.001f, dealerSpacing + 0.001f);

            var firstRight = first.X + cardSize.X / 2f;
            var secondLeft = second.X - cardSize.X / 2f;
            Assert.True(secondLeft >= firstRight - 0.001f);
        }
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void ComputeMultiHandCardCenter_ForFourSplitHands_TwoCardsEach_DoesNotOverlapWithinEachHand(
        int viewportWidth,
        int viewportHeight)
    {
        var cardSize = GameLayoutCalculator.CalculateCardSize(viewportHeight);
        var centerY = GameLayoutCalculator.CalculatePlayerCardsY(viewportHeight) + cardSize.Y / 2f;
        var counts = new[] { 2, 2, 2, 2 };

        for (int handIndex = 0; handIndex < counts.Length; handIndex++)
        {
            var first = GameLayoutCalculator.ComputeMultiHandCardCenter(
                viewportWidth,
                cardSize,
                counts,
                handIndex,
                0,
                centerY);
            var second = GameLayoutCalculator.ComputeMultiHandCardCenter(
                viewportWidth,
                cardSize,
                counts,
                handIndex,
                1,
                centerY);

            var firstRight = first.X + cardSize.X / 2f;
            var secondLeft = second.X - cardSize.X / 2f;
            Assert.True(
                secondLeft >= firstRight - 0.001f,
                $"Expected non-overlap for split hand {handIndex} in {viewportWidth}x{viewportHeight}: firstRight={firstRight}, secondLeft={secondLeft}");
        }
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void ComputeAdaptiveMultiHandCardCenter_WhenCrowded_StacksInactiveHands(int viewportWidth, int viewportHeight)
    {
        var cardSize = GameLayoutCalculator.CalculateCardSize(viewportHeight);
        var centerY = GameLayoutCalculator.CalculatePlayerCardsY(viewportHeight) + cardSize.Y / 2f;
        var counts = new[] { 4, 4, 4 };
        const int activeHand = 1;

        // Inactive hand should have tighter spacing than the active hand.
        var inactiveFirst = GameLayoutCalculator.ComputeAdaptiveMultiHandCardCenter(
            viewportWidth, cardSize, counts, 0, 0, centerY, activeHand);
        var inactiveSecond = GameLayoutCalculator.ComputeAdaptiveMultiHandCardCenter(
            viewportWidth, cardSize, counts, 0, 1, centerY, activeHand);
        var activeFirst = GameLayoutCalculator.ComputeAdaptiveMultiHandCardCenter(
            viewportWidth, cardSize, counts, activeHand, 0, centerY, activeHand);
        var activeSecond = GameLayoutCalculator.ComputeAdaptiveMultiHandCardCenter(
            viewportWidth, cardSize, counts, activeHand, 1, centerY, activeHand);

        var inactiveStep = inactiveSecond.X - inactiveFirst.X;
        var activeStep = activeSecond.X - activeFirst.X;
        Assert.True(inactiveStep < activeStep, $"Expected inactive step < active step, got inactive={inactiveStep}, active={activeStep}");
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void ComputeAdaptiveMultiHandCardCenter_WhenNotCrowded_MatchesDealerLikeSpacing(int viewportWidth, int viewportHeight)
    {
        var cardSize = GameLayoutCalculator.CalculateCardSize(viewportHeight);
        var centerY = GameLayoutCalculator.CalculatePlayerCardsY(viewportHeight) + cardSize.Y / 2f;
        var counts = new[] { 2, 2, 2 };
        const int activeHand = 1;

        var dealerFirst = GameLayoutCalculator.ComputeRowCardCenter(viewportWidth, cardSize, 2, 0, centerY);
        var dealerSecond = GameLayoutCalculator.ComputeRowCardCenter(viewportWidth, cardSize, 2, 1, centerY);
        var dealerStep = dealerSecond.X - dealerFirst.X;

        for (int hand = 0; hand < counts.Length; hand++)
        {
            var first = GameLayoutCalculator.ComputeAdaptiveMultiHandCardCenter(
                viewportWidth, cardSize, counts, hand, 0, centerY, activeHand);
            var second = GameLayoutCalculator.ComputeAdaptiveMultiHandCardCenter(
                viewportWidth, cardSize, counts, hand, 1, centerY, activeHand);
            var step = second.X - first.X;
            Assert.InRange(step, dealerStep - 0.001f, dealerStep + 0.001f);
        }
    }

    [Theory]
    [InlineData(800, 600, 6, 6)]
    [InlineData(1280, 720, 7, 6)]
    [InlineData(1920, 1080, 8, 8)]
    public void ComputeAdaptiveMultiHandCardCenter_ForTwoHandsWithManyCards_DoesNotOverlapBetweenHands(
        int viewportWidth,
        int viewportHeight,
        int firstHandCount,
        int secondHandCount)
    {
        var cardSize = GameLayoutCalculator.CalculateCardSize(viewportHeight);
        var centerY = GameLayoutCalculator.CalculatePlayerCardsY(viewportHeight) + cardSize.Y / 2f;
        var counts = new[] { firstHandCount, secondHandCount };
        const int activeHand = 0;

        float firstRight = float.MinValue;
        for (int card = 0; card < firstHandCount; card++)
        {
            var center = GameLayoutCalculator.ComputeAdaptiveMultiHandCardCenter(
                viewportWidth,
                cardSize,
                counts,
                handIndex: 0,
                cardIndexInHand: card,
                centerY,
                activeHandIndex: activeHand);
            firstRight = Math.Max(firstRight, center.X + cardSize.X / 2f);
        }

        float secondLeft = float.MaxValue;
        for (int card = 0; card < secondHandCount; card++)
        {
            var center = GameLayoutCalculator.ComputeAdaptiveMultiHandCardCenter(
                viewportWidth,
                cardSize,
                counts,
                handIndex: 1,
                cardIndexInHand: card,
                centerY,
                activeHandIndex: activeHand);
            secondLeft = Math.Min(secondLeft, center.X - cardSize.X / 2f);
        }

        Assert.True(
            secondLeft >= firstRight - 0.001f,
            $"Expected adaptive split layout to avoid overlap for {viewportWidth}x{viewportHeight}: firstRight={firstRight}, secondLeft={secondLeft}");
    }
}
