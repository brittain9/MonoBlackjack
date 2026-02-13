namespace MonoBlackjack.Core.Events;

public enum HandOutcome
{
    Win,
    Lose,
    Push,
    Blackjack,
    Surrender
}

// Betting
public record BetPlaced(string PlayerName, decimal Amount) : GameEvent;

// Dealing
public record CardDealt(Card Card, string Recipient, int HandIndex, bool FaceDown) : GameEvent;
public record InitialDealComplete : GameEvent;
public record BlackjackDetected(string Who) : GameEvent;

// Player turn
public record PlayerTurnStarted(string PlayerName) : GameEvent;
public record PlayerHit(string PlayerName, Card Card, int HandIndex) : GameEvent;
public record PlayerStood(string PlayerName, int HandIndex) : GameEvent;
public record PlayerBusted(string PlayerName, int HandIndex) : GameEvent;
public record PlayerDoubledDown(string PlayerName, Card Card, int HandIndex) : GameEvent;
public record PlayerSplit(string PlayerName, int HandIndex) : GameEvent;
public record PlayerSurrendered(string PlayerName, int HandIndex) : GameEvent;

// Dealer turn
public record DealerTurnStarted : GameEvent;
public record DealerHoleCardRevealed(Card Card) : GameEvent;
public record DealerHit(Card Card) : GameEvent;
public record DealerStood : GameEvent;
public record DealerBusted : GameEvent;

// Resolution
public record HandResolved(string PlayerName, int HandIndex, HandOutcome Outcome, decimal Payout) : GameEvent;
public record RoundComplete : GameEvent;
