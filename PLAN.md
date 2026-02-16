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

## Phase 1: Critical Architecture Fixes (Foundation)
**Priority:** P0 - MUST DO FIRST
**Estimated Effort:** 5-7 hours
**Dependencies:** None - these are foundational

### 1.1 Refactor GameConfig → Immutable GameRules Record (COMPLETE)

### 1.2 Standardize Coordinate System to Center-Anchor (COMPLETE)
**Problem:** `CardRenderer.DrawCard` uses top-left origin while `Sprite`/`Button`/`CardSprite` use center-anchor, causing confusion and potential layout bugs.

**Files Affected:**
- `src/MonoBlackjack.App/Rendering/CardRenderer.cs`
- `src/MonoBlackjack.App/Rendering/Sprite.cs`
- `src/MonoBlackjack.App/Controls/Button.cs`
- `src/MonoBlackjack.App/States/GameState.cs`

**Approach:**
1. Audit usage of `CardRenderer.DrawCard` and `DrawHand` methods - **FINDING: They're unused!**
2. Delete `DrawCard` and `DrawHand` methods from `CardRenderer` (only `CreateCardSprite` is used)
3. Add documentation to `Sprite.cs` and `Button.cs` stating center-anchor is the standard
4. Create developer guidelines in `CLAUDE.md`:
   ```
   ## Coordinate System Standard
   - ALL UI elements use CENTER-ANCHOR positioning
   - `Position` property = center point of element
   - `DestRect` calculates top-left from center: `(Position.X - Size.X/2, Position.Y - Size.Y/2)`
   - Direct `SpriteBatch.Draw` calls should calculate center→top-left manually
   ```
5. Verify all positioning code in `GameState.CalculatePositions()` uses center-anchor (already does)

**Benefits:**
- Single mental model for all positioning
- Prevents future off-by-one pixel bugs
- Easier rotation/scaling (centered transformations)

**Verification:**
- Visual inspection: Cards and buttons align correctly after resize
- No layout regressions in MenuState, SettingsState, StatsState

---

### 1.3 Add Input Validation Layer
**Problem:** Public methods in Core lack defensive checks (negative bets, NaN/Infinity decimals, out-of-range values).

**Files Affected:**
- `src/MonoBlackjack.Core/GameRound.cs`
- `src/MonoBlackjack.Core/Shoe.cs`
- `src/MonoBlackjack.Core/Hand.cs`

**Approach:**
1. Add guard clauses to public methods:
   ```csharp
   public void PlaceBet(decimal amount)
   {
       if (amount < 0)
           throw new ArgumentOutOfRangeException(nameof(amount), "Bet cannot be negative");
       if (decimal.IsNaN(amount) || decimal.IsInfinity(amount))
           throw new ArgumentException("Bet must be a valid number", nameof(amount));
       if (Phase != RoundPhase.Betting)
           throw new InvalidOperationException($"Cannot place bet during {Phase} phase");
       // ... existing logic
   }
   ```
2. Validate constructor parameters in all Core classes
3. Add tests for edge cases (negative values, boundary conditions)

**Benefits:**
- Fail fast with clear errors
- Prevents garbage-in-garbage-out
- Better error messages for debugging

**Verification:**
```bash
dotnet test MonoBlackjack.slnx  # New validation tests pass
```

---

## Phase 2: UI/UX Core Fixes (Visual Quality)
**Priority:** P1 - High Impact
**Estimated Effort:** 6-8 hours
**Dependencies:** Phase 1.2 (coordinate standardization)

### 2.1 Improve Window Resizing and Font Scaling
**Problem:** Fonts don't scale responsively (static asset with runtime scale factor), text becomes unreadable on small windows.

**Files Affected:**
- All States: `GameState.cs`, `MenuState.cs`, `SettingsState.cs`, `StatsState.cs`
- `src/MonoBlackjack.App/Controls/Button.cs`

**Approach:**
1. Add min/max scale clamping to all text rendering:
   ```csharp
   var scale = Math.Clamp(calculatedScale, 0.5f, 2.0f);
   ```
2. Extract font scaling logic to helper method in `State` base class:
   ```csharp
   protected float GetResponsiveScale(float baseScale)
   {
       var vp = _graphicsDevice.Viewport;
       var scaleFactor = Math.Min(vp.Width / 1280f, vp.Height / 720f); // Assume 720p baseline
       return Math.Clamp(baseScale * scaleFactor, 0.5f, 2.0f);
   }
   ```
