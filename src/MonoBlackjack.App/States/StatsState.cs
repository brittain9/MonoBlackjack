using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MonoBlackjack;

/// <summary>
/// Statistics and data visualization screen.
/// TODO: Query SQLite for game history, display charts/tables, hand history viewer.
/// </summary>
internal class StatsState : State
{
    public StatsState(BlackjackGame game, GraphicsDevice graphicsDevice, ContentManager content)
        : base(game, graphicsDevice, content)
    {
        // TODO: Initialize database connection, load stats
    }

    public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        spriteBatch.Begin();
        // TODO: Draw stats panels (win/loss ratio, profit graph, hand history list)
        spriteBatch.End();
    }

    public override void Update(GameTime gameTime)
    {
        // TODO: Handle scrolling, filtering, return to menu button
    }

    public override void PostUpdate(GameTime gameTime) { }

    public override void HandleResize(Rectangle vp) { }
}
