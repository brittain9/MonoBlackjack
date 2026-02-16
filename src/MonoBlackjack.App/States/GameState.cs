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

    private readonly Texture2D _pixelTexture;
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

    // Betting phase buttons
    private readonly Button _betDownButton;
    private readonly Button _betUpButton;
    private readonly Button _dealButton;
    private readonly Button _repeatBetButton;

    // Bankrupt phase buttons
    private readonly Button _resetBankrollButton;
    private readonly Button _menuButton;
    private readonly StatsRecorder _statsRecorder;
    private readonly List<IDisposable> _subscriptions = [];

    private readonly List<TrackedCardSprite> _trackedCards = [];
    private readonly HashSet<int> _bustedHands = new();

    private enum GamePhase { Betting, Playing, Bankrupt }
    private GamePhase _gamePhase;
    private decimal _pendingBet;
    private decimal _lastBet;

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
        _pixelTexture = game.PixelTexture;

        _cardRenderer = new CardRenderer();
        _cardRenderer.LoadTextures(content);

        _shoe = new Shoe(GameConfig.NumberOfDecks);
        _player = new Human();
        _dealer = new Dealer();

        _eventBus = new EventBus();
        _tweenManager = new TweenManager();
        _sceneRenderer = new SceneRenderer();
        _statsRecorder = new StatsRecorder(_eventBus, statsRepository, profileId);

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

        // Betting phase buttons
        _betDownButton = new Button(buttonTexture, _font) { Text = "<", PenColor = Color.Black };
        _betUpButton = new Button(buttonTexture, _font) { Text = ">", PenColor = Color.Black };
        _dealButton = new Button(buttonTexture, _font) { Text = "Deal", PenColor = Color.Black };
        _repeatBetButton = new Button(buttonTexture, _font) { Text = "Repeat Bet", PenColor = Color.Black };

        // Bankrupt phase buttons
        _resetBankrollButton = new Button(buttonTexture, _font) { Text = "Reset", PenColor = Color.Black };
        _menuButton = new Button(buttonTexture, _font) { Text = "Menu", PenColor = Color.Black };

        _hitButton.Click += OnHitClicked;
        _standButton.Click += OnStandClicked;
        _splitButton.Click += OnSplitClicked;
        _doubleButton.Click += OnDoubleClicked;
        _surrenderButton.Click += OnSurrenderClicked;
        _insuranceButton.Click += OnInsuranceClicked;
        _declineInsuranceButton.Click += OnDeclineInsuranceClicked;
        _betDownButton.Click += OnBetDownClicked;
        _betUpButton.Click += OnBetUpClicked;
        _dealButton.Click += OnDealClicked;
        _repeatBetButton.Click += OnRepeatBetClicked;
        _resetBankrollButton.Click += OnResetBankrollClicked;
        _menuButton.Click += OnMenuClicked;

        _subscriptions.Add(_eventBus.Subscribe<CardDealt>(OnCardDealt));
        _subscriptions.Add(_eventBus.Subscribe<InitialDealComplete>(OnInitialDealComplete));
        _subscriptions.Add(_eventBus.Subscribe<PlayerHit>(OnPlayerHit));
        _subscriptions.Add(_eventBus.Subscribe<PlayerDoubledDown>(OnPlayerDoubledDown));
        _subscriptions.Add(_eventBus.Subscribe<PlayerBusted>(OnPlayerBusted));
        _subscriptions.Add(_eventBus.Subscribe<PlayerStood>(OnPlayerStood));
        _subscriptions.Add(_eventBus.Subscribe<PlayerTurnStarted>(OnPlayerTurnStarted));
        _subscriptions.Add(_eventBus.Subscribe<PlayerSplit>(OnPlayerSplit));
        _subscriptions.Add(_eventBus.Subscribe<DealerTurnStarted>(OnDealerTurnStarted));
        _subscriptions.Add(_eventBus.Subscribe<DealerHit>(OnDealerHit));
        _subscriptions.Add(_eventBus.Subscribe<DealerHoleCardRevealed>(OnDealerHoleCardRevealed));
        _subscriptions.Add(_eventBus.Subscribe<DealerBusted>(OnDealerBusted));
        _subscriptions.Add(_eventBus.Subscribe<DealerStood>(OnDealerStood));
        _subscriptions.Add(_eventBus.Subscribe<InsuranceOffered>(OnInsuranceOffered));
        _subscriptions.Add(_eventBus.Subscribe<InsuranceResult>(OnInsuranceResult));
        _subscriptions.Add(_eventBus.Subscribe<HandResolved>(OnHandResolved));
        _subscriptions.Add(_eventBus.Subscribe<RoundComplete>(OnRoundComplete));

        CalculatePositions();

        if (GameConfig.BetFlow == BetFlowMode.FreePlay)
        {
            _gamePhase = GamePhase.Playing;
            _round = new GameRound(_shoe, _player, _dealer, _eventBus.Publish);
            _round.PlaceBet(0);
            _round.Deal();
        }
        else
        {
            _gamePhase = GamePhase.Betting;
            _pendingBet = GameConfig.MinimumBet;
            _round = null!;
        }
    }

    private void CalculatePositions()
    {
        var vp = _graphicsDevice.Viewport;
        var cardHeight = Math.Clamp(vp.Height * 0.2f, 120f, 220f);
        var cardWidth = cardHeight * CardAspectRatio;
        _cardSize = new Vector2(cardWidth, cardHeight);

        _deckPosition = new Vector2(-_cardSize.X, vp.Height / 2f);

        var centerX = vp.Width / 2f;
        var buttonY = GetPlayerCardsY() + _cardSize.Y + vp.Height / 18f;

        // Calculate button size to fit 5 action buttons in a single row
        const int maxActionButtons = 5; // Hit, Stand, Split, Double, Surrender
        const float buttonPadding = 12f; // Minimum gap between buttons
        var availableWidth = vp.Width * 0.95f; // Use 95% of viewport width
        var maxButtonWidth = (availableWidth - (buttonPadding * (maxActionButtons - 1))) / maxActionButtons;
        var buttonWidth = Math.Min(_cardSize.X * 1.2f, maxButtonWidth);
        var buttonHeight = _cardSize.Y * 0.35f;
        var buttonSize = new Vector2(buttonWidth, buttonHeight);

        // Set all button sizes
        _hitButton.Size = buttonSize;
        _standButton.Size = buttonSize;
        _splitButton.Size = buttonSize;
        _doubleButton.Size = buttonSize;
        _surrenderButton.Size = buttonSize;
        _insuranceButton.Size = buttonSize;
        _declineInsuranceButton.Size = buttonSize;

        // Single row layout for action buttons: Hit | Stand | Split | Double | Surrender
        var totalRowWidth = (buttonWidth * maxActionButtons) + (buttonPadding * (maxActionButtons - 1));
        var startX = centerX - (totalRowWidth / 2f) + (buttonWidth / 2f);

        _hitButton.Position = new Vector2(startX, buttonY);
        _standButton.Position = new Vector2(startX + (buttonWidth + buttonPadding), buttonY);
        _splitButton.Position = new Vector2(startX + (buttonWidth + buttonPadding) * 2, buttonY);
        _doubleButton.Position = new Vector2(startX + (buttonWidth + buttonPadding) * 3, buttonY);
        _surrenderButton.Position = new Vector2(startX + (buttonWidth + buttonPadding) * 4, buttonY);

        // Insurance buttons (shown only during insurance offer, centered)
        var insuranceTotalWidth = (buttonWidth * 2) + buttonPadding;
        var insuranceStartX = centerX - (insuranceTotalWidth / 2f) + (buttonWidth / 2f);
        _insuranceButton.Position = new Vector2(insuranceStartX, buttonY);
        _declineInsuranceButton.Position = new Vector2(insuranceStartX + buttonWidth + buttonPadding, buttonY);

        // Betting phase layout
        var arrowSize = new Vector2(buttonSize.Y * 1.2f, buttonSize.Y);
        var betCenterY = vp.Height * 0.5f;
        var betArrowSpacing = buttonSize.X * 0.8f;
        _betDownButton.Size = arrowSize;
        _betUpButton.Size = arrowSize;
        _betDownButton.Position = new Vector2(centerX - betArrowSpacing, betCenterY);
        _betUpButton.Position = new Vector2(centerX + betArrowSpacing, betCenterY);

        _dealButton.Size = buttonSize;
        _dealButton.Position = new Vector2(centerX, betCenterY + buttonSize.Y * 1.5f);

        _repeatBetButton.Size = buttonSize;
        _repeatBetButton.Position = new Vector2(centerX, betCenterY + buttonSize.Y * 2.8f);

        // Bankrupt phase layout - two buttons centered
        _resetBankrollButton.Size = buttonSize;
        _menuButton.Size = buttonSize;
        var bankruptY = vp.Height * 0.55f;
        var bankruptTotalWidth = (buttonWidth * 2) + buttonPadding;
        var bankruptStartX = centerX - (bankruptTotalWidth / 2f) + (buttonWidth / 2f);
        _resetBankrollButton.Position = new Vector2(bankruptStartX, bankruptY);
        _menuButton.Position = new Vector2(bankruptStartX + buttonWidth + buttonPadding, bankruptY);
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

    /// <summary>
    /// Returns the CENTER position for a card sprite (all sprites now use center-anchor).
    /// </summary>
    private Vector2 GetCardTargetPosition(string recipient, int handIndex, int cardIndexInHand)
    {
        var vp = _graphicsDevice.Viewport;
        var halfCard = _cardSize / 2f;

        if (recipient == _dealer.Name)
        {
            int dealerTotal = Math.Max(_dealerCardCount, 2);
            return ComputeRowCardCenter(vp, dealerTotal, cardIndexInHand, GetDealerCardsY() + halfCard.Y);
        }

        // Player: hands spread horizontally from center
        int totalHands = GetPlayerHandCount();
        float centerY = GetPlayerCardsY() + halfCard.Y;

        if (totalHands <= 1)
        {
            int cardCount = _playerHandCardCounts.GetValueOrDefault(0, 2);
            return ComputeRowCardCenter(vp, cardCount, cardIndexInHand, centerY);
        }

        // Multiple hands: spread horizontally
        var cardOverlap = _cardSize.X * 0.45f;
        var handGap = _cardSize.X * 1.8f;

        // Calculate total width of all hands
        float totalWidth = ComputeMultiHandWidth(totalHands, cardOverlap, handGap);

        // Adaptive scaling: if total width exceeds viewport, scale down
        float maxWidth = vp.Width * 0.9f;
        if (totalWidth > maxWidth)
        {
            float scale = maxWidth / totalWidth;
            cardOverlap *= scale;
            handGap *= scale;
            totalWidth = ComputeMultiHandWidth(totalHands, cardOverlap, handGap);
        }

        // Find x offset for this hand's first card center
        float handStartX = vp.Width / 2f - totalWidth / 2f;
        for (int h = 0; h < handIndex; h++)
        {
            int cc = _playerHandCardCounts.GetValueOrDefault(h, 2);
            handStartX += _cardSize.X + cardOverlap * (cc - 1) + handGap;
        }

        // Card center within this hand
        float cardCenterX = handStartX + halfCard.X + cardOverlap * cardIndexInHand;
        return new Vector2(cardCenterX, centerY);
    }

    /// <summary>
    /// Computes center position for a card in a single row (dealer or single player hand).
    /// Adapts overlap when cards would overflow the viewport.
    /// </summary>
    private Vector2 ComputeRowCardCenter(Viewport vp, int cardCount, int cardIndex, float centerY)
    {
        var halfCard = _cardSize / 2f;
        var spacing = _cardSize.X * 1.15f;

        // Adaptive: if row overflows viewport, compress spacing
        float rowWidth = _cardSize.X + spacing * (cardCount - 1);
        float maxWidth = vp.Width * 0.9f;
        if (rowWidth > maxWidth && cardCount > 1)
            spacing = (maxWidth - _cardSize.X) / (cardCount - 1);

        float totalRowWidth = _cardSize.X + spacing * (cardCount - 1);
        float startX = vp.Width / 2f - totalRowWidth / 2f;
        float cardCenterX = startX + halfCard.X + spacing * cardIndex;
        return new Vector2(cardCenterX, centerY);
    }

    private float ComputeMultiHandWidth(int totalHands, float cardOverlap, float handGap)
    {
        float totalWidth = 0;
        for (int h = 0; h < totalHands; h++)
        {
            int cc = _playerHandCardCounts.GetValueOrDefault(h, 2);
            totalWidth += _cardSize.X + cardOverlap * (cc - 1);
        }
        totalWidth += handGap * (totalHands - 1);
        return totalWidth;
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
            // Single hand: centered between dealer and player card rows
            float dealerBottom = GetDealerCardsY() + _cardSize.Y;
            float playerTop = GetPlayerCardsY();
            float midY = (dealerBottom + playerTop) / 2f;
            return new Vector2(vp.Width / 2f, midY);
        }

        // Multiple hands: position above each hand's column
        // Card positions are now center-anchored, so average of first+last = hand center
        int cardCount = _playerHandCardCounts.GetValueOrDefault(handIndex, 2);
        var firstCardCenter = GetCardTargetPosition(_player.Name, handIndex, 0);
        var lastCardCenter = GetCardTargetPosition(_player.Name, handIndex, cardCount - 1);
        float centerX = (firstCardCenter.X + lastCardCenter.X) / 2f;
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

    private void ClearRoundVisualState()
    {
        _cardLayer.Clear();
        _uiLayer.Clear();
        _trackedCards.Clear();
        _bustedHands.Clear();

        _dealCardIndex = 0;
        _playerHandCardCounts.Clear();
        _dealerCardCount = 0;
        _dealerAnimationDelay = 0f;
        _roundCompleteTimer = 0f;
        _waitingForRoundReset = false;
        _activePlayerHandIndex = 0;
    }

    private void StartNewRound()
    {
        ClearRoundVisualState();

        if (GameConfig.BetFlow == BetFlowMode.FreePlay)
        {
            _gamePhase = GamePhase.Playing;
            _round = new GameRound(_shoe, _player, _dealer, _eventBus.Publish);
            _round.PlaceBet(0);
            _round.Deal();
        }
        else
        {
            // Check for bankruptcy
            if (_player.Bank < GameConfig.MinimumBet)
            {
                _gamePhase = GamePhase.Bankrupt;
                return;
            }

            _gamePhase = GamePhase.Betting;
            _pendingBet = Math.Clamp(_pendingBet, GameConfig.MinimumBet,
                Math.Min(GameConfig.MaximumBet, _player.Bank));
        }
    }

    private void DealWithBet(decimal bet)
    {
        ClearRoundVisualState();
        _gamePhase = GamePhase.Playing;
        _lastBet = bet;
        _round = new GameRound(_shoe, _player, _dealer, _eventBus.Publish);
        _round.PlaceBet(bet);
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
        _bustedHands.Add(evt.HandIndex);
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
        // Skip LOSE label for busted hands — they already show BUST
        if (evt.Outcome == HandOutcome.Lose && _bustedHands.Contains(evt.HandIndex))
            return;

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

        // For betting mode, check bankruptcy after round ends (bankrupt state set in StartNewRound)
    }

    private void OnBetDownClicked(object? sender, EventArgs e)
    {
        if (_gamePhase != GamePhase.Betting) return;
        _pendingBet = Math.Max(GameConfig.MinimumBet, _pendingBet - GameConfig.MinimumBet);
    }

    private void OnBetUpClicked(object? sender, EventArgs e)
    {
        if (_gamePhase != GamePhase.Betting) return;
        var max = Math.Min(GameConfig.MaximumBet, _player.Bank);
        _pendingBet = Math.Min(max, _pendingBet + GameConfig.MinimumBet);
    }

    private void OnDealClicked(object? sender, EventArgs e)
    {
        if (_gamePhase != GamePhase.Betting) return;
        DealWithBet(_pendingBet);
    }

    private void OnRepeatBetClicked(object? sender, EventArgs e)
    {
        if (_gamePhase != GamePhase.Betting) return;
        if (_lastBet <= 0) return;
        var bet = Math.Clamp(_lastBet, GameConfig.MinimumBet, Math.Min(GameConfig.MaximumBet, _player.Bank));
        DealWithBet(bet);
    }

    private void OnResetBankrollClicked(object? sender, EventArgs e)
    {
        if (_gamePhase != GamePhase.Bankrupt) return;
        _player.Bank = GameConfig.StartingBank;
        _pendingBet = GameConfig.MinimumBet;
        _gamePhase = GamePhase.Betting;
    }

    private void OnMenuClicked(object? sender, EventArgs e)
    {
        _game.ChangeState(new MenuState(_game, _graphicsDevice, _content));
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
        if (_gamePhase == GamePhase.Betting)
        {
            _betDownButton.Update(gameTime);
            _betUpButton.Update(gameTime);
            _dealButton.Update(gameTime);
            if (_lastBet > 0)
                _repeatBetButton.Update(gameTime);
            return;
        }

        if (_gamePhase == GamePhase.Bankrupt)
        {
            _resetBankrollButton.Update(gameTime);
            _menuButton.Update(gameTime);
            return;
        }

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
        var vp = _graphicsDevice.Viewport;
        bool isBettingMode = GameConfig.BetFlow == BetFlowMode.Betting;

        if (_gamePhase == GamePhase.Betting)
        {
            spriteBatch.Begin();
            DrawHud(spriteBatch, vp);

            // Bet amount display centered between arrows
            var betText = $"${_pendingBet}";
            var betTextSize = _font.MeasureString(betText);
            var betTextPos = new Vector2(vp.Width / 2f - betTextSize.X / 2f, vp.Height * 0.5f - betTextSize.Y / 2f);
            spriteBatch.DrawString(_font, betText, betTextPos, Color.Gold);

            var betLabel = "Place Your Bet";
            var labelSize = _font.MeasureString(betLabel) * 0.8f;
            var labelPos = new Vector2(vp.Width / 2f - labelSize.X / 2f, vp.Height * 0.5f - betTextSize.Y - 20f);
            spriteBatch.DrawString(_font, betLabel, labelPos, Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);

            _betDownButton.Draw(gameTime, spriteBatch);
            _betUpButton.Draw(gameTime, spriteBatch);
            _dealButton.Draw(gameTime, spriteBatch);
            if (_lastBet > 0)
                _repeatBetButton.Draw(gameTime, spriteBatch);

            spriteBatch.End();
            return;
        }

        if (_gamePhase == GamePhase.Bankrupt)
        {
            spriteBatch.Begin();

            var outText = "Out of Funds";
            var outSize = _font.MeasureString(outText);
            var outPos = new Vector2(vp.Width / 2f - outSize.X / 2f, vp.Height * 0.4f);
            spriteBatch.DrawString(_font, outText, outPos, Color.Red);

            _resetBankrollButton.Draw(gameTime, spriteBatch);
            _menuButton.Draw(gameTime, spriteBatch);

            spriteBatch.End();
            return;
        }

        // Playing phase
        _sceneRenderer.Draw(spriteBatch);

        if (IsPlayerInteractionEnabled())
        {
            spriteBatch.Begin();
            if (isBettingMode) DrawHud(spriteBatch, vp);
            DrawHandValues(spriteBatch);
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
            if (isBettingMode) DrawHud(spriteBatch, vp);
            DrawHandValues(spriteBatch);
            _insuranceButton.Draw(gameTime, spriteBatch);
            _declineInsuranceButton.Draw(gameTime, spriteBatch);
            spriteBatch.End();
        }
        else if (isBettingMode)
        {
            // Draw HUD during dealer turn / resolution too
            spriteBatch.Begin();
            DrawHud(spriteBatch, vp);
            DrawHandValues(spriteBatch);
            spriteBatch.End();
        }
    }

    private void DrawHud(SpriteBatch spriteBatch, Viewport vp)
    {
        var bankText = $"Bank: ${_player.Bank}";
        var bankSize = _font.MeasureString(bankText) * 0.7f;
        spriteBatch.DrawString(_font, bankText, new Vector2(12f, 8f), Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        if (_gamePhase == GamePhase.Playing && _round != null!)
        {
            var betText = $"Bet: ${_lastBet}";
            var betSize = _font.MeasureString(betText) * 0.7f;
            spriteBatch.DrawString(_font, betText, new Vector2(vp.Width - betSize.X - 12f, 8f), Color.Gold, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        }
    }

    private void DrawHandValues(SpriteBatch spriteBatch)
    {
        if (_round == null! || _gamePhase != GamePhase.Playing)
            return;

        var vp = _graphicsDevice.Viewport;
        const float scale = 0.9f;

        // Draw dealer hand value (only show upcard value before dealer turn)
        if (_dealerCardCount > 0)
        {
            string dealerValueText;
            if (_round.Phase == RoundPhase.DealerTurn || _round.Phase == RoundPhase.Resolution || _round.Phase == RoundPhase.Complete)
            {
                // Show full dealer hand value
                int dealerValue = _dealer.Hand.Value;
                bool dealerSoft = _dealer.Hand.IsSoft;
                dealerValueText = dealerSoft ? $"{dealerValue} (soft)" : $"{dealerValue}";
            }
            else
            {
                // Show only upcard value
                dealerValueText = "?";
            }

            var dealerTextSize = _font.MeasureString(dealerValueText) * scale;
            var dealerY = GetDealerCardsY() - dealerTextSize.Y - 8f;
            var dealerX = vp.Width / 2f - dealerTextSize.X / 2f;
            spriteBatch.DrawString(_font, dealerValueText, new Vector2(dealerX, dealerY), Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        // Draw player hand values
        int handCount = GetPlayerHandCount();
        for (int h = 0; h < handCount; h++)
        {
            if (h >= _player.Hands.Count)
                continue;

            var hand = _player.Hands[h];
            int handValue = hand.Value;
            bool isSoft = hand.IsSoft;
            bool isBusted = _bustedHands.Contains(h);

            string valueText = isBusted ? "BUST" : (isSoft ? $"{handValue} (soft)" : $"{handValue}");
            Color valueColor = isBusted ? Color.Red : (h == _activePlayerHandIndex ? Color.Gold : Color.LightGray);

            var textSize = _font.MeasureString(valueText) * scale;

            // Position below the hand
            int cardCount = _playerHandCardCounts.GetValueOrDefault(h, 2);
            Vector2 firstCardPos = GetCardTargetPosition(_player.Name, h, 0);
            Vector2 lastCardPos = GetCardTargetPosition(_player.Name, h, cardCount - 1);
            float handCenterX = (firstCardPos.X + lastCardPos.X) / 2f;
            float handBottom = GetPlayerCardsY() + _cardSize.Y;

            var textX = handCenterX - textSize.X / 2f;
            var textY = handBottom + 8f;

            spriteBatch.DrawString(_font, valueText, new Vector2(textX, textY), valueColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    private void DrawActiveHandIndicator(SpriteBatch spriteBatch)
    {
        int cardCount = _playerHandCardCounts.GetValueOrDefault(_activePlayerHandIndex, 2);

        var firstCardCenter = GetCardTargetPosition(_player.Name, _activePlayerHandIndex, 0);
        var lastCardCenter = GetCardTargetPosition(_player.Name, _activePlayerHandIndex, cardCount - 1);
        float handCenterX = (firstCardCenter.X + lastCardCenter.X) / 2f;

        float triangleHeight = 10f;
        float triangleHalfWidth = 8f;
        float indicatorY = GetPlayerCardsY() + _cardSize.Y + 8f;

        // Draw upward-pointing triangle using horizontal line strips
        int rows = (int)triangleHeight;
        for (int row = 0; row < rows; row++)
        {
            float t = row / triangleHeight;
            float rowWidth = triangleHalfWidth * 2f * (1f - t);
            float x = handCenterX - rowWidth / 2f;
            float y = indicatorY + triangleHeight - 1 - row;

            var rect = new Rectangle((int)x, (int)y, (int)rowWidth, 1);
            spriteBatch.Draw(_pixelTexture, rect, Color.Gold);
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
            tracked.Sprite.Position = GetCardTargetPosition(tracked.Recipient, tracked.HandIndex, tracked.CardIndexInHand);
            tracked.Sprite.Opacity = 1f;
            tracked.Sprite.ScaleX = 1f;
        }
    }

    public override void PostUpdate(GameTime gameTime) { }

    public override void Dispose()
    {
        // Unsubscribe all event handlers to prevent memory leaks
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
        _subscriptions.Clear();

        // Dispose StatsRecorder (which also unsubscribes its handlers)
        _statsRecorder.Dispose();

        _eventBus.Clear();
        _tweenManager.Clear();
        _cardLayer.Clear();
        _uiLayer.Clear();
    }

    private readonly record struct TrackedCardSprite(CardSprite Sprite, string Recipient, int HandIndex, int CardIndexInHand);
}
