using MonoBlackjack.Core.Events;
using MonoBlackjack.Core.Players;

namespace MonoBlackjack.Core;

public enum RoundPhase
{
    Betting,
    Dealing,
    Insurance,
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
    private readonly Dictionary<int, decimal> _bets = new();
    private decimal _insuranceBet;
    private int _currentHandIndex;
    private int _splitCount;

    public RoundPhase Phase { get; private set; } = RoundPhase.Betting;

    private Hand CurrentHand => _player.Hands[_currentHandIndex];
    private decimal CurrentBet => _bets[_currentHandIndex];

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

        var bet = Math.Clamp(amount, GameConfig.MinimumBet, GameConfig.MaximumBet);
        _bets[0] = bet;
        _publish(new BetPlaced(_player.Name, bet));
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

        // Dealer upcard determines peek/insurance flow
        var dealerUpcard = _dealer.Hand.Cards[0];

        if (dealerUpcard.Rank == Rank.Ace)
        {
            // Dealer shows Ace: offer insurance, then peek
            Phase = RoundPhase.Insurance;
            _publish(new InsuranceOffered(_player.Name, _bets[0] / 2));
            return;
        }

        if (dealerUpcard.PointValue == 10)
        {
            // Dealer shows 10-value: peek for blackjack (no insurance)
            if (DealerPeek())
                return; // Dealer had blackjack, round resolved
        }

        // Check for player blackjack (dealer doesn't have one at this point)
        if (_player.Hands[0].IsBlackjack)
        {
            _publish(new BlackjackDetected(_player.Name));
            Phase = RoundPhase.Resolution;
            Resolve();
            return;
        }

