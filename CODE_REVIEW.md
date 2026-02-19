# MonoBlackjack Code Review

Date: 2026-02-19  
Reviewer: Senior C# gameplay/data review pass

## Scope
- Core blackjack correctness (`MonoBlackjack.Core`)
- Analytics capture/persistence integrity (`MonoBlackjack.App` + `MonoBlackjack.Data`)
- App-state integration risk (`GameState`, `StatsState`)

## Baseline Health
- `dotnet build MonoBlackjack.slnx` passed
- `dotnet test MonoBlackjack.slnx` passed (`216/216`)

## Findings (Ordered by Severity)

### High

1. **Early surrender is unreachable during insurance flow in UI**
   - Core permits surrender in insurance phase when rules allow early surrender (`src/MonoBlackjack.Core/GameRound.cs:355`, `src/MonoBlackjack.Core/GameRound.cs:495`).
   - App insurance flow only routes accept/decline and never surrender (`src/MonoBlackjack.App/States/GameState.cs:621`, `src/MonoBlackjack.App/States/GameState.cs:745`, `src/MonoBlackjack.App/States/Game/GameInputController.cs:85`).
   - Impact: rule contract is partially implemented in practice.

2. **Pairs strategy matrix is statistically unreliable for training decisions**
   - Decision capture links results to same hand index only (`src/MonoBlackjack.App/Stats/StatsRecorder.cs:245`).
   - Pairs matrix query filters on existence of any split action for that hand (`src/MonoBlackjack.Data/Repositories/SqliteStatsRepository.cs:542`).
   - This excludes unsplit-pair states and mixes in post-split non-pair decisions.
   - Impact: matrix can mislead users on pair play quality.

### Medium

3. **Dealer bust-by-upcard metric uses a sampled denominator**
   - Query uses `Decision` rows as source (`src/MonoBlackjack.Data/Repositories/SqliteStatsRepository.cs:453`).
   - Rounds with no player decision (naturals/fast resolves) are underrepresented.
   - Impact: bust percentages are not true per-upcard bust rates.

4. **Settings save can leave runtime/persisted state inconsistent on failure**
   - Settings are applied before repository save (`src/MonoBlackjack.App/States/SettingsState.cs:593`, `src/MonoBlackjack.App/States/SettingsState.cs:594`).
   - No exception handling in save path.
   - Impact: app may run with settings that were not persisted, or throw out of UI flow.

### Low

5. **Some core tests are vacuous under random-path skips**
   - Several tests early-return when they do not find the expected round phase (`tests/MonoBlackjack.Core.Tests/GameRoundTests.cs:293`, `tests/MonoBlackjack.Core.Tests/GameRoundTests.cs:309`, `tests/MonoBlackjack.Core.Tests/GameRoundTests.cs:461`).
   - Impact: lower regression detection power.

6. **Legacy migration complexity remains despite no-compat policy**
   - Data startup includes compatibility migration scaffolding (`src/MonoBlackjack.Data/DatabaseManager.cs:143`, `src/MonoBlackjack.Data/DatabaseManager.cs:392`).
   - Impact: maintainability overhead and policy mismatch.

## Game-First Recommendation
1. Fix gameplay-critical rule/UI mismatches first (early surrender accessibility).
2. Mark stats page explicitly experimental in UI/docs (done separately in this change set).
3. Defer deep analytics redesign until gameplay behavior is stable and validated.
4. When redesign begins, define authoritative metrics first (sampling rules, denominators, action attribution).
