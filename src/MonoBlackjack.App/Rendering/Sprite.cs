using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoBlackjack.Rendering;

/// <summary>
/// Base drawable element with position, scale, rotation, and opacity.
/// Sprites live in layers and are sorted by ZOrder for draw ordering.
/// Coordinate system standard: Position is always the CENTER point.
/// </summary>
public class Sprite
{
    public Texture2D? Texture { get; set; }
    /// <summary>
    /// Center-anchor screen position.
    /// </summary>
    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; }
    public float Scale { get; set; } = 1f;
    public float ScaleX { get; set; } = 1f;
    public float Rotation { get; set; }
    public float Opacity { get; set; } = 1f;
    public bool Visible { get; set; } = true;
    public int ZOrder { get; set; }
    public float Depth { get; set; }

    /// <summary>
    /// Converts center-anchor Position into SpriteBatch's top-left destination rectangle.
    /// </summary>
    public Rectangle DestRect
    {
        get
        {
            var w = (int)(Size.X * Scale * ScaleX);
            var h = (int)(Size.Y * Scale);
            return new Rectangle(
                (int)Position.X - w / 2,
                (int)Position.Y - h / 2,
                w, h);
        }
    }

    public virtual void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible || Texture == null || Opacity <= 0f)
            return;

        spriteBatch.Draw(
            Texture,
            DestRect,
            null,
            Color.White * Opacity,
            Rotation,
            Vector2.Zero,
            SpriteEffects.None,
            Depth);
    }
}