        Phase = RoundPhase.PlayerTurn;
        _publish(new PlayerTurnStarted(_player.Name, 0));
    }

    public void PlaceInsurance()
    {
        if (Phase != RoundPhase.Insurance)
            throw new InvalidOperationException($"Cannot place insurance in phase {Phase}");

        _insuranceBet = _bets[0] / 2;
        _publish(new InsurancePlaced(_player.Name, _insuranceBet));

        ResolveAfterInsuranceDecision();
    }

    public void DeclineInsurance()
    {
        if (Phase != RoundPhase.Insurance)
            throw new InvalidOperationException($"Cannot decline insurance in phase {Phase}");

        _insuranceBet = 0;
        _publish(new InsuranceDeclined(_player.Name));

        ResolveAfterInsuranceDecision();
    }

    private void ResolveAfterInsuranceDecision()
    {
        bool dealerHasBj = _dealer.Hand.IsBlackjack;
        _publish(new DealerPeeked(dealerHasBj));

        if (dealerHasBj)
        {
            // Reveal hole card
            _publish(new DealerHoleCardRevealed(_dealer.Hand.Cards[1]));
            _publish(new BlackjackDetected(_dealer.Name));

            // Resolve insurance side bet
            if (_insuranceBet > 0)
            {
                var insurancePayout = _insuranceBet * GameConfig.InsurancePayout;
                _player.Bank += insurancePayout;
                _publish(new InsuranceResult(_player.Name, true, insurancePayout));
            }

            // Check if player also has blackjack (push)
            if (_player.Hands[0].IsBlackjack)
                _publish(new BlackjackDetected(_player.Name));

            Phase = RoundPhase.Resolution;
            Resolve();
            return;
        }

        // Dealer doesn't have blackjack — collect insurance bet (player loses it)
        if (_insuranceBet > 0)
        {
            _player.Bank -= _insuranceBet;
            _publish(new InsuranceResult(_player.Name, false, -_insuranceBet));
        }

        // Check for player blackjack
        if (_player.Hands[0].IsBlackjack)
        {
            _publish(new BlackjackDetected(_player.Name));
            Phase = RoundPhase.Resolution;
            Resolve();
            return;
        }

        Phase = RoundPhase.PlayerTurn;
        _publish(new PlayerTurnStarted(_player.Name, 0));
    }

    /// <summary>
    /// Dealer peeks at hole card for blackjack (when upcard is 10-value).
    /// Returns true if dealer has blackjack and the round was resolved.
    /// </summary>
    private bool DealerPeek()
    {
        bool dealerHasBj = _dealer.Hand.IsBlackjack;
        _publish(new DealerPeeked(dealerHasBj));

        if (!dealerHasBj)
            return false;

        _publish(new DealerHoleCardRevealed(_dealer.Hand.Cards[1]));
        _publish(new BlackjackDetected(_dealer.Name));

        if (_player.Hands[0].IsBlackjack)
            _publish(new BlackjackDetected(_player.Name));

        Phase = RoundPhase.Resolution;
        Resolve();
        return true;
    }

    public void PlayerHit()
    {
        if (Phase != RoundPhase.PlayerTurn)
            throw new InvalidOperationException($"Cannot hit in phase {Phase}");

        var card = _shoe.Draw();
        _player.AddCardToHand(_currentHandIndex, card);
        _publish(new PlayerHit(_player.Name, card, _currentHandIndex));

        if (CurrentHand.IsBusted)
        {
            _publish(new PlayerBusted(_player.Name, _currentHandIndex));
            AdvanceToNextHand();
        }
    }

    public void PlayerStand()
    {
        if (Phase != RoundPhase.PlayerTurn)
            throw new InvalidOperationException($"Cannot stand in phase {Phase}");

        _publish(new PlayerStood(_player.Name, _currentHandIndex));
        AdvanceToNextHand();
    }

    public void PlayerDoubleDown()
    {
        if (Phase != RoundPhase.PlayerTurn)
            throw new InvalidOperationException($"Cannot double down in phase {Phase}");
        if (!CanDoubleDown())
            throw new InvalidOperationException("Cannot double down for this hand state or bankroll.");

        _bets[_currentHandIndex] *= 2;
        var card = _shoe.Draw();
        _player.AddCardToHand(_currentHandIndex, card);
        _publish(new PlayerDoubledDown(_player.Name, card, _currentHandIndex));

        if (CurrentHand.IsBusted)
        {
            _publish(new PlayerBusted(_player.Name, _currentHandIndex));
        }

        AdvanceToNextHand();
    }

    public void PlayerSurrender()
    {
        if (Phase != RoundPhase.PlayerTurn)
            throw new InvalidOperationException($"Cannot surrender in phase {Phase}");
        if (_splitCount > 0)
            throw new InvalidOperationException("Cannot surrender after splitting.");

        _publish(new PlayerSurrendered(_player.Name, 0));
        Phase = RoundPhase.Resolution;

        var payout = -_bets[0] / 2;
        _player.Bank += payout;
        _publish(new HandResolved(_player.Name, 0, HandOutcome.Surrender, payout));
        _publish(new RoundComplete());
        Phase = RoundPhase.Complete;
    }

    public void PlayerSplit()
    {
        if (Phase != RoundPhase.PlayerTurn)
            throw new InvalidOperationException($"Cannot split in phase {Phase}");
        if (!CanSplit())
            throw new InvalidOperationException("Cannot split this hand.");

        // Remove second card from current hand
        var splitCard = CurrentHand.RemoveAt(1);

        // Create new hand and add the split card
        int newHandIndex = _player.CreateHand();
        _player.AddCardToHand(newHandIndex, splitCard);

        // Copy bet to new hand
        _bets[newHandIndex] = _bets[_currentHandIndex];

        _splitCount++;

        bool splitAces = splitCard.Rank == Rank.Ace;

        _publish(new PlayerSplit(_player.Name, _currentHandIndex, newHandIndex, splitCard));

        // Deal one new card to each hand
        var cardForCurrent = _shoe.Draw();
        _player.AddCardToHand(_currentHandIndex, cardForCurrent);
        _publish(new CardDealt(cardForCurrent, _player.Name, _currentHandIndex, false));

        var cardForNew = _shoe.Draw();
        _player.AddCardToHand(newHandIndex, cardForNew);
        _publish(new CardDealt(cardForNew, _player.Name, newHandIndex, false));

        // Split aces special case: auto-stand both hands
        if (splitAces)
        {
            _publish(new PlayerStood(_player.Name, _currentHandIndex));
            _publish(new PlayerStood(_player.Name, newHandIndex));

            // Skip to after all hands
            _currentHandIndex = _player.Hands.Count;

            // Check if any non-busted hand exists
            bool anyNonBusted = false;
            for (int i = 0; i < _player.Hands.Count; i++)
            {
                if (!_player.Hands[i].IsBusted)
                {
                    anyNonBusted = true;
                    break;
                }
            }

            if (anyNonBusted)
            {
                Phase = RoundPhase.DealerTurn;
                PlayDealerTurn();
            }
            else
            {
                Phase = RoundPhase.Resolution;
                Resolve();
            }
            return;
        }

        // Continue playing the current hand
        _publish(new PlayerTurnStarted(_player.Name, _currentHandIndex));
    }

    public bool CanSplit()
    {
        if (Phase != RoundPhase.PlayerTurn)
            return false;

        var hand = CurrentHand;
        if (hand.Cards.Count != 2)
            return false;

        // Cards must match by rank
        if (hand.Cards[0].Rank != hand.Cards[1].Rank)
            return false;

        if (_splitCount >= GameConfig.MaxSplits)
            return false;

        // If aces and already split once, check ResplitAces
        if (hand.Cards[0].Rank == Rank.Ace && _splitCount > 0 && !GameConfig.ResplitAces)
            return false;

        // Player must have enough bank for additional bet
        if (_player.Bank < _bets[_currentHandIndex])
            return false;

        return true;
    }

    public bool CanDoubleDown()
    {
        var hand = CurrentHand;
        if (hand.Cards.Count != 2)
            return false;

        if (_bets[_currentHandIndex] * 2 > GameConfig.MaximumBet)
            return false;

        // Block on split hands if DoubleAfterSplit is disabled
        if (_splitCount > 0 && !GameConfig.DoubleAfterSplit)
            return false;

        return _player.Bank >= _bets[_currentHandIndex];
    }

    private void AdvanceToNextHand()
    {
        _currentHandIndex++;

        if (_currentHandIndex < _player.Hands.Count)
        {
            _publish(new PlayerTurnStarted(_player.Name, _currentHandIndex));
            return;
        }

        // All hands done — check if any non-busted hand exists
        bool anyNonBusted = false;
        for (int i = 0; i < _player.Hands.Count; i++)
        {
            if (!_player.Hands[i].IsBusted)
            {
                anyNonBusted = true;
                break;
            }
        }

        if (anyNonBusted)
        {
            Phase = RoundPhase.DealerTurn;
            PlayDealerTurn();
        }
        else
        {
            Phase = RoundPhase.Resolution;
            Resolve();
        }
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

        for (int i = 0; i < _player.Hands.Count; i++)
        {
            var playerHand = _player.Hands[i];
            var bet = _bets[i];

            HandOutcome outcome;
            decimal payout;

            // A 21 on a split hand is NOT blackjack (pays 1:1, not 3:2)
            bool isNaturalBlackjack = playerHand.IsBlackjack && _splitCount == 0;

            if (isNaturalBlackjack && !_dealer.Hand.IsBlackjack)
            {
                outcome = HandOutcome.Blackjack;
                payout = bet * GameConfig.BlackjackPayout;
            }
            else if (_dealer.Hand.IsBlackjack && !isNaturalBlackjack)
            {
                outcome = HandOutcome.Lose;
                payout = -bet;
            }
            else if (isNaturalBlackjack && _dealer.Hand.IsBlackjack)
            {
                outcome = HandOutcome.Push;
                payout = 0;
            }
            else if (playerHand.IsBusted)
            {
                outcome = HandOutcome.Lose;
                payout = -bet;
            }
            else if (_dealer.Hand.IsBusted)
            {
                outcome = HandOutcome.Win;
                payout = bet;
            }
            else if (playerHand.Value > _dealer.Hand.Value)
            {
                outcome = HandOutcome.Win;
                payout = bet;
            }
            else if (playerHand.Value < _dealer.Hand.Value)
            {
                outcome = HandOutcome.Lose;
                payout = -bet;
            }
            else
            {
                outcome = HandOutcome.Push;
                payout = 0;
            }

            _player.Bank += payout;
            _publish(new HandResolved(_player.Name, i, outcome, payout));
        }

        _publish(new RoundComplete());
        Phase = RoundPhase.Complete;
    }
}
