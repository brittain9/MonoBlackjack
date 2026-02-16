# MonoBlackjack Code Review

**Reviewer:** Senior C# Game Developer
**Date:** 2026-02-15
**Codebase Stats:** ~5,887 LOC | 94 Tests (All Passing) | 5 Projects
**Phase:** 7 of 8 Complete (Production-Ready Stats Dashboard)

---

## Executive Summary

**Overall Assessment: Strong B+ (Production-Ready with Architectural Concerns)**

MonoBlackjack demonstrates **exceptional domain modeling** and **event-driven architecture** for a casino simulation. The separation between pure domain logic (Core) and UI concerns (App) is textbook clean architecture. The codebase is well-tested, thoughtfully documented, and shows clear design intent.

**However**, there are **critical architectural anti-patterns** (mutable static config) and **coordinate system inconsistencies** that could cause maintenance headaches. The project is in a mature state but needs refactoring in specific areas before scaling further.

**Production Readiness:** ✅ Yes, with caveats
**Code Quality:** High (clean, testable, well-documented)
**Architecture:** Mixed (excellent domain layer, problematic global state)
**Maintainability:** Good (needs config refactoring to become excellent)

---

## High-Level Architecture Review

### Clean Architecture Implementation ✅

The three-project structure is **exemplary**:

```
MonoBlackjack.Core   → Pure domain logic (zero dependencies)
MonoBlackjack.Data   → SQLite infrastructure (depends on Core)
MonoBlackjack.App    → MonoGame UI + composition root (depends on both)
```

**Strengths:**
- **True dependency inversion**: Core defines port interfaces (`IStatsRepository`, `IProfileRepository`), Data implements them
- **No framework leakage**: Core has zero MonoGame/SQLite references
- **Composition root**: `BlackjackGame.cs` wires everything up properly
- **Testability**: 94 tests run without UI/database (in-memory stubs)

**Evidence of Excellence:**
- `GameRound` takes `Action<GameEvent>` instead of coupling to `EventBus` directly
- `Hand.Evaluate()` is a pure static method (no state mutation)
- `Card`, `Hand`, `Shoe` are framework-agnostic value objects/entities

### Domain-Driven Design ✅

**Strong domain modeling:**
- **Aggregates**: `GameRound` orchestrates the round lifecycle, `Hand` owns its cards
- **Events**: 22 domain events in `RoundEvents.cs` (e.g., `PlayerHit`, `DealerBusted`, `InsuranceOffered`)
- **Ubiquitous language**: `Shoe`, `Dealer`, `Surrender`, `Peek`, `Upcard` — matches casino terminology exactly
- **Encapsulation**: `GameRound._currentHandIndex` is private; external code can't break phase flow

**Why this matters:**
- The domain logic **reads like a rulebook** (`CanSplit()`, `CanDoubleDown()` methods encode casino rules)
- You could swap MonoGame for Unity/Godot/Console and reuse 100% of Core
- Business rules are **unit testable** without UI/DB spin-up

### Event-Driven Architecture ✅

**Implementation:**
- `EventBus` queues events during game logic, flushes during `Update()` loop
- Decouples domain mutations from rendering reactions
- `StatsRecorder` subscribes to events → writes to SQLite at round end (async persistence)

