using MonoBlackjack.Core;

namespace MonoBlackjack.Core.Players;

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

    public int CreateHand()
    {
        var hand = new Hand();
        _hands.Add(hand);
        return _hands.Count - 1;
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
