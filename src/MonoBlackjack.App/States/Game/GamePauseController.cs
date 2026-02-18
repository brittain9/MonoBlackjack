using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoBlackjack;

internal sealed class GamePauseController
{
    private readonly SpriteFont _font;
    private readonly Button _resumeButton;
    private readonly Button _settingsButton;
    private readonly Button _quitButton;
    private readonly Button _confirmQuitButton;
    private readonly Button _cancelQuitButton;

    public GamePauseController(Texture2D buttonTexture, SpriteFont font)
    {
        _font = font;
        _resumeButton = new Button(buttonTexture, _font) { Text = "Resume", PenColor = Color.Black };
        _settingsButton = new Button(buttonTexture, _font) { Text = "Settings", PenColor = Color.Black };
        _quitButton = new Button(buttonTexture, _font) { Text = "Quit to Menu", PenColor = Color.Black };
        _confirmQuitButton = new Button(buttonTexture, _font) { Text = "Quit", PenColor = Color.Black };
        _cancelQuitButton = new Button(buttonTexture, _font) { Text = "Cancel", PenColor = Color.Black };

        _resumeButton.Click += (_, _) =>
        {
            IsPaused = false;
            IsQuitConfirmationVisible = false;
        };

        _settingsButton.Click += (_, _) =>
        {
            if (!IsPaused)
                return;

            IsQuitConfirmationVisible = false;
            RequestSettings?.Invoke();
        };

        _quitButton.Click += (_, _) =>
        {
            if (!IsPaused)
                return;

            IsQuitConfirmationVisible = true;
        };

        _confirmQuitButton.Click += (_, _) =>
        {
            IsPaused = false;
            IsQuitConfirmationVisible = false;
            RequestQuitToMenu?.Invoke();
        };

        _cancelQuitButton.Click += (_, _) =>
        {
            IsQuitConfirmationVisible = false;
        };
    }

    public bool IsPaused { get; private set; }

    public bool IsQuitConfirmationVisible { get; private set; }

    public event Action? RequestSettings;

    public event Action? RequestQuitToMenu;

    public void HandlePauseBackInput(bool pausePressed, bool backPressed)
    {
        if (pausePressed)
        {
            if (IsQuitConfirmationVisible)
                IsQuitConfirmationVisible = false;
            else
                IsPaused = !IsPaused;
        }
        else if (backPressed)
        {
            if (IsQuitConfirmationVisible)
                IsQuitConfirmationVisible = false;
            else if (IsPaused)
                IsPaused = false;
        }
    }

    public void Update(GameTime gameTime, in MouseFrameSnapshot mouseSnapshot)
    {
        if (!IsPaused)
            return;

        if (IsQuitConfirmationVisible)
        {
            _confirmQuitButton.Update(gameTime, mouseSnapshot);
            _cancelQuitButton.Update(gameTime, mouseSnapshot);
            return;
        }

        _resumeButton.Update(gameTime, mouseSnapshot);
        _settingsButton.Update(gameTime, mouseSnapshot);
        _quitButton.Update(gameTime, mouseSnapshot);
    }

    public void HandleResize(Viewport vp, float actionButtonPadding)
    {
        var pauseButtonSize = new Vector2(
            Math.Clamp(vp.Width * 0.22f, 240f, 420f),
            Math.Clamp(vp.Height * 0.08f, 54f, 86f));

        _resumeButton.Size = pauseButtonSize;
        _settingsButton.Size = pauseButtonSize;
        _quitButton.Size = pauseButtonSize;
        _confirmQuitButton.Size = pauseButtonSize;
        _cancelQuitButton.Size = pauseButtonSize;

        float centerX = vp.Width / 2f;
        float pauseStartY = vp.Height * 0.45f;
        float pauseSpacing = pauseButtonSize.Y * 1.2f;
        _resumeButton.Position = new Vector2(centerX, pauseStartY);
        _settingsButton.Position = new Vector2(centerX, pauseStartY + pauseSpacing);
        _quitButton.Position = new Vector2(centerX, pauseStartY + pauseSpacing * 2f);

        float confirmY = pauseStartY + pauseSpacing * 1.5f;
        float confirmTotalWidth = (pauseButtonSize.X * 2) + actionButtonPadding;
        float confirmStartX = centerX - (confirmTotalWidth / 2f) + (pauseButtonSize.X / 2f);
        _confirmQuitButton.Position = new Vector2(confirmStartX, confirmY);
        _cancelQuitButton.Position = new Vector2(confirmStartX + pauseButtonSize.X + actionButtonPadding, confirmY);
    }

