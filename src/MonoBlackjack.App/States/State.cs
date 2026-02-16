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

        public virtual void Dispose() { }
    }
}
