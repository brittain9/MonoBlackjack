using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoBlackjack.Animation;
using MonoBlackjack.Core;
using MonoBlackjack.Core.Events;
using MonoBlackjack.Core.Ports;
using MonoBlackjack.Core.Players;
using MonoBlackjack.Infrastructure.DevTools;
using MonoBlackjack.Infrastructure.Events;
using MonoBlackjack.Layout;
using MonoBlackjack.Rendering;
using MonoBlackjack.Services;

namespace MonoBlackjack;

internal class GameState : State
{
    private readonly Texture2D _pixelTexture;
    private readonly int _profileId;
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

    private readonly GameInputController _inputController;
    private readonly GamePauseController _pauseController;
    private readonly GameAnimationCoordinator _animationCoordinator;
    private readonly GameHudPresenter _hudPresenter;
    private readonly GameTableRenderer _tableRenderer;

    private readonly Button _hitButton;
    private readonly Button _standButton;
    private readonly Button _splitButton;
    private readonly Button _doubleButton;
    private readonly Button _surrenderButton;
    private readonly Button _insuranceButton;
    private readonly Button _declineInsuranceButton;

    private readonly Button _betDownButton;
    private readonly Button _betUpButton;
    private readonly Button _dealButton;
    private readonly Button _repeatBetButton;

    private readonly Button _resetBankrollButton;
    private readonly Button _menuButton;
    private readonly StatsRecorder _statsRecorder;
    private readonly List<IDisposable> _subscriptions = [];
    private readonly DevMenuOverlay _devMenu;

    private bool _showAlignmentGuides;
    private bool _showHandValues = true;
    private KeybindMap _keybinds = null!;
    private string _warningMessage = string.Empty;
    private float _warningSecondsRemaining;

    private GamePhase _gamePhase;
    private decimal _pendingBet;
    private decimal _lastBet;

    private GameRound _round;
    private float _roundCompleteTimer;
    private bool _waitingForRoundReset;

    private Vector2 _actionButtonSize;
    private float _actionButtonY;
    private float _actionButtonPadding;

