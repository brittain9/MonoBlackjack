# CodeX Instructions: Complete GameConfig → GameRules Refactor

## Current Refactor Policy (2026-02-17)
- Backward compatibility is not a goal for this codebase.
- Legacy code paths should be removed instead of preserved.
- Existing persisted data can be deleted/reset as needed during refactors.
- Prefer clean forward-only implementations over migration-heavy solutions.

## Context
You are completing a refactor that replaces mutable static `GameConfig` with an immutable `GameRules` record. Most of the work is done, but there are **63 remaining compilation errors** in the test file `GameRoundTests.cs` that need to be fixed.

## What Has Been Completed

### ✅ Core Layer (All Done)
- Created `GameRules.cs` - immutable record with factory methods
- Updated `GameConfig.cs` - made constants truly const
- Updated `Shoe.cs` - accepts penetrationPercent and useCryptographicShuffle via constructor
- Updated `Dealer.cs` - accepts hitsSoft17 via constructor
- Updated `Human.cs` - requires name and startingBank (no defaults)
- Updated `GameRound.cs` - accepts GameRules and uses it for all rule checks

### ✅ App Layer (All Done)
- Updated `BlackjackGame.cs` - manages CurrentRules property
- Updated `GameState.cs` - uses rules to create game objects
- Updated `SettingsState.cs` - loads/saves via GameRules
- Updated `StatsRecorder.cs` - accepts GameRules for rule fingerprinting

### ✅ Test Files (Mostly Done)
- `ShoeTests.cs` - ✅ Complete
- `DealerTests.cs` - ✅ Complete
- `GameConfigTests.cs` - ✅ Complete
- `GameRoundTests.cs` - ⚠️ **INCOMPLETE - 63 errors remaining**

## What You Need to Do

Fix the remaining 63 compilation errors in `/home/Alex/Projects/MonoBlackjack/tests/MonoBlackjack.Core.Tests/GameRoundTests.cs`

### Step 1: Read the File
```bash
# First, read the file to see the current state
```
Read: `/home/Alex/Projects/MonoBlackjack/tests/MonoBlackjack.Core.Tests/GameRoundTests.cs`

### Step 2: Fix Human Constructor Calls

**Problem:** Several tests still use the old `new Human(startingBank: X)` pattern.

**OLD PATTERN (Wrong):**
```csharp
var player = new Human(startingBank: 500m);
```

**NEW PATTERN (Correct):**
```csharp
var player = new Human("Player", 500m);
```

**Action:** Search for all instances of `new Human(startingBank:` and replace with the correct pattern.

**Locations to check:**
- Around lines 936-939
- Around lines 1034-1037
- Around lines 1065-1068
- Around lines 1089-1092
- Any other lines with `new Human(startingBank:`

**Example Edit:**
```csharp
// BEFORE
var player = new Human(startingBank: 500m);

// AFTER
var player = new Human("Player", 500m);
```

### Step 3: Fix GameRound Constructor Calls

**Problem:** Several tests still use the old GameRound constructor without the `GameRules` parameter.

**OLD PATTERN (Wrong):**
```csharp
var round = new GameRound(shoe, player, dealer, e => _events.Add(e));
```

**NEW PATTERN (Correct):**
```csharp
var round = new GameRound(shoe, player, dealer, GameRules.Standard, e => _events.Add(e));
```

**Action:** Search for all instances of `new GameRound(` with 4 parameters and add `GameRules.Standard,` as the 4th parameter (before the event publishing Action).

**Example Edit:**
```csharp
// BEFORE
var round = new GameRound(shoe, player, dealer, e => _events.Add(e));

// AFTER
var round = new GameRound(shoe, player, dealer, GameRules.Standard, e => _events.Add(e));
```

### Step 4: Handle Tests with Custom Rules

Some tests need custom rules (not GameRules.Standard). Look for tests that previously modified GameConfig and now need custom GameRules.

**Pattern to Look For:**
Tests that used to have try/finally blocks with GameConfig modifications.

**Example:**
If a test needs MaxSplits = 1:
```csharp
// Create custom rules
var rules = GameRules.Standard with { MaxSplits = 1 };

// Use in GameRound
var round = new GameRound(shoe, player, dealer, rules, e => _events.Add(e));
```

### Step 5: Verify All Constructors

Double-check that all object creations match these signatures:

**Shoe Constructor:**
```csharp
new Shoe(
    int deckCount,              // 1 or 6 typically
    int penetrationPercent,     // Usually 75
    bool useCryptographicShuffle, // Usually false in tests
    Random? rng                 // Optional, for seeded tests
)

// Examples:
new Shoe(1, 75, false)
new Shoe(1, 75, false, new Random(42))
new Shoe(6, 75, false, new Random(99))
```

**Dealer Constructor:**
```csharp
new Dealer(
    bool hitsSoft17  // true for H17, false for S17
)

// Examples:
new Dealer(false)  // Stand on soft 17 (S17)
new Dealer(true)   // Hit on soft 17 (H17)
```

