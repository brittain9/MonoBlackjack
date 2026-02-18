using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoBlackjack
{
    public abstract class State : IDisposable
    {
        protected ContentManager _content;
        protected GraphicsDevice _graphicsDevice;
        protected BlackjackGame _game;
        protected KeyboardState _currentKeyboardState;
        protected KeyboardState _previousKeyboardState;

        public abstract void Draw(GameTime gameTime, SpriteBatch spriteBatch);
        public abstract void PostUpdate(GameTime gameTime);
        public State(BlackjackGame game, GraphicsDevice graphicsDevice, ContentManager content)
        {
            _game = game;
            _graphicsDevice = graphicsDevice;
            _content = content;
        }
        public abstract void Update(GameTime gameTime);

        public abstract void HandleResize(Rectangle vp);

        protected void CaptureKeyboardState()
        {
            _currentKeyboardState = Keyboard.GetState();
        }

        protected void CommitKeyboardState()
        {
            _previousKeyboardState = _currentKeyboardState;
        }

        protected bool WasKeyJustPressed(Keys key)
        {
            return _currentKeyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        protected float GetResponsiveScale(float baseScale)
        {
            var vp = _graphicsDevice.Viewport;
            var scaleFactor = MathF.Min(
                vp.Width / (float)UIConstants.BaselineWidth,
                vp.Height / (float)UIConstants.BaselineHeight);
            var runtimeScale = _game?.RuntimeGraphicsSettings.FontScaleMultiplier ?? 1.0f;

            var logicalScale = Math.Clamp(
                baseScale * scaleFactor * runtimeScale,
                UIConstants.TextMinScale,
                UIConstants.TextMaxScale);

            // Font atlas is baked larger to improve sampling quality, then drawn down.
            return logicalScale * UIConstants.FontSupersampleDrawScale;
        }

        public virtual void Dispose() { }
    }
}
