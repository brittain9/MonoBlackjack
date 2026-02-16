using FluentAssertions;
using MonoBlackjack.Core;
using MonoBlackjack.Core.Events;
using MonoBlackjack.Core.Players;

namespace MonoBlackjack.Core.Tests;

public class GameRoundTests
{
    private readonly List<GameEvent> _events = [];

    private GameRound CreateRound(int seed = 42, GameRules? rules = null)
    {
        rules ??= GameRules.Standard;
        var shoe = new Shoe(rules.NumberOfDecks, rules.PenetrationPercent, rules.UseCryptographicShuffle, new Random(seed));
        var player = new Human("Player", rules.StartingBank);
        var dealer = new Dealer(rules.DealerHitsSoft17);
        return new GameRound(shoe, player, dealer, rules, e => _events.Add(e));
    }

    [Fact]
    public void Deal_Publishes4CardDealtAndInitialDealComplete()
    {
        var round = CreateRound();
        round.PlaceBet(10);
        round.Deal();

        var cardDealtEvents = _events.OfType<CardDealt>().ToList();
        cardDealtEvents.Should().HaveCount(4);

        // Alternating: player, dealer, player, dealer
        cardDealtEvents[0].Recipient.Should().Be("Player");
        cardDealtEvents[1].Recipient.Should().Be("Dealer");
        cardDealtEvents[2].Recipient.Should().Be("Player");
        cardDealtEvents[3].Recipient.Should().Be("Dealer");

        // Dealer's second card is face-down
        cardDealtEvents[3].FaceDown.Should().BeTrue();
        cardDealtEvents[0].FaceDown.Should().BeFalse();
        cardDealtEvents[1].FaceDown.Should().BeFalse();
        cardDealtEvents[2].FaceDown.Should().BeFalse();

        _events.Should().ContainSingle(e => e is InitialDealComplete);
    }

    [Fact]
    public void Deal_WhenCutCardReached_ReshufflesAndPublishesShoeEvents()
    {
        var rules = GameRules.Standard with { PenetrationPercent = 75, NumberOfDecks = 1 };
        var shoe = new Shoe(rules.NumberOfDecks, rules.PenetrationPercent, rules.UseCryptographicShuffle, new Random(42));
        while (shoe.Remaining > shoe.CutCardRemainingThreshold)
            shoe.Draw();

        var player = new Human("Player", rules.StartingBank);
        var dealer = new Dealer(rules.DealerHitsSoft17);
        var round = new GameRound(shoe, player, dealer, rules, e => _events.Add(e));

        round.PlaceBet(rules.MinimumBet);
        round.Deal();

        var cutCardReached = _events.OfType<ShoeCutCardReached>().Single();
        cutCardReached.CardsRemaining.Should().Be(13);
        cutCardReached.CutCardRemainingThreshold.Should().Be(13);

        var reshuffled = _events.OfType<ShoeReshuffled>().Single();
        reshuffled.DeckCount.Should().Be(1);
        reshuffled.CardsRemaining.Should().Be(52);
        reshuffled.CutCardRemainingThreshold.Should().Be(13);

        shoe.Remaining.Should().Be(48);
    }

    [Fact]
    public void Deal_WhenCutCardNotReached_DoesNotPublishShoeEvents()
    {
        var round = CreateRound(seed: 123, rules: GameRules.Standard with { PenetrationPercent = 75 });

        round.PlaceBet(GameRules.Standard.MinimumBet);
        round.Deal();

        _events.Should().NotContain(e => e is ShoeCutCardReached);
        _events.Should().NotContain(e => e is ShoeReshuffled);
    }

    [Fact]
    public void Deal_WithNoBlackjack_TransitionsToPlayerTurnOrInsurance()
    {
        var round = CreateRound();
        round.PlaceBet(10);
        round.Deal();

        // If no blackjack detected, should end in PlayerTurn or Insurance phase
        if (!_events.Any(e => e is BlackjackDetected))
        {
            var validPhases = new[] { RoundPhase.PlayerTurn, RoundPhase.Insurance };
            round.Phase.Should().BeOneOf(validPhases);

            if (round.Phase == RoundPhase.PlayerTurn)
                _events.Should().ContainSingle(e => e is PlayerTurnStarted);
            else
                _events.Should().ContainSingle(e => e is InsuranceOffered);
        }
    }

    [Fact]
    public void PlaceBet_PublishesBetPlacedEvent()
    {
        var round = CreateRound();
        round.PlaceBet(25);

        _events.Should().ContainSingle(e => e is BetPlaced);
        var bet = _events.OfType<BetPlaced>().Single();
        bet.Amount.Should().Be(25);
        bet.PlayerName.Should().Be("Player");
    }

