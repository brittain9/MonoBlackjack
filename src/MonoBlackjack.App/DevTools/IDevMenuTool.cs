using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoBlackjack.DevTools;

internal interface IDevMenuTool
{
    string Name { get; }

    void HandleResize(Rectangle panelBounds, Vector2 buttonSize);

    void Update(GameTime gameTime, KeyboardState currentKeyboardState, KeyboardState previousKeyboardState);

    void Draw(GameTime gameTime, SpriteBatch spriteBatch, SpriteFont font, Rectangle panelBounds, float textScale);
}
