using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoBlackjack.Rendering;

public class TextSprite : Sprite
{
    public string Text { get; set; } = string.Empty;
    public SpriteFont? Font { get; set; }
    public Color TextColor { get; set; } = Color.White;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible || Font == null || string.IsNullOrEmpty(Text) || Opacity <= 0f)
            return;

        var measured = Font.MeasureString(Text);
        var origin = measured / 2f;

        spriteBatch.DrawString(
            Font,
            Text,
            Position,
            TextColor * Opacity,
            Rotation,
            origin,
            Scale,
            SpriteEffects.None,
            Depth);
    }
}
