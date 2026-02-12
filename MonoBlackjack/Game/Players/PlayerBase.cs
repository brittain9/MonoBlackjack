using MonoBlackjack.Game;

namespace MonoBlackjack.Game.Players;

/// <summary>
/// Shared logic for all player types. No MonoGame dependencies, no rendering.
/// </summary>
public abstract class PlayerBase
{
    private readonly List<Hand> _hands = [];

    public string Name { get; }
    public IReadOnlyList<Hand> Hands => _hands;

    protected PlayerBase(string name)
    {
        Name = name;
    }

    public virtual void DealInitialHand(Shoe shoe)
    {
        ClearHands();
        var hand = new Hand();
        hand.AddCard(shoe.Draw());
        hand.AddCard(shoe.Draw());
        _hands.Add(hand);
    }

    public void AddCardToHand(int handIndex, Card card)
    {
        _hands[handIndex].AddCard(card);
    }

    public void ClearHands()
    {
        _hands.Clear();
    }

    protected void AddHand(Hand hand) => _hands.Add(hand);
}
