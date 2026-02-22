using Microsoft.Xna.Framework;

namespace MonoBlackjack.Layout;

/// <summary>
/// Pure layout math for gameplay UI/card positioning.
/// Keeps rendering coordinate rules centralized and testable.
/// </summary>
public static class GameLayoutCalculator
{
    public const float CardAspectRatio = UIConstants.CardAspectRatio;
    public const float TableSourceWidth = 3840f;
    public const float TableSourceHeight = 1388f;

    // Arc geometry derived from pixel measurements of Content/Art/BlackjackTable.png (3840×1388 px).
    // All three arcs share the same concentric center: X=1920 (image midpoint), Y=-1000 (off-screen above).
    // Radii place each text band on the corresponding yellow line of the felt.
    // Angles use standard MonoGame convention: 0°=right, 90°=down, 135°=down-left, 45°=down-right.
    public static readonly ArcLayoutInfo PayoutArc = new(
        CenterSource: new Vector2(1920f, -1000f),
        RadiusSource: 1300f,
        StartAngleDeg: 135f,
        EndAngleDeg: 45f);

    public static readonly ArcLayoutInfo DealerRuleArc = new(
        CenterSource: new Vector2(1920f, -1000f),
        RadiusSource: 1400f,
        StartAngleDeg: 135f,
        EndAngleDeg: 45f);

    public static readonly ArcLayoutInfo InsuranceArc = new(
        CenterSource: new Vector2(1920f, -1000f),
        RadiusSource: 1625f,
        StartAngleDeg: 135f,
        EndAngleDeg: 45f);

    public const float DealerCardsTopPaddingSource = 22f;
    public const float PlayerCardsTopPaddingSource = 22f;

    public const float DealerCardsYRatio = 0.18f;
    public const float PlayerCardsYRatio = 0.52f;
    public const float SingleHandSpacingRatio = 1.08f;
    // Multi-hand card step matches single-hand/dealer spacing by default.
    // Compression only kicks in if the full multi-hand layout cannot fit.
    public const float MultiHandCardStepRatio = SingleHandSpacingRatio;
    public const float MultiHandGapRatio = 1.2f;
    public const float MinMultiHandGapViewportRatio = 0.05f;
    public const float StackedInactiveCardStepRatio = 0.22f;
    public const float MinStackedInactiveCardStepRatio = 0.12f;
    public const float MinActiveCardStepRatio = 0.45f;
    public const float StackedHandGapRatio = 0.8f;
    public const float MinStackedHandGapViewportRatio = 0.015f;
    public const float TwoHandMinGapCardRatio = 0.35f;
    public const float CompressedMinHandGapCardRatio = 0.12f;
    public const float CompressedMinHandGapViewportRatio = 0.01f;
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

    public static float CalculateDealerCardsY(int viewportWidth, int viewportHeight, float cardHeight)
    {
        // Dealer cards sit in the top margin above the table.
        // Center the card at DealerCardsViewportCenterYRatio of viewport height.
        float centerY = viewportHeight * UIConstants.DealerCardsViewportCenterYRatio;
        return Math.Max(0f, centerY - cardHeight / 2f);
    }

    public static float CalculatePlayerCardsY(int viewportHeight)
    {
        return viewportHeight * PlayerCardsYRatio;
    }

    public static float CalculatePlayerCardsY(int viewportWidth, int viewportHeight, float cardHeight)
    {
        var tableLayout = CalculateTableLayout(viewportWidth, viewportHeight);
        float insuranceArcY = tableLayout.Top + ((InsuranceArc.CenterSource.Y + InsuranceArc.RadiusSource) * tableLayout.Scale);
        float topPadding = Math.Max(4f, PlayerCardsTopPaddingSource * tableLayout.Scale);
        float y = insuranceArcY + topPadding;
        float maxY = Math.Max(0f, viewportHeight - cardHeight);
        return Math.Min(y, maxY);
    }

