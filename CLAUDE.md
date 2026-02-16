# MonoBlackjack - Project Context

## What This Is
Casino-grade blackjack simulator with SQLite-backed analytics. Not just a game — a data tool for strategy analysis with configurable rules, detailed stats tracking, and strategy matrix visualization.

## Architecture

**Multi-Project Clean Architecture**
```
MonoBlackjack.Core   → Domain logic, events, game rules (no dependencies)
MonoBlackjack.Data   → SQLite repositories, schema, persistence
MonoBlackjack.App    → MonoGame UI, EventBus, state machine, rendering
```

- **Event-driven:** GameRound emits events → EventBus → StatsRecorder/UI subscribe
- **Composition root:** BlackjackGame.cs wires everything up
- **State pattern:** MenuState, GameState, SettingsState, StatsState

## Tech Stack
- .NET 10, MonoGame 3.8.4.1 (DesktopGL)
- SQLite (Microsoft.Data.Sqlite)
- xUnit + FluentAssertions (94 tests, all passing)

## Build & Run
```bash
dotnet build MonoBlackjack.slnx
dotnet test MonoBlackjack.slnx
dotnet run --project src/MonoBlackjack.App
```

## Key Conventions

- **Domain purity:** Core project has zero external dependencies
- **Testability:** All game logic unit tested, rules flow via immutable `GameRules` instances
- **Events:** All state changes emit domain events (see RoundEvents.cs)
- **Config philosophy:** Configurable = real casino variations. Hardcoded = universal blackjack constants (BustNumber=21, etc.)
- **Cross-platform:** No OS-specific code, Liberation Sans font

## Coordinate System Standard
- ALL UI/game render elements use CENTER-ANCHOR positioning
- `Position` is always the element center point
- `DestRect` converts center to top-left: `(Position.X - Size.X/2, Position.Y - Size.Y/2)`
- Direct `SpriteBatch.Draw`/`DrawString` calls should explicitly convert center coordinates to top-left (or use centered origins)

## Database
- **Location:** `~/.local/share/MonoBlackjack/monoblackjack.db`
- **Schema:** Profile → Session → Round → HandResult/CardSeen/Decision
- **Rule fingerprint:** BlackjackPayout, DealerHitsS17, DeckCount, SurrenderRule stored per round
- **Migrations:** Handled in DatabaseManager.RunMigrations()

## Important Files
- `GameRound.cs` (572 lines) — Round orchestration, all game actions (Hit/Stand/Split/Double/Surrender)
- `GameState.cs` (975 lines) — Main gameplay UI, card rendering, button layout
- `StatsState.cs` — Dashboard with Overview/Analysis tabs
- `GameConfig.cs` — Constants + setting keys (no mutable gameplay state)
