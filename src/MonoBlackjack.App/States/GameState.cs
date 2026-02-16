using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Animation;
using MonoBlackjack.Core;
using MonoBlackjack.Core.Events;
using MonoBlackjack.Core.Ports;
using MonoBlackjack.Core.Players;
using MonoBlackjack.Events;
using MonoBlackjack.Layout;
using MonoBlackjack.Rendering;
using MonoBlackjack.Stats;
using Microsoft.Xna.Framework.Input;

namespace MonoBlackjack;

internal class GameState : State
{
    private readonly Texture2D _pixelTexture;
    private readonly GameRules _rules;
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
    private readonly Button _pauseResumeButton;
    private readonly Button _pauseSettingsButton;
    private readonly Button _pauseQuitButton;
    private readonly Button _pauseConfirmQuitButton;
    private readonly Button _pauseCancelQuitButton;
    private readonly StatsRecorder _statsRecorder;
    private readonly List<IDisposable> _subscriptions = [];

    private readonly List<TrackedCardSprite> _trackedCards = [];
    private readonly HashSet<int> _bustedHands = new();
    private bool _isPaused;
    private bool _isQuitConfirmationVisible;
    private bool _showAlignmentGuides;

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
    private Vector2 _actionButtonSize;
    private float _actionButtonY;
    private float _actionButtonPadding;

    public GameState(
        BlackjackGame game,
        GraphicsDevice graphicsDevice,
        ContentManager content,
        IStatsRepository statsRepository,
        int profileId)
        : base(game, graphicsDevice, content)
    {
        _pixelTexture = game.PixelTexture;
        _rules = game.CurrentRules;

        _cardRenderer = new CardRenderer();
        _cardRenderer.LoadTextures(content);

        _shoe = new Shoe(_rules.NumberOfDecks, _rules.PenetrationPercent, _rules.UseCryptographicShuffle);
        _player = new Human("Player", _rules.StartingBank);
        _dealer = new Dealer(_rules.DealerHitsSoft17);

        _eventBus = new EventBus();
        _tweenManager = new TweenManager();
        _sceneRenderer = new SceneRenderer();
        _statsRecorder = new StatsRecorder(_eventBus, statsRepository, profileId, _rules);

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

        // Pause menu buttons
        _pauseResumeButton = new Button(buttonTexture, _font) { Text = "Resume", PenColor = Color.Black };
        _pauseSettingsButton = new Button(buttonTexture, _font) { Text = "Settings", PenColor = Color.Black };
        _pauseQuitButton = new Button(buttonTexture, _font) { Text = "Quit to Menu", PenColor = Color.Black };
        _pauseConfirmQuitButton = new Button(buttonTexture, _font) { Text = "Quit", PenColor = Color.Black };
        _pauseCancelQuitButton = new Button(buttonTexture, _font) { Text = "Cancel", PenColor = Color.Black };

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
        _pauseResumeButton.Click += OnPauseResumeClicked;
        _pauseSettingsButton.Click += OnPauseSettingsClicked;
        _pauseQuitButton.Click += OnPauseQuitClicked;
        _pauseConfirmQuitButton.Click += OnPauseConfirmQuitClicked;
        _pauseCancelQuitButton.Click += OnPauseCancelQuitClicked;

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

        if (_rules.BetFlow == BetFlowMode.FreePlay)
        {
            _gamePhase = GamePhase.Playing;
            _round = new GameRound(_shoe, _player, _dealer, _rules, _eventBus.Publish);
            _round.PlaceBet(0);
            _round.Deal();
        }
        else
        {
            _gamePhase = GamePhase.Betting;
            _pendingBet = _rules.MinimumBet;
            _round = null!;
        }
    }

