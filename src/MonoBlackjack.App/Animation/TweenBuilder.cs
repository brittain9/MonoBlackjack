using Microsoft.Xna.Framework;
using MonoBlackjack.Rendering;

namespace MonoBlackjack.Animation;

/// <summary>
/// Static factory for creating common sprite tweens.
/// </summary>
public static class TweenBuilder
{
    public static Tween MoveTo(Sprite sprite, Vector2 target, float duration,
        float delay = 0f, Func<float, float>? ease = null)
    {
        Vector2? start = null;
        ease ??= Easing.EaseOutQuad;
        return new Tween(duration, delay, ease,
            t =>
            {
                start ??= sprite.Position;
                sprite.Position = Vector2.Lerp(start.Value, target, t);
            });
    }

    public static Tween FadeTo(Sprite sprite, float targetOpacity, float duration,
        float delay = 0f, Func<float, float>? ease = null)
    {
        float? start = null;
        ease ??= Easing.Linear;
        return new Tween(duration, delay, ease,
            t =>
            {
                start ??= sprite.Opacity;
                sprite.Opacity = start.Value + (targetOpacity - start.Value) * t;
            });
    }

    public static Tween ScaleTo(Sprite sprite, float targetScale, float duration,
        float delay = 0f, Func<float, float>? ease = null)
    {
        float? start = null;
        ease ??= Easing.EaseOutQuad;
        return new Tween(duration, delay, ease,
            t =>
            {
                start ??= sprite.Scale;
                sprite.Scale = start.Value + (targetScale - start.Value) * t;
            });
    }

    public static Tween FlipX(Sprite sprite, float duration, float delay,
        Action? onMidpoint = null)
    {
        bool midpointFired = false;

        return new Tween(duration, delay, Easing.Linear,
            t =>
            {
                float scaleX;
                if (t < 0.5f)
                {
                    // First half: 1 → 0 with ease-in
                    float phase = t * 2f;
                    scaleX = 1f - Easing.EaseInQuad(phase);
                }
                else
                {
                    if (!midpointFired)
                    {
                        midpointFired = true;
                        onMidpoint?.Invoke();
                    }

                    // Second half: 0 → 1 with ease-out
                    float phase = (t - 0.5f) * 2f;
                    scaleX = Easing.EaseOutQuad(phase);
                }

                // Center-anchored sprites stay centered as ScaleX changes
                sprite.ScaleX = scaleX;
            });
    }
}
