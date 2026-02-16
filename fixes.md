## Phase Plan: Gameplay UX Stabilization + Betting/Money System

### Summary
Fix the runtime UX issues you reported (garbled/duplicate outcomes, button/text overlap, split-hand readability), and add a real playable money loop with configurable betting flow.
This plan keeps the current visual style/assets and focuses on correctness, layout resilience, and usability.

### Scope (Phases 1-6) — COMPLETE
1. ~~Fix duplicate/garbled outcome labels (`BUST` + `LOSE` overlap).~~
2. ~~Fix responsive layout/text fitting for action and insurance buttons.~~
3. ~~Reduce confusing split-hand card overlap.~~
4. ~~Add real betting/bankroll gameplay flow.~~
5. ~~Make betting flow configurable in Settings (Betting vs Free Play).~~
6. ~~Add bankrupt handling (out-of-funds screen with reset/menu).~~

### Phase 7 — Stats Dashboard — COMPLETE
- Overview tab: hero net profit, bankroll chart, stat grid (win/loss/push rates, BJ rate, bust rate, avg bet, streak, best/worst).
- Analysis tab: dealer bust by upcard, outcomes by hand value, strategy matrix (hard/soft/pairs), card distribution.
- Scrollable analysis with mouse wheel.
- Menu integration via Stats button.

---

## Phase 8 — Code Review Fixes

Issues found during full code review of all phases + dashboard.

### 8.1) Bankroll can go negative on splits/doubles (BUG)
Files: `GameRound.cs`

The net-payout model never reserves bet amounts from the bank upfront. `CanSplit()` checks `_player.Bank >= _bets[_currentHandIndex]`, but the original bet hasn't been deducted. With Bank=100 and Bet=100 the player can split (two 100-bets), and if both lose: `100 + (-100) + (-100) = -100`.

Fix: track total committed wagers in `GameRound` and check affordability against `Bank - totalCommitted` for split/double/insurance.

### 8.2) Dealer bust detection query is inaccurate (BUG)
Files: `SqliteStatsRepository.cs`

The dealer bust query checks "all hand results are Win, none are Lose/Push". Fails when the player has split hands where one busted (Lose) while the dealer also busted. That round has both Win and Lose outcomes so the query misses the dealer bust.

Fix: record a `DealerBusted` flag on the Round row when persisting, and use that for the query.

### 8.3) Bust count query misses double-down busts (BUG)
Files: `SqliteStatsRepository.cs`

Counting busts as "Hit decisions with Lose outcome where no Stand exists" misses double-down busts (action is "Double" not "Hit").

Fix: include `Action IN ('Hit', 'Double')` in the bust count query.

### 8.4) Test race condition on static GameConfig (BUG)
Files: `GameConfigTests.cs`, `DealerTests.cs`

`GameConfig` is static mutable state and xUnit runs test classes in parallel. `DealerTests` sets `GameConfig.DealerHitsSoft17 = false` concurrently with `GameConfigTests` setting it to `true`.

Fix: add `[Collection("GameConfig")]` to both test classes to serialize them.

### 8.5) No scroll clipping on Analysis tab (VISUAL)
Files: `StatsState.cs`

The Analysis tab uses `_scrollOffset` but never sets a scissor rectangle. Charts scroll over the tab buttons, title, and back button.

Fix: enable `RasterizerState.ScissorTestEnable` on the `SpriteBatch` and set `GraphicsDevice.ScissorRectangle` to the content region.

### 8.6) Missing database indexes on foreign keys (PERF)
Files: `DatabaseManager.cs`

`HandResult.RoundId`, `CardSeen.RoundId`, and `Decision.RoundId` are joined frequently but have no indexes. Strategy matrix and dealer bust queries will be slow on large datasets.

Fix: add indexes in `EnsureSchema()`.

### 8.7) Pixel texture leak on state transitions (QUALITY)
Files: `GameState.cs`, `StatsState.cs`

Both states create a 1x1 `Texture2D` but never dispose it. GPU resources leak each time the player navigates between states.

Fix: create the pixel texture once in `BlackjackGame` and pass it to states that need it.

### 8.8) StatsRecorder held alive only by closure (QUALITY)
Files: `GameState.cs:92`

