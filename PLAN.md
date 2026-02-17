# MonoBlackjack: Production Roadmap

## Context

MonoBlackjack is a casino-grade blackjack simulator with strong domain modeling and clean architecture. Through user testing (Testing.md) and comprehensive code review (CODE_REVIEW.md), we've identified critical architectural issues, UI/UX problems, and opportunities for improvement. This roadmap organizes all issues into prioritized phases to bring the game to production quality.

**Current State:**
- 94 passing tests, clean architecture (Core/Data/App separation)
- Strong event-driven design with EventBus
- Critical anti-pattern: Mutable static GameConfig
- UI scaling issues, poor resize handling
- No pause menu or keyboard controls
- Settings need restructuring into sections

**Goal:** Transform from "B+ with caveats" to "A+ showcase-quality codebase" suitable for portfolio, open-source promotion, or commercial productization.

---

## Phase 1: Critical Architecture Fixes (Foundation) (COMPLETE)
### 1.1 Refactor GameConfig → Immutable GameRules Record (COMPLETE)
### 1.2 Standardize Coordinate System to Center-Anchor (COMPLETE)
### 1.3 Add Input Validation Layer (COMPLETE)

## Phase 2: UI/UX Core Fixes (Visual Quality) (COMPLETE)
### 2.1 Improve Window Resizing and Font Scaling (COMPLETE)
### 2.2 Fix Button Layout and Spacing (COMPLETE)
### 2.3 Fix Card Positioning Alignment (COMPLETE)

## Phase 3: Navigation & State Management (UX Flow) (COMPLETE)
### 3.1 Implement Pause Menu in GameState (COMPLETE)
### 3.2 Add State History Stack for Back Navigation (COMPLETE)

## Phase 4: Settings Reorganization (Extensibility) (COMPLETE)
### 4.1 Restructure Settings into Sections with Tabs (COMPLETE)
### 4.2 Move Bet Mode to Game Mode Selection in MenuState (COMPLETE)
### 4.3 Add Hand Value Display Configuration (COMPLETE)
 

## Phase 5: Stats Dashboard Improvements
**Priority:** P2 - Polish
**Estimated Effort:** 3-4 hours
**Dependencies:** Phase 2.1 (font scaling)

### 5.1 Improve Stats Layout and Readability
**Problem:** User reported stats are "hard to read" and "needs a lot of work" on resizing.

**Files Affected:**
- `src/MonoBlackjack.App/States/StatsState.cs`

**Approach:**
1. Use responsive font scaling from Phase 2.1
2. Increase contrast (darker background, lighter text)
3. Add more vertical spacing between stat lines
4. Use larger font for hero numbers (net profit, bankroll)
5. Add visual separators between sections (horizontal lines)
6. Improve matrix visualization (larger cells, clearer colors)

**Benefits:**
- Professional dashboard appearance
- Easy to read at all resolutions
- Better data visualization

**Verification:**
- Stats remain readable at 800x600 and 4K
- Matrix colors clearly distinguishable
- No text overlap or clipping

---

## Phase 6: Testing & Quality (Stability)
**Priority:** P3 - Maintenance
**Estimated Effort:** 3-5 hours
**Dependencies:** Phases 1-4 complete

### 6.1 Add UI Smoke Tests
**Problem:** No automated tests for GameState, MenuState, SettingsState, StatsState.

**Files Affected:**
- New: `tests/MonoBlackjack.App.Tests/StateTests.cs`

**Approach:**
1. Create test project for App layer
2. Add smoke tests for each state:
   ```csharp
   [Fact]
   public void GameState_Update_DoesNotThrow()
   {
       var state = CreateGameState(); // Helper with mocks
       var act = () => state.Update(new GameTime());
       act.Should().NotThrow();
   }

   [Fact]
   public void GameState_HandleResize_UpdatesPositions()
   {
       var state = CreateGameState();
       state.HandleResize(new Rectangle(0, 0, 1920, 1080));
       // Assert button positions are within viewport
   }
   ```
3. Mock MonoGame dependencies (GraphicsDevice, ContentManager)
4. Verify states don't crash on basic operations

