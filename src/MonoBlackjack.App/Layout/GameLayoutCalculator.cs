using Microsoft.Xna.Framework;

namespace MonoBlackjack.Layout;

public enum BetPanelPlacementMode
{
    RightRail,
    BottomCenter
}

public readonly record struct BetPanelLayout(
    BetPanelPlacementMode PlacementMode,
    Rectangle PanelRect,
    Vector2 PanelCenter,
    float PanelWidth,
    float PanelHeight);

public readonly record struct TableSurfaceLayout(
    Rectangle ViewportBounds,
    Rectangle GameplayBounds,
    Rectangle ReservedUiBounds,
    Vector2 Center,
    float OuterRadius,
    float MiddleRadius,
    float InnerRadius,
    float TopAnchorRadians,
    float ArcHalfSweepRadians,
    float DealerCardsBottomY,
    float PlayerCardsTopY)
{
    public float ArcStartRadians => TopAnchorRadians - ArcHalfSweepRadians;

    public float ArcEndRadians => TopAnchorRadians + ArcHalfSweepRadians;

    public float LeftEdgeX => Center.X + MathF.Cos(ArcStartRadians) * OuterRadius;

    public float RightEdgeX => Center.X + MathF.Cos(ArcEndRadians) * OuterRadius;

    public float TopEdgeY => Center.Y - OuterRadius;
}