`_ = new StatsRecorder(...)` discards the reference. It survives only because `EventBus` holds delegates referencing it. Fragile if `EventBus` ever changes.

Fix: store in a field.

### 8.9) Redundant `RulesEqual` method (QUALITY)
Files: `SqliteStatsRepository.cs`

`RuleFingerprint` is a `record` with auto-generated value equality. The custom `RulesEqual` method is unnecessary.

Fix: replace with `left == right`.

### 8.10) Round table missing DealerBusted column (SCHEMA)
Files: `DatabaseManager.cs`, `SqliteStatsRepository.cs`, `StatsRecorder.cs`

Needed for accurate dealer bust stats (see 8.2). Add `DealerBusted INTEGER NOT NULL DEFAULT 0` to the Round table and populate it from `StatsRecorder` when `DealerBusted` event fires.

---

## Phase 9 — Full Architecture & Casino-Grade Code Review

Deep review treating the codebase as a professional casino-grade game and data application with event-driven/DDD-esque clean architecture.

### P0 — Critical

### 9.1) Mixed coordinate systems — cards top-left, buttons center-anchored (LAYOUT ROOT CAUSE)
Files: `Sprite.cs`, `Button.cs`, `GameState.cs`

Cards use top-left origin (`Sprite.Draw` draws from `Position` as top-left), but buttons use center origin (`Button.DestRect` subtracts half-size from `Position`). Every layout formula that mixes card + button positions must mentally switch coordinate systems. This is the root cause of button placement pain and off-by-half-size errors across the codebase.

Fix: Standardize on center-anchor for all positionable elements. Add `Origin` support to `Sprite` base class, update `Button` and card positioning to use the same convention.

### 9.2) EventBus subscriptions never unsubscribed — memory leak on state transitions
Files: `GameState.cs`, `StatsRecorder.cs`, `EventBus.cs`

`GameState` subscribes 18 events, `StatsRecorder` subscribes 12 more. When the player navigates Menu→Game→Menu→Game, new instances are created but old ones are never unsubscribed. Since `EventBus` captures closures over `GameState`'s fields, the old `GameState` (all textures, sprites, etc.) stays rooted in memory. Each round trip leaks the entire previous game's object graph.

Fix: Add `IDisposable` to `GameState`, call `EventBus.Clear()` or add `Unsubscribe` support. Dispose states on transition in `BlackjackGame`.

### 9.3) Menu/Settings resize duplicates click handlers
Files: `MenuState.cs:144-147`, `SettingsState.cs:85-88`

`HandleResize` calls `IntializeMainMenu`/`BuildLayout` which recreates all buttons and re-subscribes click handlers, but old handlers are never removed. After N resize events, clicking "Play" creates N game instances simultaneously.

Fix: Detach old handlers before rebuild, or separate layout (position/size) from construction (button creation + event subscription).

### P1 — Game Logic & Rendering

### 9.4) Dealer card layout doesn't scale with card count
Files: `GameState.cs:276-280`

Dealer cards use fixed spacing (`_cardSize.X * 1.15f`). With 5+ dealer cards, they overflow the viewport edge. Player side has adaptive overlap scaling for splits, but dealer side has none.

Fix: Apply the same adaptive overlap logic used for split hands to the dealer row.

### 9.5) Insurance bet not checked against available funds
Files: `GameRound.cs:157`

`PlaceInsurance` sets `_insuranceBet = _bets[0] / 2` with no affordability check. After a split the player may have committed all funds, making insurance unaffordable but still granted.

Fix: Check `AvailableFunds >= _bets[0] / 2` before allowing insurance, or auto-decline if insufficient.

### 9.6) Single-hand card overflow on many hits
Files: `GameState.cs:273`

Single-hand cards have fixed spacing of `1.15 * cardWidth`. With 7+ cards the hand extends past the viewport. Adaptive overlap only applies to split hands.

Fix: Apply adaptive overlap to single-hand layout too.

### 9.7) Magic multiplier button layout — brittle and unmaintainable
Files: `GameState.cs:206-250`

Button positions use chains of magic multipliers (0.58, 1.1, 0.8, 1.5, 2.8) that don't compose predictably across viewport sizes. Causes overlap on narrow windows and excessive gaps on wide ones.

