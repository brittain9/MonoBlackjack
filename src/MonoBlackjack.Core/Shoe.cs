using System.Security.Cryptography;

namespace MonoBlackjack.Core;

/// <summary>
/// Multi-deck shoe. Owns the card collection, handles shuffle and draw.
/// Supports both deterministic (testing) and cryptographic (production) shuffling.
/// No MonoGame dependencies.
/// </summary>
public class Shoe
{
    private readonly int _deckCount;
    private readonly int _penetrationPercent;
    private readonly Random? _rng;
    private readonly bool _useCryptoShuffle;
    private List<Card> _cards;

    public int Remaining => _cards.Count;
    public int DeckCount => _deckCount;
    public int TotalCards => _deckCount * 52;
    public int CutCardRemainingThreshold => ComputeCutCardRemainingThreshold(TotalCards, _penetrationPercent);
    public bool IsCutCardReached => Remaining <= CutCardRemainingThreshold;

    public Shoe(int deckCount, int penetrationPercent, bool useCryptographicShuffle, Random? rng = null)
    {
        if (deckCount < 1 || deckCount > 1000)
            throw new ArgumentOutOfRangeException(nameof(deckCount), deckCount, "Deck count must be between 1 and 1000.");
        if (penetrationPercent < 1 || penetrationPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(penetrationPercent), penetrationPercent, "Penetration percent must be between 1 and 100.");

        _deckCount = deckCount;
        _penetrationPercent = penetrationPercent;
        _useCryptoShuffle = useCryptographicShuffle;

        // Only use provided RNG if crypto shuffle is disabled
        _rng = _useCryptoShuffle ? null : (rng ?? Random.Shared);

        _cards = BuildCards(_deckCount);
        Shuffle();
    }

    /// <summary>
    /// Fisher-Yates shuffle. Uses cryptographic RNG if enabled in Globals, otherwise deterministic Random.
    /// </summary>
    public void Shuffle()
    {
        int n = _cards.Count;
        while (n > 1)
        {
            n--;
            int k = GetRandomInt(n + 1);
            (_cards[k], _cards[n]) = (_cards[n], _cards[k]);
        }
    }

    /// <summary>
    /// Draw the top card. Auto-reshuffles if empty.
    /// </summary>
    public Card Draw()
    {
        if (_cards.Count == 0)
        {
            _cards = BuildCards(_deckCount);
            Shuffle();
        }

        var card = _cards[^1];
        _cards.RemoveAt(_cards.Count - 1);
        return card;
    }

    /// <summary>
    /// Rebuild all decks and reshuffle.
    /// </summary>
    public void Reset()
    {
        _cards = BuildCards(_deckCount);
        Shuffle();
    }

    /// <summary>
    /// Reshuffle at the start of a round if the cut card has been reached.
    /// </summary>
    public bool ReshuffleIfCutCardReached()
    {
        if (!IsCutCardReached)
            return false;

        Reset();
        return true;
    }

    /// <summary>
    /// Get random integer in range [0, maxValue).
    /// Uses cryptographic RNG if enabled, otherwise deterministic Random.
    /// </summary>
    private int GetRandomInt(int maxValue)
    {
        if (maxValue <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxValue), maxValue, "Max value must be greater than zero.");

        return _useCryptoShuffle
            ? RandomNumberGenerator.GetInt32(maxValue)
            : _rng!.Next(maxValue);
    }

    private static List<Card> BuildCards(int deckCount)
    {
        if (deckCount < 1 || deckCount > 1000)
            throw new ArgumentOutOfRangeException(nameof(deckCount), deckCount, "Deck count must be between 1 and 1000.");

        var cards = new List<Card>(deckCount * 52);
        for (int d = 0; d < deckCount; d++)
        {
            foreach (var suit in Enum.GetValues<Suit>())
            {
                foreach (var rank in Enum.GetValues<Rank>())
                {
                    cards.Add(new Card(rank, suit));
                }
            }
        }
        return cards;
    }

    private static int ComputeCutCardRemainingThreshold(int totalCards, int penetrationPercent)
    {
        if (penetrationPercent < 1 || penetrationPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(penetrationPercent), penetrationPercent, "Penetration percent must be between 1 and 100.");

        decimal remainingFraction = (100 - penetrationPercent) / 100m;
        return (int)Math.Ceiling(totalCards * remainingFraction);
    }
}
