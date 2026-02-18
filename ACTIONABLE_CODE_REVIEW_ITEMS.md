# Actionable Code Review Items

Date: 2026-02-18  
Status: Ready for implementation

## P0 - Correctness First (COMPLETE)

1. **Fix `ResplitAces` behavior mismatch**
2. **Implement real early vs late surrender timing**
3. **Fix dealer hole-card analytics undercount**
4. **Correct bust-rate computation**

## P1 - Product Integrity + Architecture (COMPLETE)

5. **Wire or remove currently dead settings**
6. **Replace swallowed stats persistence errors**
7. **Eliminate money precision drift risk**
8. **Decompose `GameState` orchestration class**
9. **Decompose `StatsState` rendering and interaction**

## P2 - Performance + Cleanup

10. **Remove per-button mouse polling hot path**
    - Problem: each button polls mouse state independently.
    - Actions:
      - Build per-frame input snapshot and pass into button updates.
      - Update state classes to use centralized input snapshot.
    - Files:
      - `src/MonoBlackjack.App/Controls/Button.cs`
      - `src/MonoBlackjack.App/States/GameState.cs`
      - `src/MonoBlackjack.App/States/StatsState.cs`
      - `src/MonoBlackjack.App/States/SettingsState.cs`
      - `src/MonoBlackjack.App/States/MenuState.cs`

11. **Optimize SQLite insert loops**
    - Problem: per-row command allocation in stats inserts.
    - Actions:
      - Reuse prepared commands for hand results, cards seen, and decisions.
      - Keep transaction semantics unchanged.
    - Files:
      - `src/MonoBlackjack.Data/Repositories/SqliteStatsRepository.cs`
      - `tests/MonoBlackjack.Data.Tests/SqliteRepositoriesTests.cs`

12. **Remove dead/unused APIs**
    - Problem: unused code surface increases maintenance overhead.
    - Actions:
      - Remove or integrate `GameRound.CanPlaceInsurance()`.
      - Remove or implement `Button.Clicked`.
    - Files:
      - `src/MonoBlackjack.Core/GameRound.cs`
      - `src/MonoBlackjack.App/Controls/Button.cs`

## Suggested Execution Order

1. Ship all P0 items with tests.
2. Resolve settings/runtime parity and stats observability.
3. Do class decomposition and perf cleanup in incremental PR-sized slices.
