namespace MonoBlackjack.Game;

public enum Suit : byte
{
    Hearts,
    Diamonds,
    Clubs,
    Spades
}

public enum Rank : byte
{
    Ace = 1,
    Two,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Ten,
    Jack,
    Queen,
    King
}

/// <summary>
/// Pure data representation of a playing card. No rendering, no MonoGame types.
/// Two bytes total (byte-backed enums).
/// </summary>
public readonly record struct Card(Rank Rank, Suit Suit)
{
    /// <summary>
    /// Blackjack point value. Ace = 1 (soft ace logic lives on Hand).
    /// Face cards = 10. Everything else = pip value.
    /// </summary>
    public int PointValue => Rank switch
    {
        Rank.Jack or Rank.Queen or Rank.King => 10,
        _ => (int)Rank
    };

    /// <summary>
    /// Asset name matching texture file convention: "ace_of_spades", "2_of_hearts", etc.
    /// </summary>
    public string AssetName
    {
        get
        {
            var rankPart = Rank switch
            {
                Rank.Ace or Rank.Jack or Rank.Queen or Rank.King
                    => Rank.ToString().ToLowerInvariant(),
                _ => ((int)Rank).ToString()
            };
            return $"{rankPart}_of_{Suit.ToString().ToLowerInvariant()}";
        }
    }

    public override string ToString() => $"{Rank} of {Suit}";
}
