using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MonoBlackjack;

/// <summary>
/// Post-game screen showing round results, winnings, and option to continue or return to menu.
/// TODO: Display hand outcomes, payout amounts, running bankroll.
/// </summary>
internal class PostGameState : State
{
    public PostGameState(BlackjackGame game, GraphicsDevice graphicsDevice, ContentManager content)
        : base(game, graphicsDevice, content)
    {
        // TODO: Accept round results as constructor parameter
    }

    public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        spriteBatch.Begin();
        // TODO: Draw round summary, payout, buttons (Next Round, Return to Menu)
        spriteBatch.End();
    }

    public override void Update(GameTime gameTime)
    {
        // TODO: Handle button clicks
    }

    public override void PostUpdate(GameTime gameTime) { }

    public override void HandleResize(Rectangle vp) { }
}
