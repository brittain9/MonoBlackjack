using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoBlackjack.Rendering;

/// <summary>
/// A layer that holds and draws sprites sorted by ZOrder.
/// </summary>
public class SpriteLayer : ILayer
{
    private readonly List<Sprite> _sprites = [];
    private bool _needsSort;

    public int DrawOrder { get; }
    public bool Visible { get; set; } = true;
    public IReadOnlyList<Sprite> Sprites => _sprites;

    public SpriteLayer(int drawOrder)
    {
        DrawOrder = drawOrder;
    }

    public void Add(Sprite sprite)
    {
        _sprites.Add(sprite);
        _needsSort = true;
    }

    public void Remove(Sprite sprite)
    {
        _sprites.Remove(sprite);
    }

    public void Clear()
    {
        _sprites.Clear();
    }

    public void Update(GameTime gameTime) { }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        if (_needsSort)
        {
            _sprites.Sort((a, b) => a.ZOrder.CompareTo(b.ZOrder));
            _needsSort = false;
        }

        foreach (var sprite in _sprites)
            sprite.Draw(spriteBatch);
    }

    public void HandleResize(Rectangle viewport) { }
}
