using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using MonoBlackjack;


namespace MonoBlackjack
{
    /*
     Helpful diagram of viewport
 (0, 0)-----------------------(width, 0)
  |                             |
  |                             |
  |                             |
  |                             |
  |                             |
  |                             |
  |                             |
(0, height)--------------(width, height)
    */
    public class BlackjackGame : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        private State _currentState;
        private State _nextState;

        public void ChangeState(State state)
        {
            // why not make public, because we can do more checks in this method
            _nextState = state;
        }

        public BlackjackGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += WindowClientSizeChangedHandler;
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;

            _graphics.ApplyChanges();

            IsMouseVisible = true;
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _currentState = new MenuState(this, _graphics.GraphicsDevice, Content);
        }

        protected override void Update(GameTime gameTime)
        {
            if(_nextState != null)
            {
                _currentState = _nextState;

                _nextState = null;
            }
            _currentState.Update(gameTime);
            _currentState.PostUpdate(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DarkGreen);
            _currentState.Draw(gameTime, _spriteBatch);

            base.Draw(gameTime);
        }

        private void WindowClientSizeChangedHandler(object sender, System.EventArgs e)
        {
          _currentState.HandleResize(Window.ClientBounds);
        }
    }

    public class Program
    {
        public static void Main()
        {
           var game = new MonoBlackjack.BlackjackGame();
           game.Run();
        }
    }
}