**Benefits:**
- Catch null reference exceptions
- Regression testing for UI changes
- Confidence in refactors

**Verification:**
```bash
dotnet test tests/MonoBlackjack.App.Tests  # All smoke tests pass
```

---

### 6.2 Extract Magic Numbers to Constants
**Problem:** Hard-coded values (100×145 card size, 0.45f overlap) scattered throughout codebase.

**Files Affected:**
- All files with magic numbers
- New: `src/MonoBlackjack.App/UIConstants.cs`

**Approach:**
1. Create `UIConstants.cs` with all layout constants (from Phase 2.2)
2. Replace all hard-coded numbers with named constants
3. Document meaning of each constant

**Benefits:**
- Self-documenting code
- Easy to adjust layout globally
- Prevents inconsistencies

---

### 6.3 Cleanup Dispose Pattern
**Problem:** `EventBus` doesn't implement `IDisposable`; `State.Dispose()` is virtual, not interface.

**Files Affected:**
- `src/MonoBlackjack.App/Events/EventBus.cs`
- `src/MonoBlackjack.App/States/State.cs`

**Approach:**
1. Make `EventBus : IDisposable`:
   ```csharp
   public void Dispose()
   {
       Clear(); // Unsubscribe all handlers
   }
   ```
2. Change `State` to implement `IDisposable` interface (not just virtual method)
3. Verify all states call `Dispose()` on transitions

**Benefits:**
- Consistent lifecycle management
- Prevents memory leaks
- Proper resource cleanup

---

### 6.4 Add Logging Abstraction
**Problem:** Console.WriteLine for errors; no structured logging.

**Files Affected:**
- All files with Console.WriteLine
- New: Add `Microsoft.Extensions.Logging` package

**Approach:**
1. Add `ILogger<T>` dependency injection
2. Replace Console.WriteLine with `_logger.LogError()`, `_logger.LogWarning()`
3. Configure logging in `BlackjackGame` (console + file sinks)

**Benefits:**
- Structured logs (JSON output)
- Easier debugging in production
- Log levels (Debug, Info, Warning, Error)

---

## Phase 7: Polish & Future Features (Enhancement)
**Priority:** P4 - Optional
**Estimated Effort:** 10-15 hours
**Dependencies:** All previous phases

### 7.1 Implement Keyboard Controls
**User Request:** "I want you to be able to play blackjack with your keyboard. That's going to be faster."

**Approach:**
1. Add keybind configuration in Settings (from Phase 4.1)
2. Default keybinds:
   - H: Hit
   - S: Stand
   - P: Split
   - D: Double Down
   - R: Surrender
   - Space/Enter: Deal, Confirm
   - Escape: Pause
3. Create `KeybindManager` to map keys to actions
4. Update GameState to check both button clicks and keybinds

---

### 7.2 Add Graphics Settings
**User Request:** "Graphics settings. Maybe you can change the font, the background color."

**Approach:**
1. Add graphics section in Settings (from Phase 4.1)
2. Settings:
   - Background color (Green, Blue, Red, Custom)
   - Font size multiplier (0.8x, 1.0x, 1.2x, 1.5x)
   - Card back design (multiple textures)
3. Apply settings in State base class

---

### 7.3 Build Assistance/Recommendation System
**User Request:** "Recommendation system depending on what the dealer up card is and what your cards are. You could have like, we recommend you do this."

**Approach:**
1. Implement basic strategy engine (optimal play matrix)
2. Add "Show Recommendations" toggle in Assistance settings
3. Display recommended action with icon/highlight in GameState
4. Track "deviation from basic strategy" in stats dashboard

---

### 7.4 Add Dealer Peek Animations
**User Request:** "Dealer peak we need like dealer peak animations, kind of clarity on what's going on in the game."

**Approach:**
1. Add animation when dealer checks for blackjack
2. Show "Dealer peeks..." message
3. Animate dealer card flip if blackjack
4. Use TweenManager for smooth animations

---

## Critical Files Reference

**Core Domain:**
- `/home/Alex/Projects/MonoBlackjack/src/MonoBlackjack.Core/GameConfig.cs` - Mutable static (refactor target)
- `/home/Alex/Projects/MonoBlackjack/src/MonoBlackjack.Core/GameRound.cs` - Game orchestration
- `/home/Alex/Projects/MonoBlackjack/src/MonoBlackjack.Core/Hand.cs` - Hand evaluation

