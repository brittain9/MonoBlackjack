using FluentAssertions;
using MonoBlackjack.Core;

namespace MonoBlackjack.Core.Tests;

public class ShoeTests
{
    [Fact]
    public void Shoe_SingleDeck_Has52Cards()
    {
        var shoe = new Shoe(1);
        shoe.Remaining.Should().Be(52);
    }

    [Fact]
    public void Shoe_SixDecks_Has312Cards()
    {
        var shoe = new Shoe(6);
        shoe.Remaining.Should().Be(312);
    }

    [Fact]
    public void Shoe_Draw_RemovesCard()
    {
        var shoe = new Shoe(1);
        var initialCount = shoe.Remaining;

        shoe.Draw();

        shoe.Remaining.Should().Be(initialCount - 1);
    }

    [Fact]
    public void Shoe_Draw_ReturnsValidCard()
    {
        var shoe = new Shoe(1);
        var card = shoe.Draw();

        Enum.IsDefined(typeof(Rank), card.Rank).Should().BeTrue();
        Enum.IsDefined(typeof(Suit), card.Suit).Should().BeTrue();
    }

    [Fact]
    public void Shoe_DrawAll_ThenDrawOne_Reshuffles()
    {
        var shoe = new Shoe(1, new Random(42)); // Seeded for determinism

        // Draw all 52 cards
        for (int i = 0; i < 52; i++)
        {
            shoe.Draw();
        }

        shoe.Remaining.Should().Be(0);

        // Next draw should auto-reshuffle
        var card = shoe.Draw();

        shoe.Remaining.Should().Be(51); // 52 reshuffled - 1 drawn
        card.Should().NotBeNull();
    }

    [Fact]
    public void Shoe_Reset_RebuildsAndReshuffles()
    {
        var shoe = new Shoe(6);

        // Draw some cards
        for (int i = 0; i < 50; i++)
        {
            shoe.Draw();
        }

        shoe.Reset();

        shoe.Remaining.Should().Be(312);
    }

    [Fact]
    public void Shoe_WithSeededRandom_IsDeterministic()
    {
        var shoe1 = new Shoe(1, new Random(42));
        var shoe2 = new Shoe(1, new Random(42));

        var cards1 = new List<Card>();
        var cards2 = new List<Card>();

        for (int i = 0; i < 10; i++)
        {
            cards1.Add(shoe1.Draw());
            cards2.Add(shoe2.Draw());
        }

        cards1.Should().Equal(cards2);
    }

    [Fact]
    public void Shoe_ContainsCorrectCardDistribution()
    {
        var shoe = new Shoe(1, new Random(42));
        var cards = new List<Card>();

        // Draw all cards
        for (int i = 0; i < 52; i++)
        {
            cards.Add(shoe.Draw());
        }

        // Should have 4 of each rank (one per suit)
        foreach (var rank in Enum.GetValues<Rank>())
        {
            cards.Count(c => c.Rank == rank).Should().Be(4);
        }

        // Should have 13 of each suit (one per rank)
        foreach (var suit in Enum.GetValues<Suit>())
        {
            cards.Count(c => c.Suit == suit).Should().Be(13);
        }
    }
}