3. Cache `MeasureString` results in Button class (recalculate only on text/size change)
4. Add minimum window size enforcement in `BlackjackGame.cs`:
   ```csharp
   _graphics.PreferredBackBufferWidth = Math.Max(Window.ClientBounds.Width, 800);
   _graphics.PreferredBackBufferHeight = Math.Max(Window.ClientBounds.Height, 600);
   ```

**Benefits:**
- Text remains readable at all window sizes
- Better performance (cached measurements)
- Professional appearance

**Verification:**
- Test at 800x600, 1920x1080, 3840x2160 resolutions
- All text remains legible
- No performance drops during resize

---

### 2.2 Fix Button Layout and Spacing
**Problem:** Hard-coded padding (12f) and magic multipliers (`buttonSize.Y * 1.5f`, `buttonSize.Y * 2.8f`) don't scale with viewport.

**Files Affected:**
- `src/MonoBlackjack.App/States/GameState.cs` (lines 198-267)

**Approach:**
1. Replace hard-coded `buttonPadding = 12f` with viewport-relative:
   ```csharp
   var buttonPadding = vp.Width * 0.01f; // 1% of viewport width
   ```
2. Replace magic multipliers with named constants:
   ```csharp
   private const float DealButtonVerticalOffset = 0.08f; // % of viewport height
   private const float RepeatBetButtonOffset = 0.14f;
   ```
3. Create `UIConstants.cs` file for all layout constants:
   ```csharp
   public static class UIConstants
   {
       public const int CardWidth = 100;
       public const int CardHeight = 145;
       public static readonly Vector2 CardSize = new(CardWidth, CardHeight);
       public const float CardAspectRatio = CardWidth / (float)CardHeight;

       public const float ButtonPaddingRatio = 0.01f;
       public const float DealButtonOffsetRatio = 0.08f;
       public const float RepeatBetOffsetRatio = 0.14f;
   }
   ```
4. Update all button positioning to use named constants

**Benefits:**
- Consistent spacing at all resolutions
- Self-documenting code (named constants explain intent)
- Easy to tweak layout globally

**Verification:**
- Visual inspection at multiple resolutions
- Button spacing looks uniform
- No overlapping buttons

---

### 2.3 Fix Card Positioning Alignment
**Problem:** Player cards don't align with dealer cards; hard-coded overlap ratios (0.45f, 1.8f) may not scale well.

**Files Affected:**
- `src/MonoBlackjack.App/States/GameState.cs` (lines 289-371)

**Approach:**
1. Standardize vertical positioning to use consistent baseline:
   ```csharp
   private float GetDealerCardsY() => _graphicsDevice.Viewport.Height * 0.18f;  // Higher up
   private float GetPlayerCardsY() => _graphicsDevice.Viewport.Height * 0.52f; // Lower down, more spacing
   ```
2. Make overlap/gap ratios viewport-aware:
   ```csharp
   var cardOverlap = _cardSize.X * 0.4f;  // Reduced from 0.45f
   var handGap = Math.Max(_cardSize.X * 1.5f, vp.Width * 0.05f); // Minimum 5% of screen width
   ```
3. Add visual alignment guides (debug mode) to verify centering
4. Test with 1, 2, 3, 4 hands and 2-10 cards per hand

**Benefits:**
- Consistent visual alignment
- Better use of screen space
- Handles edge cases (many cards/hands)

**Verification:**
- Play through game with splits (2-4 hands)
- Verify cards don't overlap excessively
- Dealer and player cards visually balanced

---

## Phase 3: Navigation & State Management (UX Flow)
**Priority:** P1 - User Requested
**Estimated Effort:** 4-6 hours
**Dependencies:** Phase 2 complete (UI fixes in place)

### 3.1 Implement Pause Menu in GameState
**Problem:** No way to exit game without closing window. User explicitly requested escape-to-menu functionality.

**Files Affected:**
- `src/MonoBlackjack.App/States/GameState.cs`
- `src/MonoBlackjack.App/States/State.cs` (add keyboard input handling)

**Approach:**
1. Add pause state to GameState:
   ```csharp
   private bool _isPaused = false;
   private Button _resumeButton;
   private Button _pauseSettingsButton;
   private Button _pauseQuitButton;
   ```