    public GameState(
        BlackjackGame game,
        GraphicsDevice graphicsDevice,
        ContentManager content,
        IStatsRepository statsRepository,
        int profileId,
        BetFlowMode mode)
        : base(game, graphicsDevice, content)
    {
        _pixelTexture = game.PixelTexture;
        _profileId = profileId;
        _rules = ApplySelectedMode(game.CurrentRules, mode);

        _cardRenderer = new CardRenderer();
        _cardRenderer.LoadTextures(content, _game.RuntimeGraphicsSettings.CardBackTheme);

        _shoe = new Shoe(_rules.NumberOfDecks, _rules.PenetrationPercent, _rules.UseCryptographicShuffle);
        _player = new Human("Player", _rules.StartingBank);
        _dealer = new Dealer(_rules.DealerHitsSoft17);

        _eventBus = new EventBus();
        _tweenManager = new TweenManager();
        _sceneRenderer = new SceneRenderer();
        _statsRecorder = new StatsRecorder(_eventBus, statsRepository, profileId, _rules, OnStatsPersistenceFailure);

        _cardLayer = new SpriteLayer(10);
        _uiLayer = new SpriteLayer(20);
        _sceneRenderer.AddLayer(_cardLayer);
        _sceneRenderer.AddLayer(_uiLayer);

        var buttonTexture = _content.Load<Texture2D>("Controls/Button");
        _font = _content.Load<SpriteFont>("Fonts/MyFont");
        var tableTexture = _content.Load<Texture2D>("Art/BlackjackTable");

        _inputController = new GameInputController();
        _pauseController = new GamePauseController(buttonTexture, _font);
        _pauseController.RequestSettings += OnPauseSettingsRequested;
        _pauseController.RequestQuitToMenu += QuitToMenu;

        _animationCoordinator = new GameAnimationCoordinator(
            _graphicsDevice,
            _font,
            _cardRenderer,
            _tweenManager,
            _cardLayer,
            _uiLayer,
            _player,
            _dealer,
            GetResponsiveScale);

        _hudPresenter = new GameHudPresenter(_graphicsDevice, _font, _pixelTexture, GetResponsiveScale);
        _tableRenderer = new GameTableRenderer(_graphicsDevice, tableTexture, _font, _rules, GetResponsiveScale);

        _hitButton = new Button(buttonTexture, _font) { Text = "Hit", PenColor = Color.Black };
        _standButton = new Button(buttonTexture, _font) { Text = "Stand", PenColor = Color.Black };
        _splitButton = new Button(buttonTexture, _font) { Text = "Split", PenColor = Color.Black };
        _doubleButton = new Button(buttonTexture, _font) { Text = "Double", PenColor = Color.Black };
        _surrenderButton = new Button(buttonTexture, _font) { Text = "Surrender", PenColor = Color.Black };
        _insuranceButton = new Button(buttonTexture, _font) { Text = "Insurance", PenColor = Color.Black };
        _declineInsuranceButton = new Button(buttonTexture, _font) { Text = "No Thanks", PenColor = Color.Black };

        _betDownButton = new Button(buttonTexture, _font) { Text = "<", PenColor = Color.Black };
        _betUpButton = new Button(buttonTexture, _font) { Text = ">", PenColor = Color.Black };
        _dealButton = new Button(buttonTexture, _font) { Text = "Deal", PenColor = Color.Black };
        _repeatBetButton = new Button(buttonTexture, _font) { Text = "Repeat Bet", PenColor = Color.Black };

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

        _devMenu = new DevMenuOverlay(
            buttonTexture,
            [
                new CardSelectorTool(_shoe, buttonTexture, _font),
                new SplitSetupTool(_shoe, buttonTexture, _font)
            ]);

        ReloadPlayerSettings();
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
        _animationCoordinator.RecalculateLayout();

        var centerX = vp.Width / 2f;
        var cardSize = _animationCoordinator.CardSize;

        _actionButtonPadding = GameLayoutCalculator.CalculateActionButtonPadding(vp.Width);
        _actionButtonSize = GameLayoutCalculator.CalculateActionButtonSize(vp.Width, cardSize, _actionButtonPadding);

        var handValueScale = GetResponsiveScale(0.9f);
        var handValueHeight = _font.MeasureString("18").Y * handValueScale;
        _actionButtonY = GameLayoutCalculator.CalculateActionButtonY(
            vp.Height,
            _animationCoordinator.GetPlayerCardsY(),
            cardSize,
            _actionButtonSize,
            handValueHeight);

        _hitButton.Size = _actionButtonSize;
        _standButton.Size = _actionButtonSize;
        _splitButton.Size = _actionButtonSize;
        _doubleButton.Size = _actionButtonSize;
        _surrenderButton.Size = _actionButtonSize;
        _insuranceButton.Size = _actionButtonSize;
        _declineInsuranceButton.Size = _actionButtonSize;

        LayoutActionButtons();

        var insuranceTotalWidth = (_actionButtonSize.X * 2) + _actionButtonPadding;
        var insuranceStartX = centerX - (insuranceTotalWidth / 2f) + (_actionButtonSize.X / 2f);
        _insuranceButton.Position = new Vector2(insuranceStartX, _actionButtonY);
        _declineInsuranceButton.Position = new Vector2(insuranceStartX + _actionButtonSize.X + _actionButtonPadding, _actionButtonY);

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

        _resetBankrollButton.Size = _actionButtonSize;
        _menuButton.Size = _actionButtonSize;
        var bankruptY = vp.Height * UIConstants.BankruptButtonsYRatio;
        var bankruptTotalWidth = (_actionButtonSize.X * 2) + _actionButtonPadding;
        var bankruptStartX = centerX - (bankruptTotalWidth / 2f) + (_actionButtonSize.X / 2f);
        _resetBankrollButton.Position = new Vector2(bankruptStartX, bankruptY);
        _menuButton.Position = new Vector2(bankruptStartX + _actionButtonSize.X + _actionButtonPadding, bankruptY);

        _pauseController.HandleResize(vp, _actionButtonPadding);
        _devMenu.HandleResize(vp.Bounds);
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
        var buttonKeys = ResolveVisibleActionButtonKeys(
            canSplit: _round.CanSplit(),
            canDoubleDown: _round.CanDoubleDown(),
            canSurrender: _round.CanSurrender());

        var buttons = new List<Button>(5)
        {
            _hitButton,
            _standButton
        };

        if (buttonKeys.Contains("Split", StringComparer.Ordinal))
            buttons.Add(_splitButton);
        if (buttonKeys.Contains("Double", StringComparer.Ordinal))
            buttons.Add(_doubleButton);
        if (buttonKeys.Contains("Surrender", StringComparer.Ordinal))
            buttons.Add(_surrenderButton);

        return buttons;
    }

