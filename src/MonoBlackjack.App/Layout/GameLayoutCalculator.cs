using Microsoft.Xna.Framework;

namespace MonoBlackjack.Layout;

/// <summary>
/// Pure layout math for gameplay UI/card positioning.
/// Keeps rendering coordinate rules centralized and testable.
/// </summary>
public static class GameLayoutCalculator
{
    public const float CardAspectRatio = UIConstants.CardAspectRatio;
    public const float DealerCardsYRatio = 0.18f;
    public const float PlayerCardsYRatio = 0.52f;
    public const float SingleHandSpacingRatio = 1.08f;
    public const float MultiHandCardStepRatio = 0.72f;
    public const float MultiHandGapRatio = 1.2f;
    public const float MinMultiHandGapViewportRatio = 0.05f;
    public const float ActionButtonPaddingRatio = UIConstants.ButtonPaddingRatio;
    public const int MaxActionButtons = UIConstants.MaxActionButtons;

    public static Vector2 CalculateCardSize(int viewportHeight)
    {
        var cardHeight = Math.Clamp(viewportHeight * 0.2f, 120f, 220f);
        var cardWidth = cardHeight * CardAspectRatio;
        return new Vector2(cardWidth, cardHeight);
    }

    public static float CalculateDealerCardsY(int viewportHeight)
    {
        return viewportHeight * DealerCardsYRatio;
    }

    public static float CalculatePlayerCardsY(int viewportHeight)
    {
        return viewportHeight * PlayerCardsYRatio;
    }

    public static float CalculateActionButtonPadding(int viewportWidth)
    {
        return Math.Clamp(
            viewportWidth * ActionButtonPaddingRatio,
            UIConstants.MinButtonPadding,
            UIConstants.MaxButtonPadding);
    }

    public static Vector2 CalculateActionButtonSize(int viewportWidth, Vector2 cardSize, float actionButtonPadding)
    {
        var availableWidth = viewportWidth * UIConstants.ActionButtonsViewportWidthRatio;
        var maxButtonWidth = (availableWidth - (actionButtonPadding * (MaxActionButtons - 1))) / MaxActionButtons;
        var buttonWidth = Math.Clamp(
            Math.Min(cardSize.X * UIConstants.ActionButtonWidthToCardRatio, maxButtonWidth),
            UIConstants.MinActionButtonWidth,
            UIConstants.MaxActionButtonWidth);
        var buttonHeight = Math.Clamp(
            cardSize.Y * UIConstants.ActionButtonHeightToCardRatio,
            UIConstants.MinActionButtonHeight,
            UIConstants.MaxActionButtonHeight);
        return new Vector2(buttonWidth, buttonHeight);
    }

    public static float CalculateActionButtonY(
        int viewportHeight,
        float playerCardsY,
        Vector2 cardSize,
        Vector2 actionButtonSize,
        float handValueTextHeight)
    {
        const float handValueTopPadding = UIConstants.HandValueTopPadding;
        var handBottom = playerCardsY + cardSize.Y;
        var valueBottom = handBottom + handValueTopPadding + handValueTextHeight;
        var valueToButtonsGap = Math.Clamp(
            viewportHeight * UIConstants.ActionButtonsVerticalGapRatio,
            UIConstants.MinActionButtonsVerticalGap,
            UIConstants.MaxActionButtonsVerticalGap);
        var actionButtonY = valueBottom + valueToButtonsGap + (actionButtonSize.Y / 2f);
        return Math.Min(actionButtonY, viewportHeight - actionButtonSize.Y * UIConstants.ActionButtonsBottomInsetRatio);
    }

    public static IReadOnlyList<float> LayoutCenteredRowX(float centerX, float itemWidth, float padding, int itemCount)
    {
        if (itemCount <= 0)
            return [];

        var totalWidth = (itemWidth * itemCount) + (padding * (itemCount - 1));
        var startX = centerX - (totalWidth / 2f) + (itemWidth / 2f);
        var centers = new float[itemCount];
        for (int i = 0; i < itemCount; i++)
            centers[i] = startX + i * (itemWidth + padding);

        return centers;
    }

