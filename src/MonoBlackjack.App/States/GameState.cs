using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Animation;
using MonoBlackjack.Core;
using MonoBlackjack.Core.Events;
using MonoBlackjack.Core.Players;
using MonoBlackjack.Events;
using MonoBlackjack.Rendering;

namespace MonoBlackjack;

internal class GameState : State
{
    private readonly Shoe _shoe;
    private readonly CardRenderer _cardRenderer;
    private readonly Human _player;
    private readonly Dealer _dealer;

    private readonly EventBus _eventBus;
    private readonly TweenManager _tweenManager;
    private readonly SceneRenderer _sceneRenderer;
    private readonly SpriteLayer _cardLayer;

    private GameRound _round;
    private int _dealCardIndex;
    private int _playerCardCount;
    private int _dealerCardCount;

    // Deck position (off-screen left) where cards animate from
    private Vector2 _deckPosition;

    public GameState(BlackjackGame game, GraphicsDevice graphicsDevice, ContentManager content)
        : base(game, graphicsDevice, content)
    {
        _cardRenderer = new CardRenderer();
        _cardRenderer.LoadTextures(content);

        _shoe = new Shoe(GameConfig.NumberOfDecks);
        _player = new Human();
        _dealer = new Dealer();

        _eventBus = new EventBus();
        _tweenManager = new TweenManager();
        _sceneRenderer = new SceneRenderer();

        _cardLayer = new SpriteLayer(10);
        _sceneRenderer.AddLayer(_cardLayer);

        _eventBus.Subscribe<CardDealt>(OnCardDealt);
        _eventBus.Subscribe<InitialDealComplete>(OnInitialDealComplete);

        CalculatePositions();

        _round = new GameRound(_shoe, _player, _dealer, _eventBus.Publish);
        _round.PlaceBet(GameConfig.MinimumBet);
        _round.Deal();
    }

    private void CalculatePositions()
    {
        var vp = _graphicsDevice.Viewport;
        _deckPosition = new Vector2(-CardRenderer.CardSize.X - 20, vp.Height / 2f);
    }

    private Vector2 GetCardTargetPosition(string recipient, int cardIndexInHand)
    {
        var vp = _graphicsDevice.Viewport;
        var spacing = vp.Width / 10f;

        if (recipient == _dealer.Name)
        {
            var basePos = new Vector2(
                vp.Width / 2f - CardRenderer.CardSize.X / 2f,
                vp.Height / 6f);
            return new Vector2(basePos.X + spacing * cardIndexInHand, basePos.Y);
        }
        else
        {
            var basePos = new Vector2(
                vp.Width / 2f - CardRenderer.CardSize.X / 2f,
                vp.Height - vp.Height / 4f);
            return new Vector2(basePos.X + spacing * cardIndexInHand, basePos.Y);
        }
    }

    private void OnCardDealt(CardDealt evt)
    {
        var sprite = _cardRenderer.CreateCardSprite(evt.Card, evt.FaceDown);
        sprite.Position = _deckPosition;
        sprite.Opacity = 0f;
        sprite.ZOrder = _dealCardIndex;

        int recipientCardIndex;
        if (evt.Recipient == _dealer.Name)
            recipientCardIndex = _dealerCardCount++;
        else
            recipientCardIndex = _playerCardCount++;

        var target = GetCardTargetPosition(evt.Recipient, recipientCardIndex);

        float delay = _dealCardIndex * 0.2f;
        float duration = 0.4f;

        _tweenManager.Add(TweenBuilder.MoveTo(sprite, target, duration, delay, Easing.EaseOutQuad));
        _tweenManager.Add(TweenBuilder.FadeTo(sprite, 1f, duration * 0.5f, delay, Easing.Linear));

        _cardLayer.Add(sprite);
        _dealCardIndex++;
    }

    private void OnInitialDealComplete(InitialDealComplete evt)
    {
        // Future: could trigger UI state changes here
    }

    public override void Update(GameTime gameTime)
    {
        _eventBus.Flush();
        _tweenManager.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        _sceneRenderer.Update(gameTime);
    }

    public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        _sceneRenderer.Draw(spriteBatch);
    }

    public override void HandleResize(Rectangle vp)
    {
        CalculatePositions();
    }

    public override void PostUpdate(GameTime gameTime) { }
}
