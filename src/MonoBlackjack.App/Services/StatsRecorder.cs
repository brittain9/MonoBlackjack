using System.Globalization;
using MonoBlackjack.Core;
using MonoBlackjack.Core.Events;
using MonoBlackjack.Core.Ports;
using MonoBlackjack.Infrastructure.Diagnostics;
using MonoBlackjack.Infrastructure.Events;

namespace MonoBlackjack.Services;

internal sealed class StatsRecorder : IDisposable
{
    private readonly IStatsRepository _statsRepository;
    private readonly Action<string>? _nonBlockingErrorNotifier;
    private readonly int _profileId;
    private readonly GameRules _rules;
    private readonly List<CardSeenRecord> _cardsSeen = [];
    private readonly List<HandResultRecord> _handResults = [];
    private readonly List<TrackedDecision> _decisions = [];
    private readonly Dictionary<int, List<Card>> _playerHands = new();
    private readonly HashSet<int> _bustedHands = [];
    private readonly List<Card> _dealerCards = [];
    private readonly List<IDisposable> _subscriptions = [];

    private decimal _betAmount;
    private decimal _insurancePayoutTotal;
    private string _dealerUpcard = "?";
    private DateTime _roundStartedUtc;
    private bool _roundOpen;
    private bool _dealerBusted;