    public static Vector2 ComputeRowCardCenter(
        int viewportWidth,
        Vector2 cardSize,
        int cardCount,
        int cardIndex,
        float centerY)
    {
        cardCount = Math.Max(cardCount, 1);
        cardIndex = Math.Clamp(cardIndex, 0, cardCount - 1);

        var halfCard = cardSize / 2f;
        var spacing = cardSize.X * SingleHandSpacingRatio;

        float rowWidth = cardSize.X + spacing * (cardCount - 1);
        float maxWidth = viewportWidth * 0.9f;
        if (rowWidth > maxWidth && cardCount > 1)
            spacing = Math.Max((maxWidth - cardSize.X) / (cardCount - 1), cardSize.X * 0.55f);

        float totalRowWidth = cardSize.X + spacing * (cardCount - 1);
        float startX = viewportWidth / 2f - totalRowWidth / 2f;
        float cardCenterX = startX + halfCard.X + spacing * cardIndex;
        return new Vector2(cardCenterX, centerY);
    }

    public static float ComputeMultiHandWidth(IReadOnlyList<int> handCardCounts, Vector2 cardSize, float cardStep, float handGap)
    {
        ArgumentNullException.ThrowIfNull(handCardCounts);

        float totalWidth = 0f;
        for (int i = 0; i < handCardCounts.Count; i++)
        {
            int cardCount = Math.Max(handCardCounts[i], 1);
            totalWidth += cardSize.X + cardStep * (cardCount - 1);
        }

        if (handCardCounts.Count > 1)
            totalWidth += handGap * (handCardCounts.Count - 1);

        return totalWidth;
    }

    public static Vector2 ComputeMultiHandCardCenter(
        int viewportWidth,
        Vector2 cardSize,
        IReadOnlyList<int> handCardCounts,
        int handIndex,
        int cardIndexInHand,
        float centerY)
    {
        ArgumentNullException.ThrowIfNull(handCardCounts);
        if (handCardCounts.Count == 0)
            throw new ArgumentException("At least one hand card count is required.", nameof(handCardCounts));
        if (handIndex < 0 || handIndex >= handCardCounts.Count)
            throw new ArgumentOutOfRangeException(nameof(handIndex), handIndex, "Hand index is out of range.");

        int currentHandCount = Math.Max(handCardCounts[handIndex], 1);
        if (cardIndexInHand < 0 || cardIndexInHand >= currentHandCount)
            throw new ArgumentOutOfRangeException(nameof(cardIndexInHand), cardIndexInHand, "Card index is out of range for the selected hand.");

        if (handCardCounts.Count == 1)
            return ComputeRowCardCenter(viewportWidth, cardSize, currentHandCount, cardIndexInHand, centerY);

        var halfCard = cardSize / 2f;
        var cardStep = cardSize.X * MultiHandCardStepRatio;
        var handGap = Math.Max(cardSize.X * MultiHandGapRatio, viewportWidth * MinMultiHandGapViewportRatio);

        float totalWidth = ComputeMultiHandWidth(handCardCounts, cardSize, cardStep, handGap);
        float maxWidth = viewportWidth * 0.9f;
        if (totalWidth > maxWidth)
        {
            float fixedWidth = cardSize.X * handCardCounts.Count;
            float handGapUnits = Math.Max(handCardCounts.Count - 1, 0);
            float cardStepUnits = 0f;
            for (int i = 0; i < handCardCounts.Count; i++)
                cardStepUnits += Math.Max(handCardCounts[i], 1) - 1;

            handGap = Math.Max(cardSize.X * 0.12f, viewportWidth * 0.01f);
            var remainingForStep = maxWidth - fixedWidth - (handGap * handGapUnits);
            if (remainingForStep < 0f)
            {
                handGap = handGapUnits > 0f
                    ? Math.Max((maxWidth - fixedWidth) / handGapUnits, 0f)
                    : 0f;
                remainingForStep = 0f;
            }

            cardStep = cardStepUnits > 0f
                ? Math.Max(remainingForStep / cardStepUnits, 0f)
                : 0f;

            totalWidth = ComputeMultiHandWidth(handCardCounts, cardSize, cardStep, handGap);
        }

        float handStartX = viewportWidth / 2f - totalWidth / 2f;
        for (int h = 0; h < handIndex; h++)
        {
            int count = Math.Max(handCardCounts[h], 1);
            handStartX += cardSize.X + cardStep * (count - 1) + handGap;
        }

        float cardCenterX = handStartX + halfCard.X + cardStep * cardIndexInHand;
        return new Vector2(cardCenterX, centerY);
    }
}