Fix: Use proper row-based layout: compute total row width, distribute evenly from center.

### 9.8) Mutable static GameConfig — global singleton anti-pattern
Files: `GameConfig.cs`

All fields are `public static` mutable. Any code can silently mutate rules mid-round. Tests must serialize. Settings changes take effect immediately even without saving. No way to have two configs simultaneously.

Fix: Convert to an instance class passed via constructor injection. Long-term improvement, not blocking.

### P2 — Quality & UX

### 9.9) `_round = null!` sentinel in betting phase
Files: `GameState.cs:193`

`_round` is set to `null!` (null-forgiving) during betting phase. If any code path accesses `_round.Phase` during betting, it throws NRE with no useful context.

Fix: Use `GameRound?` nullable and guard access, or create a no-op sentinel round.

### 9.10) No hand values displayed during gameplay
Files: `GameState.cs`

Neither dealer upcard value nor player hand value is shown. Players must mentally count, especially painful with soft hands.

Fix: Draw hand value labels above/below card groups.

### 9.11) Button hover state persists when buttons become invisible
Files: `Button.cs:83-100`

Buttons only have `Update()` called when their phase is active. `_isHovering` is never reset when the button stops updating. Can render in hover state when it reappears.

Fix: Reset `_isHovering = false` at the start of `Draw`, or track visibility explicitly.

### 9.12) `decimal` stored as `double` in SQLite — precision loss
Files: `SqliteStatsRepository.cs:69-70`

Bet amounts and payouts cast `decimal` → `double` for storage. Floating-point drift accumulates over many rounds.

Fix: Store as TEXT and parse with `decimal.Parse`, or store as integer cents.

### 9.13) HandleResize snaps cards instantly, breaks visual continuity
Files: `GameState.cs:922-936`

On resize, all tweens are cleared and cards teleport to new positions. On laptops with continuous resize events, this breaks animations and may cause timer state issues.

Fix: Accept as known limitation or debounce resize handling.

### 9.14) Hand.Evaluate depends on GameConfig statics — impure function
Files: `Hand.cs:47-63`

`Evaluate()` reads `GameConfig.AceExtraValue` and `GameConfig.BustNumber`. The same hand can evaluate differently if config changes between calls.

Fix: Pass bust threshold and ace value as parameters (or accept as known coupling until 9.8 is done).

### 9.15) Domain events use string player names instead of typed identifiers
Files: `RoundEvents.cs`, `StatsRecorder.cs:65`

Events use `string PlayerName`. `StatsRecorder` does `evt.Recipient == "Player"` — if `Human` is constructed with a different name, stats silently break.

Fix: Use a typed enum or constant for recipient identity.

### 9.16) No IDisposable on GameState or StatsState
Files: `GameState.cs`, `StatsState.cs`

GPU resources (`RasterizerState`), event subscriptions, and content references are never cleaned up on state transitions.

Fix: Implement `IDisposable`, call from `BlackjackGame` on state change.

### 9.17) Bankroll history loads all rounds into memory
Files: `SqliteStatsRepository.cs:401-424`

`GetBankrollHistory` materializes every round. After 10K rounds, this creates 10K objects every time the stats screen opens.

Fix: Sample/aggregate in SQL (e.g., every Nth round or window function).

### P3 — Cleanup

### 9.18) `StartingBank` is int but `Bank` is decimal — type mismatch
Files: `GameConfig.cs:64`, `Human.cs:5`

### 9.19) Dead `PostGameState` class — entirely TODO stubs, never used
Files: `PostGameState.cs`

### 9.20) Unused `_scale` field in `State` base class
Files: `State.cs:14`

### 9.21) Typo: `IntializeMainMenu` → `InitializeMainMenu`
Files: `MenuState.cs:23`

### 9.22) `Card.AssetName` allocates string every access
Files: `Card.cs:47-58`

### 9.23) SQL string interpolation in GetStrategyMatrix (code smell)
Files: `SqliteStatsRepository.cs:534`

`softFilter` is a hardcoded constant, not user input, so not injectable. But interpolated SQL makes security audits harder. Should use parameterized approach.
