using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Core;

namespace MonoBlackjack.Rendering;

/// <summary>
/// A sprite representing a playing card. Holds a domain Card reference
/// and can render face-up or face-down (back texture).
/// </summary>
public class CardSprite : Sprite
{
    public Card Card { get; }
    public bool FaceDown { get; set; }
    public Texture2D? BackTexture { get; set; }
    public Color BackTint { get; set; } = Color.White;

    public CardSprite(Card card)
    {
        Card = card;
        Size = CardRenderer.CardSize;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible || Opacity <= 0f)
            return;

        var texture = FaceDown && BackTexture != null ? BackTexture : Texture;
        if (texture == null)
            return;

        var drawColor = FaceDown && BackTexture != null
            ? BackTint
            : Color.White;

        spriteBatch.Draw(
            texture,
            DestRect,
            null,
            drawColor * Opacity,
            Rotation,
            Vector2.Zero,
            SpriteEffects.None,
            Depth);
    }
}
