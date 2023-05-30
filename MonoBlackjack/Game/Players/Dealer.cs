using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoBlackjack
{
    public class Dealer : IPlayer
    {
        public List<Card> Hand;

        public Dealer(ref List<Card> deck) : base(ref deck)
        {
            Hand = CreateHand(ref deck);
        }

        public List<Card> CreateHand(ref List<Card> deck)
        {
            var hand = new List<Card>()
            {
                Card.DrawCard(ref deck),
                Card.DrawCard(ref deck)
            };
            return hand;
        }

        public void SetHandPosition(Vector2 pos, int CardSpacing)
        {
            for(int i = 0; i < Hand.Count; i++)
            {
                Hand[i].Position.X = pos.X + CardSpacing * i;
                Hand[i].Position.Y = pos.Y;
                Hand[i].DestRect = new Rectangle((int)Hand[i].Position.X, (int)Hand[i].Position.Y, (int)Hand[i].Size.X, (int)Hand[i].Size.Y);
            }
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            foreach (var card in Hand)
            {
                card.Draw(gameTime, spriteBatch);
            }
        }

        public override void Update(GameTime gameTime)
        {
        }
    }
}
