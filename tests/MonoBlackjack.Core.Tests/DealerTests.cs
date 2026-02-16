using FluentAssertions;
using MonoBlackjack.Core;
using MonoBlackjack.Core.Players;

namespace MonoBlackjack.Core.Tests;

public class DealerTests
{
    [Fact]
    public void Dealer_PlayHand_HitsOn16()
    {
        var dealer = new Dealer(false); // S17
        var shoe = new Shoe(1, 75, false, new Random(42));

        dealer.Hand.AddCard(new Card(Rank.Ten, Suit.Hearts));
        dealer.Hand.AddCard(new Card(Rank.Six, Suit.Spades));

        dealer.PlayHand(shoe);

        dealer.Hand.Value.Should().BeGreaterThan(16);
    }

    [Fact]
    public void Dealer_PlayHand_StandsOn17()
    {
        var dealer = new Dealer(false); // S17
        var shoe = new Shoe(1, 75, false, new Random(42));

        dealer.Hand.AddCard(new Card(Rank.Ten, Suit.Hearts));
        dealer.Hand.AddCard(new Card(Rank.Seven, Suit.Spades));

        var cardCountBefore = dealer.Hand.Cards.Count;
        dealer.PlayHand(shoe);

        dealer.Hand.Cards.Count.Should().Be(cardCountBefore); // No new cards
        dealer.Hand.Value.Should().Be(17);
    }

    [Fact]
    public void Dealer_PlayHand_StandsOnSoft17_WhenConfigured()
    {
        var dealer = new Dealer(false); // S17
        var shoe = new Shoe(1, 75, false, new Random(42));

        dealer.Hand.AddCard(new Card(Rank.Ace, Suit.Hearts));
        dealer.Hand.AddCard(new Card(Rank.Six, Suit.Spades));

        var cardCountBefore = dealer.Hand.Cards.Count;
        dealer.PlayHand(shoe);

        dealer.Hand.Value.Should().Be(17);
        dealer.Hand.IsSoft.Should().BeTrue();
        dealer.Hand.Cards.Count.Should().Be(cardCountBefore); // No new cards
    }

    [Fact]
    public void Dealer_PlayHand_HitsOnSoft17_WhenConfigured()
    {
        var dealer = new Dealer(true); // H17 rule
        var shoe = new Shoe(1, 75, false, new Random(42));

        dealer.Hand.AddCard(new Card(Rank.Ace, Suit.Hearts));
        dealer.Hand.AddCard(new Card(Rank.Six, Suit.Spades));

        dealer.PlayHand(shoe);

        // Should have drawn at least one more card
        dealer.Hand.Cards.Count.Should().BeGreaterThan(2);
    }

    [Fact]
    public void Dealer_PlayHand_StopsWhenBusted()
    {
        var dealer = new Dealer(false); // S17

        // Manually create a hand that will bust
        dealer.Hand.AddCard(new Card(Rank.Ten, Suit.Hearts));
        dealer.Hand.AddCard(new Card(Rank.Nine, Suit.Spades));
        dealer.Hand.AddCard(new Card(Rank.Five, Suit.Diamonds));

        var shoe = new Shoe(1, 75, false, new Random(42));

        dealer.PlayHand(shoe);

        dealer.Hand.IsBusted.Should().BeTrue();
    }

    [Fact]
    public void Dealer_ClearHand_RemovesAllCards()
    {
        var dealer = new Dealer(false); // S17
        dealer.Hand.AddCard(new Card(Rank.King, Suit.Hearts));
        dealer.Hand.AddCard(new Card(Rank.Queen, Suit.Spades));

        dealer.ClearHand();

        dealer.Hand.Cards.Should().BeEmpty();
    }
}
