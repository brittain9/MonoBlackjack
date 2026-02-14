using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoBlackjack.Rendering;

/// <summary>
/// Base drawable element with position, scale, rotation, and opacity.
/// Sprites live in layers and are sorted by ZOrder for draw ordering.
/// </summary>
public class Sprite
{
    public Texture2D? Texture { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; }
    public float Scale { get; set; } = 1f;
    public float ScaleX { get; set; } = 1f;
    public float Rotation { get; set; }
    public float Opacity { get; set; } = 1f;
    public bool Visible { get; set; } = true;
    public int ZOrder { get; set; }
    public float Depth { get; set; }

    public virtual void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible || Texture == null || Opacity <= 0f)
            return;

        var destRect = new Rectangle(
            (int)Position.X, (int)Position.Y,
            (int)(Size.X * Scale * ScaleX), (int)(Size.Y * Scale));

        spriteBatch.Draw(
            Texture,
            destRect,
            null,
            Color.White * Opacity,
            Rotation,
            Vector2.Zero,
            SpriteEffects.None,
            Depth);
    }
}