    private bool IsPlayerInteractionEnabled()
    {
        return !_tweenManager.HasActiveTweens && _round.Phase == RoundPhase.PlayerTurn;
    }

    private bool IsInsuranceInteractionEnabled()
    {
        return !_tweenManager.HasActiveTweens && _round.Phase == RoundPhase.Insurance;
    }

    private void StartNewRound()
    {
        _animationCoordinator.ClearRoundVisualState();
        _roundCompleteTimer = 0f;
        _waitingForRoundReset = false;

        if (_rules.BetFlow == BetFlowMode.FreePlay)
        {
            _gamePhase = GamePhase.Playing;
            _round = new GameRound(_shoe, _player, _dealer, _rules, _eventBus.Publish);
            _round.PlaceBet(0);
            _round.Deal();
        }
        else
        {
            if (_player.Bank < _rules.MinimumBet)
            {
                _gamePhase = GamePhase.Bankrupt;
                return;
            }

            _gamePhase = GamePhase.Betting;
            _pendingBet = Math.Clamp(_pendingBet, _rules.MinimumBet, Math.Min(_rules.MaximumBet, _player.Bank));
        }
    }

    private void DealWithBet(decimal bet)
    {
        _animationCoordinator.ClearRoundVisualState();
        _roundCompleteTimer = 0f;
        _waitingForRoundReset = false;
        _gamePhase = GamePhase.Playing;
        _lastBet = bet;
        _round = new GameRound(_shoe, _player, _dealer, _rules, _eventBus.Publish);
        _round.PlaceBet(bet);
        _round.Deal();
    }

    private void OnCardDealt(CardDealt evt) => _animationCoordinator.OnCardDealt(evt);
    private void OnInitialDealComplete(InitialDealComplete evt) { }
    private void OnPlayerTurnStarted(PlayerTurnStarted evt) => _animationCoordinator.OnPlayerTurnStarted(evt);
    private void OnPlayerHit(PlayerHit evt) => _animationCoordinator.OnPlayerHit(evt);
    private void OnPlayerDoubledDown(PlayerDoubledDown evt) => _animationCoordinator.OnPlayerDoubledDown(evt);
    private void OnPlayerBusted(PlayerBusted evt) => _animationCoordinator.OnPlayerBusted(evt);
    private void OnPlayerStood(PlayerStood evt) { }
    private void OnPlayerSplit(PlayerSplit evt) => _animationCoordinator.OnPlayerSplit(evt);
    private void OnDealerTurnStarted(DealerTurnStarted evt) => _animationCoordinator.OnDealerTurnStarted(evt);
    private void OnDealerHit(DealerHit evt) => _animationCoordinator.OnDealerHit(evt);
    private void OnDealerHoleCardRevealed(DealerHoleCardRevealed evt) => _animationCoordinator.OnDealerHoleCardRevealed(evt);
    private void OnInsuranceOffered(InsuranceOffered evt) => _animationCoordinator.OnInsuranceOffered(evt);
    private void OnInsuranceResult(InsuranceResult evt) => _animationCoordinator.OnInsuranceResult(evt);
    private void OnDealerBusted(DealerBusted evt) => _animationCoordinator.OnDealerBusted(evt);
    private void OnDealerStood(DealerStood evt) { }
    private void OnHandResolved(HandResolved evt) => _animationCoordinator.OnHandResolved(evt);

    private void OnRoundComplete(RoundComplete evt)
    {
        _waitingForRoundReset = true;
        _roundCompleteTimer = 1.5f;
    }

    private void OnBetDownClicked(object? sender, EventArgs e)
    {
        if (_gamePhase != GamePhase.Betting)
            return;

        _pendingBet = Math.Max(_rules.MinimumBet, _pendingBet - _rules.MinimumBet);
    }

