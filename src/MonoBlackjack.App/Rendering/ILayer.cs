using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoBlackjack.Rendering;

/// <summary>
/// A renderable layer in the scene. Layers are composited back-to-front by DrawOrder.
/// </summary>
public interface ILayer
{
    int DrawOrder { get; }
    bool Visible { get; set; }
    void Update(GameTime gameTime);
    void Draw(SpriteBatch spriteBatch);
    void HandleResize(Rectangle viewport);
}
