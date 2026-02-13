using MonoBlackjack.Core.Events;

namespace MonoBlackjack.Events;

/// <summary>
/// Queues domain events during game logic, flushes to typed handlers during Update().
/// This decouples domain operations from rendering reactions and allows staggered animation delays.
/// </summary>
public class EventBus
{
    private readonly Queue<GameEvent> _queue = new();
    private readonly Dictionary<Type, List<Action<GameEvent>>> _handlers = new();

    public void Subscribe<T>(Action<T> handler) where T : GameEvent
    {
        var type = typeof(T);
        if (!_handlers.TryGetValue(type, out var list))
        {
            list = [];
            _handlers[type] = list;
        }
        list.Add(e => handler((T)e));
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
                foreach (var handler in list)
                    handler(evt);
            }
        }
    }

    public void Clear()
    {
        _queue.Clear();
        _handlers.Clear();
    }
}