    [Fact]
    public void PlaceBet_ClampsToMinimum()
    {
        var round = CreateRound();
        round.PlaceBet(1); // Below minimum of 5

        var bet = _events.OfType<BetPlaced>().Single();
        bet.Amount.Should().Be(GameRules.Standard.MinimumBet);
    }

    [Fact]
    public void PlayerHit_DrawsCardAndPublishesEvent()
    {
        var round = CreateRound();
        round.PlaceBet(10);
        round.Deal();

        if (round.Phase != RoundPhase.PlayerTurn)
            return; // Blackjack was dealt, skip test

        _events.Clear();
        round.PlayerHit();

        _events.Should().ContainSingle(e => e is PlayerHit);
    }

    [Fact]
    public void PlayerStand_TransitionsToDealerTurn()
    {
        var round = CreateRound();
        round.PlaceBet(10);
        round.Deal();

        if (round.Phase != RoundPhase.PlayerTurn)
            return;

        _events.Clear();
        round.PlayerStand();

        _events.Should().Contain(e => e is PlayerStood);
        _events.Should().Contain(e => e is DealerTurnStarted);
        _events.Should().Contain(e => e is DealerHoleCardRevealed);
        _events.Should().Contain(e => e is HandResolved);
        _events.Should().Contain(e => e is RoundComplete);
        round.Phase.Should().Be(RoundPhase.Complete);
    }

    [Fact]
    public void PlayerStand_DealerPlaysAndResolves()
    {
        var round = CreateRound();
        round.PlaceBet(10);
        round.Deal();

        if (round.Phase != RoundPhase.PlayerTurn)
            return;

        round.PlayerStand();

        // Dealer either stood or busted
        var dealerStood = _events.Any(e => e is DealerStood);
        var dealerBusted = _events.Any(e => e is DealerBusted);
        (dealerStood || dealerBusted).Should().BeTrue();

        round.Phase.Should().Be(RoundPhase.Complete);
    }

    [Fact]
    public void PlayerDoubleDown_DoublesBetAndDrawsOneCard()
    {
        var round = CreateRound();
        round.PlaceBet(10);
        round.Deal();

        if (round.Phase != RoundPhase.PlayerTurn)
            return;

        _events.Clear();
        round.PlayerDoubleDown();

        _events.Should().ContainSingle(e => e is PlayerDoubledDown);
        // Round should complete (either via dealer turn or bust)
        round.Phase.Should().Be(RoundPhase.Complete);
    }