**Application:**
- `/home/Alex/Projects/MonoBlackjack/src/MonoBlackjack.App/BlackjackGame.cs` - Composition root
- `/home/Alex/Projects/MonoBlackjack/src/MonoBlackjack.App/States/GameState.cs` - Main gameplay (1065 lines, complex)
- `/home/Alex/Projects/MonoBlackjack/src/MonoBlackjack.App/States/SettingsState.cs` - Settings UI
- `/home/Alex/Projects/MonoBlackjack/src/MonoBlackjack.App/States/MenuState.cs` - Menu UI
- `/home/Alex/Projects/MonoBlackjack/src/MonoBlackjack.App/States/StatsState.cs` - Analytics dashboard

**Rendering:**
- `/home/Alex/Projects/MonoBlackjack/src/MonoBlackjack.App/Rendering/Sprite.cs` - Center-anchor base
- `/home/Alex/Projects/MonoBlackjack/src/MonoBlackjack.App/Rendering/CardRenderer.cs` - Texture loading
- `/home/Alex/Projects/MonoBlackjack/src/MonoBlackjack.App/Controls/Button.cs` - Button component

**Tests:**
- `/home/Alex/Projects/MonoBlackjack/tests/MonoBlackjack.Core.Tests/GameRoundTests.cs` - Has workarounds
- `/home/Alex/Projects/MonoBlackjack/tests/MonoBlackjack.Core.Tests/ShoeTests.cs` - Has workarounds

---

## Success Metrics

**Phase 1 (Architecture):**
- ✅ GameConfig class deleted
- ✅ All tests pass without [Collection] attributes
- ✅ Zero try/finally blocks for config restoration
- ✅ CardRenderer.DrawCard deleted

**Phase 2 (Visual Quality):**
- ✅ Game playable at 800x600 minimum resolution
- ✅ Text readable at 4K (3840x2160)
- ✅ Button spacing consistent at all sizes
- ✅ Player/dealer cards visually aligned

**Phase 3 (UX Flow):**
- ✅ Escape key pauses game
- ✅ Can navigate: Game → Pause → Settings → Back → Resume
- ✅ State history respects navigation intent

**Phase 4 (Settings):**
- ✅ Settings organized into 4 tabs (Rules, Keybinds, Graphics, Assistance)
- ✅ Game mode selection separate from settings
- ✅ Hand values configurable

**Phase 5 (Stats):**
- ✅ Stats dashboard readable at all resolutions
- ✅ Matrix visualization clear

**Phase 6 (Quality):**
- ✅ 100+ tests passing (94 existing + new UI tests)
- ✅ Zero magic numbers in positioning code
- ✅ EventBus implements IDisposable
- ✅ Structured logging in place

**Phase 7 (Future):**
- ✅ Keyboard controls functional
- ✅ Graphics settings applied
- ✅ Recommendation system accurate
- ✅ Dealer peek animations smooth

---

## Risks & Mitigations

**Risk:** GameConfig refactor breaks existing functionality
**Mitigation:** Keep GameConfig as adapter during transition; incremental refactor; comprehensive testing

**Risk:** Pause menu breaks game state
**Mitigation:** Freeze tweens when paused; test all phase transitions

**Risk:** Font scaling makes text too large/small
**Mitigation:** Clamp scale factors (0.5x-2.0x); test at extreme resolutions

**Risk:** Settings sections increase complexity
**Mitigation:** Start with placeholder content; implement iteratively

**Risk:** State history stack grows unbounded
**Mitigation:** Limit to 5 entries; clear on MenuState

---

## Post-Roadmap: Future Enhancements (Beyond Scope)

- Multiplayer (local/online)
- Card counting practice mode
- AI opponents at table
- Session import/export (CSV)
- Mobile/web versions
- Accessibility (screen reader, colorblind modes)
- Advanced analytics (strategy deviation tracking)
- Multiple table skins/themes
- Sound effects and music
- Achievements/progression system
