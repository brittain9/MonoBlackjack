using MonoBlackjack.Core.Events;
using MonoBlackjack.Core.Players;

namespace MonoBlackjack.Core;

public enum RoundPhase
{
    Betting,
    Dealing,
    PlayerTurn,
    DealerTurn,
    Resolution,
    Complete
}

/// <summary>
/// Orchestrates one round of blackjack, emitting domain events at each step.
/// Takes Action&lt;GameEvent&gt; so it never references EventBus directly.
/// </summary>
public class GameRound
{
    private readonly Shoe _shoe;
    private readonly Human _player;
    private readonly Dealer _dealer;
    private readonly Action<GameEvent> _publish;
    private decimal _currentBet;

    public RoundPhase Phase { get; private set; } = RoundPhase.Betting;

    public GameRound(Shoe shoe, Human player, Dealer dealer, Action<GameEvent> publish)
    {
        _shoe = shoe;
        _player = player;
        _dealer = dealer;
        _publish = publish;
    }

    public void PlaceBet(decimal amount)
    {
        if (Phase != RoundPhase.Betting)
            throw new InvalidOperationException($"Cannot place bet in phase {Phase}");

        _currentBet = Math.Clamp(amount, GameConfig.MinimumBet, GameConfig.MaximumBet);
        _publish(new BetPlaced(_player.Name, _currentBet));
        Phase = RoundPhase.Dealing;
    }

    public void Deal()
    {
        if (Phase != RoundPhase.Dealing)
            throw new InvalidOperationException($"Cannot deal in phase {Phase}");

        _player.ClearHands();
        _dealer.ClearHand();
        _player.CreateHand();

        // Deal alternating: player, dealer, player, dealer
        var card1 = _shoe.Draw();
        _player.AddCardToHand(0, card1);
        _publish(new CardDealt(card1, _player.Name, 0, false));

        var dealerCard1 = _shoe.Draw();
        _dealer.Hand.AddCard(dealerCard1);
        _publish(new CardDealt(dealerCard1, _dealer.Name, 0, false));

        var card2 = _shoe.Draw();
        _player.AddCardToHand(0, card2);
        _publish(new CardDealt(card2, _player.Name, 0, false));

        var dealerCard2 = _shoe.Draw();
        _dealer.Hand.AddCard(dealerCard2);
        _publish(new CardDealt(dealerCard2, _dealer.Name, 0, true)); // Hole card face-down

        _publish(new InitialDealComplete());

        // Check for blackjacks
        if (_player.Hands[0].IsBlackjack)
        {
            _publish(new BlackjackDetected(_player.Name));
            if (_dealer.Hand.IsBlackjack)
            {
                _publish(new BlackjackDetected(_dealer.Name));
                _publish(new DealerHoleCardRevealed(_dealer.Hand.Cards[1]));
            }
            Phase = RoundPhase.Resolution;
            Resolve();
            return;
        }

        if (_dealer.Hand.IsBlackjack)
        {
            _publish(new BlackjackDetected(_dealer.Name));
            _publish(new DealerHoleCardRevealed(_dealer.Hand.Cards[1]));
            Phase = RoundPhase.Resolution;
            Resolve();
            return;
        }

        Phase = RoundPhase.PlayerTurn;
        _publish(new PlayerTurnStarted(_player.Name));
    }

    public void PlayerHit()
    {
        if (Phase != RoundPhase.PlayerTurn)
            throw new InvalidOperationException($"Cannot hit in phase {Phase}");

        var card = _shoe.Draw();
        _player.AddCardToHand(0, card);
        _publish(new PlayerHit(_player.Name, card, 0));

        if (_player.Hands[0].IsBusted)
        {
            _publish(new PlayerBusted(_player.Name, 0));
            Phase = RoundPhase.Resolution;
            Resolve();
        }
    }

    public void PlayerStand()
    {
        if (Phase != RoundPhase.PlayerTurn)
            throw new InvalidOperationException($"Cannot stand in phase {Phase}");

        _publish(new PlayerStood(_player.Name, 0));
        Phase = RoundPhase.DealerTurn;
        PlayDealerTurn();
    }

