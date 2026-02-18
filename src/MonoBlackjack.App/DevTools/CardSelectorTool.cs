using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoBlackjack.Core;

namespace MonoBlackjack.DevTools;

internal sealed class CardSelectorTool : IDevMenuTool
{
    private readonly Shoe _shoe;
    private readonly Button _rankLeftButton;
    private readonly Button _rankRightButton;
    private readonly Button _suitLeftButton;
    private readonly Button _suitRightButton;
    private readonly Button _queueSelectedCardButton;
    private readonly Button _clearQueueButton;

    private readonly Rank[] _ranks = Enum.GetValues<Rank>();
    private readonly Suit[] _suits = Enum.GetValues<Suit>();

    private int _selectedRankIndex;
    private int _selectedSuitIndex;

    private Vector2 _rankLabelPosition;
    private Vector2 _suitLabelPosition;
    private Vector2 _helperPosition;
    private Rectangle _sectionBounds;

    public string Name => "Card Selector";

    public CardSelectorTool(Shoe shoe, Texture2D buttonTexture, SpriteFont font)
    {
        _shoe = shoe;

        _selectedRankIndex = Array.IndexOf(_ranks, Rank.Ace);
        _selectedSuitIndex = Array.IndexOf(_suits, Suit.Spades);

        _rankLeftButton = new Button(buttonTexture, font) { Text = "<", PenColor = Color.Black };
        _rankRightButton = new Button(buttonTexture, font) { Text = ">", PenColor = Color.Black };
        _suitLeftButton = new Button(buttonTexture, font) { Text = "<", PenColor = Color.Black };
        _suitRightButton = new Button(buttonTexture, font) { Text = ">", PenColor = Color.Black };
        _queueSelectedCardButton = new Button(buttonTexture, font) { Text = "Queue Selected Card", PenColor = Color.Black };
        _clearQueueButton = new Button(buttonTexture, font) { Text = "Clear Queue", PenColor = Color.Black };

        _rankLeftButton.Click += (_, _) => MoveRank(-1);
        _rankRightButton.Click += (_, _) => MoveRank(1);
        _suitLeftButton.Click += (_, _) => MoveSuit(-1);
        _suitRightButton.Click += (_, _) => MoveSuit(1);
        _queueSelectedCardButton.Click += (_, _) => QueueSelectedCard();
        _clearQueueButton.Click += (_, _) => _shoe.ClearForcedDraws();
    }

    public void HandleResize(Rectangle panelBounds, Vector2 buttonSize)
    {
        _sectionBounds = panelBounds;

        var arrowSize = new Vector2(72f, 40f);
        var actionButtonSize = new Vector2(Math.Clamp(panelBounds.Width * 0.24f, 180f, 300f), 44f);

        _rankLeftButton.Size = arrowSize;
        _rankRightButton.Size = arrowSize;
        _suitLeftButton.Size = arrowSize;
        _suitRightButton.Size = arrowSize;
        _queueSelectedCardButton.Size = actionButtonSize;
        _clearQueueButton.Size = actionButtonSize;

        float centerX = panelBounds.Center.X;
        float firstRowY = panelBounds.Y + 54f;
        float rowGap = 46f;
        float arrowOffset = Math.Clamp(panelBounds.Width * 0.22f, 120f, 220f);

        _rankLeftButton.Position = new Vector2(centerX - arrowOffset, firstRowY);
        _rankRightButton.Position = new Vector2(centerX + arrowOffset, firstRowY);

        _suitLeftButton.Position = new Vector2(centerX - arrowOffset, firstRowY + rowGap);
        _suitRightButton.Position = new Vector2(centerX + arrowOffset, firstRowY + rowGap);

        _rankLabelPosition = new Vector2(centerX, firstRowY - 1f);
        _suitLabelPosition = new Vector2(centerX, firstRowY + rowGap - 1f);

        _queueSelectedCardButton.Position = new Vector2(centerX - actionButtonSize.X * 0.56f, firstRowY + rowGap * 2f + 6f);
        _clearQueueButton.Position = new Vector2(centerX + actionButtonSize.X * 0.56f, firstRowY + rowGap * 2f + 6f);

        _helperPosition = new Vector2(centerX, firstRowY + rowGap * 3f + 6f);
    }