    private void CalculatePositions()
    {
        var vp = _graphicsDevice.Viewport;
        _cardSize = GameLayoutCalculator.CalculateCardSize(vp.Height);

        _deckPosition = new Vector2(-_cardSize.X, vp.Height / 2f);

        var centerX = vp.Width / 2f;

        // Size buttons so 5 actions can fit, then reflow visible actions each frame.
        _actionButtonPadding = GameLayoutCalculator.CalculateActionButtonPadding(vp.Width);
        _actionButtonSize = GameLayoutCalculator.CalculateActionButtonSize(vp.Width, _cardSize, _actionButtonPadding);

        // Keep action buttons below hand-value labels to avoid collisions.
        var handValueScale = GetResponsiveScale(0.9f);
        var handValueHeight = _font.MeasureString("18").Y * handValueScale;
        _actionButtonY = GameLayoutCalculator.CalculateActionButtonY(
            vp.Height,
            GetPlayerCardsY(),
            _cardSize,
            _actionButtonSize,
            handValueHeight);

        // Set all button sizes once; row placement happens in LayoutActionButtons.
        _hitButton.Size = _actionButtonSize;
        _standButton.Size = _actionButtonSize;
        _splitButton.Size = _actionButtonSize;
        _doubleButton.Size = _actionButtonSize;
        _surrenderButton.Size = _actionButtonSize;
        _insuranceButton.Size = _actionButtonSize;
        _declineInsuranceButton.Size = _actionButtonSize;

        LayoutActionButtons();

        // Insurance buttons (shown only during insurance offer, centered)
        var insuranceTotalWidth = (_actionButtonSize.X * 2) + _actionButtonPadding;
        var insuranceStartX = centerX - (insuranceTotalWidth / 2f) + (_actionButtonSize.X / 2f);
        _insuranceButton.Position = new Vector2(insuranceStartX, _actionButtonY);
        _declineInsuranceButton.Position = new Vector2(insuranceStartX + _actionButtonSize.X + _actionButtonPadding, _actionButtonY);

        // Betting phase layout
        var arrowSize = new Vector2(
            _actionButtonSize.Y * UIConstants.BetArrowWidthToActionButtonHeightRatio,
            _actionButtonSize.Y);
        var betCenterY = vp.Height * UIConstants.BetCenterYRatio;
        var betArrowSpacing = _actionButtonSize.X * UIConstants.BetArrowSpacingToActionButtonWidthRatio;
        _betDownButton.Size = arrowSize;
        _betUpButton.Size = arrowSize;
        _betDownButton.Position = new Vector2(centerX - betArrowSpacing, betCenterY);
        _betUpButton.Position = new Vector2(centerX + betArrowSpacing, betCenterY);

        _dealButton.Size = _actionButtonSize;
        _dealButton.Position = new Vector2(centerX, betCenterY + vp.Height * UIConstants.DealButtonOffsetRatio);

        _repeatBetButton.Size = _actionButtonSize;
        _repeatBetButton.Position = new Vector2(centerX, betCenterY + vp.Height * UIConstants.RepeatBetButtonOffsetRatio);

        // Bankrupt phase layout - two buttons centered
        _resetBankrollButton.Size = _actionButtonSize;
        _menuButton.Size = _actionButtonSize;
        var bankruptY = vp.Height * UIConstants.BankruptButtonsYRatio;
        var bankruptTotalWidth = (_actionButtonSize.X * 2) + _actionButtonPadding;
        var bankruptStartX = centerX - (bankruptTotalWidth / 2f) + (_actionButtonSize.X / 2f);
        _resetBankrollButton.Position = new Vector2(bankruptStartX, bankruptY);
        _menuButton.Position = new Vector2(bankruptStartX + _actionButtonSize.X + _actionButtonPadding, bankruptY);

        // Pause menu layout
        _pauseResumeButton.Size = _actionButtonSize;
        _pauseSettingsButton.Size = _actionButtonSize;
        _pauseQuitButton.Size = _actionButtonSize;
        _pauseConfirmQuitButton.Size = _actionButtonSize;
        _pauseCancelQuitButton.Size = _actionButtonSize;

        float pauseStartY = vp.Height * 0.43f;
        float pauseSpacing = _actionButtonSize.Y * 1.2f;
        _pauseResumeButton.Position = new Vector2(centerX, pauseStartY);
        _pauseSettingsButton.Position = new Vector2(centerX, pauseStartY + pauseSpacing);
        _pauseQuitButton.Position = new Vector2(centerX, pauseStartY + pauseSpacing * 2f);

        float confirmY = pauseStartY + pauseSpacing * 1.5f;
        float confirmTotalWidth = (_actionButtonSize.X * 2) + _actionButtonPadding;
        float confirmStartX = centerX - (confirmTotalWidth / 2f) + (_actionButtonSize.X / 2f);
        _pauseConfirmQuitButton.Position = new Vector2(confirmStartX, confirmY);
        _pauseCancelQuitButton.Position = new Vector2(confirmStartX + _actionButtonSize.X + _actionButtonPadding, confirmY);
    }

