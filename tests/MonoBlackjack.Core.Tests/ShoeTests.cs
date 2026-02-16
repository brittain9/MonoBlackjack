using FluentAssertions;
using MonoBlackjack.Core;

namespace MonoBlackjack.Core.Tests;

public class ShoeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Shoe_InvalidDeckCount_Throws(int invalidDeckCount)
    {
        var act = () => new Shoe(invalidDeckCount, 75, false);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("deckCount");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(101)]
    public void Shoe_InvalidPenetrationPercent_Throws(int invalidPenetration)
    {
        var act = () => new Shoe(6, invalidPenetration, false);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("penetrationPercent");
    }

    [Fact]
    public void Shoe_SingleDeck_Has52Cards()
    {
        var shoe = new Shoe(1, 75, false);
        shoe.Remaining.Should().Be(52);
    }

    [Fact]
    public void Shoe_SixDecks_Has312Cards()
    {
        var shoe = new Shoe(6, 75, false);
        shoe.Remaining.Should().Be(312);
    }

    [Fact]
    public void Shoe_Draw_RemovesCard()
    {
        var shoe = new Shoe(1, 75, false);
        var initialCount = shoe.Remaining;

        shoe.Draw();

        shoe.Remaining.Should().Be(initialCount - 1);
    }

    [Fact]
    public void Shoe_Draw_ReturnsValidCard()
    {
        var shoe = new Shoe(1, 75, false);
        var card = shoe.Draw();

        Enum.IsDefined(typeof(Rank), card.Rank).Should().BeTrue();
        Enum.IsDefined(typeof(Suit), card.Suit).Should().BeTrue();
    }

    [Fact]
    public void Shoe_DrawAll_ThenDrawOne_Reshuffles()
    {
        var shoe = new Shoe(1, 75, false, new Random(42)); // Seeded for determinism

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
    public void Shoe_CutCardReached_AtConfiguredPenetration()
    {
        const int penetration = 75;
        var shoe = new Shoe(1, penetration, false, new Random(42));

        shoe.CutCardRemainingThreshold.Should().Be(13);
        shoe.IsCutCardReached.Should().BeFalse();

        while (shoe.Remaining > shoe.CutCardRemainingThreshold)
            shoe.Draw();

        shoe.Remaining.Should().Be(13);
        shoe.IsCutCardReached.Should().BeTrue();
    }

    [Fact]
    public void Shoe_ReshuffleIfCutCardReached_ResetsShoe()
    {
        const int penetration = 75;
        var shoe = new Shoe(1, penetration, false, new Random(42));

        while (shoe.Remaining > shoe.CutCardRemainingThreshold)
            shoe.Draw();

        var reshuffled = shoe.ReshuffleIfCutCardReached();

        reshuffled.Should().BeTrue();
        shoe.Remaining.Should().Be(52);
        shoe.IsCutCardReached.Should().BeFalse();
    }

    [Fact]
    public void Shoe_ReshuffleIfCutCardNotReached_DoesNothing()
    {
        const int penetration = 75;
        var shoe = new Shoe(1, penetration, false, new Random(42));
        var remainingBefore = shoe.Remaining;

        var reshuffled = shoe.ReshuffleIfCutCardReached();

        reshuffled.Should().BeFalse();
        shoe.Remaining.Should().Be(remainingBefore);
    }

    [Fact]
    public void Shoe_Reset_RebuildsAndReshuffles()
    {
        var shoe = new Shoe(6, 75, false);

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
        var shoe1 = new Shoe(1, 75, false, new Random(42));
        var shoe2 = new Shoe(1, 75, false, new Random(42));

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
        var shoe = new Shoe(1, 75, false, new Random(42));
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
