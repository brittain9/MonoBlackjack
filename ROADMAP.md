# MonoBlackjack Roadmap

## Context

Clean arch + DDD + event-driven without enterprise distributed-systems patterns. Configurability is traded off for a better data dashboard — every config dimension should be a real casino rule variation worth filtering analytics by.

---

## Architecture (Simplified)

```
MonoBlackjack.Core     (domain: entities, events, game logic, port interfaces)
MonoBlackjack.Data     (infrastructure: SQLite repos, DatabaseManager, DTOs)
MonoBlackjack.App      (MonoGame UI, EventBus, StatsRecorder, states)
```

No Application project. Port interfaces in Core. Implementations in Data. Composition root in `BlackjackGame.cs`. Current event flow (`Action<GameEvent>` → `EventBus` → subscribers) stays as-is.

---

## Over-Engineering Cuts

1. **No Phase 0 gate** — fold useful pieces into phases that need them
2. **No Application project** — `GameRound` is the use case
3. **No enterprise event envelope** — skip `EventId`, `Version`, `OccurredAtUtc`, idempotency, at-least-once delivery
4. **No excessive value objects** — use `decimal` for money, `int` for IDs
5. **No bounded contexts** — use folders/namespaces
6. **No Monte Carlo CI suite or `IClock`**
7. **Hardcode non-blackjack config** — `BustNumber=21`, `AceExtraValue=10`, `InsurancePayout=2:1` become `const`

---

## Configuration → Analytics Alignment

**Configurable (real casino variations, stored per-round for analytics filtering):**
- `DealerHitsSoft17` (H17/S17), `BlackjackPayout` (3:2/6:5), `NumberOfDecks`, `SurrenderRule` (none/late/early), `DoubleAfterSplit`, `ResplitAces`, `MaxSplits`, `DoubleDownRestriction`, `PenetrationPercent`

**Hardcoded (not blackjack if you change them):**
- `BustNumber = 21`, `AceExtraValue = 10`, `InsurancePayout = 2:1`

**Rule fingerprint stored per round** — 4 key columns on Round table that affect outcome math:
- `BlackjackPayout`, `DealerHitsSoft17`, `DeckCount`, `SurrenderRule`

These are the dimensions worth filtering the dashboard by. Everything else is edge-case config that doesn't meaningfully shift aggregate stats.

---

## Data Dashboard Design

### Design Decisions
- **Auto-split sessions on rule change** — changing any outcome-affecting rule ends current session, starts new one. Data stays honest without user thinking about it.
- **Two tabs**: Overview and Analysis
- **Rules filter** — dropdown in top-right, defaults to "All Rounds" when rules are consistent, shows filter when mixed rulesets exist
- **Full strategy matrix** — outcome by hand value + action + dealer upcard (the feature that makes this a legitimate tool, not a win counter)

### Schema Addition

Add rule columns to `Round` table:

```sql
-- Add to Round table (these 4 columns are the "rule fingerprint")
BlackjackPayout  TEXT NOT NULL,     -- "3:2" or "6:5"
DealerHitsS17    INTEGER NOT NULL,  -- 0 or 1
DeckCount        INTEGER NOT NULL,  -- 1/2/4/6/8
SurrenderRule    TEXT NOT NULL       -- "none"/"late"/"early"
```

Add strategy tracking table:

```sql
-- One row per decision point (for strategy matrix)
CREATE TABLE Decision (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    RoundId         INTEGER NOT NULL REFERENCES Round(Id),
    HandIndex       INTEGER NOT NULL,
    PlayerValue     INTEGER NOT NULL,    -- hand value at decision time
    IsSoft          INTEGER NOT NULL,    -- soft hand (ace counted as 11)
    DealerUpcard    TEXT NOT NULL,        -- e.g. "6"
    Action          TEXT NOT NULL,        -- Hit/Stand/Double/Split/Surrender
    ResultOutcome   TEXT,                 -- Win/Lose/Push (filled after resolution)
    ResultPayout    REAL                  -- filled after resolution
);

CREATE INDEX IX_Decision_Action ON Decision(Action);
CREATE INDEX IX_Decision_PlayerValue ON Decision(PlayerValue);
CREATE INDEX IX_Decision_DealerUpcard ON Decision(DealerUpcard);
```

