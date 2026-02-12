using MonoBlackjack.Game;

namespace MonoBlackjack.Game.Players;

/// <summary>
/// The dealer. Never splits, never makes decisions.
/// Follows fixed house rules (hit on 16, stand on 17/soft 17).
/// </summary>
public class Dealer
{
    public string Name => "Dealer";
    public Hand Hand { get; private set; } = new();

    public void DealInitialHand(Shoe shoe)
    {
        Hand = new Hand();
        Hand.AddCard(shoe.Draw());
        Hand.AddCard(shoe.Draw());
    }

    public void Hit(Shoe shoe)
    {
        Hand.AddCard(shoe.Draw());
    }

    public void ClearHand()
    {
        Hand = new Hand();
    }

    /// <summary>
    /// Execute dealer AI according to house rules.
    /// Hits on 16 or less, stands on 17+, configurable soft 17 behavior.
    /// </summary>
    public void PlayHand(Shoe shoe)
    {
        // Dealer keeps hitting until:
        // - Hard 17 or more
        // - Soft 18 or more
        // - Soft 17 if DealerHitsSoft17 is false (stand on soft 17)
        while (Hand.Value < 17 || (Globals.DealerHitsSoft17 && Hand.IsSoft && Hand.Value == 17))
        {
            Hit(shoe);

            // Safety: if dealer busts, stop (shouldn't loop, but defensive)
            if (Hand.IsBusted)
                break;
        }
    }
}
