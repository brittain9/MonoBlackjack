using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoBlackjack.States
{
    internal class GameState : State
    {
        private List<Card> _cardDeck;

        public GameState(BlackjackGame game, GraphicsDevice graphicsDevice, ContentManager content) : base(game, graphicsDevice, content)
        {
            _cardDeck = new List<Card>();
            Texture2D texture = content.Load<Texture2D>("Cards/ace_of_spades2");
            var playingCard = new Card(texture)
            {
                Position = new Vector2(graphicsDevice.Viewport.Width / 2, graphicsDevice.Viewport.Height / 2),
                Size = new Vector2(200, 300)
            };
            playingCard.DestRect = new Rectangle((int)playingCard.Position.X, (int)playingCard.Position.Y, (int)playingCard.Size.X, (int)playingCard.Size.X);

            _cardDeck.Add(playingCard);
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            spriteBatch.Begin();

            foreach (var card in _cardDeck)
            {
                card.Draw(gameTime, spriteBatch);
            }

            spriteBatch.End();
        }

        public override void HandleResize(Rectangle vp)
        {

        }

        public override void PostUpdate(GameTime gameTime)
        {

        }

        public override void Update(GameTime gameTime)
        {

        }

        public void CreateDeck()
        {

        }
    }
}
