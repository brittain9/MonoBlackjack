using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MonoBlackjack;

internal class MenuState : State
{
    private readonly Texture2D _logoTexture;
    private readonly Button _playButton;
    private readonly Button _settingsButton;
    private readonly Button _statsButton;
    private readonly Button _quitButton;
    private readonly List<Button> _buttons;
    private Rectangle _logoRect;

    public MenuState(BlackjackGame game, GraphicsDevice graphicsDevice, ContentManager content)
        : base(game, graphicsDevice, content)
    {
        _logoTexture = content.Load<Texture2D>("Art/menuLogo");

        var buttonTexture = content.Load<Texture2D>("Controls/Button");
        var buttonFont = content.Load<SpriteFont>("Fonts/MyFont");

        _playButton = new Button(buttonTexture, buttonFont) { Text = "Play", PenColor = Color.Black };
        _settingsButton = new Button(buttonTexture, buttonFont) { Text = "Settings", PenColor = Color.Black };
        _statsButton = new Button(buttonTexture, buttonFont) { Text = "Stats", PenColor = Color.Black };
        _quitButton = new Button(buttonTexture, buttonFont) { Text = "Quit", PenColor = Color.Black };

        _playButton.Click += (_, _) =>
            _game.ChangeState(new GameState(_game, _graphicsDevice, _content, _game.StatsRepository, _game.ActiveProfileId));
        _settingsButton.Click += (_, _) =>
            _game.ChangeState(new SettingsState(_game, _graphicsDevice, _content, _game.SettingsRepository, _game.ActiveProfileId));
        _statsButton.Click += (_, _) =>
            _game.ChangeState(new StatsState(_game, _graphicsDevice, _content, _game.StatsRepository, _game.ActiveProfileId));
        _quitButton.Click += (_, _) => _game.Exit();

        _buttons = [_playButton, _settingsButton, _statsButton, _quitButton];

        UpdateLayout();
    }

    private void UpdateLayout()
    {
        var vp = _graphicsDevice.Viewport;
        int spacing = vp.Height / 15;

        _logoRect = new Rectangle(
            vp.Width / 2,
            vp.Height / 3,
            vp.Width / 2,
            vp.Height / 2);

        var buttonSize = new Vector2(vp.Width / 4f, vp.Height / 20f);
        float startY = _logoRect.Y + spacing * 3;

        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Size = buttonSize;
            _buttons[i].Position = new Vector2(_logoRect.X, startY + spacing * i);
        }
    }

    public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        spriteBatch.Begin();

        spriteBatch.Draw(
            _logoTexture,
            _logoRect,
            null,
            Color.White,
            0f,
            new Vector2(_logoTexture.Width / 2, _logoTexture.Height / 2),
            SpriteEffects.None,
            0f);

        foreach (var button in _buttons)
            button.Draw(gameTime, spriteBatch);

        spriteBatch.End();
    }

    public override void PostUpdate(GameTime gameTime) { }

    public override void Update(GameTime gameTime)
    {
        foreach (var button in _buttons)
            button.Update(gameTime);
    }

    public override void HandleResize(Rectangle vp)
    {
        UpdateLayout();
    }
}
