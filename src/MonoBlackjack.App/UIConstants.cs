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

    public const float BetCenterYRatio = 0.5f;
    public const float BetArrowWidthToActionButtonHeightRatio = 1.2f;
    public const float BetArrowSpacingToActionButtonWidthRatio = 0.8f;
    public const float DealButtonOffsetRatio = 0.08f;
    public const float RepeatBetButtonOffsetRatio = 0.14f;
    public const float BankruptButtonsYRatio = 0.55f;
}
