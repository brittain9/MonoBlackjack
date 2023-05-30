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
    public class Player : IPlayer
    {
        public string Name { get; set; }
        public int Bank { get; set; }
        public List<List<Card>> Hands { get; set; } // List of lists is due to the fact that a player can split to have multiple hands

        public Player(ref List<Card> deck) : base(ref deck)
        {
            Hands = new List<List<Card>>();
            Hands.Add(CreateHand(ref deck));
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

        public static void SetHandPosition(List<Card> hand, Vector2 pos, int CardSpacing)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                hand[i].Position.X = pos.X + CardSpacing * i;
                hand[i].Position.Y = pos.Y;
                hand[i].DestRect = new Rectangle((int)hand[i].Position.X, (int)hand[i].Position.Y, (int)hand[i].Size.X, (int)hand[i].Size.Y);
            }
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            foreach (var card in Hands[0])
            {
                card.Draw(gameTime, spriteBatch);
            }
        }

        public override void Update(GameTime gameTime)
        {

        }
    }
}
