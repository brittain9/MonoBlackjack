# MonoBlackjack Review Remediation Work Spec

Date: 2026-02-19  
Source of findings: `CODE_REVIEW.md` (review pass dated 2026-02-19)  
Priority mode: Gameplay and rule correctness first, analytics integrity second, cleanup last

## Purpose
This document is the execution contract for fixing all current code review findings with clear implementation scope and acceptance criteria.

## Scope
- In scope: Findings F1-F6 listed in `CODE_REVIEW.md`
- In scope: Test updates required to make each fix verifiable
- Out of scope: Broad stats dashboard redesign beyond finding-specific corrections

## Constraints
- Keep `Core` as domain-rule authority; do not duplicate rule logic in `App`
- Preserve clean `Core -> Data/App` boundaries
- No backward compatibility scaffolding or legacy fallbacks
- Prefer deterministic tests over random-path tests

## Work Items

## F1 - Early surrender unreachable during insurance flow (High)
### Problem
Core allows surrender during insurance phase when early surrender is enabled, but App insurance handling currently only supports accept/decline insurance actions.

### Implementation specification
- Update insurance-phase action availability in `GameState` to include surrender when the current `GameRound` state allows it.
- Update insurance-phase input routing in `GameInputController` so surrender input dispatches to the same core action path used outside insurance flow.
- Ensure button visibility/enablement and keyboard routing stay consistent for insurance interactions.
- Keep rule checks in Core; App should only reflect available actions exposed by round state.

### Acceptance criteria
- Testable: With early surrender enabled and dealer Ace upcard, insurance phase offers surrender action and selecting it resolves as surrender.
- Testable: With early surrender disabled, insurance phase does not expose surrender.
- Narrative: Insurance-phase UI cannot hide a legal action that Core allows.

### Required tests
- App test validating insurance action set when early surrender is enabled.
- App test validating insurance action set when early surrender is disabled.
- End-to-end state test for surrender path during insurance phase.

### Out of scope
- Redesigning insurance UX layout beyond enabling the missing action.

## F2 - Pairs strategy matrix attribution is unreliable (High)
### Problem
Current pair analytics sample selection excludes unsplit pair opportunities and can include post-split non-pair decisions, producing misleading strategy guidance.

### Implementation specification
- Redefine decision capture to mark initial pair-opportunity context at the moment a pair decision is made.
- Ensure unsplit pair decisions remain in the sample set.
- Exclude post-split non-pair follow-up decisions from pair-opportunity metrics.
- Update repository query logic so matrix denominator and numerator are based only on true pair-opportunity events.

### Acceptance criteria
- Testable: Unsplit pair decisions are counted in pair matrix samples.
- Testable: Post-split non-pair decisions are excluded from pair matrix samples.
- Testable: Matrix output for controlled fixtures matches expected pair-opportunity outcomes.
- Narrative: Pair matrix semantics represent pair decision quality, not generic hand outcome noise.

### Required tests
- `StatsRecorder` tests for pair-opportunity tagging and persistence.
- `SqliteStatsRepository` tests for corrected pair matrix filtering/aggregation.
- Regression fixture test comparing known expected matrix values.

### Out of scope
- Full redesign of stats UI presentation.

## F3 - Dealer bust-by-upcard denominator is sampled from decisions (Medium)
### Problem
Bust-by-upcard metric currently uses decision rows, undercounting rounds that resolve without player decisions (for example naturals and fast resolves).

### Implementation specification
- Rebuild dealer bust-by-upcard query using round-level population for each dealer upcard.
- Include eligible rounds regardless of whether a decision row exists.
- Keep metric naming and comments aligned with denominator semantics.

### Acceptance criteria
- Testable: Controlled data including no-decision rounds yields correct per-upcard bust percentage.
- Testable: Query results are stable when adding rounds with naturals/no player actions.
- Narrative: Metric semantics are explicit and match implementation.

### Required tests
- Repository tests with synthetic round datasets covering decision and no-decision outcomes.
- Regression test for expected percentages by upcard.

### Out of scope
- Additional dealer metrics beyond bust-by-upcard correction.

## F4 - Settings save can desync runtime and persisted state on failure (Medium)
### Problem
Settings are currently applied before save; a persistence failure can leave runtime state changed while stored settings remain old, or throw through UI flow.

### Implementation specification
- Change settings flow to persist first, then apply runtime settings only after successful save.
- Add exception handling in save path to keep state consistent on failure.
- Define failure behavior: keep previous runtime settings, surface error feedback, and log details.

### Acceptance criteria
- Testable: Save failure does not crash settings flow.
- Testable: Runtime settings remain unchanged when persistence fails.
- Testable: Successful save updates both persisted and runtime settings.
- Narrative: User-visible behavior is consistent and recoverable after save errors.

### Required tests
- `SettingsState` test for repository failure path.
- `SettingsState` test for successful save/apply path.
- Optional logger/assertion test that failure is recorded.

### Out of scope
- New settings storage backend or retry queue system.

## F5 - Random-path skips create vacuous core tests (Low)
### Problem
Some tests return early when target state is not reached under random flow, allowing tests to pass without asserting behavior.

### Implementation specification
- Replace random setup with deterministic hand/shoe setup to guarantee target phase.
- Remove early-return pass paths in affected tests.
- Keep each test focused on one domain behavior with explicit preconditions.

### Acceptance criteria
- Testable: Affected tests always execute assertions.
- Testable: Repeated runs do not produce pass-by-skip behavior.
- Narrative: Test intent and setup are explicit and deterministic.

### Required tests
- Refactor affected cases in `tests/MonoBlackjack.Core.Tests/GameRoundTests.cs`.
- Add helper setup methods if needed to build deterministic rounds.

### Out of scope
- Full rewrite of all core tests not implicated by this finding.

## F6 - Legacy migration scaffolding conflicts with no-compat policy (Low)
### Problem
Data startup still carries compatibility migration complexity even though project policy disallows backward compatibility work.

### Implementation specification
- Remove legacy compatibility migration branches from `DatabaseManager`.
- Keep only current-contract schema ensure/migration path.
- Update related comments/docs to reflect current policy.

### Acceptance criteria
- Testable: Fresh database initialization succeeds.
- Testable: Current repository tests pass with simplified migration path.
- Narrative: Data layer code matches no-backward-compatibility policy.

### Required tests
- Data bootstrap test for current schema path.
- Existing `MonoBlackjack.Data.Tests` suite green after cleanup.

### Out of scope
- Supporting historical/legacy database versions.

## Cross-Cutting Validation
- `dotnet build MonoBlackjack.slnx` passes.
- `dotnet test MonoBlackjack.slnx` passes.
- Updated tests for each finding are present and deterministic.
- Manual gameplay sanity check confirms surrender accessibility behavior under early-surrender insurance scenario.

## Execution Order
1. F1 gameplay rule/UI parity
2. F2 pair analytics attribution correctness
3. F3 dealer bust denominator correction
4. F4 settings save consistency and resilience
5. F5 deterministic core test cleanup
6. F6 migration policy alignment cleanup

## Definition of Done
- All six findings are implemented and verified against acceptance criteria above.
- No compatibility scaffolding is introduced.
- All relevant automated tests pass.
- Any changed contracts are reflected in corresponding tests and comments.