    private void LayoutActionButtons()
    {
        IReadOnlyList<Button> rowButtons = _gamePhase == GamePhase.Playing && _round.Phase == RoundPhase.PlayerTurn
            ? GetVisibleActionButtons()
            : [_hitButton, _standButton, _splitButton, _doubleButton, _surrenderButton];

        if (rowButtons.Count == 0)
            return;

        var centerX = _graphicsDevice.Viewport.Width / 2f;
        var centers = GameLayoutCalculator.LayoutCenteredRowX(
            centerX,
            _actionButtonSize.X,
            _actionButtonPadding,
            rowButtons.Count);

        for (int i = 0; i < rowButtons.Count; i++)
        {
            var button = rowButtons[i];
            button.Size = _actionButtonSize;
            button.Position = new Vector2(centers[i], _actionButtonY);
        }
    }

    private List<Button> GetVisibleActionButtons()
    {
        var buttons = new List<Button>(5)
        {
            _hitButton,
            _standButton
        };

        if (_round.CanSplit())
            buttons.Add(_splitButton);
        if (_round.CanDoubleDown())
            buttons.Add(_doubleButton);
        if (_round.CanSurrender())
            buttons.Add(_surrenderButton);

        return buttons;
    }

    private float GetDealerCardsY()
    {
        return GameLayoutCalculator.CalculateDealerCardsY(_graphicsDevice.Viewport.Height);
    }

    private float GetPlayerCardsY()
    {
        return GameLayoutCalculator.CalculatePlayerCardsY(_graphicsDevice.Viewport.Height);
    }

    private int GetPlayerHandCount()
    {
        if (_player.Hands.Count > 0)
            return _player.Hands.Count;

        return _playerHandCardCounts.Count;
    }

    private int GetPlayerCardCount(int handIndex)
    {
        int trackedCount = _playerHandCardCounts.GetValueOrDefault(handIndex, 0);
        int domainCount = handIndex >= 0 && handIndex < _player.Hands.Count
            ? _player.Hands[handIndex].Cards.Count
            : 0;

        return Math.Max(1, Math.Max(trackedCount, domainCount));
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
            return GameLayoutCalculator.ComputeRowCardCenter(
                vp.Width,
                _cardSize,
                dealerTotal,
                cardIndexInHand,
                GetDealerCardsY() + halfCard.Y);
        }

        // Player: hands spread horizontally from center
        int totalHands = GetPlayerHandCount();
        float centerY = GetPlayerCardsY() + halfCard.Y;

        if (totalHands <= 1)
        {
            int cardCount = GetPlayerCardCount(0);
            return GameLayoutCalculator.ComputeRowCardCenter(
                vp.Width,
                _cardSize,
                cardCount,
                cardIndexInHand,
                centerY);
        }

        var handCardCounts = new int[totalHands];
        for (int i = 0; i < totalHands; i++)
            handCardCounts[i] = GetPlayerCardCount(i);

        return GameLayoutCalculator.ComputeMultiHandCardCenter(
            vp.Width,
            _cardSize,
            handCardCounts,
            handIndex,
            cardIndexInHand,
            centerY);
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

        // Adding a card changes centered row layout; reflow existing cards now
        // (without waiting for resize) to avoid temporary overlap artifacts.
        RepositionRecipientCards(recipient, duration: 0.25f, delay: delay, excludeSprite: sprite);