    public void Update(
        GameTime gameTime,
        KeyboardState currentKeyboardState,
        KeyboardState previousKeyboardState,
        in MouseFrameSnapshot mouseSnapshot)
    {
        _rankLeftButton.Update(gameTime, mouseSnapshot);
        _rankRightButton.Update(gameTime, mouseSnapshot);
        _suitLeftButton.Update(gameTime, mouseSnapshot);
        _suitRightButton.Update(gameTime, mouseSnapshot);
        _queueSelectedCardButton.Update(gameTime, mouseSnapshot);
        _clearQueueButton.Update(gameTime, mouseSnapshot);

        if (WasKeyJustPressed(Keys.Left, currentKeyboardState, previousKeyboardState))
            MoveRank(-1);
        if (WasKeyJustPressed(Keys.Right, currentKeyboardState, previousKeyboardState))
            MoveRank(1);
        if (WasKeyJustPressed(Keys.Up, currentKeyboardState, previousKeyboardState))
            MoveSuit(-1);
        if (WasKeyJustPressed(Keys.Down, currentKeyboardState, previousKeyboardState))
            MoveSuit(1);

        if (WasKeyJustPressed(Keys.Enter, currentKeyboardState, previousKeyboardState))
            QueueSelectedCard();
        if (WasKeyJustPressed(Keys.Back, currentKeyboardState, previousKeyboardState))
            _shoe.ClearForcedDraws();
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch, SpriteFont font, Rectangle panelBounds, float textScale)
    {
        DrawCenteredText(spriteBatch, font, $"Rank: {_ranks[_selectedRankIndex]}", _rankLabelPosition, Color.Gold, textScale * 0.9f);
        DrawCenteredText(spriteBatch, font, $"Suit: {_suits[_selectedSuitIndex]}", _suitLabelPosition, Color.Gold, textScale * 0.9f);

        _rankLeftButton.Draw(gameTime, spriteBatch);
        _rankRightButton.Draw(gameTime, spriteBatch);
        _suitLeftButton.Draw(gameTime, spriteBatch);
        _suitRightButton.Draw(gameTime, spriteBatch);
        _queueSelectedCardButton.Draw(gameTime, spriteBatch);
        _clearQueueButton.Draw(gameTime, spriteBatch);

        DrawCenteredText(
            spriteBatch,
            font,
            "Left/Right: Rank  Up/Down: Suit  Enter: Queue",
            _helperPosition,
            Color.LightGray,
            textScale * 0.62f);

        var queuedText = $"Queued draws: {_shoe.ForcedDrawCount}";
        spriteBatch.DrawString(
            font,
            queuedText,
            new Vector2(_sectionBounds.X + 10f, _sectionBounds.Bottom - 22f),
            Color.LightGray,
            0f,
            Vector2.Zero,
            textScale * 0.6f,
            SpriteEffects.None,
            0f);
    }

    private void QueueSelectedCard()
    {
        _shoe.EnqueueForcedDraw(new Card(_ranks[_selectedRankIndex], _suits[_selectedSuitIndex]));
    }

    private void MoveRank(int delta)
    {
        _selectedRankIndex = WrapIndex(_selectedRankIndex + delta, _ranks.Length);
    }

    private void MoveSuit(int delta)
    {
        _selectedSuitIndex = WrapIndex(_selectedSuitIndex + delta, _suits.Length);
    }

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
            return 0;

        int mod = value % count;
        return mod < 0 ? mod + count : mod;
    }

    private static bool WasKeyJustPressed(Keys key, KeyboardState current, KeyboardState previous)
    {
        return current.IsKeyDown(key) && !previous.IsKeyDown(key);
    }

    private static void DrawCenteredText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 center, Color color, float scale)
    {
        var size = font.MeasureString(text) * scale;
        var topLeft = new Vector2(center.X - size.X / 2f, center.Y - size.Y / 2f);
        spriteBatch.DrawString(font, text, topLeft, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