    private void OnBetUpClicked(object? sender, EventArgs e)
    {
        if (_gamePhase != GamePhase.Betting)
            return;

        var max = Math.Min(_rules.MaximumBet, _player.Bank);
        _pendingBet = Math.Min(max, _pendingBet + _rules.MinimumBet);
    }

    private void OnDealClicked(object? sender, EventArgs e)
    {
        if (_gamePhase != GamePhase.Betting)
            return;

        DealWithBet(_pendingBet);
    }

    private void OnRepeatBetClicked(object? sender, EventArgs e)
    {
        if (_gamePhase != GamePhase.Betting)
            return;
        if (_lastBet <= 0)
            return;

        var bet = Math.Clamp(_lastBet, _rules.MinimumBet, Math.Min(_rules.MaximumBet, _player.Bank));
        DealWithBet(bet);
    }

    private void OnResetBankrollClicked(object? sender, EventArgs e)
    {
        if (_gamePhase != GamePhase.Bankrupt)
            return;

        _player.Bank = _rules.StartingBank;
        _pendingBet = _rules.MinimumBet;
        _gamePhase = GamePhase.Betting;
    }

    private void OnMenuClicked(object? sender, EventArgs e)
    {
        QuitToMenu();
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

    private void ExecutePhaseCommand(GamePhaseActionCommand command)
    {
        switch (command)
        {
            case GamePhaseActionCommand.BetDown:
                OnBetDownClicked(this, EventArgs.Empty);
                break;
            case GamePhaseActionCommand.BetUp:
                OnBetUpClicked(this, EventArgs.Empty);
                break;
            case GamePhaseActionCommand.Deal:
                OnDealClicked(this, EventArgs.Empty);
                break;
            case GamePhaseActionCommand.RepeatBet:
                OnRepeatBetClicked(this, EventArgs.Empty);
                break;
            case GamePhaseActionCommand.ResetBankroll:
                OnResetBankrollClicked(this, EventArgs.Empty);
                break;
            case GamePhaseActionCommand.Menu:
                OnMenuClicked(this, EventArgs.Empty);
                break;
            case GamePhaseActionCommand.Hit:
                OnHitClicked(this, EventArgs.Empty);
                break;
            case GamePhaseActionCommand.Stand:
                OnStandClicked(this, EventArgs.Empty);
                break;
            case GamePhaseActionCommand.Split:
                OnSplitClicked(this, EventArgs.Empty);
                break;
            case GamePhaseActionCommand.Double:
                OnDoubleClicked(this, EventArgs.Empty);
                break;
            case GamePhaseActionCommand.Surrender:
                OnSurrenderClicked(this, EventArgs.Empty);
                break;
            case GamePhaseActionCommand.InsuranceAccept:
                OnInsuranceClicked(this, EventArgs.Empty);
                break;
            case GamePhaseActionCommand.InsuranceDecline:
                OnDeclineInsuranceClicked(this, EventArgs.Empty);
                break;
        }
    }

    public override void Update(GameTime gameTime)
    {
        CaptureKeyboardState();
        var mouseSnapshot = CaptureMouseSnapshot();
        float deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        TickWarning(deltaSeconds);

        bool pausePressed = _inputController.IsPausePressed(_keybinds, _currentKeyboardState, _previousKeyboardState);
        bool backPressed = _inputController.IsBackPressed(_keybinds, _currentKeyboardState, _previousKeyboardState);

        if (_inputController.IsAlignmentGuideTogglePressed(_currentKeyboardState, _previousKeyboardState))
            _showAlignmentGuides = !_showAlignmentGuides;

        if (_inputController.IsDevMenuTogglePressed(_currentKeyboardState, _previousKeyboardState))
            _devMenu.Toggle();

        if (_devMenu.IsOpen)
        {
            if (pausePressed || backPressed)
                _devMenu.Close();
            else
                _devMenu.Update(gameTime, _currentKeyboardState, _previousKeyboardState, mouseSnapshot);

            CommitMouseState();
            CommitKeyboardState();
            return;
        }

        _pauseController.HandlePauseBackInput(pausePressed, backPressed);
        if (_pauseController.IsPaused)
        {
            _pauseController.Update(gameTime, mouseSnapshot);
            CommitMouseState();
            CommitKeyboardState();
            return;
        }

        if (_gamePhase == GamePhase.Betting)
        {
            ExecutePhaseCommand(_inputController.ResolveBettingCommand(_keybinds, _currentKeyboardState, _previousKeyboardState));
            _betDownButton.Update(gameTime, mouseSnapshot);
            _betUpButton.Update(gameTime, mouseSnapshot);
            _dealButton.Update(gameTime, mouseSnapshot);
            if (_lastBet > 0)
                _repeatBetButton.Update(gameTime, mouseSnapshot);

            CommitMouseState();
            CommitKeyboardState();
            return;
        }

        if (_gamePhase == GamePhase.Bankrupt)
        {
            ExecutePhaseCommand(_inputController.ResolveBankruptCommand(_keybinds, _currentKeyboardState, _previousKeyboardState));
            _resetBankrollButton.Update(gameTime, mouseSnapshot);
            _menuButton.Update(gameTime, mouseSnapshot);

            CommitMouseState();
            CommitKeyboardState();
            return;
        }

        _eventBus.Flush();

        if (IsPlayerInteractionEnabled())
        {
            ExecutePhaseCommand(_inputController.ResolvePlayerTurnCommand(_keybinds, _currentKeyboardState, _previousKeyboardState));
            LayoutActionButtons();
            foreach (var button in GetVisibleActionButtons())
                button.Update(gameTime, mouseSnapshot);
        }
        else if (IsInsuranceInteractionEnabled())
        {
            ExecutePhaseCommand(_inputController.ResolveInsuranceCommand(_keybinds, _currentKeyboardState, _previousKeyboardState));
            _insuranceButton.Update(gameTime, mouseSnapshot);
            _declineInsuranceButton.Update(gameTime, mouseSnapshot);
        }

        _tweenManager.Update(deltaSeconds);

        if (_waitingForRoundReset && !_tweenManager.HasActiveTweens)
        {
            if (_inputController.ResolveRoundAdvanceCommand(_keybinds, _currentKeyboardState, _previousKeyboardState) == GamePhaseActionCommand.AdvanceRound)
            {
                StartNewRound();
            }
            else
            {
                _roundCompleteTimer -= deltaSeconds;
                if (_roundCompleteTimer <= 0f)
                    StartNewRound();
            }
        }

        _sceneRenderer.Update(gameTime);
        CommitMouseState();
        CommitKeyboardState();
    }

    public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        var vp = _graphicsDevice.Viewport;
        bool isBettingMode = _rules.BetFlow == BetFlowMode.Betting;

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullCounterClockwise);
        _tableRenderer.Draw(spriteBatch);
        spriteBatch.End();

