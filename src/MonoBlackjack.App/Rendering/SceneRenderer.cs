using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoBlackjack.Rendering;

/// <summary>
/// Composites all layers back-to-front. Manages Begin/End and delegates Update/Draw/Resize.
/// </summary>
public class SceneRenderer
{
    private readonly SortedList<int, ILayer> _layers = new();

    public void AddLayer(ILayer layer)
    {
        _layers[layer.DrawOrder] = layer;
    }

    public void Update(GameTime gameTime)
    {
        foreach (var layer in _layers.Values)
            layer.Update(gameTime);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        foreach (var layer in _layers.Values)
            layer.Draw(spriteBatch);

        spriteBatch.End();
    }

    public void HandleResize(Rectangle viewport)
    {
        foreach (var layer in _layers.Values)
            layer.HandleResize(viewport);
    }
}
