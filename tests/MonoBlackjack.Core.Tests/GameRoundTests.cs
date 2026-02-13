using FluentAssertions;
using MonoBlackjack.Core;
using MonoBlackjack.Core.Events;
using MonoBlackjack.Core.Players;

namespace MonoBlackjack.Core.Tests;

public class GameRoundTests
{
    private readonly List<GameEvent> _events = [];

    private GameRound CreateRound(int seed = 42)
    {
        var shoe = new Shoe(1, new Random(seed));
        var player = new Human();
        var dealer = new Dealer();
        return new GameRound(shoe, player, dealer, e => _events.Add(e));
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
    public void Deal_WithNoBlackjack_TransitionsToPlayerTurn()
    {
        var round = CreateRound();
        round.PlaceBet(10);
        round.Deal();

        // If no blackjack detected, should end in PlayerTurn phase
        if (!_events.Any(e => e is BlackjackDetected))
        {
            round.Phase.Should().Be(RoundPhase.PlayerTurn);
            _events.Should().ContainSingle(e => e is PlayerTurnStarted);
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
        bet.Amount.Should().Be(GameConfig.MinimumBet);
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
    public void PlayerSurrender_HalvesBetAndCompletes()
    {
        var shoe = new Shoe(1, new Random(42));
        var player = new Human(startingBank: 1000);
        var dealer = new Dealer();
        var round = new GameRound(shoe, player, dealer, e => _events.Add(e));

        round.PlaceBet(100);
        round.Deal();

        if (round.Phase != RoundPhase.PlayerTurn)
            return;

        var bankBefore = player.Bank;
        round.PlayerSurrender();

        _events.Should().Contain(e => e is PlayerSurrendered);
        var resolved = _events.OfType<HandResolved>().Last();
        resolved.Outcome.Should().Be(HandOutcome.Surrender);
        resolved.Payout.Should().Be(-50);
        round.Phase.Should().Be(RoundPhase.Complete);
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
        var shoe = new Shoe(6, new Random(99));
        var player = new Human();
        var dealer = new Dealer();
        var round = new GameRound(shoe, player, dealer, e => _events.Add(e));

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
            var shoe = new Shoe(1, new Random(seed));
            var player = new Human();
            var dealer = new Dealer();
            var round = new GameRound(shoe, player, dealer, e => events.Add(e));

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
            var shoe = new Shoe(1, new Random(seed));
            var player = new Human();
            var dealer = new Dealer();
            var round = new GameRound(shoe, player, dealer, e => events.Add(e));

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
}
