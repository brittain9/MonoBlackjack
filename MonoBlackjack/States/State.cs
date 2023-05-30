using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoBlackjack
{
    public abstract class State
    {
        #region Field
        protected ContentManager _content;
        protected GraphicsDevice _graphicsDevice;
        protected BlackjackGame _game;
        // scale the game to be at a correct size for a given resolution
        protected int _scale;
        #endregion

        #region Methods
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
        #endregion
    }
}