2. Add keyboard handling in `State` base class:
   ```csharp
   protected KeyboardState _previousKeyboardState;
   protected KeyboardState _currentKeyboardState;

   protected bool WasKeyJustPressed(Keys key)
   {
       return _currentKeyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
   }
   ```
3. Handle escape key in GameState.Update():
   ```csharp
   _currentKeyboardState = Keyboard.GetState();

   if (WasKeyJustPressed(Keys.Escape))
   {
       _isPaused = !_isPaused;
   }

   if (_isPaused)
   {
       UpdatePauseMenu(gameTime);
       _previousKeyboardState = _currentKeyboardState;
       return; // Don't update game logic
   }
   ```
4. Create pause menu overlay (dark semi-transparent background + 3 buttons):
   - Resume (unpause)
   - Settings (navigate to SettingsState with return-to-game support)
   - Quit to Menu (confirm dialog, then MenuState)
5. Render pause menu at highest z-order (above all game elements)

**Benefits:**
- User can pause/resume game
- Access settings without losing game state
- Professional game feel

**Verification:**
- Press Escape during betting phase → pause menu appears
- Press Escape during play phase → pause menu appears
- Resume button returns to exact game state
- Quit button returns to MenuState

---

### 3.2 Add State History Stack for Back Navigation
**Problem:** All back buttons hard-coded to MenuState; can't return to previous screen (e.g., Game → Settings → Back to Game).

**Files Affected:**
- `src/MonoBlackjack.App/BlackjackGame.cs`
- `src/MonoBlackjack.App/States/SettingsState.cs`
- `src/MonoBlackjack.App/States/StatsState.cs`

**Approach:**
1. Add state stack to `BlackjackGame`:
   ```csharp
   private readonly Stack<State> _stateHistory = new();

   public void ChangeState(State state, bool pushHistory = true)
   {
       if (pushHistory && _currentState != null)
       {
           _stateHistory.Push(_currentState);
       }
       _nextState = state;
   }

   public void GoBack()
   {
       if (_stateHistory.Count > 0)
       {
           _nextState = _stateHistory.Pop();
       }
       else
       {
           // Fallback: go to menu
           _nextState = new MenuState(this, _graphicsDevice, _content);
       }
   }
   ```
2. Update SettingsState back button to call `_game.GoBack()` instead of hard-coded MenuState
3. Update pause menu "Settings" button to use `ChangeState(..., pushHistory: true)`
4. Add "Back" button to StatsState that respects history

**Benefits:**
- Intuitive navigation (back goes to previous screen)
- Supports complex navigation flows
- User can pause game, check settings/stats, return to game

**Verification:**
- Navigate: Menu → Game → Pause → Settings → Back → Resume
- Navigate: Menu → Stats → Back → Menu
- State stack doesn't grow unbounded (limit to 5 entries?)

---

## Phase 4: Settings Reorganization (Extensibility)
**Priority:** P2 - Medium Impact
**Estimated Effort:** 5-7 hours
**Dependencies:** Phase 3 (navigation in place)

### 4.1 Restructure Settings into Sections with Tabs
**Problem:** 10 unrelated settings in flat list; no room for future keybinds/graphics/assistance features.

**Files Affected:**
- `src/MonoBlackjack.App/States/SettingsState.cs`

**Approach:**
1. Create tabbed settings UI with 4 sections:
   - **Rules** (current casino rule settings)
   - **Keybinds** (future: action mappings)
   - **Graphics** (future: font, background color)
   - **Assistance** (future: show hand values, recommendations)
2. Add section enum and tab buttons:
   ```csharp
   private enum SettingsSection { Rules, Keybinds, Graphics, Assistance }
   private SettingsSection _activeSection = SettingsSection.Rules;

   private Button _rulesTab;
   private Button _keybindsTab;
   private Button _graphicsTab;
   private Button _assistanceTab;
   ```
3. Filter `_rows` display by active section:
   ```csharp
   private void InitializeRows()
   {
       _allRows.Clear();
       var current = _settingsRepository.LoadSettings(_profileId)
           ?? GameRules.Standard.ToSettingsDictionary();

       // Rules section
       AddRow(SettingsSection.Rules, "Dealer Soft 17", ...);
       // ... other rule settings

       // Keybinds section (placeholder)
       AddRow(SettingsSection.Keybinds, "Hit Action", ["H", "Space"], current);
       // ... other keybinds

       // Graphics section (placeholder)
       AddRow(SettingsSection.Graphics, "Background Color", ["Green", "Blue", "Red"], current);

       // Assistance section
       AddRow(SettingsSection.Assistance, "Show Hand Values", ["Yes", "No"], current);
       AddRow(SettingsSection.Assistance, "Show Recommendations", ["Yes", "No"], current);
   }

   private List<SettingRow> GetVisibleRows()
   {
       return _allRows.Where(r => r.Section == _activeSection).ToList();
   }
   ```
