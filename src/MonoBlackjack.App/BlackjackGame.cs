using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Core;
using MonoBlackjack.Core.Ports;
using MonoBlackjack.Data;
using MonoBlackjack.Data.Repositories;

namespace MonoBlackjack;

public class BlackjackGame : Microsoft.Xna.Framework.Game
{
    private const int MaxStateHistory = 5;
    private readonly GraphicsDeviceManager _graphics;
    private readonly List<State> _stateHistory = [];
    private SpriteBatch _spriteBatch = null!;
    private State _currentState = null!;
    private State? _nextState;
    private bool _clearHistoryOnTransition;
    private bool _pushCurrentStateToHistoryOnTransition;
    private DatabaseManager _database = null!;
    private IProfileRepository _profileRepository = null!;
    private IStatsRepository _statsRepository = null!;
    private ISettingsRepository _settingsRepository = null!;
    private Texture2D _pixelTexture = null!;
    private bool _enforcingMinimumWindowSize;

    public void ChangeState(State state, bool pushHistory = true, bool clearHistory = false)
    {
        QueueTransition(state, pushHistory, clearHistory);
    }

    public void GoBack()
    {
        if (_stateHistory.Count > 0)
        {
            int lastIndex = _stateHistory.Count - 1;
            var previousState = _stateHistory[lastIndex];
            _stateHistory.RemoveAt(lastIndex);
            QueueTransition(previousState, pushHistory: false, clearHistory: false);
            return;
        }

        QueueTransition(new MenuState(this, _graphics.GraphicsDevice, Content), pushHistory: false, clearHistory: true);
    }

    public IStatsRepository StatsRepository => _statsRepository;
    public ISettingsRepository SettingsRepository => _settingsRepository;
    public Texture2D PixelTexture => _pixelTexture;
    public int ActiveProfileId { get; private set; }
    public GameRules CurrentRules { get; private set; } = GameRules.Standard;

    public void UpdateRules(GameRules rules) => CurrentRules = rules;

    public BlackjackGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
    }

    protected override void Initialize()
    {
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += OnClientSizeChanged;

        _graphics.PreferredBackBufferWidth = UIConstants.BaselineWidth;
        _graphics.PreferredBackBufferHeight = UIConstants.BaselineHeight;
        _graphics.ApplyChanges();
        EnforceMinimumWindowSize();

        IsMouseVisible = true;
        base.Initialize();
    }

    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        if (_enforcingMinimumWindowSize)
            return;

        EnforceMinimumWindowSize();

        if (_currentState is not null)
            _currentState.HandleResize(Window.ClientBounds);
    }

    private void EnforceMinimumWindowSize()
    {
        var width = Math.Max(Window.ClientBounds.Width, UIConstants.MinWindowWidth);
        var height = Math.Max(Window.ClientBounds.Height, UIConstants.MinWindowHeight);

        if (width == Window.ClientBounds.Width && height == Window.ClientBounds.Height)
            return;

        _enforcingMinimumWindowSize = true;
        _graphics.PreferredBackBufferWidth = width;
        _graphics.PreferredBackBufferHeight = height;
        _graphics.ApplyChanges();
        _enforcingMinimumWindowSize = false;
    }

    protected override void LoadContent()
    {
        _database = new DatabaseManager();
        _profileRepository = new SqliteProfileRepository(_database);
        _statsRepository = new SqliteStatsRepository(_database);
        _settingsRepository = new SqliteSettingsRepository(_database);

        var active = _profileRepository.GetActiveProfile()
            ?? _profileRepository.GetOrCreateProfile("Default");
        _profileRepository.SetActiveProfile(active.Id);
        ActiveProfileId = active.Id;

        var persistedSettings = _settingsRepository.LoadSettings(ActiveProfileId);
        if (persistedSettings.Count > 0)
        {
            CurrentRules = GameRules.FromSettings(persistedSettings);
        }

        _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _currentState = new MenuState(this, _graphics.GraphicsDevice, Content);
    }

    protected override void Update(GameTime gameTime)
    {
        if (_nextState != null)
        {
            if (_clearHistoryOnTransition)
                ClearStateHistory();

            if (_pushCurrentStateToHistoryOnTransition)
                PushStateHistory(_currentState);
            else
                _currentState.Dispose();

            _currentState = _nextState;
            _nextState = null;
            _pushCurrentStateToHistoryOnTransition = false;
            _clearHistoryOnTransition = false;

            _currentState.HandleResize(Window.ClientBounds);
        }

        _currentState.Update(gameTime);
        _currentState.PostUpdate(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.DarkGreen);
        _currentState.Draw(gameTime, _spriteBatch);
        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _nextState?.Dispose();
            _nextState = null;

            if (_currentState is not null)
                _currentState.Dispose();

            ClearStateHistory();
        }

        base.Dispose(disposing);
    }

    private void QueueTransition(State state, bool pushHistory, bool clearHistory)
    {
        if (_nextState is not null && !ReferenceEquals(_nextState, state))
            _nextState.Dispose();

        _nextState = state;
        _pushCurrentStateToHistoryOnTransition = pushHistory;
        _clearHistoryOnTransition = clearHistory;
    }

    private void PushStateHistory(State state)
    {
        _stateHistory.Add(state);

        if (_stateHistory.Count <= MaxStateHistory)
            return;

        _stateHistory[0].Dispose();
        _stateHistory.RemoveAt(0);
    }

    private void ClearStateHistory()
    {
        foreach (var state in _stateHistory)
            state.Dispose();
        _stateHistory.Clear();
    }
}

public class Program
{
    public static void Main()
    {
        using var game = new BlackjackGame();
        game.Run();
    }
}
