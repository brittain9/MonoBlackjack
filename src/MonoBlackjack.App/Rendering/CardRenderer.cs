using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Core;

namespace MonoBlackjack.Rendering;

/// <summary>
/// Loads card textures and draws Card data objects.
/// This is the rendering boundary — game logic types come in, MonoGame draws go out.
/// </summary>
public class CardRenderer
{
    private readonly Dictionary<string, Texture2D> _textureCache = new();
    private Texture2D? _defaultBackTexture;

    // Card size in pixels. Preserves 500:726 source texture aspect ratio.
    public static readonly Vector2 CardSize = new(100, 145);

    public void LoadTextures(ContentManager content)
    {
        foreach (var suit in Enum.GetValues<Suit>())
        {
            foreach (var rank in Enum.GetValues<Rank>())
            {
                var card = new Card(rank, suit);
                _textureCache[card.AssetName] =
                    content.Load<Texture2D>($"Cards/{card.AssetName}");
            }
        }

        try
        {
            _defaultBackTexture = content.Load<Texture2D>("Cards/Backs/card_back_blue");
        }
        catch (ContentLoadException)
        {
            // Keep gameplay running even if optional back texture is missing.
            _defaultBackTexture = null;
        }
    }

    public void DrawCard(SpriteBatch spriteBatch, Card card, Vector2 position)
    {
        if (!_textureCache.TryGetValue(card.AssetName, out var texture))
            return;

        var destRect = new Rectangle(
            (int)position.X, (int)position.Y,
            (int)CardSize.X, (int)CardSize.Y);

        spriteBatch.Draw(texture, destRect, Color.White);
    }

    public void DrawHand(SpriteBatch spriteBatch, IReadOnlyList<Card> cards,
        Vector2 startPosition, float spacing)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            var pos = new Vector2(startPosition.X + spacing * i, startPosition.Y);
            DrawCard(spriteBatch, cards[i], pos);
        }
    }

    /// <summary>
    /// Factory method: creates a CardSprite with the correct face texture assigned.
    /// </summary>
    public CardSprite CreateCardSprite(Card card, bool faceDown = false)
    {
        var sprite = new CardSprite(card)
        {
            FaceDown = faceDown,
            BackTexture = _defaultBackTexture
        };

        if (_textureCache.TryGetValue(card.AssetName, out var texture))
            sprite.Texture = texture;

        return sprite;
    }
}
