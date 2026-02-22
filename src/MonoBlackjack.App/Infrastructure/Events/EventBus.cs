using MonoBlackjack.Core.Events;

namespace MonoBlackjack.Infrastructure.Events;

/// <summary>
/// Queues domain events during game logic, flushes to typed handlers during Update().
/// This decouples domain operations from rendering reactions and allows staggered animation delays.
/// </summary>
public class EventBus
{
    private readonly Queue<GameEvent> _queue = new();
    private readonly Dictionary<Type, List<SubscriptionToken>> _handlers = new();

    public IDisposable Subscribe<T>(Action<T> handler) where T : GameEvent
    {
        var type = typeof(T);
        if (!_handlers.TryGetValue(type, out var list))
        {
            list = [];
            _handlers[type] = list;
        }

        var token = new SubscriptionToken(this, type, e => handler((T)e));
        list.Add(token);
        return token;
    }

    private void Unsubscribe(Type eventType, SubscriptionToken token)
    {
        if (!_handlers.TryGetValue(eventType, out var list))
            return;

        list.Remove(token);

        if (list.Count == 0)
            _handlers.Remove(eventType);
    }

    public void Publish(GameEvent evt)
    {
        _queue.Enqueue(evt);
    }

    public void Flush()
    {
        while (_queue.Count > 0)
        {
            var evt = _queue.Dequeue();
            var type = evt.GetType();
            if (_handlers.TryGetValue(type, out var list))
            {
                // Copy to avoid modification during iteration
                foreach (var token in list.ToList())
                    token.Handler(evt);
            }
        }
    }

    public void Clear()
    {
        _queue.Clear();
        _handlers.Clear();
    }

    private sealed class SubscriptionToken : IDisposable
    {
        private EventBus? _bus;
        private readonly Type _eventType;
        public readonly Action<GameEvent> Handler;

        public SubscriptionToken(EventBus bus, Type eventType, Action<GameEvent> handler)
        {
            _bus = bus;
            _eventType = eventType;
            Handler = handler;
        }

        public void Dispose()
        {
            _bus?.Unsubscribe(_eventType, this);
            _bus = null;
        }
    }
}
