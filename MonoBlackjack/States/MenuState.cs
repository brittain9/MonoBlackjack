using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.States;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace MonoBlackjack
{
    internal class MenuState : State
    {
        private List<Component> _components;

        public MenuState(BlackjackGame game, GraphicsDevice graphicsDevice, ContentManager content) : base(game, graphicsDevice, content)
        {
            IntializeButtons(graphicsDevice, content);
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            spriteBatch.Begin();

            foreach (var component in _components)
            {
                component.Draw(gameTime, spriteBatch);
            }

            spriteBatch.End();
        }

        public override void PostUpdate(GameTime gameTime)
        {
            // remove sprites if they are not needed
            Console.WriteLine("Post update");
        }

        public override void Update(GameTime gameTime)
        {
            foreach (var component in _components)
            {
                component.Update(gameTime);
            }
        }

        public override void HandleResize(Rectangle vp)
        {
            for (int i = 0; i < _components.Count; i++)
            {
                if(_components[i] is Button) 
                {
                    Button button = (Button)_components[i];
                    // TODO: fix this
                    button.Repos(_graphicsDevice.Viewport.Width / 2, _graphicsDevice.Viewport.Height / 2 + 100 * i);
                    _components[i] = button;
                }
            }
        }

        public void IntializeButtons(GraphicsDevice graphicsDevice, ContentManager content)
        {
            var ButtonSpacing = 100;
            var buttonTexture = _content.Load<Texture2D>("Controls/Button");
            var buttonFont = _content.Load<SpriteFont>("Fonts/MyFont");

            var newGameButton = new Button(buttonTexture, buttonFont)
            {
                Position = new Vector2(graphicsDevice.Viewport.Width / 2, graphicsDevice.Viewport.Height / 2),
                Text = "New Game",
            };

            newGameButton.Click += (s, e) => { _game.ChangeState(new GameState(_game, _graphicsDevice, content)); };

            var quitGameButton = new Button(buttonTexture, buttonFont)
            {
                Position = new Vector2(graphicsDevice.Viewport.Width / 2, graphicsDevice.Viewport.Height / 2 + ButtonSpacing),
                Text = "Quit Game",
            };

            quitGameButton.Click += (s, e) => { _game.Exit(); };

            _components = new List<Component>()
            {
                newGameButton, quitGameButton,
            };
        }
    }
}
