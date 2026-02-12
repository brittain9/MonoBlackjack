# MonoBlackjack - Project Context

## Overview
Blackjack simulator and data application built with MonoGame. Not just a game — a fully configurable blackjack environment with AI players, rule editing, card manipulation, and detailed statistical tracking backed by SQLite.

## Tech Stack
- **Framework:** MonoGame 3.8.4.1 (DesktopGL)
- **Runtime:** .NET 10
- **GUI:** ImGui.NET (for simulator controls, stats panels, configuration)
- **Database:** SQLite (via Microsoft.Data.Sqlite or similar) for stats persistence
- **Platform:** Cross-platform (Linux, Windows, macOS)

## Project Structure
```
MonoBlackjack/
├── MonoBlackjack/          # Main project
│   ├── BlackjackGame.cs    # Entry point, game loop, state machine
│   ├── Controls/           # UI components (buttons, etc.)
│   ├── Game/               # Core game logic
│   │   ├── Card/           # Card, deck, shoe management
│   │   ├── Players/        # Player, Dealer, AI player classes
│   │   ├── Component.cs    # Abstract drawable/updatable base
│   │   └── Globals.cs      # Game constants (bust number, deck count, etc.)
│   ├── States/             # Game state machine (Menu, Game, etc.)
│   └── Content/            # Assets (card textures, fonts, UI textures)
│       └── Content.mgcb    # MonoGame content pipeline definition
```

## Architecture
- **State pattern:** `BlackjackGame` manages `_currentState`/`_nextState` transitions (MenuState, GameState, etc.)
- **Component pattern:** `Component` base class for anything that draws/updates
- **Card system:** Static texture cache, 6-deck shoe with Fisher-Yates shuffle, soft ace handling
- **Rendering:** MonoGame SpriteBatch for card rendering, ImGui overlay for simulator UI

## Build & Run
```bash
cd MonoBlackjack/MonoBlackjack
dotnet tool restore    # Restores mgcb content builder
dotnet build
dotnet run
```

## Content Pipeline
Assets are managed through MonoGame's MGCB content pipeline (`Content/Content.mgcb`). The `dotnet-mgcb` tool is registered as a local tool in `.config/dotnet-tools.json` and is invoked automatically during build.

## Conventions
- Keep MonoGame rendering and ImGui UI as separate concerns
- Game simulation logic should be decoupled from rendering (testable independently)
- Stats/data layer uses SQLite — no in-memory-only stats
- Cross-platform always: no Windows-only dependencies, no platform-specific fonts
- Font: Liberation Sans (cross-platform substitute for Impact)

## Design Direction
- Old-school aesthetic: the current dark green felt + card textures set the tone
- ImGui panels for all configuration/stats (not game-styled custom UI)
- Data-first: the simulator is a tool for analysis, not just a game to play
- Configurable everything: rules, deck count, dealer behavior, AI strategies
