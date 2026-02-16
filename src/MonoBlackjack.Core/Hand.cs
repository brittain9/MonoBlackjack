namespace MonoBlackjack.Core;

/// <summary>
/// A single blackjack hand. Domain aggregate that owns its cards and evaluation logic.
/// </summary>
public class Hand
{
    private readonly List<Card> _cards = [];

    public IReadOnlyList<Card> Cards => _cards;
    public int Value => Evaluate(_cards);
    public bool IsBusted => Value > GameConfig.BustNumber;
    public bool IsBlackjack => _cards.Count == 2 && Value == GameConfig.BustNumber;

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
            return hasAce && hard + GameConfig.AceExtraValue <= GameConfig.BustNumber;
        }
    }

    public void AddCard(Card card)
    {
        if (!Enum.IsDefined(card.Rank))
            throw new ArgumentOutOfRangeException(nameof(card), card, "Card rank must be a defined Rank value.");
        if (!Enum.IsDefined(card.Suit))
            throw new ArgumentOutOfRangeException(nameof(card), card, "Card suit must be a defined Suit value.");

        _cards.Add(card);
    }

    public void Clear() => _cards.Clear();

    /// <summary>
    /// Remove a card at index (for splitting). Returns the removed card.
    /// </summary>
    public Card RemoveAt(int index)
    {
        if (index < 0 || index >= _cards.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index must reference an existing card in the hand.");

        var card = _cards[index];
        _cards.RemoveAt(index);
        return card;
    }

    /// <summary>
    /// Evaluate any collection of cards without needing a Hand instance.
    /// </summary>
    public static int Evaluate(IReadOnlyList<Card> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        int value = 0;
        bool hasAce = false;

        foreach (var card in cards)
        {
            value += card.PointValue;
            if (card.Rank == Rank.Ace)
                hasAce = true;
        }

        if (hasAce && value + GameConfig.AceExtraValue <= GameConfig.BustNumber)
            value += GameConfig.AceExtraValue;

        return value;
    }

    public override string ToString() => $"{string.Join(", ", _cards)} = {Value}";
}
