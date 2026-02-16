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

    public void ChangeState(State state) => _nextState = state;
    public IStatsRepository StatsRepository => _statsRepository;
    public ISettingsRepository SettingsRepository => _settingsRepository;
    public Texture2D PixelTexture => _pixelTexture;
    public int ActiveProfileId { get; private set; }

    public BlackjackGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
    }

    protected override void Initialize()
    {
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += (_, _) => _currentState.HandleResize(Window.ClientBounds);

        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        _graphics.ApplyChanges();

        IsMouseVisible = true;
        base.Initialize();
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
            GameConfig.ApplySettings(persistedSettings);

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