    public static TableLayoutInfo CalculateTableLayout(int viewportWidth, int viewportHeight)
    {
        float naturalScale = MathF.Min(viewportWidth / TableSourceWidth, viewportHeight / TableSourceHeight);
        float maxScaleByWidth  = viewportWidth  * UIConstants.MaxTableWidthFraction  / TableSourceWidth;
        float maxScaleByHeight = viewportHeight * UIConstants.MaxTableHeightFraction / TableSourceHeight;
        float scale = MathF.Min(1f, MathF.Min(naturalScale, MathF.Min(maxScaleByWidth, maxScaleByHeight)));

        float width = TableSourceWidth * scale;
        float height = TableSourceHeight * scale;
        float left = (viewportWidth - width) / 2f;
        float top = (viewportHeight - height) / 2f;
        return new TableLayoutInfo(left, top, width, height, scale);
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

    public static float ComputeMultiHandWidth(IReadOnlyList<int> handCardCounts, Vector2 cardSize, IReadOnlyList<float> cardStepsByHand, float handGap)
    {
        ArgumentNullException.ThrowIfNull(handCardCounts);
        ArgumentNullException.ThrowIfNull(cardStepsByHand);

        if (handCardCounts.Count != cardStepsByHand.Count)
            throw new ArgumentException("Hand counts and card-step arrays must have equal length.", nameof(cardStepsByHand));

        float totalWidth = 0f;
        for (int i = 0; i < handCardCounts.Count; i++)
        {
            int cardCount = Math.Max(handCardCounts[i], 1);
            totalWidth += cardSize.X + cardStepsByHand[i] * (cardCount - 1);
        }

        if (handCardCounts.Count > 1)
            totalWidth += handGap * (handCardCounts.Count - 1);

        return totalWidth;
    }

    public static Vector2 ComputeAdaptiveMultiHandCardCenter(
        int viewportWidth,
        Vector2 cardSize,
        IReadOnlyList<int> handCardCounts,
        int handIndex,
        int cardIndexInHand,
        float centerY,
        int activeHandIndex)
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

        bool hasActiveHand = activeHandIndex >= 0 && activeHandIndex < handCardCounts.Count;
        if (!hasActiveHand)
        {
            // No active hand context: fall back to symmetric multi-hand layout.
            return ComputeMultiHandCardCenter(viewportWidth, cardSize, handCardCounts, handIndex, cardIndexInHand, centerY);
        }

        var halfCard = cardSize / 2f;
        float maxWidth = viewportWidth * 0.9f;

        float defaultStep = cardSize.X * MultiHandCardStepRatio;
        float stackedStep = cardSize.X * StackedInactiveCardStepRatio;
        float minStackedStep = cardSize.X * MinStackedInactiveCardStepRatio;
        float minActiveStep = cardSize.X * MinActiveCardStepRatio;

        float handGap = Math.Max(cardSize.X * MultiHandGapRatio, viewportWidth * MinMultiHandGapViewportRatio);
        handGap = Math.Max(handGap, GetTwoHandGapFloor(handCardCounts, cardSize));

        var perHandStep = new float[handCardCounts.Count];
        for (int h = 0; h < handCardCounts.Count; h++)
            perHandStep[h] = defaultStep;

        float totalWidth = ComputeMultiHandWidth(handCardCounts, cardSize, perHandStep, handGap);

        // If the fully-open layout does not fit, stack inactive hands first.
        if (totalWidth > maxWidth)
        {
            handGap = Math.Max(cardSize.X * StackedHandGapRatio, viewportWidth * MinStackedHandGapViewportRatio);
            handGap = Math.Max(handGap, GetTwoHandGapFloor(handCardCounts, cardSize));

            for (int h = 0; h < handCardCounts.Count; h++)
                perHandStep[h] = h == activeHandIndex ? defaultStep : stackedStep;

            totalWidth = ComputeMultiHandWidth(handCardCounts, cardSize, perHandStep, handGap);

            // If it still doesn't fit, compress gaps/inactive/active in that order.
            const int maxIterations = 48;
            int iteration = 0;
            while (totalWidth > maxWidth + 0.001f && iteration++ < maxIterations)
            {
                bool changed = false;

                float minGap = Math.Max(viewportWidth * 0.005f, 0f);
                if (handGap > minGap)
                {
                    float nextGap = Math.Max(minGap, handGap * 0.92f);
                    if (nextGap < handGap - 0.0001f)
                    {
                        handGap = nextGap;
                        changed = true;
                    }
                }

                for (int h = 0; h < handCardCounts.Count; h++)
                {
                    if (h == activeHandIndex)
                        continue;

                    if (perHandStep[h] > minStackedStep)
                    {
                        float next = Math.Max(minStackedStep, perHandStep[h] * 0.9f);
                        if (next < perHandStep[h] - 0.0001f)
                        {
                            perHandStep[h] = next;
                            changed = true;
                        }
                    }
                }

                if (perHandStep[activeHandIndex] > minActiveStep)
                {
                    float next = Math.Max(minActiveStep, perHandStep[activeHandIndex] * 0.93f);
                    if (next < perHandStep[activeHandIndex] - 0.0001f)
                    {
                        perHandStep[activeHandIndex] = next;
                        changed = true;
                    }
                }

                totalWidth = ComputeMultiHandWidth(handCardCounts, cardSize, perHandStep, handGap);
                if (!changed)
                    break;
            }
        }

        float handStartX = viewportWidth / 2f - totalWidth / 2f;
        for (int h = 0; h < handIndex; h++)
        {
            int count = Math.Max(handCardCounts[h], 1);
            handStartX += cardSize.X + perHandStep[h] * (count - 1) + handGap;
        }

        float cardCenterX = handStartX + halfCard.X + perHandStep[handIndex] * cardIndexInHand;
        return new Vector2(cardCenterX, centerY);
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
        handGap = Math.Max(handGap, GetTwoHandGapFloor(handCardCounts, cardSize));

        float totalWidth = ComputeMultiHandWidth(handCardCounts, cardSize, cardStep, handGap);
        float maxWidth = viewportWidth * 0.9f;
        if (totalWidth > maxWidth)
        {
            float fixedWidth = cardSize.X * handCardCounts.Count;
            float handGapUnits = Math.Max(handCardCounts.Count - 1, 0);
            float cardStepUnits = 0f;
            for (int i = 0; i < handCardCounts.Count; i++)
                cardStepUnits += Math.Max(handCardCounts[i], 1) - 1;

            handGap = Math.Max(cardSize.X * CompressedMinHandGapCardRatio, viewportWidth * CompressedMinHandGapViewportRatio);
            handGap = Math.Max(handGap, GetTwoHandGapFloor(handCardCounts, cardSize));
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

    private static float GetTwoHandGapFloor(IReadOnlyList<int> handCardCounts, Vector2 cardSize)
    {
        return handCardCounts.Count == 2 ? cardSize.X * TwoHandMinGapCardRatio : 0f;
    }
}

public readonly record struct TableLayoutInfo(
    float Left,
    float Top,
    float Width,
    float Height,
    float Scale);

public readonly record struct ArcLayoutInfo(
    Vector2 CenterSource,
    float RadiusSource,
    float StartAngleDeg,
    float EndAngleDeg);