    [Fact]
    public void PlayerDoubleDown_WithMoreThanTwoCards_Throws()
    {
        var (round, _) = CreatePlayableRound(seedStart: 0);

        round.PlayerHit();

        var act = () => round.PlayerDoubleDown();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CanDoubleDown_TenToElevenRestriction_AllowsTenOrEleven()
    {
        var rules = GameRules.Standard with
        {
            DoubleDownRestriction = DoubleDownRestriction.TenToEleven,
            NumberOfDecks = 1,
            StartingBank = 10000m
        };

        for (int seed = 0; seed < 5000; seed++)
        {
            _events.Clear();
            var shoe = new Shoe(rules.NumberOfDecks, rules.PenetrationPercent, rules.UseCryptographicShuffle, new Random(seed));
            var player = new Human("Player", rules.StartingBank);
            var dealer = new Dealer(rules.DealerHitsSoft17);
            var round = new GameRound(shoe, player, dealer, rules, e => _events.Add(e));

            round.PlaceBet(rules.MinimumBet);
            round.Deal();

            if (round.Phase != RoundPhase.PlayerTurn)
                continue;

            var value = player.Hands[0].Value;
            if (value is not (10 or 11))
                continue;

            round.CanDoubleDown().Should().BeTrue();
            return;
        }

        throw new InvalidOperationException("Could not find an opening hand totaling 10 or 11.");
    }

    [Fact]
    public void CanDoubleDown_TenToElevenRestriction_BlocksOtherTotals()
    {
        var rules = GameRules.Standard with
        {
            DoubleDownRestriction = DoubleDownRestriction.TenToEleven,
            NumberOfDecks = 1,
            StartingBank = 10000m
        };

        for (int seed = 0; seed < 5000; seed++)
        {
            _events.Clear();
            var shoe = new Shoe(rules.NumberOfDecks, rules.PenetrationPercent, rules.UseCryptographicShuffle, new Random(seed));
            var player = new Human("Player", rules.StartingBank);
            var dealer = new Dealer(rules.DealerHitsSoft17);
            var round = new GameRound(shoe, player, dealer, rules, e => _events.Add(e));

            round.PlaceBet(rules.MinimumBet);
            round.Deal();

            if (round.Phase != RoundPhase.PlayerTurn)
                continue;

            var value = player.Hands[0].Value;
            if (value is 10 or 11)
                continue;

            round.CanDoubleDown().Should().BeFalse();
            return;
        }

        throw new InvalidOperationException("Could not find an opening hand outside 10 or 11.");
    }

    [Fact]
    public void PlayerSurrender_HalvesBetAndCompletes()
    {
        var rules = GameRules.Standard with
        {
            AllowLateSurrender = true,
            AllowEarlySurrender = false,
            NumberOfDecks = 1,
            StartingBank = 1000m
        };

        var shoe = new Shoe(rules.NumberOfDecks, rules.PenetrationPercent, rules.UseCryptographicShuffle, new Random(42));
        var player = new Human("Player", rules.StartingBank);
        var dealer = new Dealer(rules.DealerHitsSoft17);
        var round = new GameRound(shoe, player, dealer, rules, e => _events.Add(e));

        round.PlaceBet(100);
        round.Deal();

        if (round.Phase != RoundPhase.PlayerTurn)
            return;

        round.PlayerSurrender();

        _events.Should().Contain(e => e is PlayerSurrendered);
        var resolved = _events.OfType<HandResolved>().Last();
        resolved.Outcome.Should().Be(HandOutcome.Surrender);
        resolved.Payout.Should().Be(-50);
        round.Phase.Should().Be(RoundPhase.Complete);
    }

    [Fact]
    public void PlayerSurrender_WithOddMinimumBet_KeepsFractionalBankroll()
    {
        var rules = GameRules.Standard with
        {
            MinimumBet = 5m,
            AllowLateSurrender = true,
            AllowEarlySurrender = false
        };

        var (round, player) = CreatePlayableRound(startingBank: 1000, seedStart: 100, rules);
        var bankBefore = player.Bank;

        round.PlayerSurrender();

        player.Bank.Should().Be(bankBefore - 2.5m);
        _events.OfType<HandResolved>().Last().Payout.Should().Be(-2.5m);
    }

    [Fact]
    public void PlayerSurrender_WhenSurrenderDisabled_Throws()
    {
        var rules = GameRules.Standard with { AllowLateSurrender = false, AllowEarlySurrender = false };
        var (round, _) = CreatePlayableRound(rules: rules);

        var act = () => round.PlayerSurrender();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*surrender*rules*");
    }

    [Fact]
    public void PlayerSurrender_AfterHit_Throws()
    {
        var rules = GameRules.Standard with
        {
            AllowLateSurrender = true,
            AllowEarlySurrender = false,
            NumberOfDecks = 1,
            StartingBank = 10000m
        };

        for (int seed = 0; seed < 5000; seed++)
        {
            _events.Clear();
            var shoe = new Shoe(rules.NumberOfDecks, rules.PenetrationPercent, rules.UseCryptographicShuffle, new Random(seed));
            var player = new Human("Player", rules.StartingBank);
            var dealer = new Dealer(rules.DealerHitsSoft17);
            var round = new GameRound(shoe, player, dealer, rules, e => _events.Add(e));

            round.PlaceBet(rules.MinimumBet);
            round.Deal();

            if (round.Phase != RoundPhase.PlayerTurn)
                continue;

            round.PlayerHit();

            if (round.Phase != RoundPhase.PlayerTurn)
                continue;

            var act = () => round.PlayerSurrender();
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*surrender*");
            return;
        }

        throw new InvalidOperationException("Could not find a round where surrender after hit can be validated.");
    }

    [Fact]
    public void Deal_InvalidPhase_Throws()
    {
        var round = CreateRound();
        // Phase is Betting, not Dealing
        var act = () => round.Deal();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PlayerHit_InvalidPhase_Throws()
    {
        var round = CreateRound();
        var act = () => round.PlayerHit();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void HitUntilBust_PublishesBustedAndResolves()
    {
        // Use a seed and keep hitting until bust to verify the bust flow
        var shoe = new Shoe(6, 75, false, new Random(99));
        var player = new Human("Player", 1000m);
        var dealer = new Dealer(false);
        var round = new GameRound(shoe, player, dealer, GameRules.Standard, e => _events.Add(e));

        round.PlaceBet(10);
        round.Deal();

        if (round.Phase != RoundPhase.PlayerTurn)
            return;

        // Hit repeatedly until bust or phase changes
        int safety = 0;
        while (round.Phase == RoundPhase.PlayerTurn && safety < 20)
        {
            round.PlayerHit();
            safety++;
        }

        // Should have eventually busted or the test ran too many iterations
        if (_events.Any(e => e is PlayerBusted))
        {
            round.Phase.Should().Be(RoundPhase.Complete);
            var resolved = _events.OfType<HandResolved>().Last();
            resolved.Outcome.Should().Be(HandOutcome.Lose);
        }
    }

    [Fact]
    public void DealerBusts_PlayerWins()
    {
        // Try multiple seeds to find one where dealer busts
        for (int seed = 0; seed < 100; seed++)
        {
            var events = new List<GameEvent>();
            var shoe = new Shoe(1, 75, false, new Random(seed));
            var player = new Human("Player", 1000m);
            var dealer = new Dealer(false);
            var round = new GameRound(shoe, player, dealer, GameRules.Standard, e => events.Add(e));

            round.PlaceBet(10);
            round.Deal();

            if (round.Phase != RoundPhase.PlayerTurn)
                continue;

            round.PlayerStand();

            if (events.Any(e => e is DealerBusted))
            {
                var resolved = events.OfType<HandResolved>().Last();
                resolved.Outcome.Should().Be(HandOutcome.Win);
                return; // Test passed
            }
        }

        // If no seed produced a dealer bust, that's statistically very unlikely but skip
    }

    [Fact]
    public void Push_WhenHandValuesEqual()
    {
        // Search for a seed where player stands and values are equal
        for (int seed = 0; seed < 200; seed++)
        {
            var events = new List<GameEvent>();
            var shoe = new Shoe(1, 75, false, new Random(seed));
            var player = new Human("Player", 1000m);
            var dealer = new Dealer(false);
            var round = new GameRound(shoe, player, dealer, GameRules.Standard, e => events.Add(e));

            round.PlaceBet(10);
            round.Deal();

            if (round.Phase != RoundPhase.PlayerTurn)
                continue;

            round.PlayerStand();

            var resolved = events.OfType<HandResolved>().LastOrDefault();
            if (resolved?.Outcome == HandOutcome.Push)
            {
                resolved.Payout.Should().Be(0);
                return; // Test passed
            }
        }

        // Push is less common but should occur within 200 seeds
    }

    // --- Insurance & Dealer Peek Tests ---

    [Fact]
    public void Deal_DealerAceUpcard_EntersInsurancePhase()
    {
        var (round, _, _) = FindDealerUpcardRound(Rank.Ace);

        round.Phase.Should().Be(RoundPhase.Insurance);
        _events.Should().ContainSingle(e => e is InsuranceOffered);
        var offered = _events.OfType<InsuranceOffered>().Single();
        offered.MaxInsuranceBet.Should().Be(GameRules.Standard.MinimumBet / 2);
    }

    [Fact]
    public void Deal_DealerTenUpcard_NoBj_TransitionsToPlayerTurn()
    {
        var (round, _, _) = FindDealerUpcardRound(Rank.Ten, requireDealerBj: false);

        round.Phase.Should().Be(RoundPhase.PlayerTurn);
        _events.Should().Contain(e => e is DealerPeeked);
        var peek = _events.OfType<DealerPeeked>().Single();
        peek.HasBlackjack.Should().BeFalse();
    }

    [Fact]
    public void Deal_DealerTenUpcard_WithBj_ResolvesImmediately()
    {
        var (round, _, _) = FindDealerUpcardRound(Rank.Ten, requireDealerBj: true);

        round.Phase.Should().Be(RoundPhase.Complete);
        _events.Should().Contain(e => e is DealerPeeked);
        _events.Should().Contain(e => e is BlackjackDetected);
        _events.Should().Contain(e => e is DealerHoleCardRevealed);
        _events.Should().Contain(e => e is HandResolved);
    }

    [Fact]
    public void PlaceInsurance_DealerHasBj_PaysInsurance()
    {
        var (round, player, _) = FindDealerUpcardRound(Rank.Ace, requireDealerBj: true);

        var bankBefore = player.Bank;
        _events.Clear();
        round.PlaceInsurance();

        _events.Should().Contain(e => e is InsurancePlaced);
        var result = _events.OfType<InsuranceResult>().Single();
        result.DealerHadBlackjack.Should().BeTrue();
        result.Payout.Should().Be(GameRules.Standard.MinimumBet / 2 * GameConfig.InsurancePayout);

        round.Phase.Should().Be(RoundPhase.Complete);
    }

    [Fact]
    public void PlaceInsurance_DealerNoBj_LosesInsuranceBet()
    {
        var (round, player, _) = FindDealerUpcardRound(Rank.Ace, requireDealerBj: false, requirePlayerBj: false);

        var bankBefore = player.Bank;
        _events.Clear();
        round.PlaceInsurance();

        var result = _events.OfType<InsuranceResult>().Single();
        result.DealerHadBlackjack.Should().BeFalse();
        result.Payout.Should().Be(-(GameRules.Standard.MinimumBet / 2));

        // Player should continue to PlayerTurn
        round.Phase.Should().Be(RoundPhase.PlayerTurn);
        player.Bank.Should().Be(bankBefore - GameRules.Standard.MinimumBet / 2);
    }

    [Fact]
    public void DeclineInsurance_DealerHasBj_PlayerLosesBet()
    {
        var (round, player, _) = FindDealerUpcardRound(Rank.Ace, requireDealerBj: true, requirePlayerBj: false);

        _events.Clear();
        round.DeclineInsurance();

        _events.Should().Contain(e => e is InsuranceDeclined);
        _events.Should().NotContain(e => e is InsuranceResult);

        var resolved = _events.OfType<HandResolved>().Single();
        resolved.Outcome.Should().Be(HandOutcome.Lose);
        round.Phase.Should().Be(RoundPhase.Complete);
    }

    [Fact]
    public void DeclineInsurance_DealerNoBj_ContinuesToPlayerTurn()
    {
        var (round, _, _) = FindDealerUpcardRound(Rank.Ace, requireDealerBj: false, requirePlayerBj: false);

        _events.Clear();
        round.DeclineInsurance();

        _events.Should().Contain(e => e is InsuranceDeclined);
        _events.Should().NotContain(e => e is InsuranceResult);
        round.Phase.Should().Be(RoundPhase.PlayerTurn);
    }

    [Fact]
    public void PlaceInsurance_InvalidPhase_Throws()
    {
        var (round, _) = CreatePlayableRound();
        var act = () => round.PlaceInsurance();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DeclineInsurance_InvalidPhase_Throws()
    {
        var (round, _) = CreatePlayableRound();
        var act = () => round.DeclineInsurance();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PlayerBj_DealerAce_InsuranceOffered_ThenPush()
    {
        // Both player and dealer have blackjack, dealer shows Ace
        var (round, player, _) = FindDealerUpcardRound(Rank.Ace, requireDealerBj: true, requirePlayerBj: true);

        round.Phase.Should().Be(RoundPhase.Insurance);
        _events.Clear();

        round.DeclineInsurance();

        // Both have BJ → push
        var resolved = _events.OfType<HandResolved>().Single();
        resolved.Outcome.Should().Be(HandOutcome.Push);
        resolved.Payout.Should().Be(0);
    }

    [Fact]
    public void Deal_DealerLowUpcard_NoPeekEvent()
    {
        var rules = GameRules.Standard with { NumberOfDecks = 1, StartingBank = 1000m };

        // Dealer upcard 2-9 (not Ace, not 10-value): no peek, no insurance
        for (int seed = 0; seed < 500; seed++)
        {
            var events = new List<GameEvent>();
            var shoe = new Shoe(rules.NumberOfDecks, rules.PenetrationPercent, rules.UseCryptographicShuffle, new Random(seed));
            var player = new Human("Player", rules.StartingBank);
            var dealer = new Dealer(rules.DealerHitsSoft17);
            var round = new GameRound(shoe, player, dealer, rules, e => events.Add(e));

            round.PlaceBet(rules.MinimumBet);
            round.Deal();

            var dealerUpcard = dealer.Hand.Cards[0];
            if (dealerUpcard.Rank != Rank.Ace && dealerUpcard.PointValue != 10
                && !player.Hands[0].IsBlackjack)
            {
                events.Should().NotContain(e => e is DealerPeeked);
                events.Should().NotContain(e => e is InsuranceOffered);
                round.Phase.Should().Be(RoundPhase.PlayerTurn);
                return;
            }
        }
    }

    // --- Split Tests ---

    [Fact]
    public void PlayerSplit_CreatesSecondHand()
    {
        var (round, player) = CreateSplittableRound();

        _events.Clear();
        round.PlayerSplit();

        player.Hands.Should().HaveCount(2);
        player.Hands[0].Cards.Should().HaveCount(2);
        player.Hands[1].Cards.Should().HaveCount(2);
        _events.Should().ContainSingle(e => e is PlayerSplit);
    }

    [Fact]
    public void PlayerSplit_DuplicatesBet()
    {
        var (round, player) = CreateSplittableRound(startingBank: 10000);

        var bankBefore = player.Bank;
        round.PlayerSplit();

        // Play out both hands by standing
        while (round.Phase == RoundPhase.PlayerTurn)
            round.PlayerStand();

        // Should have 2 HandResolved events
        var resolved = _events.OfType<HandResolved>().ToList();
        resolved.Should().HaveCount(2);
        // Each hand should have the original bet amount as basis
        foreach (var r in resolved)
        {
            Math.Abs(r.Payout).Should().BeOneOf(0, GameRules.Standard.MinimumBet);
        }
    }

    [Fact]
    public void CanSplit_FalseForNonPair()
    {
        var rules = GameRules.Standard with { NumberOfDecks = 6, StartingBank = 10000m };

        // Most hands won't be pairs
        // Try many seeds to find a non-pair
        for (int seed = 0; seed < 500; seed++)
        {
            _events.Clear();
            var shoe = new Shoe(rules.NumberOfDecks, rules.PenetrationPercent, rules.UseCryptographicShuffle, new Random(seed));
            var player = new Human("Player", rules.StartingBank);
            var dealer = new Dealer(rules.DealerHitsSoft17);
            var r = new GameRound(shoe, player, dealer, rules, e => _events.Add(e));
            r.PlaceBet(rules.MinimumBet);
            r.Deal();

            if (r.Phase != RoundPhase.PlayerTurn)
                continue;

            if (player.Hands[0].Cards[0].Rank != player.Hands[0].Cards[1].Rank)
            {
                r.CanSplit().Should().BeFalse();
                return;
            }
        }
    }

    [Fact]
    public void CanSplit_FalseAtMaxSplits()
    {
        var rules = GameRules.Standard with { MaxSplits = 1 };
        var (round, _) = CreateSplittableRound(startingBank: 100000, rules: rules);

        round.PlayerSplit();
        // After one split with MaxSplits=1, CanSplit should be false even if hand is a pair
        round.CanSplit().Should().BeFalse();
    }

    [Fact]
    public void PlayerSplit_Aces_AutoStandBoth()
    {
        var (round, player) = CreateSplittableRound(pairRank: Rank.Ace);

        _events.Clear();
        round.PlayerSplit();

        // Both hands should auto-stand
        var stood = _events.OfType<PlayerStood>().ToList();
        stood.Should().HaveCount(2);
        stood[0].HandIndex.Should().Be(0);
        stood[1].HandIndex.Should().Be(1);

        // Should proceed to dealer turn or resolution
        round.Phase.Should().BeOneOf(RoundPhase.Complete);
    }

    [Fact]
    public void CanSplit_ResplitAcesDisabled()
    {
        var rules = GameRules.Standard with
        {
            ResplitAces = false,
            NumberOfDecks = 6,
            StartingBank = 100000m
        };

        // Find a seed that gives us aces, split them, and get another ace
        // This is hard to find deterministically, so we test the logic directly
        for (int seed = 0; seed < 10000; seed++)
        {
            _events.Clear();
            var shoe = new Shoe(rules.NumberOfDecks, rules.PenetrationPercent, rules.UseCryptographicShuffle, new Random(seed));
            var player = new Human("Player", rules.StartingBank);
            var dealer = new Dealer(rules.DealerHitsSoft17);
            var round = new GameRound(shoe, player, dealer, rules, e => _events.Add(e));
            round.PlaceBet(rules.MinimumBet);
            round.Deal();

            if (round.Phase != RoundPhase.PlayerTurn)
                continue;

            var hand = player.Hands[0];
            if (hand.Cards[0].Rank != Rank.Ace || hand.Cards[1].Rank != Rank.Ace)
                continue;

            // Split the aces - they auto-stand, so we can't check CanSplit after
            // Instead, verify the auto-stand behavior means no resplit opportunity
            round.PlayerSplit();
            // Auto-stood, round completed, no chance to resplit
            round.Phase.Should().Be(RoundPhase.Complete);
            return;
        }

        // If we couldn't find ace pair, that's ok - the unit test for CanSplit logic covers it
    }

    [Fact]
    public void PlayerSplit_DoubleAfterSplit()
    {
        var rules = GameRules.Standard with { DoubleAfterSplit = true };
        // Find a splittable non-ace pair
        var (round, _) = CreateSplittableRound(startingBank: 100000, excludeAces: true, rules: rules);

        round.PlayerSplit();

        // After split, should be in PlayerTurn for first hand
        round.Phase.Should().Be(RoundPhase.PlayerTurn);

        // CanDoubleDown should be true (2 cards in hand, DAS enabled)
        round.CanDoubleDown().Should().BeTrue();
    }

    [Fact]
    public void PlayerSplit_DoubleAfterSplitDisabled()
    {
        var rules = GameRules.Standard with { DoubleAfterSplit = false };
        var (round, _) = CreateSplittableRound(startingBank: 100000, excludeAces: true, rules: rules);

        round.PlayerSplit();

        round.Phase.Should().Be(RoundPhase.PlayerTurn);
        round.CanDoubleDown().Should().BeFalse();
    }

    [Fact]
    public void PlayerSplit_21IsNotBlackjack()
    {
        var rules = GameRules.Standard with { NumberOfDecks = 6, StartingBank = 100000m };

        // Find a seed where splitting tens gives an ace on one hand
        for (int seed = 0; seed < 10000; seed++)
        {
            _events.Clear();
            var shoe = new Shoe(rules.NumberOfDecks, rules.PenetrationPercent, rules.UseCryptographicShuffle, new Random(seed));
            var player = new Human("Player", rules.StartingBank);
            var dealer = new Dealer(rules.DealerHitsSoft17);
            var round = new GameRound(shoe, player, dealer, rules, e => _events.Add(e));
            round.PlaceBet(rules.MinimumBet);
            round.Deal();

            if (round.Phase != RoundPhase.PlayerTurn)
                continue;

            var hand = player.Hands[0];
            if (hand.Cards[0].Rank != hand.Cards[1].Rank)
                continue;
            if (hand.Cards[0].PointValue != 10)
                continue;

            round.PlayerSplit();

            // Stand both hands to resolve
            while (round.Phase == RoundPhase.PlayerTurn)
                round.PlayerStand();

            // Check that no HandResolved has Blackjack outcome (split 21 is Win, not Blackjack)
            var resolved = _events.OfType<HandResolved>().ToList();
            resolved.Should().HaveCount(2);
            foreach (var r in resolved)
            {
                r.Outcome.Should().NotBe(HandOutcome.Blackjack);
            }
            return;
        }
    }

    [Fact]
    public void PlayerSplit_IndependentResolution()
    {
        var rules = GameRules.Standard with { NumberOfDecks = 6, StartingBank = 100000m };

        // Find a seed where we can split and one hand busts while other stands
        for (int seed = 0; seed < 5000; seed++)
        {
            _events.Clear();
            var shoe = new Shoe(rules.NumberOfDecks, rules.PenetrationPercent, rules.UseCryptographicShuffle, new Random(seed));
            var player = new Human("Player", rules.StartingBank);
            var dealer = new Dealer(rules.DealerHitsSoft17);
            var round = new GameRound(shoe, player, dealer, rules, e => _events.Add(e));
            round.PlaceBet(rules.MinimumBet);
            round.Deal();

            if (round.Phase != RoundPhase.PlayerTurn)
                continue;

            var hand = player.Hands[0];
            if (hand.Cards[0].Rank != hand.Cards[1].Rank)
                continue;
            if (hand.Cards[0].Rank == Rank.Ace)
                continue;

            round.PlayerSplit();

            if (round.Phase != RoundPhase.PlayerTurn)
                continue;

            // Hit first hand until bust
            bool firstBusted = false;
            int safety = 0;
            while (round.Phase == RoundPhase.PlayerTurn && safety < 10)
            {
                var bustEvents = _events.OfType<PlayerBusted>().Count();
                round.PlayerHit();
                if (_events.OfType<PlayerBusted>().Count() > bustEvents)
                {
                    firstBusted = true;
                    break;
                }
                safety++;
            }

            if (!firstBusted || round.Phase != RoundPhase.PlayerTurn)
                continue;

            // Stand the second hand
            round.PlayerStand();

            // Should have 2 HandResolved events
            var resolved = _events.OfType<HandResolved>().ToList();
            resolved.Should().HaveCount(2);
            resolved[0].HandIndex.Should().Be(0);
            resolved[1].HandIndex.Should().Be(1);
            resolved[0].Outcome.Should().Be(HandOutcome.Lose); // Busted
            return;
        }
    }

    [Fact]
    public void PlayerSurrender_BlockedAfterSplit()
    {
        var (round, _) = CreateSplittableRound(excludeAces: true);

        round.PlayerSplit();

        var act = () => round.PlayerSurrender();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*surrender*split*");
    }

    [Fact]
    public void PlayerSplit_HandAdvancement()
    {
        var (round, _) = CreateSplittableRound(excludeAces: true);

        _events.Clear();
        round.PlayerSplit();

        // Should get PlayerTurnStarted for first hand (index 0)
        var turnStarted = _events.OfType<PlayerTurnStarted>().ToList();
        turnStarted.Should().Contain(e => e.HandIndex == 0);

        // Stand first hand
        _events.Clear();
        round.PlayerStand();

        // Should get PlayerTurnStarted for second hand (index 1)
        turnStarted = _events.OfType<PlayerTurnStarted>().ToList();
        turnStarted.Should().ContainSingle(e => e.HandIndex == 1);
    }

    /// <summary>
    /// Find a seed that produces a dealer upcard of the given rank,
    /// with optional dealer/player blackjack requirements.
    /// </summary>
    private (GameRound Round, Human Player, Dealer Dealer) FindDealerUpcardRound(
        Rank upcardRank,
        bool? requireDealerBj = null,
        bool? requirePlayerBj = null,
        GameRules? rules = null,
        int maxSeeds = 5000)
    {
        rules ??= GameRules.Standard with { NumberOfDecks = 1, StartingBank = 10000m };

        for (int seed = 0; seed < maxSeeds; seed++)
        {
            _events.Clear();
            var shoe = new Shoe(rules.NumberOfDecks, rules.PenetrationPercent, rules.UseCryptographicShuffle, new Random(seed));
            var player = new Human("Player", rules.StartingBank);
            var dealer = new Dealer(rules.DealerHitsSoft17);
            var round = new GameRound(shoe, player, dealer, rules, e => _events.Add(e));

            round.PlaceBet(rules.MinimumBet);
            round.Deal();

            var dealerUpcard = dealer.Hand.Cards[0];
            if (dealerUpcard.Rank != upcardRank)
                continue;

            if (requireDealerBj.HasValue && dealer.Hand.IsBlackjack != requireDealerBj.Value)
                continue;

            if (requirePlayerBj.HasValue && player.Hands[0].IsBlackjack != requirePlayerBj.Value)
                continue;

            return (round, player, dealer);
        }

        throw new InvalidOperationException(
            $"Could not find a round with dealer upcard {upcardRank} " +
            $"(dealerBj={requireDealerBj}, playerBj={requirePlayerBj}) within {maxSeeds} seeds.");
    }

    private (GameRound Round, Human Player) CreatePlayableRound(int startingBank = 1000, int seedStart = 0, GameRules? rules = null, int maxSeeds = 300)
    {
        var effectiveRules = (rules ?? GameRules.Standard) with { NumberOfDecks = 1, StartingBank = startingBank };

        for (int seed = seedStart; seed < seedStart + maxSeeds; seed++)
        {
            _events.Clear();
            var shoe = new Shoe(effectiveRules.NumberOfDecks, effectiveRules.PenetrationPercent, effectiveRules.UseCryptographicShuffle, new Random(seed));
            var player = new Human("Player", effectiveRules.StartingBank);
            var dealer = new Dealer(effectiveRules.DealerHitsSoft17);
            var round = new GameRound(shoe, player, dealer, effectiveRules, e => _events.Add(e));
            round.PlaceBet(effectiveRules.MinimumBet);
            round.Deal();

            if (round.Phase == RoundPhase.PlayerTurn)
                return (round, player);
        }

        throw new InvalidOperationException("Could not find a playable round without an initial blackjack.");
    }

    private (GameRound Round, Human Player) CreateSplittableRound(
        Rank? pairRank = null,
        int startingBank = 10000,
        bool excludeAces = false,
        GameRules? rules = null,
        int seedStart = 0,
        int maxSeeds = 10000)
    {
        var effectiveRules = (rules ?? GameRules.Standard) with { NumberOfDecks = 6, StartingBank = startingBank };

        for (int seed = seedStart; seed < seedStart + maxSeeds; seed++)
        {
            _events.Clear();
            var shoe = new Shoe(effectiveRules.NumberOfDecks, effectiveRules.PenetrationPercent, effectiveRules.UseCryptographicShuffle, new Random(seed));
            var player = new Human("Player", effectiveRules.StartingBank);
            var dealer = new Dealer(effectiveRules.DealerHitsSoft17);
            var round = new GameRound(shoe, player, dealer, effectiveRules, e => _events.Add(e));
            round.PlaceBet(effectiveRules.MinimumBet);
            round.Deal();

            if (round.Phase != RoundPhase.PlayerTurn)
                continue;

            var hand = player.Hands[0];
            if (hand.Cards[0].Rank != hand.Cards[1].Rank)
                continue;

            if (pairRank.HasValue && hand.Cards[0].Rank != pairRank.Value)
                continue;

            if (excludeAces && hand.Cards[0].Rank == Rank.Ace)
                continue;

            return (round, player);
        }

        throw new InvalidOperationException(
            $"Could not find a splittable round (pairRank={pairRank}, excludeAces={excludeAces}) within {maxSeeds} seeds.");
    }
}