        _cardLayer.Add(sprite);
        _dealCardIndex++;
    }

    private TextSprite CreateLabel(string text, Color color, Vector2 position)
    {
        var targetScale = GetResponsiveScale(1f);
        var label = new TextSprite
        {
            Text = text,
            Font = _font,
            TextColor = color,
            Position = position,
            Opacity = 0f,
            Scale = targetScale * 0.5f,
            ZOrder = 100
        };

        _uiLayer.Add(label);
        _tweenManager.Add(TweenBuilder.FadeTo(label, 1f, 0.3f));
        _tweenManager.Add(TweenBuilder.ScaleTo(label, targetScale, 0.3f, 0f, Easing.EaseOutBack));

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
        int cardCount = GetPlayerCardCount(handIndex);
        var firstCardCenter = GetCardTargetPosition(_player.Name, handIndex, 0);
        var lastCardCenter = GetCardTargetPosition(_player.Name, handIndex, cardCount - 1);
        float centerX = (firstCardCenter.X + lastCardCenter.X) / 2f;
        float labelY = GetPlayerCardsY() - 30f;

        return new Vector2(centerX, labelY);
    }

    private void RepositionRecipientCards(string recipient, float duration, float delay, CardSprite? excludeSprite = null)
    {
        foreach (var tracked in _trackedCards)
        {
            if (tracked.Recipient != recipient)
                continue;
            if (excludeSprite != null && ReferenceEquals(tracked.Sprite, excludeSprite))
                continue;

            var target = GetCardTargetPosition(tracked.Recipient, tracked.HandIndex, tracked.CardIndexInHand);
            _tweenManager.Add(TweenBuilder.MoveTo(tracked.Sprite, target, duration, delay, Easing.EaseOutQuad));
        }
    }

    private void RepositionPlayerCards()
    {
        RepositionRecipientCards(_player.Name, duration: 0.3f, delay: 0f);
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

        if (_rules.BetFlow == BetFlowMode.FreePlay)
        {
            _gamePhase = GamePhase.Playing;
            _round = new GameRound(_shoe, _player, _dealer, _rules, _eventBus.Publish);
            _round.PlaceBet(0);
            _round.Deal();
        }
        else
        {
            // Check for bankruptcy
            if (_player.Bank < _rules.MinimumBet)
            {
                _gamePhase = GamePhase.Bankrupt;
                return;
            }

            _gamePhase = GamePhase.Betting;
            _pendingBet = Math.Clamp(_pendingBet, _rules.MinimumBet,
                Math.Min(_rules.MaximumBet, _player.Bank));
        }
    }

    private void DealWithBet(decimal bet)
    {
        ClearRoundVisualState();
        _gamePhase = GamePhase.Playing;
        _lastBet = bet;
        _round = new GameRound(_shoe, _player, _dealer, _rules, _eventBus.Publish);
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
        _pendingBet = Math.Max(_rules.MinimumBet, _pendingBet - _rules.MinimumBet);
    }

    private void OnBetUpClicked(object? sender, EventArgs e)
    {
        if (_gamePhase != GamePhase.Betting) return;
        var max = Math.Min(_rules.MaximumBet, _player.Bank);
        _pendingBet = Math.Min(max, _pendingBet + _rules.MinimumBet);
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
        var bet = Math.Clamp(_lastBet, _rules.MinimumBet, Math.Min(_rules.MaximumBet, _player.Bank));
        DealWithBet(bet);
    }

    private void OnResetBankrollClicked(object? sender, EventArgs e)
    {
        if (_gamePhase != GamePhase.Bankrupt) return;
        _player.Bank = _rules.StartingBank;
        _pendingBet = _rules.MinimumBet;
        _gamePhase = GamePhase.Betting;
    }

    private void OnMenuClicked(object? sender, EventArgs e)
    {
        QuitToMenu();
    }

    private void OnPauseResumeClicked(object? sender, EventArgs e)
    {
        _isPaused = false;
        _isQuitConfirmationVisible = false;
    }

    private void OnPauseSettingsClicked(object? sender, EventArgs e)
    {
        if (!_isPaused)
            return;

        _isQuitConfirmationVisible = false;
        _game.ChangeState(new SettingsState(_game, _graphicsDevice, _content, _game.SettingsRepository, _game.ActiveProfileId));
    }

    private void OnPauseQuitClicked(object? sender, EventArgs e)
    {
        if (!_isPaused)
            return;

        _isQuitConfirmationVisible = true;
    }

    private void OnPauseConfirmQuitClicked(object? sender, EventArgs e)
    {
        QuitToMenu();
    }

    private void OnPauseCancelQuitClicked(object? sender, EventArgs e)
    {
        _isQuitConfirmationVisible = false;
    }

    private void QuitToMenu()
    {
        _isPaused = false;
        _isQuitConfirmationVisible = false;
        _game.ChangeState(
            new MenuState(_game, _graphicsDevice, _content),
            pushHistory: false,
            clearHistory: true);
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

    private void UpdatePauseMenu(GameTime gameTime)
    {
        if (_isQuitConfirmationVisible)
        {
            _pauseConfirmQuitButton.Update(gameTime);
            _pauseCancelQuitButton.Update(gameTime);
            return;
        }

        _pauseResumeButton.Update(gameTime);
        _pauseSettingsButton.Update(gameTime);
        _pauseQuitButton.Update(gameTime);
    }

    public override void Update(GameTime gameTime)
    {
        CaptureKeyboardState();

        if (WasKeyJustPressed(Keys.F3))
            _showAlignmentGuides = !_showAlignmentGuides;

        if (WasKeyJustPressed(Keys.Escape))
        {
            if (_isQuitConfirmationVisible)
            {
                _isQuitConfirmationVisible = false;
            }
            else
            {
                _isPaused = !_isPaused;
            }
        }

        if (_isPaused)
        {
            UpdatePauseMenu(gameTime);
            CommitKeyboardState();
            return;
        }

        if (_gamePhase == GamePhase.Betting)
        {
            _betDownButton.Update(gameTime);
            _betUpButton.Update(gameTime);
            _dealButton.Update(gameTime);
            if (_lastBet > 0)
                _repeatBetButton.Update(gameTime);
            CommitKeyboardState();
            return;
        }

        if (_gamePhase == GamePhase.Bankrupt)
        {
            _resetBankrollButton.Update(gameTime);
            _menuButton.Update(gameTime);
            CommitKeyboardState();
            return;
        }

        _eventBus.Flush();

        if (IsPlayerInteractionEnabled())
        {
            LayoutActionButtons();
            foreach (var button in GetVisibleActionButtons())
                button.Update(gameTime);
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
        CommitKeyboardState();
    }

    public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        var vp = _graphicsDevice.Viewport;
        bool isBettingMode = _rules.BetFlow == BetFlowMode.Betting;

        if (_gamePhase == GamePhase.Betting)
        {
            spriteBatch.Begin();
            DrawHud(spriteBatch, vp);

            // Bet amount display centered between arrows
            var betText = $"${_pendingBet}";
            var betTextScale = GetResponsiveScale(1f);
            var betTextSize = _font.MeasureString(betText) * betTextScale;
            var betTextPos = new Vector2(vp.Width / 2f - betTextSize.X / 2f, vp.Height * 0.5f - betTextSize.Y / 2f);
            spriteBatch.DrawString(_font, betText, betTextPos, Color.Gold, 0f, Vector2.Zero, betTextScale, SpriteEffects.None, 0f);

            var betLabel = "Place Your Bet";
            var betLabelScale = GetResponsiveScale(0.8f);
            var labelSize = _font.MeasureString(betLabel) * betLabelScale;
            var labelYOffset = Math.Max(vp.Height * 0.028f, 12f);
            var labelPos = new Vector2(vp.Width / 2f - labelSize.X / 2f, vp.Height * 0.5f - betTextSize.Y - labelYOffset);
            spriteBatch.DrawString(_font, betLabel, labelPos, Color.White, 0f, Vector2.Zero, betLabelScale, SpriteEffects.None, 0f);

            _betDownButton.Draw(gameTime, spriteBatch);
            _betUpButton.Draw(gameTime, spriteBatch);
            _dealButton.Draw(gameTime, spriteBatch);
            if (_lastBet > 0)
                _repeatBetButton.Draw(gameTime, spriteBatch);

            if (_isPaused)
                DrawPauseOverlay(gameTime, spriteBatch);

            spriteBatch.End();
            return;
        }

        if (_gamePhase == GamePhase.Bankrupt)
        {
            spriteBatch.Begin();

            var outText = "Out of Funds";
            var outScale = GetResponsiveScale(1f);
            var outSize = _font.MeasureString(outText) * outScale;
            var outPos = new Vector2(vp.Width / 2f - outSize.X / 2f, vp.Height * 0.4f);
            spriteBatch.DrawString(_font, outText, outPos, Color.Red, 0f, Vector2.Zero, outScale, SpriteEffects.None, 0f);

            _resetBankrollButton.Draw(gameTime, spriteBatch);
            _menuButton.Draw(gameTime, spriteBatch);

            if (_isPaused)
                DrawPauseOverlay(gameTime, spriteBatch);

            spriteBatch.End();
            return;
        }

        // Playing phase
        _sceneRenderer.Draw(spriteBatch);

        if (_showAlignmentGuides)
        {
            spriteBatch.Begin();
            DrawAlignmentGuides(spriteBatch);
            spriteBatch.End();
        }

        if (!_isPaused && IsPlayerInteractionEnabled())
        {
            LayoutActionButtons();
            spriteBatch.Begin();
            if (isBettingMode) DrawHud(spriteBatch, vp);
            DrawHandValues(spriteBatch);
            foreach (var button in GetVisibleActionButtons())
                button.Draw(gameTime, spriteBatch);

            // Draw active hand indicator when multiple hands
            if (GetPlayerHandCount() > 1)
                DrawActiveHandIndicator(spriteBatch);

            spriteBatch.End();
        }
        else if (!_isPaused && IsInsuranceInteractionEnabled())
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

        if (_isPaused)
        {
            spriteBatch.Begin();
            DrawPauseOverlay(gameTime, spriteBatch);
            spriteBatch.End();
        }
    }

    private void DrawPauseOverlay(GameTime gameTime, SpriteBatch spriteBatch)
    {
        var vp = _graphicsDevice.Viewport;
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, vp.Width, vp.Height), new Color(0, 0, 0, 180));

        string title = _isQuitConfirmationVisible ? "Quit to Menu?" : "Paused";
        float titleScale = GetResponsiveScale(1.1f);
        var titleSize = _font.MeasureString(title) * titleScale;
        var titlePos = new Vector2(vp.Width / 2f - titleSize.X / 2f, vp.Height * 0.28f);
        spriteBatch.DrawString(_font, title, titlePos, Color.White, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

        if (_isQuitConfirmationVisible)
        {
            const string warning = "Current round progress will be lost.";
            float warningScale = GetResponsiveScale(0.6f);
            var warningSize = _font.MeasureString(warning) * warningScale;
            var warningPos = new Vector2(vp.Width / 2f - warningSize.X / 2f, titlePos.Y + titleSize.Y + 14f);
            spriteBatch.DrawString(_font, warning, warningPos, Color.LightGray, 0f, Vector2.Zero, warningScale, SpriteEffects.None, 0f);

            _pauseConfirmQuitButton.Draw(gameTime, spriteBatch);
            _pauseCancelQuitButton.Draw(gameTime, spriteBatch);
            return;
        }

        _pauseResumeButton.Draw(gameTime, spriteBatch);
        _pauseSettingsButton.Draw(gameTime, spriteBatch);
        _pauseQuitButton.Draw(gameTime, spriteBatch);
    }

    private void DrawAlignmentGuides(SpriteBatch spriteBatch)
    {
        var vp = _graphicsDevice.Viewport;
        var centerX = vp.Width / 2f;
        var dealerTop = GetDealerCardsY();
        var dealerBottom = dealerTop + _cardSize.Y;
        var playerTop = GetPlayerCardsY();
        var playerBottom = playerTop + _cardSize.Y;

        var axisColor = new Color(120, 255, 255, 170);
        var boundsColor = new Color(255, 235, 120, 120);
        var handCenterColor = new Color(255, 120, 220, 200);

        // View center axis + row boundaries.
        spriteBatch.Draw(_pixelTexture, new Rectangle((int)centerX, 0, 1, vp.Height), axisColor);
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, (int)dealerTop, vp.Width, 1), boundsColor);
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, (int)dealerBottom, vp.Width, 1), boundsColor);
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, (int)playerTop, vp.Width, 1), boundsColor);
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, (int)playerBottom, vp.Width, 1), boundsColor);

        // Dealer hand center marker.
        int dealerCount = Math.Max(_dealerCardCount, 2);
        var dealerFirst = GetCardTargetPosition(_dealer.Name, 0, 0);
        var dealerLast = GetCardTargetPosition(_dealer.Name, 0, dealerCount - 1);
        var dealerCenter = (dealerFirst.X + dealerLast.X) / 2f;
        spriteBatch.Draw(_pixelTexture, new Rectangle((int)dealerCenter, (int)dealerTop - 8, 1, 16), handCenterColor);

        // Player hand center markers.
        int handCount = GetPlayerHandCount();
        for (int h = 0; h < handCount; h++)
        {
            int cardCount = GetPlayerCardCount(h);
            var first = GetCardTargetPosition(_player.Name, h, 0);
            var last = GetCardTargetPosition(_player.Name, h, cardCount - 1);
            var handCenter = (first.X + last.X) / 2f;
            spriteBatch.Draw(_pixelTexture, new Rectangle((int)handCenter, (int)playerTop - 8, 1, 16), handCenterColor);
        }

        var debugTextScale = GetResponsiveScale(0.55f);
        var debugText = "Alignment Guides (F3)";
        spriteBatch.DrawString(
            _font,
            debugText,
            new Vector2(8f, vp.Height - _font.MeasureString(debugText).Y * debugTextScale - 8f),
            new Color(220, 255, 220),
            0f,
            Vector2.Zero,
            debugTextScale,
            SpriteEffects.None,
            0f);
    }

    private void DrawHud(SpriteBatch spriteBatch, Viewport vp)
    {
        var hudScale = GetResponsiveScale(0.7f);
        var hudPaddingX = Math.Max(vp.Width * 0.01f, 8f);
        var hudPaddingY = Math.Max(vp.Height * 0.011f, 6f);

        var bankText = $"Bank: ${_player.Bank}";
        spriteBatch.DrawString(
            _font,
            bankText,
            new Vector2(hudPaddingX, hudPaddingY),
            Color.White,
            0f,
            Vector2.Zero,
            hudScale,
            SpriteEffects.None,
            0f);

        if (_gamePhase == GamePhase.Playing && _round != null!)
        {
            var betText = $"Bet: ${_lastBet}";
            var betSize = _font.MeasureString(betText) * hudScale;
            spriteBatch.DrawString(
                _font,
                betText,
                new Vector2(vp.Width - betSize.X - hudPaddingX, hudPaddingY),
                Color.Gold,
                0f,
                Vector2.Zero,
                hudScale,
                SpriteEffects.None,
                0f);
        }
    }

    private void DrawHandValues(SpriteBatch spriteBatch)
    {
        if (_round == null! || _gamePhase != GamePhase.Playing)
            return;

        var vp = _graphicsDevice.Viewport;
        var scale = GetResponsiveScale(0.9f);
        var labelPadding = Math.Max(vp.Height * 0.01f, 6f);

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
            var dealerY = GetDealerCardsY() - dealerTextSize.Y - labelPadding;
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
            int cardCount = GetPlayerCardCount(h);
            Vector2 firstCardPos = GetCardTargetPosition(_player.Name, h, 0);
            Vector2 lastCardPos = GetCardTargetPosition(_player.Name, h, cardCount - 1);
            float handCenterX = (firstCardPos.X + lastCardPos.X) / 2f;
            float handBottom = GetPlayerCardsY() + _cardSize.Y;

            var textX = handCenterX - textSize.X / 2f;
            var textY = handBottom + labelPadding;

            spriteBatch.DrawString(_font, valueText, new Vector2(textX, textY), valueColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    private void DrawActiveHandIndicator(SpriteBatch spriteBatch)
    {
        int cardCount = GetPlayerCardCount(_activePlayerHandIndex);

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