**Human Constructor:**
```csharp
new Human(
    string name,        // Usually "Player"
    decimal startingBank // Amount in dollars
)

// Examples:
new Human("Player", 1000m)
new Human("Player", 500m)
```

**GameRound Constructor:**
```csharp
new GameRound(
    Shoe shoe,
    Human player,
    Dealer dealer,
    GameRules rules,           // NEW PARAMETER
    Action<GameEvent> publish
)

// Example:
new GameRound(shoe, player, dealer, GameRules.Standard, e => _events.Add(e))
```

### Step 6: Build and Verify

After making all edits:

```bash
# Build the solution
dotnet build MonoBlackjack.slnx

# If build succeeds, run all tests
dotnet test MonoBlackjack.slnx

# Expected result: All 94 tests should pass
```

If you still have errors:
1. Read the error message carefully
2. It will tell you the exact line number and what's wrong
3. Check that line and fix the constructor call
4. Repeat until all errors are gone

### Step 7: Verify Game Still Works

```bash
# Run the game to make sure it still works
dotnet run --project src/MonoBlackjack.App
```

Test in the game:
- Main menu appears
- Can start a game
- Can place bets
- Can hit/stand/split/double
- Can access settings
- Can view stats

## Common Mistakes to Avoid

### ❌ WRONG: Missing GameRules parameter
```csharp
var round = new GameRound(shoe, player, dealer, e => _events.Add(e));
```

### ✅ CORRECT: Include GameRules parameter
```csharp
var round = new GameRound(shoe, player, dealer, GameRules.Standard, e => _events.Add(e));
```

### ❌ WRONG: Using old Human constructor
```csharp
var player = new Human(startingBank: 1000m);
```

### ✅ CORRECT: Use new Human constructor
```csharp
var player = new Human("Player", 1000m);
```

### ❌ WRONG: Missing Shoe parameters
```csharp
var shoe = new Shoe(6, new Random(42));
```

### ✅ CORRECT: Include all Shoe parameters
```csharp
var shoe = new Shoe(6, 75, false, new Random(42));
```

## Systematic Approach (Recommended)

1. **Read GameRoundTests.cs completely** - Understand the file structure
2. **Search for "new Human(startingBank:"** - Fix all instances
3. **Search for "new GameRound("** - Fix all instances (add GameRules.Standard)
4. **Build** - Check remaining errors
5. **Read error messages** - They tell you exactly what's wrong and where
6. **Fix remaining errors one by one**
7. **Build again** - Repeat until clean build
8. **Run tests** - All 94 should pass
9. **Run game** - Verify it works

## Quick Reference: GameRules API

```csharp
// Standard rules (default Vegas rules)
var rules = GameRules.Standard;

// Custom rules (using 'with' expression)
var rules = GameRules.Standard with
{
    MaxSplits = 1,
    BlackjackPayout = 1.2m  // 6:5 payout
};

// From settings dictionary
var rules = GameRules.FromSettings(settingsDictionary);

// To settings dictionary
var dict = rules.ToSettingsDictionary();
```

## After Everything Works: Commit Message

Once all tests pass and the game works, create a commit with this message:

```
Refactor GameConfig from mutable static to immutable GameRules record

BREAKING CHANGE: Replaces mutable static GameConfig with immutable GameRules record pattern

Changes:
- Create GameRules.cs: Immutable record with validation and factory methods
- Update GameConfig.cs: Make constants truly const (BustNumber, AceExtraValue, InsurancePayout)
- Update Core layer: Shoe, Dealer, Human, GameRound now accept configuration via constructor injection
- Update App layer: BlackjackGame manages CurrentRules, GameState/SettingsState/StatsRecorder use injected rules
- Update tests: Remove [Collection("GameConfig")] attributes, remove try/finally restoration blocks, update all constructors

Benefits:
- Thread-safe (enables future multiplayer/async features)
- Testable without serialization workarounds
- Explicit dependencies (no hidden static coupling)
- Immutable (prevents mid-game rule changes)

Files changed:
- Core: GameRules.cs (new), GameConfig.cs, Shoe.cs, Dealer.cs, Human.cs, GameRound.cs
- App: BlackjackGame.cs, GameState.cs, SettingsState.cs, StatsRecorder.cs
- Tests: GameRoundTests.cs, ShoeTests.cs, DealerTests.cs, GameConfigTests.cs

Tests: All 94 tests pass

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

## Questions/Problems?

If you get stuck:
1. Read the compiler error message - it tells you exactly what's wrong
2. Look at the examples in this file
3. Check the helper method `CreateRound()` in GameRoundTests.cs - it shows the correct pattern
4. Compare working tests (ShoeTests.cs, DealerTests.cs) to see correct constructor usage

## Success Criteria

✅ `dotnet build MonoBlackjack.slnx` - No errors
✅ `dotnet test MonoBlackjack.slnx` - All 94 tests pass
✅ `dotnet run --project src/MonoBlackjack.App` - Game runs and works
✅ No [Collection("GameConfig")] attributes in test files
✅ No try/finally blocks restoring GameConfig in tests
✅ All constructors use new signatures (Shoe, Dealer, Human, GameRound)

Good luck! Take it step by step, and you'll get there.
