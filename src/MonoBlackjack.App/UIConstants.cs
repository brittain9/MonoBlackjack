namespace MonoBlackjack;

internal static class UIConstants
{
    public const int BaselineWidth = 1280;
    public const int BaselineHeight = 720;
    public const int MinWindowWidth = 800;
    public const int MinWindowHeight = 600;

    public const int BaseCardWidth = 100;
    public const int BaseCardHeight = 145;
    public const float CardAspectRatio = BaseCardWidth / (float)BaseCardHeight;

    public const float TextMinScale = 0.5f;
    public const float TextMaxScale = 2.0f;
    public const float FontSupersampleFactor = 2.0f;
    public const float FontSupersampleDrawScale = 1.0f / FontSupersampleFactor;

    public const float ButtonPaddingRatio = 0.012f;
    public const float DealButtonOffsetRatio = 0.08f;
    public const float RepeatBetButtonOffsetRatio = 0.14f;
}