public readonly record struct GameSurfaceLayout(
    Rectangle ViewportBounds,
    Rectangle GameplayBounds,
    BetPanelLayout BetPanel,
    TableSurfaceLayout Table);

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

    public static GameSurfaceLayout CalculateGameSurfaceLayout(
        int viewportWidth,
        int viewportHeight,
        Vector2 actionButtonSize,
        float actionButtonPadding)
    {
        if (viewportWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportWidth), viewportWidth, "Viewport width must be positive.");
        if (viewportHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportHeight), viewportHeight, "Viewport height must be positive.");

        var viewportBounds = new Rectangle(0, 0, viewportWidth, viewportHeight);
        float panelWidth = actionButtonSize.X + actionButtonPadding * 2f;
        float panelHeight = actionButtonSize.Y * UIConstants.BetPanelHeightToActionButtonHeightRatio;

        float sideInset = Math.Max(12f, viewportWidth * UIConstants.BetPanelRightInsetRatio);
        float bottomInset = Math.Max(12f, viewportHeight * UIConstants.BetPanelBottomInsetRatio);
        float safeGap = Math.Max(10f, viewportWidth * UIConstants.BetPanelSafeGapToTableRatio);

        var rightCenter = new Vector2(
            viewportWidth - sideInset - panelWidth / 2f,
            Math.Clamp(
                viewportHeight * UIConstants.BetPanelPreferredRightYRatio,
                sideInset + panelHeight / 2f,
                viewportHeight - sideInset - panelHeight / 2f));
        var rightRect = CreateCenteredRect(rightCenter, panelWidth, panelHeight);

        float gameplayRightInRightRail = rightRect.Left - safeGap;
        bool useRightRail = gameplayRightInRightRail >= viewportWidth * UIConstants.BetPanelMinGameplayWidthRatio
            && viewportHeight >= UIConstants.BetPanelRightRailMinViewportHeight;

        BetPanelLayout betPanel;
        Rectangle gameplayBounds;
        if (useRightRail)
        {
            betPanel = new BetPanelLayout(BetPanelPlacementMode.RightRail, rightRect, rightCenter, panelWidth, panelHeight);
            gameplayBounds = new Rectangle(
                viewportBounds.Left,
                viewportBounds.Top,
                Math.Max(1, (int)MathF.Floor(gameplayRightInRightRail)),
                viewportBounds.Height);
        }
        else
        {
            var bottomCenter = new Vector2(
                viewportWidth / 2f,
                viewportHeight - bottomInset - panelHeight / 2f);
            var bottomRect = CreateCenteredRect(bottomCenter, panelWidth, panelHeight);
            betPanel = new BetPanelLayout(BetPanelPlacementMode.BottomCenter, bottomRect, bottomCenter, panelWidth, panelHeight);

            gameplayBounds = new Rectangle(
                viewportBounds.Left,
                viewportBounds.Top,
                viewportBounds.Width,
                Math.Max(1, bottomRect.Top - (int)MathF.Ceiling(safeGap)));
        }

        var table = CalculateTableSurfaceLayout(
            viewportBounds,
            gameplayBounds,
            betPanel.PanelRect,
            useRightRail);

        return new GameSurfaceLayout(viewportBounds, gameplayBounds, betPanel, table);
    }

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

    private static TableSurfaceLayout CalculateTableSurfaceLayout(
        Rectangle viewportBounds,
        Rectangle gameplayBounds,
        Rectangle reservedUiBounds,
        bool hasRightRail)
    {
        float topAnchor = MathHelper.ToRadians(270f);
        float halfSweep = MathHelper.ToRadians(UIConstants.TableArcHalfSweepDegrees);
        float edgeCos = MathF.Abs(MathF.Cos(topAnchor + halfSweep));

        var cardSize = CalculateCardSize(viewportBounds.Height);
        float dealerBottom = CalculateDealerCardsY(viewportBounds.Height) + cardSize.Y;
        float playerTop = CalculatePlayerCardsY(viewportBounds.Height);
        float topGap = Math.Clamp(
            viewportBounds.Height * UIConstants.TableTopGapRatio,
            UIConstants.TableMinTopGap,
            UIConstants.TableMaxTopGap);
        float bottomGap = Math.Clamp(
            viewportBounds.Height * UIConstants.TableBottomGapRatio,
            UIConstants.TableMinBottomGap,
            UIConstants.TableMaxBottomGap);

        float topTarget = dealerBottom + topGap;
        float bottomLimit = playerTop - bottomGap;
        float verticalDenominator = Math.Max(0.15f, 1f - MathF.Cos(halfSweep));
        float radiusByVertical = Math.Max(18f, (bottomLimit - topTarget) / verticalDenominator);
        float maxRadius = MathF.Min(viewportBounds.Width, viewportBounds.Height) * UIConstants.TableMaxOuterRadiusRatio;
        float preferredRadius = MathF.Min(viewportBounds.Width, viewportBounds.Height) * 0.31f;
        float radius = Math.Min(Math.Min(preferredRadius, maxRadius), radiusByVertical);

        float sideInset = Math.Max(8f, viewportBounds.Width * UIConstants.TableSideInsetRatio);
        float leftBoundary = Math.Max(gameplayBounds.Left + sideInset, viewportBounds.Left + sideInset);
        float rightBoundary = Math.Min(gameplayBounds.Right - sideInset, viewportBounds.Right - sideInset);

        if (hasRightRail && reservedUiBounds.Width > 0)
            rightBoundary = Math.Min(rightBoundary, reservedUiBounds.Left - sideInset);

        if (edgeCos < 0.1f)
            edgeCos = 0.1f;

        float centerX = viewportBounds.Width / 2f;
        float maxByLeft = (centerX - leftBoundary) / edgeCos;
        float maxByRight = (rightBoundary - centerX) / edgeCos;
        float maxByHorizontal = Math.Max(0f, Math.Min(maxByLeft, maxByRight));
        radius = Math.Min(radius, maxByHorizontal);

        if (radius < 24f)
            radius = 24f;

        float centerY = topTarget + radius;
        float outerRadius = radius;
        float middleRadius = outerRadius * UIConstants.TableMiddleRadiusRatio;
        float innerRadius = outerRadius * UIConstants.TableInnerRadiusRatio;

        return new TableSurfaceLayout(
            viewportBounds,
            gameplayBounds,
            reservedUiBounds,
            new Vector2(centerX, centerY),
            outerRadius,
            middleRadius,
            innerRadius,
            topAnchor,
            halfSweep,
            dealerBottom,
            playerTop);
    }

    private static Rectangle CreateCenteredRect(Vector2 center, float width, float height)
    {
        return new Rectangle(
            (int)MathF.Round(center.X - width / 2f),
            (int)MathF.Round(center.Y - height / 2f),
            (int)MathF.Round(width),
            (int)MathF.Round(height));
    }
}
