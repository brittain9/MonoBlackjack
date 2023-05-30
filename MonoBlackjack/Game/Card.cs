using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoBlackjack
{
    public class Card
    {
        private Texture2D _texture;
        
        public Vector2 Position;
        public Vector2 Size { get; set; }

        // Destination Rect is made up of position, then the size
        public Rectangle DestRect;

        public Card(Texture2D texture)
        {
            _texture = texture;
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(
                _texture,
                DestRect,
                null,
                Color.White,
                0f,
                new Vector2(_texture.Width / 2, _texture.Height / 2),
                SpriteEffects.None,
                0f);
        }

    }
}