using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoBlackjack.Infrastructure.DevTools;

internal sealed class DevMenuOverlay
{
    private readonly List<IDevMenuTool> _tools;
    private readonly GraphicsDevice _graphicsDevice;

    private readonly List<Rectangle> _toolBounds = [];
    private Rectangle _panelBounds;
    private int _lastViewportWidth;
    private int _lastViewportHeight;

    public bool IsOpen { get; private set; }

    public DevMenuOverlay(Texture2D buttonTexture, IEnumerable<IDevMenuTool> tools)
    {
        _graphicsDevice = buttonTexture.GraphicsDevice;
        _tools = tools.ToList();
        if (_tools.Count == 0)
            throw new ArgumentException("At least one dev tool is required.", nameof(tools));
    }

    public void Toggle()
    {
        IsOpen = !IsOpen;
    }

    public void Close()
    {
        IsOpen = false;
    }

    public void HandleResize(Rectangle viewport)
    {
        int viewportWidth = Math.Max(viewport.Width, 0);
        int viewportHeight = Math.Max(viewport.Height, 0);
        _lastViewportWidth = viewportWidth;
        _lastViewportHeight = viewportHeight;

        int panelWidth = (int)Math.Clamp(viewportWidth * 0.88f, 700f, 1200f);
        int panelHeight = (int)Math.Clamp(viewportHeight * 0.86f, 460f, 900f);
        _panelBounds = new Rectangle(
            viewportWidth / 2 - panelWidth / 2,
            viewportHeight / 2 - panelHeight / 2,
            panelWidth,
            panelHeight);

        LayoutToolSections();
    }

    public void Update(
        GameTime gameTime,
        KeyboardState currentKeyboardState,
        KeyboardState previousKeyboardState,
        in MouseFrameSnapshot mouseSnapshot)
    {
        if (!IsOpen)
            return;

        SyncLayoutToCurrentViewport();

        for (int i = 0; i < _tools.Count; i++)
            _tools[i].Update(gameTime, currentKeyboardState, previousKeyboardState, mouseSnapshot);
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, float textScale)
    {
        if (!IsOpen)
            return;

        var vp = spriteBatch.GraphicsDevice.Viewport;
        EnsureLayoutInitialized(vp.Bounds);

        spriteBatch.Draw(pixelTexture, new Rectangle(0, 0, vp.Width, vp.Height), new Color(0, 0, 0, 200));
        spriteBatch.Draw(pixelTexture, _panelBounds, new Color(18, 66, 22, 240));

        const int borderThickness = 3;
        spriteBatch.Draw(pixelTexture, new Rectangle(_panelBounds.X, _panelBounds.Y, _panelBounds.Width, borderThickness), Color.Gold);
        spriteBatch.Draw(pixelTexture, new Rectangle(_panelBounds.X, _panelBounds.Bottom - borderThickness, _panelBounds.Width, borderThickness), Color.Gold);
        spriteBatch.Draw(pixelTexture, new Rectangle(_panelBounds.X, _panelBounds.Y, borderThickness, _panelBounds.Height), Color.Gold);
        spriteBatch.Draw(pixelTexture, new Rectangle(_panelBounds.Right - borderThickness, _panelBounds.Y, borderThickness, _panelBounds.Height), Color.Gold);

        var title = "Developer Menu";
        DrawCenteredText(
            spriteBatch,
            font,
            title,
            new Vector2(_panelBounds.Center.X, _panelBounds.Y + 24f),
            Color.White,
            textScale);

        for (int i = 0; i < _tools.Count; i++)
        {
            var toolBounds = _toolBounds[i];
            spriteBatch.Draw(pixelTexture, toolBounds, new Color(0, 0, 0, 42));
            spriteBatch.Draw(pixelTexture, new Rectangle(toolBounds.X, toolBounds.Y, toolBounds.Width, 1), new Color(255, 215, 0, 120));
            spriteBatch.Draw(pixelTexture, new Rectangle(toolBounds.X, toolBounds.Bottom - 1, toolBounds.Width, 1), new Color(255, 215, 0, 120));

            var toolTitlePosition = new Vector2(toolBounds.X + 14f, toolBounds.Y + 8f);
            spriteBatch.DrawString(
                font,
                _tools[i].Name,
                toolTitlePosition,
                Color.Gold,
                0f,
                Vector2.Zero,
                textScale * 0.72f,
                SpriteEffects.None,
                0f);

            _tools[i].Draw(gameTime, spriteBatch, font, toolBounds, textScale * 0.72f);
        }

        var closeHelp = "F1 or Pause/Back: close";
        var closeScale = textScale * 0.65f;
        var closeSize = font.MeasureString(closeHelp) * closeScale;
        spriteBatch.DrawString(
            font,
            closeHelp,
            new Vector2(_panelBounds.Right - closeSize.X - 18f, _panelBounds.Bottom - closeSize.Y - 10f),
            Color.LightGray,
            0f,
            Vector2.Zero,
            closeScale,
            SpriteEffects.None,
            0f);
    }

    private void LayoutToolSections()
    {
        _toolBounds.Clear();

        const int sidePadding = 18;
        const int topPadding = 62;
        const int bottomPadding = 30;
        const int sectionGap = 14;

        int sectionCount = _tools.Count;
        int contentX = _panelBounds.X + sidePadding;
        int contentY = _panelBounds.Y + topPadding;
        int contentWidth = Math.Max(1, _panelBounds.Width - sidePadding * 2);
        int contentHeight = Math.Max(1, _panelBounds.Height - topPadding - bottomPadding);

        int totalGap = sectionGap * Math.Max(sectionCount - 1, 0);
        int sectionHeight = Math.Max(140, (contentHeight - totalGap) / sectionCount);

        for (int i = 0; i < sectionCount; i++)
        {
            int y = contentY + i * (sectionHeight + sectionGap);
            if (i == sectionCount - 1)
                sectionHeight = Math.Max(140, _panelBounds.Bottom - bottomPadding - y);

            var bounds = new Rectangle(contentX, y, contentWidth, sectionHeight);
            _toolBounds.Add(bounds);

            var buttonSize = new Vector2(Math.Clamp(bounds.Width * 0.2f, 90f, 220f), 44f);
            _tools[i].HandleResize(bounds, buttonSize);
        }
    }

    private void SyncLayoutToCurrentViewport()
    {
        var vp = _graphicsDevice.Viewport;
        if (vp.Width <= 0 || vp.Height <= 0)
            return;

        if (vp.Width != _lastViewportWidth || vp.Height != _lastViewportHeight)
            HandleResize(vp.Bounds);
    }

    private void EnsureLayoutInitialized(Rectangle viewportBounds)
    {
        if (_panelBounds.Width > 0
            && _panelBounds.Height > 0
            && viewportBounds.Width == _lastViewportWidth
            && viewportBounds.Height == _lastViewportHeight)
            return;

        if (viewportBounds.Width <= 0 || viewportBounds.Height <= 0)
            return;

        HandleResize(viewportBounds);
    }

    private static void DrawCenteredText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 center, Color color, float scale)
    {
        var size = font.MeasureString(text) * scale;
        var topLeft = new Vector2(center.X - size.X / 2f, center.Y - size.Y / 2f);
        spriteBatch.DrawString(font, text, topLeft, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