        if (_gamePhase == GamePhase.Betting)
        {
            spriteBatch.Begin();
            _hudPresenter.DrawHud(spriteBatch, _player.Bank, _gamePhase, _lastBet);

            var betCenterY = vp.Height * UIConstants.BetCenterYRatio;
            var betText = $"${_pendingBet}";
            var betTextScale = GetResponsiveScale(1f);
            var betTextSize = _font.MeasureString(betText) * betTextScale;
            var betTextPos = new Vector2(vp.Width / 2f - betTextSize.X / 2f, betCenterY - betTextSize.Y / 2f);
            spriteBatch.DrawString(_font, betText, betTextPos, Color.Gold, 0f, Vector2.Zero, betTextScale, SpriteEffects.None, 0f);

            const string betLabel = "Place Your Bet";
            var betLabelScale = GetResponsiveScale(0.8f);
            var labelSize = _font.MeasureString(betLabel) * betLabelScale;
            var labelYOffset = Math.Max(vp.Height * 0.028f, 12f);
            var labelPos = new Vector2(vp.Width / 2f - labelSize.X / 2f, betCenterY - betTextSize.Y - labelYOffset);
            spriteBatch.DrawString(_font, betLabel, labelPos, Color.White, 0f, Vector2.Zero, betLabelScale, SpriteEffects.None, 0f);

            _betDownButton.Draw(gameTime, spriteBatch);
            _betUpButton.Draw(gameTime, spriteBatch);
            _dealButton.Draw(gameTime, spriteBatch);
            if (_lastBet > 0)
                _repeatBetButton.Draw(gameTime, spriteBatch);

            if (_pauseController.IsPaused)
                _pauseController.DrawOverlay(gameTime, spriteBatch, _pixelTexture, GetResponsiveScale, _keybinds);

            if (_devMenu.IsOpen)
                _devMenu.Draw(gameTime, spriteBatch, _pixelTexture, _font, GetResponsiveScale(1f));

            _hudPresenter.DrawWarningBanner(spriteBatch, _warningMessage, _warningSecondsRemaining);
            spriteBatch.End();
            return;
        }