    public StatsRecorder(
        EventBus eventBus,
        IStatsRepository statsRepository,
        int profileId,
        GameRules rules,
        Action<string>? nonBlockingErrorNotifier = null)
    {
        _statsRepository = statsRepository;
        _nonBlockingErrorNotifier = nonBlockingErrorNotifier;
        _profileId = profileId;
        _rules = rules;

        _subscriptions.Add(eventBus.Subscribe<BetPlaced>(OnBetPlaced));
        _subscriptions.Add(eventBus.Subscribe<CardDealt>(OnCardDealt));
        _subscriptions.Add(eventBus.Subscribe<PlayerHit>(OnPlayerHit));
        _subscriptions.Add(eventBus.Subscribe<PlayerStood>(OnPlayerStood));
        _subscriptions.Add(eventBus.Subscribe<PlayerDoubledDown>(OnPlayerDoubledDown));
        _subscriptions.Add(eventBus.Subscribe<PlayerSplit>(OnPlayerSplit));
        _subscriptions.Add(eventBus.Subscribe<PlayerSurrendered>(OnPlayerSurrendered));
        _subscriptions.Add(eventBus.Subscribe<PlayerBusted>(OnPlayerBusted));
        _subscriptions.Add(eventBus.Subscribe<DealerHit>(OnDealerHit));
        _subscriptions.Add(eventBus.Subscribe<DealerHoleCardRevealed>(OnDealerHoleCardRevealed));
        _subscriptions.Add(eventBus.Subscribe<DealerBusted>(OnDealerBusted));
        _subscriptions.Add(eventBus.Subscribe<InsuranceResult>(OnInsuranceResult));
        _subscriptions.Add(eventBus.Subscribe<HandResolved>(OnHandResolved));
        _subscriptions.Add(eventBus.Subscribe<RoundComplete>(OnRoundComplete));
    }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
        _subscriptions.Clear();
    }

    private void OnBetPlaced(BetPlaced evt)
    {
        ResetRoundState();
        _roundOpen = true;
        _roundStartedUtc = DateTime.UtcNow;
        _betAmount = evt.Amount;
    }

    private void OnCardDealt(CardDealt evt)
    {
        if (!_roundOpen)
            return;

        _cardsSeen.Add(new CardSeenRecord(
            evt.Recipient,
            evt.HandIndex,
            evt.Card.Rank.ToString(),
            evt.Card.Suit.ToString(),
            evt.FaceDown));

        if (evt.Recipient == "Player")
        {
            AddPlayerCard(evt.HandIndex, evt.Card);
            return;
        }

        if (evt.Recipient == "Dealer")
        {
            _dealerCards.Add(evt.Card);
            if (!evt.FaceDown && _dealerCards.Count == 1)
                _dealerUpcard = ToUpcardLabel(evt.Card);
        }
    }

    private void OnPlayerHit(PlayerHit evt)
    {
        if (!_roundOpen)
            return;

        CaptureDecision(evt.HandIndex, "Hit");
        AddPlayerCard(evt.HandIndex, evt.Card);
        _cardsSeen.Add(new CardSeenRecord(
            "Player",
            evt.HandIndex,
            evt.Card.Rank.ToString(),
            evt.Card.Suit.ToString(),
            false));
    }

    private void OnPlayerStood(PlayerStood evt)
    {
        if (!_roundOpen)
            return;

        CaptureDecision(evt.HandIndex, "Stand");
    }

    private void OnPlayerDoubledDown(PlayerDoubledDown evt)
    {
        if (!_roundOpen)
            return;

        CaptureDecision(evt.HandIndex, "Double");
        AddPlayerCard(evt.HandIndex, evt.Card);
        _cardsSeen.Add(new CardSeenRecord(
            "Player",
            evt.HandIndex,
            evt.Card.Rank.ToString(),
            evt.Card.Suit.ToString(),
            false));
    }

    private void OnPlayerSplit(PlayerSplit evt)
    {
        if (!_roundOpen)
            return;

        CaptureDecision(evt.OriginalHandIndex, "Split");

        if (!_playerHands.TryGetValue(evt.OriginalHandIndex, out var originalCards))
        {
            _playerHands[evt.NewHandIndex] = [evt.SplitCard];
            return;
        }

        Card splitCard = evt.SplitCard;
        if (originalCards.Count > 1)
        {
            splitCard = originalCards[1];
            originalCards.RemoveAt(1);
        }

        _playerHands[evt.NewHandIndex] = [splitCard];
    }

    private void OnPlayerSurrendered(PlayerSurrendered evt)
    {
        if (!_roundOpen)
            return;

        CaptureDecision(evt.HandIndex, "Surrender");
    }

    private void OnDealerHit(DealerHit evt)
    {
        if (!_roundOpen)
            return;

        _dealerCards.Add(evt.Card);
        _cardsSeen.Add(new CardSeenRecord(
            "Dealer",
            0,
            evt.Card.Rank.ToString(),
            evt.Card.Suit.ToString(),
            false));
    }

    private void OnDealerBusted(DealerBusted evt)
    {
        if (!_roundOpen)
            return;

        _dealerBusted = true;
    }

    private void OnPlayerBusted(PlayerBusted evt)
    {
        if (!_roundOpen)
            return;

        _bustedHands.Add(evt.HandIndex);
    }

    private void OnDealerHoleCardRevealed(DealerHoleCardRevealed evt)
    {
        if (!_roundOpen)
            return;

        string rank = evt.Card.Rank.ToString();
        string suit = evt.Card.Suit.ToString();

        for (int i = _cardsSeen.Count - 1; i >= 0; i--)
        {
            var seen = _cardsSeen[i];
            if (seen.Recipient != "Dealer" || !seen.FaceDown)
                continue;
            if (!string.Equals(seen.Rank, rank, StringComparison.Ordinal))
                continue;
            if (!string.Equals(seen.Suit, suit, StringComparison.Ordinal))
                continue;

            _cardsSeen[i] = seen with { FaceDown = false };
            return;
        }

        _cardsSeen.Add(new CardSeenRecord("Dealer", 0, rank, suit, false));
    }

    private void OnInsuranceResult(InsuranceResult evt)
    {
        if (!_roundOpen)
            return;

        _insurancePayoutTotal += evt.Payout;
    }

    private void OnHandResolved(HandResolved evt)
    {
        if (!_roundOpen)
            return;

        _handResults.Add(new HandResultRecord(
            evt.HandIndex,
            evt.Outcome,
            evt.Payout,
            _bustedHands.Contains(evt.HandIndex)));

        for (int i = 0; i < _decisions.Count; i++)
        {
            if (_decisions[i].HandIndex != evt.HandIndex)
                continue;
            if (_decisions[i].ResultOutcome.HasValue)
                continue;

            _decisions[i] = _decisions[i] with
            {
                ResultOutcome = evt.Outcome,
                ResultPayout = evt.Payout
            };
        }
    }

    private void OnRoundComplete(RoundComplete evt)
    {
        if (!_roundOpen)
            return;

        var ruleFingerprint = new RuleFingerprint(
            FormatBlackjackPayout(_rules.BlackjackPayout),
            _rules.DealerHitsSoft17,
            _rules.NumberOfDecks,
            ResolveSurrenderRule());

        var round = new RoundRecord(
            PlayedAtUtc: _roundStartedUtc,
            BetAmount: _betAmount,
            NetPayout: _handResults.Sum(x => x.Payout) + _insurancePayoutTotal,
            DealerBusted: _dealerBusted,
            Rules: ruleFingerprint,
            HandResults: _handResults.ToList(),
            CardsSeen: _cardsSeen.ToList(),
            Decisions: _decisions.Select(x => x.ToRecord()).ToList());

        try
        {
            _statsRepository.RecordRound(_profileId, round);
        }
        catch (Exception ex)
        {
            AppLogger.LogError(
                nameof(StatsRecorder),
                "Failed to persist round statistics.",
                ex);
            _nonBlockingErrorNotifier?.Invoke("Stats save failed. Session analytics may be incomplete.");
        }

        _roundOpen = false;
    }

    private void CaptureDecision(int handIndex, string action)
    {
        if (!_playerHands.TryGetValue(handIndex, out var handCards))
            return;

        int value = Hand.Evaluate(handCards);
        bool isSoft = IsSoft(handCards);

        _decisions.Add(new TrackedDecision(
            handIndex,
            value,
            isSoft,
            _dealerUpcard,
            action,
            null,
            null));
    }

    private void AddPlayerCard(int handIndex, Card card)
    {
        if (!_playerHands.TryGetValue(handIndex, out var cards))
        {
            cards = [];
            _playerHands[handIndex] = cards;
        }

        cards.Add(card);
    }

    private void ResetRoundState()
    {
        _cardsSeen.Clear();
        _handResults.Clear();
        _decisions.Clear();
        _playerHands.Clear();
        _bustedHands.Clear();
        _dealerCards.Clear();
        _betAmount = 0;
        _insurancePayoutTotal = 0;
        _dealerBusted = false;
        _dealerUpcard = "?";
        _roundStartedUtc = DateTime.UtcNow;
    }

    private static bool IsSoft(IReadOnlyList<Card> cards)
    {
        int hard = 0;
        bool hasAce = false;
        foreach (var card in cards)
        {
            hard += card.PointValue;
            if (card.Rank == Rank.Ace)
                hasAce = true;
        }

        return hasAce && hard + GameConfig.AceExtraValue <= GameConfig.BustNumber;
    }

    private string ResolveSurrenderRule()
    {
        if (_rules.AllowEarlySurrender)
            return "early";
        if (_rules.AllowLateSurrender)
            return "late";
        return "none";
    }

    private static string FormatBlackjackPayout(decimal payout)
    {
        if (payout == 1.5m)
            return "3:2";
        if (payout == 1.2m)
            return "6:5";
        return payout.ToString(CultureInfo.InvariantCulture);
    }

    private static string ToUpcardLabel(Card card)
    {
        return card.Rank switch
        {
            Rank.Ace => "A",
            Rank.Ten or Rank.Jack or Rank.Queen or Rank.King => "T",
            _ => ((int)card.Rank).ToString(CultureInfo.InvariantCulture)
        };
    }

    private sealed record TrackedDecision(
        int HandIndex,
        int PlayerValue,
        bool IsSoft,
        string DealerUpcard,
        string Action,
        HandOutcome? ResultOutcome,
        decimal? ResultPayout)
    {
        public DecisionRecord ToRecord()
        {
            return new DecisionRecord(
                HandIndex,
                PlayerValue,
                IsSoft,
                DealerUpcard,
                Action,
                ResultOutcome,
                ResultPayout);
        }
    }
}