    public void PlayerDoubleDown()
    {
        if (Phase != RoundPhase.PlayerTurn)
            throw new InvalidOperationException($"Cannot double down in phase {Phase}");
        if (!CanDoubleDown())
            throw new InvalidOperationException("Cannot double down for this hand state or bankroll.");

        _currentBet *= 2;
        var card = _shoe.Draw();
        _player.AddCardToHand(0, card);
        _publish(new PlayerDoubledDown(_player.Name, card, 0));

        if (_player.Hands[0].IsBusted)
        {
            _publish(new PlayerBusted(_player.Name, 0));
            Phase = RoundPhase.Resolution;
            Resolve();
            return;
        }

        Phase = RoundPhase.DealerTurn;
        PlayDealerTurn();
    }

    public void PlayerSurrender()
    {
        if (Phase != RoundPhase.PlayerTurn)
            throw new InvalidOperationException($"Cannot surrender in phase {Phase}");

        _publish(new PlayerSurrendered(_player.Name, 0));
        Phase = RoundPhase.Resolution;

        var payout = -_currentBet / 2;
        _player.Bank += payout;
        _publish(new HandResolved(_player.Name, 0, HandOutcome.Surrender, payout));
        _publish(new RoundComplete());
        Phase = RoundPhase.Complete;
    }

    public void PlayDealerTurn()
    {
        if (Phase != RoundPhase.DealerTurn)
            throw new InvalidOperationException($"Cannot play dealer turn in phase {Phase}");

        _publish(new DealerTurnStarted());

        // Reveal hole card
        var holeCard = _dealer.Hand.Cards[1];
        _publish(new DealerHoleCardRevealed(holeCard));

        // Dealer hits per house rules (delegated to Dealer AI)
        _dealer.PlayHand(_shoe, card => _publish(new DealerHit(card)));

        if (_dealer.Hand.IsBusted)
            _publish(new DealerBusted());
        else
            _publish(new DealerStood());

        Phase = RoundPhase.Resolution;
        Resolve();
    }

    public void Resolve()
    {
        if (Phase != RoundPhase.Resolution)
            throw new InvalidOperationException($"Cannot resolve in phase {Phase}");

        var playerHand = _player.Hands[0];

        HandOutcome outcome;
        decimal payout;

        if (playerHand.IsBlackjack && !_dealer.Hand.IsBlackjack)
        {
            outcome = HandOutcome.Blackjack;
            payout = _currentBet * GameConfig.BlackjackPayout;
        }
        else if (_dealer.Hand.IsBlackjack && !playerHand.IsBlackjack)
        {
            outcome = HandOutcome.Lose;
            payout = -_currentBet;
        }
        else if (playerHand.IsBlackjack && _dealer.Hand.IsBlackjack)
        {
            outcome = HandOutcome.Push;
            payout = 0;
        }
        else if (playerHand.IsBusted)
        {
            outcome = HandOutcome.Lose;
            payout = -_currentBet;
        }
        else if (_dealer.Hand.IsBusted)
        {
            outcome = HandOutcome.Win;
            payout = _currentBet;
        }
        else if (playerHand.Value > _dealer.Hand.Value)
        {
            outcome = HandOutcome.Win;
            payout = _currentBet;
        }
        else if (playerHand.Value < _dealer.Hand.Value)
        {
            outcome = HandOutcome.Lose;
            payout = -_currentBet;
        }
        else
        {
            outcome = HandOutcome.Push;
            payout = 0;
        }

        _player.Bank += payout;
        _publish(new HandResolved(_player.Name, 0, outcome, payout));
        _publish(new RoundComplete());
        Phase = RoundPhase.Complete;
    }

    private bool CanDoubleDown()
    {
        var hand = _player.Hands[0];
        if (hand.Cards.Count != 2)
            return false;

        if (_currentBet * 2 > GameConfig.MaximumBet)
            return false;

        return _player.Bank >= _currentBet;
    }
}
