using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MonoBlackjack
{
    public enum Suit
    {
        hearts,
        diamonds,
        clubs,
        spades
    }

    public enum Rank
    {
        ace = 1,
        two,
        three,
        four,
        five,
        six,
        seven,
        eight,
        nine,
        ten,
        jack,
        queen,
        king
    }

    public class Card
    {

        private static Dictionary<string, Texture2D> TextureCache;
        public static void LoadTextures(ContentManager content)
        {
            TextureCache = CacheCardTextures(content);
        }

        private Suit _suit;
        private Rank _rank;

        public Vector2 Position;
        public Vector2 Size;
        public Rectangle DestRect; // made up of position, then the size

        public Texture2D Texture;
        public string AssetName;

        public Card(Rank r, Suit s)
        {
            _suit = s;
            _rank = r;
            AssetName = GetCardAsset(r, s);
            Size = new Vector2(100, 200);
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            TextureCache.TryGetValue(AssetName, out Texture);
            spriteBatch.Draw(
                Texture,
                DestRect,
                null,
                Color.White,
                0f,
                new Vector2(Texture.Width / 2, Texture.Height / 2),
                SpriteEffects.None,
                0f);
        }

        public int RankValue()
        {
            switch (_rank)
            {
                case Rank.ace:
                    return 1;
                case Rank.two:
                    return 2;
                case Rank.three:
                    return 3;
                case Rank.four:
                    return 4;
                case Rank.five:
                    return 5;
                case Rank.six:
                    return 6;
                case Rank.seven:
                    return 7;
                case Rank.eight:
                    return 8;
                case Rank.nine:
                    return 9;
                case Rank.ten:
                case Rank.jack:
                case Rank.queen:
                case Rank.king:
                    return 10;
            }
            return 0;
        } 

        private static Rank[] allRanks =
        {
                Rank.ace,
                Rank.two,
                Rank.three,
                Rank.four,
                Rank.five,
                Rank.six,
                Rank.seven,
                Rank.eight,
                Rank.nine,
                Rank.ten,
                Rank.jack,
                Rank.queen,
                Rank.king
        };
        private static Suit[] allSuits =
        {
                Suit.hearts,
                Suit.diamonds,
                Suit.clubs,
                Suit.spades
        };

        public static List<Card> CreateDeck()
        {
            List<Card> cards = new List<Card>();
            for (int i = 0; i < Globals.NumberOfDecks; i++)
            {
                foreach (var rank in allRanks)
                {
                    foreach (var suit in allSuits)
                    {
                        Card card = new Card(rank, suit);
                        cards.Add(card);
                    }
                }
            }
            return cards;
        }

        public static void ShuffleDeck(ref List<Card> cards)
        {
            Random rng = new Random();

            int n = cards.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                Card tempCard = cards[k];
                cards[k] = cards[n];
                cards[n] = tempCard;
            }
        }

        public static Card DrawCard(ref List<Card> cards)
        {
            if (cards == null || cards.Count == 0)
            {
                cards = CreateDeck();
            }
            Card card = cards[0];
            cards.Remove(cards[0]);
            return card;
        }

        public static int GetCardsValue(List<Card> cards)
        {
            // Return rank value of a list of cards
            int value = 0;
            bool hasAce = false;

            foreach (var card in cards)
            {
                value += card.RankValue();
                if (card.RankValue() == ((int)Rank.ace))
                {
                    hasAce = true;
                }
            }
            if (hasAce && (value + Globals.AceExtraValue) <= Globals.BustNumber)
                value += Globals.AceExtraValue;
            return value;
        }

        private static Dictionary<string, Texture2D> CacheCardTextures(ContentManager content)
        {
            Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
            foreach (var rank in allRanks)
            {
                foreach (var suit in allSuits)
                {
                    string asset = GetCardAsset(rank, suit);
                    textureCache.Add(asset, content.Load<Texture2D>($"Cards/{asset}"));
                }
            }
            return textureCache;
        }

        private static string GetCardAsset(Rank r, Suit s)
        {
            // Get the texture from our card pack that uses format {rank}_of_{suit}.png
            StringBuilder asset = new StringBuilder();
            switch (r)
            {
                case Rank.jack:
                case Rank.queen:
                case Rank.king:
                case Rank.ace:
                    asset.Append(r.ToString());
                    break;
                default:
                    asset.Append(((int)r));
                    break;
            }
            asset.Append($"_of_{s.ToString()}");
            return asset.ToString();
        }
    }
}