using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBlackjack.Animation;
using MonoBlackjack.Core;
using MonoBlackjack.Core.Events;
using MonoBlackjack.Core.Players;
using MonoBlackjack.Layout;
using MonoBlackjack.Rendering;

namespace MonoBlackjack;

internal sealed class GameAnimationCoordinator
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteFont _font;
    private readonly CardRenderer _cardRenderer;
    private readonly TweenManager _tweenManager;
    private readonly SpriteLayer _cardLayer;
    private readonly SpriteLayer _uiLayer;
    private readonly Human _player;
    private readonly Dealer _dealer;
    private readonly Func<float, float> _getResponsiveScale;
    private readonly List<TrackedCardSprite> _trackedCards = [];
    private readonly HashSet<int> _bustedHands = new();
    private readonly Dictionary<int, int> _playerHandCardCounts = new();

    private int _dealCardIndex;
    private int _dealerCardCount;
    private float _dealerAnimationDelay;
    private int _activePlayerHandIndex;
    private Vector2 _deckPosition;
    private Vector2 _cardSize;

    public GameAnimationCoordinator(
        GraphicsDevice graphicsDevice,
        SpriteFont font,
        CardRenderer cardRenderer,
        TweenManager tweenManager,
        SpriteLayer cardLayer,
        SpriteLayer uiLayer,
        Human player,
        Dealer dealer,
        Func<float, float> getResponsiveScale)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _cardRenderer = cardRenderer;
        _tweenManager = tweenManager;
        _cardLayer = cardLayer;
        _uiLayer = uiLayer;
        _player = player;
        _dealer = dealer;
        _getResponsiveScale = getResponsiveScale;
        RecalculateLayout();
    }

    public int ActivePlayerHandIndex => _activePlayerHandIndex;

    public int DealerCardCount => _dealerCardCount;

    public Vector2 CardSize => _cardSize;

    public Vector2 DeckPosition => _deckPosition;

    public IReadOnlySet<int> BustedHands => _bustedHands;

    public void RecalculateLayout()
    {
        var vp = _graphicsDevice.Viewport;
        _cardSize = GameLayoutCalculator.CalculateCardSize(vp.Height);
        _deckPosition = new Vector2(-_cardSize.X, vp.Height / 2f);
    }

    public void SnapTrackedSpritesToTargets()
    {
        foreach (var tracked in _trackedCards)
        {
            tracked.Sprite.Size = _cardSize;
            tracked.Sprite.Position = GetCardTargetPosition(tracked.Recipient, tracked.HandIndex, tracked.CardIndexInHand);
            tracked.Sprite.Opacity = 1f;
            tracked.Sprite.ScaleX = 1f;
        }
    }

    public void ApplyCardBackTint(Color backTint)
    {
        foreach (var tracked in _trackedCards)
            tracked.Sprite.BackTint = backTint;
    }

    public void ClearRoundVisualState()
    {
        _cardLayer.Clear();
        _uiLayer.Clear();
        _trackedCards.Clear();
        _bustedHands.Clear();

        _dealCardIndex = 0;
        _playerHandCardCounts.Clear();
        _dealerCardCount = 0;
        _dealerAnimationDelay = 0f;
        _activePlayerHandIndex = 0;
    }

    public void OnCardDealt(CardDealt evt)
    {
        float delay = _dealCardIndex * 0.2f;
        AddAnimatedCard(evt.Card, evt.FaceDown, evt.Recipient, evt.HandIndex, delay);
    }

    public void OnPlayerTurnStarted(PlayerTurnStarted evt)
    {
        bool activeChanged = _activePlayerHandIndex != evt.HandIndex;
        _activePlayerHandIndex = evt.HandIndex;

        if (activeChanged)
            RepositionPlayerCards();
    }

    public void OnPlayerHit(PlayerHit evt)
    {
        AddAnimatedCard(evt.Card, false, _player.Name, evt.HandIndex, 0f);
    }

    public void OnPlayerDoubledDown(PlayerDoubledDown evt)
    {
        AddAnimatedCard(evt.Card, false, _player.Name, evt.HandIndex, 0f);
    }

    public void OnPlayerBusted(PlayerBusted evt)
    {
        _bustedHands.Add(evt.HandIndex);
        var pos = GetOutcomeLabelPosition(evt.HandIndex);
        CreateLabel("BUST", Color.Red, pos);
    }

    public void OnPlayerSplit(PlayerSplit evt)
    {
        for (int i = 0; i < _trackedCards.Count; i++)
        {
            var tracked = _trackedCards[i];
            if (tracked.Recipient == _player.Name
                && tracked.HandIndex == evt.OriginalHandIndex
                && tracked.CardIndexInHand == 1)
            {
                _trackedCards[i] = tracked with { HandIndex = evt.NewHandIndex, CardIndexInHand = 0 };

                if (_playerHandCardCounts.ContainsKey(evt.OriginalHandIndex))
                    _playerHandCardCounts[evt.OriginalHandIndex]--;
                _playerHandCardCounts[evt.NewHandIndex] = 1;

                break;
            }
        }

        RepositionPlayerCards();
    }

    public void OnDealerTurnStarted(DealerTurnStarted evt)
    {
        _dealerAnimationDelay = 0f;
    }

    public void OnDealerHit(DealerHit evt)
    {
        AddAnimatedCard(evt.Card, false, _dealer.Name, 0, _dealerAnimationDelay);
        _dealerAnimationDelay += 0.3f;
    }

    public void OnDealerHoleCardRevealed(DealerHoleCardRevealed evt)
    {
        for (int i = 0; i < _trackedCards.Count; i++)
        {
            var tracked = _trackedCards[i];
            if (tracked.Recipient == _dealer.Name && tracked.CardIndexInHand == 1)
            {
                var sprite = tracked.Sprite;
                _tweenManager.Add(TweenBuilder.FlipX(sprite, 0.3f, 0f,
                    onMidpoint: () => sprite.FaceDown = false));
                break;
            }
        }
    }

    public void OnInsuranceOffered(InsuranceOffered evt)
    {
        var vp = _graphicsDevice.Viewport;
        var labelY = (GetDealerCardsY() + _cardSize.Y + GetPlayerCardsY()) / 2f;
        CreateLabel("INSURANCE?", Color.Yellow, new Vector2(vp.Width / 2f, labelY));
    }

    public void OnInsuranceResult(InsuranceResult evt)
    {
        var vp = _graphicsDevice.Viewport;
        var labelY = (GetDealerCardsY() + _cardSize.Y + GetPlayerCardsY()) / 2f;
        if (evt.DealerHadBlackjack && evt.Payout > 0)
        {
            CreateLabel($"INSURANCE PAYS ${evt.Payout}", Color.Gold,
                new Vector2(vp.Width / 2f, labelY - 30f));
        }
        else if (!evt.DealerHadBlackjack && evt.Payout < 0)
        {
            CreateLabel("INSURANCE LOST", Color.Gray,
                new Vector2(vp.Width / 2f, labelY - 30f));
        }
    }

    public void OnDealerBusted(DealerBusted evt)
    {
        var vp = _graphicsDevice.Viewport;
        var bustY = GetDealerCardsY() - 20f;
        CreateLabel("BUST", Color.Red, new Vector2(vp.Width / 2f, bustY));
    }

    public void OnHandResolved(HandResolved evt)
    {
        if (evt.Outcome == HandOutcome.Lose && _bustedHands.Contains(evt.HandIndex))
            return;

        var (text, color) = evt.Outcome switch
        {
            HandOutcome.Win => ("WIN", Color.Gold),
            HandOutcome.Blackjack => ("BLACKJACK!", Color.Gold),
            HandOutcome.Lose => ("LOSE", Color.Red),
            HandOutcome.Push => ("PUSH", Color.White),
            HandOutcome.Surrender => ("SURRENDER", Color.Gray),
            _ => ("", Color.White)
        };

        if (!string.IsNullOrEmpty(text))
            CreateLabel(text, color, GetOutcomeLabelPosition(evt.HandIndex));
    }

    public int GetPlayerHandCount()
    {
        if (_player.Hands.Count > 0)
            return _player.Hands.Count;

        return _playerHandCardCounts.Count;
    }

    public int GetPlayerCardCount(int handIndex)
    {
        int trackedCount = _playerHandCardCounts.GetValueOrDefault(handIndex, 0);
        int domainCount = handIndex >= 0 && handIndex < _player.Hands.Count
            ? _player.Hands[handIndex].Cards.Count
            : 0;

        return Math.Max(1, Math.Max(trackedCount, domainCount));
    }

    public bool IsBustedHand(int handIndex)
    {
        return _bustedHands.Contains(handIndex);
    }

    public float GetDealerCardsY()
    {
        return GameLayoutCalculator.CalculateDealerCardsY(_graphicsDevice.Viewport.Height);
    }

    public float GetPlayerCardsY()
    {
        return GameLayoutCalculator.CalculatePlayerCardsY(_graphicsDevice.Viewport.Height);
    }

    public Vector2 GetCardTargetPosition(string recipient, int handIndex, int cardIndexInHand)
    {
        var vp = _graphicsDevice.Viewport;
        var halfCard = _cardSize / 2f;

        if (recipient == _dealer.Name)
        {
            int dealerTotal = Math.Max(_dealerCardCount, 2);
            return GameLayoutCalculator.ComputeRowCardCenter(
                vp.Width,
                _cardSize,
                dealerTotal,
                cardIndexInHand,
                GetDealerCardsY() + halfCard.Y);
        }

        int totalHands = GetPlayerHandCount();
        float centerY = GetPlayerCardsY() + halfCard.Y;

        if (totalHands <= 1)
        {
            int cardCount = GetPlayerCardCount(0);
            return GameLayoutCalculator.ComputeRowCardCenter(
                vp.Width,
                _cardSize,
                cardCount,
                cardIndexInHand,
                centerY);
        }

        var handCardCounts = new int[totalHands];
        for (int i = 0; i < totalHands; i++)
            handCardCounts[i] = GetPlayerCardCount(i);

        return GameLayoutCalculator.ComputeAdaptiveMultiHandCardCenter(
            vp.Width,
            _cardSize,
            handCardCounts,
            handIndex,
            cardIndexInHand,
            centerY,
            _activePlayerHandIndex);
    }

    private void AddAnimatedCard(Card card, bool faceDown, string recipient, int handIndex, float delay)
    {
        var sprite = _cardRenderer.CreateCardSprite(card, faceDown);
        sprite.Size = _cardSize;
        sprite.Position = _deckPosition;
        sprite.Opacity = 0f;
        sprite.ZOrder = _dealCardIndex;

        int recipientCardIndex;
        if (recipient == _dealer.Name)
        {
            recipientCardIndex = _dealerCardCount++;
        }
        else
        {
            if (!_playerHandCardCounts.ContainsKey(handIndex))
                _playerHandCardCounts[handIndex] = 0;
            recipientCardIndex = _playerHandCardCounts[handIndex]++;
        }

        _trackedCards.Add(new TrackedCardSprite(sprite, recipient, handIndex, recipientCardIndex));

        var target = GetCardTargetPosition(recipient, handIndex, recipientCardIndex);
        float duration = 0.4f;

        _tweenManager.Add(TweenBuilder.MoveTo(sprite, target, duration, delay, Easing.EaseOutQuad));
        _tweenManager.Add(TweenBuilder.FadeTo(sprite, 1f, duration * 0.5f, delay, Easing.Linear));
        RepositionRecipientCards(recipient, duration: 0.25f, delay: 0f, excludeSprite: sprite);

        _cardLayer.Add(sprite);
        _dealCardIndex++;
    }

    private TextSprite CreateLabel(string text, Color color, Vector2 position)
    {
        var targetScale = _getResponsiveScale(1f);
        var label = new TextSprite
        {
            Text = text,
            Font = _font,
            TextColor = color,
            Position = position,
            Opacity = 0f,
            Scale = targetScale * 0.5f,
            ZOrder = 100
        };

        _uiLayer.Add(label);
        _tweenManager.Add(TweenBuilder.FadeTo(label, 1f, 0.3f));
        _tweenManager.Add(TweenBuilder.ScaleTo(label, targetScale, 0.3f, 0f, Easing.EaseOutBack));

        return label;
    }

    private Vector2 GetOutcomeLabelPosition(int handIndex = 0)
    {
        var vp = _graphicsDevice.Viewport;

        if (GetPlayerHandCount() <= 1)
        {
            float dealerBottom = GetDealerCardsY() + _cardSize.Y;
            float playerTop = GetPlayerCardsY();
            float midY = (dealerBottom + playerTop) / 2f;
            return new Vector2(vp.Width / 2f, midY);
        }

        int cardCount = GetPlayerCardCount(handIndex);
        var firstCardCenter = GetCardTargetPosition(_player.Name, handIndex, 0);
        var lastCardCenter = GetCardTargetPosition(_player.Name, handIndex, cardCount - 1);
        float centerX = (firstCardCenter.X + lastCardCenter.X) / 2f;
        float labelY = GetPlayerCardsY() - 30f;

        return new Vector2(centerX, labelY);
    }

    private void RepositionRecipientCards(string recipient, float duration, float delay, CardSprite? excludeSprite = null)
    {
        foreach (var tracked in _trackedCards)
        {
            if (tracked.Recipient != recipient)
                continue;
            if (excludeSprite != null && ReferenceEquals(tracked.Sprite, excludeSprite))
                continue;

            var target = GetCardTargetPosition(tracked.Recipient, tracked.HandIndex, tracked.CardIndexInHand);
            _tweenManager.Add(TweenBuilder.MoveTo(tracked.Sprite, target, duration, delay, Easing.EaseOutQuad));
        }
    }

    private void RepositionPlayerCards()
    {
        RepositionRecipientCards(_player.Name, duration: 0.3f, delay: 0f);
    }

    private readonly record struct TrackedCardSprite(CardSprite Sprite, string Recipient, int HandIndex, int CardIndexInHand);
}