    public void DrawOverlay(
        GameTime gameTime,
        SpriteBatch spriteBatch,
        Texture2D pixelTexture,
        Func<float, float> getResponsiveScale,
        KeybindMap keybinds)
    {
        if (!IsPaused)
            return;

        var vp = spriteBatch.GraphicsDevice.Viewport;
        spriteBatch.Draw(pixelTexture, new Rectangle(0, 0, vp.Width, vp.Height), new Color(3, 8, 4, 204));

        int panelWidth = (int)Math.Clamp(vp.Width * 0.52f, 460f, 860f);
        int panelHeight = IsQuitConfirmationVisible
            ? (int)Math.Clamp(vp.Height * 0.40f, 280f, 460f)
            : (int)Math.Clamp(vp.Height * 0.58f, 360f, 720f);
        int panelX = vp.Width / 2 - panelWidth / 2;
        int panelY = (int)(vp.Height * 0.16f);
        var panelRect = new Rectangle(panelX, panelY, panelWidth, panelHeight);

        spriteBatch.Draw(pixelTexture, panelRect, new Color(12, 48, 24, 242));
        var headerRect = new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, Math.Clamp((int)(panelRect.Height * 0.16f), 48, 92));
        spriteBatch.Draw(pixelTexture, headerRect, new Color(23, 78, 37, 255));

        const int borderThickness = 3;
        spriteBatch.Draw(pixelTexture, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, borderThickness), Color.Gold);
        spriteBatch.Draw(pixelTexture, new Rectangle(panelRect.X, panelRect.Bottom - borderThickness, panelRect.Width, borderThickness), Color.Gold);
        spriteBatch.Draw(pixelTexture, new Rectangle(panelRect.X, panelRect.Y, borderThickness, panelRect.Height), Color.Gold);
        spriteBatch.Draw(pixelTexture, new Rectangle(panelRect.Right - borderThickness, panelRect.Y, borderThickness, panelRect.Height), Color.Gold);

        string title = IsQuitConfirmationVisible ? "Leave This Round?" : "Round Paused";
        float titleScale = getResponsiveScale(1.1f);
        var titleSize = _font.MeasureString(title) * titleScale;
        var titlePos = new Vector2(
            headerRect.Center.X - titleSize.X / 2f,
            headerRect.Center.Y - titleSize.Y / 2f);
        spriteBatch.DrawString(_font, title, titlePos, Color.White, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

        if (IsQuitConfirmationVisible)
        {
            const string warning = "Current round progress will be lost.";
            float warningScale = getResponsiveScale(0.6f);
            var warningSize = _font.MeasureString(warning) * warningScale;
            var warningPos = new Vector2(vp.Width / 2f - warningSize.X / 2f, headerRect.Bottom + panelRect.Height * 0.12f);
            spriteBatch.DrawString(_font, warning, warningPos, Color.LightGray, 0f, Vector2.Zero, warningScale, SpriteEffects.None, 0f);

            var hint = $"{keybinds.GetLabel(InputAction.Pause)} closes this prompt";
            var hintScale = getResponsiveScale(0.56f);
            var hintSize = _font.MeasureString(hint) * hintScale;
            var hintPos = new Vector2(vp.Width / 2f - hintSize.X / 2f, warningPos.Y + warningSize.Y + 10f);
            spriteBatch.DrawString(_font, hint, hintPos, Color.LightGray, 0f, Vector2.Zero, hintScale, SpriteEffects.None, 0f);

            _confirmQuitButton.Draw(gameTime, spriteBatch);
            _cancelQuitButton.Draw(gameTime, spriteBatch);
            return;
        }

        var menuHint = $"{keybinds.GetLabel(InputAction.Pause)} closes this menu";
        float menuHintScale = getResponsiveScale(0.56f);
        var menuHintSize = _font.MeasureString(menuHint) * menuHintScale;
        var menuHintPos = new Vector2(vp.Width / 2f - menuHintSize.X / 2f, headerRect.Bottom + panelRect.Height * 0.08f);
        spriteBatch.DrawString(_font, menuHint, menuHintPos, Color.LightGray, 0f, Vector2.Zero, menuHintScale, SpriteEffects.None, 0f);

        const string menuSubtext = "Use buttons for Settings or Quit to Menu.";
        float subtextScale = getResponsiveScale(0.5f);
        var subtextSize = _font.MeasureString(menuSubtext) * subtextScale;
        var subtextPos = new Vector2(vp.Width / 2f - subtextSize.X / 2f, menuHintPos.Y + menuHintSize.Y + 8f);
        spriteBatch.DrawString(_font, menuSubtext, subtextPos, Color.LightGray, 0f, Vector2.Zero, subtextScale, SpriteEffects.None, 0f);

        _resumeButton.Draw(gameTime, spriteBatch);
        _settingsButton.Draw(gameTime, spriteBatch);
        _quitButton.Draw(gameTime, spriteBatch);
    }
}
