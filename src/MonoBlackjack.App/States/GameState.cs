using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Animation;
using MonoBlackjack.Core;
using MonoBlackjack.Core.Events;
using MonoBlackjack.Core.Ports;
using MonoBlackjack.Core.Players;
using MonoBlackjack.Events;
using MonoBlackjack.Rendering;
using MonoBlackjack.Stats;

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
    private readonly SpriteLayer _uiLayer;
    private readonly SpriteFont _font;

    private readonly Button _hitButton;
    private readonly Button _standButton;
    private readonly Button _splitButton;
    private readonly Button _doubleButton;
    private readonly Button _surrenderButton;
    private readonly Button _insuranceButton;
    private readonly Button _declineInsuranceButton;
    private readonly List<TrackedCardSprite> _trackedCards = [];

    private GameRound _round;
    private int _dealCardIndex;
    private readonly Dictionary<int, int> _playerHandCardCounts = new();
    private int _dealerCardCount;
    private float _dealerAnimationDelay;
    private float _roundCompleteTimer;
    private bool _waitingForRoundReset;
    private int _activePlayerHandIndex;

    // Deck position (off-screen left) where cards animate from
    private Vector2 _deckPosition;
    private Vector2 _cardSize;

    public GameState(
        BlackjackGame game,
        GraphicsDevice graphicsDevice,
        ContentManager content,
        IStatsRepository statsRepository,
        int profileId)
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
        _ = new StatsRecorder(_eventBus, statsRepository, profileId);

        _cardLayer = new SpriteLayer(10);
        _uiLayer = new SpriteLayer(20);
        _sceneRenderer.AddLayer(_cardLayer);
        _sceneRenderer.AddLayer(_uiLayer);

        var buttonTexture = _content.Load<Texture2D>("Controls/Button");
        _font = _content.Load<SpriteFont>("Fonts/MyFont");

        _hitButton = new Button(buttonTexture, _font)
        {
            Text = "Hit",
            PenColor = Color.Black
        };
        _standButton = new Button(buttonTexture, _font)
        {
            Text = "Stand",
            PenColor = Color.Black
        };
        _splitButton = new Button(buttonTexture, _font)
        {
            Text = "Split",
            PenColor = Color.Black
        };
        _doubleButton = new Button(buttonTexture, _font)
        {
            Text = "Double",
            PenColor = Color.Black
        };
        _surrenderButton = new Button(buttonTexture, _font)
        {
            Text = "Surrender",
            PenColor = Color.Black
        };
        _insuranceButton = new Button(buttonTexture, _font)
        {
            Text = "Insurance",
            PenColor = Color.Black
        };
        _declineInsuranceButton = new Button(buttonTexture, _font)
        {
            Text = "No Thanks",
            PenColor = Color.Black
        };

        _hitButton.Click += OnHitClicked;
        _standButton.Click += OnStandClicked;
        _splitButton.Click += OnSplitClicked;
        _doubleButton.Click += OnDoubleClicked;
        _surrenderButton.Click += OnSurrenderClicked;
        _insuranceButton.Click += OnInsuranceClicked;
        _declineInsuranceButton.Click += OnDeclineInsuranceClicked;

        _eventBus.Subscribe<CardDealt>(OnCardDealt);
        _eventBus.Subscribe<InitialDealComplete>(OnInitialDealComplete);
        _eventBus.Subscribe<PlayerHit>(OnPlayerHit);
        _eventBus.Subscribe<PlayerDoubledDown>(OnPlayerDoubledDown);
        _eventBus.Subscribe<PlayerBusted>(OnPlayerBusted);
        _eventBus.Subscribe<PlayerStood>(OnPlayerStood);
        _eventBus.Subscribe<PlayerTurnStarted>(OnPlayerTurnStarted);
        _eventBus.Subscribe<PlayerSplit>(OnPlayerSplit);
        _eventBus.Subscribe<DealerTurnStarted>(OnDealerTurnStarted);
        _eventBus.Subscribe<DealerHit>(OnDealerHit);
        _eventBus.Subscribe<DealerHoleCardRevealed>(OnDealerHoleCardRevealed);
        _eventBus.Subscribe<DealerBusted>(OnDealerBusted);
        _eventBus.Subscribe<DealerStood>(OnDealerStood);
        _eventBus.Subscribe<InsuranceOffered>(OnInsuranceOffered);
        _eventBus.Subscribe<InsuranceResult>(OnInsuranceResult);
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
        _splitButton.Size = buttonSize;
        _doubleButton.Size = buttonSize;
        _surrenderButton.Size = buttonSize;
        _insuranceButton.Size = buttonSize;
        _declineInsuranceButton.Size = buttonSize;

        // Primary actions row: Hit | Split | Stand
        _hitButton.Position = new Vector2(centerX - gap * 1.1f, buttonY);
        _splitButton.Position = new Vector2(centerX, buttonY);
        _standButton.Position = new Vector2(centerX + gap * 1.1f, buttonY);
        var secondaryY = buttonY + buttonSize.Y + vp.Height / 80f;
        _doubleButton.Position = new Vector2(centerX - gap * 0.8f, secondaryY);
        _surrenderButton.Position = new Vector2(centerX + gap * 0.8f, secondaryY);
        _insuranceButton.Position = new Vector2(centerX - gap, buttonY);
        _declineInsuranceButton.Position = new Vector2(centerX + gap, buttonY);
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

    private int GetPlayerHandCount()
    {
        return _playerHandCardCounts.Count;
    }

    private Vector2 GetCardTargetPosition(string recipient, int handIndex, int cardIndexInHand)
    {
        var vp = _graphicsDevice.Viewport;
        var spacing = _cardSize.X * 1.15f;

        if (recipient == _dealer.Name)
        {
            // Dealer: single row, centered
            var baseX = vp.Width / 2f - _cardSize.X / 2f - spacing * 0.5f;
            return new Vector2(baseX + spacing * cardIndexInHand, GetDealerCardsY());
        }

        // Player: hands spread horizontally from center
        int totalHands = GetPlayerHandCount();
        if (totalHands <= 1)
        {
            // Single hand: centered exactly as before
            var baseX = vp.Width / 2f - _cardSize.X / 2f - spacing * 0.5f;
            return new Vector2(baseX + spacing * cardIndexInHand, GetPlayerCardsY());
        }

        // Multiple hands: spread horizontally
        var cardOverlap = _cardSize.X * 0.35f; // Cards within a hand overlap
        var handGap = _cardSize.X * 1.8f; // Space between hand groups

        // Calculate total width of all hands
        float totalWidth = 0;
        for (int h = 0; h < totalHands; h++)
        {
            int cardCount = _playerHandCardCounts.ContainsKey(h) ? _playerHandCardCounts[h] : 2;
            totalWidth += _cardSize.X + cardOverlap * (cardCount - 1);
        }
        totalWidth += handGap * (totalHands - 1);

        // Find x offset for this hand
        float handStartX = vp.Width / 2f - totalWidth / 2f;
        for (int h = 0; h < handIndex; h++)
        {
            int cardCount = _playerHandCardCounts.ContainsKey(h) ? _playerHandCardCounts[h] : 2;
            handStartX += _cardSize.X + cardOverlap * (cardCount - 1) + handGap;
        }

        return new Vector2(handStartX + cardOverlap * cardIndexInHand, GetPlayerCardsY());
    }

    private bool IsPlayerInteractionEnabled()
    {
        return !_tweenManager.HasActiveTweens && _round.Phase == RoundPhase.PlayerTurn;
    }

    private bool IsInsuranceInteractionEnabled()
    {
        return !_tweenManager.HasActiveTweens && _round.Phase == RoundPhase.Insurance;
    }

    private void AddAnimatedCard(Card card, bool faceDown, string recipient, int handIndex, float delay)
    {
        var sprite = _cardRenderer.CreateCardSprite(card, faceDown);
        sprite.Size = _cardSize;
        sprite.Position = _deckPosition;
        sprite.Opacity = 0f;
        sprite.ZOrder = _dealCardIndex;

        int recipientCardIndex;
        if (recipient == _dealer.Name)
        {
            recipientCardIndex = _dealerCardCount++;
        }
        else
        {
            if (!_playerHandCardCounts.ContainsKey(handIndex))
                _playerHandCardCounts[handIndex] = 0;
            recipientCardIndex = _playerHandCardCounts[handIndex]++;
        }

        _trackedCards.Add(new TrackedCardSprite(sprite, recipient, handIndex, recipientCardIndex));

        var target = GetCardTargetPosition(recipient, handIndex, recipientCardIndex);
        float duration = 0.4f;

        _tweenManager.Add(TweenBuilder.MoveTo(sprite, target, duration, delay, Easing.EaseOutQuad));
        _tweenManager.Add(TweenBuilder.FadeTo(sprite, 1f, duration * 0.5f, delay, Easing.Linear));

        _cardLayer.Add(sprite);
        _dealCardIndex++;
    }

    private TextSprite CreateLabel(string text, Color color, Vector2 position)
    {
        var label = new TextSprite
        {
            Text = text,
            Font = _font,
            TextColor = color,
            Position = position,
            Opacity = 0f,
            Scale = 0.5f,
            ZOrder = 100
        };

        _uiLayer.Add(label);
        _tweenManager.Add(TweenBuilder.FadeTo(label, 1f, 0.3f));
        _tweenManager.Add(TweenBuilder.ScaleTo(label, 1f, 0.3f, 0f, Easing.EaseOutBack));

        return label;
    }

    private Vector2 GetOutcomeLabelPosition(int handIndex = 0)
    {
        var vp = _graphicsDevice.Viewport;

        if (GetPlayerHandCount() <= 1)
        {
            // Single hand: centered
            var midY = (GetDealerCardsY() + _cardSize.Y + GetPlayerCardsY()) / 2f;
            return new Vector2(vp.Width / 2f, midY);
        }

        // Multiple hands: position above each hand's column
        int cardCount = _playerHandCardCounts.ContainsKey(handIndex) ? _playerHandCardCounts[handIndex] : 2;
        var handCenter = GetCardTargetPosition(_player.Name, handIndex, 0);
        var cardOverlap = _cardSize.X * 0.35f;
        float handWidth = _cardSize.X + cardOverlap * (cardCount - 1);
        float centerX = handCenter.X + handWidth / 2f;
        float labelY = GetPlayerCardsY() - 30f;

        return new Vector2(centerX, labelY);
    }

    private void RepositionPlayerCards()
    {
        foreach (var tracked in _trackedCards)
        {
            if (tracked.Recipient != _player.Name)
                continue;

            var target = GetCardTargetPosition(_player.Name, tracked.HandIndex, tracked.CardIndexInHand);
            _tweenManager.Add(TweenBuilder.MoveTo(tracked.Sprite, target, 0.3f, 0f, Easing.EaseOutQuad));
        }
    }

    private void StartNewRound()
    {
        _cardLayer.Clear();
        _uiLayer.Clear();
        _trackedCards.Clear();

        _dealCardIndex = 0;
        _playerHandCardCounts.Clear();
        _dealerCardCount = 0;
        _dealerAnimationDelay = 0f;
        _roundCompleteTimer = 0f;
        _waitingForRoundReset = false;
        _activePlayerHandIndex = 0;

        _round = new GameRound(_shoe, _player, _dealer, _eventBus.Publish);
        _round.PlaceBet(GameConfig.MinimumBet);
        _round.Deal();
    }

    private void OnCardDealt(CardDealt evt)
    {
        float delay = _dealCardIndex * 0.2f;
        AddAnimatedCard(evt.Card, evt.FaceDown, evt.Recipient, evt.HandIndex, delay);
    }

    private void OnInitialDealComplete(InitialDealComplete evt)
    {
        // Future: could trigger UI state changes here
    }

    private void OnPlayerTurnStarted(PlayerTurnStarted evt)
    {
        _activePlayerHandIndex = evt.HandIndex;
    }

    private void OnPlayerHit(PlayerHit evt)
    {
        AddAnimatedCard(evt.Card, false, _player.Name, evt.HandIndex, 0f);
    }

    private void OnPlayerDoubledDown(PlayerDoubledDown evt)
    {
        AddAnimatedCard(evt.Card, false, _player.Name, evt.HandIndex, 0f);
    }

    private void OnPlayerBusted(PlayerBusted evt)
    {
        var pos = GetOutcomeLabelPosition(evt.HandIndex);
        CreateLabel("BUST", Color.Red, pos);
    }

    private void OnPlayerStood(PlayerStood evt)
    {
        // No visual change for now; buttons hide automatically by phase.
    }

    private void OnPlayerSplit(PlayerSplit evt)
    {
        // Find the tracked card that was the 2nd card in the original hand
        for (int i = 0; i < _trackedCards.Count; i++)
        {
            var tracked = _trackedCards[i];
            if (tracked.Recipient == _player.Name
                && tracked.HandIndex == evt.OriginalHandIndex
                && tracked.CardIndexInHand == 1)
            {
                // Update to the new hand
                _trackedCards[i] = tracked with { HandIndex = evt.NewHandIndex, CardIndexInHand = 0 };

                // Update card counts: original hand loses a card, new hand gets one
                if (_playerHandCardCounts.ContainsKey(evt.OriginalHandIndex))
                    _playerHandCardCounts[evt.OriginalHandIndex]--;
                _playerHandCardCounts[evt.NewHandIndex] = 1;

                break;
            }
        }

        // Animate all player cards to new multi-hand layout
        RepositionPlayerCards();
    }

    private void OnDealerTurnStarted(DealerTurnStarted evt)
    {
        _dealerAnimationDelay = 0f;
    }

    private void OnDealerHit(DealerHit evt)
    {
        AddAnimatedCard(evt.Card, false, _dealer.Name, 0, _dealerAnimationDelay);
        _dealerAnimationDelay += 0.3f;
    }

    private void OnDealerHoleCardRevealed(DealerHoleCardRevealed evt)
    {
        for (int i = 0; i < _trackedCards.Count; i++)
        {
            var tracked = _trackedCards[i];
            if (tracked.Recipient == _dealer.Name && tracked.CardIndexInHand == 1)
            {
                var sprite = tracked.Sprite;
                _tweenManager.Add(TweenBuilder.FlipX(sprite, 0.3f, 0f,
                    onMidpoint: () => sprite.FaceDown = false));
                break;
            }
        }
    }

    private void OnInsuranceOffered(InsuranceOffered evt)
    {
        var vp = _graphicsDevice.Viewport;
        var labelY = (GetDealerCardsY() + _cardSize.Y + GetPlayerCardsY()) / 2f;
        CreateLabel("INSURANCE?", Color.Yellow, new Vector2(vp.Width / 2f, labelY));
    }

    private void OnInsuranceResult(InsuranceResult evt)
    {
        var vp = _graphicsDevice.Viewport;
        var labelY = (GetDealerCardsY() + _cardSize.Y + GetPlayerCardsY()) / 2f;
        if (evt.DealerHadBlackjack && evt.Payout > 0)
        {
            CreateLabel($"INSURANCE PAYS ${evt.Payout}", Color.Gold,
                new Vector2(vp.Width / 2f, labelY - 30f));
        }
        else if (!evt.DealerHadBlackjack && evt.Payout < 0)
        {
            CreateLabel($"INSURANCE LOST", Color.Gray,
                new Vector2(vp.Width / 2f, labelY - 30f));
        }
    }

    private void OnDealerBusted(DealerBusted evt)
    {
        var vp = _graphicsDevice.Viewport;
        var bustY = GetDealerCardsY() - 20f;
        CreateLabel("BUST", Color.Red, new Vector2(vp.Width / 2f, bustY));
    }

    private void OnDealerStood(DealerStood evt)
    {
        // No visual change needed.
    }

    private void OnHandResolved(HandResolved evt)
    {
        var (text, color) = evt.Outcome switch
        {
            HandOutcome.Win => ("WIN", Color.Gold),
            HandOutcome.Blackjack => ("BLACKJACK!", Color.Gold),
            HandOutcome.Lose => ("LOSE", Color.Red),
            HandOutcome.Push => ("PUSH", Color.White),
            HandOutcome.Surrender => ("SURRENDER", Color.Gray),
            _ => ("", Color.White)
        };

        if (!string.IsNullOrEmpty(text))
            CreateLabel(text, color, GetOutcomeLabelPosition(evt.HandIndex));
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

    private void OnSplitClicked(object? sender, EventArgs e)
    {
        if (!IsPlayerInteractionEnabled())
            return;
        if (!_round.CanSplit())
            return;

        _round.PlayerSplit();
        _eventBus.Flush();
    }

    private void OnDoubleClicked(object? sender, EventArgs e)
    {
        if (!IsPlayerInteractionEnabled())
            return;
        if (!_round.CanDoubleDown())
            return;

        _round.PlayerDoubleDown();
        _eventBus.Flush();
    }

    private void OnSurrenderClicked(object? sender, EventArgs e)
    {
        if (!IsPlayerInteractionEnabled())
            return;
        if (!_round.CanSurrender())
            return;

        _round.PlayerSurrender();
        _eventBus.Flush();
    }

    private void OnInsuranceClicked(object? sender, EventArgs e)
    {
        if (!IsInsuranceInteractionEnabled())
            return;

        _uiLayer.Clear();
        _round.PlaceInsurance();
        _eventBus.Flush();
    }

    private void OnDeclineInsuranceClicked(object? sender, EventArgs e)
    {
        if (!IsInsuranceInteractionEnabled())
            return;

        _uiLayer.Clear();
        _round.DeclineInsurance();
        _eventBus.Flush();
    }

    public override void Update(GameTime gameTime)
    {
        _eventBus.Flush();

        if (IsPlayerInteractionEnabled())
        {
            _hitButton.Update(gameTime);
            _standButton.Update(gameTime);
            if (_round.CanSplit())
                _splitButton.Update(gameTime);
            if (_round.CanDoubleDown())
                _doubleButton.Update(gameTime);
            if (_round.CanSurrender())
                _surrenderButton.Update(gameTime);
        }
        else if (IsInsuranceInteractionEnabled())
        {
            _insuranceButton.Update(gameTime);
            _declineInsuranceButton.Update(gameTime);
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
            if (_round.CanSplit())
                _splitButton.Draw(gameTime, spriteBatch);
            if (_round.CanDoubleDown())
                _doubleButton.Draw(gameTime, spriteBatch);
            if (_round.CanSurrender())
                _surrenderButton.Draw(gameTime, spriteBatch);

            // Draw active hand indicator when multiple hands
            if (GetPlayerHandCount() > 1)
                DrawActiveHandIndicator(spriteBatch);

            spriteBatch.End();
        }
        else if (IsInsuranceInteractionEnabled())
        {
            spriteBatch.Begin();
            _insuranceButton.Draw(gameTime, spriteBatch);
            _declineInsuranceButton.Draw(gameTime, spriteBatch);
            spriteBatch.End();
        }
    }

    private void DrawActiveHandIndicator(SpriteBatch spriteBatch)
    {
        int cardCount = _playerHandCardCounts.ContainsKey(_activePlayerHandIndex)
            ? _playerHandCardCounts[_activePlayerHandIndex] : 2;

        var firstCardPos = GetCardTargetPosition(_player.Name, _activePlayerHandIndex, 0);
        var cardOverlap = _cardSize.X * 0.35f;
        float handWidth = _cardSize.X + cardOverlap * (cardCount - 1);

        float indicatorY = GetPlayerCardsY() + _cardSize.Y + 8f;
        float indicatorHeight = 4f;

        var rect = new Rectangle(
            (int)firstCardPos.X,
            (int)indicatorY,
            (int)handWidth,
            (int)indicatorHeight);

        // Draw a simple colored rectangle using a 1x1 white pixel
        var pixel = new Texture2D(_graphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.Gold });
        spriteBatch.Draw(pixel, rect, Color.Gold);
        pixel.Dispose();
    }

    public override void HandleResize(Rectangle vp)
    {
        CalculatePositions();
        _sceneRenderer.HandleResize(vp);

        _tweenManager.Clear();

        foreach (var tracked in _trackedCards)
        {
            tracked.Sprite.Size = _cardSize;
            tracked.Sprite.Position = GetCardTargetPosition(tracked.Recipient, tracked.HandIndex, tracked.CardIndexInHand);
            tracked.Sprite.Opacity = 1f;
            tracked.Sprite.ScaleX = 1f;
        }
    }

    public override void PostUpdate(GameTime gameTime) { }

    private readonly record struct TrackedCardSprite(CardSprite Sprite, string Recipient, int HandIndex, int CardIndexInHand);
}
