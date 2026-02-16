using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Core;
using MonoBlackjack.Core.Ports;
using MonoBlackjack.Data;
using MonoBlackjack.Data.Repositories;

namespace MonoBlackjack;

public class BlackjackGame : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private State _currentState = null!;
    private State? _nextState;
    private DatabaseManager _database = null!;
    private IProfileRepository _profileRepository = null!;
    private IStatsRepository _statsRepository = null!;
    private ISettingsRepository _settingsRepository = null!;
    private Texture2D _pixelTexture = null!;
    private bool _enforcingMinimumWindowSize;

    public void ChangeState(State state) => _nextState = state;
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
            _currentState.Dispose();
            _currentState = _nextState;
            _nextState = null;
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
}

public class Program
{
    public static void Main()
    {
        using var game = new BlackjackGame();
        game.Run();
    }
}
