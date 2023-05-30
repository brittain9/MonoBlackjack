using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoBlackjack
{
    public class Button : MonoBlackjack.Component
    {
        #region Fields
        private MouseState _currentMouse;
        private SpriteFont _font;
        private bool _isHovering;
        private MouseState _previousMouse;
        private Texture2D _texture;
        #endregion

        #region Properties
        public event EventHandler Click;
        public bool Clicked { get; private set; }
        public Color PenColor { get;set; }
        public Vector2 Position { get; set; }
        public Vector2 Size { get; set; }
        public Rectangle DestRect
        {
            get
            {
                return new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y);
            }
            set {}
        }
        public string Text { get; set; }

        public Texture2D Texture { get { return _texture; } }

        #endregion

        #region Methods
        public Button(Texture2D texture, SpriteFont font)
        {
            _texture = texture;
            _font = font;
            PenColor = Color.Black;
            Size = new Vector2(200, 40);
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            var color = Color.White;
            if (_isHovering)
                color = Color.Gray;

            spriteBatch.Draw(
                _texture,
                DestRect,
                null,
                color,
                0f,
                new Vector2(_texture.Width / 2, _texture.Height / 2),
                SpriteEffects.None,
                0f);

            if(!string.IsNullOrEmpty(Text))
            {
                spriteBatch.DrawString(
                    _font, 
                    Text, 
                    new Vector2(DestRect.X, DestRect.Y), 
                    PenColor,
                    0f, 
                    new Vector2((_font.MeasureString(Text).X / 2), (_font.MeasureString(Text).Y / 2)),
                    1f, 
                    SpriteEffects.None,
                    0f);
            }
        }

        public override void Update(GameTime gameTime)
        {
            _previousMouse = _currentMouse;
            _currentMouse = Mouse.GetState();

            // Since the button is centered on the size 
            var mouseRectangle = new Rectangle(_currentMouse.X + DestRect.Width / 2, _currentMouse.Y + DestRect.Height / 2, 1, 1);
            _isHovering = false;

            if (mouseRectangle.Intersects(DestRect))
            {
                _isHovering = true;

                if(_currentMouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed)
                {
                    Click?.Invoke(this, new EventArgs());
                }
            }
        }

        public void Repos(int x, int y)
        {
            Position = new Vector2(x,y);
            DestRect = new Rectangle(x, y, (int)Size.X, (int)Size.Y);
        }
        #endregion
    }
}