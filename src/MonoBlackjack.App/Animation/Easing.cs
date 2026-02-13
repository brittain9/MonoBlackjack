namespace MonoBlackjack.Animation;

/// <summary>
/// Static easing functions for tween animations.
/// Input t is normalized [0..1], output is the eased value.
/// </summary>
public static class Easing
{
    public static float Linear(float t) => t;

    public static float EaseInQuad(float t) => t * t;

    public static float EaseOutQuad(float t) => t * (2f - t);

    public static float EaseInOutQuad(float t) =>
        t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

    public static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * MathF.Pow(t - 1f, 3) + c1 * MathF.Pow(t - 1f, 2);
    }
}
