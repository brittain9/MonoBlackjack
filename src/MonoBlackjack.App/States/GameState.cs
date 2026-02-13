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
    private const float CardAspectRatio = 100f / 145f;

    private readonly Shoe _shoe;
    private readonly CardRenderer _cardRenderer;
    private readonly Human _player;
    private readonly Dealer _dealer;

    private readonly EventBus _eventBus;
    private readonly TweenManager _tweenManager;
    private readonly SceneRenderer _sceneRenderer;
    private readonly SpriteLayer _cardLayer;

    private readonly Button _hitButton;
    private readonly Button _standButton;
    private readonly List<TrackedCardSprite> _trackedCards = [];

    private GameRound _round;
    private int _dealCardIndex;
    private int _playerCardCount;
    private int _dealerCardCount;
    private float _dealerAnimationDelay;
    private float _roundCompleteTimer;
    private bool _waitingForRoundReset;

    // Deck position (off-screen left) where cards animate from
    private Vector2 _deckPosition;
    private Vector2 _cardSize;

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

        var buttonTexture = _content.Load<Texture2D>("Controls/Button");
        var buttonFont = _content.Load<SpriteFont>("Fonts/MyFont");

        _hitButton = new Button(buttonTexture, buttonFont)
        {
            Text = "Hit",
            PenColor = Color.Black
        };
        _standButton = new Button(buttonTexture, buttonFont)
        {
            Text = "Stand",
            PenColor = Color.Black
        };

        _hitButton.Click += OnHitClicked;
        _standButton.Click += OnStandClicked;

        _eventBus.Subscribe<CardDealt>(OnCardDealt);
        _eventBus.Subscribe<InitialDealComplete>(OnInitialDealComplete);
        _eventBus.Subscribe<PlayerHit>(OnPlayerHit);
        _eventBus.Subscribe<PlayerBusted>(OnPlayerBusted);
        _eventBus.Subscribe<PlayerStood>(OnPlayerStood);
        _eventBus.Subscribe<DealerTurnStarted>(OnDealerTurnStarted);
        _eventBus.Subscribe<DealerHit>(OnDealerHit);
        _eventBus.Subscribe<DealerHoleCardRevealed>(OnDealerHoleCardRevealed);
        _eventBus.Subscribe<DealerBusted>(OnDealerBusted);
        _eventBus.Subscribe<DealerStood>(OnDealerStood);
        _eventBus.Subscribe<HandResolved>(OnHandResolved);
        _eventBus.Subscribe<RoundComplete>(OnRoundComplete);

        CalculatePositions();

        _round = new GameRound(_shoe, _player, _dealer, _eventBus.Publish);
        _round.PlaceBet(GameConfig.MinimumBet);
        _round.Deal();
    }

    private void CalculatePositions()
    {
        var vp = _graphicsDevice.Viewport;
        var cardHeight = Math.Clamp(vp.Height * 0.2f, 120f, 220f);
        var cardWidth = cardHeight * CardAspectRatio;
        _cardSize = new Vector2(cardWidth, cardHeight);

        _deckPosition = new Vector2(-_cardSize.X - 20, vp.Height / 2f);

        var buttonSize = new Vector2(_cardSize.X * 1.35f, _cardSize.Y * 0.35f);
        var centerX = vp.Width / 2f;
        var gap = buttonSize.X * 0.58f;
        var buttonY = GetPlayerCardsY() + _cardSize.Y + vp.Height / 18f + buttonSize.Y / 2f;

        _hitButton.Size = buttonSize;
        _standButton.Size = buttonSize;

        _hitButton.Position = new Vector2(centerX - gap, buttonY);
        _standButton.Position = new Vector2(centerX + gap, buttonY);
    }

    private float GetDealerCardsY()
    {
        var vp = _graphicsDevice.Viewport;
        return vp.Height * 0.2f;
    }

    private float GetPlayerCardsY()
    {
        var vp = _graphicsDevice.Viewport;
        return vp.Height * 0.56f;
    }

    private Vector2 GetCardTargetPosition(string recipient, int cardIndexInHand)
    {
        var vp = _graphicsDevice.Viewport;
        var spacing = _cardSize.X * 1.15f;
        var baseX = vp.Width / 2f - _cardSize.X / 2f - spacing * 0.5f;
        var y = recipient == _dealer.Name ? GetDealerCardsY() : GetPlayerCardsY();
        return new Vector2(baseX + spacing * cardIndexInHand, y);
    }

    private bool IsPlayerInteractionEnabled()
    {
        return !_tweenManager.HasActiveTweens && _round.Phase == RoundPhase.PlayerTurn;
    }

    private void AddAnimatedCard(Card card, bool faceDown, string recipient, float delay)
    {
        var sprite = _cardRenderer.CreateCardSprite(card, faceDown);
        sprite.Size = _cardSize;
        sprite.Position = _deckPosition;
        sprite.Opacity = 0f;
        sprite.ZOrder = _dealCardIndex;

        int recipientCardIndex;
        if (recipient == _dealer.Name)
            recipientCardIndex = _dealerCardCount++;
        else
            recipientCardIndex = _playerCardCount++;

        _trackedCards.Add(new TrackedCardSprite(sprite, recipient, recipientCardIndex));

        var target = GetCardTargetPosition(recipient, recipientCardIndex);
        float duration = 0.4f;

        _tweenManager.Add(TweenBuilder.MoveTo(sprite, target, duration, delay, Easing.EaseOutQuad));
        _tweenManager.Add(TweenBuilder.FadeTo(sprite, 1f, duration * 0.5f, delay, Easing.Linear));

        _cardLayer.Add(sprite);
        _dealCardIndex++;
    }

    private void StartNewRound()
    {
        _cardLayer.Clear();
        _trackedCards.Clear();

        _dealCardIndex = 0;
        _playerCardCount = 0;
        _dealerCardCount = 0;
        _dealerAnimationDelay = 0f;
        _roundCompleteTimer = 0f;
        _waitingForRoundReset = false;

        _round = new GameRound(_shoe, _player, _dealer, _eventBus.Publish);
        _round.PlaceBet(GameConfig.MinimumBet);
        _round.Deal();
    }

    private void OnCardDealt(CardDealt evt)
    {
        float delay = _dealCardIndex * 0.2f;
        AddAnimatedCard(evt.Card, evt.FaceDown, evt.Recipient, delay);
    }

    private void OnInitialDealComplete(InitialDealComplete evt)
    {
        // Future: could trigger UI state changes here
    }

    private void OnPlayerHit(PlayerHit evt)
    {
        AddAnimatedCard(evt.Card, false, _player.Name, 0f);
    }

    private void OnPlayerBusted(PlayerBusted evt)
    {
        Console.WriteLine($"Player busted: hand {evt.HandIndex}");
    }

    private void OnPlayerStood(PlayerStood evt)
    {
        // No visual change for now; buttons hide automatically by phase.
    }

    private void OnDealerTurnStarted(DealerTurnStarted evt)
    {
        _dealerAnimationDelay = 0f;
    }

    private void OnDealerHit(DealerHit evt)
    {
        AddAnimatedCard(evt.Card, false, _dealer.Name, _dealerAnimationDelay);
        _dealerAnimationDelay += 0.3f;
    }

    private void OnDealerHoleCardRevealed(DealerHoleCardRevealed evt)
    {
        for (int i = 0; i < _trackedCards.Count; i++)
        {
            var tracked = _trackedCards[i];
            if (tracked.Recipient == _dealer.Name && tracked.CardIndexInHand == 1)
            {
                tracked.Sprite.FaceDown = false;
                break;
            }
        }
    }

    private void OnDealerBusted(DealerBusted evt)
    {
        // Future: show dealer bust UI.
    }

    private void OnDealerStood(DealerStood evt)
    {
        // Future: show dealer stood UI.
    }

    private void OnHandResolved(HandResolved evt)
    {
        // Future: show win/lose text.
    }

    private void OnRoundComplete(RoundComplete evt)
    {
        _waitingForRoundReset = true;
        _roundCompleteTimer = 1.5f;
    }

    private void OnHitClicked(object? sender, EventArgs e)
    {
        if (!IsPlayerInteractionEnabled())
            return;

        _round.PlayerHit();
        _eventBus.Flush();
    }

    private void OnStandClicked(object? sender, EventArgs e)
    {
        if (!IsPlayerInteractionEnabled())
            return;

        _round.PlayerStand();
        _eventBus.Flush();
    }

    public override void Update(GameTime gameTime)
    {
        _eventBus.Flush();

        if (IsPlayerInteractionEnabled())
        {
            _hitButton.Update(gameTime);
            _standButton.Update(gameTime);
        }

        float deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _tweenManager.Update(deltaSeconds);

        if (_waitingForRoundReset && !_tweenManager.HasActiveTweens)
        {
            _roundCompleteTimer -= deltaSeconds;
            if (_roundCompleteTimer <= 0f)
                StartNewRound();
        }

        _sceneRenderer.Update(gameTime);
    }

    public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        _sceneRenderer.Draw(spriteBatch);

        if (IsPlayerInteractionEnabled())
        {
            spriteBatch.Begin();
            _hitButton.Draw(gameTime, spriteBatch);
            _standButton.Draw(gameTime, spriteBatch);
            spriteBatch.End();
        }
    }

    public override void HandleResize(Rectangle vp)
    {
        CalculatePositions();
        _sceneRenderer.HandleResize(vp);

        _tweenManager.Clear();

        foreach (var tracked in _trackedCards)
        {
            tracked.Sprite.Size = _cardSize;
            tracked.Sprite.Position = GetCardTargetPosition(tracked.Recipient, tracked.CardIndexInHand);
            tracked.Sprite.Opacity = 1f;
        }
    }

    public override void PostUpdate(GameTime gameTime) { }

    private readonly record struct TrackedCardSprite(CardSprite Sprite, string Recipient, int CardIndexInHand);
}
