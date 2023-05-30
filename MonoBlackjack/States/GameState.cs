using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack;

namespace MonoBlackjack
{
    internal class GameState : State
    {
        private int CardSpacing;

        public List<Card> CardDeck;
        public List<IPlayer> Players;

        public GameState(BlackjackGame game, GraphicsDevice graphicsDevice, ContentManager content) : base(game, graphicsDevice, content)
        {
            Card.LoadTextures(content);
            CardDeck = Card.CreateDeck();
            Card.ShuffleDeck(ref CardDeck);

            InitializePlayers();
        }

        public void InitializePlayers()
        {
            Player player = new Player(ref CardDeck);
            Dealer dealer = new Dealer(ref CardDeck);

            CardSpacing = _graphicsDevice.Viewport.Width / 10;
            dealer.SetHandPosition(new Vector2(
                _graphicsDevice.Viewport.Width / 2 - (dealer.Hand[0].Size.X / 2),
                _graphicsDevice.Viewport.Height / 6),
                CardSpacing);

            Player.SetHandPosition(player.Hands[0],
                new Vector2(_graphicsDevice.Viewport.Width / 2 - (dealer.Hand[0].Size.X / 2),
                _graphicsDevice.Viewport.Height - _graphicsDevice.Viewport.Height / 4),
                CardSpacing);

            Players = new List<IPlayer>()
            {
                player, dealer
            };
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            spriteBatch.Begin();

            foreach (var p in Players)
            {
                p.Draw(gameTime, spriteBatch);
            }

            spriteBatch.End();
        }

        public override void HandleResize(Rectangle vp)
        {
            InitializePlayers();
        }

        public override void PostUpdate(GameTime gameTime)
        {

        }

        public override void Update(GameTime gameTime)
        {

        }
    }
}