        if (_gamePhase == GamePhase.Bankrupt)
        {
            spriteBatch.Begin();

            const string outText = "Out of Funds";
            var outScale = GetResponsiveScale(1f);
            var outSize = _font.MeasureString(outText) * outScale;
            var outPos = new Vector2(vp.Width / 2f - outSize.X / 2f, vp.Height * 0.4f);
            spriteBatch.DrawString(_font, outText, outPos, Color.Red, 0f, Vector2.Zero, outScale, SpriteEffects.None, 0f);

            _resetBankrollButton.Draw(gameTime, spriteBatch);
            _menuButton.Draw(gameTime, spriteBatch);

            if (_pauseController.IsPaused)
                _pauseController.DrawOverlay(gameTime, spriteBatch, _pixelTexture, GetResponsiveScale, _keybinds);

            if (_devMenu.IsOpen)
                _devMenu.Draw(gameTime, spriteBatch, _pixelTexture, _font, GetResponsiveScale(1f));

            _hudPresenter.DrawWarningBanner(spriteBatch, _warningMessage, _warningSecondsRemaining);
            spriteBatch.End();
            return;
        }

        _sceneRenderer.Draw(spriteBatch);

        if (_showAlignmentGuides)
        {
            spriteBatch.Begin();
            _hudPresenter.DrawAlignmentGuides(spriteBatch, _animationCoordinator, _dealer.Name, _player.Name);
            spriteBatch.End();
        }

        if (!_pauseController.IsPaused && IsPlayerInteractionEnabled())
        {
            LayoutActionButtons();
            spriteBatch.Begin();
            if (isBettingMode)
                _hudPresenter.DrawHud(spriteBatch, _player.Bank, _gamePhase, _lastBet);
            _hudPresenter.DrawHandValues(spriteBatch, _showHandValues, _round, _gamePhase, _dealer, _player, _animationCoordinator);
            foreach (var button in GetVisibleActionButtons())
                button.Draw(gameTime, spriteBatch);

            if (_animationCoordinator.GetPlayerHandCount() > 1)
                _hudPresenter.DrawActiveHandIndicator(spriteBatch, _animationCoordinator, _player.Name);

            _hudPresenter.DrawWarningBanner(spriteBatch, _warningMessage, _warningSecondsRemaining);
            spriteBatch.End();
        }
        else if (!_pauseController.IsPaused && IsInsuranceInteractionEnabled())
        {
            spriteBatch.Begin();
            if (isBettingMode)
                _hudPresenter.DrawHud(spriteBatch, _player.Bank, _gamePhase, _lastBet);
            _hudPresenter.DrawHandValues(spriteBatch, _showHandValues, _round, _gamePhase, _dealer, _player, _animationCoordinator);
            _insuranceButton.Draw(gameTime, spriteBatch);
            _declineInsuranceButton.Draw(gameTime, spriteBatch);
            _hudPresenter.DrawWarningBanner(spriteBatch, _warningMessage, _warningSecondsRemaining);
            spriteBatch.End();
        }
        else if (isBettingMode)
        {
            spriteBatch.Begin();
            _hudPresenter.DrawHud(spriteBatch, _player.Bank, _gamePhase, _lastBet);
            _hudPresenter.DrawHandValues(spriteBatch, _showHandValues, _round, _gamePhase, _dealer, _player, _animationCoordinator);
            _hudPresenter.DrawWarningBanner(spriteBatch, _warningMessage, _warningSecondsRemaining);
            spriteBatch.End();
        }
        else if (_warningSecondsRemaining > 0f)
        {
            spriteBatch.Begin();
            _hudPresenter.DrawWarningBanner(spriteBatch, _warningMessage, _warningSecondsRemaining);
            spriteBatch.End();
        }