This table is what powers the strategy matrix. Each decision the player makes gets a row with the context (what they had, what the dealer showed, what they did) and the result. Queries like "when I hit on soft 16 vs dealer 10, what was my win rate?" become trivial SELECTs.

### Tab 1: Overview

The bankroll story + headline stats. What you see after a session.

```
┌─────────────────────────────────────────────────────────┐
│  OVERVIEW    [Analysis]              [Current Rules ▼]  │
│                                       6-deck, S17, 3:2  │
├─────────────────────────────────────────────────────────┤
│                                                         │
│   $1,247  net profit       512 rounds    8 sessions     │
│                                                         │
│  ┌─────────────────────────────────────────────────┐    │
│  │  Bankroll Over Time                             │    │
│  │  ╱─╲    ╱──╲   ╱╲  ╱────                       │    │
│  │ ╱   ╲──╱    ╲─╱  ╲╱         session markers ▼  │    │
│  │╱                                                │    │
│  └─────────────────────────────────────────────────┘    │
│                                                         │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────────┐    │
│  │  Win  43.2% │  │  BJ   4.6%  │  │  Bust 16.1%  │    │
│  │  Loss 49.1% │  │  (exp 4.8%) │  │              │    │
│  │  Push  7.7% │  │             │  │  Avg bet $25 │    │
│  └─────────────┘  └─────────────┘  └──────────────┘    │
│                                                         │
│  Current streak: W3    Best session: +$340              │
│  Biggest win: $500     Worst loss: -$200                │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

**Key stats:**
- Bankroll line chart with session boundary markers
- Net profit (big hero number)
- Rounds played, sessions played
- Win/Loss/Push rate
- Blackjack frequency vs expected
- Bust rate
- Average bet, biggest win, worst loss, current streak

### Tab 2: Analysis

The data nerd section. Three sub-sections, scrollable.

```
┌─────────────────────────────────────────────────────────┐
│  [Overview]    ANALYSIS              [Current Rules ▼]  │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  DEALER BUST RATE BY UPCARD                             │
│  ┌─────────────────────────────────────────────────┐    │
│  │  A   2   3   4   5   6   7   8   9   T         │    │
│  │  █   █   █   █   █   █   █   █   █   █         │    │
│  │  █   █   █  ██  ██  ██   █   █   █   █         │    │
│  │ 17% 35% 37% 40% 42% 42% 26% 24% 23% 21%       │    │
│  └─────────────────────────────────────────────────┘    │
│                                                         │
│  YOUR OUTCOMES BY HAND VALUE                            │
│  ┌─────────────────────────────────────────────────┐    │
│  │      12   13   14   15   16   17   18   19   20│    │
│  │ Win  ░░   ░░   ░░   ░▓   ░▓   ▓▓   ▓█   ██  ██│    │
│  │ Loss ██   ██   ██   █▓   █▓   ▓░   ░░   ░░  ░░│    │
│  │  (green = win rate, red = loss rate)            │    │
│  └─────────────────────────────────────────────────┘    │
│                                                         │
│  STRATEGY MATRIX                                        │
│  ┌─────────────────────────────────────────────────┐    │
│  │  [Hard ▼]           Dealer Upcard               │    │
│  │         2    3    4    5    6   ...  T    A      │    │
│  │  21   +92% +94% +93% +95% +96% ... +88% +81%   │    │
│  │  20   +88% +90% +91% +93% +92% ... +79% +72%   │    │
│  │  19   +71% +73% +75% +80% +82% ... +52% +43%   │    │
│  │  ...                                            │    │
│  │  12   -25% -22% -18% -12% -10% ... -30% -36%   │    │
│  │                                                 │    │
│  │  Action filter: [All ▼]  (Hit/Stand/Double)     │    │
│  │  Cell color: green=profitable, red=losing       │    │
│  │  Click cell for action breakdown                │    │
│  └─────────────────────────────────────────────────┘    │
│                                                         │
│  CARD DISTRIBUTION                                      │
│  ┌─────────────────────────────────────────────────┐    │
│  │  A  2  3  4  5  6  7  8  9  T  J  Q  K         │    │
│  │  Heatmap: actual frequency vs expected          │    │
│  └─────────────────────────────────────────────────┘    │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

