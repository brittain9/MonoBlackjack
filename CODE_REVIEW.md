# MonoBlackjack Code Review

Date: 2026-02-17  
Reviewer: Principal C# Game Development Review Pass

## Summary

- Overall score: **80/100**
- Build health: `dotnet build MonoBlackjack.slnx -warnaserror` passed
- Test health: `dotnet test MonoBlackjack.slnx` passed (`197/197`)

The project has a strong architectural foundation and good automated test coverage, but there are several correctness issues in gameplay analytics/rules behavior and some maintainability hotspots in the UI state layer.

## Detailed Findings

### High Severity

1. **`ResplitAces` rule is effectively non-functional**
   - `GameRound` automatically stands both hands immediately after splitting aces, which eliminates any chance to resplit regardless of `ResplitAces`.
   - References:
   - `src/MonoBlackjack.Core/GameRound.cs:373`
   - `src/MonoBlackjack.Core/GameRound.cs:395`
   - `src/MonoBlackjack.Core/GameRound.cs:427`
   - Impact:
   - Rule configuration can claim support for a player-favorable rule that cannot actually occur at runtime.
   - Recommendation:
   - Gate ace auto-stand behavior behind explicit rules semantics and allow ace resplit path when enabled.

2. **Card distribution analytics undercount dealer cards**
   - Dealer hole card is recorded as `FaceDown=true` and is never corrected when revealed.
   - Card distribution query excludes face-down cards, so revealed hole cards are missing from frequency stats.
   - References:
   - `src/MonoBlackjack.App/Stats/StatsRecorder.cs:68`
   - `src/MonoBlackjack.App/Stats/StatsRecorder.cs:84`
   - `src/MonoBlackjack.Data/Repositories/SqliteStatsRepository.cs:599`
   - Impact:
   - Distribution chart is statistically biased.
   - Recommendation:
   - Either persist hole-card reveal updates or include dealer revealed cards through an updated event mapping.

3. **Bust-rate metric is inflated by non-bust double-down losses**
   - Bust query treats many losing `Double` actions as busts if there is no `Stand`, which includes valid non-bust 1-card doubles that lose to dealer.
   - References:
   - `src/MonoBlackjack.Data/Repositories/SqliteStatsRepository.cs:348`
   - `src/MonoBlackjack.Data/Repositories/SqliteStatsRepository.cs:353`
   - `src/MonoBlackjack.Core/GameRound.cs:312`
   - `src/MonoBlackjack.Core/GameRound.cs:319`
   - Impact:
   - Dashboard bust rate over-reports bust events and can mislead strategy interpretation.
   - Recommendation:
   - Derive busts from explicit bust events/flags, or infer from final hand value instead of action class.

### Medium Severity

4. **Early-vs-late surrender semantics are not behaviorally differentiated**
   - Surrender eligibility is checked in `PlayerTurn`, after dealer-peek flow decisions.
   - `AllowEarlySurrender` and `AllowLateSurrender` are treated as effectively equivalent in round timing.
   - References:
   - `src/MonoBlackjack.Core/GameRound.cs:152`
   - `src/MonoBlackjack.Core/GameRound.cs:476`
   - Impact:
   - Rules fidelity gap relative to casino semantics.
   - Recommendation:
   - Split surrender windows by phase/timing and enforce each explicitly.

5. **Stats persistence failures are swallowed**
   - `StatsRecorder` catches all exceptions and only writes to console.
   - References:
   - `src/MonoBlackjack.App/Stats/StatsRecorder.cs:235`
   - Impact:
   - Silent data-loss risk in production runs and no user-visible indication of persistence failure.
   - Recommendation:
   - Route to structured logging and surface non-blocking UI warning or retry policy.

6. **Monetary values stored via `REAL`/double conversion**
   - Payout/bet values are cast to `double` before SQLite writes and read via `GetDouble`.
   - References:
   - `src/MonoBlackjack.Data/Repositories/SqliteStatsRepository.cs:69`
   - `src/MonoBlackjack.Data/Repositories/SqliteStatsRepository.cs:296`
   - Impact:
   - Potential precision drift over long sessions.
   - Recommendation:
   - Use integer minor units (cents) or decimal string persistence to preserve exact monetary arithmetic.

