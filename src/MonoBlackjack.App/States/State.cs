using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MonoBlackjack
{
    public abstract class State : IDisposable
    {
        protected ContentManager _content;
        protected GraphicsDevice _graphicsDevice;
        protected BlackjackGame _game;

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

        protected float GetResponsiveScale(float baseScale)
        {
            var vp = _graphicsDevice.Viewport;
            var scaleFactor = MathF.Min(
                vp.Width / (float)UIConstants.BaselineWidth,
                vp.Height / (float)UIConstants.BaselineHeight);

            var logicalScale = Math.Clamp(
                baseScale * scaleFactor,
                UIConstants.TextMinScale,
                UIConstants.TextMaxScale);

            // Font atlas is baked larger to improve sampling quality, then drawn down.
            return logicalScale * UIConstants.FontSupersampleDrawScale;
        }

        public virtual void Dispose() { }
    }
}
