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
        var start = sprite.Position;
        ease ??= Easing.EaseOutQuad;
        return new Tween(duration, delay, ease,
            t => sprite.Position = Vector2.Lerp(start, target, t));
    }

    public static Tween FadeTo(Sprite sprite, float targetOpacity, float duration,
        float delay = 0f, Func<float, float>? ease = null)
    {
        var start = sprite.Opacity;
        ease ??= Easing.Linear;
        return new Tween(duration, delay, ease,
            t => sprite.Opacity = start + (targetOpacity - start) * t);
    }

    public static Tween ScaleTo(Sprite sprite, float targetScale, float duration,
        float delay = 0f, Func<float, float>? ease = null)
    {
        var start = sprite.Scale;
        ease ??= Easing.EaseOutQuad;
        return new Tween(duration, delay, ease,
            t => sprite.Scale = start + (targetScale - start) * t);
    }
}
