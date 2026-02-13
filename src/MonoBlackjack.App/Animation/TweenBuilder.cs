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
}
