namespace MonoBlackjack.Animation;

/// <summary>
/// A single animation that interpolates a value from 0 to 1 over a duration,
/// applying an easing function, with optional delay and completion callback.
/// </summary>
public class Tween
{
    private readonly float _duration;
    private readonly float _delay;
    private readonly Func<float, float> _ease;
    private readonly Action<float> _apply;
    private readonly Action? _onComplete;
    private float _elapsed;
    private bool _started;

    public bool IsComplete { get; private set; }

    public Tween(float duration, float delay, Func<float, float> ease, Action<float> apply, Action? onComplete = null)
    {
        _duration = duration;
        _delay = delay;
        _ease = ease;
        _apply = apply;
        _onComplete = onComplete;
    }

    public void Update(float deltaSeconds)
    {
        if (IsComplete)
            return;

        _elapsed += deltaSeconds;

        if (_elapsed < _delay)
            return;

        if (!_started)
        {
            _started = true;
            _apply(_ease(0f));
        }

        float active = _elapsed - _delay;
        float t = Math.Clamp(active / _duration, 0f, 1f);
        _apply(_ease(t));

        if (t >= 1f)
        {
            IsComplete = true;
            _onComplete?.Invoke();
        }
    }
}
