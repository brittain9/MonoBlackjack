using FluentAssertions;
using MonoBlackjack.Core;

namespace MonoBlackjack.Core.Tests;

public class HandTests
{
    [Fact]
    public void Hand_EmptyHand_HasValueZero()
    {
        var hand = new Hand();
        hand.Value.Should().Be(0);
        hand.Cards.Should().BeEmpty();
    }

    [Fact]
    public void Hand_TwoCards_Value_CalculatesCorrectly()
    {
        var hand = new Hand();
        hand.AddCard(new Card(Rank.Seven, Suit.Hearts));
        hand.AddCard(new Card(Rank.Five, Suit.Spades));

        hand.Value.Should().Be(12);
    }

    [Fact]
    public void Hand_Blackjack_IsDetected()
    {
        var hand = new Hand();
        hand.AddCard(new Card(Rank.Ace, Suit.Spades));
        hand.AddCard(new Card(Rank.King, Suit.Hearts));

        hand.IsBlackjack.Should().BeTrue();
        hand.Value.Should().Be(21);
    }

    [Fact]
    public void Hand_TenPlusAce_IsBlackjack()
    {
        var hand = new Hand();
        hand.AddCard(new Card(Rank.Ten, Suit.Diamonds));
        hand.AddCard(new Card(Rank.Ace, Suit.Clubs));

        hand.IsBlackjack.Should().BeTrue();
    }

    [Fact]
    public void Hand_ThreeCardsTo21_IsNotBlackjack()
    {
        var hand = new Hand();
        hand.AddCard(new Card(Rank.Seven, Suit.Hearts));
        hand.AddCard(new Card(Rank.Seven, Suit.Spades));
        hand.AddCard(new Card(Rank.Seven, Suit.Diamonds));

        hand.Value.Should().Be(21);
        hand.IsBlackjack.Should().BeFalse();
    }

    [Fact]
    public void Hand_SoftAce_CountsAs11_WhenUnder21()
    {
        var hand = new Hand();
        hand.AddCard(new Card(Rank.Ace, Suit.Hearts));
        hand.AddCard(new Card(Rank.Six, Suit.Spades));

        hand.Value.Should().Be(17); // 1 + 6 + 10 (soft ace)
        hand.IsSoft.Should().BeTrue();
    }

    [Fact]
    public void Hand_SoftAce_CountsAs1_WhenWouldBust()
    {
        var hand = new Hand();
        hand.AddCard(new Card(Rank.Ace, Suit.Hearts));
        hand.AddCard(new Card(Rank.Six, Suit.Spades));
        hand.AddCard(new Card(Rank.Seven, Suit.Diamonds));

        hand.Value.Should().Be(14); // 1 + 6 + 7 (ace forced to 1)
        hand.IsSoft.Should().BeFalse();
    }

    [Fact]
    public void Hand_Busted_IsDetected()
    {
        var hand = new Hand();
        hand.AddCard(new Card(Rank.Ten, Suit.Hearts));
        hand.AddCard(new Card(Rank.Nine, Suit.Spades));
        hand.AddCard(new Card(Rank.Five, Suit.Diamonds));

        hand.Value.Should().Be(24);
        hand.IsBusted.Should().BeTrue();
    }

    [Fact]
    public void Hand_Clear_RemovesAllCards()
    {
        var hand = new Hand();
        hand.AddCard(new Card(Rank.King, Suit.Hearts));
        hand.AddCard(new Card(Rank.Queen, Suit.Spades));

        hand.Clear();

        hand.Cards.Should().BeEmpty();
        hand.Value.Should().Be(0);
    }

    [Fact]
    public void Hand_RemoveAt_RemovesCard()
    {
        var hand = new Hand();
        hand.AddCard(new Card(Rank.Eight, Suit.Hearts));
        hand.AddCard(new Card(Rank.Eight, Suit.Spades));

        var removed = hand.RemoveAt(0);

        removed.Rank.Should().Be(Rank.Eight);
        hand.Cards.Should().HaveCount(1);
    }

    [Fact]
    public void Hand_RemoveAt_InvalidIndex_Throws()
    {
        var hand = new Hand();
        hand.AddCard(new Card(Rank.Eight, Suit.Hearts));

        var act = () => hand.RemoveAt(2);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("index");
    }

    [Fact]
    public void Hand_AddCard_InvalidRank_Throws()
    {
        var hand = new Hand();

        var act = () => hand.AddCard(new Card((Rank)0, Suit.Hearts));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("card");
    }

    [Fact]
    public void Hand_AddCard_InvalidSuit_Throws()
    {
        var hand = new Hand();

        var act = () => hand.AddCard(new Card(Rank.Ace, (Suit)99));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("card");
    }

    [Fact]
    public void Hand_StaticEvaluate_WorksWithoutHandInstance()
    {
        var cards = new List<Card>
        {
            new Card(Rank.Ace, Suit.Hearts),
            new Card(Rank.King, Suit.Spades)
        };

        Hand.Evaluate(cards).Should().Be(21);
    }

    [Fact]
    public void Hand_StaticEvaluate_NullCards_Throws()
    {
        var act = () => Hand.Evaluate(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cards");
    }
}