        if (_pauseController.IsPaused)
        {
            spriteBatch.Begin();
            _pauseController.DrawOverlay(gameTime, spriteBatch, _pixelTexture, GetResponsiveScale, _keybinds);
            if (_devMenu.IsOpen)
                _devMenu.Draw(gameTime, spriteBatch, _pixelTexture, _font, GetResponsiveScale(1f));
            _hudPresenter.DrawWarningBanner(spriteBatch, _warningMessage, _warningSecondsRemaining);
            spriteBatch.End();
            return;
        }

        if (_devMenu.IsOpen)
        {
            spriteBatch.Begin();
            _devMenu.Draw(gameTime, spriteBatch, _pixelTexture, _font, GetResponsiveScale(1f));
            _hudPresenter.DrawWarningBanner(spriteBatch, _warningMessage, _warningSecondsRemaining);
            spriteBatch.End();
        }
    }

    private void TickWarning(float deltaSeconds)
    {
        if (_warningSecondsRemaining <= 0f)
            return;

        _warningSecondsRemaining = Math.Max(0f, _warningSecondsRemaining - deltaSeconds);
        if (_warningSecondsRemaining <= 0f)
            _warningMessage = string.Empty;
    }

    private void OnStatsPersistenceFailure(string message)
    {
        _warningMessage = message;
        _warningSecondsRemaining = 4.0f;
    }

    private void OnPauseSettingsRequested()
    {
        _game.ChangeState(new SettingsState(_game, _graphicsDevice, _content, _game.SettingsRepository, _game.ActiveProfileId));
    }

    private void QuitToMenu()
    {
        _game.ChangeState(
            new MenuState(_game, _graphicsDevice, _content),
            pushHistory: false,
            clearHistory: true);
    }

    public override void HandleResize(Rectangle vp)
    {
        ReloadPlayerSettings();
        CalculatePositions();
        _sceneRenderer.HandleResize(vp);

        _tweenManager.Clear();
        _animationCoordinator.SnapTrackedSpritesToTargets();
    }

    public override void PostUpdate(GameTime gameTime) { }

    public override void Dispose()
    {
        _pauseController.RequestSettings -= OnPauseSettingsRequested;
        _pauseController.RequestQuitToMenu -= QuitToMenu;

        foreach (var subscription in _subscriptions)
            subscription.Dispose();
        _subscriptions.Clear();

        _statsRecorder.Dispose();

        _eventBus.Clear();
        _tweenManager.Clear();
        _cardLayer.Clear();
        _uiLayer.Clear();
    }

    private void ReloadPlayerSettings()
    {
        var settings = _game.SettingsRepository.LoadSettings(_profileId);
        _showHandValues = ResolveShowHandValues(settings);
        _keybinds = KeybindMap.FromSettings(settings);
        _cardRenderer.SetCardBackTheme(_game.RuntimeGraphicsSettings.CardBackTheme);
        _animationCoordinator.ApplyCardBackTint(_cardRenderer.BackTint);
    }

    internal static GameRules ApplySelectedMode(GameRules rules, BetFlowMode mode)
    {
        return rules with { BetFlow = mode };
    }

    internal static bool ResolveShowHandValues(IReadOnlyDictionary<string, string> settings)
    {
        if (!settings.TryGetValue(GameConfig.SettingShowHandValues, out var rawValue))
            return true;

        if (bool.TryParse(rawValue, out var parsed))
            return parsed;

        return true;
    }

    internal static IReadOnlyList<string> ResolveVisibleActionButtonKeys(bool canSplit, bool canDoubleDown, bool canSurrender)
    {
        var buttonKeys = new List<string>(5) { "Hit", "Stand" };

        if (canSplit)
            buttonKeys.Add("Split");
        if (canDoubleDown)
            buttonKeys.Add("Double");
        if (canSurrender)
            buttonKeys.Add("Surrender");

        return buttonKeys;
    }
}
