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
11. **Optimize SQLite insert loops**
12. **Remove dead/unused APIs**

## Suggested Execution Order

1. Ship all P0 items with tests.
2. Resolve settings/runtime parity and stats observability.
3. Do class decomposition and perf cleanup in incremental PR-sized slices.

- Make cool cursor/souunds in future.