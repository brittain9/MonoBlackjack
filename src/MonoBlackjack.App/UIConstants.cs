using Microsoft.Xna.Framework;

namespace MonoBlackjack;

internal static class UIConstants
{
    public const int BaselineWidth = 1280;
    public const int BaselineHeight = 720;
    public const int MinWindowWidth = 800;
    public const int MinWindowHeight = 600;

    public const int CardWidth = 100;
    public const int CardHeight = 145;
    public static readonly Vector2 CardSize = new(CardWidth, CardHeight);
    public const float CardAspectRatio = CardWidth / (float)CardHeight;

    public const float TextMinScale = 0.5f;
    public const float TextMaxScale = 2.0f;
    public const float FontSupersampleFactor = 2.0f;
    public const float FontSupersampleDrawScale = 1.0f / FontSupersampleFactor;

    public const int MaxActionButtons = 5;
    public const float ButtonPaddingRatio = 0.01f;
    public const float MinButtonPadding = 10f;
    public const float MaxButtonPadding = 20f;
    public const float ActionButtonsViewportWidthRatio = 0.92f;
    public const float ActionButtonWidthToCardRatio = 1.2f;
    public const float ActionButtonHeightToCardRatio = 0.35f;
    public const float MinActionButtonWidth = 90f;
    public const float MaxActionButtonWidth = 220f;
    public const float MinActionButtonHeight = 34f;
    public const float MaxActionButtonHeight = 74f;
    public const float HandValueTopPadding = 8f;
    public const float ActionButtonsVerticalGapRatio = 0.015f;
    public const float MinActionButtonsVerticalGap = 6f;
    public const float MaxActionButtonsVerticalGap = 14f;
    public const float ActionButtonsBottomInsetRatio = 0.75f;

    public const float BetPanelHeightToActionButtonHeightRatio = 5.2f;
    public const float BetArrowSizeToActionButtonHeightRatio = 0.95f;
    public const float BetArrowVerticalOffsetToActionButtonHeightRatio = 1.05f;
    public const float DealButtonVerticalOffsetRatio = 0.0f;
    public const float RepeatBetButtonVerticalOffsetRatio = 0.13f;
    public const float BetPanelAmountOffsetToActionButtonHeightRatio = 1.55f;
    public const float BetPanelLabelOffsetToActionButtonHeightRatio = 2.2f;
    public const float BankruptButtonsYRatio = 0.55f;

    public const float BetPanelPreferredRightYRatio = 0.66f;
    public const float BetPanelRightInsetRatio = 0.02f;
    public const float BetPanelBottomInsetRatio = 0.03f;
    public const float BetPanelMinGameplayWidthRatio = 0.68f;
    public const int BetPanelRightRailMinViewportHeight = 620;
    public const float BetPanelSafeGapToTableRatio = 0.02f;

    public const float TableArcHalfSweepDegrees = 62f;
    public const float TableTopGapRatio = 0.03f;
    public const float TableBottomGapRatio = 0.045f;
    public const float TableMinTopGap = 14f;
    public const float TableMaxTopGap = 36f;
    public const float TableMinBottomGap = 18f;
    public const float TableMaxBottomGap = 46f;
    public const float TableMinOuterRadiusRatio = 0.19f;
    public const float TableMaxOuterRadiusRatio = 0.4f;
    public const float TableMiddleRadiusRatio = 0.84f;
    public const float TableInnerRadiusRatio = 0.69f;
    public const float TableSideInsetRatio = 0.02f;
}