4. Implement tab switching with visual feedback (active tab highlighted)

**Benefits:**
- Settings organized logically
- Room for future features without cluttering UI
- Professional settings screen

**Verification:**
- Switch between tabs → correct settings appear
- Save in Rules tab → only rules saved
- Keybinds/Graphics tabs show placeholder content

---

### 4.2 Move Bet Mode to Game Mode Selection in MenuState
**Problem:** "Bet Mode" is mixed with casino rules in settings; should be a game mode choice.

**Files Affected:**
- `src/MonoBlackjack.App/States/MenuState.cs`
- `src/MonoBlackjack.App/States/GameState.cs`
- `src/MonoBlackjack.App/States/SettingsState.cs`

**Approach:**
1. Remove "Bet Mode" row from SettingsState (line ~220)
2. Replace "Play" button in MenuState with two buttons:
   - "Casino Mode" (sets `BetFlowMode.Betting` before entering GameState)
   - "Practice Mode" (sets `BetFlowMode.FreePlay`)
3. Or: Create intermediate "Mode Select" screen with visual description:
   ```
   Casino Mode: Full betting experience, track bankroll
   Practice Mode: No betting, focus on strategy
   ```
4. Pass selected mode to GameState constructor:
   ```csharp
   public GameState(BlackjackGame game, GraphicsDevice graphics, ContentManager content, BetFlowMode mode)
   ```

**Benefits:**
- Clear separation: mode selection vs rule configuration
- Intuitive UX (choose mode before playing)
- Aligns with user's stated vision

**Verification:**
- Click "Casino Mode" → game starts in betting mode
- Click "Practice Mode" → game skips betting, starts immediately
- Settings no longer shows Bet Mode

---

### 4.3 Add Hand Value Display Configuration
**Problem:** User stated "I don't really like" seeing hand values; should be configurable in Assistance section.

**Files Affected:**
- `src/MonoBlackjack.App/States/SettingsState.cs` (add setting)
- `src/MonoBlackjack.App/States/GameState.cs` (respect setting)

**Approach:**
1. Add "Show Hand Values" setting to Assistance section:
   ```csharp
   AddRow(SettingsSection.Assistance,
       "ShowHandValues",
       "Show Hand Values",
       [new SettingChoice("Yes", "True"), new SettingChoice("No", "False")],
       current);
   ```
2. Read setting in GameState and conditionally render values:
   ```csharp
   private bool _showHandValues;

   // In constructor:
   var settings = _settingsRepository.LoadSettings(_profileId);
   _showHandValues = settings.GetValueOrDefault("ShowHandValues", "True") == "True";

   // In DrawHandValues method:
   if (!_showHandValues) return;
   ```

**Benefits:**
- User control over visual clarity
- Cleaner look for experienced players
- Aligns with user feedback

**Verification:**
- Disable hand values in settings → values disappear
- Enable hand values → values reappear
- Default to enabled (current behavior)

---

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

## Implementation Order Summary

**Week 1: Foundation**
- Day 1-2: Phase 1.1 (GameConfig → GameRules refactor)
- Day 3: Phase 1.2 (Coordinate system standardization)
- Day 4: Phase 1.3 (Input validation)

**Week 2: Visual Quality**
- Day 1-2: Phase 2.1 (Font scaling)
- Day 2-3: Phase 2.2 (Button layout)
- Day 3-4: Phase 2.3 (Card alignment)

**Week 3: UX Flow**
- Day 1-2: Phase 3.1 (Pause menu)
- Day 2-3: Phase 3.2 (State history)

**Week 4: Settings & Stats**
- Day 1-2: Phase 4.1 (Settings sections)
- Day 2-3: Phase 4.2-4.3 (Game mode, hand values)
- Day 4: Phase 5.1 (Stats improvements)

**Week 5: Quality & Polish**
- Day 1-2: Phase 6 (Testing, cleanup)
- Day 3-5: Phase 7 (Future features as time permits)

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