### Low Severity

7. **Hidden matrix-mode buttons still process input**
   - Stats matrix mode buttons are updated regardless of active tab visibility.
   - References:
   - `src/MonoBlackjack.App/States/StatsState.cs:141`
   - `src/MonoBlackjack.App/States/StatsState.cs:546`
   - Impact:
   - Potential hidden click behavior overlap.
   - Recommendation:
   - Update only controls visible in current tab.

## Strengths

- Clear solution layering (`Core`, `Data`, `App`) with good dependency direction.
- Domain validation and constrained config model are strong.
- Strong settings contract normalization and merge behavior.
- Event-driven round flow is explicit and testable.
- Good baseline test suite breadth across core/app/data.

References:
- `src/MonoBlackjack.Core/GameRules.cs:92`
- `src/MonoBlackjack.Core/Shoe.cs:26`
- `src/MonoBlackjack.Core/SettingsContract.cs:92`
- `src/MonoBlackjack.Core/SettingsContract.cs:121`

## Weaknesses

- Very large state classes are doing too much orchestration/render/input/persistence wiring in one place.
- Some configuration options are surfaced in settings but not consumed in runtime behavior.
- Analytics logic contains domain assumptions that drift from game logic truth.

References:
- `src/MonoBlackjack.App/States/GameState.cs`
- `src/MonoBlackjack.App/States/SettingsState.cs`
- `src/MonoBlackjack.App/States/StatsState.cs`
- `src/MonoBlackjack.Core/GameConfig.cs:32`
- `src/MonoBlackjack.Core/GameConfig.cs:40`
- `src/MonoBlackjack.Core/GameConfig.cs:41`
- `src/MonoBlackjack.Core/GameConfig.cs:42`

## Dead Code / Unused Surface

1. `GameRound.CanPlaceInsurance()` appears unused.
   - `src/MonoBlackjack.Core/GameRound.cs:467`
2. Legacy player/dealer API methods unused by current round orchestration.
   - `src/MonoBlackjack.Core/Players/Dealer.cs:21`
   - `src/MonoBlackjack.Core/Players/Dealer.cs:28`
   - `src/MonoBlackjack.Core/Players/PlayerBase.cs:20`
   - `src/MonoBlackjack.Core/Players/PlayerBase.cs:46`
3. `Button.Clicked` property is defined but unused.
   - `src/MonoBlackjack.App/Controls/Button.cs:27`

## Optimization Opportunities

1. **State decomposition**
   - Split `GameState` into dedicated collaborators:
   - Round UI presenter
   - Input controller
   - Pause/menu overlay controller
   - Card animation coordinator
2. **SQLite write efficiency**
   - Reuse prepared commands for per-row inserts in loops.
   - References:
   - `src/MonoBlackjack.Data/Repositories/SqliteStatsRepository.cs:86`
   - `src/MonoBlackjack.Data/Repositories/SqliteStatsRepository.cs:108`
   - `src/MonoBlackjack.Data/Repositories/SqliteStatsRepository.cs:132`
3. **Input polling**
   - Avoid repeated `Mouse.GetState()` in each button update; pass input snapshot down once per frame.
   - Reference:
   - `src/MonoBlackjack.App/Controls/Button.cs:142`
4. **Avoid repeated keybind reconstruction**
   - `SettingsState` rebuilds map for back-key checks each call.
   - Reference:
   - `src/MonoBlackjack.App/States/SettingsState.cs:809`

## Organization & Refactor Recommendations

1. Refactor large state classes toward feature modules and smaller testable units.
2. Add explicit domain test cases for:
   - `ResplitAces = true` behavior
   - early vs late surrender timing
   - analytics consistency with in-round events
3. Move stats derivation logic closer to event-truth model (event-sourced summaries or round-derived invariants).
4. Align README claims and runtime defaults for cryptographic shuffle configurability.

## Suggested Prioritized Fix Plan

1. Fix rules correctness:
   - resplit aces semantics
   - surrender timing semantics
2. Fix analytics correctness:
   - hole-card reveal inclusion
   - bust query correction
3. Improve observability:
   - robust stats error logging/reporting
4. Incremental maintainability:
   - extract `GameState` subcomponents
   - optimize hot path input/DB loops

