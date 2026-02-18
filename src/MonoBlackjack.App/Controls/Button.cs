using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoBlackjack
{
    /// <summary>
    /// UI button using the global center-anchor coordinate standard.
    /// Position is the center point; DestRect performs center-to-top-left conversion for drawing.
    /// </summary>
    public class Button : MonoBlackjack.Component
    {
        private readonly SpriteFont _font;
        private bool _isHovering;
        private readonly Texture2D _texture;
        private Vector2 _size;
        private string _text = string.Empty;
        private bool _textLayoutDirty = true;
        private Vector2 _cachedTextSize;
        private Vector2 _cachedScaledTextSize;
        private float _cachedTextScale = 1f;

        public event EventHandler? Click;
        public bool Clicked { get; private set; }
        public Color PenColor { get; set; }

        /// <summary>
        /// Center-anchor screen position.
        /// </summary>
        public Vector2 Position { get; set; }

        public Vector2 Size
        {
            get => _size;
            set
            {
                if (_size == value)
                    return;

                _size = value;
                _textLayoutDirty = true;
            }
        }

        public Rectangle DestRect
        {
            get
            {
                // SpriteBatch destination rectangles are top-left anchored.
                return new Rectangle(
                    (int)(Position.X - Size.X / 2f),
                    (int)(Position.Y - Size.Y / 2f),
                    (int)Size.X,
                    (int)Size.Y);
            }
        }

        public string Text
        {
            get => _text;
            set
            {
                value ??= string.Empty;
                if (string.Equals(_text, value, StringComparison.Ordinal))
                    return;

                _text = value;
                _textLayoutDirty = true;
            }
        }

        public Texture2D Texture => _texture;

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

            if (!string.IsNullOrEmpty(Text))
            {
                UpdateTextLayoutCache();
                var textPosition = new Vector2(
                    DestRect.Center.X - _cachedScaledTextSize.X / 2f,
                    DestRect.Center.Y - _cachedScaledTextSize.Y / 2f);

                spriteBatch.DrawString(
                    _font,
                    Text,
                    textPosition,
                    PenColor,
                    0f,
                    Vector2.Zero,
                    _cachedTextScale,
                    SpriteEffects.None,
                    0f);
            }
        }

        private void UpdateTextLayoutCache()
        {
            if (!_textLayoutDirty)
                return;

            _cachedTextSize = _font.MeasureString(Text);
            if (_cachedTextSize.X <= 0f || _cachedTextSize.Y <= 0f)
            {
                _cachedTextScale = UIConstants.FontSupersampleDrawScale;
                _cachedScaledTextSize = Vector2.Zero;
                _textLayoutDirty = false;
                return;
            }

            const float padding = 8f;
            var maxWidth = Math.Max(1f, DestRect.Width - padding * 2f);
            var maxHeight = Math.Max(1f, DestRect.Height - padding * 2f);
            var fitScale = Math.Min(maxWidth / _cachedTextSize.X, maxHeight / _cachedTextSize.Y);
            var minScale = UIConstants.TextMinScale * UIConstants.FontSupersampleDrawScale;
            var maxScale = UIConstants.FontSupersampleDrawScale;
            _cachedTextScale = Math.Clamp(fitScale, minScale, maxScale);
            _cachedScaledTextSize = _cachedTextSize * _cachedTextScale;
            _textLayoutDirty = false;
        }

        public override void Update(GameTime gameTime, in MouseFrameSnapshot mouseSnapshot)
        {
            _isHovering = mouseSnapshot.CursorRect.Intersects(DestRect);
            Clicked = _isHovering && mouseSnapshot.LeftReleasedThisFrame;

            if (Clicked)
                Click?.Invoke(this, EventArgs.Empty);
        }
    }
}