**Strategy Matrix detail:**
- Toggle between Hard hands, Soft hands, Pairs
- Rows = player hand value, Columns = dealer upcard
- Cell value = net profit rate (win% - loss%) from YOUR actual play
- Color gradient: green (profitable) → red (losing)
- Action filter dropdown: show results for all actions, or filter to just Hit, just Stand, etc.
- Clicking a cell expands to show: "You had hard 16 vs dealer 10: Hit 23 times (won 8, lost 15), Stood 12 times (won 3, lost 9)"
- Powered by the `Decision` table — trivial queries per cell

**Card Distribution:**
- Rank frequency heatmap (actual vs expected)
- Fun for card-counting curious players
- Powered by existing `CardSeen` table

---

## Phase Order

### Phase 1: Dealer Peek + Insurance ✅

Fix deal flow timing — foundational correctness.
- **Files**: `GameRound.cs`, `RoundEvents.cs`, `GameRoundTests.cs`, `GameState.cs`

**Completed:**
- Added `Insurance` phase to `RoundPhase`
- New events: `InsuranceOffered`, `InsurancePlaced`, `InsuranceDeclined`, `InsuranceResult`, `DealerPeeked`
- `Deal()` restructured: Ace upcard → Insurance phase; 10-value upcard → silent peek; low upcard → straight to PlayerTurn
- `PlaceInsurance()` / `DeclineInsurance()` methods with correct peek-after-decision flow
- Insurance UI: Insurance/Decline buttons, "INSURANCE?" label, result feedback
- 11 new tests (68 total, all passing)

### Phase 2: Splitting

Most complex game feature, completes multi-hand.
- **Files**: `GameRound.cs`, `Hand.cs`, `RoundEvents.cs`, `GameState.cs`

### Phase 3: Double Down + Surrender Polish

Complete partial implementations, add UI buttons.
- **Files**: `GameRound.cs`, `GameConfig.cs`, `GameState.cs`

### Phase 4: Shoe Penetration

Cut card, reshuffle logic.
- **Files**: `Shoe.cs`, `GameConfig.cs`, `RoundEvents.cs`

### Phase 5: SQLite + Profiles + Stats Recording

Database foundation, all recording, profiles. One phase because they're tightly coupled.
- Add `Microsoft.Data.Sqlite` to Data project
- `DatabaseManager` with full schema (including `Decision` table and rule fingerprint columns on `Round`)
- Repository implementations in Data, interfaces in Core
- `StatsRecorder` subscribes to EventBus, batches one write per round
- Auto-split session when rule fingerprint changes
- Profile CRUD, active profile tracking
- Wire composition root in `BlackjackGame.cs`
- **Files**: `Data/DatabaseManager.cs`, `Data/Repositories/*.cs`, `Core/Ports/*.cs`, `App/Stats/StatsRecorder.cs`, `BlackjackGame.cs`

### Phase 6: Settings UI
- `SettingsState` with controls for all real casino rule variations
- Load/save through `SettingsRepository`
- `GameConfig.ApplySettings()` + `ToSettingsDictionary()`
- **Files**: `GameConfig.cs`, `App/States/SettingsState.cs`

### Phase 7: Stats Dashboard

Two-tab dashboard consuming the data layer.
- **Overview tab**: bankroll chart, win rates, headline stats
- **Analysis tab**: dealer bust by upcard, outcomes by hand value, strategy matrix, card distribution
- Rules filter dropdown (top-right, context-aware)
- **Files**: `App/States/StatsState.cs` (flesh out existing stub), new rendering components for charts/tables

### Phase 8: Dev Overlay

Power tool, lowest priority.
- **Files**: `App/DevOverlay.cs`, `App/IDevContext.cs`

---

## Verification

- `dotnet build` + `dotnet test` after each phase
- Phase 5: Play rounds programmatically → verify all DB records (Round, HandResult, CardSeen, Decision)
- Phase 7: Play scripted session → verify Overview tab totals match raw queries, strategy matrix cells match Decision table aggregations
- Manual: change rules mid-play → verify session auto-splits → verify filter correctly separates data
