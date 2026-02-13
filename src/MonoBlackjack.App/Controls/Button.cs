using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoBlackjack
{
    public class Button : MonoBlackjack.Component
    {
        private MouseState _currentMouse;
        private SpriteFont _font;
        private bool _isHovering;
        private MouseState _previousMouse;
        private Texture2D _texture;

        public event EventHandler? Click;
        public bool Clicked { get; private set; }
        public Color PenColor { get;set; }
        public Vector2 Position { get; set; }
        public Vector2 Size { get; set; }
        public Rectangle DestRect
        {
            get
            {
                return new Rectangle(
                    (int)(Position.X - Size.X / 2f),
                    (int)(Position.Y - Size.Y / 2f),
                    (int)Size.X,
                    (int)Size.Y);
            }
        }
        public string Text { get; set; } = "";
        public Texture2D Texture { get { return _texture; } }

        public Button(Texture2D texture, SpriteFont font)
        {
            _texture = texture;
            _font = font;
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            var color = Color.LightGray;
            if (_isHovering)
                color = Color.DarkSlateGray;

            spriteBatch.Draw(
                _texture,
                DestRect,
                null,
                color,
                0f,
                Vector2.Zero,
                SpriteEffects.None,
                0f);

            if(!string.IsNullOrEmpty(Text))
            {
                var textSize = _font.MeasureString(Text);
                var textPosition = new Vector2(
                    DestRect.Center.X - textSize.X / 2f,
                    DestRect.Center.Y - textSize.Y / 2f);

                spriteBatch.DrawString(
                    _font, 
                    Text, 
                    textPosition, 
                    PenColor,
                    0f, 
                    Vector2.Zero,
                    1f, 
                    SpriteEffects.None,
                    0f);
            }
        }

        public override void Update(GameTime gameTime)
        {
            _previousMouse = _currentMouse;
            _currentMouse = Mouse.GetState();

            var mouseRectangle = new Rectangle(_currentMouse.X, _currentMouse.Y, 1, 1);
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
    }
}
