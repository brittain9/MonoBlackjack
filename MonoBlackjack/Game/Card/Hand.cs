namespace MonoBlackjack.Game;

/// <summary>
/// A single blackjack hand. Domain aggregate that owns its cards and evaluation logic.
/// </summary>
public class Hand
{
    private readonly List<Card> _cards = [];

    public IReadOnlyList<Card> Cards => _cards;
    public int Value => Evaluate(_cards);
    public bool IsBusted => Value > Globals.BustNumber;
    public bool IsBlackjack => _cards.Count == 2 && Value == Globals.BustNumber;

    public bool IsSoft
    {
        get
        {
            int hard = 0;
            bool hasAce = false;
            foreach (var card in _cards)
            {
                hard += card.PointValue;
                if (card.Rank == Rank.Ace)
                    hasAce = true;
            }
            return hasAce && hard + Globals.AceExtraValue <= Globals.BustNumber;
        }
    }

    public void AddCard(Card card) => _cards.Add(card);
    public void Clear() => _cards.Clear();

    /// <summary>
    /// Remove a card at index (for splitting). Returns the removed card.
    /// </summary>
    public Card RemoveAt(int index)
    {
        var card = _cards[index];
        _cards.RemoveAt(index);
        return card;
    }

    /// <summary>
    /// Evaluate any collection of cards without needing a Hand instance.
    /// </summary>
    public static int Evaluate(IReadOnlyList<Card> cards)
    {
        int value = 0;
        bool hasAce = false;

        foreach (var card in cards)
        {
            value += card.PointValue;
            if (card.Rank == Rank.Ace)
                hasAce = true;
        }

        if (hasAce && value + Globals.AceExtraValue <= Globals.BustNumber)
            value += Globals.AceExtraValue;

        return value;
    }

    public override string ToString() => $"{string.Join(", ", _cards)} = {Value}";
}
