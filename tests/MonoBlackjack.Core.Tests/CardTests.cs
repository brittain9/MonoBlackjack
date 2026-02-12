using FluentAssertions;
using MonoBlackjack.Core;

namespace MonoBlackjack.Core.Tests;

public class CardTests
{
    [Fact]
    public void Card_PointValue_Ace_ReturnsOne()
    {
        var card = new Card(Rank.Ace, Suit.Spades);
        card.PointValue.Should().Be(1);
    }

    [Fact]
    public void Card_PointValue_FaceCards_ReturnTen()
    {
        var jack = new Card(Rank.Jack, Suit.Hearts);
        var queen = new Card(Rank.Queen, Suit.Diamonds);
        var king = new Card(Rank.King, Suit.Clubs);

        jack.PointValue.Should().Be(10);
        queen.PointValue.Should().Be(10);
        king.PointValue.Should().Be(10);
    }

    [Theory]
    [InlineData(Rank.Two, 2)]
    [InlineData(Rank.Three, 3)]
    [InlineData(Rank.Four, 4)]
    [InlineData(Rank.Five, 5)]
    [InlineData(Rank.Six, 6)]
    [InlineData(Rank.Seven, 7)]
    [InlineData(Rank.Eight, 8)]
    [InlineData(Rank.Nine, 9)]
    [InlineData(Rank.Ten, 10)]
    public void Card_PointValue_NumericCards_ReturnCorrectValue(Rank rank, int expected)
    {
        var card = new Card(rank, Suit.Spades);
        card.PointValue.Should().Be(expected);
    }

    [Theory]
    [InlineData(Rank.Ace, Suit.Spades, "ace_of_spades")]
    [InlineData(Rank.Two, Suit.Hearts, "2_of_hearts")]
    [InlineData(Rank.Jack, Suit.Diamonds, "jack_of_diamonds")]
    [InlineData(Rank.King, Suit.Clubs, "king_of_clubs")]
    public void Card_AssetName_ReturnsCorrectFormat(Rank rank, Suit suit, string expected)
    {
        var card = new Card(rank, suit);
        card.AssetName.Should().Be(expected);
    }

    [Fact]
    public void Card_IsValueType_AndComparesCorrectly()
    {
        var card1 = new Card(Rank.Ace, Suit.Spades);
        var card2 = new Card(Rank.Ace, Suit.Spades);
        var card3 = new Card(Rank.Ace, Suit.Hearts);

        card1.Should().Be(card2); // Same rank and suit
        card1.Should().NotBe(card3); // Different suit
    }
}