**Smart design choices:**
1. **Queue-then-flush pattern**: Prevents reentrancy issues (game logic never blocks on UI)
2. **Type-safe subscriptions**: `Subscribe<PlayerHit>(handler)` uses generics, not string keys
3. **Disposable tokens**: `IDisposable` subscriptions prevent memory leaks (recently fixed in #257-#264)

**Minor concern:**
EventBus now uses `SubscriptionToken` pattern (good), but the implementation could use `WeakReference` to prevent leaked handlers if someone forgets to call `Dispose()`. Current design requires explicit cleanup.

### State Pattern for Game Screens ✅

```csharp
MenuState → SettingsState → GameState → StatsState
```

Each state inherits from `State` base class with `Update()`, `Draw()`, `HandleResize()` lifecycle.

**Pros:**
- Clean transitions via `BlackjackGame.ChangeState()`
- Each state owns its resources (disposed on transition)
- No giant switch-case mess

**Cons:**
- No state history/back button support (can't go Settings → Game → Settings → Back to Game)
- Resize handler is duplicated in MenuState/SettingsState (see observation #265)

---

## Design Strengths

### 1. **Exceptional Test Coverage**
- **94 tests, all passing** (CardTests, HandTests, GameRoundTests, ShoeTests, etc.)
- **FluentAssertions** for readable test expectations
- **Deterministic seeding**: `new Random(42)` for reproducible tests
- **`[Collection("GameConfig")]`** attribute serializes tests touching static config (prevents race conditions)

**Example of quality:**
```csharp
[Fact]
public void Deal_WhenCutCardReached_ReshufflesAndPublishesShoeEvents()
{
    // Deterministic setup, event verification, state assertions
}
```

### 2. **Cryptographic Shuffling**
- `Shoe` supports both `Random` (testing) and `RandomNumberGenerator` (production)
- `GameConfig.UseCryptographicShuffle` flag toggles behavior
- **This is casino-grade** — many hobby projects ignore RNG quality

### 3. **SQLite Schema Design**
- **Rule fingerprint storage**: `BlackjackPayout`, `DealerHitsS17`, `DeckCount`, `SurrenderRule` columns on `Round` table
- **Decision tracking**: Every player action stored with context (hand value, dealer upcard, action, outcome)
- **Foreign key enforcement**: `PRAGMA foreign_keys = ON;` + proper indexes
- **Migration pattern**: `RunMigrations()` handles schema evolution gracefully

**This enables analytics queries like:**
```sql
SELECT Action, AVG(ResultPayout)
FROM Decision
WHERE PlayerValue = 16 AND DealerUpcard = 'T'
GROUP BY Action;
```

### 4. **Configurable House Rules**
All **real casino variations** are configurable:
- Blackjack payout (3:2 vs 6:5)
- Dealer hits soft 17 (H17 vs S17)
- Surrender rules (early/late/none)
- Double after split, resplit aces, max splits
- Shoe penetration (cut card threshold)

**Smart decision:** Hardcoded `BustNumber=21`, `InsurancePayout=2:1` (not blackjack if you change these)

### 5. **Clean Separation of Concerns**
- `CardRenderer` is the **only** class that knows about textures
- `GameState` handles input/rendering, never touches domain logic
- `StatsRecorder` is a passive event subscriber (zero game logic)

---

## Design Issues & Concerns

### 🔴 **CRITICAL: Mutable Static GameConfig**

**The Problem:**
```csharp
public static class GameConfig
{
    public static int NumberOfDecks = 6;
    public static bool DealerHitsSoft17 = false;
    public static decimal BlackjackPayout = 1.5m;
    // ... 12 more mutable static fields
}
```

**Why This Is Bad:**
1. **Global mutable state** — any code anywhere can mutate game rules mid-flight
2. **Thread-unsafe** — if you ever add multiplayer or background simulation, this will cause race conditions
3. **Testing pollution** — tests must serialize (`[Collection("GameConfig")]`) to avoid interference
4. **No validation** — nothing prevents `NumberOfDecks = -5` or `BlackjackPayout = 0`
5. **Hidden dependencies** — `GameRound`, `Shoe`, `Hand` all secretly depend on this static class

**Evidence:**
- GameRoundTests.cs:49-79 shows tests manually resetting `GameConfig.PenetrationPercent` in try/finally blocks
- CLAUDE.md observation #288 notes "Test Race Condition Already Fixed with Collection Attributes" (workaround, not solution)

**How to Fix:**
Refactor to immutable value object passed through constructors:

```csharp
public sealed record GameRules(
    int NumberOfDecks,
    bool DealerHitsSoft17,
    decimal BlackjackPayout,
    // ... other rules
)
{
    // Factory method with defaults
    public static GameRules Standard => new(
        NumberOfDecks: 6,
        DealerHitsSoft17: false,
        BlackjackPayout: 1.5m,
        // ...
    );

    // Validation in constructor
    public GameRules
    {
        if (NumberOfDecks < 1) throw new ArgumentException(...);
    }
}

// Usage
public class GameRound
{
    private readonly GameRules _rules;
    public GameRound(Shoe shoe, Human player, Dealer dealer,
                     GameRules rules, Action<GameEvent> publish)
    {
        _rules = rules;
        // ...
    }
}
```

**Impact:** High effort (~2-3 hours), high value (removes architectural smell)

---

### 🟡 **MODERATE: Coordinate System Inconsistency**

**The Problem:**
Two different positioning models coexist:

1. **CardRenderer**: Top-left origin (traditional UI coordinates)
   ```csharp
   var destRect = new Rectangle((int)position.X, (int)position.Y, ...);
   ```

2. **CardSprite/Button**: Center-anchor origin (game object coordinates)
   ```csharp
   public Rectangle DestRect => new(
       (int)(Position.X - Size.X / 2),
       (int)(Position.Y - Size.Y / 2), ...);
   ```

**Evidence:** Observations #248-#250 document this inconsistency

**Why This Matters:**
- **Layout bugs**: Button positioning code looks correct but renders incorrectly (or vice versa)
- **Cognitive load**: Developers must mentally track "which coordinate system am I in?"
- **Copy-paste errors**: Easy to use wrong method and get subtly wrong positions

**How to Fix:**
1. **Option A (Recommended)**: Standardize on center-anchor everywhere
   - More intuitive for game objects (rotation, scaling)
   - Aligns with physics/animation systems

2. **Option B**: Standardize on top-left everywhere
   - Matches HTML/CSS/WPF conventions
   - Easier for absolute layouts

3. **Option C**: Create explicit types
   ```csharp
   readonly record struct TopLeftPosition(float X, float Y);
   readonly record struct CenterPosition(float X, float Y);
   ```
   Type system prevents mixing (compile error instead of runtime bug)

**Impact:** Medium effort (2-4 hours), medium value (prevents future bugs)

---

### 🟡 **MODERATE: No Validation Layer**

**Missing:**
- Input validation at boundaries (UI → Core)
- Defensive checks in public APIs

**Examples:**
```csharp
public void PlaceBet(decimal amount)
{
    // ✅ Good: validates in Betting phase
    if (Phase != RoundPhase.Betting)
        throw new InvalidOperationException(...);

    // ❌ Missing: no validation for negative amounts
    // ❌ Missing: no validation for NaN/Infinity decimals
}
```

**Recommendation:**
Add guard clauses to public methods:
```csharp
public void PlaceBet(decimal amount)
{
    if (amount < 0)
        throw new ArgumentOutOfRangeException(nameof(amount), "Bet cannot be negative");
    if (decimal.IsNaN(amount) || decimal.IsInfinity(amount))
        throw new ArgumentException("Bet must be a valid number", nameof(amount));
    // ... existing phase check
}
```

Use **FluentValidation** or manual guard clauses. Prevents garbage-in-garbage-out.

---

### 🟢 **MINOR: String-Based Lookups**

**Current:**
```csharp
if (evt.Recipient == "Player") // Magic string
if (evt.Recipient == "Dealer") // Magic string
```

**Better:**
```csharp
public enum Recipient { Player, Dealer }
public record CardDealt(Card Card, Recipient Recipient, int HandIndex, bool FaceDown);
```

**Why:** Type safety, autocomplete, refactor-proof

**Impact:** Low effort (30 min), low value (cosmetic improvement)

---

### 🟢 **MINOR: No Async/Await for Database**

**Current:**
```csharp
_statsRepository.RecordRound(_profileId, round); // Blocking I/O
```

**Observation:**
- SQLite writes are fast (~1ms), so this is fine for single-player
- If you add online leaderboards or cloud sync, blocking I/O will freeze the game

**Future-Proofing:**
Make repositories async:
```csharp
Task RecordRoundAsync(int profileId, RoundRecord round);
```

But **only if** you plan to add network features. Don't prematurely optimize.

---

## Bugs & Technical Debt

### Known Issues (From CLAUDE.md Context)

**Phase 9 Issues:**
- ✅ **P0 Fixed**: EventBus memory leak (#257-#264)
- ⚠️ **P0 Acknowledged**: Mixed coordinate systems (#248-#250, see above)
- ⚠️ **P1**: Card overflow on 5+ cards (adaptive spacing exists, may need tuning)
- ⚠️ **P1**: Insurance affordability check (fixed in #281-#285)
- ✅ **P2 Fixed**: No hand values displayed (#291-#296)
- **P2**: Decimal→Double precision loss (minor, blackjack uses whole dollars mostly)
- **P3**: Typos, dead code, type mismatches (polish phase)

### Discovered Issues

#### 1. **Dispose Pattern Inconsistency**
- `StatsRecorder` implements `IDisposable` ✅
- `EventBus` **does not** implement `IDisposable` ❌
- `State` base class has `virtual void Dispose()` (not `IDisposable`) ⚠️

**Recommendation:** Make `EventBus : IDisposable`, call `Clear()` in `Dispose()`

#### 2. **Potential Integer Overflow in Shoe**
```csharp
public int TotalCards => _deckCount * 52;
```
If `_deckCount = 1000` (max from GameConfig), `TotalCards = 52,000` (safe).
But if someone changes max to 50,000, this overflows.

**Fix:** Use `long` or add validation.

#### 3. **No Logging/Telemetry**
- `StatsRecorder` catches exceptions and writes to `Console.WriteLine` (234)
- No structured logging (Serilog, NLog, etc.)
- No telemetry for debugging production issues

**For a hobby project:** Console.WriteLine is fine
**For production:** Add `ILogger<T>` from `Microsoft.Extensions.Logging`

#### 4. **Nullable Reference Type Warnings Likely Disabled**
`BlackjackGame.cs` has several nullable fields initialized in `LoadContent()`:
```csharp
private SpriteBatch _spriteBatch = null!; // Suppressed warning
```

**This is OK if:**
- LoadContent is guaranteed to be called before Update/Draw
- MonoGame lifecycle enforces this

**Risk:** If someone creates BlackjackGame manually (tests?), NullReferenceException

**Recommendation:** Constructor injection or null checks

---

## Testing Strategy

### Current Approach ✅

**Strengths:**
- **Unit tests** for all core logic (Card, Hand, Shoe, GameRound, Dealer)
- **Integration tests** for SQLite repositories
- **FluentAssertions** for readable expectations
- **xUnit** with Collection attributes for isolation

**Coverage Gaps:**
1. **No UI tests**: GameState, MenuState, SettingsState have zero automated tests
2. **No event ordering tests**: Does EventBus guarantee FIFO order?
3. **No stress tests**: What happens with 1000 deck shoe? 10,000 rounds?
4. **No mutation tests**: Would tests catch if `IsBusted` logic was inverted?

### Recommendations

#### 1. Add Rendering Smoke Tests
```csharp
[Fact]
public void GameState_Draw_DoesNotThrow()
{
    // Given: initialized GameState with mock services
    var state = new GameState(...);
    var spriteBatch = new Mock<SpriteBatch>();

    // When: Draw is called
    var act = () => state.Draw(gameTime, spriteBatch.Object);

    // Then: No exceptions
    act.Should().NotThrow();
}
```

#### 2. Property-Based Tests
Use **FsCheck** to generate random card sequences:
```csharp
[Property]
public Property Hand_Value_Never_Exceeds_21_Unless_Busted(Card[] cards)
{
    var hand = new Hand();
    foreach (var card in cards)
        hand.AddCard(card);

    return (hand.IsBusted || hand.Value <= 21).ToProperty();
}
```

#### 3. Benchmark Tests
```csharp
[Benchmark]
public void Shoe_Shuffle_1000Decks()
{
    var shoe = new Shoe(1000);
    shoe.Shuffle();
}
```

Ensures performance stays acceptable as you add features.

---

## Future Enhancements

### Phase 8: Dev Overlay (Planned)
From ROADMAP.md — debugging UI for developers.

**Suggestions:**
- Show EventBus queue depth (detect event storms)
- Display current GameConfig values
- Toggle slow-motion (0.1x game speed)
- Frame time graph (detect stutter)

### Beyond Phase 8

#### 1. **AI Opponent / Strategy Advisor**
- Implement **basic strategy engine** (optimal decisions per hand value + dealer upcard)
- Show "recommended action" button in UI
- Track "deviation from basic strategy" in stats dashboard
- **Value:** Turns this into a learning tool, not just a game

#### 2. **Card Counting Practice Mode**
- Add "running count" display (Hi-Lo, KO systems)
- "True count" calculation (running count ÷ decks remaining)
- Track betting correlation (did you bet more on positive counts?)
- **Value:** Unique selling point for casino enthusiasts

#### 3. **Session Import/Export**
- Export decision history as CSV
- Import into R/Python for custom analysis
- Share rulesets as JSON files
- **Value:** Appeal to data nerds

#### 4. **Multiplayer (Local/Online)**
- Multiple players at same table
- Compare decisions in real-time
- Leaderboards (who deviates least from basic strategy?)
- **Technical:** Requires async refactor + state synchronization

#### 5. **Platform Expansion**
- **Mobile**: Touch controls, portrait layout
- **Web**: Blazor WebAssembly (Core project is already portable!)
- **Console**: ASCII art for terminal blackjack

#### 6. **Accessibility**
- Screen reader support (announce card values)
- Colorblind modes (heatmaps with patterns, not just color)
- Keyboard-only controls (no mouse required)
- Configurable font sizes

---

## Architectural Recommendations

### Immediate (Do Now)

1. **Refactor GameConfig to immutable record** (2-3 hours)
   - Remove global mutable state
   - Thread-safe, testable, composable

2. **Standardize coordinate system** (2-4 hours)
   - Choose center-anchor or top-left
   - Document decision in CLAUDE.md

3. **Implement EventBus.Dispose()** (15 min)
   - Prevent potential memory leaks

### Short-Term (Next Sprint)

4. **Add input validation guards** (1-2 hours)
   - Public method preconditions
   - Fail fast with clear errors

5. **Introduce ILogger abstraction** (1 hour)
   - Replace Console.WriteLine
   - Enable structured logging

6. **Write UI smoke tests** (2-3 hours)
   - Ensure states don't crash on basic operations

### Long-Term (Future Phases)

7. **Extract EventBus to NuGet package**
   - Reusable across projects
   - Proper documentation, versioning

8. **Migrate to .NET Hosted Services pattern**
   - Replace State pattern with DI-friendly approach
   - Enables testability of state transitions

9. **Consider CQRS for analytics queries**
   - Read models optimized for dashboard
   - Separate write model (game events)

---

## Performance Considerations

### Current Bottlenecks (Estimated)

1. **SQLite writes**: ~1ms per round (negligible)
2. **Card rendering**: 52 sprite draws max (~0.1ms at 60fps)
3. **Event flushing**: O(n) where n = events per frame (~10-20)

**Verdict:** Performance is **not a concern** for current scope.

### Future Concerns

- **1000-deck shoe**: 52,000 cards × shuffle = ~5ms (still acceptable)
- **10,000 rounds in stats dashboard**: SQLite query time could hit 50-100ms
  - Mitigation: Add indexes on Decision.Action, Decision.PlayerValue
  - Already done! (DatabaseManager.cs:127-129)

---

## Code Quality Metrics

### Strengths
- ✅ **SOLID principles** followed (SRP, DIP, OCP evident)
- ✅ **DRY**: Minimal duplication (Hand.Evaluate is static helper)
- ✅ **Naming**: Clear, domain-aligned (Shoe, Peek, Upcard, Surrender)
- ✅ **Comments**: Used sparingly, explain "why" not "what"
- ✅ **File organization**: Logical folder structure

### Areas for Improvement
- ⚠️ **Cyclomatic complexity**: `GameRound.Resolve()` has nested ifs (manageable, but ~8 branches)
- ⚠️ **Method length**: `GameState.cs` methods can reach 50-100 lines (rendering code)
- ⚠️ **Magic numbers**: `CardSize = new(100, 145)` (should be named constant)

**Extract magic numbers:**
```csharp
public static class UIConstants
{
    public const int CardWidth = 100;
    public const int CardHeight = 145;
    public static readonly Vector2 CardSize = new(CardWidth, CardHeight);
}
```

---

## Security Considerations

### Current State
- ✅ **No user input validation needed** (single-player, local game)
- ✅ **SQLite injection prevented** (parameterized queries)
- ✅ **Cryptographic RNG available** (production mode)

### If Adding Multiplayer
- 🔐 **Encrypt save files** (prevent bankroll editing)
- 🔐 **Validate server responses** (prevent cheating)
- 🔐 **Rate limit API calls** (prevent abuse)

---

## Final Recommendations

### Priority 1 (Critical)
1. **Refactor GameConfig to immutable GameRules record**
   - Removes architectural anti-pattern
   - Enables safe concurrency
   - Simplifies testing

### Priority 2 (Important)
2. **Resolve coordinate system inconsistency**
   - Choose one model, document it
   - Prevents layout bugs

3. **Add input validation to public APIs**
   - Defensive programming
   - Better error messages

### Priority 3 (Nice-to-Have)
4. **Implement ILogger abstraction**
5. **Write UI smoke tests**
6. **Extract magic numbers to constants**

---

## Conclusion

**MonoBlackjack is a well-architected, thoughtfully designed casino simulator.** The domain modeling is exemplary, the event-driven design is clean, and the separation of concerns is textbook. The codebase demonstrates maturity beyond a typical hobby project.

**The GameConfig anti-pattern is the primary architectural concern.** Refactoring it to an immutable value object would elevate this project from "good" to "excellent" and remove the biggest maintenance risk.

**The coordinate system inconsistency is a quality-of-life issue** that will bite you as the UI grows. Standardizing early prevents future pain.

**With these two fixes, MonoBlackjack would be a showcase-quality codebase** suitable for portfolio demonstrations, open-source promotion, or commercial productization.

**Estimated Refactor Effort:**
- GameConfig → GameRules: **2-3 hours**
- Coordinate standardization: **2-4 hours**
- Input validation: **1-2 hours**
- **Total: 5-9 hours** to address all critical/important issues

**ROI:** High. These changes prevent entire classes of bugs and make future features (multiplayer, AI, mobile) much easier to implement.

---

## Appendix: Files Reviewed

### Core Domain
- GameRound.cs (591 lines) — Round orchestration
- Hand.cs (67 lines) — Hand evaluation
- Card.cs — Value object
- Shoe.cs (120 lines) — Multi-deck shoe with crypto shuffle
- GameConfig.cs (260 lines) — **⚠️ Mutable static config**
- PlayerBase.cs, Dealer.cs, Human.cs — Player hierarchy
- RoundEvents.cs — 22 domain events

### Data Layer
- DatabaseManager.cs (151 lines) — SQLite schema + migrations
- SqliteStatsRepository.cs, SqliteProfileRepository.cs, SqliteSettingsRepository.cs

### Application Layer
- BlackjackGame.cs (100 lines) — Composition root
- EventBus.cs (85 lines) — Event queue + subscriptions
- StatsRecorder.cs (348 lines) — Event → SQLite persistence
- GameState.cs (975 lines) — Main gameplay UI
- MenuState.cs, SettingsState.cs, StatsState.cs

### Rendering
- CardRenderer.cs (82 lines) — Texture loading + draw
- CardSprite.cs, Sprite.cs, Button.cs — UI components
- SceneRenderer.cs — Layer composition

### Tests
- GameRoundTests.cs (94 tests total)
- HandTests.cs, ShoeTests.cs, CardTests.cs
- SqliteRepositoriesTests.cs

**Total Reviewed:** ~5,887 LOC across 40+ files

---

**Review Complete.** Questions? Need clarification on any recommendation? Let me know!
