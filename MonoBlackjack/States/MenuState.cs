using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

using MonoBlackjack;


namespace MonoBlackjack
{
    internal class MenuState : State
    {
        private List<Component> _components;

        Texture2D logoTexture;
        Rectangle logoRect;

        public int CompSpacing;

        public MenuState(BlackjackGame game, GraphicsDevice graphicsDevice, ContentManager content) : base(game, graphicsDevice, content)
        {
            IntializeMainMenu(graphicsDevice, content);
        }

        public void IntializeMainMenu(GraphicsDevice graphicsDevice, ContentManager content)
        {
            CompSpacing = graphicsDevice.Viewport.Height / 15;

            logoTexture = content.Load<Texture2D>("Art/menuLogo");
            logoRect = new Rectangle(
                graphicsDevice.Viewport.Width / 2, // middle screen width
                graphicsDevice.Viewport.Height / 3, // space down from screen
                graphicsDevice.Viewport.Width / 2, // size of logo scaled to screen size
                graphicsDevice.Viewport.Height / 2); 

            var buttonTexture = _content.Load<Texture2D>("Controls/Button");
            var buttonFont = _content.Load<SpriteFont>("Fonts/MyFont");
            Vector2 buttonSize = new Vector2(graphicsDevice.Viewport.Width / 4, graphicsDevice.Viewport.Height / 20);

            // This button will be after the below the logo
            var playButton = new Button(buttonTexture, buttonFont)
            {
                Position = new Vector2(logoRect.X, logoRect.Y + CompSpacing * 3),
                Text = "Play",
                Size = buttonSize,
                PenColor = Color.Black
            };
            playButton.Click += (s, e) => 
            { 
                _game.ChangeState(new GameState(_game, _graphicsDevice, content)); 
            };

            // This button is below the n
            var quitGameButton = new Button(buttonTexture, buttonFont)
            {
                Position = new Vector2(playButton.Position.X, playButton.Position.Y + CompSpacing),
                Text = "Quit",
                Size = buttonSize,
                PenColor = Color.Black
            };
            quitGameButton.Click += (s, e) => 
            { 
                _game.Exit(); 
            };

            _components = new List<Component>()
            {
                playButton, quitGameButton,
            };
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            spriteBatch.Begin();
            // Draw logo
            spriteBatch.Draw(
                logoTexture,
                logoRect,
                null,
                Color.White,
                0f,
                new Vector2(logoTexture.Width / 2, logoTexture.Height / 2),
                SpriteEffects.None,
                0f);

            // Draw buttons
            foreach (var component in _components)
            {
                component.Draw(gameTime, spriteBatch);
            }
            spriteBatch.End();
        }

        public override void PostUpdate(GameTime gameTime)
        {
            // remove sprites if they are not needed
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
            IntializeMainMenu(_graphicsDevice, _content);
        } 
    }
}
