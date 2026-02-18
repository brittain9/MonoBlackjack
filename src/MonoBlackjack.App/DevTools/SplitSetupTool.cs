using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoBlackjack.Core;

namespace MonoBlackjack.DevTools;

internal sealed class SplitSetupTool : IDevMenuTool
{
    private readonly Shoe _shoe;
    private readonly Button _queueRandomSplitButton;

    private readonly Rank[] _ranks = Enum.GetValues<Rank>();
    private readonly Suit[] _suits = Enum.GetValues<Suit>();

    private Rectangle _sectionBounds;
    private Vector2 _buttonPosition;

    public string Name => "Split Tool";

    public SplitSetupTool(Shoe shoe, Texture2D buttonTexture, SpriteFont font)
    {
        _shoe = shoe;
        _queueRandomSplitButton = new Button(buttonTexture, font)
        {
            Text = "Queue Random Split Next Hand",
            PenColor = Color.Black
        };

        _queueRandomSplitButton.Click += (_, _) => QueueRandomSplitNextHand();
    }

    public void HandleResize(Rectangle panelBounds, Vector2 buttonSize)
    {
        _sectionBounds = panelBounds;

        var buttonSizeResolved = new Vector2(Math.Clamp(panelBounds.Width * 0.34f, 260f, 430f), 50f);
        _queueRandomSplitButton.Size = buttonSizeResolved;

        _buttonPosition = new Vector2(panelBounds.Center.X, panelBounds.Y + 82f);
        _queueRandomSplitButton.Position = _buttonPosition;
    }

    public void Update(
        GameTime gameTime,
        KeyboardState currentKeyboardState,
        KeyboardState previousKeyboardState,
        in MouseFrameSnapshot mouseSnapshot)
    {
        _queueRandomSplitButton.Update(gameTime, mouseSnapshot);

        if (WasKeyJustPressed(Keys.S, currentKeyboardState, previousKeyboardState))
            QueueRandomSplitNextHand();
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch, SpriteFont font, Rectangle panelBounds, float textScale)
    {
        _queueRandomSplitButton.Draw(gameTime, spriteBatch);

        DrawCenteredText(
            spriteBatch,
            font,
            "Queues next deal as Player,Dealer,Player,Dealer with a random player pair.",
            new Vector2(panelBounds.Center.X, panelBounds.Y + 128f),
            Color.LightGray,
            textScale * 0.62f);

        DrawCenteredText(
            spriteBatch,
            font,
            "Pressing this replaces existing forced draws to keep setup deterministic.",
            new Vector2(panelBounds.Center.X, panelBounds.Y + 154f),
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

    private void QueueRandomSplitNextHand()
    {
        _shoe.ClearForcedDraws();

        Rank pairRank = _ranks[Random.Shared.Next(_ranks.Length)];
        Suit firstSuit = _suits[Random.Shared.Next(_suits.Length)];
        Suit secondSuit = ChooseDifferentSuit(firstSuit);

        var takenCards = new HashSet<Card>
        {
            new(pairRank, firstSuit),
            new(pairRank, secondSuit)
        };

        Card dealerFirst = DrawUniqueRandomCard(takenCards);
        Card dealerSecond = DrawUniqueRandomCard(takenCards);

        _shoe.EnqueueForcedDraw(new Card(pairRank, firstSuit));
        _shoe.EnqueueForcedDraw(dealerFirst);
        _shoe.EnqueueForcedDraw(new Card(pairRank, secondSuit));
        _shoe.EnqueueForcedDraw(dealerSecond);
    }

    private Suit ChooseDifferentSuit(Suit existing)
    {
        if (_suits.Length <= 1)
            return existing;

        Suit suit;
        do
        {
            suit = _suits[Random.Shared.Next(_suits.Length)];
        } while (suit == existing);

        return suit;
    }

    private Card DrawUniqueRandomCard(HashSet<Card> excluded)
    {
        for (int i = 0; i < 128; i++)
        {
            var card = new Card(
                _ranks[Random.Shared.Next(_ranks.Length)],
                _suits[Random.Shared.Next(_suits.Length)]);

            if (excluded.Add(card))
                return card;
        }

        foreach (var rank in _ranks)
        {
            foreach (var suit in _suits)
            {
                var card = new Card(rank, suit);
                if (excluded.Add(card))
                    return card;
            }
        }

        return new Card(Rank.Ace, Suit.Spades);
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
