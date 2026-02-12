using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Core;
using MonoBlackjack.Core.Players;
using MonoBlackjack.Rendering;

namespace MonoBlackjack;

internal class GameState : State
{
    private readonly Shoe _shoe;
    private readonly CardRenderer _cardRenderer;
    private readonly Human _player;
    private readonly Dealer _dealer;

    private float _cardSpacing;

    public GameState(BlackjackGame game, GraphicsDevice graphicsDevice, ContentManager content)
        : base(game, graphicsDevice, content)
    {
        _cardRenderer = new CardRenderer();
        _cardRenderer.LoadTextures(content);

        _shoe = new Shoe(GameConfig.NumberOfDecks);

        _player = new Human();
        _dealer = new Dealer();

        _player.DealInitialHand(_shoe);
        _dealer.DealInitialHand(_shoe);

        _cardSpacing = graphicsDevice.Viewport.Width / 10f;
    }

    public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        spriteBatch.Begin();

        var vp = _graphicsDevice.Viewport;

        // Dealer hand — top center
        var dealerPos = new Vector2(
            vp.Width / 2f - CardRenderer.CardSize.X / 2f,
            vp.Height / 6f);
        _cardRenderer.DrawHand(spriteBatch, _dealer.Hand.Cards, dealerPos, _cardSpacing);

        // Player hand — bottom center
        var playerPos = new Vector2(
            vp.Width / 2f - CardRenderer.CardSize.X / 2f,
            vp.Height - vp.Height / 4f);

        foreach (var hand in _player.Hands)
        {
            _cardRenderer.DrawHand(spriteBatch, hand.Cards, playerPos, _cardSpacing);
        }

        spriteBatch.End();
    }

    public override void HandleResize(Rectangle vp)
    {
        _cardSpacing = vp.Width / 10f;
    }

    public override void PostUpdate(GameTime gameTime) { }

    public override void Update(GameTime gameTime) { }
}
