# MonoBlackjack: Production Roadmap

## Context

MonoBlackjack is a casino-grade blackjack simulator with clean architecture, strong domain modeling, and SQLite-backed analytics.

This roadmap is the current execution plan. Completed milestones are preserved; active work starts in Phase 6.

**Current State (as of 2026-02-17):**
- Phases 1-5 completed
- Core/Data/App architecture in place
- Event-driven round tracking and stats persistence working
- Remaining focus is quality, bug fixing, UX clarity, and polish

**Core Engineering Policy:**
- No backward compatibility paths for old data/contracts
- Keep contracts clean and current; remove legacy branches and dead compatibility code

---

## Phase 1: Critical Architecture Fixes (Foundation) (COMPLETE)
### 1.1 Refactor GameConfig -> Immutable GameRules Record (COMPLETE)
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

## Phase 5: Stats Dashboard Improvements (COMPLETE)

---

## Phase 6: Testing, Quality, and Gameplay Coherence (ACTIVE)
**Priority:** P1 - Current
**Estimated Effort:** 8-14 hours
**Goal:** Eliminate known gameplay/layout defects, tighten UX behavior, and lock in quality with targeted tests.

### 6.1 Split Hand Layout Bug + Regression Tests
**Problem:** Split hands still overlap visually in some playthroughs.

**Approach:**
1. Add or refine split-layout calculation logic in `GameState`/layout helper.
2. Add deterministic tests for split layout spacing and card bounds.
3. Cover edge cases: two split hands, 5+ cards, varying resolutions.

**Verification:**
- No overlap between split hand cards in tested viewport set.
- New tests fail on overlap regressions.

### 6.2 App-Layer Layout/Smoke Test Expansion
**Problem:** Existing app tests do not fully guard layout regressions.

**Approach:**
1. Expand `tests/MonoBlackjack.App.Tests` with focused tests for:
   - action button visibility/state by round state
   - split hand card positioning constraints
   - resize stability (small, medium, large viewports)
2. Keep tests deterministic and fast.

**Verification:**
- `dotnet test MonoBlackjack.slnx` passes with new app-level tests.

### 6.3 Freeplay Rules Contract (Action Set + Naming)
**Problem:** Freeplay behavior and naming need to be explicit and consistent.

**Approach:**
1. Rename UI label from "Practice Mode" to "Freeplay".
2. Lock a clear Freeplay action contract and document it:
   - Split allowed
   - Double down allowed in Freeplay (same one-card rule semantics as betting mode)
3. Add tests for mode-specific action availability and behavior.

**Verification:**
- Mode naming is consistent in menu/UI/tests/docs.
- Freeplay action set is deterministic and tested.

### 6.4 Pause Menu UX Rework
**Problem:** Pause menu works functionally but is not clear/polished.

**Approach:**
1. Redesign pause menu hierarchy and copy for clarity.
2. Improve visual grouping, spacing, and focused action flow.
3. Keep state transitions and back behavior predictable.

**Verification:**
- Pause menu is readable and obvious at 1280x720, 1920x1080, and 4K.
- Navigation path is unambiguous (Resume/Settings/Menu).

### 6.5 Keybinds System Completion (Editable + Applied)
**Problem:** Keybind settings exist as placeholders but are not fully wired into gameplay.

**Approach:**
1. Implement keybind resolution/validation from settings.
2. Apply bindings across gameplay, pause, and round-advance controls.
3. Add conflict handling (duplicate keys) and defaults reset.

**Verification:**
- Edited keybinds are persisted and used at runtime.
- Critical actions are fully playable without mouse.

### 6.6 Remove Roadmap Labels from Code Surface
**Problem:** Internal roadmap "phase" labels leaked into code/test naming.

**Approach:**
1. Remove phase-number naming/comments from source and tests.
2. Keep phase naming only in planning markdown docs.

**Status:**
- Test file/class names already cleaned (`StateAndSettingsTests`, `StatsDashboardTests`).

### 6.7 Enforce Clean Contract-Only Data Rules
**Problem:** Prior attempts introduced backward-compatible behavior that conflicts with current direction.

**Approach:**
1. Review settings/data code for fallback parsing or legacy keys.
2. Remove compatibility branches; keep only current supported schema/settings contract.
3. Add tests that assert strict accepted keys/values.

**Verification:**
- Runtime paths do not include legacy fallback logic.
- Persistence only reads/writes current contract.

---

## Phase 7: Polish Pass (Game Feel & Presentation)
**Priority:** P2 - Next
**Estimated Effort:** 12-20 hours
**Dependencies:** Phase 6 complete
**Goal:** Make the game feel complete and production-polished.

### 7.1 Audio System (SFX + Music)
- Add table/gameplay SFX (deal, chip, click, win/loss cues)
- Add background music support with volume controls
- Add mute and independent SFX/music levels

### 7.2 Skins and Themes
- Add theme system for table/background/card-back style
- Keep theme contract clean and explicit (no legacy theme migration paths)
- Add settings UI for theme selection

### 7.3 Betting Menu UX Rework
- Redesign betting screen hierarchy (bet amount, controls, primary action)
- Improve chip/bet control affordances and readable bankroll feedback
- Ensure quick-play flow while preserving clarity

### 7.4 Round Transition Control (Manual Next-Round Option)
- Add explicit post-round state with clear "Next Round" action
- Default to Enter/Space as fast continue
- Provide optional auto-advance behavior as a configurable setting

### 7.5 Animation Polish
- Improve dealing/transitions with consistent timing curves
- Add feedback animations for key actions/results
- Implement dealer peek animation flow for hole-card check clarity
- Keep animation settings performant and resolution-safe

### 7.6 UI Clarity Sweep
- Final pass on visual hierarchy and readability across screens
- Improve microcopy and call-to-action clarity
- Ensure consistent spacing and typography across states

---

## Future Roadmap (Post-Phase 7, Not Now)
These are intentionally deferred:
- Recommendation/basic-strategy assistant
- Card counting practice mode
- AI opponent at table
- Multiplayer
- Mobile/web versions
- Accessibility expansion (screen reader, colorblind variants, etc.)
- Session import/export
- Advanced analytics extensions

---

## Success Metrics

**Phase 6 Done When:**
- Split-hand overlap bug is fixed and covered by automated tests
- Freeplay naming/action contract is finalized and tested
- Pause menu clarity is materially improved
- Editable keybinds are fully functional
- No roadmap-phase labels remain in code/test naming
- No backward-compatibility branches remain in settings/data runtime paths

**Phase 7 Done When:**
- Audio is integrated and configurable
- Theme system is functional and stable
- Betting menu UX is materially improved
- Round transition behavior is configurable and keyboard-friendly
- Animations improve readability/game feel without hurting responsiveness
- Overall UI feels coherent and release-ready

---

## Risks & Mitigations

**Risk:** UI polish introduces layout regressions
**Mitigation:** Add focused layout regression tests before/alongside changes

**Risk:** Action-rule changes create mode inconsistency
**Mitigation:** Define explicit mode contracts and test them end-to-end

**Risk:** Strict contract cleanup breaks stale local data
**Mitigation:** Accept break as intentional; document clean-contract policy clearly

**Risk:** Audio/theme additions increase complexity
**Mitigation:** Introduce simple contracts first, then extend incrementally
