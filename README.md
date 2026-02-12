# MonoBlackjack

A blackjack simulator and data application built with MonoGame and ImGui. Fully configurable rules, AI players, card manipulation, and detailed statistical tracking backed by SQLite.

## Features

### Current
- MonoGame rendering with card textures and responsive layout
- 6-deck shoe with proper shuffle and auto-reshuffle
- Player and dealer hand management with soft ace calculation
- State machine (menu, gameplay)
- Resizable window

### Planned
- **Simulator Controls** — ImGui-based panels for real-time configuration
- **Configurable Rules** — Deck count, dealer hit/stand rules, blackjack payouts, surrender, insurance, etc.
- **Card Editing** — Manually set player/dealer hands for scenario testing
- **AI Players** — Multiple configurable AI strategies (basic strategy, card counting, custom)
- **Statistics Tracking** — Detailed per-session and historical stats stored in SQLite
- **Data Views** — Win rates, bust rates, hand distributions, strategy effectiveness, bankroll tracking

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Game Framework | MonoGame 3.8.4.1 (DesktopGL) |
| Runtime | .NET 10 |
| GUI | ImGui.NET |
| Database | SQLite |
| Platform | Cross-platform (Linux, Windows, macOS) |

## Build & Run

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Build
```bash
cd MonoBlackjack/MonoBlackjack
dotnet tool restore
dotnet build
dotnet run
```

### Content Pipeline
Game assets (card textures, fonts) are compiled through MonoGame's MGCB content pipeline. The `dotnet-mgcb` CLI tool is registered as a local tool and runs automatically during build. To manage assets manually:

```bash
# Restore the MGCB tool
dotnet tool restore

# Run content builder directly (usually not needed — build does this)
dotnet mgcb /quiet /@:Content/Content.mgcb /platform:DesktopGL
```

To add new assets, edit `Content/Content.mgcb` directly or install the MGCB Editor:
```bash
# Optional: install the MGCB Editor GUI
dotnet tool install dotnet-mgcb-editor
dotnet mgcb-editor Content/Content.mgcb
```

## License
TBD
