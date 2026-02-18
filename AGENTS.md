# MonoBlackjack Codex

This file is the general-purpose working guide for contributors and coding agents.

## Project Summary
MonoBlackjack is a casino-grade blackjack simulator with SQLite-backed analytics.

Primary goals:
- Accurate blackjack domain behavior
- Configurable real-world casino rule variants
- Strong analytics/data capture
- Clean architecture and maintainable code

## Architecture
Solution structure:
- `src/MonoBlackjack.Core`: domain model, rules engine, round flow, domain events, interfaces/ports
- `src/MonoBlackjack.Data`: SQLite persistence, repository implementations, schema/migrations
- `src/MonoBlackjack.App`: MonoGame UI, rendering, input/state flow, composition root
- `tests/MonoBlackjack.Core.Tests`: unit tests for core game logic

Direction of dependencies:
- `Core` has no framework/infrastructure dependencies
- `Data` depends on `Core`
- `App` depends on `Core` and `Data`

## Core Runtime Patterns
- Event-driven flow: core emits round/game events, app layer reacts/subscribes.
- Composition root in app startup (`BlackjackGame`) wires dependencies.
- State pattern drives screens (`MenuState`, `GameState`, `SettingsState`, `StatsState`).
- Rules/config should be immutable while in use (favor `GameRules`-style contracts).

## Non-Negotiable Engineering Rule
No backward compatibility with old data/contracts.

Implications:
- Do not add legacy adapters, dual-read logic, fallback parsing, or compatibility branches for old schemas/settings/contracts.
- Persist and read only the current supported contract.
- When a contract changes, update callers and storage to the new contract directly.
- Remove dead compatibility code instead of preserving it.

## Contract Design Guidance
- Keep contracts explicit and narrow at boundaries (Core <-> Data, Core <-> App).
- Prefer strongly typed models/enums over magic strings.
- Validate at boundaries; reject invalid values early.
- Keep domain rules in `Core`, not in UI or persistence glue.

## UI/Rendering Conventions
- Use center-anchor positioning consistently for UI/game elements.
- `Position` represents center point; convert to top-left only at draw rectangle/origin boundaries.
- Maintain responsive layout behavior across common resolutions.

## Data/Persistence Notes
- SQLite is the persistence backend.
- Migrations are managed in data layer startup (`DatabaseManager.RunMigrations()` in current implementation).
- Store full round/rule context needed for analytics queries.
- Since backward compatibility is not required, schema evolves for clarity and correctness over legacy support.

## Build, Run, Test
From repository root:

```bash
dotnet build MonoBlackjack.slnx
dotnet test MonoBlackjack.slnx
dotnet run --project src/MonoBlackjack.App
```

## Working Expectations for Changes
- Preserve clean separation of concerns across Core/Data/App.
- Prefer small, explicit refactors over hidden behavior changes.
- Add or update tests when domain behavior or contract shape changes.
- Keep codebase free of stale phase/legacy comments and compatibility scaffolding.

## High-Value Files (Orientation)
- `src/MonoBlackjack.Core/GameRound.cs`: main round orchestration and actions
- `src/MonoBlackjack.Core/RoundEvents.cs`: event contracts emitted by domain
- `src/MonoBlackjack.App/BlackjackGame.cs`: composition root and app wiring
- `src/MonoBlackjack.App/States/GameState.cs`: primary gameplay UI/state logic
- `src/MonoBlackjack.Data/DatabaseManager.cs`: schema and migrations
